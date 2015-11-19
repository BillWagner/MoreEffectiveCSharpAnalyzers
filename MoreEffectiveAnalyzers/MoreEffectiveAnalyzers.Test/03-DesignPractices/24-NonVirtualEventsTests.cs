using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using MoreEffectiveAnalyzers;

namespace MoreEffectiveAnalyzers.Test
{
    [TestClass]
    public class DeclareOnlyNonVirtualEventsTests : CodeFixVerifier
    {

        //No diagnostics expected to show up
        [TestMethod]
        public void NoDiagnosticOnEmptySpan()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void SuggestAndCreateFixOnVirtualFieldLikeEvent()
        {
            var test = @"namespace VirtualEventTestCode
{
    public class Driver
    {
        public virtual event EventHandler<EventArgs> OnVirtualEvent;
    }
}";
            var expected = new DiagnosticResult
            {
                Id = "MoreEffectiveAnalyzersItem24Field",
                Message = "Event 'OnVirtualEvent' should not be virtual",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 5, 54)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"namespace VirtualEventTestCode
{
    public class Driver
    {
        public event EventHandler<EventArgs> OnVirtualEvent;
    }
}";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void SuggestAndCreateFixOnVirtualPropertyLikeEvent()
        {
            var test = @"namespace VirtualEventTestCode
{
    public class Driver
    {
        protected event EventHandler<EventArgs> eventField;

        public virtual event EventHandler<EventArgs> OnVirtualEvent
        {
            add { eventField += value; }
            remove { eventField -= value; }
        }
    }
}";
            var expected = new DiagnosticResult
            {
                Id = "MoreEffectiveAnalyzersItem24Property",
                Message = "Event 'OnVirtualEvent' should not be virtual",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 54)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"namespace VirtualEventTestCode
{
    public class Driver
    {
        protected event EventHandler<EventArgs> eventField;

        public event EventHandler<EventArgs> OnVirtualEvent
        {
            add { eventField += value; }
            remove { eventField -= value; }
        }
    }
}";
            VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new MoreEffectiveAnalyzersCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DeclareOnlyNonVirtualEventsAnalyzer();
        }
    }
}