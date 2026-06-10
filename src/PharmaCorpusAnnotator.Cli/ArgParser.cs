namespace PharmaCorpusAnnotator.Cli;

public static class ArgParser
{
    /// <summary>
    /// Parses args of the form: --key value  or  --flag
    /// Returns a dictionary. Flags (no value) are stored with value "true".
    /// </summary>
    public static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        while (i < args.Length)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    result[arg] = args[i + 1];
                    i += 2;
                }
                else
                {
                    result[arg] = "true";
                    i++;
                }
            }
            else
            {
                i++;
            }
        }
        return result;
    }
}
