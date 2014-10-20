using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace ClrHeapAllocationsAnalyzer.Test
{
    public abstract class AllocationAnalyzerTests
    {
        protected static readonly List<MetadataReference> references = new List<MetadataReference>
            {
                new MetadataFileReference(typeof(int).Assembly.Location),
                new MetadataFileReference(typeof(Console).Assembly.Location),
                new MetadataFileReference(typeof(Enumerable).Assembly.Location),
                new MetadataFileReference(typeof(IList<>).Assembly.Location)
            };

        protected IList<SyntaxNode> GetExpectedDescendants(IEnumerable<SyntaxNode> nodes, ImmutableArray<SyntaxKind> expected)
        {
            var descendants = new List<SyntaxNode>();
            foreach (var node in nodes)
            {
                if (expected.Any(e => e == node.CSharpKind()))
                {
                    descendants.Add(node);
                    continue;
                }

                foreach (var child in node.ChildNodes())
                {
                    if (expected.Any(e => e == child.CSharpKind()))
                    {
                        descendants.Add(child);
                        continue;
                    }

                    if (child.ChildNodes().Count() > 0)
                        descendants.AddRange(GetExpectedDescendants(child.ChildNodes(), expected));
                }
            }
            return descendants;
        }

        protected Info ProcessCode(ISyntaxNodeAnalyzer<SyntaxKind> analyser, string sampleProgram, 
                                   ImmutableArray<SyntaxKind> expected, bool allowBuildErrors = false)
        {
            var options = new CSharpParseOptions(kind: SourceCodeKind.Script); //, languageVersion: LanguageVersion.CSharp5);
            var tree = CSharpSyntaxTree.ParseText(sampleProgram, options);
            var compilation = CSharpCompilation.Create("Test", new[] { tree }, references);

            var diagnostics = compilation.GetDiagnostics();
            if (diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error) > 0)
            {
                var msg = "There were Errors in the sample code\n";
                if (allowBuildErrors == false)
                    Assert.Fail(msg + string.Join("\n", diagnostics));
                else
                    Console.WriteLine(msg + string.Join("\n", diagnostics));
            }

            var semanticModel = compilation.GetSemanticModel(tree);
            var matches = GetExpectedDescendants(tree.GetRoot().ChildNodes(), expected);

            // Run the code tree thru the analyser and record the allocations it reports
            var allocations = new List<Diagnostic>();
            foreach (var expression in matches)
            {
                var code = expression.GetLeadingTrivia().ToFullString() + expression.ToString();
                Console.WriteLine("\n### CODE ### " + (code.StartsWith("\r\n") ? code : "\n" + code));
                analyser.AnalyzeNode(expression, semanticModel, d =>
                    {
                        allocations.Add(d);
                        Console.WriteLine("*** Diagnostic: " + d.ToString() + " ***");
                    }, 
                    null, 
                    CancellationToken.None);
            }

            return new Info
                {
                    Options = options,
                    Tree = tree,
                    Compilation = compilation,
                    Diagnostics = diagnostics,
                    SemanticModel = semanticModel,
                    Matches = matches,
                    Allocations = allocations,
                };
        }

        protected class Info
        {
            public CSharpParseOptions Options { get; set; }
            public SyntaxTree Tree { get; set; }
            public CSharpCompilation Compilation { get; set; }
            public ImmutableArray<Diagnostic> Diagnostics { get; set; }
            public SemanticModel SemanticModel { get; set; }
            public IList<SyntaxNode> Matches { get; set; }
            public List<Diagnostic> Allocations { get; set; }
        }
    }
}
