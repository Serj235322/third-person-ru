// Used only in isolated InstallerTest builds. Never distributed as .NET.
using System;
using System.IO;

internal static class FrameworkStub
{
    private static string Value(string[] args, string name, string fallback)
    {
        foreach (string arg in args)
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return arg.Substring(name.Length + 1);
        return fallback;
    }

    private static int Main(string[] args)
    {
        string root = Value(args, "/TestRoot", "");
        if (!Directory.Exists(root)) return 5100;
        File.AppendAllText(Path.Combine(root, "framework-calls.txt"),
            string.Join(" ", args) + Environment.NewLine);
        if (Array.IndexOf(args, "/norestart") < 0 ||
            Array.IndexOf(args, "/ChainingPackage") < 0) return 5100;
        int code = int.Parse(Value(args, "/TestExitCode", "0"));
        if (code == 0 && Value(args, "/TestNoRegistration", "0") != "1")
            File.WriteAllText(Path.Combine(root, "framework-installed.txt"), "test only");
        return code;
    }
}
