using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Wikia.Sites;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    /// <summary>
    /// Contains tests that confirm certain issues have been resolved.
    /// </summary>
    public class ValidationTests : WikiSiteTestsBase
    {

        public ValidationTests(ITestOutputHelper output) : base(output)
        {

        }

        /// <summary>
        /// [B][Wikia]"Value was either too large or too small for an Int32."
        /// </summary>
        [Fact]
        public async Task Issue39()
        {
            // Cause: cacheBuster=7030077030012 in /api/v1/Mercury/WikiVariables request exceeds Int32 limit.
            var site = new WikiaSite(WikiClient, "https://theedgechronicles.fandom.com/");
            await site.Initialization;
        }

        /// <summary>
        /// [B]ArgumentNullException in WikiPagePropertyList&lt;T&gt;
        /// </summary>
        [Fact]
        public async Task Issue67()
        {
            var site = await WpEnSiteAsync;
            var items = await new CategoriesGenerator(site)
            {
                PageTitle = MediaWikiHelper.JoinValues(new[] { "Test", ".test", "Test_(Unix)", "Test_(assessment)" }),
            }.EnumItemsAsync().ToListAsync();
            ShallowTrace(items);
        }

        /// <summary>
        /// [T]Paring/truncating Debian MediaWiki package version.
        /// </summary>
        [Fact]
        public void Issue72()
        {
            Assert.Throws<FormatException>(() => MediaWikiVersion.Parse("1.19.5-1+deb7u1"));
            var version = MediaWikiVersion.Parse("1.19.5-1+deb7u1", true);
            Assert.Equal(new MediaWikiVersion(1, 19, 5), version);
        }

        /// <summary>
        /// [B]Debian package release adds +dfsg to revision number, breaking version parsing.
        /// </summary>
        [Fact]
        public void Issue86()
        {
            Assert.Throws<FormatException>(() => MediaWikiVersion.Parse("1.19.20+dfsg-0+deb7u3"));
            var version = MediaWikiVersion.Parse("1.19.20+dfsg-0+deb7u3", true);
            Assert.Equal(new MediaWikiVersion(1, 19, 20), version);
        }

    }
}
