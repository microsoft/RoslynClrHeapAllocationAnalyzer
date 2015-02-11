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

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(ValueTypeToReferenceTypeConversionRule, DelegateOnStructInstanceRule, MethodGroupAllocationRule);
            }
        }

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
            var cancellationToken = context.CancellationToken;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            string filePath = node.SyntaxTree.FilePath;

            // this.fooObjCall(10);
            // new myobject(10);
            if (node is ArgumentSyntax)
            {
                ArgumentSyntaxCheck(node, semanticModel, reportDiagnostic, filePath, cancellationToken);
            }

            // object foo { get { return 0; } }
            if (node is ReturnStatementSyntax)
            {
                ReturnStatementExpressionCheck(node, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // yield return 0
            if (node is YieldStatementSyntax)
            {
                YieldReturnStatementExpressionCheck(node, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // object a = x ?? 0;
            // var a = 10 as object;
            if (node is BinaryExpressionSyntax)
            {
                BinaryExpressionCheck(node, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // foreach (var a in new[] ...)
            if (node is ForEachStatementSyntax)
            {
                ForEachExpressionCheck(node, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // for (object i = 0;;)
            if (node is EqualsValueClauseSyntax)
            {
                EqualsValueClauseCheck(node, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // object = true ? 0 : obj
            if (node is ConditionalExpressionSyntax)
            {
                ConditionalExpressionCheck(node, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // var f = (object)
            if (node is CastExpressionSyntax)
            {
                CastExpressionCheck(node, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }
        }

        private static void ReturnStatementExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var returnStatementExpression = node as ReturnStatementSyntax;
            if (returnStatementExpression.Expression != null)
            {
                var returnTypeInfo = semanticModel.GetTypeInfo(returnStatementExpression.Expression, cancellationToken);
                CheckTypeConversion(returnTypeInfo, reportDiagnostic, returnStatementExpression.Expression.GetLocation(), filePath);
            }
        }

        private static void YieldReturnStatementExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var yieldExpression = node as YieldStatementSyntax;
            if (yieldExpression.Expression != null)
            {
                var returnTypeInfo = semanticModel.GetTypeInfo(yieldExpression.Expression, cancellationToken);
                CheckTypeConversion(returnTypeInfo, reportDiagnostic, yieldExpression.Expression.GetLocation(), filePath);
            }
        }

        private static void ArgumentSyntaxCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var argument = node as ArgumentSyntax;
            if (argument.Expression != null)
            {
                var argumentTypeInfo = semanticModel.GetTypeInfo(argument.Expression, cancellationToken);
                CheckTypeConversion(argumentTypeInfo, reportDiagnostic, argument.Expression.GetLocation(), filePath);
                CheckDelegateCreation(argument.Expression, argumentTypeInfo, semanticModel, reportDiagnostic, argument.Expression.GetLocation(), filePath, cancellationToken);
            }
        }

        private static void BinaryExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var binaryExpression = node as BinaryExpressionSyntax;

            // as expression
            if (binaryExpression.Kind() == SyntaxKind.AsExpression && binaryExpression.Left != null && binaryExpression.Right != null)
            {
                var leftT = semanticModel.GetTypeInfo(binaryExpression.Left, cancellationToken);
                var rightT = semanticModel.GetTypeInfo(binaryExpression.Right, cancellationToken);

                if (leftT.Type != null && leftT.Type.IsValueType && rightT.Type != null && rightT.Type.IsReferenceType)
                {
                    reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, binaryExpression.Left.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.BoxingAllocation(filePath);
                }

                return;
            }

            if (binaryExpression.Right != null)
            {
                var assignmentExprTypeInfo = semanticModel.GetTypeInfo(binaryExpression.Right, cancellationToken);
                CheckTypeConversion(assignmentExprTypeInfo, reportDiagnostic, binaryExpression.Right.GetLocation(), filePath);
                CheckDelegateCreation(binaryExpression.Right, assignmentExprTypeInfo, semanticModel, reportDiagnostic, binaryExpression.Right.GetLocation(), filePath, cancellationToken);
                return;
            }
        }

        private static void CastExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var castExpression = node as CastExpressionSyntax;
            if (castExpression.Expression != null)
            {
                var castTypeInfo = semanticModel.GetTypeInfo(castExpression, cancellationToken);
                var expressionTypeInfo = semanticModel.GetTypeInfo(castExpression.Expression, cancellationToken);

                if (castTypeInfo.Type != null && expressionTypeInfo.Type != null && castTypeInfo.Type.IsReferenceType && expressionTypeInfo.Type.IsValueType)
                {
                    reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, castExpression.Expression.GetLocation(), EmptyMessageArgs));
                }
            }
        }

        private static void ConditionalExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var conditionalExpression = node as ConditionalExpressionSyntax;

            var trueExp = conditionalExpression.WhenTrue;
            var falseExp = conditionalExpression.WhenFalse;

            if (trueExp != null)
            {
                CheckTypeConversion(semanticModel.GetTypeInfo(trueExp, cancellationToken), reportDiagnostic, trueExp.GetLocation(), filePath);
            }

            if (falseExp != null)
            {
                CheckTypeConversion(semanticModel.GetTypeInfo(falseExp, cancellationToken), reportDiagnostic, falseExp.GetLocation(), filePath);
            }
        }

        private static void ForEachExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var foreachExpression = node as ForEachStatementSyntax;
            if (foreachExpression.Expression != null)
            {
                var typeInfo = semanticModel.GetTypeInfo(foreachExpression.Expression, cancellationToken);
                if (typeInfo.Type != null)
                {
                    var arraySymbol = typeInfo.Type as IArrayTypeSymbol;
                    if (arraySymbol != null && arraySymbol.ElementType.IsValueType)
                    {
                        reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, foreachExpression.Expression.GetLocation(), EmptyMessageArgs));
                        return;
                    }

                    var namedTypeSymbol = typeInfo.Type as INamedTypeSymbol;
                    if (namedTypeSymbol != null && namedTypeSymbol.Arity == 1 && namedTypeSymbol.TypeArguments[0].IsValueType && foreachExpression.Type != null)
                    {
                        var leftHandType = semanticModel.GetTypeInfo(foreachExpression.Type, cancellationToken).Type;
                        if (leftHandType != null && leftHandType.IsValueType)
                        {
                            reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, foreachExpression.Expression.GetLocation(), EmptyMessageArgs));
                            return;
                        }
                    }
                }
            }
        }

        private static void EqualsValueClauseCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var initializer = node as EqualsValueClauseSyntax;
            if (initializer.Value != null)
            {
                var typeInfo = semanticModel.GetTypeInfo(initializer.Value, cancellationToken);
                CheckTypeConversion(typeInfo, reportDiagnostic, initializer.Value.GetLocation(), filePath);
                CheckDelegateCreation(initializer.Value, typeInfo, semanticModel, reportDiagnostic, initializer.Value.GetLocation(), filePath, cancellationToken);
            }
        }

        private static void CheckTypeConversion(TypeInfo typeInfo, Action<Diagnostic> reportDiagnostic, Location location, string filePath)
        {
            if (typeInfo.Type != null && typeInfo.ConvertedType != null && typeInfo.Type.IsValueType && !typeInfo.ConvertedType.IsValueType)
            {
                reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, location, EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.BoxingAllocation(filePath);
            }
        }

        private static void CheckDelegateCreation(SyntaxNode node, TypeInfo typeInfo, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, Location location, string filePath, CancellationToken cancellationToken)
        {
            // special case: method groups
            if (typeInfo.ConvertedType != null && typeInfo.ConvertedType.TypeKind == TypeKind.Delegate)
            {
                // new Action<Foo>(MethodGroup); should skip this one
                var insideObjectCreation = (node.Parent != null && node.Parent.Parent != null && node.Parent.Parent.Parent != null &&
                                            (node.Parent.Parent.Parent.Kind() == SyntaxKind.ObjectCreationExpression));
                if (node is ParenthesizedLambdaExpressionSyntax || node is SimpleLambdaExpressionSyntax ||
                    node is AnonymousMethodExpressionSyntax || node is ObjectCreationExpressionSyntax || insideObjectCreation)
                {
                    // skip this, because it's intended.
                }
                else
                {
                    if (node.Kind() == SyntaxKind.IdentifierName)
                    {
                        var symbol = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol as IMethodSymbol;
                        if (symbol != null)
                        {
                            reportDiagnostic(Diagnostic.Create(MethodGroupAllocationRule, location, EmptyMessageArgs));
                            HeapAllocationAnalyzerEventSource.Logger.MethodGroupAllocation(filePath);
                        }
                    }
                    else if (node.Kind() == SyntaxKind.SimpleMemberAccessExpression)
                    {
                        var memberAccess = node as MemberAccessExpressionSyntax;
                        var symbol = semanticModel.GetSymbolInfo(memberAccess.Name, cancellationToken).Symbol as IMethodSymbol;
                        if (symbol != null)
                        {
                            reportDiagnostic(Diagnostic.Create(MethodGroupAllocationRule, location, EmptyMessageArgs));
                            HeapAllocationAnalyzerEventSource.Logger.MethodGroupAllocation(filePath);
                        }
                    }
                }

                var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol;
                if (symbolInfo != null && symbolInfo.ContainingType != null && symbolInfo.ContainingType.IsValueType)
                {
                    reportDiagnostic(Diagnostic.Create(DelegateOnStructInstanceRule, location, EmptyMessageArgs));
                }
            }
        }
    }
}