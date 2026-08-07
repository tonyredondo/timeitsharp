# Plan detallado de remediación — PR #77

## 1. Objetivo y alcance

Resolver los hallazgos de la revisión independiente del commit `ac096e1a597fddd10e1baec76430273f36e0b6b6` antes de solicitar una nueva revisión del PR #77.

El plan debe conservar:

- Compatibilidad con `net6.0`–`net10.0` y SDK `10.0.300`.
- El paquete estable `Datadog.Trace.BenchmarkDotNet` v2.61.0.
- La prohibición de mezclar APIs o assets incompatibles de Datadog v3.
- Las etiquetas completas de los spans Datadog.
- `AnsiConsole.WriteException(ex)` para mostrar excepciones de exportación.
- El PR en estado draft hasta que pasen CI, las pruebas de empaquetado y una revisión externa.

> Este documento describe el trabajo pendiente; no implica que los hallazgos estén corregidos.

## 2. Hallazgos y prioridad

| Prioridad | Área | Resultado actual | Archivos principales |
|---|---|---|---|
| P1 | Datadog Profiler Linux | Se exige `Datadog.Linux.ApiWrapper.x64.so`, pero el paquete v2.61.0 no lo contiene; el profiler queda deshabilitado. | `src/TimeItSharp.Common/Services/DatadogProfilerService.cs` |
| P1 | CLI | Una línea de comando cuyo texto termina en `.json` se interpreta como configuración. | `src/TimeItSharp/Program.cs` |
| P1 | Secretos | Se filtran aliases como `GITHUB_PAT`, `JWT` y `DATABASE_URL`; argumentos y tags se exportan sin redacción. | `Utils.cs`, `JsonExporter.cs`, `DatadogExporter.cs` |
| P2 | Entorno | `ScenarioProcessor` conserva un snapshot estático del entorno y no observa cambios posteriores. | `ScenarioProcessor.cs` |
| P2 | Lifecycle | Una cancelación devuelve sin ejecutar `ScenarioFinish`. | `ScenarioProcessor.cs`, `TimeItEngine.cs` |
| P2 | Extensiones | Hay `NullReferenceException` en el builder y aliases que el engine no resuelve. | `ConfigBuilder.cs`, `TimeItEngine.cs` |
| P2 | NuGet/single-file | Consumidores de `TimeItSharp.Common` pueden perder StartupHook y assets nativos. | proyectos `.csproj`, `ScenarioProcessor.cs` |
| P2 | CI/trimming | Las pruebas no cubren todos los TFMs y la supresión de warnings oculta problemas de trimming. | `.github/workflows/ci.yml`, sample `.csproj` |
| P2 condicional | Compatibilidad de configuración | `metricsFrequencyInMs: 0` y timeout negativo cambian de semántica al validar. | `Config.cs`, `ScenarioProcessor.cs` |
| P3 | Robustez de salida | PATH sensible a mayúsculas en Unix, markup sin escapar y recursos temporales/CTS sin cierre completo. | `ScenarioProcessor.cs`, `ConsoleExporter.cs` |

## 3. Fase 0 — Congelar la reproducción y ampliar la cobertura

### 3.1 Crear pruebas de regresión antes de editar el código

Añadir casos que fallen en el estado actual y que queden asociados a cada hallazgo:

1. **CLI**
   - `echo hello.json` como comando completo.
   - `echo --config foo.json` y `echo --config=foo.json` como argumentos.
   - Un ejecutable existente cuyo nombre termine en `.json`.
   - Una configuración existente `.json` que siga cargándose como configuración.
   - Una configuración inexistente `.json` sin confundirla con una línea de comando con espacios.

2. **Redacción**
   - Variables `GITHUB_PAT`, `JWT`, `DATABASE_URL`, `AWS_ACCESS_KEY_ID`, `API_KEY` y `PATH`.
   - Argumentos `--password secret`, `--token=secret` y connection strings.
   - Tags `api_token`, `client_secret` y tags normales.
   - Verificar tanto JSON como Datadog usando una abstracción o fake del cliente, sin enviar datos reales.
   - Verificar que la redacción no muta el `ScenarioResult` original.

3. **Datadog Profiler**
   - Enumerar los assets que realmente contiene el `.nupkg` v2.61.0.
   - Verificar que el profiler no se marque como disponible si falta un archivo obligatorio.
   - Smoke test Linux x64, Linux arm64 si el runner está disponible y Linux musl si se declara soporte.

4. **Entorno**
   - Ejecutar dos veces `TimeItEngine.RunAsync` en el mismo proceso, cambiando una variable entre ejecuciones.
   - Cambiar una variable desde un callback antes de ejecutar el proceso.
   - Comprobar que el PATH añadido a un comando no modifica el entorno del host ni se queda en la siguiente ejecución.

5. **Lifecycle**
   - Cancelar durante warmup y durante run.
   - Verificar la relación `ScenarioStart`/`ScenarioFinish` y que se ejecuten `AfterAll` y `Finish` una sola vez.
   - Verificar que `ParentService` se limpie incluso cuando se cancela durante un extra run.

6. **Extensiones**
   - `Exporters = null`, listas con elementos `null` y llamadas a `WithJsonExporterPath`.
   - Repetir `WithAssertor<DefaultAssertor>()` con `{ name: "DefaultAssertor" }` existente.
   - Repetir `WithService<NoopService>()` con `{ name: "NoopService" }` existente.
   - Cargar aliases `Console`, `Json`, `Datadog`, `JsonExporter` y `DatadogProfiler`.

### 3.2 Mantener una baseline

Registrar para cada prueba:

- comando exacto;
- TFM y sistema operativo;
- código de salida;
- mensaje observado;
- archivos generados y si fueron eliminados;
- resultado esperado después de la corrección.

## 4. Fase 1 — Corregir CLI, configuración y seguridad

### 4.1 Separar detección de configuración y parsing de comandos

En `Program.cs`:

1. No usar `Path.GetExtension(argumentValue)` sobre toda la línea.
2. Distinguir primero una ruta de configuración de una línea de comando:
   - si el argumento completo apunta a un archivo existente, inspeccionar JSON;
   - si contiene varios tokens, parsearlo como comando antes de decidir que un sufijo `.json` es configuración;
   - conservar un error claro para una ruta de configuración inexistente introducida como argumento único.
3. Mantener soporte para rutas entrecomilladas con espacios.
4. Extraer `ParseProcessCommand`, la detección de configuración y la validación de quotes a código testeable desde xUnit.
5. Añadir pruebas de compatibilidad para opciones antes y después de `--`, sin permitir que el argumento del proceso sea consumido por `System.CommandLine`.

**Criterio de aceptación:** todos los comandos `.json` anteriores llegan a `Cmd:` y se ejecutan; los JSON válidos siguen entrando por `Config.LoadConfiguration`; los JSON malformados no se ejecutan como binarios.

### 4.2 Definir un sanitizador único de metadata

En `Utils` o una clase de seguridad dedicada:

1. Normalizar nombres ignorando separadores y mayúsculas.
2. Cubrir aliases documentados y habituales: `PAT`, `JWT`, `SAS`, `PASSWORD`, `PASSWD`, `PWD`, `SECRET`, `TOKEN`, `API_KEY`, `APP_KEY`, `ACCESS_KEY`, `PRIVATE_KEY`, `CREDENTIAL`, `AUTH`, `CONNECTION_STRING` y equivalentes.
3. Evitar que la lógica quede duplicada entre JSON y Datadog.
4. Redactar variables de entorno por nombre.
5. Definir explícitamente la política para:
   - `processArguments` y `processName`;
   - tags y custom metrics;
   - errores y stdout/stderr, que pueden contener secretos.
6. Como mínimo, si un tag tiene una clave sensible, emitir `[REDACTED]`; para argumentos, ocultar valores asociados a flags sensibles o no exportar el argumento completo si no es posible sanearlo de forma segura.
7. No registrar valores secretos en errores de exportación ni en pruebas.

**Criterio de aceptación:** ningún valor de los casos de prueba aparece en el JSON ni en los tags Datadog; las etiquetas de spans no sensibles se conservan completas.

### 4.3 Revisar la validación compatible

Decidir y documentar la semántica de valores especiales:

- Si `metricsFrequencyInMs == 0` significa “usar el default de 200 ms”, normalizarlo a 200 antes de validar o permitirlo.
- Si valores negativos de timeout son un sentinel de herencia, conservar esa semántica; de lo contrario, rechazar negativos y cambiar `PrepareScenario` para no sugerir que son válidos.
- Mantener la validación estricta para NaN, infinito, count inválido, intervalos estadísticos imposibles y colecciones nulas.
- Añadir pruebas de configuración válida, inválida y mensajes agregados.

## 5. Fase 2 — Reparar Datadog y el empaquetado nativo

### 5.1 Elegir una estrategia compatible con Datadog v2

Antes de modificar el código, comparar el contrato de `Datadog.Trace.BenchmarkDotNet 2.61.0` con sus assets reales.

Opciones permitidas:

1. Usar únicamente los archivos que entrega el paquete v2 y no exigir `Datadog.Linux.ApiWrapper.x64.so` si no forma parte de ese contrato.
2. Incluir explícitamente assets v2 compatibles, verificando licencias, nombres, arquitectura y runtime.
3. Si una funcionalidad requiere obligatoriamente un asset de v3, deshabilitarla con un diagnóstico explícito en vez de mezclar paquetes v2/v3.

No aceptar una solución que simplemente copie assets de `Datadog.Trace.Bundle` v3 junto con APIs v2 sin comprobar el ABI y las dependencias.

### 5.2 Corregir resolución de RID y archivos

En `DatadogProfilerService`:

- Linux x64 debe elegir `linux-x64`.
- Linux arm64 debe elegir `linux-arm64` y no un wrapper x64.
- Detectar Linux musl y elegir `linux-musl-x64` cuando el paquete lo soporte.
- Configurar `LD_PRELOAD` únicamente cuando el archivo exista y sea compatible.
- Validar que `loader.conf` referencia nombres realmente presentes en el paquete.
- Emitir un mensaje que indique exactamente qué asset falta y qué RID se seleccionó.
- Separar “assets encontrados” de “profiler efectivamente adjuntado”; `_isEnabled` no debe afirmar éxito únicamente porque se construyó un diccionario de entorno.

### 5.3 Añadir una verificación de paquete

Crear un script o test de packaging que:

- desempaque `TimeItSharp.0.4.8.nupkg`;
- compruebe los assets Datadog esperados por RID;
- falle si el código exige un archivo que no está en el paquete;
- compruebe que no aparece una dependencia v3 no permitida;
- ejecute un proceso instrumentado y valide la señal observable del profiler.

## 6. Fase 3 — Corregir entorno y lifecycle del engine

### 6.1 Sustituir el snapshot estático de entorno

En `ScenarioProcessor`:

1. Obtener `Environment.GetEnvironmentVariables()` por ejecución o por `RunAsync`, según la semántica elegida.
2. Copiarlo siempre a un diccionario nuevo antes de añadir variables del escenario.
3. No modificar `Environment` global para cambiar PATH.
4. Mantener las variables de servicio/callback que se hayan establecido antes de ejecutar el comando.
5. Añadir una prueba de dos ejecuciones en el mismo proceso.

### 6.2 Hacer el lifecycle cancelable y simétrico

En `ProcessScenarioAsync` y `TimeItEngine`:

- envolver la ejecución de un escenario en un `try/finally`;
- si `ScenarioStart` se completó y la ejecución se cancela, generar un `ScenarioResult` fallido o un resultado cancelado con los datapoints parciales;
- invocar `ScenarioFinish` exactamente una vez;
- limpiar `ParentService` siempre;
- conservar `AfterAll` y `Finish` como callbacks únicos y ordenados;
- decidir si los exporters deben recibir resultados parciales al cancelar y documentarlo;
- distinguir excepciones de callbacks de cancelación para no perder cleanup.

Añadir pruebas con un service que cuente cada callback y que cancele durante warmup, run y extra run.

### 6.3 Cerrar recursos de ejecución

Auditar `CancellationTokenSource`, timers y archivos temporales:

- disponer `cmdCts`, `timeoutCts` y cualquier linked CTS mediante `using`/`try/finally`;
- esperar correctamente la finalización de la tarea de timeout;
- garantizar la eliminación del archivo de métricas si falla `ExecutionStart`, `ExecuteAssertions`, el comando o `ExecutionEnd`;
- conservar la protección contra symlinks y evitar comprobaciones TOCTOU siempre que sea posible.

## 7. Fase 4 — Resolver extensiones y robustez de exporters

### 7.1 Centralizar resolución de extensiones

Crear una función común para:

- validar entradas nulas;
- resolver `InMemoryType`, `FilePath + Type` y `Name`;
- mapear aliases canónicos (`Console`, `ConsoleExporter`, etc.) a tipos built-in;
- aplicar comparación case-insensitive sólo donde sea parte del contrato;
- producir errores accionables sin `MissingMethodException` ni `Sequence contains no elements`.

Aplicarla tanto al builder como a `TimeItEngine.GetFromAssemblyLoadInfoList`.

### 7.2 Endurecer `ConfigBuilder`

- Proteger `WithJsonExporterPath` frente a lista nula y elementos nulos.
- Hacer idempotentes assertors y services por tipo, nombre y alias.
- Validar argumentos `Type`, builder y arrays nulos con excepciones claras o no-op documentado.
- Evitar que el builder oculte una configuración inválida que después no pueda ejecutar.

### 7.3 Exporters y consola

- Escapar nombres de escenario, tags y rutas antes de interpolarlos en markup Spectre.
- Manejar duraciones finitas extremas y rangos que desborden al crear histogramas.
- Validar arrays y filas de `Overheads` nulos o incompletos.
- Mantener filtros de NaN/infinito en todas las rutas, incluyendo tags y medidas Datadog vacías.
- No dejar que un escenario malformado impida exportar los demás.

## 8. Fase 5 — Corregir consumo NuGet, single-file y trimming

### 8.1 StartupHook para consumidores de `TimeItSharp.Common`

Decidir cómo debe funcionar `TimeItSharp.Common` en un consumidor single-file:

- incluir un `.props`/`.targets` de NuGet que preserve `TimeItSharp.StartupHook` fuera del bundle;
- o empaquetar/cargar el hook mediante un mecanismo compatible con single-file;
- o documentar y validar formalmente que el escenario no está soportado.

No aceptar que el único indicio sea `Startup hook location is empty.` mientras `enableMetrics` sigue activo.

Añadir un consumer test que:

1. restaure únicamente desde el `.nupkg`;
2. publique single-file y trimmed;
3. ejecute una configuración con métricas;
4. compruebe que existe y se procesa el archivo de métricas.

### 8.2 Assets nativos para la librería

Si `DatadogProfilerService` es parte del API soportado de `TimeItSharp.Common`, el paquete debe entregar o declarar correctamente todos los assets necesarios. Verificar cada TFM/RID en un consumidor limpio, no sólo en el árbol de solución.

### 8.3 Hacer útil la validación de trimming

- Retirar `IL2026`/`IL2104` de `NoWarn` globales.
- Justificar individualmente warnings inevitables.
- Mantener `RequiresUnreferencedCode` en las APIs de carga reflectiva.
- Añadir anotaciones/dynamic dependencies sólo para tipos built-in realmente requeridos.
- Ejecutar publish trimmed con warnings tratados como error cuando sea viable.
- Separar el smoke test de reflexión del test de compatibilidad trimming.
- Si AOT no está soportado por el `netcoreapp3.1` StartupHook, documentarlo explícitamente y no marcar `IsAotCompatible` como promesa general sin matices.

## 9. Fase 6 — CI, matriz y release

Actualizar `.github/workflows/ci.yml` para que:

1. Ejecute tests en los TFMs soportados o cambie el proyecto de tests a una matriz real.
2. Publique y ejecute una configuración Datadog, no sólo `--version`.
3. Incluya smoke tests Linux x64, musl y arm64 cuando haya runners disponibles.
4. Pruebe el CLI con comandos que tengan argumentos `.json`.
5. Restaure y consuma los paquetes desde un feed limpio.
6. Ejecute el consumer single-file de `TimeItSharp.Common`.
7. No suprima warnings de trimming sin una justificación localizada.
8. Mantenga la auditoría de vulnerabilidades visible.

Sobre `GHSA-38wr-vpc7`:

- mantener la allowlist sólo si la dependencia v2 es una decisión aprobada;
- documentar el riesgo, alcance y ausencia de parche compatible v2;
- registrar la decisión de seguridad y revisarla antes de publicar;
- no ocultar el warning ni ampliar la allowlist a otros advisories.

Antes de release:

- decidir si se incrementa la versión en `src/Directory.Build.props`;
- evitar publicar una corrección funcional con una versión ya distribuida;
- actualizar el comando de instalación de la tool y las pruebas de paquete al nuevo número.

## 10. Orden recomendado de implementación

1. Añadir las pruebas de reproducción de Fase 0.
2. Corregir la detección CLI `.json`.
3. Resolver la estrategia Datadog v2 y eliminar el bloqueo del profiler Linux.
4. Aplicar redacción centralizada y añadir pruebas de seguridad.
5. Corregir snapshot de entorno y lifecycle de cancelación.
6. Arreglar builder/resolver de extensiones.
7. Corregir empaquetado Common, StartupHook, assets nativos y single-file.
8. Endurecer exporters, consola y cleanup de recursos.
9. Ajustar validación de compatibilidad y documentar decisiones.
10. Ampliar CI, quitar supresiones injustificadas y ejecutar consumer tests.
11. Ejecutar la validación final y solicitar revisión externa manteniendo el PR en draft.

## 11. Definición de terminado

El PR puede pasar a revisión final cuando se cumpla todo lo siguiente:

- Ningún P1/P2 de la tabla permanece sin una decisión documentada.
- Los comandos `.json` funcionan y los JSON malformados siguen produciendo errores claros.
- Las pruebas de redacción no encuentran secretos en JSON ni Datadog.
- El profiler Datadog v2 funciona o informa explícitamente una limitación soportada en cada RID.
- `ScenarioStart` y `ScenarioFinish` son simétricos bajo cancelación.
- Las ejecuciones repetidas observan el entorno correcto.
- Builder y engine resuelven los mismos aliases y entradas inválidas de forma consistente.
- Consumer tests de NuGet, single-file y trimmed pasan.
- Build, tests, samples net6–net10, `git diff --check`, pack, instalación de tool y smoke tests pasan.
- La auditoría de vulnerabilidades muestra únicamente el advisory v2 explícitamente aceptado.
- La rama queda limpia y se obtiene una revisión externa antes de marcar el PR como ready.

## 12. Comandos de validación final

```bash
dotnet restore
dotnet build TimeItSharp.sln -c Release --no-restore
dotnet test TimeItSharp.sln -c Release --no-build --no-restore
dotnet run --project test/TimeItSharp.FluentConfiguration.Sample -c Release --framework net6.0
dotnet run --project test/TimeItSharp.FluentConfiguration.Sample -c Release --framework net8.0
dotnet run --project test/TimeItSharp.FluentConfiguration.Sample -c Release --framework net10.0
dotnet publish test/TimeItSharp.FluentConfiguration.Sample/TimeItSharp.FluentConfiguration.Sample.csproj \
  -c Release --framework net8.0 --self-contained false -p:PublishTrimmed=true

dotnet list TimeItSharp.sln package --vulnerable --include-transitive --no-restore
dotnet pack TimeItSharp.sln -c Release -o artifacts
git diff --check
git status --short
```

Para las pruebas de consumidor, usar un directorio temporal y un feed que contenga únicamente los `.nupkg` generados por esta revisión.


## 13. Revisión de segundo pase del plan y del diff

Esta sección se añadió después de revisar de nuevo `git diff main...HEAD`, ejecutar reproducciones
contra `ac096e1`, inspeccionar los tres paquetes generados y auditar las rutas de ejecución caliente.
Sus decisiones y criterios prevalecen sobre cualquier frase anterior que resulte más amplia o
ambigua. El hecho de que las 31 pruebas actuales pasen no invalida estas reproducciones: varias
rutas no tienen un seam testeable todavía y otras sólo se ejecutan en Linux o en un consumidor
NuGet limpio.

### 13.1 Bloqueadores que siguen reproducibles en `HEAD`

| Prioridad | Reproducción/evidencia | Corrección que debe entrar en el plan |
|---|---|---|
| P1 | `Program.cs` calcula `Path.GetExtension(argumentValue)` sobre toda la línea. `Path.GetExtension("echo hello.json")` es `.json`; por tanto sigue intentando cargar `echo hello.json` como configuración. | Parsear el primer token antes de clasificar y definir precedencia explícita para `--`/`--command`/`--config`; añadir una prueba de proceso real, no sólo de la función de extensión. |
| P1 | En v2.61.0 no existe `Datadog.Linux.ApiWrapper.x64.so`. El chequeo nuevo de `GetProfilerPaths` lo exige y el servicio termina anunciando que no pudo adjuntar el profiler. Además, el contrato v2 usa `DD_NATIVELOADER_CONFIGFILE`/`loader.conf`; no se debe inventar `LD_PRELOAD` con un ABI de otra versión. | Auditar el contrato firmado v2, eliminar `LD_PRELOAD`/ApiWrapper para v2.61.0 salvo una integración ABI separada y validada, comprobar todas las filas de `loader.conf` y emitir “RID/asset no soportado” sin afirmar attach. |
| P1 | El sanitizador no detecta `GITHUB_PAT`, `JWT`, `SAS` ni `DATABASE_URL`; sólo se aplica a algunas variables de entorno. Argumentos, tags, errores, stdout, nombre de configuración, `Environment.CommandLine` usado por `TestSession` y metadata de CI siguen siendo superficies de fuga. | Un único sanitizador recursivo y pruebas de sink para JSON y Datadog. Elegir allowlist de variables exportables o redacción por nombre **y valor**; no prometer “ningún secreto” usando sólo una denylist. |
| P1 funcional | `Name: "Json"` (y aliases equivalentes) puede pasar la validación pero el resolver compara sólo `candidate.Name` exacto; el engine falla con `Could not create IExporter extension 'Json'`. El builder acepta/considera aliases distintos a los que carga el engine. | Resolver/canonicalizador único, usado por builder, CLI y engine, con aliases built-in explícitos y comparación documentada. |
| P1 de observabilidad | `DatadogExporter` crea `TestSession` durante `Initialize`, pero el engine inicializa exporters después de ejecutar escenarios. `_startDate` queda antes de la ejecución y la sesión empieza después; el span padre puede comenzar después de sus tests y omite el benchmark. | Inicializar la sesión habilitada antes de `BeforeAll`/del primer proceso, sin inicializar exporters dos veces. Separar configuración de entorno hijo de creación de sesión. |
| P1 de contrato | El plan exige conservar `AnsiConsole.WriteException(ex)`, pero el diff reemplaza llamadas en `Program`, `TimeItEngine`, `JsonExporter` y `Utils` por `WriteLine(ex.ToString())`. | Restaurar `WriteException` en todas las rutas requeridas y aplicar redacción antes de imprimir; añadir una aserción o sink de consola que detecte la ruta usada. |

El constructor de `DatadogExporter` también hace `Environment.SetEnvironmentVariable
("DD_CIVISIBILITY_LOGS_ENABLED", "true")` siempre que el tipo default se instancia, incluso con
Datadog deshabilitado. Es una mutación global que contamina ejecuciones posteriores y el snapshot
de entorno de `ScenarioProcessor`. Debe desaparecer: el valor se añade al diccionario del hijo
sólo cuando Datadog está habilitado, o se restaura exactamente en un `finally`.

### 13.2 Invariante de colecciones y auditoría de `ConfigBuilder`

Las propiedades de `Config`/`ProcessData` están anotadas como no-nullable, pero sus setters y
`System.Text.Json` permiten asignar `null`. El builder mezcla actualmente:

- guards `is null` que convierten una llamada en no-op silencioso;
- `?.Clear()` que oculta una configuración inválida;
- `?? Enumerable.Empty<T>()` después de haber comprobado null;
- `!` inmediatamente antes de `Add`/`AddRange`;
- métodos sin guard (`WithJsonExporterPath`, escenarios, environment, tags y timeout);
- `params` que permiten array nulo o elementos nulos;
- overloads `Type` que dereferencian un `Type` nulo.

Esto no es una optimización útil del benchmark: el builder se ejecuta una vez. Sí es un problema
de contrato y permite que una opción CLI se pierda sin error. En cambio, los guards de
`_assertors` y elementos nulos dentro de `ScenarioProcessor` sí están en cada datapoint y deben
eliminarse después de normalizar.

Antes de editar hay que escoger **una** semántica, no combinar ambas:

1. **Null inválido (recomendado si se conserva la validación agregada actual):** ejecutar una
   validación estructural única en el ingreso al builder/engine, producir un error indexado y no
   convertir una llamada del builder en no-op. Después de ese límite, usar listas no-nullable,
   sin `?.`, `??` ni `!`; `RunAsync` debe validar antes de `Clone`. Un objeto que el consumidor
   muta a null después de `Build` vuelve a ser inválido y debe fallar en el siguiente límite.
2. **Null equivalente a omitido:** normalizar en setters/DTO de deserialización con `?? new`,
   incluidos `Scenarios`, `Exporters`, `Assertors`, `Services`, diccionarios, listas y `Timeout`.
   Entonces se eliminan los errores “collection cannot be null” y se actualizan las pruebas; no
   debe seguir existiendo una validación que pretenda detectar un null que ya fue convertido.

La implementación no debe seguir con guards repetidos y no-op silenciosos. `ConfigBuilder(Config)`
además debe hacer `ThrowIfNull`, y todos los overloads deben aplicar una política uniforme:
argumento nulo => `ArgumentNullException`/`ArgumentException` (o una normalización documentada),
arrays `params` validados, elementos no nulos y nombres no vacíos.

La identidad de una extensión debe centralizarse y probarse: `InMemoryType`, ruta/assembly,
tipo, alias/nombre y, si cambia el comportamiento, `Options`. No se deben colapsar dos entradas
con distinto `Options`, ni dos tipos con el mismo nombre completo provenientes de assemblies
distintos. Una entrada debe declarar exactamente un selector (`InMemoryType`, `FilePath + Type` o
`Name`), salvo una combinación explícitamente soportada; `Type` solo, `Name + FilePath` ambiguo y
ruta sin `Type` deben producir errores con índice. Las rutas relativas deben resolverse respecto a
`Config.Path`, no al CWD accidental.

### 13.3 Lifecycle, cancelación, timeout y resultados parciales

El arreglo no puede limitarse a comprobar `IsCancellationRequested` en tres returns:

- `ScenarioStart`, `ExecutionStart`, `ExecutionEnd`, assertors y callbacks de extra-run pueden
  lanzar; hoy `ExecutionStart` está fuera del `try` de comando y una excepción puede saltarse
  `ExecutionEnd`, la eliminación de métricas y `ScenarioFinish`.
- La cancelación durante warmup/run/extra-run devuelve `null` y no emite `ScenarioFinish`.
- Un token ya cancelado todavía puede ejecutar `BeforeAll`/`ScenarioStart`; se debe decidir si se
  suprimen callbacks o se emite un resultado `Cancelled`.
- `TimeItEngine` marca `callbacksStarted` antes de que termine `BeforeAll` y puede invocar
  `AfterAll` cuando `BeforeAll` falló. Debe modelar estados `BeforeAllCompleted`,
  `ScenarioStarted`, `ScenarioFinished` y `AfterAllCompleted`.
- Cada escenario iniciado debe tener un `try/finally`, `ScenarioFinish` exactamente una vez,
  `ParentService = null` siempre y un resultado parcial/cancelado con código de salida definido.
  Los callbacks posteriores deben continuar aunque uno falle, pero la excepción y el estado de
  salida deben conservarse.
- `RunCommandTimeoutAsync` es fire-and-forget; después del `Task.Delay` usa sólo el token de la
  aplicación, no el `timeoutCts`, y su `finally` llama cancelación incluso cuando la tarea terminó
  normalmente o fue cancelada. Hay que enlazar el token también al comando auxiliar, esperarlo en
  todos los caminos y cancelar el target sólo cuando el timeout realmente venció.
- Disponer `cmdCts`, `timeoutCts`, tareas y archivos en un único scope. La ruta de métricas se crea
  antes de `ExecutionStart`; cualquier excepción de callback debe pasar por la limpieza. La
  protección contra symlink debe considerarse junto al riesgo TOCTOU, no como garantía absoluta.

Hay dos fallos estadísticos que no puede reparar un exporter: después de `RemoveOutliers`,
`newDurations` puede quedar vacío y `Maximum`/`Median` fallar; una lista de métricas también puede
quedar vacía cuando todos los valores se eliminan y luego se llama `Mean`/`Maximum`. Debe
conservarse la última lista no vacía o producir un resultado fallido finito, y probar muestras
extremas, no finitas y 100% outliers.

También hay que fijar la semántica de `ScenarioResult.Duration`: el diff lo cambia de la fase
normal a todo el intervalo que incluye extra-runs y overhead. Si Datadog necesita duración de
benchmark y lifecycle total, exponerlas por separado o documentar cuál se cierra en cada span.

### 13.4 Entorno, PATH y estado global

El snapshot estático `ScenarioProcessor.EnvironmentVariables` ya existía en `main`; por ello no
es una regresión introducida por este diff, pero el plan debe distinguir “bug de base que sigue
pendiente” de “cambio de PR”. Si se requiere que dos `RunAsync` en el mismo proceso observen cambios,
el snapshot debe ser por ejecución o por comando. La prueba de callback debe respetar el orden
real: hoy el entorno se copia/mezcla antes de `ExecutionStart`; un callback que llama a
`Environment.SetEnvironmentVariable` no afecta ese comando a menos que modifique el `Command`.
Decidir y probar una de estas dos semánticas, sin carreras globales en xUnit.

La lógica PATH nueva usa `OrdinalIgnoreCase` también en Unix. Debe comparar claves y entradas con
ignorancia de mayúsculas sólo en Windows, preferir la clave exacta `PATH` en Unix y no confundir
`/tmp/Foo` con `/tmp/foo`. Añadir pruebas de claves `PATH`/`path` y directorios sensibles a caso.
La construcción del diccionario, el split de PATH y `IsSafeMetricsFilePath` (syscalls y
`GetAttributes`) ocurren por datapoint; medir el coste y, si la semántica lo permite, preparar
una base por escenario sin perder aislamiento.

`DatadogMetadata.MetadataByExecution` es un `ConcurrentDictionary` estático que retiene cada
`Scenario`/`DataPoint` usado como clave y nunca elimina entradas. Las ejecuciones repetidas en un
host largo filtran memoria. Añadir `Release` al terminar el escenario/exportación o sustituirlo
por IDs scoped/weak, y probar múltiples `RunAsync` con Datadog habilitado.

### 13.5 Hot path y presupuesto de performance

La nueva implementación de `TimeItCallbacks` llama `GetInvocationList()` en cada ejecución y
pasa lambdas a `InvokeAll`. Medición del auditor: `ExecutionEnd` asigna aproximadamente 96 bytes
por datapoint incluso sin callbacks; con un callback llega a ~128 bytes, y el par start/end con
un servicio llega a ~160 bytes. Esto afecta especialmente a `DatadogProfilerService`, aunque no
cambie `DataPoint.Duration`. Debe existir un fast path sin callback y sin lambda; para el camino
con múltiples callbacks/aislamiento de excepciones, cachear una estructura de invocación o medir
un dispatcher explícito. Añadir un microbenchmark/Allocation test con cero, uno y dos callbacks.

La reflexión del resolver por `Name` escanea assemblies y tipos y ejecuta constructores de todos
los candidatos asignables para descubrir `Name`; es O(extensiones × assemblies × tipos) y tiene
efectos laterales. Construir un índice/cache de tipos, comparar el nombre sin activar cuando sea
posible y activar una sola coincidencia. El doble ordenamiento de baselines (`Config.Clone` y
engine) es setup, pero debe eliminarse o medirse. Los guards null de assertors/services se
eliminan tras la invariante; los del builder no deben venderse como mejora del benchmark.

### 13.6 Contrato exacto del profiler Datadog v2.61.0

La verificación debe desempaquetar el paquete v2.61.0 y leer el `loader.conf`, no sólo comprobar
que exista una carpeta. Los assets observados incluyen `Datadog.Profiler.Native.so`,
`Datadog.Trace.ClrProfiler.Native.so` y `loader.conf`; no incluyen
`Datadog.Linux.ApiWrapper.x64.so`. El `loader.conf` contiene filas `PROFILER` con GUID
`{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}` y `TRACER` con
`{50DA5EED-F1ED-B00B-1055-5AFE55A1ADE5}` y referencias que deben reconciliarse con los nombres
reales. El GUID `{846F5F1C-F9AE-4B07-969E-05C26BC060D8}` usado por el código puede ser correcto
por el mapeo del native loader; no reemplazarlo a ciegas: exigir una prueba/diagnóstico del
contrato upstream v2.

Para v2.61.0, `LD_PRELOAD`/ApiWrapper debe eliminarse o quedar explícitamente opt-in para un
asset/ABI verificado por separado; no se debe mezclar `Datadog.Trace.Bundle` v3. Las carpetas
`linux-musl-x64` presentes en el nupkg no demuestran soporte: sus tablas pueden no contener una
fila musl. Parsear la tabla y declarar musl no soportado si no existe mapping validado. Resolver
un mapa finito: Linux x64/arm64 sólo cuando la tabla y arquitectura coincidan; rechazar x86 u
otras arquitecturas, no caer silenciosamente en x64; documentar/testear Windows x86/x64 y reportar
Windows ARM64 si no hay asset. Validar también provenance de `DD_DOTNET_TRACER_HOME`: un home
heredado de v3 no puede ganar al asset v2 sin comprobar versión, y no se deben heredar
`CORECLR_PROFILER*`/`DD_NATIVELOADER_CONFIGFILE` incompatibles sin una política clara.

Separar tres estados en código y pruebas: assets localizados, variables configuradas y señal
observable de attach. `_isEnabled = true` no es evidencia suficiente. El test determinista debe
validar RID, env y loader; el smoke de attach debe ser opcional cuando runner/runtime/backend no
estén disponibles, con diagnóstico explícito.

### 13.7 Superficie de secretos y exporters

La política debe cubrir los built-ins completos, no sólo `EnvironmentVariables`:

- process name/arguments, working directory, config/file/module/suite names y
  `Environment.CommandLine` de la sesión;
- claves **y valores** de environment, `DD_TAGS`, URLs de repositorio con credenciales, branch/tag,
  opciones y variables de template;
- claves/valores de tags y métricas, incluidos `JsonElement` anidados (objetos/arrays),
  connection strings y valores no string;
- `ScenarioResult.Error`, `DataPoint.Error`, stderr/stdout y mensajes de excepciones.

Implementar una política conservadora: allowlist de variables exportables por defecto o redacción
por nombre más sustitución de todos los valores secretos conocidos en texto. La denylist debe
normalizar separadores/case y cubrir PAT/JWT/SAS/PWD/PASSWORD/PASSWD/SECRET/TOKEN/API_KEY/APP_KEY/
ACCESS_KEY/PRIVATE_KEY/CREDENTIAL/AUTH/CONNECTION_STRING; añadir casos de nombres desconocidos
con valores sentinel para no sobreestimar la garantía. Documentar que exporters custom reciben
resultados crudos y no pueden prometer sanitización si no usan el sink central.

JSON debe construir un DTO sanitizado totalmente nuevo, escribir a un archivo temporal dentro del
mismo directorio y hacer rename atómico; si falla, borrar el parcial. Debe tolerar colecciones
nulas/malformadas y no mutar el `ScenarioResult` original. Datadog necesita una abstracción/fake
de `TestSession`/`Test` para probar tags, nombres, session commandline y logs sin red; cada
escenario debe aislar errores, cerrar su test iniciado y permitir que los siguientes se exporten.
Los errores internos deben propagarse o devolverse como estado para que `TimeItEngine` no termine
con código 0 cuando `DatadogExporter` atrapó y ocultó una exportación fallida.

Limitar y truncar `LastStandardOutput`/logs por línea y por escenario antes de sanitizar/enviar;
una salida enorme no debe agotar memoria ni superar límites del backend. Escapar todos los valores
controlados por usuario antes de `Spectre.Console.Markup` (incluidos nombres, comandos, errores,
rutas, tags, métricas y stdout/stderr), no sólo los nombres visibles de la tabla. El error de
`CalculateConfidenceInterval` y todos los catches auditados deben mantener `WriteException` según
el contrato, con texto saneado.

### 13.8 Contrato CLI sin ambigüedad

La implementación debe documentar esta precedencia, o introducir opciones explícitas que la hagan
innecesaria:

1. `--config <path>` fuerza configuración; `--command <line>` o la línea después de `--` fuerza
   comando.
2. Un único token que sea un archivo `.json`/`.JSON` existente se trata como configuración y un
   JSON malformado produce error, nunca se ejecuta como proceso.
3. Una línea con varios tokens se trata como comando; un `.json` que aparezca sólo en argumentos
   (`echo hello.json`, `echo --config=foo.json`) no cambia el modo.
4. Un ejecutable real cuyo propio nombre termina en `.json` requiere el modo comando explícito;
   no intentar inferirlo del sufijo. Una ruta de configuración inexistente en modo config produce
   `FileNotFoundException` claro.
5. Si se soporta JSON sin extensión, detectar cualquier valor JSON relevante (BOM, `{`, `[` o
   `null`) sólo en una ruta que el usuario haya identificado como configuración; no convertir un
   comando en config por inspeccionar toda la línea. Un JSON no objeto debe fallar con mensaje de
   esquema, no ejecutarse.

Extraer parser/clasificador a una clase del proyecto Common o a una librería testeable. El proyecto
xUnit actual sólo referencia Common y no puede llamar funciones locales de `Program.cs`; la Fase 0
debe añadir ese seam o un proyecto de tests CLI. Probar quotes, rutas con espacios, `--` y opciones
antes/después sin que System.CommandLine consuma el argumento del proceso. Verificar también que
`--metrics false` y overrides explícitos se aplican en command y config mode.

### 13.9 Packaging, trimming, CI y versión: correcciones al plan original

Un feed con sólo los tres paquetes first-party no basta para restaurar un consumer Common: el
nuspec depende de CliWrap, Datadog.Trace.BenchmarkDotNet, DatadogTestLogger, MathNet y Spectre,
y un publish trimmed necesita runtime packs. El consumer test debe aislar el global packages cache,
usar un feed local first-party más nuget.org (o espejar todas las dependencias), y afirmar que no
aparecen assets first-party inesperados. Debe probarse el nupkg, no una referencia de proyecto.

`TimeItSharp.Common.0.4.8.nupkg` sólo contiene `lib/*.dll` y su dependencia StartupHook
netcoreapp3.1 no entrega automáticamente `TimeItSharp.StartupHook.dll` a un consumer net8; tampoco
entrega los assets nativos Datadog. La Fase 5 debe elegir una solución concreta (props/targets
transitivo, asset compatible/copied fuera del single-file, o declarar la característica no
soportada) y verificar consumer normal, single-file y trimmed en un workspace limpio. No aceptar
sólo `TrimmerRootAssembly` en el sample ni el mensaje `Startup hook location is empty.`.

El comando de trimming del plan y el de CI no son reproducibles tal como están:
`PublishTrimmed=true` framework-dependent produce `NETSDK1102`, y `publish --no-build` sin RID puede
producir `NETSDK1144` si no existe un output self-contained previo. Separar los casos y usar, por
ejemplo, `-f net8.0 -r linux-x64 --self-contained true -p:PublishTrimmed=true -p:PublishSingleFile=true`
en una carpeta limpia, sin `--no-build` salvo que exista un build con exactamente los mismos
parámetros. Probar por separado un publish framework-dependent no trimmed. Incluir IL3050/IL3000/
IL3056 además de IL2026/IL2104, y auditar `NoWarn` global (`IL3000`, `NETSDK1138`, `0436`) e
`IsAotCompatible=true` del Common antes de llamar al resultado “AOT compatible”.

El sample actual y los comandos finales sólo ejecutan net6, net8 y net10 aunque el build/matrix
promete net6–net10. Ejecutar net7/net9 o documentar de forma explícita que son build-only por EOL;
validar también que el tool package sólo tiene assets de net6/net10 y que la afirmación de
compatibilidad no promete ejecución no comprobada. La CI debe limpiar `artifacts`, `bin`, `obj` y
usar un output único para evitar que `--no-build` reutilice publishes de otra prueba.

La versión `0.4.8` ya está en el nupkg/distribución observada; decidir y aplicar un incremento antes
de publicar una corrección funcional, actualizar README, `dotnet tool install` y fixtures. La
allowlist debe usar el advisory exacto
`GHSA-38wr-vpc7-2mp4`, registrar owner/fecha de expiración/riesgo y fallar ante cualquier advisory
que no sea exactamente esa dependencia aprobada. No archivar outputs/logs de las pruebas de
secretos con sentinels en artefactos CI; usar hashes, temporales y un scan de ausencia antes de
subir resultados.

### 13.10 Hallazgos de base que deben etiquetarse correctamente

La segunda revisión confirma que varias observaciones son bugs reales pero ya estaban en `main` y
no deben describirse como regresiones de este diff: el snapshot estático del entorno, la falta de
`ScenarioFinish` en cancelación, el helper de timeout fire-and-forget, los guards de assertors en
la ruta caliente, el empaquetado Common sin hook/activos nativos y parte de las supresiones AOT.
Pueden mantenerse como trabajo requerido por el objetivo global de robustez, pero el PR debe
indicar cuáles son correcciones nuevas y cuáles son deuda preexistente. En cambio, el clasificador
`.json`, el chequeo ApiWrapper, los aliases, la inicialización tardía de Datadog, el sanitizador
incompleto, las asignaciones de callbacks, `WriteException` y los cambios de CI/trim son parte del
estado de esta rama y deben validarse como regresiones del PR.

### 13.11 Orden revisado de ejecución

1. Crear seams y pruebas de reproducción sin subir secretos: clasificador CLI, resolver de
   extensiones, fake de Datadog, fixture de profiler/loader, consumer NuGet y máquina de estados
   de lifecycle.
2. Elegir la política de null/colecciones y canonicalización de extensiones; eliminar no-op y
   aliases divergentes antes de que CLI vuelva a duplicar lógica.
3. Fijar el contrato CLI y `WriteException`.
4. Fijar contrato/provenance/RID de Datadog v2 y el momento de creación de sesión.
5. Aplicar sanitización central (incluidos logs, nombres y valores anidados) y aislamiento de
   exporters.
6. Reparar lifecycle/cancelación, timeout cleanup, resultados estadísticos vacíos y metadatos
   estáticos.
7. Resolver entorno/PATH y medir allocations/syscalls de callbacks y métricas.
8. Corregir packaging Common/StartupHook/native assets y trimming con consumer limpio.
9. Ampliar CI a la matriz decidida, vulnerability policy exacta y versión nueva.
10. Ejecutar validación final, inspeccionar paquetes/salida y solicitar revisión externa; mantener
    el PR draft hasta entonces.

### 13.12 Definition of Done adicional

- `echo hello.json`, `echo --config foo.json` y rutas con espacios ejecutan el proceso en modo
  comando; una configuración explícita inválida nunca se ejecuta como binario; un ejecutable
  `.json` tiene un modo explícito documentado.
- Aliases de builder, CLI y engine producen el mismo tipo y no activan constructores no
  relacionados; entradas mixtas o nulas dan errores indexados.
- No queda ningún `Exporters is null`/`Assertors is null`/`Services is null` dentro de la API
  después del límite de normalización elegido; no hay `Enumerable.Empty`/`!` redundantes ni
  no-op silenciosos. Las listas usadas por `ScenarioProcessor` no contienen null y no se
  comprueban por datapoint.
- `ScenarioFinish`, limpieza de temp/CTS y cierre de cada span ocurren exactamente una vez en
  éxito, excepción, token precancelado, warmup cancelado, run cancelado y extra-run cancelado.
- Estadística finita con 0/1/2 muestras, todos outliers y métricas vacías nunca lanza ni crea
  medidas NaN/Infinity; se documenta si `Duration` es fase normal o lifecycle total.
- El profiler sólo reporta assets/env encontrados conforme a la tabla v2 validada; no selecciona
  x64 para arquitecturas desconocidas, no hereda un home v3 y no usa ApiWrapper/LD_PRELOAD v2 sin
  ABI aprobado.
- Ningún sentinel de secreto aparece en JSON, tags, nombres Datadog, commandline de sesión,
  errores, stdout/stderr, métricas o logs; los resultados de entrada permanecen sin mutar y los
  logs están acotados. Los exporters custom quedan fuera de la garantía documentada.
- Un fallo de exporter hace que la API/CLI devuelva estado no cero después de cerrar recursos y
  no deja archivos JSON parciales.
- El consumer Common limpio recibe una estrategia de StartupHook explícita y assets/RID
  verificables, o la característica se marca como no soportada; normal, single-file, trimmed y
  framework-dependent se prueban con comandos válidos.
- La matriz CI ejecuta o documenta cada TFM, limpia outputs, no reutiliza publish stale, escanea
  artefactos de secretos y permite sólo el advisory exacto aprobado.
- Se actualiza la versión de release, README, fixtures y comandos de instalación; la decisión de
  mantener el plan en el PR está tomada antes de exigir una rama limpia.


### 13.13 Rangos estadísticos que aún requieren una decisión

La validación mantiene `acceptableRelativeWidth` como una anchura relativa positiva y limita
`minimumErrorReduction` al intervalo [0, 1], que es el rango de una reducción relativa. Los
extremos 0 y 1 son válidos y quedan cubiertos por la validación finita; valores mayores se
rechazan antes de clonar.


### 13.14 Cierre Datadog y errores del engine

El cierre Datadog también debe ser excepcionalmente seguro: `test.Close`, `TestSuite.Close`,
`TestModule.Close` y `TestSession.Close` requieren `try/finally` anidados para que un fallo en uno
no impida cerrar los siguientes. Probar errores de cierre con el fake, conservar `WriteException`
y devolver estado fallido sin ocultar el primer error.


### 13.15 Captura de excepciones y orden lifecycle

El `try` global actual tampoco captura de forma uniforme una excepción de `BeforeAll` o del bucle
principal: puede escapar de `RunAsync`, saltarse la fase de exporters y dejar sólo cleanup. La
máquina de estados debe capturar/registrar errores no fatales, decidir si exporta resultados
parciales y devolver un exit code estable; sólo excepciones verdaderamente fatales deben escapar.
`AfterAll` y `Finish` deben conservar orden y cardinalidad aun en ese caso.


El orden actual tampoco es determinista: en éxito se llama `AfterAllScenariosFinishes` antes de
`CleanScenario`, mientras que la ruta de excepción limpia primero y llama AfterAll después. Fijar
un orden único (por ejemplo `ScenarioFinish` → limpieza del escenario → `AfterAll` → `OnFinish`,
o la alternativa documentada), separar los flags de “intentado” y “completado” y probar éxito,
cancelación y excepción con observación de `ParentService`.


### 13.16 Validación de repeticiones y timeout

`ScenarioStartArg.RepeatScenarioForService` es otra frontera pública: hoy acepta service null y
counts negativos/cero, pero después se dereferencia `repeat.ServiceAskingForRepeat.Name` y puede
crear una repetición inválida. Validar `ThrowIfNull` y `count > 0` (o documentar un no-op) y cubrir
el callback con excepciones y cancelación.


En particular, el helper de timeout no debe cancelar el proceso cuando se cancela porque el
proceso terminó, cuando no existe el ejecutable auxiliar o cuando éste falla: la cancelación debe
ser idempotente y ocurrir sólo al vencer el límite bajo una condición atómica de “run still
active”. Añadir fixtures para helper ausente, helper fallido y helper largo.


El proceso auxiliar de timeout tampoco recibe el entorno/PATH preparado para el comando principal;
si depende de una variable o de un ejecutable en `workingDirectory`, puede fallar y aun así
cancelar el target. Definir si hereda exactamente el entorno preparado, resolver su ruta con la
misma regla OS-aware y probar helper en PATH/directorio de trabajo.


### 13.17 Caso de alias Datadog en el builder

`WithExporter<DatadogExporter>()` tiene además un caso de idempotencia incorrecta: si ya existe un
alias `Name="Datadog"`/`"DatadogExporter"`, retorna antes de ejecutar `EnableDatadog = true`.
La resolución/canonicalización debe separar “ya existe” de “habilitar configuración” y probar
alias fluent con `EnableDatadog=false`.


### 13.18 Verificación del follow-up de robustez (2026-08-07)

La implementación posterior a la revisión cubre también las rutas que no pasan por el CLI: los
argumentos de configuración, timeout y `ExecuteService` se tokenizan y se entregan a CliWrap como
argv ya delimitados. La captura de callbacks queda acotada y sanitizada, y el timeout mantiene una
cancelación de reserva con una pequeña gracia para permitir que el helper configurado se ejecute.
La instantánea de entorno capturada al entrar en `RunAsync` se propaga mediante `InitOptions` al
servicio de profiler, evitando leer `DD_DOTNET_TRACER_HOME` en vivo durante la inicialización.

Los grafos de resultados sanitizados limitan escenarios, datapoints, métricas y matrices de
overhead; los valores de secretos se descubren con presupuesto de recorrido. Las rutas literales
de JSON/Datadog se registran antes de operaciones de filesystem y se incluyen en la redacción de
excepciones. Los nombres de conexión/DSN reconocidos, controles ANSI/OSC y excepciones de getters
se filtran en los sinks integrados. La validación local final pasó: build Release de la solución,
132 pruebas Common, 60 pruebas CLI, verificador de paquete Common/trim y smoke tests de quoting,
timeout, callback output, consumo transitivo y aislamiento de assets Datadog v2/v3. `NU1903` de
`Datadog.Trace` 2.61.0 continúa siendo el advisory exacto documentado; el attach real del profiler
Linux permanece como aserción específica de CI.


### 13.19 Endurecimiento adicional verificado durante la revisión adversarial

Se añadió semántica de inicio intentado: `ScenarioFinish` se intenta aunque un callback de
`ScenarioStart` falle, y `ParentService` se limpia siempre; los clones nunca heredan contexto de
servicio. Los valores sensibles del snapshot de entorno se incluyen en todos los sinks integrados,
las validaciones de rutas y working directories se registran antes de informar errores, y se
eliminan también controles C1 además de ANSI/OSC/C0.

La configuración limita escenarios, iteraciones, warmups, extensiones, variables, tags y
validaciones de rutas. Las repeticiones extra tienen un límite agregado y consumen el presupuesto de
duración; cada comando también queda sujeto al deadline global cuando no hay timeout específico.
La tabla de comparación y la materialización de escenarios en exporters comparten presupuestos
globales de elementos y caracteres, evitando productos multiplicativos. La CLI trata todo positional
legacy terminado en `.json` como configuración —los comandos ambiguos deben usar `--command` o
`--`— y conserva exactamente las fronteras argv situadas después de `--`. La resolución de comandos
simples nunca sondea nombres combinados en el CWD; sólo las rutas explícitas pueden usar probing de
filesystem para recuperar ejecutables con espacios.


La carga de configuración también rechaza archivos mayores de 16 MiB y limita strings, opciones
JSON, colecciones de extensiones y valores de proceso antes de clonar. La selección de profiler
sólo considera raíces absolutas (nunca el CWD mutable), exige archivos/directorios regulares y
limita `loader.conf` por bytes, filas y longitud de fila antes de validar su provenance.


Las variables de plantilla están acotadas por número, tamaño de nombre/valor y expansión; la
expansión no puede amplificar una ruta, tag o argumento más allá del límite de texto compartido.
El release de metadata Datadog se ejecuta después de `OnFinish`, incluyendo metadata recreada por
ese callback.


### 13.20 Cierre del segundo pase adversarial

La clasificación y tokenización CLI ya no dependen de prefijos existentes en el directorio de
trabajo. Los argumentos discretos posteriores a `--` conservan sus fronteras, incluyendo espacios,
apóstrofes, rutas UNC y secuencias CRT de backslashes/comillas. `--config` y `--command` requieren
un valor, no pueden consumir otra opción por accidente y reciben el token de cancelación de la
invocación. El modo positional legacy reserva cualquier valor terminado en `.json` para
configuración; los comandos cuyo texto termine así deben declararse explícitamente.

Los deadlines se validan contra el rango común soportado por `CancellationTokenSource` y también se
acotan defensivamente en runtime antes de iniciar procesos. El presupuesto global se carga una sola
vez, los callbacks de `ExecuteService` combinan timeout y cancelación del engine, y toda ruta mata y
espera procesos iniciados. Todos los exporters resueltos se disponen por identidad de referencia,
incluso si falla antes un servicio/assertor; primero se dispone el cleanup ordinario, después se
notifica el outcome definitivo y finalmente se cierran exporters outcome-aware como Datadog.

Los sinks integrados consumen un snapshot separado de lectura única. Los controles terminales se
eliminan antes y después de la redacción estructural; Authorization se oculta para cualquier scheme;
las fuentes de secretos sobre presupuesto o con enumeradores hostiles fallan cerradas; y los
presupuestos de elementos/caracteres y el tamaño posterior a sustituciones son globales. JSON usa
reemplazo atómico con temporales `0600`; net7+ restaura el modo Unix previo y net6 conserva el
fallback restrictivo `0600` cuando no existe una API portable para leerlo. La correlación trace/span
Datadog se captura como tuplas de valor durante la única enumeración, sin retener el grafo runtime.

El profiler usa un home privado de exactamente veinte assets 2.61.0: dieciséis binarios/configs del
paquete BenchmarkDotNet y cuatro `Datadog.Trace.dll` administrados. Se comprueban conjunto exacto,
SHA-256, versión, archivos regulares y todos los ancestros; homes v3, mixtos, corruptos, con extras o
reparse se rechazan. Los targets `build` y `buildTransitive` copian tarde el home v2 aislado y el
startup hook externo, de modo que wrappers y consumidores con Bundle v3 no mezclan payloads. La
detección musl compara basenames mapeados exactos y Windows anuncia únicamente el bitness compatible
con el loader seleccionado.

El audit de cierre añadió regresiones para impedir que `--command <ruta> -- <args>` vuelva a sondear
un nombre combinado del filesystem, disponer extensiones creadas antes de un fallo posterior de
resolución y degradar a fallo los outcomes aún abiertos si otro cierre falla. Los snapshots de
secretos verifican que `Count` coincida con la enumeración real y fallan cerrados ante iteradores
parciales; la normalización elimina también Unicode Default-Ignorable —incluyendo CGJ y variation
selectors— sin eliminar acentos combinantes ordinarios.

Validación final sobre el HEAD combinado: build Release net6.0–net10.0 con 0 errores; 132/132 pruebas
Common y 60/60 CLI; tres `.nupkg`; consumer Common trimmed/single-file con startup hook y métricas;
consumer mediante paquete wrapper; aislamiento y hashes v2 frente a Bundle v3; XML, YAML, shell y
`git diff --check`. Permanece únicamente el advisory aceptado `NU1903` de Datadog.Trace 2.61.0
(`GHSA-38wr-vpc7-2mp4`) y el attach nativo real Linux se ejecuta de forma platform-gated en CI.
