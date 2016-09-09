using System.Collections.Immutable;
using System.Linq;
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

        private static readonly DiagnosticDescriptor RuleField = new DiagnosticDescriptor(FieldEventDiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);
        private static readonly DiagnosticDescriptor RuleProperty = new DiagnosticDescriptor(PropertyEventDiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleField, RuleProperty);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeEventDeclaration,
                SyntaxKind.EventDeclaration,
                SyntaxKind.EventFieldDeclaration);
        }

        private static void AnalyzeEventDeclaration(SyntaxNodeAnalysisContext eventDeclarationSyntaxContext)
        {
            var n =  eventDeclarationSyntaxContext.Node;
            var modifiers = default(SyntaxTokenList);
            var eventName = default(string);
            var location = default(Location);
            var descriptor = default(DiagnosticDescriptor);
            if (n.Kind() == SyntaxKind.EventFieldDeclaration)
            {
                var eventNode = eventDeclarationSyntaxContext.Node as EventFieldDeclarationSyntax;
                modifiers = eventNode.Modifiers;
                var variable = eventNode.Declaration.Variables.Single();
                eventName = variable.Identifier.ValueText;
                location = variable.GetLocation();
                descriptor = RuleField;
            }
            else if (n.Kind() == SyntaxKind.EventDeclaration)
            {
                var eventNode = eventDeclarationSyntaxContext.Node as EventDeclarationSyntax;
                eventName = eventNode.Identifier.ValueText;
                modifiers = eventNode.Modifiers;
                location = eventNode.Identifier.GetLocation();
                descriptor = RuleProperty;
            }
            var isVirtual = modifiers.Any(m => m.Kind() == SyntaxKind.VirtualKeyword);
            if (isVirtual)
            {
                var diagnostic = Diagnostic.Create(descriptor, location, eventName);
                eventDeclarationSyntaxContext.ReportDiagnostic(diagnostic);
            }
        }

    }
}
