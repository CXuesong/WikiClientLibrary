using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Infrastructures.Logging
{
    public static class WikiLoggingHelper
    {

        public static IDisposable BeginActionScope(this ILogger logger, object target, [CallerMemberName] string actionName = null)
        {
            return BeginActionScope(logger, target, null, actionName);
        }

        public static IDisposable BeginActionScope(this ILogger logger, object target, object param1, object param2, object param3, [CallerMemberName] string actionName = null)
        {
            return BeginActionScope(logger, target, new[] { param1, param2, param3 }, actionName);
        }

        public static IDisposable BeginActionScope(this ILogger logger, object target, object param1, object param2, [CallerMemberName] string actionName = null)
        {
            return BeginActionScope(logger, target, new[] { param1, param2 }, actionName);
        }

        public static IDisposable BeginActionScope(this ILogger logger, object target, object param1, [CallerMemberName] string actionName = null)
        {
            return BeginActionScope(logger, target, new[] { param1 }, actionName);
        }

        public static IDisposable BeginActionScope(this ILogger logger, object target, IEnumerable parameters, [CallerMemberName] string actionName = null)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (logger is NullLogger) return EmptyDisposable.Instance;
            return logger.BeginScope(new ActionLogScopeState(target, actionName, parameters));
        }

        public static IDisposable BeginActionScope(this IWikiClientLoggable loggable, object target, [CallerMemberName] string actionName = null)
        {
            if (loggable == null) throw new ArgumentNullException(nameof(loggable));
            return BeginActionScope(loggable, target, null, actionName);
        }

        public static IDisposable BeginActionScope(this IWikiClientLoggable loggable, object target, 
            object param1, object param2, object param3, [CallerMemberName] string actionName = null)
        {
            return BeginActionScope(loggable, target, new[] { param1, param2, param3 }, actionName);
        }

        public static IDisposable BeginActionScope(this IWikiClientLoggable loggable, object target,
            object param1, object param2, [CallerMemberName] string actionName = null)
        {
            return BeginActionScope(loggable, target, new[] { param1, param2 }, actionName);
        }

        public static IDisposable BeginActionScope(this IWikiClientLoggable loggable, object target, 
            object param1, [CallerMemberName] string actionName = null)
        {
            return BeginActionScope(loggable, target, new[] { param1 }, actionName);
        }

        public static IDisposable BeginActionScope(this IWikiClientLoggable loggable, object target,
            IEnumerable parameters, [CallerMemberName] string actionName = null)
        {
            if (loggable == null) throw new ArgumentNullException(nameof(loggable));
            if (loggable.Logger is NullLogger) return EmptyDisposable.Instance;
            var outer = loggable.Logger.BeginScope(loggable);
            var inner = loggable.Logger.BeginScope(new ActionLogScopeState(target, actionName, parameters));
            return new CombinedDisposable(inner, outer);
        }

        private class CombinedDisposable : IDisposable
        {

            private IDisposable disposable1, disposable2;

            public CombinedDisposable(IDisposable disposable1, IDisposable disposable2)
            {
                this.disposable1 = disposable1;
                this.disposable2 = disposable2;
            }

            public void Dispose()
            {
                disposable1?.Dispose();
                disposable1 = null;
                disposable2?.Dispose();
                disposable2 = null;
            }
        }

        private class EmptyDisposable : IDisposable
        {

            public static readonly EmptyDisposable Instance = new EmptyDisposable();

            public void Dispose()
            {
            }
        }

        private sealed class ActionLogScopeState : IReadOnlyList<KeyValuePair<string, object>>
        {

            private static readonly object[] emptyParameters = { };

            private readonly object target;
            private readonly string action;
            private readonly IList parameters;
            private string str;

            public ActionLogScopeState(object target, string action, IEnumerable parameters)
            {
                this.target = target;
                this.action = action;
                if (parameters == null)
                {
                    this.parameters = emptyParameters;
                }
                else if (parameters is IList list)
                {
                    this.parameters = list.Count > 0 ? list : emptyParameters;
                }
                else
                {
                    this.parameters = parameters.Cast<object>().ToArray();
                }
            }

            /// <inheritdoc />
            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                yield return new KeyValuePair<string, object>("Target", target);
                yield return new KeyValuePair<string, object>("Action", action);
                yield return new KeyValuePair<string, object>("Parameters", parameters);
            }

            /// <inheritdoc />
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            /// <inheritdoc />
            public int Count => 3;

            /// <inheritdoc />
            public KeyValuePair<string, object> this[int index]
            {
                get
                {
                    switch (index)
                    {
                        case 0: return new KeyValuePair<string, object>("Target", target);
                        case 1: return new KeyValuePair<string, object>("Action", action);
                        case 2: return new KeyValuePair<string, object>("Parameters", parameters);
                        default: throw new IndexOutOfRangeException();
                    }
                }
            }

            /// <inheritdoc />
            public override string ToString()
            {
                if (str != null) return str;
                var builder = new StringBuilder();
                var localTarget = target;
                var isCollection = false;
                var anyMoreItems = false;
                if (target is IEnumerable<object> enu)
                {
                    isCollection = true;
                    using (var e = enu.GetEnumerator())
                    {
                        if (e.MoveNext())
                        {
                            localTarget = e.Current;
                            anyMoreItems = e.MoveNext();
                        }
                        else
                        {
                            localTarget = null;
                            builder.Append("{[]}");
                        }
                    }
                }
                if (localTarget != null)
                {
                    if (isCollection) builder.Append('[');
                    var type = localTarget.GetType().GetTypeInfo();
                    if (!type.IsPrimitive && !type.Namespace.StartsWith("System."))
                        builder.Append(type.Name);
                    builder.Append('{');
                    builder.Append(localTarget);
                    builder.Append('}');
                    if (anyMoreItems) builder.Append(", …");
                    if (isCollection) builder.Append(']');
                }
                builder.Append("::");
                builder.Append(action);
                builder.Append('(');
                var isFirst = true;
                foreach (var p in parameters)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        builder.Append(", ");
                    }
                    builder.Append(p);
                }
                builder.Append(')');
                var localStr = builder.ToString();
                Volatile.Write(ref str, localStr);
                return localStr;
            }
        }

    }

}