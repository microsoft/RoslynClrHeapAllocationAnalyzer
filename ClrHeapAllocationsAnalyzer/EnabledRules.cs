using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
namespace ClrHeapAllocationAnalyzer
{
    public class EnabledRules
    {
        private readonly IReadOnlyDictionary<string, DiagnosticDescriptor> rules;

        public EnabledRules(IReadOnlyDictionary<string, DiagnosticDescriptor> rules)
        {
            this.rules = rules;
        }

        public static readonly EnabledRules None = new EnabledRules(new Dictionary<string, DiagnosticDescriptor>());

        public bool AnyEnabled => rules.Count > 0;

        public bool IsEnabled(string ruleId)
        {
            return rules.ContainsKey(ruleId);
        }

        public DiagnosticDescriptor Get(string ruleId)
        {
            if (!rules.ContainsKey(ruleId))
            {
                throw new ArgumentException($"Rule '{ruleId}' is not among the enabled rules.", nameof(ruleId));
            }

            return rules[ruleId];
        }

        public bool TryGet(string ruleId, out DiagnosticDescriptor descriptor)
        {
            return rules.TryGetValue(ruleId, out descriptor);
        }
    }
}