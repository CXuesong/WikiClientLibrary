using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WikiClientLibrary.Wikibase;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1.Tests
{
    public class InfrastructureTests : UnitTestsBase
    {

        /// <inheritdoc />
        public InfrastructureTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void WbMonolingualTextCollectionTest()
        {
            var collection = new WbMonolingualTextCollection(new[] {new WbMonolingualText("en", "Wikipedia"),})
            {
                {"zh-hans", "维基百科"},
                {"zh-Hant", "維基百科"},
                new WbMonolingualText("ja", "ウィキペディア")
            };
            collection["ru"] = "Википедия";
            Assert.Equal(5, collection.Count);
            Assert.True(collection.ContainsLanguage("zh-HANS"));
            Assert.Contains(new WbMonolingualText("zh-hanT", "維基百科"), collection);
            Assert.Equal("ウィキペディア", collection["JA"]);
            Assert.Equal(new WbMonolingualText("RU", "Википедия"), collection.TryGetMonolingualText("ru"));
            ShallowTrace(collection);
        }

        [Fact]
        public void WbMonolingualTextsCollectionTest()
        {
            var collection = new WbMonolingualTextsCollection(new Dictionary<string, IEnumerable<string>>
                {{"en", new[] {"Wikipedia"}}});
            Assert.True(collection.Add("zh-hans", "维基百科"));
            Assert.True(collection.Add("zh-Hant", "維基百科"));
            Assert.True(collection.Add(new WbMonolingualText("ja", "ウィキペディア")));
            Assert.False(collection.Add("zh-hans", "维基百科"));
            Assert.True(collection.Add("en", "WP"));
            collection["ru"] = new[] {"Википедия"};
            Assert.Equal(6, ((ICollection<WbMonolingualText>)collection).Count);
            Assert.True(collection.ContainsLanguage("zh-HANS"));
            Assert.Contains(new WbMonolingualText("zh-hanT", "維基百科"), collection);
            Assert.Equal("ウィキペディア", collection["JA"].Single());
            Assert.Equal(new WbMonolingualText("RU", "Википедия"), collection.TryGetMonolingualTexts("ru").Single());
            ShallowTrace(collection);
        }

    }
}
