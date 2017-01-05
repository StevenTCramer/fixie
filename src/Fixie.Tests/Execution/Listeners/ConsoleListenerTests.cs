﻿namespace Fixie.Tests.Execution.Listeners
{
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Fixie.Execution.Listeners;
    using Fixie.Internal;
    using static Utility;

    public class ConsoleListenerTests
    {
        public void ShouldReportResultsToTheConsole()
        {
            using (var console = new RedirectedConsole())
            {
                var listener = new ConsoleListener();
                var convention = SampleTestClassConvention.Build();

                typeof(SampleTestClass).Run(listener, convention);

                var testClass = FullName<SampleTestClass>();

                console.Lines()
                       .Select(CleanBrittleValues)
                       .ShouldEqual(
                           "------ Testing Assembly Fixie.Tests.dll ------",
                           "",
                           "Test '" + testClass + ".SkipWithReason' skipped: Skipped with reason.",
                           "Test '" + testClass + ".SkipWithoutReason' skipped",
                           "Console.Out: Fail",
                           "Console.Error: Fail",
                           "Console.Out: FailByAssertion",
                           "Console.Error: FailByAssertion",
                           "Console.Out: Pass",
                           "Console.Error: Pass",

                           "Test '" + testClass + ".Fail' failed: Fixie.Tests.FailureException",
                           "'Fail' failed!",
                           At<SampleTestClass>("Fail()"),
                           "",
                           "Test '" + testClass + ".FailByAssertion' failed: Should.Core.Exceptions.EqualException",
                           "Assert.Equal() Failure",
                           "Expected: 2",
                           "Actual:   1",
                           At<SampleTestClass>("FailByAssertion()"),
                           "",
                           "1 passed, 2 failed, 2 skipped, took 1.23 seconds (" + Framework.Version + ").");
            }
        }

        public void ShouldNotReportSkipCountsWhenZeroTestsHaveBeenSkipped()
        {
            using (var console = new RedirectedConsole())
            {
                var listener = new ConsoleListener();
                var convention = SampleTestClassConvention.Build();

                convention
                    .Methods
                    .Where(method => !method.Has<SkipAttribute>());

                typeof(SampleTestClass).Run(listener, convention);

                var testClass = FullName<SampleTestClass>();

                console.Lines()
                       .Select(CleanBrittleValues)
                       .ShouldEqual(
                           "------ Testing Assembly Fixie.Tests.dll ------",
                           "",
                           "Console.Out: Fail",
                           "Console.Error: Fail",
                           "Console.Out: FailByAssertion",
                           "Console.Error: FailByAssertion",
                           "Console.Out: Pass",
                           "Console.Error: Pass",

                           "Test '" + testClass + ".Fail' failed: Fixie.Tests.FailureException",
                           "'Fail' failed!",
                           At<SampleTestClass>("Fail()"),
                           "",
                           "Test '" + testClass + ".FailByAssertion' failed: Should.Core.Exceptions.EqualException",
                           "Assert.Equal() Failure",
                           "Expected: 2",
                           "Actual:   1",
                           At<SampleTestClass>("FailByAssertion()"),
                           "",
                           "1 passed, 2 failed, took 1.23 seconds (" + Framework.Version + ").");
            }
        }

        static string CleanBrittleValues(string actualRawContent)
        {
            //Avoid brittle assertion introduced by test duration.
            var decimalSeparator = Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            var cleaned = Regex.Replace(actualRawContent, @"took [\d" + Regex.Escape(decimalSeparator) + @"]+ seconds", @"took 1.23 seconds");

            //Avoid brittle assertion introduced by stack trace line numbers.
            cleaned = Regex.Replace(cleaned, @":line \d+", ":line #");

            return cleaned;
        }
    }
}