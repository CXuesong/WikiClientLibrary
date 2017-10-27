using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WikiClientLibrary
{
    public interface IWikiClientAsyncInitialization
    {

        Task Initialization { get; }

    }
}
