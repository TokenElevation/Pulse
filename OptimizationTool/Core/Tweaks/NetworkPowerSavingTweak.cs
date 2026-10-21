using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class NetworkPowerSavingTweak : ITweak
    {
        public string Id => "NetworkPowerSaving";
        public string Title => "Turn off Network Power-Saving";
        public string Description => "On physical adapters: stops Windows from powering the adapter down and disables Energy-Efficient Ethernet / green-mode settings where the driver has them. Increases idle power draw on laptops.";
        public bool IsAction => false;

        private const string ClassPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
        private const int PnPNoPowerOff = 24;

        private static readonly string[] SavingKeywords =
            { "*EEE", "EnableGreenEthernet", "GreenEthernet", "PowerSavingMode", "ULPMode" };

        public bool IsApplied()
        {
            try
            {
                var adapters = PhysicalAdapters();
                if (adapters.Count == 0) return false;

                foreach (var (path, _) in adapters)
                {
                    if (RegDword.Read(RegistryHive.LocalMachine, path, "PnPCapabilities") != PnPNoPowerOff)
                        return false;

                    foreach (var kw in SavingKeywords)
                        if (ReadString(path, kw) is string s && s != "0")
                            return false;
                }
                return true;
            }
            catch { return false; }
        }

        public void Apply()
        {
            bool first = !TweakBackup.Has(Id);

            foreach (var (path, guid) in PhysicalAdapters())
            {
                if (first)
                {
                    TweakBackup.Save(Id, $"{guid}|PnPCapabilities",
                        RegDword.Read(RegistryHive.LocalMachine, path, "PnPCapabilities")?.ToString());

                    foreach (var kw in SavingKeywords)
                        if (ReadString(path, kw) is string old)
                            TweakBackup.Save(Id, $"{guid}|{kw}", old);
                }

                RegDword.Write(RegistryHive.LocalMachine, path, "PnPCapabilities", PnPNoPowerOff);

                foreach (var kw in SavingKeywords)
                    if (ReadString(path, kw) != null)
                        WriteString(path, kw, "0");
            }
        }

        public void Revert()
        {
            foreach (var (path, guid) in PhysicalAdapters())
            {
                int? pnp = TweakBackup.TryLoad(Id, $"{guid}|PnPCapabilities", out var s) && int.TryParse(s, out var n)
                    ? n : null;
                RegDword.Write(RegistryHive.LocalMachine, path, "PnPCapabilities", pnp);

                foreach (var kw in SavingKeywords)
                    if (TweakBackup.TryLoad(Id, $"{guid}|{kw}", out var old) && old != null)
                        WriteString(path, kw, old);
            }

            TweakBackup.Clear(Id);
        }

        // helpers

        private static List<(string Path, string Guid)> PhysicalAdapters()
        {
            var list = new List<(string, string)>();
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var cls = hklm.OpenSubKey(ClassPath);
            if (cls == null) return list;

            foreach (var name in cls.GetSubKeyNames())
            {
                if (name.Length != 4 || !name.All(char.IsDigit)) continue;

                using var k = cls.OpenSubKey(name);
                if (k?.GetValue("*NdisDeviceType") is int type && type == 0 &&
                    k.GetValue("NetCfgInstanceId") is string guid)
                    list.Add(($@"{ClassPath}\{name}", guid));
            }
            return list;
        }

        private static string? ReadString(string path, string name)
        {
            using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var k = b.OpenSubKey(path);
            return k?.GetValue(name)?.ToString();
        }

        private static void WriteString(string path, string name, string value)
        {
            using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var k = b.OpenSubKey(path, true)
                ?? throw new InvalidOperationException($"Could not open {path}");
            k.SetValue(name, value, RegistryValueKind.String);
        }
    }
}