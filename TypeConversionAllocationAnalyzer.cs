namespace ClrHeapAllocationAnalyzer
{
    using System;
    using System.Collections.Immutable;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TypeConversionAllocationAnalyzer : ISyntaxNodeAnalyzer<SyntaxKind>
    {
        internal static DiagnosticDescriptor ValueTypeToReferenceTypeConversionRule = new DiagnosticDescriptor("HeapAnalyzerBoxingRule", "Value type to reference type conversion causing boxing allocation", "Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable", "Performance", DiagnosticSeverity.Warning, true);

        internal static DiagnosticDescriptor DelegateOnStructInstanceRule = new DiagnosticDescriptor("HeapAnalyzerDelegateOnStructRule", "Delegate on struct instance caused a boxing allocation", "Struct instance method being used for delegate creation, this will result in a boxing instruction", "Performance", DiagnosticSeverity.Warning, true);

        internal static DiagnosticDescriptor MethodGroupAllocationRule = new DiagnosticDescriptor("HeapAnalyzerMethodGroupAllocationRule", "Delegate allocation from a method group", "This will allocate a delegate instance", "Performance", DiagnosticSeverity.Warning, true);

        internal static object[] EmptyMessageArgs = { };

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(ValueTypeToReferenceTypeConversionRule, DelegateOnStructInstanceRule, MethodGroupAllocationRule);
            }
        }

        public ImmutableArray<SyntaxKind> SyntaxKindsOfInterest
        {
            get
            {
                return ImmutableArray.Create(new[] {
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxKind.ReturnStatement,
                    SyntaxKind.YieldReturnStatement,
                    SyntaxKind.CastExpression,
                    SyntaxKind.AsExpression,
                    SyntaxKind.CoalesceExpression,
                    SyntaxKind.ConditionalExpression,
                    SyntaxKind.ForEachStatement,
                    SyntaxKind.EqualsValueClause,
                    SyntaxKind.Argument});
            }
        }

        public void AnalyzeNode(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, AnalyzerOptions options, CancellationToken cancellationToken)
        {
            string filePath = node.SyntaxTree.FilePath;

            // this.fooObjCall(10);
            // new myobject(10);
            if (node is ArgumentSyntax)
            {
                ArgumentSyntaxCheck(node, semanticModel, addDiagnostic, filePath);
            }

            // object foo { get { return 0; } }
            if (node is ReturnStatementSyntax)
            {
                ReturnStatementExpressionCheck(node, semanticModel, addDiagnostic, filePath);
                return;
            }

            // yield return 0
            if (node is YieldStatementSyntax)
            {
                YieldReturnStatementExpressionCheck(node, semanticModel, addDiagnostic, filePath);
                return;
            }

            // object a = x ?? 0;
            // var a = 10 as object;
            if (node is BinaryExpressionSyntax)
            {
                BinaryExpressionCheck(node, semanticModel, addDiagnostic, filePath);
                return;
            }

            // foreach (var a in new[] ...)
            if (node is ForEachStatementSyntax)
            {
                ForEachExpressionCheck(node, semanticModel, addDiagnostic, filePath);
                return;
            }

            // for (object i = 0;;)
            if (node is EqualsValueClauseSyntax)
            {
                EqualsValueClauseCheck(node, semanticModel, addDiagnostic, filePath);
                return;
            }

            // object = true ? 0 : obj
            if (node is ConditionalExpressionSyntax)
            {
                ConditionalExpressionCheck(node, semanticModel, addDiagnostic, filePath);
                return;
            }

            // var f = (object)
            if (node is CastExpressionSyntax)
            {
                CastExpressionCheck(node, semanticModel, addDiagnostic, filePath);
                return;
            }
        }

        private static void ReturnStatementExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, string filePath)
        {
            var returnStatementExpression = node as ReturnStatementSyntax;
            if (returnStatementExpression.Expression != null)
            {
                var returnTypeInfo = semanticModel.GetTypeInfo(returnStatementExpression.Expression);
                CheckTypeConversion(returnTypeInfo, addDiagnostic, returnStatementExpression.Expression.GetLocation(), filePath);
            }
        }

        private static void YieldReturnStatementExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, string filePath)
        {
            var yieldExpression = node as YieldStatementSyntax;
            if (yieldExpression.Expression != null)
            {
                var returnTypeInfo = semanticModel.GetTypeInfo(yieldExpression.Expression);
                CheckTypeConversion(returnTypeInfo, addDiagnostic, yieldExpression.Expression.GetLocation(), filePath);
            }
        }

        private static void ArgumentSyntaxCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, string filePath)
        {
            var argument = node as ArgumentSyntax;
            if (argument.Expression != null)
            {
                var argumentTypeInfo = semanticModel.GetTypeInfo(argument.Expression);
                CheckTypeConversion(argumentTypeInfo, addDiagnostic, argument.Expression.GetLocation(), filePath);
                CheckDelegateCreation(argument.Expression, argumentTypeInfo, semanticModel, addDiagnostic, argument.Expression.GetLocation(), filePath);
            }
        }

        private static void BinaryExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, string filePath)
        {
            var binaryExpression = node as BinaryExpressionSyntax;

            // as expression
            if (binaryExpression.CSharpKind() == SyntaxKind.AsExpression && binaryExpression.Left != null && binaryExpression.Right != null)
            {
                var leftT = semanticModel.GetTypeInfo(binaryExpression.Left);
                var rightT = semanticModel.GetTypeInfo(binaryExpression.Right);

                if (leftT.Type != null && leftT.Type.IsValueType && rightT.Type != null && rightT.Type.IsReferenceType)
                {
                    addDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, binaryExpression.Left.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.BoxingAllocation(filePath);
                }

                return;
            }

            if (binaryExpression.Right != null)
            {
                var assignmentExprTypeInfo = semanticModel.GetTypeInfo(binaryExpression.Right);
                CheckTypeConversion(assignmentExprTypeInfo, addDiagnostic, binaryExpression.Right.GetLocation(), filePath);
                CheckDelegateCreation(binaryExpression.Right, assignmentExprTypeInfo, semanticModel, addDiagnostic, binaryExpression.Right.GetLocation(), filePath);
                return;
            }
        }

        private static void CastExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, string filePath)
        {
            var castExpression = node as CastExpressionSyntax;
            if (castExpression.Expression != null)
            {
                var castTypeInfo = semanticModel.GetTypeInfo(castExpression);
                var expressionTypeInfo = semanticModel.GetTypeInfo(castExpression.Expression);

                if (castTypeInfo.Type != null && expressionTypeInfo.Type != null && castTypeInfo.Type.IsReferenceType && expressionTypeInfo.Type.IsValueType)
                {
                    addDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, castExpression.Expression.GetLocation(), EmptyMessageArgs));
                }
            }
        }

        private static void ConditionalExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, string filePath)
        {
            var conditionalExpression = node as ConditionalExpressionSyntax;

            var trueExp = conditionalExpression.WhenTrue;
            var falseExp = conditionalExpression.WhenFalse;

            if (trueExp != null)
            {
                CheckTypeConversion(semanticModel.GetTypeInfo(trueExp), addDiagnostic, trueExp.GetLocation(), filePath);
            }

            if (falseExp != null)
            {
                CheckTypeConversion(semanticModel.GetTypeInfo(falseExp), addDiagnostic, falseExp.GetLocation(), filePath);
            }
        }

        private static void ForEachExpressionCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, string filePath)
        {
            var foreachExpression = node as ForEachStatementSyntax;
            if (foreachExpression.Expression != null)
            {
                var typeInfo = semanticModel.GetTypeInfo(foreachExpression.Expression);
                if (typeInfo.Type != null)
                {
                    var arraySymbol = typeInfo.Type as IArrayTypeSymbol;
                    if (arraySymbol != null && arraySymbol.ElementType.IsValueType)
                    {
                        addDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, foreachExpression.Expression.GetLocation(), EmptyMessageArgs));
                        return;
                    }

                    var namedTypeSymbol = typeInfo.Type as INamedTypeSymbol;
                    if (namedTypeSymbol != null && namedTypeSymbol.Arity == 1 && namedTypeSymbol.TypeArguments[0].IsValueType && foreachExpression.Type != null)
                    {
                        var leftHandType = semanticModel.GetTypeInfo(foreachExpression.Type).Type;
                        if (leftHandType != null && leftHandType.IsReferenceType)
                        {
                            addDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, foreachExpression.Expression.GetLocation(), EmptyMessageArgs));
                            return;
                        }
                    }
                }
            }
        }

        private static void EqualsValueClauseCheck(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, string filePath)
        {
            var initializer = node as EqualsValueClauseSyntax;
            if (initializer.Value != null)
            {
                var typeInfo = semanticModel.GetTypeInfo(initializer.Value);
                CheckTypeConversion(typeInfo, addDiagnostic, initializer.Value.GetLocation(), filePath);
                CheckDelegateCreation(initializer.Value, typeInfo, semanticModel, addDiagnostic, initializer.Value.GetLocation(), filePath);
            }
        }

        private static void CheckTypeConversion(TypeInfo typeInfo, Action<Diagnostic> addDiagnostic, Location location, string filePath)
        {
            if (typeInfo.Type != null && typeInfo.ConvertedType != null && typeInfo.Type.IsValueType && !typeInfo.ConvertedType.IsValueType)
            {
                addDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, location, EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.BoxingAllocation(filePath);
            }
        }

        private static void CheckDelegateCreation(SyntaxNode node, TypeInfo typeInfo, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, Location location, string filePath)
        {
            // special case: method groups
            if (typeInfo.ConvertedType != null && typeInfo.ConvertedType.TypeKind == TypeKind.Delegate)
            {
                // new Action<Foo>(MethodGroup); should skip this one
                if (node is ParenthesizedLambdaExpressionSyntax || node is SimpleLambdaExpressionSyntax || node is AnonymousMethodExpressionSyntax || node is ObjectCreationExpressionSyntax || (node.Parent != null && node.Parent.Parent != null && node.Parent.Parent.Parent != null && node.Parent.Parent.Parent.CSharpKind() == SyntaxKind.ObjectCreationExpression))
                {
                    // skip this, because it's intended.
                }
                else
                {
                    if (node.CSharpKind() == SyntaxKind.IdentifierName)
                    {
                        var symbol = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
                        if (symbol != null)
                        {
                            addDiagnostic(Diagnostic.Create(MethodGroupAllocationRule, location, EmptyMessageArgs));
                            HeapAllocationAnalyzerEventSource.Logger.MethodGroupAllocation(filePath);
                        }
                    }
                }

                var symbolInfo = semanticModel.GetSymbolInfo(node).Symbol;
                if (symbolInfo != null && symbolInfo.ContainingType != null && symbolInfo.ContainingType.IsValueType)
                {
                    addDiagnostic(Diagnostic.Create(DelegateOnStructInstanceRule, location, EmptyMessageArgs));
                }
            }
        }
    }
}