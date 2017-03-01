using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WikiClientLibrary
{
    /// <summary>
    /// Provides support for asynchronous lazy initialization. This type is fully threadsafe.
    /// </summary>
    /// <typeparam name="T">The type of object that is being asynchronously initialized.</typeparam>
   // http://blog.stephencleary.com/2012/08/asynchronous-lazy-initialization.html
    internal sealed class AsyncLazy<T>
    {
        /// <summary>
        /// The underlying lazy task.
        /// </summary>
        private readonly Lazy<Task<T>> instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="factory">The delegate that is invoked on a background thread to produce the value when it is needed.</param>
        public AsyncLazy(Func<T> factory)
        {
            instance = new Lazy<Task<T>>(() => Task.Run(factory));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="factory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
        public AsyncLazy(Func<Task<T>> factory)
        {
            instance = new Lazy<Task<T>>(() => Task.Run(factory));
        }

        /// <summary>
        /// Asynchronous infrastructure support. This method permits instances of <see cref="AsyncLazy&lt;T&gt;"/> to be await'ed.
        /// </summary>
        public TaskAwaiter<T> GetAwaiter()
        {
            return instance.Value.GetAwaiter();
        }

        /// <summary>
        /// Starts the asynchronous initialization, if it has not already started.
        /// </summary>
        public void Start()
        {
            var unused = instance.Value;
        }
    }

    // https://blogs.msdn.microsoft.com/pfxteam/2012/02/12/building-async-coordination-primitives-part-7-asyncreaderwriterlock/
    internal class AsyncReaderWriterLock
    {
        private readonly Task<IDisposable> m_readerReleaser;
        private readonly Task<IDisposable> m_writerReleaser;

        private readonly Queue<TaskCompletionSource<Releaser>> m_waitingWriters =
            new Queue<TaskCompletionSource<Releaser>>();

        private TaskCompletionSource<Releaser> m_waitingReader =
            new TaskCompletionSource<Releaser>();

        private int readersWaiting;
        private int readersAcquired;

        public AsyncReaderWriterLock()
        {
            m_readerReleaser = Task.FromResult((IDisposable) new Releaser(this, false));
            m_writerReleaser = Task.FromResult((IDisposable) new Releaser(this, true));
        }

        public Task<IDisposable> ReaderLockAsync()
        {
            return ReaderLockAsync(CancellationToken.None);
        }

        public Task<IDisposable> ReaderLockAsync(CancellationToken ct)
        {
            lock (m_waitingWriters)
            {
                if (readersAcquired >= 0 && m_waitingWriters.Count == 0)
                {
                    ++readersAcquired;
                    return m_readerReleaser;
                }
                else
                {
                    // Hold on
                    ++readersWaiting;
                    return m_waitingReader.Task.ContinueWith(t => t.Result, ct)
                        // ReSharper disable once MethodSupportsCancellation
                        .ContinueWith(t => t.IsCanceled ? null : (IDisposable) t.Result);
                }
            }
        }

        public Task<IDisposable> WriterLockAsync(CancellationToken ct)
        {
            lock (m_waitingWriters)
            {
                if (readersAcquired == 0)
                {
                    readersAcquired = -1;
                    return m_writerReleaser;
                }
                else
                {
                    var waiter = new TaskCompletionSource<Releaser>();
                    m_waitingWriters.Enqueue(waiter);
                    var reg = ct.Register(w => ((TaskCompletionSource<Releaser>) w).TrySetCanceled(), waiter);
                    return waiter.Task.ContinueWith(t => t.Result, ct)
                        // ReSharper disable once MethodSupportsCancellation
                        .ContinueWith((t, r) =>
                        {
                            ((CancellationTokenRegistration) r).Dispose();
                            return t.IsCanceled ? null : (IDisposable) t.Result;
                        }, reg);
                }
            }
        }

        private void ReaderRelease()
        {
            TaskCompletionSource<Releaser> toWake = null;
            lock (m_waitingWriters)
            {
                --readersAcquired;
                // No readers now
                if (readersAcquired == 0)
                {
                    // Check for writers
                    while (m_waitingWriters.Count > 0)
                    {
                        toWake = m_waitingWriters.Dequeue();
                        if (!toWake.Task.IsCanceled)
                        {
                            readersAcquired = -1;
                            goto RET;
                        }
                    }
                    toWake = null;
                }
            }
            RET:
            if (toWake != null && !toWake.Task.IsCanceled) toWake.SetResult(new Releaser(this, true));
        }

        private void WriterRelease()
        {
            TaskCompletionSource<Releaser> toWake = null;
            var toWakeIsWriter = false;
            lock (m_waitingWriters)
            {
                RETRY:
                if (m_waitingWriters.Count > 0)
                {
                    toWake = m_waitingWriters.Dequeue();
                    if (toWake.Task.IsCanceled) goto RETRY;
                    toWakeIsWriter = true;
                }
                else if (readersWaiting > 0)
                {
                    toWake = m_waitingReader;
                    readersAcquired = readersWaiting;
                    readersWaiting = 0;
                    m_waitingReader = new TaskCompletionSource<Releaser>();
                }
                else readersAcquired = 0;
            }
            toWake?.SetResult(new Releaser(this, toWakeIsWriter));
        }

        private class Releaser : IDisposable
        {
            private readonly AsyncReaderWriterLock _Owner;
            private readonly bool _IsWriter;

            internal Releaser(AsyncReaderWriterLock owner, bool isWriter)
            {
                _Owner = owner;
                _IsWriter = isWriter;
            }

            public void Dispose()
            {
                if (_Owner != null)
                {
                    if (_IsWriter) _Owner.WriterRelease();
                    else _Owner.ReaderRelease();
                }
            }
        }
    }
}
