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
            const string test = @"";

            VerifyCSharpDiagnostic(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void SuggestAndCreateFixOnVirtualFieldLikeEvent()
        {
            const string test = @"namespace VirtualEventTestCode
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

            const string fixtest = @"namespace VirtualEventTestCode
{
    public class Driver
    {
        public event EventHandler<EventArgs> OnVirtualEvent;
    }
}";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void AddVirtualRaiseEventMethodForFieldLikeEvents()
        {
            const string test = @"namespace VirtualEventTestCode
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

            const string fixtest = @"namespace VirtualEventTestCode
{
    public class Driver
    {
        public event EventHandler<EventArgs> OnVirtualEvent;

        protected virtual EventArgs RaiseVirtualEvent(EventArgs args)
        {
            OnVirtualEvent?.Invoke(this, args);
            return args;
        }
    }
}";
            VerifyCSharpFix(test, fixtest, 1);
        }

        [TestMethod]
        public void SuggestAndCreateFixOnVirtualPropertyLikeEvent()
        {
            const string test = @"namespace VirtualEventTestCode
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

            const string fixtest = @"namespace VirtualEventTestCode
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

        [TestMethod]
        public void AddVirtualRaiseEventMethodForPropertyLikeEvent()
        {
            const string test = @"namespace VirtualEventTestCode
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

            const string fixtest = @"namespace VirtualEventTestCode
{
    public class Driver
    {
        protected event EventHandler<EventArgs> eventField;

        public event EventHandler<EventArgs> OnVirtualEvent
        {
            add { eventField += value; }
            remove { eventField -= value; }
        }

        protected virtual EventArgs RaiseVirtualEvent(EventArgs args)
        {
            eventField?.Invoke(this, args);
            return args;
        }
    }
}";
            VerifyCSharpFix(test, fixtest, 1);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider() => new DeclareOnlyNonVirtuaEventsCodeFixProvider();

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new DeclareOnlyNonVirtualEventsAnalyzer();
    }
}