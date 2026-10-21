using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class NetworkTweaks : ITweak
    {
        public string Id => "network_nagles";
        public string Title => "Disable Nagle's Algorithm";
        public string Description => "Cuts small-packet TCP latency. Gains are small and mostly for TCP-based games.";

        private const string InterfacesPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        private static readonly string[] Names = { "TcpAckFrequency", "TCPNoDelay" };

        public bool IsApplied()
        {
            try
            {
                var active = ActiveInterfaces();
                return active.Count > 0 &&
                       active.All(id => Names.All(n => ReadValue(id, n) == 1));
            }
            catch { return false; }
        }

        public void Apply()
        {
            bool first = !TweakBackup.Has(Id);

            foreach (var id in ActiveInterfaces())
                foreach (var n in Names)
                {
                    if (first)
                        TweakBackup.Save(Id, $"{id}|{n}", ReadValue(id, n)?.ToString());

                    RegDword.Write(RegistryHive.LocalMachine, $@"{InterfacesPath}\{id}", n, 1);
                }
        }

        public void Revert()
        {
            foreach (var id in AllInterfaces())
                foreach (var n in Names)
                {
                    int? target = TweakBackup.TryLoad(Id, $"{id}|{n}", out var saved) && int.TryParse(saved, out var v)
                        ? v : null;

                    RegDword.Write(RegistryHive.LocalMachine, $@"{InterfacesPath}\{id}", n, target);
                }

            TweakBackup.Clear(Id);
        }

        // helpers

        private static int? ReadValue(string id, string name) =>
            RegDword.Read(RegistryHive.LocalMachine, $@"{InterfacesPath}\{id}", name);

        private static string[] AllInterfaces()
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = hklm.OpenSubKey(InterfacesPath);
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        private static List<string> ActiveInterfaces()
        {
            var result = new List<string>();
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

            foreach (var id in AllInterfaces())
            {
                using var k = hklm.OpenSubKey($@"{InterfacesPath}\{id}");
                if (k == null) continue;

                var dhcp = k.GetValue("DhcpIPAddress") as string;
                var static_ = k.GetValue("IPAddress") as string[];

                bool hasDhcp = !string.IsNullOrEmpty(dhcp) && dhcp != "0.0.0.0";
                bool hasStatic = static_ != null && static_.Any(s => !string.IsNullOrEmpty(s) && s != "0.0.0.0");

                if (hasDhcp || hasStatic) result.Add(id);
            }
            return result;
        }
    }
}