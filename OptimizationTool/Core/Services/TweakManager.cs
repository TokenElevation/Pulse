using System.Collections.Generic;
using OptimizationTool.Core.Interfaces;
using OptimizationTool.Core.Tweaks;

namespace OptimizationTool.Core.Services
{
    public static class TweakManager
    {
        // list of all available tweaks in the application
        private static readonly List<ITweak> _tweaks = new()
        {
            new NetworkThrottlingTweak(),
            new NetworkResponsivenessTweak()
            // register tweaks here
        };

        public static IReadOnlyList<ITweak> GetAllTweaks() => _tweaks;


        /// reverts every single tweak back to its Windows default state
        public static void RevertAll()
        {
            foreach (var tweak in _tweaks)
            {
                try
                {
                    // only revert when current one is applied 
                    if (tweak.IsApplied())
                    {
                        tweak.Revert();
                    }
                }
                catch
                {
                    // log or handle individual tweak error silently so others can revert
                }
            }
        }

        /// check how many tweaks are currently applied t o windows
        public static int GetAppliedCount()
        {
            int count = 0;
            foreach (var tweak in _tweaks)
            {
                if (tweak.IsApplied())
                {
                    count++;
                }
            }
            return count;
        }
    }
}