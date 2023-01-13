namespace TimeIt;

static class Utils
{
    public static string ReplaceCustomVars(string value)
    {
        return value.Replace("$(CWD)", Environment.CurrentDirectory);
    }
}