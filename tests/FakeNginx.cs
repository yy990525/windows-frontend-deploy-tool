using System;
using System.IO;

internal static class FakeNginx
{
    private static int Main(string[] args)
    {
        string config = string.Empty;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-c")
            {
                config = args[i + 1];
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(config) || !File.Exists(config))
        {
            Console.Error.WriteLine("nginx: configuration file not found");
            return 2;
        }

        string text = File.ReadAllText(config);
        if (text.IndexOf("INVALID_TEST_CONFIG", StringComparison.Ordinal) >= 0)
        {
            Console.Error.WriteLine("nginx: configuration test failed");
            return 1;
        }

        Console.Error.WriteLine("nginx: configuration syntax is ok");
        return 0;
    }
}
