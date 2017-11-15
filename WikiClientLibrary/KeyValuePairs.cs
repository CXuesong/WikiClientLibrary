using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary
{
    internal class KeyValuePairs<TKey, TValue> : List<KeyValuePair<TKey, TValue>>
    {

        public void Add(TKey key, TValue value)
        {
            Add(new KeyValuePair<TKey, TValue>(key, value));
        }

    }
}
