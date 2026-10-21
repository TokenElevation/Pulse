using System.Diagnostics;
using System.Text.RegularExpressions;
using OptimizationTool.Core.Interfaces;

public class PowerPlanTweak : ITweak
{
    public string Id => "PowerPlan";
    public string Title => "Ultimate Performance Power Plan";
    public string Description => "Activates the Ultimate Performance power scheme (falls back to High Performance) to prevent CPU throttling.";
    public bool IsAction => false;

    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a15-77724aaa5f6c";
    private const string UltimateTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    private const string OurUltimateGuid = "5f1c2a3e-7b44-4d0e-9a6b-0c1d2e3f4a5b";

    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OptimizationTool", "powerplan_prev.txt");

    private static readonly Regex GuidRx = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    public bool IsApplied()
    {
        var active = GetActiveGuid();
        return Eq(active, OurUltimateGuid) || Eq(active, HighPerfGuid);
    }

    public void Apply()
    {
        var current = GetActiveGuid();

        if (current != null && !Eq(current, OurUltimateGuid) && !Eq(current, HighPerfGuid))
            SavePrevious(current);

        string target = OurUltimateGuid;

        if (!SchemeExists(OurUltimateGuid))
        {
            var (code, _) = Powercfg("-duplicatescheme", UltimateTemplateGuid, OurUltimateGuid);
            if (code != 0)
                target = HighPerfGuid;
        }

        var (setCode, _) = Powercfg("/setactive", target);

        // High Performance
        if (setCode != 0 && target == HighPerfGuid)
        {
            Powercfg("-duplicatescheme", HighPerfGuid, HighPerfGuid);
            Powercfg("/setactive", HighPerfGuid);
        }
    }

    public void Revert()
    {
        var prev = LoadPrevious();
        var target = (prev != null && SchemeExists(prev)) ? prev : BalancedGuid;

        var (code, _) = Powercfg("/setactive", target);
        if (code != 0 && target != BalancedGuid)
            Powercfg("/setactive", BalancedGuid);

        if (SchemeExists(OurUltimateGuid))
            Powercfg("-delete", OurUltimateGuid);

        try { File.Delete(StatePath); } catch { }
    }

    // helpers

    private static bool Eq(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string? GetActiveGuid()
    {
        var (_, output) = Powercfg("/getactivescheme");
        var m = GuidRx.Match(output);
        return m.Success ? m.Value : null;
    }

    private static bool SchemeExists(string guid)
    {
        var (_, output) = Powercfg("/list");
        return output.IndexOf(guid, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SavePrevious(string guid)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, guid);
        }
        catch { }
    }

    private static string? LoadPrevious()
    {
        try
        {
            if (!File.Exists(StatePath)) return null;
            var text = File.ReadAllText(StatePath).Trim();
            return GuidRx.IsMatch(text) ? text : null;
        }
        catch { return null; }
    }
    private static (int ExitCode, string Output) Powercfg(params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi)!;
            var errTask = p.StandardError.ReadToEndAsync(); // avoid pipe deadlock
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(10_000))
            {
                try { p.Kill(); } catch { }
                return (-1, output);
            }
            return (p.ExitCode, output + errTask.Result);
        }
        catch
        {
            return (-1, string.Empty);
        }
    }
}