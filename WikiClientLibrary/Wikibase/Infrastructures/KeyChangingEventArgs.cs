using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Wikibase.Infrastructures
{

    internal class KeyChangingEventArgs : EventArgs
    {
        public KeyChangingEventArgs(object newKey)
        {
            NewKey = newKey;
        }

        public object NewKey { get; }

    }
}
