using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MoreEffectiveAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DeclareOnlyNonVirtualEventsAnalyzer : DiagnosticAnalyzer
    {
        public const string FieldEventDiagnosticId = "MoreEffectiveAnalyzersItem24Field";
        public const string PropertyEventDiagnosticId = "MoreEffectiveAnalyzersItem24Property";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "DesignPractices";

        private static DiagnosticDescriptor RuleField = new DiagnosticDescriptor(FieldEventDiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);
        private static DiagnosticDescriptor RuleProperty= new DiagnosticDescriptor(PropertyEventDiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(RuleField, RuleProperty); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeEventDeclaration, 
                SyntaxKind.EventDeclaration, 
                SyntaxKind.EventFieldDeclaration);
        }

        private void AnalyzeEventDeclaration(SyntaxNodeAnalysisContext eventDeclarationSyntaxContext)
        {
            var n =  eventDeclarationSyntaxContext.Node;
            if (n.Kind() == SyntaxKind.EventFieldDeclaration)
            {
                var eventNode = eventDeclarationSyntaxContext.Node as EventFieldDeclarationSyntax;
                var modifiers = eventNode.Modifiers;
                var isVirtual = modifiers.Any(m => m.Kind() == SyntaxKind.VirtualKeyword);
                if (isVirtual)
                {
                    var decl = eventNode.Declaration;
                    var variable = decl.Variables.Single();
                    var eventName = variable.Identifier.ValueText;
                    var diagnostic = Diagnostic.Create(RuleField, variable.GetLocation(), eventName);
                    eventDeclarationSyntaxContext.ReportDiagnostic(diagnostic);
                }
            }
            else if (n.Kind() == SyntaxKind.EventDeclaration)
            {
                var eventNode = eventDeclarationSyntaxContext.Node as EventDeclarationSyntax;
                var modifiers = eventNode.Modifiers;
                var isVirtual = modifiers.Any(m => m.Kind() == SyntaxKind.VirtualKeyword);
                if (isVirtual)
                {
                    var eventName = eventNode.Identifier.ValueText;
                    var diagnostic = Diagnostic.Create(RuleProperty, eventNode.Identifier.GetLocation(), eventName);
                    eventDeclarationSyntaxContext.ReportDiagnostic(diagnostic);
                }

            }
        }

    }
}
