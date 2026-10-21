using System;
using System.IO;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class PrefetchCleanupTweak : ITweak
    {
        public string Id => "PrefetchCleanup";
        public string Title => "Clear Windows Prefetch Cache";
        public string Description => "Deletes Windows app launch traces.";
        public bool IsAction => true;

        public int LastFilesDeleted { get; private set; }
        public int LastFilesSkipped { get; private set; }
        public long LastBytesFreed { get; private set; }
        public string? LastError { get; private set; }

        private static string PrefetchPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");

        public bool IsApplied() => false;

        public void Apply()
        {
            LastFilesDeleted = 0;
            LastFilesSkipped = 0;
            LastBytesFreed = 0;
            LastError = null;

            if (!Directory.Exists(PrefetchPath))
            {
                LastError = "Prefetch folder not found (prefetching may be disabled).";
                return;
            }

            FileInfo[] files;
            try
            {
                files = new DirectoryInfo(PrefetchPath).GetFiles("*.pf");
            }
            catch (UnauthorizedAccessException)
            {
                LastError = "Administrator privileges required.";
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    long size = file.Length;
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                    LastFilesDeleted++;
                    LastBytesFreed += size;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    LastFilesSkipped++;
                }
            }

            if (LastFilesSkipped > 0 && LastFilesDeleted == 0)
                LastError = "No files could be deleted. Administrator privileges are likely required.";
        }

        public void Revert() { }
    }
}