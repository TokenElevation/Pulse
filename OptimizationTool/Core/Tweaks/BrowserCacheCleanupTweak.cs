using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class BrowserCacheCleanupTweak : ITweak
    {
        public string Id => "BrowserCacheCleanup";
        public string Title => "Clear Browser Cache";
        public string Description => "Deletes cached files for browsers. Close your browsers first for a full cleanup.";
        public bool IsAction => true;

        public long LastBytesFreed { get; private set; }
        public int LastFilesSkipped { get; private set; }
        public bool BrowsersWereRunning { get; private set; }

        private static readonly string[] ChromiumRoots =
        {
            @"Google\Chrome\User Data",
            @"Microsoft\Edge\User Data",
            @"BraveSoftware\Brave-Browser\User Data"
        };

        private static readonly string[] ChromiumCacheDirs =
        {
            "Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage"
        };

        private static readonly string[] BrowserProcesses =
            { "chrome", "msedge", "brave", "firefox" };

        public bool IsApplied() => false;

        public void Apply()
        {
            LastBytesFreed = 0;
            LastFilesSkipped = 0;
            BrowsersWereRunning = BrowserProcesses.Any(n => Process.GetProcessesByName(n).Length > 0);

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            foreach (var cacheDir in EnumerateCacheDirs(localAppData))
                ClearDirectory(cacheDir);
        }

        public void Revert() { }

        private static IEnumerable<string> EnumerateCacheDirs(string localAppData)
        {
            // Chromium browsers: Default + Profile N
            foreach (var rel in ChromiumRoots)
            {
                string userData = Path.Combine(localAppData, rel);
                if (!Directory.Exists(userData)) continue;

                var profiles = Directory.EnumerateDirectories(userData)
                    .Where(d =>
                    {
                        string n = Path.GetFileName(d);
                        return n == "Default" || n.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)
                            || n == "Guest Profile";
                    });

                foreach (var profile in profiles)
                    foreach (var sub in ChromiumCacheDirs)
                    {
                        string p = Path.Combine(profile, sub);
                        if (Directory.Exists(p)) yield return p;
                    }
            }

            string ffRoot = Path.Combine(localAppData, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ffRoot))
                foreach (var profile in Directory.EnumerateDirectories(ffRoot))
                {
                    string p = Path.Combine(profile, "cache2");
                    if (Directory.Exists(p)) yield return p;
                }
        }

        private void ClearDirectory(string path)
        {
            var dir = new DirectoryInfo(path);

            if (dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) return;

            foreach (var file in SafeEnumerate(() => dir.EnumerateFiles()))
            {
                try
                {
                    long size = file.Length;
                    file.Attributes = FileAttributes.Normal; // clear read-only
                    file.Delete();
                    LastBytesFreed += size;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    LastFilesSkipped++; // locked by a running browser
                }
            }

            foreach (var sub in SafeEnumerate(() => dir.EnumerateDirectories()))
            {
                ClearDirectory(sub.FullName);
                try { sub.Delete(false); } // remove only if now empty
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            }
        }

        private static IEnumerable<T> SafeEnumerate<T>(Func<IEnumerable<T>> source)
        {
            try { return source().ToList(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Array.Empty<T>();
            }
        }
    }
}