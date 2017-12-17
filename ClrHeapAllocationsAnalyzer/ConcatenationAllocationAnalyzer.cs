using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConcatenationAllocationAnalyzer : AllocationAnalyzer {
        protected override string[] Rules => new[] { AllocationRules.StringConcatenationAllocationRule.Id, AllocationRules.ValueTypeToReferenceTypeInAStringConcatenationRule.Id };

        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression };

        private static readonly object[] EmptyMessageArgs = { };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                AllocationRules.GetDescriptor(AllocationRules.StringConcatenationAllocationRule.Id),
                AllocationRules.GetDescriptor(AllocationRules.ValueTypeToReferenceTypeInAStringConcatenationRule.Id)
            );

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context, IReadOnlyDictionary<string, DiagnosticDescriptor> enabledRules)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            var cancellationToken = context.CancellationToken;
            string filePath = node.SyntaxTree.FilePath;
            var binaryExpressions = node.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().Reverse(); // need inner most expressions

            foreach (var binaryExpression in binaryExpressions)
            {
                if (binaryExpression.Left != null && binaryExpression.Right != null)
                {
                    var left = semanticModel.GetTypeInfo(binaryExpression.Left, cancellationToken);
                    var right = semanticModel.GetTypeInfo(binaryExpression.Right, cancellationToken);

                    if (enabledRules.ContainsKey(AllocationRules.ValueTypeToReferenceTypeInAStringConcatenationRule.Id))
                    {
                        CheckForTypeConversion(enabledRules[AllocationRules.ValueTypeToReferenceTypeInAStringConcatenationRule.Id], binaryExpression.Left, left, reportDiagnostic, filePath);
                        CheckForTypeConversion(enabledRules[AllocationRules.ValueTypeToReferenceTypeInAStringConcatenationRule.Id], binaryExpression.Right, right, reportDiagnostic, filePath);
                    }

                    // regular string allocation
                    if (enabledRules.ContainsKey(AllocationRules.StringConcatenationAllocationRule.Id))
                    {
                        if (left.Type != null && left.Type.SpecialType == SpecialType.System_String || right.Type != null && right.Type.SpecialType == SpecialType.System_String)
                        {
                            reportDiagnostic(Diagnostic.Create(enabledRules[AllocationRules.StringConcatenationAllocationRule.Id], binaryExpression.OperatorToken.GetLocation(), EmptyMessageArgs));
                            HeapAllocationAnalyzerEventSource.Logger.StringConcatenationAllocation(filePath);
                        }
                    }
                }
            }
        }

        private static void CheckForTypeConversion(DiagnosticDescriptor rule, ExpressionSyntax expression, TypeInfo typeInfo, Action<Diagnostic> reportDiagnostic, string filePath)
        {
            if (typeInfo.Type != null && typeInfo.Type.IsValueType && typeInfo.ConvertedType != null && !typeInfo.ConvertedType.IsValueType)
            {
                reportDiagnostic(Diagnostic.Create(rule, expression.GetLocation(), new object[] { typeInfo.Type.ToDisplayString() }));
                HeapAllocationAnalyzerEventSource.Logger.BoxingAllocationInStringConcatenation(filePath);
            }
        }
    }
}