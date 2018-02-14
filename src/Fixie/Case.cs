﻿namespace Fixie
{
    using System;
    using System.Reflection;

    /// <summary>
    /// A test case being executed, representing a single call to a test method.
    /// </summary>
    public class Case
    {
        public Case(MethodInfo caseMethod, params object[] parameters)
        {
            Parameters = parameters != null && parameters.Length == 0 ? null : parameters;
            Class = caseMethod.ReflectedType;

            Method = caseMethod.TryResolveTypeArguments(parameters);

            Name = CaseNameBuilder.GetName(Class, Method, Parameters);
        }

        internal Case(Case originalCase, Exception secondaryFailureReason)
        {
            Parameters = originalCase.Parameters;
            Class = originalCase.Class;
            Method = originalCase.Method;
            Name = originalCase.Name;

            Fail(secondaryFailureReason);
        }

        /// <summary>
        /// Gets the name of the test case, including any input parameters.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the test class in which this test case is defined.
        /// </summary>
        public Type Class { get; }

        /// <summary>
        /// Gets the method that defines this test case.
        /// </summary>
        public MethodInfo Method { get; }

        /// <summary>
        /// For parameterized test cases, gets the set of parameters to be passed into the test method.
        /// For zero-argument test methods, this property is null.
        /// </summary>
        public object[] Parameters { get; }

        /// <summary>
        /// Gets the exception describing this test case's failure.
        /// </summary>
        public Exception Exception { get; private set; }

        public void Skip(string reason)
        {
            State = CaseState.Skipped;
            Exception = null;
            SkipReason = reason;
        }

        public void Pass()
        {
            State = CaseState.Passed;
            Exception = null;
            SkipReason = null;
        }

        /// <summary>
        /// Indicate a test failure with the given reason.
        /// </summary>
        public void Fail(Exception reason)
        {
            State = CaseState.Failed;

            if (reason is PreservedException wrapped)
                Exception = wrapped.OriginalException;
            else
                Exception = reason;

            SkipReason = null;

            if (reason == null)
                Fail("The custom test class lifecycle did not provide an Exception for this test case failure.");
        }

        /// <summary>
        /// Indicate a test failure with the given reason.
        /// </summary>
        public void Fail(string reason)
        {
            try
            {
                throw new Exception(reason);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// The object returned by the invocation of the test case method.
        /// </summary>
        public object ReturnValue { get; internal set; }

        internal TimeSpan Duration { get; set; }
        internal string Output { get; set; }
        internal string SkipReason { get; private set; }
        internal CaseState State { get; private set; }
    }

    enum CaseState
    {
        Skipped,
        Passed,
        Failed
    }
}
