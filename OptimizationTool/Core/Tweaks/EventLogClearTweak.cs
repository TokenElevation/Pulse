using System;
using System.Collections.Generic;
using System.Diagnostics;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class EventLogClearTweak : ITweak
    {
        public string Id => "EventLogClear";
        public string Title => "Clear Application & System Event Logs";
        public string Description => "Erases the Application and System event logs. The Security log is never touched.";
        public bool IsAction => true;
        private static readonly string[] LogNames = { "Application", "System" };

        public IReadOnlyDictionary<string, string> LastErrors { get; private set; } =
            new Dictionary<string, string>();

        public bool IsApplied() => false;

        public void Apply()
        {
            var errors = new Dictionary<string, string>();

            foreach (var logName in LogNames)
            {
                try
                {
                    if (!EventLog.Exists(logName))
                    {
                        errors[logName] = "Log not found.";
                        continue;
                    }

                    using var log = new EventLog(logName);
                    log.Clear();
                }
                catch (System.Security.SecurityException)
                {
                    errors[logName] = "Administrator privileges required.";
                }
                catch (Exception ex)
                {
                    errors[logName] = ex.Message;
                }
            }

            LastErrors = errors;
        }

        public void Revert()
        {
        }
    }
}