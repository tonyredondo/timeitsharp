using Microsoft.AspNetCore.Builder;

namespace TimeIt.WebApiService;

public class Class1
{
    public void Method()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        app.Run("http://localhost:8126");
    }
}