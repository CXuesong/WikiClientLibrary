using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Cargo
{

    internal class Utility
    {

#if BCL_FEATURE_ARRAY_EMPTY

        public static T[] EmptyArray<T>() => Array.Empty<T>();

#else

        public static T[] EmptyArray<T>() => EmptyArrayHolder<T>.Value;

        private static class EmptyArrayHolder<T>
        {
            public static readonly T[] Value = new T[0];
        }

#endif

    }

}
