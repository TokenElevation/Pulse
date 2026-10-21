using System.Diagnostics;

namespace OptimizationTool.Core.Tweaks
{
    internal static class Powercfg
    {
        public static (int ExitCode, string Output) Run(params string[] args)
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
                var err = p.StandardError.ReadToEndAsync();
                var output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(10_000))
                {
                    try { p.Kill(); } catch { }
                    return (-1, output);
                }
                return (p.ExitCode, output + err.Result);
            }
            catch (Exception ex)
            {
                return (-1, ex.Message);
            }
        }

        public static string RunOrThrow(params string[] args)
        {
            var (code, output) = Run(args);
            if (code != 0)
                throw new InvalidOperationException($"powercfg {string.Join(' ', args)} failed ({code}): {output.Trim()}");
            return output;
        }
    }
}