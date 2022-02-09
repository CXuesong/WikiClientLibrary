using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1
{
    public class TestOutputLogger : ILogger
    {

        private readonly LogLevel minLogLevel;

        public TestOutputLogger(ITestOutputHelper output, string name, LogLevel minLogLevel)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            this.minLogLevel = minLogLevel;
            Output = output;
            Name = name;
        }

        public string Name { get; }

        public ITestOutputHelper Output { get; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (logLevel < minLogLevel)
                return;
            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message)) return;
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("O"));
            sb.Append(" ");
            sb.Append(logLevel);
            sb.Append(": ");
            var leftMargin = 4;
            sb.Append(Name);
            if (LoggingScope.Current != null)
            {
                sb.AppendLine();
                sb.Append(' ', leftMargin);
                foreach (var scope in LoggingScope.Trace())
                {
                    sb.Append(" -> ");
                    sb.Append(scope.State);
                }
            }
            sb.AppendLine();
            sb.Append(' ', leftMargin);
            sb.Append(message);
            if (exception != null)
            {
                sb.AppendLine();
                sb.Append(' ', leftMargin);
                sb.Append(exception);
            }
            Output.WriteLine(sb.ToString());
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= minLogLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return LoggingScope.Push(state);
        }

        private class LoggingScope : IDisposable
        {

            private static readonly AsyncLocal<LoggingScope?> currentScope = new AsyncLocal<LoggingScope?>();

            private LoggingScope(object? state, LoggingScope? parent)
            {
                State = state;
                Parent = parent;
            }

            public object? State { get; }

            public LoggingScope? Parent { get; }

            public static IEnumerable<LoggingScope> Trace()
            {
                var scope = Current;
                if (scope == null) return Enumerable.Empty<LoggingScope>();
                var stack = new Stack<LoggingScope>();
                while (scope != null)
                {
                    stack.Push(scope);
                    scope = scope.Parent;
                }
                return stack;
            }

            public static LoggingScope? Current => currentScope.Value;

            public static LoggingScope Push(object? state)
            {
                var current = currentScope.Value;
                var next = new LoggingScope(state, current);
                currentScope.Value = next;
                return next;
            }

            public void Dispose()
            {
                if (currentScope.Value != this) throw new InvalidOperationException();
                currentScope.Value = Parent;
            }
        }
    }

    public sealed class TestOutputLoggerProvider : ILoggerProvider
    {

        public TestOutputLoggerProvider(ITestOutputHelper output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public ITestOutputHelper Output { get; }

        public void Dispose()
        {

        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestOutputLogger(Output, categoryName, LogLevel.Trace);
        }
    }
}
