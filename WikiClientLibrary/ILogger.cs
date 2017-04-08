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
        void Trace(object source, string message);

        void Info(object source, string message);

        void Warn(object source, string message);

        void Error(object source, Exception exception, string message);

    }
}
