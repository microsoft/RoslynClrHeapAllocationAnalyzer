using System;
using System.Collections.Immutable;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ExplicitAllocationAnalyzer : AllocationAnalyzer
    {
        protected override SyntaxKind[] Expressions => new[] {
            SyntaxKind.ObjectCreationExpression,            // Used
            SyntaxKind.AnonymousObjectCreationExpression,   // Used
            SyntaxKind.ArrayInitializerExpression,          // Used (this is inside an ImplicitArrayCreationExpression)
            SyntaxKind.CollectionInitializerExpression,     // Is this used anywhere?
            SyntaxKind.ComplexElementInitializerExpression, // Is this used anywhere? For what this is see http://source.roslyn.codeplex.com/#Microsoft.CodeAnalysis.CSharp/Compilation/CSharpSemanticModel.cs,80
            SyntaxKind.ObjectInitializerExpression,         // Used linked to InitializerExpressionSyntax
            SyntaxKind.ArrayCreationExpression,             // Used
            SyntaxKind.ImplicitArrayCreationExpression,     // Used (this then contains an ArrayInitializerExpression)
            SyntaxKind.LetClause                            // Used
        };

        private static readonly object[] EmptyMessageArgs = { };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                AllocationRules.GetDescriptor(AllocationRules.LetCauseRule.Id),
                AllocationRules.GetDescriptor(AllocationRules.InitializerCreationRule.Id),
                AllocationRules.GetDescriptor(AllocationRules.ImplicitArrayCreationRule.Id),
                AllocationRules.GetDescriptor(AllocationRules.AnonymousNewObjectRule.Id),
                AllocationRules.GetDescriptor(AllocationRules.NewObjectRule.Id),
                AllocationRules.GetDescriptor(AllocationRules.NewArrayRule.Id)
            );

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context, EnabledRules rules)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            var cancellationToken = context.CancellationToken;
            string filePath = node.SyntaxTree.FilePath;

            // An InitializerExpressionSyntax has an ObjectCreationExpressionSyntax as it's parent, i.e
            // var testing = new TestClass { Name = "Bob" };
            //               |             |--------------| <- InitializerExpressionSyntax or SyntaxKind.ObjectInitializerExpression
            //               |----------------------------| <- ObjectCreationExpressionSyntax or SyntaxKind.ObjectCreationExpression
            if (rules.IsEnabled(AllocationRules.InitializerCreationRule.Id))
            {
                var initializerExpression = node as InitializerExpressionSyntax;
                if (initializerExpression?.Parent is ObjectCreationExpressionSyntax)
                {
                    var objectCreation = node.Parent as ObjectCreationExpressionSyntax;
                    var typeInfo = semanticModel.GetTypeInfo(objectCreation, cancellationToken);
                    if (typeInfo.ConvertedType?.TypeKind != TypeKind.Error &&
                        typeInfo.ConvertedType?.IsReferenceType == true &&
                        objectCreation.Parent?.IsKind(SyntaxKind.EqualsValueClause) == true &&
                        objectCreation.Parent?.Parent?.IsKind(SyntaxKind.VariableDeclarator) == true)
                    {
                        reportDiagnostic(Diagnostic.Create(rules.Get(AllocationRules.InitializerCreationRule.Id), ((VariableDeclaratorSyntax) objectCreation.Parent.Parent).Identifier.GetLocation(), EmptyMessageArgs));
                        HeapAllocationAnalyzerEventSource.Logger.NewInitializerExpression(filePath);
                        return;
                    }
                }
            }

            if (rules.IsEnabled(AllocationRules.ImplicitArrayCreationRule.Id) && node is ImplicitArrayCreationExpressionSyntax implicitArrayExpression)
            {
                reportDiagnostic(Diagnostic.Create(rules.Get(AllocationRules.ImplicitArrayCreationRule.Id), implicitArrayExpression.NewKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewImplicitArrayCreationExpression(filePath);
                return;
            }

            if (rules.IsEnabled(AllocationRules.AnonymousNewObjectRule.Id) && node is AnonymousObjectCreationExpressionSyntax newAnon)
            {
                reportDiagnostic(Diagnostic.Create(rules.Get(AllocationRules.AnonymousNewObjectRule.Id), newAnon.NewKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewAnonymousObjectCreationExpression(filePath);
                return;
            }

            if (rules.IsEnabled(AllocationRules.NewArrayRule.Id) && node is ArrayCreationExpressionSyntax newArr)
            {
                reportDiagnostic(Diagnostic.Create(rules.Get(AllocationRules.NewArrayRule.Id), newArr.NewKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewArrayExpression(filePath);
                return;
            }

            if (rules.IsEnabled(AllocationRules.NewObjectRule.Id) && node is ObjectCreationExpressionSyntax newObj)
            {
                var typeInfo = semanticModel.GetTypeInfo(newObj, cancellationToken);
                if (typeInfo.ConvertedType != null && typeInfo.ConvertedType.TypeKind != TypeKind.Error && typeInfo.ConvertedType.IsReferenceType)
                {
                    reportDiagnostic(Diagnostic.Create(rules.Get(AllocationRules.NewObjectRule.Id), newObj.NewKeyword.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.NewObjectCreationExpression(filePath);
                }
                return;
            }

            if (rules.IsEnabled(AllocationRules.LetCauseRule.Id) && node is LetClauseSyntax letKind)
            {
                reportDiagnostic(Diagnostic.Create(rules.Get(AllocationRules.LetCauseRule.Id), letKind.LetKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.LetClauseExpression(filePath);
                return;
            }
        }
    }
}