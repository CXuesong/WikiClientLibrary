using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WikiClientLibrary.Client;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Represents a generator (or iterator) of <see cref="Page"/>.
    /// </summary>
    public abstract class PageGenerator : IObservable<Page>
    {
        public Site Site { get; }

        public WikiClient Client => Site.WikiClient;

        /// <summary>
        /// The IObservable used to publish pages.
        /// </summary>
        private IObservable<Page> myObservable;

        public PageGenerator(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
            myObservable = Observable.Create((Func<IObserver<Page>, CancellationToken, Task>)SubscribeAsync);
        }

        /// <summary>
        /// 通知提供程序：某观察程序将要接收通知。
        /// </summary>
        /// <returns>
        /// 对允许观察者在提供程序发送完通知前停止接收这些通知的接口的引用。
        /// </returns>
        /// <param name="observer">要接收通知的对象。</param>
        public IDisposable Subscribe(IObserver<Page> observer)
        {
            return myObservable.Subscribe(observer);
        }

        protected abstract Task SubscribeAsync(IObserver<Page> observer, CancellationToken cancellationToken);

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <param name="queryDictionary">The dictioanry containning request value pairs.</param>
        protected abstract void FillQueryRequestParams(IDictionary<string, string> queryDictionary);
    }
}
