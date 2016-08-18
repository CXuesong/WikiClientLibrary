using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiClientLibrary
{
    /// <summary>
    /// An interface used to receive logging information.
    /// </summary>
    public interface ILogger
    {
        void Trace(string message);

        void Warn(string message);

        void Error(Exception exception, string message);

    }
}
