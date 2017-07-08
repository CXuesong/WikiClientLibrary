using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WikiClientLibrary.Infrastructures
{
    /// <summary>
    /// An <see cref="IAsyncEnumerable{T}"/> implementation that uses delegate to generate items.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    public class DelegateAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        /// <summary>
        /// A delegate used to generate next item of the sequence.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Tuple{T1, T2}"/>
        /// The first item is the next result; The second is <c>true</c> if the next result has been successfully
        /// generated, or <c>false</c> otherwise. You can use either <c>null</c> or
        /// <c>Tuple.Create(&lt;any&gt;, false)</c> to indicate there's no more item or the operation
        /// has been cancelled.
        /// </returns>
        public delegate Task<Tuple<T, bool>> ItemGeneratorDelegate(CancellationToken cancellationToken);

        private readonly ItemGeneratorDelegate generator;

        public DelegateAsyncEnumerable(ItemGeneratorDelegate generator)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            this.generator = generator;
        }

        public DelegateAsyncEnumerable(Func<Task<Tuple<T, bool>>> generator)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            this.generator = ct => generator();
        }

        /// <summary>
        /// Gets an asynchronous enumerator over the sequence.
        /// </summary>
        /// <returns>
        /// Enumerator for asynchronous enumeration over the sequence.
        /// </returns>
        public IAsyncEnumerator<T> GetEnumerator()
        {
            return new MyEnumerator(generator);
        }

        public class MyEnumerator : IAsyncEnumerator<T>
        {
            private readonly ItemGeneratorDelegate generator;

            /// <summary>
            /// 执行与释放或重置非托管资源关联的应用程序定义的任务。
            /// </summary>
            public void Dispose()
            {
                
            }

            /// <summary>
            /// Advances the enumerator to the next element in the sequence, returning the result asynchronously.
            /// </summary>
            /// <param name="cancellationToken">Cancellation token that can be used to cancel the operation.</param>
            /// <returns>
            /// Task containing the result of the operation: true if the enumerator was successfully advanced 
            ///             to the next element; false if the enumerator has passed the end of the sequence.
            /// </returns>
            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                var result = await generator(cancellationToken);
                if (result == null) return false;
                Current = result.Item1;
                return result.Item2;
            }

            /// <summary>
            /// Gets the current element in the iteration.
            /// </summary>
            public T Current { get; private set; }

            public MyEnumerator(ItemGeneratorDelegate generator)
            {
                if (generator == null) throw new ArgumentNullException(nameof(generator));
                this.generator = generator;
                this.Current = default(T);
            }
        }
    }
}
