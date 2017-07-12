using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class TestOutputLogger : ILogger
    {
        public ITestOutputHelper Output { get; }

        public TestOutputLogger(ITestOutputHelper output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            Output = output;
        }

        /// <inheritdoc />
        public void Trace(object source, string message)
        {
            Output.WriteLine("[{0}]TRACE:{1}", ToString(source), message);
        }

        /// <inheritdoc />
        public void Info(object source, string message)
        {
            Output.WriteLine("[{0}]INFO:{1}", ToString(source), message);
        }

        /// <inheritdoc />
        public void Warn(object source, string message)
        {
            Output.WriteLine("[{0}]WARN:{1}", ToString(source), message);
        }

        /// <inheritdoc />
        public void Error(object source, Exception exception, string message)
        {
            Output.WriteLine("[{0}]ERROR:{1}", ToString(source), message);
        }

        private static string ToString(object obj)
        {
            if (obj == null) return "null";
            if (obj is WikiClientBase) return "WikiClient#" + obj.GetHashCode();
            return obj.ToString();
        }
    }
}
