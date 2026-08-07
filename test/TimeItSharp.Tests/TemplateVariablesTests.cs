namespace TimeItSharp.Tests;

public sealed class TemplateVariablesTests
{
    [Fact]
    public void Constructor_registers_current_working_directory()
    {
        var variables = new TemplateVariables();

        Assert.Equal(1, variables.Length);
        Assert.Equal(Environment.CurrentDirectory, variables.Expand("$(CWD)"));
        Assert.Equal("prefix/$(UNKNOWN)/suffix", variables.Expand("prefix/$(UNKNOWN)/suffix"));
    }

    [Fact]
    public void Add_expands_custom_variables_and_preserves_first_value()
    {
        var variables = new TemplateVariables();

        variables.Add("ROOT", "/tmp/root");
        variables.Add("ROOT", "/tmp/other");

        Assert.Equal(2, variables.Length);
        Assert.Equal("/tmp/root/bin:/tmp/root/lib", variables.Expand("$(ROOT)/bin:$(ROOT)/lib"));
    }

    [Fact]
    public void Expand_returns_input_unchanged_for_empty_or_non_template_values()
    {
        var variables = new TemplateVariables();

        Assert.Equal(string.Empty, variables.Expand(string.Empty));
        Assert.Equal("   ", variables.Expand("   "));
        Assert.Equal("plain text", variables.Expand("plain text"));
        Assert.Null(variables.Expand(null!));
    }

    [Fact]
    public void Clone_copies_values_without_sharing_variable_storage()
    {
        var variables = new TemplateVariables();
        variables.Add("ROOT", "/tmp/root");

        var clone = variables.Clone();
        clone.Add("CHILD", "value");

        Assert.Equal(2, variables.Length);
        Assert.Equal(3, clone.Length);
        Assert.Equal("$(CHILD)", variables.Expand("$(CHILD)"));
        Assert.Equal("value", clone.Expand("$(CHILD)"));
        Assert.Equal("/tmp/root/child", clone.Expand("$(ROOT)/child"));
    }
    [Fact]
    public void Duplicate_name_with_markup_is_rendered_as_text()
    {
        var variables = new TemplateVariables();
        const string maliciousName = "broken[/][red]\" <script>";
        variables.Add(maliciousName, "first");

        var exception = Record.Exception(() => variables.Add(maliciousName, "second"));

        Assert.Null(exception);
        Assert.Equal("first", variables.Expand($"$({maliciousName})"));
    }

    [Fact]
    public void Add_rejects_oversized_variable_values()
    {
        var variables = new TemplateVariables();

        Assert.Throws<ArgumentException>(() => variables.Add("BIG", new string('x', 65 * 1024)));
    }

    [Fact]
    public void Expand_rejects_amplified_template_text()
    {
        var variables = new TemplateVariables();
        variables.Add("BIG", new string('x', 64 * 1024));

        Assert.Throws<ArgumentException>(() => variables.Expand(string.Concat(Enumerable.Repeat("$(BIG)", 32))));
    }

}
