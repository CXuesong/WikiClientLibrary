using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class TestOutputLogger : ILogger
    {

        public TestOutputLogger(ITestOutputHelper output, string name)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            Output = output;
            Name = name;
        }

        public string Name { get; }

        public ITestOutputHelper Output { get; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message)) return;
            message = $"[{Name}] {logLevel}: {message}";
            if (exception != null)
                message += Environment.NewLine + exception;
            Output.WriteLine(message);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotSupportedException();
        }
    }

    public class TestOutputLoggerProvider : ILoggerProvider
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
            return new TestOutputLogger(Output, categoryName);
        }
    }
}
