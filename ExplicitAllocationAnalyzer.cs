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
        internal static DiagnosticDescriptor NewObjectRule = new DiagnosticDescriptor("Explicit new reference type allocation", string.Empty, string.Empty, "Performance", DiagnosticSeverity.Info, true);

        internal static DiagnosticDescriptor AnonymousNewObjectRule = new DiagnosticDescriptor("Explicit new anonymous type allocation", string.Empty, string.Empty, "Performance", DiagnosticSeverity.Info, true, string.Empty, "http://msdn.microsoft.com/en-us/library/bb397696.aspx");

        internal static DiagnosticDescriptor ImplicitArrayCreationRule = new DiagnosticDescriptor("Implicit new array creation allocation", string.Empty, string.Empty, "Performance", DiagnosticSeverity.Info, true);

        internal static DiagnosticDescriptor InitializerCreationRule = new DiagnosticDescriptor("Initializer reference type allocation", string.Empty, string.Empty, "Performance", DiagnosticSeverity.Info, true);

        internal static DiagnosticDescriptor LetCauseRule = new DiagnosticDescriptor("Let clause induced allocation", string.Empty, string.Empty, "Performance", DiagnosticSeverity.Info, true);

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(LetCauseRule, InitializerCreationRule, ImplicitArrayCreationRule, AnonymousNewObjectRule, NewObjectRule);
            }
        }

        public ImmutableArray<SyntaxKind> SyntaxKindsOfInterest
        {
            get
            {
                return ImmutableArray.Create(SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.AnonymousObjectCreationExpression,
                    SyntaxKind.ArrayInitializerExpression,
                    SyntaxKind.CollectionInitializerExpression,
                    SyntaxKind.ComplexElementInitializerExpression,
                    SyntaxKind.ObjectInitializerExpression,
                    SyntaxKind.ArrayCreationExpression,
                    SyntaxKind.ImplicitArrayCreationExpression,
                    SyntaxKind.LetClause);
            }
        }

        public void AnalyzeNode(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, AnalyzerOptions options, CancellationToken cancellationToken)
        {
            string filePath = node.SyntaxTree.FilePath;

            var initializerExpression = node as InitializerExpressionSyntax;
            if (initializerExpression != null && node.Parent != null && node.Parent.IsKind(SyntaxKind.EqualsValueClause) && node.Parent.Parent != null && node.Parent.Parent.IsKind(SyntaxKind.VariableDeclarator))
            {
                addDiagnostic(Diagnostic.Create(InitializerCreationRule, ((VariableDeclaratorSyntax)node.Parent.Parent).Identifier.GetLocation()));
                HeapAllocationAnalyzerEventSource.Logger.NewInitializerExpression(filePath);
                return;
            }

            var implicitArrayExpression = node as ImplicitArrayCreationExpressionSyntax;
            if (implicitArrayExpression != null)
            {
                addDiagnostic(Diagnostic.Create(ImplicitArrayCreationRule, implicitArrayExpression.NewKeyword.GetLocation()));
                HeapAllocationAnalyzerEventSource.Logger.NewImplicitArrayCreationExpression(filePath);
                return;
            }

            var newAnon = node as AnonymousObjectCreationExpressionSyntax;
            if (newAnon != null)
            {
                addDiagnostic(Diagnostic.Create(AnonymousNewObjectRule, newAnon.NewKeyword.GetLocation()));
                HeapAllocationAnalyzerEventSource.Logger.NewAnonymousObjectCreationExpression(filePath);
                return;
            }

            var newObj = node as ObjectCreationExpressionSyntax;
            if (newObj != null)
            {
                var typeInfo = semanticModel.GetTypeInfo(newObj);
                if (typeInfo.ConvertedType != null && typeInfo.ConvertedType.TypeKind != TypeKind.Error && typeInfo.ConvertedType.IsReferenceType)
                {
                    addDiagnostic(Diagnostic.Create(NewObjectRule, newObj.NewKeyword.GetLocation()));
                    HeapAllocationAnalyzerEventSource.Logger.NewObjectCreationExpression(filePath);
                }

                return;
            }

            var letKind = node as LetClauseSyntax;
            if (letKind != null)
            {
                addDiagnostic(Diagnostic.Create(LetCauseRule, letKind.LetKeyword.GetLocation()));
                HeapAllocationAnalyzerEventSource.Logger.LetClauseExpression(filePath);
                return;
            }
        }
    }
}