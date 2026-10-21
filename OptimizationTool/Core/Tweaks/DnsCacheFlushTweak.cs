using System;
using System.Diagnostics;
using System.IO;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class DnsCacheFlushTweak : ITweak
    {
        public string Id => "DnsCacheFlush";
        public string Title => "Flush DNS Resolver Cache";
        public string Description => "Clears saved website address lookups. Can fix stale or wrong DNS results.";
        public bool IsAction => true;

        public string? LastError { get; private set; }

        public bool IsApplied() => false;

        public void Apply()
        {
            LastError = null;

            string exe = Path.Combine(Environment.SystemDirectory, "ipconfig.exe");

            var psi = new ProcessStartInfo(exe, "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var p = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start ipconfig.exe");

            string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
                LastError = string.IsNullOrWhiteSpace(output)
                    ? $"ipconfig failed (exit {p.ExitCode})."
                    : output.Trim();
        }

        public void Revert() { }
    }
}