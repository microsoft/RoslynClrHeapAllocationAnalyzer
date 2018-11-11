using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ClrHeapAllocationAnalyzer
{
    internal class HotPathAnalysis
    {
        /// <summary>
        /// Maps rule ids to their respective hot path <see cref="AllocationKind"/>.
        /// </summary>
        private static readonly Dictionary<string, AllocationKind> idToKind = new Dictionary<string, AllocationKind> {
            { "HAA0501", AllocationKind.Explicit },
            { "HAA0502", AllocationKind.Explicit },
            { "HAA0503", AllocationKind.Explicit },
            { "HAA0504", AllocationKind.Explicit },
            { "HAA0505", AllocationKind.Explicit },
            { "HAA0506", AllocationKind.Explicit },
        };

        public static EnabledRules GetEnabledRules(ImmutableArray<DiagnosticDescriptor> supportedDiagnostics,
          SyntaxNodeAnalysisContext context)
        {

            var hotPathAttributes = context.ContainingSymbol.GetAttributes().Where(IsHotPathAttribute).ToList();
            if (!ShouldAnalyze(false, hotPathAttributes, context))
            {
                return EnabledRules.None;
            }

            return GetRules(supportedDiagnostics, hotPathAttributes);
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
            IEnumerable<AttributeData> contextHotPathAttributes,
            SyntaxNodeAnalysisContext context)
        {
            if (contextHotPathAttributes.Any())
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
        
        private static EnabledRules GetRules(ImmutableArray<DiagnosticDescriptor> supportedDiagnostics,
            IList<AttributeData> hotPathAttributes)
        {
            // Aggregate all attributes.
            AllocationKind allocationTypes = 0;
            foreach (var attribute in hotPathAttributes)
            {
                PerformanceAttributeData data = ExtractAttributeData(attribute);
                allocationTypes |= data.Allocations;
            }
            
            // 
            Dictionary<string, DiagnosticDescriptor> rules = new Dictionary<string, DiagnosticDescriptor>();
            foreach (var diagnostic in supportedDiagnostics)
            {
                if (!idToKind.TryGetValue(diagnostic.Id, out var kind))
                {
                    // TODO: Should never happen when all kinds are added.
                    kind = AllocationKind.TheRest;
                }

                
                if (!allocationTypes.HasFlag(kind))
                {
                    
                    continue;
                }

                rules.Add(diagnostic.Id, diagnostic);
            }
            

            return new EnabledRules(rules);
        }

        private static PerformanceAttributeData ExtractAttributeData(AttributeData attribute)
        {
            AllocationKind allocations = AllocationKind.All;
            if (attribute.NamedArguments.Length == 0)
            {
                return new PerformanceAttributeData(allocations);
            }

            if (attribute.NamedArguments[0].Key == "Allocations")
            {
                allocations = (AllocationKind)attribute.NamedArguments[0].Value.Value;
            } else
            {
                throw new ArgumentException($"Unknown named argument {attribute.NamedArguments[0].Key}", nameof(attribute));
            }

            return new PerformanceAttributeData(allocations);
        }

        /// <summary>
        /// Helper struct that represents the data of a <see cref="PerformanceCriticalAttribute"/>.
        /// </summary>
        private struct PerformanceAttributeData
        {
            public AllocationKind Allocations { get; }
            
            public PerformanceAttributeData(AllocationKind allocations = AllocationKind.All)
            {
                Allocations = allocations;
            }
        }
    }
}
