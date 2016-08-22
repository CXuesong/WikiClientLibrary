using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a generator (or iterator) of <see cref="Page"/>.
    /// </summary>
    public abstract class Generator : IEnumerable<Page>
    {
        /// <summary>
        /// 返回一个循环访问集合的枚举器。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.Generic.IEnumerator`1"/>。
        /// </returns>
        public abstract IEnumerator<Page> GetEnumerator();

        /// <summary>
        /// 返回一个循环访问集合的枚举器。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.IEnumerator"/> 对象。
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
