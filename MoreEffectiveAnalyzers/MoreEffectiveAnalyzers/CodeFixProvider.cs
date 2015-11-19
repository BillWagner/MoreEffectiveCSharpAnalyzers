using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace MoreEffectiveAnalyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MoreEffectiveAnalyzersCodeFixProvider)), Shared]
    public class MoreEffectiveAnalyzersCodeFixProvider : CodeFixProvider
    {
        private const string title = "Remove virtual keyword";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DeclareOnlyNonVirtualEventsAnalyzer.FieldEventDiagnosticId, DeclareOnlyNonVirtualEventsAnalyzer.PropertyEventDiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            if (diagnostic.Id == DeclareOnlyNonVirtualEventsAnalyzer.FieldEventDiagnosticId)
            {
                var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf()
                    .OfType<EventFieldDeclarationSyntax>().First();


                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: title,
                        createChangedDocument: c => RemoveVirtualEventFieldAsync(context.Document, declaration, c),
                        equivalenceKey: title),
                    diagnostic);
            } else
            {
                var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf()
                    .OfType<EventDeclarationSyntax>().First();


                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: title,
                        createChangedDocument: c => RemoveVirtualEventPropertyAsync(context.Document, declaration, c),
                        equivalenceKey: title),
                    diagnostic);

            }
        }

        private async Task<Document> RemoveVirtualEventPropertyAsync(Document document, EventDeclarationSyntax declaration, CancellationToken c)
        {
            var modifiers = declaration.Modifiers;
            var virtualToken = modifiers.Single(m => m.Kind() == SyntaxKind.VirtualKeyword);


            var root = await document.GetSyntaxRootAsync(c);
            var newRoot = root.ReplaceToken(virtualToken, SyntaxFactory.Token(SyntaxKind.None));

            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> RemoveVirtualEventFieldAsync(Document document, EventFieldDeclarationSyntax declaration, CancellationToken c)
        {
            var modifiers = declaration.Modifiers;
            var virtualToken = modifiers.Single(m => m.Kind() == SyntaxKind.VirtualKeyword);


            var root = await document.GetSyntaxRootAsync(c);
            var newRoot = root.ReplaceToken(virtualToken, SyntaxFactory.Token(SyntaxKind.None));

            return document.WithSyntaxRoot(newRoot);
        }
    }
}