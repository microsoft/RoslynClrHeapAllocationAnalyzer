namespace ClrHeapAllocationAnalyzer
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConcatenationAllocationAnalyzer : DiagnosticAnalyzer
    {
        public static DiagnosticDescriptor StringConcatenationAllocationRule = new DiagnosticDescriptor("HeapAnalyzerStringConcatRule", "Implicit string concatenation allocation", "Considering using StringBuilder", "Performance", DiagnosticSeverity.Warning, true, string.Empty, "http://msdn.microsoft.com/en-us/library/2839d5h5(v=vs.110).aspx");

        public static DiagnosticDescriptor ValueTypeToReferenceTypeInAStringConcatenationRule = new DiagnosticDescriptor("HeapAnalyzerBoxingRule", "Value type to reference type conversion allocation for string concatenation", "Value type ({0}) is being boxed to a reference type for a string concatenation.", "Performance", DiagnosticSeverity.Warning, true, string.Empty, "http://msdn.microsoft.com/en-us/library/yz2be5wk.aspx");

        internal static object[] EmptyMessageArgs = { };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(StringConcatenationAllocationRule, ValueTypeToReferenceTypeInAStringConcatenationRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
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
                    CheckForTypeConversion(binaryExpression.Left, left, reportDiagnostic, filePath);

                    var right = semanticModel.GetTypeInfo(binaryExpression.Right, cancellationToken);
                    CheckForTypeConversion(binaryExpression.Right, right, reportDiagnostic, filePath);

                    // regular string allocation
                    if (left.Type != null && left.Type.SpecialType == SpecialType.System_String || right.Type != null && right.Type.SpecialType == SpecialType.System_String)
                    {
                        reportDiagnostic(Diagnostic.Create(StringConcatenationAllocationRule, binaryExpression.OperatorToken.GetLocation(), EmptyMessageArgs));
                        HeapAllocationAnalyzerEventSource.Logger.StringConcatenationAllocation(filePath);
                    }
                }
            }
        }

        private static void CheckForTypeConversion(ExpressionSyntax expression, TypeInfo typeInfo, Action<Diagnostic> reportDiagnostic, string filePath)
        {
            if (typeInfo.Type != null && typeInfo.Type.IsValueType && typeInfo.ConvertedType != null && !typeInfo.ConvertedType.IsValueType)
            {
                reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeInAStringConcatenationRule, expression.GetLocation(), new object[] { typeInfo.Type.ToDisplayString() }));
                HeapAllocationAnalyzerEventSource.Logger.BoxingAllocationInStringConcatenation(filePath);
            }
        }
    }
}