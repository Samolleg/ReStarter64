using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

class WineEnvironmentCheck
{
    static int count;
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        count++;
    }

    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--child")
        {
            Console.WriteLine(Environment.GetEnvironmentVariable("WINEDEBUG"));
            Console.WriteLine(Environment.GetEnvironmentVariable("EI_STARTER_ENV_TEST"));
            return 0;
        }
        try
        {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var helper = assembly.GetType("EIStarter.WineCompatibility", true)
                .GetMethod("PrepareGameStart", BindingFlags.Static | BindingFlags.NonPublic);
            string parent = Environment.GetEnvironmentVariable("WINEDEBUG");
            foreach (string previous in new[] { null, "-all", "trace+seh", "warn+heap" })
            {
                var start = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--child")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                if (previous == null) start.EnvironmentVariables.Remove("WINEDEBUG");
                else start.EnvironmentVariables["WINEDEBUG"] = previous;
                start.EnvironmentVariables["EI_STARTER_ENV_TEST"] = "preserved";
                helper.Invoke(null, new object[] { start });
                using (var child = Process.Start(start))
                {
                    string output = child.StandardOutput.ReadToEnd().Replace("\r\n", "\n");
                    Check(child.WaitForExit(10000) && child.ExitCode == 0, "child exit");
                    Check(output == "warn+heap\npreserved\n", "child receives requested and unrelated environment");
                }
                Check(Environment.GetEnvironmentVariable("WINEDEBUG") == parent, "parent environment unchanged");
            }
            Console.WriteLine("PASS " + count + " Wine child-environment checks; headless Windows child only.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
