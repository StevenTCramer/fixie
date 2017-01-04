﻿using TestDriven.Framework;

namespace Fixie.TestDriven
{
    using Execution;

    public class TestDrivenListener :
        Handler<CaseSkipped>,
        Handler<CasePassed>,
        Handler<CaseFailed>
    {
        readonly ITestListener tdnet;

        public TestDrivenListener(ITestListener tdnet)
        {
            this.tdnet = tdnet;
            Summary = new ExecutionSummary();
        }

        public ExecutionSummary Summary { get; }

        public void Handle(CaseSkipped message)
        {
            Summary.Add(message);

            tdnet.TestFinished(new TestResult
            {
                Name = message.Name,
                State = TestState.Ignored,
                Message = message.Reason
            });
        }

        public void Handle(CasePassed message)
        {
            Summary.Add(message);

            tdnet.TestFinished(new TestResult
            {
                Name = message.Name,
                State = TestState.Passed
            });
        }

        public void Handle(CaseFailed message)
        {
            Summary.Add(message);

            tdnet.TestFinished(new TestResult
            {
                Name = message.Name,
                State = TestState.Failed,
                Message = message.Exception.PrimaryException.DisplayName,
                StackTrace = message.Exception.CompoundStackTrace,
            });
        }
    }
}