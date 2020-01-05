using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClrHeapAllocationAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidAllocationWithEnumerableEmptyCodeFix)), Shared]
    public class AvoidAllocationWithEnumerableEmptyCodeFix : CodeFixProvider
    {
        private const string RemoveUnnecessaryListCreation = "Avoid allocation by using Enumerable.Empty<>()";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(ExplicitAllocationAnalyzer.NewObjectRule.Id, ExplicitAllocationAnalyzer.NewArrayRule.Id);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);

            if (IsReturnStatement(node) == false)
            {
                return;
            }

            switch (node)
            {
                case ObjectCreationExpressionSyntax objectCreation:
                {
                    var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
                    if (IsInsideMemberReturningEnumerable(node, semanticModel) && CanBeReplaceWithEnumerableEmpty(objectCreation, semanticModel))
                    {
                        if (objectCreation.Type is GenericNameSyntax genericName)
                        {
                            var codeAction = CodeAction.Create(RemoveUnnecessaryListCreation, token => Transform(context.Document, node, genericName.TypeArgumentList.Arguments[0], token), RemoveUnnecessaryListCreation);
                            context.RegisterCodeFix(codeAction, diagnostic);
                        }
                    }
                }
                break;

                case ArrayCreationExpressionSyntax arrayCreation:
                {
                    var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
                    if (IsInsideMethodReturningEnumerable(node, semanticModel) && CanBeReplaceWithEnumerableEmpty(arrayCreation))
                    {
                        var codeAction = CodeAction.Create(RemoveUnnecessaryListCreation, token => Transform(context.Document, node, arrayCreation.Type.ElementType, token), RemoveUnnecessaryListCreation);
                        context.RegisterCodeFix(codeAction, diagnostic);
                    }
                }
                break;
            }
        }

        private static bool IsReturnStatement(SyntaxNode node)
        {
            return node.Parent is ReturnStatementSyntax || node.Parent is YieldStatementSyntax || node.Parent is ArrowExpressionClauseSyntax;
        }

        private bool IsInsideMemberReturningEnumerable(SyntaxNode node, SemanticModel semanticModel)
        {
            return IsInsideMethodReturningEnumerable(node, semanticModel) ||
                   IsInsidePropertyDeclaration(node, semanticModel);

        }

        private bool IsInsidePropertyDeclaration(SyntaxNode node, SemanticModel semanticModel)
        {
            if(node.FindContainer<PropertyDeclarationSyntax>() is PropertyDeclarationSyntax propertyDeclaration && 
               IsTypeIEnumerable(semanticModel, propertyDeclaration.Type))
            {
                return IsAutoPropertyReturningEnumerable(node) || IsArrowExpressionReturningEnumerable(node);
            }

            return false;
        }

        private bool IsAutoPropertyReturningEnumerable(SyntaxNode node)
        {
            if(node.FindContainer<AccessorDeclarationSyntax>() is AccessorDeclarationSyntax accessorDeclaration)
            {
                return accessorDeclaration.Keyword.Text == "get";
            }

            return false;
        }
        
        private bool IsArrowExpressionReturningEnumerable(SyntaxNode node)
        {
            return node.FindContainer<ArrowExpressionClauseSyntax>() != null;
        }

        private bool CanBeReplaceWithEnumerableEmpty(ArrayCreationExpressionSyntax arrayCreation)
        {
            return IsInitializationBlockEmpty(arrayCreation.Initializer);
        }

        private bool CanBeReplaceWithEnumerableEmpty(ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel)
        {
            return IsCollectionType(semanticModel, objectCreation) &&
                   IsInitializationBlockEmpty(objectCreation.Initializer) &&
                   IsCopyConstructor(semanticModel, objectCreation) == false;
        }

        private static bool IsInsideMethodReturningEnumerable(SyntaxNode node, SemanticModel semanticModel)
        {
            if (node.FindContainer<MethodDeclarationSyntax>() is MethodDeclarationSyntax methodDeclaration)
            {
                if (IsReturnTypeIEnumerable(semanticModel, methodDeclaration))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<Document> Transform(Document contextDocument, SyntaxNode node,  TypeSyntax typeArgument, CancellationToken cancellationToken)
        {
            var noAllocation = SyntaxFactory.ParseExpression($"Enumerable.Empty<{typeArgument}>()");
            var newNode = ReplaceExpression(node, noAllocation);
            if (newNode == null)
            {
                return contextDocument;
            }
            var syntaxRootAsync = await contextDocument.GetSyntaxRootAsync(cancellationToken);
            var newSyntaxRoot = syntaxRootAsync.ReplaceNode(node.Parent, newNode);
            return contextDocument.WithSyntaxRoot(newSyntaxRoot);
        }

        private SyntaxNode ReplaceExpression(SyntaxNode node, ExpressionSyntax newExpression)
        {
            switch (node.Parent)
            {
                case ReturnStatementSyntax parentReturn:
                    return parentReturn.WithExpression(newExpression);
                case ArrowExpressionClauseSyntax arrowStatement:
                    return arrowStatement.WithExpression(newExpression);
                default:
                    return null;
            }
        }

        private bool IsCopyConstructor(SemanticModel semanticModel, ObjectCreationExpressionSyntax objectCreation)
        {
            if (objectCreation.ArgumentList == null || objectCreation.ArgumentList.Arguments.Count == 0)
            {
                return false;
            }

            if (semanticModel.GetSymbolInfo(objectCreation).Symbol is IMethodSymbol methodSymbol)
            {
                if (methodSymbol.Parameters.Any(x=> x.Type is INamedTypeSymbol namedType && IsCollectionType(namedType)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsInitializationBlockEmpty(InitializerExpressionSyntax initializer)
        {
            return initializer == null || initializer.Expressions.Count == 0;
        }

        private bool IsCollectionType(SemanticModel semanticModel, ObjectCreationExpressionSyntax objectCreationExpressionSyntax)
        {
            return semanticModel.GetTypeInfo(objectCreationExpressionSyntax).Type is INamedTypeSymbol createdType  && 
                   (createdType.TypeKind == TypeKind.Array ||  IsCollectionType(createdType) );
        }

        private bool IsCollectionType(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.ConstructedFrom.Interfaces.Any(x =>
                x.IsGenericType && x.ToString().StartsWith("System.Collections.Generic.ICollection"));
        }

        private static bool IsReturnTypeIEnumerable(SemanticModel semanticModel, MethodDeclarationSyntax methodDeclarationSyntax)
        {
            var typeSyntax = methodDeclarationSyntax.ReturnType;
            return IsTypeIEnumerable(semanticModel, typeSyntax);
        }

        private static bool IsTypeIEnumerable(SemanticModel semanticModel, TypeSyntax typeSyntax)
        {
            var returnType = ModelExtensions.GetTypeInfo(semanticModel, typeSyntax).Type as INamedTypeSymbol;
            var ienumerable = semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
            return returnType != null && ienumerable.Equals(returnType.ConstructedFrom);
        }
    }

    static class SyntaxHelper
    {
        public static T FindContainer<T>(this SyntaxNode tokenParent) where T : SyntaxNode
        {
            if (tokenParent is T invocation)
            {
                return invocation;
            }

            return tokenParent.Parent == null ? null : FindContainer<T>(tokenParent.Parent);
        }
    }
}
