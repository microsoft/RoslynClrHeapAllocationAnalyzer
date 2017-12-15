using System.Collections.Generic;

namespace ClrHeapAllocationAnalyzer.Common {
    public interface IHeapAllocationAnalyzerSettings {
        bool Enabled { get; set; }
    }

    public static class Settings {
        private static readonly IReadOnlyCollection<string> Empty = new List<string>();

        public static IHeapAllocationAnalyzerSettings Instance { get; set; }

        public static bool IsAnyEnabled(IEnumerable<string> ruleIds) {
            foreach (var ruleId in ruleIds) {
                if (IsEnabled(ruleId)) {
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyCollection<string> GetEnabled(IEnumerable<string> ruleIds)
        {
            List<string> result = null;
            foreach (var ruleId in ruleIds) {
                if (IsEnabled(ruleId)) {
                    if (result == null)
                    {
                        result = new List<string>();
                    }

                    result.Add(ruleId);
                }
            }

            return result ?? Empty;
        }

        public static bool IsEnabled(string ruleId)
        {
            return Instance.Enabled;
        }
    }
}
