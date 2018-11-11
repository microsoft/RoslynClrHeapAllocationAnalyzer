using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ClrHeapAllocationAnalyzer
{
    internal class HotPathAnalysis
    {
        public static EnabledRules GetEnabledRules(ImmutableArray<DiagnosticDescriptor> supportedDiagnostics,
          SyntaxNodeAnalysisContext context)
        {
            if (!ShouldAnalyze(false, context))
            {
                return EnabledRules.None;
            }

            var allDiagnostrics = supportedDiagnostics.ToDictionary(x => x.Id, x => x);
            return new EnabledRules(allDiagnostrics);
        }

        private static bool IsHotPathAttribute(AttributeData attribute)
        {
            return attribute.AttributeClass.ContainingNamespace.ToString() == "Microsoft.Diagnostics" &&
                attribute.AttributeClass.Name == "PerformanceCriticalAttribute";
        }

        /// <summary>
        /// Based on the hot path settings and the current context, should an
        /// analysis be performed?
        /// </summary>
        private static bool ShouldAnalyze(bool onlyReportOnHotPath,
            SyntaxNodeAnalysisContext context)
        {
            IEnumerable<AttributeData> hotPathAttributes =
                context.ContainingSymbol.GetAttributes().Where(IsHotPathAttribute);
            if (hotPathAttributes.Any())
            {
                // Always perform analysis regardless of setting when a hot path
                // is found.
                return true;
            }

            if (onlyReportOnHotPath)
            {
                // There was no hot path specified.
                return false;
            }

            if (context.ContainingSymbol.ContainingType == null)
            {
                // Happens for scripts and snippets.
                return true;
            }

            // Check for other members in the type for hot paths. If there is
            // one, lets not do any analysis for the current context.
            foreach (ISymbol member in context.ContainingSymbol.ContainingType.GetMembers())
            {
                if (ReferenceEquals(context.ContainingSymbol, member))
                {
                    // Already checked above.
                    continue;
                }

                if (member.GetAttributes().Any(IsHotPathAttribute))
                {
                    return false;
                }
            }

            // The containing type does not have any other member with a hot
            // path attribute -> do analysis.
            return true;
        }
    }
}
