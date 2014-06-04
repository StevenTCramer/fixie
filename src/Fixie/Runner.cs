﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fixie.Conventions;
using Fixie.Results;

namespace Fixie
{
    public class Runner
    {
        readonly Listener listener;
        readonly ILookup<string, string> options;

        public Runner(Listener listener)
            : this(listener, Enumerable.Empty<string>().ToLookup(x => x, x => x)) { }

        public Runner(Listener listener, ILookup<string, string> options)
        {
            this.listener = listener;
            this.options = options;
        }

        public AssemblyResult RunAssembly(Assembly assembly)
        {
            var runContext = new RunContext(assembly, options);

            return RunTypes(runContext, assembly.GetTypes());
        }

        public AssemblyResult RunNamespace(Assembly assembly, string ns)
        {
            var runContext = new RunContext(assembly, options);

            return RunTypes(runContext, assembly.GetTypes().Where(type => type.IsInNamespace(ns)).ToArray());
        }

        public AssemblyResult RunType(Assembly assembly, Type type)
        {
            if (type.IsSubclassOf(typeof(Convention)))
            {
                var singleConventionRunContext = new RunContext(assembly, options);
                var singleConvention = Construct<Convention>(type, singleConventionRunContext);

                return RunTypes(singleConventionRunContext, singleConvention, assembly.GetTypes());
            }

            var runContext = new RunContext(assembly, options, type);

            return RunTypes(runContext, type);
        }

        public AssemblyResult RunType(Assembly assembly, Convention convention, Type type)
        {
            var runContext = new RunContext(assembly, options);

            return RunTypes(runContext, convention, type);
        }

        public AssemblyResult RunMethod(Assembly assembly, MethodInfo method)
        {
            var runContext = new RunContext(assembly, options, method);

            var conventions = GetConventions(runContext);

            foreach (var convention in conventions)
                convention.Methods.Where(m => m == method);

            var type = method.DeclaringType;

            return Run(runContext, conventions, type);
        }

        private AssemblyResult RunTypes(RunContext runContext, params Type[] types)
        {
            return Run(runContext, GetConventions(runContext), types);
        }

        private AssemblyResult RunTypes(RunContext runContext, Convention convention, params Type[] types)
        {
            return Run(runContext, new[] { convention }, types);
        }

        static Convention[] GetConventions(RunContext runContext)
        {
            var explicitlyAppliedConventionTypes =
                runContext.Assembly
                    .GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(TestAssembly)))
                    .Select(t => Construct<TestAssembly>(t, runContext))
                    .ToArray().SelectMany(x => x.ConventionTypes);

            var localConventionTypes =
                runContext.Assembly
                    .GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(Convention)));

            var customConventions =
                localConventionTypes
                    .Concat(explicitlyAppliedConventionTypes)
                    .Distinct()
                    .Select(t => Construct<Convention>(t, runContext))
                    .ToArray();

            if (customConventions.Any())
                return customConventions;

            return new[] { (Convention) new DefaultConvention() };
        }

        static T Construct<T>(Type type, RunContext runContext)
        {
            var container = new Container(runContext);

            return (T)container.GetInstance(type);
        }

        AssemblyResult Run(RunContext runContext, IEnumerable<Convention> conventions, params Type[] candidateTypes)
        {
            var conventionRunner = new ConventionRunner();

            var assemblyResult = new AssemblyResult(runContext.Assembly.Location);
            
            listener.AssemblyStarted(runContext.Assembly);

            foreach (var convention in conventions)
            {
                var conventionResult = conventionRunner.Run(convention, listener, candidateTypes);

                assemblyResult.Add(conventionResult);
            }

            listener.AssemblyCompleted(runContext.Assembly, assemblyResult);

            return assemblyResult;
        }

        class Container
        {
            readonly RunContext runContext;

            public Container(RunContext runContext)
            {
                this.runContext = runContext;
            }

            public object GetInstance(Type type)
            {
                if (type == typeof(RunContext))
                    return runContext;

                var constructor = GetConstructor(type);

                try
                {
                    return constructor.Invoke(constructor.GetParameters().Select(parameter => GetInstance(parameter.ParameterType)).ToArray());
                }
                catch (Exception ex)
                {
                    throw new Exception(String.Format("Could not construct an instance of type '{0}'.", type.FullName), ex);
                }
            }

            static ConstructorInfo GetConstructor(Type type)
            {
                var constructors = type.GetConstructors();

                if (constructors.Length == 1)
                    return constructors.Single();

                throw new Exception(
                    String.Format("Could not construct an instance of type '{0}'.  Expected to find exactly 1 public constructor, but found {1}.",
                        type.FullName, constructors.Length));
            }
        }
    }
}