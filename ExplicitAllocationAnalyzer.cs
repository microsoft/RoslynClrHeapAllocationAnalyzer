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
    public sealed class ExplicitAllocationAnalyzer : ISyntaxNodeAnalyzer<SyntaxKind>
    {
        internal static DiagnosticDescriptor NewArrayRule = new DiagnosticDescriptor("HeapAnalyzerExplicitNewArrayRule", "Explicit new array type allocation", "Explicit new array type allocation", "Performance", DiagnosticSeverity.Info, true);

        internal static DiagnosticDescriptor NewObjectRule = new DiagnosticDescriptor("HeapAnalyzerExplicitNewObjectRule", "Explicit new reference type allocation", "Explicit new reference type allocation", "Performance", DiagnosticSeverity.Info, true);

        internal static DiagnosticDescriptor AnonymousNewObjectRule = new DiagnosticDescriptor("HeapAnalyzerExplicitNewAnonymousObjectRule", "Explicit new anonymous object allocation", "Explicit new anonymous object allocation", "Performance", DiagnosticSeverity.Info, true, string.Empty, "http://msdn.microsoft.com/en-us/library/bb397696.aspx");

        internal static DiagnosticDescriptor ImplicitArrayCreationRule = new DiagnosticDescriptor("HeapAnalyzerImplicitNewArrayCreationRule", "Implicit new array creation allocation", "Implicit new array creation allocation", "Performance", DiagnosticSeverity.Info, true);

        internal static DiagnosticDescriptor InitializerCreationRule = new DiagnosticDescriptor("HeapAnalyzerInitializerCreationRule", "Initializer reference type allocation", "Initializer reference type allocation", "Performance", DiagnosticSeverity.Info, true);

        internal static DiagnosticDescriptor LetCauseRule = new DiagnosticDescriptor("HeapAnalyzerLetClauseRule", "Let clause induced allocation", "Let clause induced allocation", "Performance", DiagnosticSeverity.Info, true);

        internal static object[] EmptyMessageArgs = { };

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(new [] { LetCauseRule, InitializerCreationRule, ImplicitArrayCreationRule, AnonymousNewObjectRule, NewObjectRule, NewArrayRule });
            }
        }

        public ImmutableArray<SyntaxKind> SyntaxKindsOfInterest
        {
            get
            {
                return ImmutableArray.Create(new [] { SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.AnonymousObjectCreationExpression,
                    SyntaxKind.ArrayInitializerExpression,
                    SyntaxKind.CollectionInitializerExpression,
                    SyntaxKind.ComplexElementInitializerExpression,
                    SyntaxKind.ObjectInitializerExpression,
                    SyntaxKind.ArrayCreationExpression,
                    SyntaxKind.ImplicitArrayCreationExpression,
                    SyntaxKind.LetClause });
            }
        }

        public void AnalyzeNode(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, AnalyzerOptions options, CancellationToken cancellationToken)
        {
            string filePath = node.SyntaxTree.FilePath;

            var initializerExpression = node as InitializerExpressionSyntax;
            if (initializerExpression != null && node.Parent != null && node.Parent.IsKind(SyntaxKind.EqualsValueClause) && node.Parent.Parent != null && node.Parent.Parent.IsKind(SyntaxKind.VariableDeclarator))
            {
                addDiagnostic(Diagnostic.Create(InitializerCreationRule, ((VariableDeclaratorSyntax)node.Parent.Parent).Identifier.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewInitializerExpression(filePath);
                return;
            }

            var implicitArrayExpression = node as ImplicitArrayCreationExpressionSyntax;
            if (implicitArrayExpression != null)
            {
                addDiagnostic(Diagnostic.Create(ImplicitArrayCreationRule, implicitArrayExpression.NewKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewImplicitArrayCreationExpression(filePath);
                return;
            }

            var newAnon = node as AnonymousObjectCreationExpressionSyntax;
            if (newAnon != null)
            {
                addDiagnostic(Diagnostic.Create(AnonymousNewObjectRule, newAnon.NewKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewAnonymousObjectCreationExpression(filePath);
                return;
            }

            var newArr = node as ArrayCreationExpressionSyntax;
            if (newArr != null)
            {
                addDiagnostic(Diagnostic.Create(NewObjectRule, newArr.NewKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewArrayExpression(filePath);
                return;
            }

            var newObj = node as ObjectCreationExpressionSyntax;
            if (newObj != null)
            {
                var typeInfo = semanticModel.GetTypeInfo(newObj);
                if (typeInfo.ConvertedType != null && typeInfo.ConvertedType.TypeKind != TypeKind.Error && typeInfo.ConvertedType.IsReferenceType)
                {
                    addDiagnostic(Diagnostic.Create(NewObjectRule, newObj.NewKeyword.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.NewObjectCreationExpression(filePath);
                }

                return;
            }

            var letKind = node as LetClauseSyntax;
            if (letKind != null)
            {
                addDiagnostic(Diagnostic.Create(LetCauseRule, letKind.LetKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.LetClauseExpression(filePath);
                return;
            }
        }
    }
}