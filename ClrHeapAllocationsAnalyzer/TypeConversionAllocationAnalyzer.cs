namespace ClrHeapAllocationAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;


    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TypeConversionAllocationAnalyzer : DiagnosticAnalyzer
    {
        public static DiagnosticDescriptor ValueTypeToReferenceTypeConversionRule = new DiagnosticDescriptor("HeapAnalyzerBoxingRule", "Value type to reference type conversion causing boxing allocation", "Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable", "Performance", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor DelegateOnStructInstanceRule = new DiagnosticDescriptor("HeapAnalyzerDelegateOnStructRule", "Delegate on struct instance caused a boxing allocation", "Struct instance method being used for delegate creation, this will result in a boxing instruction", "Performance", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor MethodGroupAllocationRule = new DiagnosticDescriptor("HeapAnalyzerMethodGroupAllocationRule", "Delegate allocation from a method group", "This will allocate a delegate instance", "Performance", DiagnosticSeverity.Warning, true);

        internal static object[] EmptyMessageArgs = { };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ValueTypeToReferenceTypeConversionRule, DelegateOnStructInstanceRule, MethodGroupAllocationRule);

        public override void Initialize(AnalysisContext context)
        {
            var kinds = new[]
            {
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxKind.ReturnStatement,
                SyntaxKind.YieldReturnStatement,
                SyntaxKind.CastExpression,
                SyntaxKind.AsExpression,
                SyntaxKind.CoalesceExpression,
                SyntaxKind.ConditionalExpression,
                SyntaxKind.ForEachStatement,
                SyntaxKind.EqualsValueClause,
                SyntaxKind.Argument
            };
            context.RegisterSyntaxNodeAction(AnalyzeNode, kinds);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            string filePath = node.SyntaxTree.FilePath;

            // this.fooObjCall(10);
            // new myobject(10);
            if (node is ArgumentSyntax)
            {
                ArgumentSyntaxCheck(node, semanticModel, reportDiagnostic, filePath);
            }

            // object foo { get { return 0; } }
            if (node is ReturnStatementSyntax)
            {
                ReturnStatementExpressionCheck(node, semanticModel, reportDiagnostic, filePath);
                return;
            }

            // yield return 0
            if (node is YieldStatementSyntax)
            {
                YieldReturnStatementExpressionCheck(node, semanticModel, reportDiagnostic, filePath);
                return;
            }

            // object a = x ?? 0;
            // var a = 10 as object;
            if (node is BinaryExpressionSyntax)
            {
                BinaryExpressionCheck(node, semanticModel, reportDiagnostic, filePath);
                return;
            }

            // for (object i = 0;;)
            if (node is EqualsValueClauseSyntax)
            {
                EqualsValueClauseCheck(node, semanticModel, reportDiagnostic, filePath);
                return;
            }

            // object = true ? 0 : obj
            if (node is ConditionalExpressionSyntax)
            {
                ConditionalExpressionCheck(node, semanticModel, reportDiagnostic, filePath);
                return;
            }

            // var f = (object)
            if (node is CastExpressionSyntax)
            {
                CastExpressionCheck(node, semanticModel, reportDiagnostic, filePath);
                return;
            }
        }

        private static void ReturnStatementExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath)
        {
            var returnStatementExpression = node as ReturnStatementSyntax;
            if (returnStatementExpression.Expression != null)
            {
                var returnTypeInfo = semanticModel.GetTypeInfo(returnStatementExpression.Expression);
                CheckTypeConversion(returnTypeInfo, reportDiagnostic, returnStatementExpression.Expression.GetLocation(), filePath);
            }
        }

        private static void YieldReturnStatementExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath)
        {
            var yieldExpression = node as YieldStatementSyntax;
            if (yieldExpression.Expression != null)
            {
                var returnTypeInfo = semanticModel.GetTypeInfo(yieldExpression.Expression);
                CheckTypeConversion(returnTypeInfo, reportDiagnostic, yieldExpression.Expression.GetLocation(), filePath);
            }
        }

        private static void ArgumentSyntaxCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath)
        {
            var argument = node as ArgumentSyntax;
            if (argument.Expression != null)
            {
                var argumentTypeInfo = semanticModel.GetTypeInfo(argument.Expression);
                CheckTypeConversion(argumentTypeInfo, reportDiagnostic, argument.Expression.GetLocation(), filePath);
                CheckDelegateCreation(argument.Expression, argumentTypeInfo, semanticModel, reportDiagnostic, argument.Expression.GetLocation(), filePath);
            }
        }

        private static void BinaryExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath)
        {
            var binaryExpression = node as BinaryExpressionSyntax;

            // as expression
            if (binaryExpression.IsKind(SyntaxKind.AsExpression) && binaryExpression.Left != null && binaryExpression.Right != null)
            {
                var leftT = semanticModel.GetTypeInfo(binaryExpression.Left);
                var rightT = semanticModel.GetTypeInfo(binaryExpression.Right);

                if (leftT.Type?.IsValueType == true && rightT.Type?.IsReferenceType == true)
                {
                    reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, binaryExpression.Left.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.BoxingAllocation(filePath);
                }

                return;
            }

            if (binaryExpression.Right != null)
            {
                var assignmentExprTypeInfo = semanticModel.GetTypeInfo(binaryExpression.Right);
                CheckTypeConversion(assignmentExprTypeInfo, reportDiagnostic, binaryExpression.Right.GetLocation(), filePath);
                CheckDelegateCreation(binaryExpression.Right, assignmentExprTypeInfo, semanticModel, reportDiagnostic, binaryExpression.Right.GetLocation(), filePath);
                return;
            }
        }

        private static void CastExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath)
        {
            var castExpression = node as CastExpressionSyntax;
            if (castExpression.Expression != null)
            {
                var castTypeInfo = semanticModel.GetTypeInfo(castExpression);
                var expressionTypeInfo = semanticModel.GetTypeInfo(castExpression.Expression);

                if (castTypeInfo.Type?.IsReferenceType == true && expressionTypeInfo.Type?.IsValueType == true)
                {
                    reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, castExpression.Expression.GetLocation(), EmptyMessageArgs));
                }
            }
        }

        private static void ConditionalExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath)
        {
            var conditionalExpression = node as ConditionalExpressionSyntax;

            var trueExp = conditionalExpression.WhenTrue;
            var falseExp = conditionalExpression.WhenFalse;

            if (trueExp != null)
            {
                CheckTypeConversion(semanticModel.GetTypeInfo(trueExp), reportDiagnostic, trueExp.GetLocation(), filePath);
            }

            if (falseExp != null)
            {
                CheckTypeConversion(semanticModel.GetTypeInfo(falseExp), reportDiagnostic, falseExp.GetLocation(), filePath);
            }
        }

        private static void EqualsValueClauseCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath)
        {
            var initializer = node as EqualsValueClauseSyntax;
            if (initializer.Value != null)
            {
                var typeInfo = semanticModel.GetTypeInfo(initializer.Value);
                CheckTypeConversion(typeInfo, reportDiagnostic, initializer.Value.GetLocation(), filePath);
                CheckDelegateCreation(initializer.Value, typeInfo, semanticModel, reportDiagnostic, initializer.Value.GetLocation(), filePath);
            }
        }

        private static void CheckTypeConversion(TypeInfo typeInfo, Action<Diagnostic> reportDiagnostic, Location location, string filePath)
        {
            if (typeInfo.Type?.IsValueType == true && !(typeInfo.ConvertedType?.IsValueType == true))
            {
                reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, location, EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.BoxingAllocation(filePath);
            }
        }

        private static void CheckDelegateCreation(SyntaxNode node, TypeInfo typeInfo, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, Location location, string filePath)
        {
            // special case: method groups
            if (typeInfo.ConvertedType?.TypeKind == TypeKind.Delegate)
            {
                // new Action<Foo>(MethodGroup); should skip this one
                var insideObjectCreation = node?.Parent?.Parent?.Parent?.Kind() == SyntaxKind.ObjectCreationExpression;
                if (node is ParenthesizedLambdaExpressionSyntax || node is SimpleLambdaExpressionSyntax ||
                    node is AnonymousMethodExpressionSyntax || node is ObjectCreationExpressionSyntax ||
                    insideObjectCreation)
                {
                    // skip this, because it's intended.
                }
                else
                {
                    if (node.IsKind(SyntaxKind.IdentifierName))
                    {
                        var symbol = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
                        if (symbol != null)
                        {
                            reportDiagnostic(Diagnostic.Create(MethodGroupAllocationRule, location, EmptyMessageArgs));
                            HeapAllocationAnalyzerEventSource.Logger.MethodGroupAllocation(filePath);
                        }
                    }
                    else if (node.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    {
                        var memberAccess = node as MemberAccessExpressionSyntax;
                        var symbol = semanticModel.GetSymbolInfo(memberAccess.Name).Symbol as IMethodSymbol;
                        if (symbol != null)
                        {
                            reportDiagnostic(Diagnostic.Create(MethodGroupAllocationRule, location, EmptyMessageArgs));
                            HeapAllocationAnalyzerEventSource.Logger.MethodGroupAllocation(filePath);
                        }
                    }
                }

                var symbolInfo = semanticModel.GetSymbolInfo(node).Symbol;
                if (symbolInfo?.ContainingType?.IsValueType == true && !insideObjectCreation)
                {
                    reportDiagnostic(Diagnostic.Create(DelegateOnStructInstanceRule, location, EmptyMessageArgs));
                }
            }
        }
    }
}