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
    public sealed class ConcatenationAllocationAnalyzer : DiagnosticAnalyzer {
        public static readonly AllocationRuleDescription StringConcatenationAllocationRule =
            new AllocationRuleDescription("HAA0201", "Implicit string concatenation allocation", "Consider using StringBuilder", DiagnosticSeverity.Warning, "http://msdn.microsoft.com/en-us/library/2839d5h5(v=vs.110).aspx");

        public static readonly AllocationRuleDescription ValueTypeToReferenceTypeInAStringConcatenationRule =
            new AllocationRuleDescription("HAA0202", "Value type to reference type conversion allocation for string concatenation", "Value type ({0}) is being boxed to a reference type for a string concatenation.", DiagnosticSeverity.Warning, "http://msdn.microsoft.com/en-us/library/yz2be5wk.aspx");

        private static readonly string[] AllRules = { StringConcatenationAllocationRule.Id, ValueTypeToReferenceTypeInAStringConcatenationRule.Id };

        private static readonly object[] EmptyMessageArgs = { };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                AllocationRules.GetDescriptor(StringConcatenationAllocationRule.Id),
                AllocationRules.GetDescriptor(ValueTypeToReferenceTypeInAStringConcatenationRule.Id)
            );

        public ConcatenationAllocationAnalyzer()
        {
            AllocationRules.RegisterAnalyzerRule(StringConcatenationAllocationRule);
            AllocationRules.RegisterAnalyzerRule(ValueTypeToReferenceTypeInAStringConcatenationRule);
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context) {
            var enabledRules = AllocationRules.GetEnabled(AllRules);
            if (enabledRules.Any())
            {
                AnalyzeNode(context, enabledRules);
            }
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context, IReadOnlyDictionary<string, DiagnosticDescriptor> enabledRules)
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

                    if (enabledRules.ContainsKey(ValueTypeToReferenceTypeInAStringConcatenationRule.Id))
                    {
                        CheckForTypeConversion(enabledRules[ValueTypeToReferenceTypeInAStringConcatenationRule.Id], binaryExpression.Left, left, reportDiagnostic, filePath);
                        CheckForTypeConversion(enabledRules[ValueTypeToReferenceTypeInAStringConcatenationRule.Id], binaryExpression.Right, right, reportDiagnostic, filePath);
                    }

                    // regular string allocation
                    if (enabledRules.ContainsKey(StringConcatenationAllocationRule.Id))
                    {
                        if (left.Type != null && left.Type.SpecialType == SpecialType.System_String || right.Type != null && right.Type.SpecialType == SpecialType.System_String)
                        {
                            reportDiagnostic(Diagnostic.Create(enabledRules[StringConcatenationAllocationRule.Id], binaryExpression.OperatorToken.GetLocation(), EmptyMessageArgs));
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