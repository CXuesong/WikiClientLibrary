using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Wikia;
using WikiClientLibrary.Wikia.Sites;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1.Tests
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

    }
}
