using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WikiClientLibrary.Infrastructures
{
    /// <summary>
    /// Used to throttle a sequence of incoming actions.
    /// </summary>
    public class Throttler
    {
        private WorkItem lastWork;
        private int _QueuedWorkCount;
        private readonly object workQueueLock = new object();
        private TimeSpan _ThrottleTime = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Asynchronously enqueues a work item.
        /// </summary>
        /// <param name="name">The name of the work, for debugging purpose.</param>
        /// <param name="cancellationToken">The token used to cancel the work before its action.</param>
        /// <returns>
        /// A task that completes after an appropriate throttling delay
        /// and that returns an <see cref="IDisposable"/> used to signal the completion of the work.
        /// </returns>
        public Task<IDisposable> QueueWorkAsync(string name, CancellationToken cancellationToken)
        {
            // Returns an IDisposable after the delay.
            async Task<IDisposable> RunWorkAsync(WorkItem previousWork, WorkItem thisWork, CancellationToken ct)
            {
                Debug.Assert(thisWork != null);
                try
                {
                    if (previousWork != null)
                    {
                        // Wait for previous work.
                        if (ct.CanBeCanceled)
                        {
                            // With cancellation support.
                            var tcs = new TaskCompletionSource<bool>();
                            using (ct.Register(o => ((TaskCompletionSource<bool>) o).SetCanceled(), tcs))
                            {
                                await Task.WhenAny(previousWork.Completion, tcs.Task);
                            }
                        }
                        else
                        {
                            await previousWork.Completion;
                        }
                    }
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(_ThrottleTime, ct);
                }
                catch (OperationCanceledException)
                {
                    thisWork.Dispose();
                    throw;
                }
                return thisWork;
            }

            var work = new WorkItem(name);
            lock (workQueueLock)
            {
                var localLastWork = lastWork;
                lastWork = work;
                _QueuedWorkCount++;
                return RunWorkAsync(localLastWork, work, cancellationToken);
            }
        }

        /// <summary>
        /// Gets the count of current queued work, including the processing ones.
        /// </summary>
        public int QueuedWorkCount
        {
            get
            {
                lock (workQueueLock) return _QueuedWorkCount;
            }
        }

        /// <summary>
        /// The time to wait before each of the queued work to be performed.
        /// </summary>
        public TimeSpan ThrottleTime
        {
            get { return _ThrottleTime; }
            set
            {
                if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value));
                _ThrottleTime = value;
            }
        }

        private static readonly Task completedTask = Task.FromResult(0);

        /// <summary>
        /// Gets a task that is completed when all the queued work items up till now
        /// has been processed.
        /// </summary>
        /// <remarks>This property will be updated whenever <see cref="QueueWorkAsync"/> has been called.</remarks>
        public Task Completion
        {
            get
            {
                lock (workQueueLock) return lastWork?.Completion ?? completedTask;
            }
        }

        [DebuggerDisplay("{Name}")]
        private class WorkItem : IDisposable
        {
            private readonly TaskCompletionSource<bool> completionTcs = new TaskCompletionSource<bool>();

            public WorkItem(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public Task Completion => completionTcs.Task;

            /// <summary>
            /// Notifies the <see cref="Throttler"/> the corresponding work has completed/failed.
            /// </summary>
            public void Dispose()
            {
                completionTcs.TrySetResult(true);
            }
        }

    }
}
