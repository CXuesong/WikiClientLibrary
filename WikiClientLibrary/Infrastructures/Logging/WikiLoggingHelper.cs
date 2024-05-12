using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections;
using Microsoft.Extensions.Logging.Abstractions;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Infrastructures.Logging;

/// <summary>
/// Provides helper methods for logging in WCL.
/// </summary>
public static class WikiLoggingHelper
{

    /// <inheritdoc cref="BeginActionScope(ILogger,object,IEnumerable,string)"/>
    public static IDisposable BeginActionScope(this ILogger logger, object? target, [CallerMemberName] string? actionName = null)
    {
        return BeginActionScope(logger, target, null, actionName);
    }

    /// <inheritdoc cref="BeginActionScope(ILogger,object,IEnumerable,string)"/>
    /// <param name="param1">The first parameter for the action.</param>
    /// <param name="param2">The second parameter for the action.</param>
    /// <param name="param3">The third parameter for the action.</param>
    public static IDisposable BeginActionScope(this ILogger logger, object? target, object param1, object param2, object param3, [CallerMemberName] string? actionName = null)
    {
        return BeginActionScope(logger, target, new[] { param1, param2, param3 }, actionName);
    }

    /// <inheritdoc cref="BeginActionScope(ILogger,object,object,object,string)"/>
    public static IDisposable BeginActionScope(this ILogger logger, object? target, object param1, object param2, [CallerMemberName] string? actionName = null)
    {
        return BeginActionScope(logger, target, new[] { param1, param2 }, actionName);
    }

    /// <inheritdoc cref="BeginActionScope(ILogger,object,object,object,string)"/>
    public static IDisposable BeginActionScope(this ILogger logger, object? target, object param1, [CallerMemberName] string? actionName = null)
    {
        return BeginActionScope(logger, target, new[] { param1 }, actionName);
    }

    /// <summary>
    /// Invokes <see cref="ILogger.BeginScope{TState}"/> with the current action(method) name and parameters.
    /// </summary>
    /// <param name="logger">The logger that will enter a new scope.</param>
    /// <param name="target">The action target. Usually the target <see cref="WikiSite"/>, <see cref="WikiPage"/>, etc.
    /// Can be <c>null</c>.</param>
    /// <param name="parameters">The action parameters. Can be <c>null</c>.</param>
    /// <param name="actionName">The action name. Leave it missing to use the caller's member name.</param>
    /// <returns>An <see cref="IDisposable"/> that when disposed, indicates the action is over.</returns>
    public static IDisposable BeginActionScope(this ILogger logger, object? target, IEnumerable? parameters, [CallerMemberName] string? actionName = null)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        if (logger is NullLogger) return EmptyDisposable.Instance;
        return logger.BeginScope(new ActionLogScopeState(target, actionName, parameters));
    }

    /// <inheritdoc cref="BeginActionScope(ILogger,object,IEnumerable,string)"/>
    /// <param name="loggable">The loggable object whose logger will enter a new scope.</param>
    public static IDisposable BeginActionScope(this IWikiClientLoggable loggable, object? target, [CallerMemberName] string? actionName = null)
    {
        if (loggable == null) throw new ArgumentNullException(nameof(loggable));
        return BeginActionScope(loggable, target, null, actionName);
    }

    /// <inheritdoc cref="BeginActionScope(IWikiClientLoggable,object,IEnumerable,string)"/>
    /// <param name="param1">The first parameter for the action.</param>
    /// <param name="param2">The second parameter for the action.</param>
    /// <param name="param3">The third parameter for the action.</param>
    public static IDisposable BeginActionScope(this IWikiClientLoggable loggable, object? target, 
        object param1, object param2, object param3, [CallerMemberName] string? actionName = null)
    {
        return BeginActionScope(loggable, target, new[] { param1, param2, param3 }, actionName);
    }

    /// <inheritdoc cref="BeginActionScope(IWikiClientLoggable,object,object,object,string)"/>
    public static IDisposable BeginActionScope(this IWikiClientLoggable loggable, object? target,
        object param1, object param2, [CallerMemberName] string? actionName = null)
    {
        return BeginActionScope(loggable, target, new[] { param1, param2 }, actionName);
    }

    /// <inheritdoc cref="BeginActionScope(IWikiClientLoggable,object,object,object,string)"/>
    public static IDisposable BeginActionScope(this IWikiClientLoggable loggable, object? target, 
        object param1, [CallerMemberName] string? actionName = null)
    {
        return BeginActionScope(loggable, target, new[] { param1 }, actionName);
    }

    /// <summary>
    /// Invokes <see cref="ILogger.BeginScope{TState}"/> on the <see cref="IWikiClientLoggable.Logger"/>
    /// with the current action(method) name and parameters.
    /// </summary>
    /// <param name="loggable">The instance whose <see cref="IWikiClientLoggable.Logger"/> will enter a new scope.</param>
    /// <param name="target">The action target. Usually the target <see cref="WikiSite"/>, <see cref="WikiPage"/>, etc.
    /// Can be <c>null</c>.</param>
    /// <param name="parameters">The action parameters. Can be <c>null</c>.</param>
    /// <param name="actionName">The action name. Leave it missing to use the caller's member name.</param>
    /// <returns>An <see cref="IDisposable"/> that when disposed, indicates the action is over.</returns>
    public static IDisposable BeginActionScope(this IWikiClientLoggable loggable, object? target,
        IEnumerable? parameters, [CallerMemberName] string? actionName = null)
    {
        if (loggable == null) throw new ArgumentNullException(nameof(loggable));
        if (loggable.Logger is NullLogger) return EmptyDisposable.Instance;
        var outer = loggable.Logger.BeginScope(loggable);
        var inner = loggable.Logger.BeginScope(new ActionLogScopeState(target, actionName, parameters));
        return new CombinedDisposable(inner, outer);
    }

    private class CombinedDisposable : IDisposable
    {

        private IDisposable? disposable1, disposable2;

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

    private sealed class ActionLogScopeState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly object? target;
        private readonly string? action;
        private readonly IList parameters;
        private string? str;

        public ActionLogScopeState(object? target, string? action, IEnumerable? parameters)
        {
            this.target = target;
            this.action = action;
            if (parameters == null)
            {
                this.parameters = Array.Empty<object>();
            }
            else if (parameters is IList list)
            {
                this.parameters = list.Count > 0 ? list : Array.Empty<object>();
            }
            else
            {
                this.parameters = parameters.Cast<object>().ToArray();
            }
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object?>("Target", target);
            yield return new KeyValuePair<string, object?>("Action", action);
            yield return new KeyValuePair<string, object?>("Parameters", parameters);
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        public int Count => 3;

        /// <inheritdoc />
        public KeyValuePair<string, object?> this[int index]
        {
            get
            {
                return index switch
                {
                    0 => new KeyValuePair<string, object?>("Target", target),
                    1 => new KeyValuePair<string, object?>("Action", action),
                    2 => new KeyValuePair<string, object?>("Parameters", parameters),
                    _ => throw new IndexOutOfRangeException()
                };
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
                using var e = enu.GetEnumerator();
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
            if (localTarget != null)
            {
                if (isCollection) builder.Append('[');
                var type = localTarget.GetType();
                if (!type.IsPrimitive && (type.Namespace == null || type.Namespace.StartsWith("System.")))
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