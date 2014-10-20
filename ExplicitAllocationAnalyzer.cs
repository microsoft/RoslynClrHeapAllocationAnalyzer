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
                return ImmutableArray.Create(new [] 
                    {
                        SyntaxKind.ObjectCreationExpression,            // Used
                        SyntaxKind.AnonymousObjectCreationExpression,   // Used
                        SyntaxKind.ArrayInitializerExpression,          // Used (this is inside an ImplicitArrayCreationExpression)
                        SyntaxKind.CollectionInitializerExpression,     // Is this used anywhere?
                        SyntaxKind.ComplexElementInitializerExpression, // Is this used anywhere? For what this is see http://source.roslyn.codeplex.com/#Microsoft.CodeAnalysis.CSharp/Compilation/CSharpSemanticModel.cs,80
                        SyntaxKind.ObjectInitializerExpression,         // Used linked to InitializerExpressionSyntax
                        SyntaxKind.ArrayCreationExpression,             // Used
                        SyntaxKind.ImplicitArrayCreationExpression,     // Used (this then contains an ArrayInitializerExpression)
                        SyntaxKind.LetClause                            // Used
                    });
            }
        }

        public void AnalyzeNode(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, AnalyzerOptions options, CancellationToken cancellationToken)
        {
            string filePath = node.SyntaxTree.FilePath;

            // An InitializerExpressionSyntax has an ObjectCreationExpressionSyntax as it's parent, i.e
            // var testing = new TestClass { Name = "Bob" };
            //               |             |--------------| <- InitializerExpressionSyntax or SyntaxKind.ObjectInitializerExpression
            //               |----------------------------| <- ObjectCreationExpressionSyntax or SyntaxKind.ObjectCreationExpression
            var initializerExpression = node as InitializerExpressionSyntax;
            if (initializerExpression != null && node.Parent is ObjectCreationExpressionSyntax)
            {
                var objectCreation = node.Parent as ObjectCreationExpressionSyntax;
                var typeInfo = semanticModel.GetTypeInfo(objectCreation);
                if (typeInfo.ConvertedType != null && typeInfo.ConvertedType.TypeKind != TypeKind.Error && typeInfo.ConvertedType.IsReferenceType &&
                    objectCreation.Parent != null && objectCreation.Parent.IsKind(SyntaxKind.EqualsValueClause) &&
                    objectCreation.Parent.Parent != null && objectCreation.Parent.Parent.IsKind(SyntaxKind.VariableDeclarator))
                {
                    addDiagnostic(Diagnostic.Create(InitializerCreationRule, ((VariableDeclaratorSyntax)objectCreation.Parent.Parent).Identifier.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.NewInitializerExpression(filePath);
                    return;
                }
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
                addDiagnostic(Diagnostic.Create(NewArrayRule, newArr.NewKeyword.GetLocation(), EmptyMessageArgs));
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