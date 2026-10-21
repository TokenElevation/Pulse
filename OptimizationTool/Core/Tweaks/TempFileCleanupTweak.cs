using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class TempFileCleanupTweak : ITweak
    {
        public string Id => "TempFileCleanup";
        public string Title => "Clean System Temp Files";
        public string Description => "Cleans temp to free up disk space";
        public bool IsAction => true;

        public long LastBytesFreed { get; private set; }
        public int LastFilesSkipped { get; private set; }
        public string? LastError { get; private set; }

        private static readonly TimeSpan MinAge = TimeSpan.FromHours(24);

        public bool IsApplied() => false;

        public void Apply()
        {
            LastBytesFreed = 0;
            LastFilesSkipped = 0;
            LastError = null;

            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.GetTempPath(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
            };

            foreach (var raw in targets)
            {
                string path = Path.GetFullPath(raw).TrimEnd(Path.DirectorySeparatorChar);
                if (!IsSafeTarget(path) || !Directory.Exists(path)) continue;

                CleanDirectory(new DirectoryInfo(path));
            }

            if (LastBytesFreed == 0 && LastFilesSkipped > 0)
                LastError = "Nothing could be deleted. Files are in use, too recent, or administrator rights are required.";
        }

        public void Revert() { }

        private static bool IsSafeTarget(string path)
        {
            string? root = Path.GetPathRoot(path)?.TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(root, path, StringComparison.OrdinalIgnoreCase)) return false;

            var forbidden = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            return !forbidden.Any(f => !string.IsNullOrEmpty(f) &&
                string.Equals(f.TrimEnd(Path.DirectorySeparatorChar), path, StringComparison.OrdinalIgnoreCase));
        }

        private void CleanDirectory(DirectoryInfo dir)
        {
            foreach (var file in SafeEnumerate(() => dir.EnumerateFiles()))
            {
                try
                {
                    if (DateTime.UtcNow - file.LastWriteTimeUtc < MinAge)
                    {
                        LastFilesSkipped++;
                        continue;
                    }

                    long size = file.Length;
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                    LastBytesFreed += size;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    LastFilesSkipped++;
                }
            }

            foreach (var sub in SafeEnumerate(() => dir.EnumerateDirectories()))
            {
                if (sub.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;

                CleanDirectory(sub);

                try { sub.Delete(false); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            }
        }

        private static List<T> SafeEnumerate<T>(Func<IEnumerable<T>> source)
        {
            try { return source().ToList(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return new List<T>();
            }
        }
    }
}