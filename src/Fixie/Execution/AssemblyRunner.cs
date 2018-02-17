﻿namespace Fixie.Execution
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Linq;
    using Cli;
    using Listeners;

    public class AssemblyRunner
    {
        const int Success = 0;
        const int FatalError = -1;

        public static int Main(string[] arguments)
        {
            try
            {
                CommandLine.Partition(arguments, out var runnerArguments, out var conventionArguments);

                var options = CommandLine.Parse<Options>(runnerArguments);

                options.Validate();

                var assembly = Assembly.GetEntryAssembly();

                var pipeName = Environment.GetEnvironmentVariable("FIXIE_NAMED_PIPE");

                var runner = new AssemblyRunner();

                if (pipeName == null)
                    return runner.RunAssembly(assembly, options, conventionArguments);

                using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
                {
                    runner.Subscribe(new PipeListener(pipe));

                    pipe.Connect();
                    pipe.ReadMode = PipeTransmissionMode.Message;

                    int exitCode = Success;

                    try
                    {
                        var messageType = pipe.ReceiveMessage();

                        if (messageType == typeof(PipeMessage.DiscoverMethods).FullName)
                        {
                            var discoverMethods = pipe.Receive<PipeMessage.DiscoverMethods>();
                            runner.DiscoverMethods(assembly, options, conventionArguments);
                        }
                        else if (messageType == typeof(PipeMessage.RunMethods).FullName)
                        {
                            var runMethods = pipe.Receive<PipeMessage.RunMethods>();
                            exitCode = runner.RunMethods(assembly, options, conventionArguments, runMethods.Methods);
                        }
                        else if (messageType == typeof(PipeMessage.RunAssembly).FullName)
                        {
                            var runAssembly = pipe.Receive<PipeMessage.RunAssembly>();
                            exitCode = runner.RunAssembly(assembly, options, conventionArguments);
                        }
                        else
                        {
                            var body = pipe.ReceiveMessage();
                            throw new Exception($"Test assembly received unexpected message of type {messageType}: {body}");
                        }
                    }
                    catch (Exception exception)
                    {
                        pipe.Send(exception);
                    }
                    finally
                    {
                        pipe.Send<PipeMessage.Completed>();
                    }

                    return exitCode;
                }
            }
            catch (Exception exception)
            {
                using (Foreground.Red)
                    Console.WriteLine($"Fatal Error: {exception}");

                return FatalError;
            }
        }

        readonly List<Listener> customListeners = new List<Listener>();

        void Subscribe<TListener>(TListener listener) where TListener : Listener
        {
            customListeners.Add(listener);
        }

        void DiscoverMethods(Assembly assembly, Options options, string[] conventionArguments)
        {
            var listeners = customListeners;
            var bus = new Bus(listeners);
            var discoverer = new Discoverer(bus, Filter(options), conventionArguments);

            discoverer.DiscoverMethods(assembly);
        }

        int RunAssembly(Assembly assembly, Options options, string[] conventionArguments)
        {
            return Run(options, conventionArguments, runner => runner.RunAssembly(assembly));
        }

        int RunMethods(Assembly assembly, Options options, string[] conventionArguments, string[] methods)
        {
            var methodGroups = methods.Select(x => new MethodGroup(x)).ToArray();

            return Run(options, conventionArguments, r => r.RunMethods(assembly, methodGroups));
        }

        int Run(Options options, string[] conventionArguments, Func<Runner, ExecutionSummary> run)
        {
            var listeners = GetExecutionListeners(options);
            var bus = new Bus(listeners);
            var runner = new Runner(bus, Filter(options), conventionArguments);

            var summary = run(runner);

            return summary.Total == 0 ? FatalError : summary.Failed;
        }

        static Filter Filter(Options options)
        {
            var filter = new Filter();
            filter.ByPatterns(options.Patterns);
            return filter;
        }

        List<Listener> GetExecutionListeners(Options options)
        {
            return customListeners.Any() ? customListeners : DefaultExecutionListeners(options).ToList();
        }

        static IEnumerable<Listener> DefaultExecutionListeners(Options options)
        {
            if (ShouldUseTeamCityListener(options))
                yield return new TeamCityListener();
            else
                yield return new ConsoleListener();

            if (ShouldUseAppVeyorListener())
                yield return new AppVeyorListener();

            if (options.Report != null)
                yield return new ReportListener(SaveReport(options));
        }

        static Action<XDocument> SaveReport(Options options)
        {
            return report => ReportListener.Save(report, FullPath(options.Report));
        }

        static string FullPath(string absoluteOrRelativePath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), absoluteOrRelativePath);
        }

        static bool ShouldUseTeamCityListener(Options options)
        {
            var runningUnderTeamCity = Environment.GetEnvironmentVariable("TEAMCITY_PROJECT_NAME") != null;

            return options.TeamCity ?? runningUnderTeamCity;
        }

        static bool ShouldUseAppVeyorListener()
        {
            return Environment.GetEnvironmentVariable("APPVEYOR") == "True";
        }
    }
}