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
        private const string argumentName = @"args";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DeclareOnlyNonVirtualEventsAnalyzer.FieldEventDiagnosticId, DeclareOnlyNonVirtualEventsAnalyzer.PropertyEventDiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
            else if (diagnostic.Id == DeclareOnlyNonVirtualEventsAnalyzer.PropertyEventDiagnosticId)
            {
                var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf()
                    .OfType<EventDeclarationSyntax>().First();

                // We'll register two actions here.
                // One will simply remove the virtual keyword.
                // The second will remove the virtual keyword,
                // and add a virtual method to raise the event.
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

        private async static Task<Document> ImplementVirtualEventPropertyAsync(Document document, EventDeclarationSyntax declaration, CancellationToken c)
        {
            // Need the left (IdentifierNameSyntax) of the Accessor Statement for the AssignmentExpressionSyntax
            var accessor = declaration.AccessorList.Accessors.First();
            var statements = accessor.Body.Statements.OfType<ExpressionStatementSyntax>();
            // Need to fine where the right side of an Add statement is "value":
            var eventFieldName = statements.Where(s => (s.Expression as AssignmentExpressionSyntax)?.OperatorToken.Kind() == SyntaxKind.PlusEqualsToken)
                .Where(s => ((s.Expression as AssignmentExpressionSyntax)?.Right as IdentifierNameSyntax)?.Identifier.ValueText == "value")
                .Select(s => ((s.Expression as AssignmentExpressionSyntax)?.Left as IdentifierNameSyntax)?.Identifier.ValueText).First();

            var raiseMethod = CreateRaiseMethod(declaration.Identifier.ValueText, eventFieldName, (declaration.Type as GenericNameSyntax));

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

        private async static Task<Document> ImplementVirtualRaiseEventFieldAsync(Document document, EventFieldDeclarationSyntax declaration, CancellationToken c)
        {
            var eventName = declaration.Declaration.Variables.Single().Identifier.ValueText;
            var raiseMethod = CreateRaiseMethod(eventName, eventName,
                (declaration.Declaration.Type as GenericNameSyntax));

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

        private static Task<Document> RemoveVirtualEventPropertyAsync(Document document, EventDeclarationSyntax declaration, CancellationToken c)
        {
            var modifiers = declaration.Modifiers;
            var virtualToken = modifiers.Single(m => m.Kind() == SyntaxKind.VirtualKeyword);
            return RemoveVirtualTokenAsync(document, virtualToken, c);
        }

        private static Task<Document> RemoveVirtualEventFieldAsync(Document document, EventFieldDeclarationSyntax declaration, CancellationToken c)
        {
            var modifiers = declaration.Modifiers;
            var virtualToken = modifiers.Single(m => m.Kind() == SyntaxKind.VirtualKeyword);
            return RemoveVirtualTokenAsync(document, virtualToken, c);
        }

        private static async Task<Document> RemoveVirtualTokenAsync(Document document, SyntaxToken virtualToken, CancellationToken c)
        {
            var root = await document.GetSyntaxRootAsync(c);
            var newRoot = root.ReplaceToken(virtualToken, SyntaxFactory.Token(SyntaxKind.None));
            return document.WithSyntaxRoot(newRoot);
        }

        private static MethodDeclarationSyntax CreateRaiseMethod(string eventName, string eventFieldName, GenericNameSyntax argType)
        {
            var shortendEventName = eventName.Replace("On", "");
            var arg = (argType.TypeArgumentList.Arguments.First() as IdentifierNameSyntax);
            var argTypeName = arg.Identifier.ValueText;
            return MethodDeclaration(arg, $"Raise{shortendEventName}")
                .WithModifiers(TokenList(
                    Token(SyntaxKind.ProtectedKeyword),
                    Token(SyntaxKind.VirtualKeyword)))
                .WithParameterList(
                    ParameterList(SingletonSeparatedList<ParameterSyntax>(
                        Parameter(Identifier(argumentName))
                            .WithType(IdentifierName(argTypeName))))
                    .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                    .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)))
                .WithBody(Block(
                    List<StatementSyntax>(
                    new StatementSyntax[]{
                        ExpressionStatement(
                            ConditionalAccessExpression(
                                IdentifierName(eventFieldName),
                                InvocationExpression(
                                    MemberBindingExpression(IdentifierName(@"Invoke"))
                                    .WithOperatorToken(Token(SyntaxKind.DotToken)))
                                .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]{
                                        Argument(ThisExpression().WithToken(Token(SyntaxKind.ThisKeyword))),
                                        Token(SyntaxKind.CommaToken),
                                        Argument(IdentifierName(argumentName))
                                    }))
                                .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                                .WithCloseParenToken(Token(SyntaxKind.CloseParenToken))))
                            .WithOperatorToken(Token(SyntaxKind.QuestionToken)))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                        ReturnStatement(IdentifierName(argumentName))
                            .WithReturnKeyword(Token(SyntaxKind.ReturnKeyword))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                    }))
                .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken)));
        }
    }
}