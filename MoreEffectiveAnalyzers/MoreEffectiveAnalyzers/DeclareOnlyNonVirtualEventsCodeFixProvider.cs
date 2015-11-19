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
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace MoreEffectiveAnalyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DeclareOnlyNonVirtuaEventsCodeFixProvider)), Shared]
    public class DeclareOnlyNonVirtuaEventsCodeFixProvider : CodeFixProvider
    {
        private const string removeVirtualTitle = "Remove virtual keyword";
        private const string implementVirtualRaiseEvent = "Implement Virtual Method to Raise Event";

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


                // We'll register two actions here.
                // One will simply remove the virtual keyword.
                // The second will remove the virtual keyword,
                // and add a virtual method to raise the event.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: removeVirtualTitle,
                        createChangedDocument: c => RemoveVirtualEventFieldAsync(context.Document, declaration, c),
                        equivalenceKey: removeVirtualTitle),
                    diagnostic);
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: implementVirtualRaiseEvent,
                        createChangedDocument: c => ImplementVirtualRaiseEventFieldAsync(context.Document, declaration, c),
                        equivalenceKey: implementVirtualRaiseEvent),
                    diagnostic);
            }
            else
            {
                var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf()
                    .OfType<EventDeclarationSyntax>().First();

                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: removeVirtualTitle,
                        createChangedDocument: c => RemoveVirtualEventPropertyAsync(context.Document, declaration, c),
                        equivalenceKey: removeVirtualTitle),
                    diagnostic);
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: implementVirtualRaiseEvent,
                        createChangedDocument: c => ImplementVirtualEventPropertyAsync(context.Document, declaration, c),
                        equivalenceKey: implementVirtualRaiseEvent),
                    diagnostic);

            }
        }

        private async Task<Document> ImplementVirtualEventPropertyAsync(Document document, EventDeclarationSyntax declaration, CancellationToken c)
        {
            var eventName = declaration.Identifier.ValueText;
            var shortendEventName = eventName.Replace("On", "");
            var argType = (declaration.Type as GenericNameSyntax);
            var arg = (argType.TypeArgumentList.Arguments.First() as IdentifierNameSyntax);
            var argTypeName = arg.Identifier.ValueText;
            var raiseMethod = MethodDeclaration(arg, $"Raise{shortendEventName}")
                .WithModifiers(TokenList(
                    Token(SyntaxKind.ProtectedKeyword),
                    Token(SyntaxKind.VirtualKeyword)))
                .WithParameterList(
                    ParameterList(SingletonSeparatedList<ParameterSyntax>(
                        Parameter(Identifier(@"args"))
                            .WithType(IdentifierName(@"EventArgs"))))
                    .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                    .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)))
                .WithBody(Block(
                    List<StatementSyntax>(
                    new StatementSyntax[]{
                        ExpressionStatement(
                            ConditionalAccessExpression(
                                IdentifierName(eventName),
                                InvocationExpression(
                                    MemberBindingExpression(IdentifierName(@"Invoke"))
                                    .WithOperatorToken(Token(SyntaxKind.DotToken)))
                                .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]{
                                        Argument(ThisExpression().WithToken(Token(SyntaxKind.ThisKeyword))),
                                        Token(SyntaxKind.CommaToken),
                                        Argument(IdentifierName(@"args"))
                                    }))
                                .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                                .WithCloseParenToken(Token(SyntaxKind.CloseParenToken))))
                            .WithOperatorToken(Token(SyntaxKind.QuestionToken)))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                        ReturnStatement(IdentifierName(@"args"))
                            .WithReturnKeyword(Token(SyntaxKind.ReturnKeyword))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                    }))
                .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken)));

            var root = await document.GetSyntaxRootAsync(c);
            var newRoot = root.InsertNodesAfter(declaration, new SyntaxNode[] { raiseMethod });
            // Note that we need to find the node again
            declaration = newRoot.FindToken(declaration.Span.Start).Parent.AncestorsAndSelf()
                .OfType<EventDeclarationSyntax>().First();

            var modifiers = declaration.Modifiers;
            var virtualToken = modifiers.Single(m => m.Kind() == SyntaxKind.VirtualKeyword);

            var newDeclaration = declaration.ReplaceToken(virtualToken, Token(SyntaxKind.None));
            newRoot = newRoot.ReplaceNode(declaration, newDeclaration
                .WithTrailingTrivia(TriviaList(CarriageReturnLineFeed, CarriageReturnLineFeed)));
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> ImplementVirtualRaiseEventFieldAsync(Document document, EventFieldDeclarationSyntax declaration, CancellationToken c)
        {
            var eventName = declaration.Declaration.Variables.Single().Identifier.ValueText;
            var shortendEventName = eventName.Replace("On", "");
            var argType = (declaration.Declaration.Type as GenericNameSyntax);
            var arg = (argType.TypeArgumentList.Arguments.First() as IdentifierNameSyntax);
            var argTypeName = arg.Identifier.ValueText;
            var raiseMethod = MethodDeclaration(arg, $"Raise{shortendEventName}")
                .WithModifiers(TokenList(
                    Token(SyntaxKind.ProtectedKeyword),
                    Token(SyntaxKind.VirtualKeyword)))
                .WithParameterList(
                    ParameterList(SingletonSeparatedList<ParameterSyntax>(
                        Parameter(Identifier(@"args"))
                            .WithType(IdentifierName(@"EventArgs"))))
                    .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                    .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)))
                .WithBody(Block(
                    List<StatementSyntax>(
                    new StatementSyntax[]{
                        ExpressionStatement(
                            ConditionalAccessExpression(
                                IdentifierName(eventName),
                                InvocationExpression(
                                    MemberBindingExpression(IdentifierName(@"Invoke"))
                                    .WithOperatorToken(Token(SyntaxKind.DotToken)))
                                .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]{
                                        Argument(ThisExpression().WithToken(Token(SyntaxKind.ThisKeyword))),
                                        Token(SyntaxKind.CommaToken),
                                        Argument(IdentifierName(@"args"))
                                    }))
                                .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                                .WithCloseParenToken(Token(SyntaxKind.CloseParenToken))))
                            .WithOperatorToken(Token(SyntaxKind.QuestionToken)))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                        ReturnStatement(IdentifierName(@"args"))
                            .WithReturnKeyword(Token(SyntaxKind.ReturnKeyword))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                    }))
                .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken)));

            var root = await document.GetSyntaxRootAsync(c);
            var newRoot = root.InsertNodesAfter(declaration, new SyntaxNode[] { raiseMethod });
            // Note that we need to find the node again
            declaration = newRoot.FindToken(declaration.Span.Start).Parent.AncestorsAndSelf()
                .OfType<EventFieldDeclarationSyntax>().First();

            var modifiers = declaration.Modifiers;
            var virtualToken = modifiers.Single(m => m.Kind() == SyntaxKind.VirtualKeyword);

            var newDeclaration = declaration.ReplaceToken(virtualToken, Token(SyntaxKind.None));
            newRoot = newRoot.ReplaceNode(declaration, newDeclaration
                .WithTrailingTrivia(TriviaList(CarriageReturnLineFeed, CarriageReturnLineFeed)));
            return document.WithSyntaxRoot(newRoot);
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