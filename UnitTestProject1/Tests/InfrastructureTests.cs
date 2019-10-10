using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Wikibase.DataTypes;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{
    public class InfrastructureTests : UnitTestsBase
    {

        /// <inheritdoc />
        public InfrastructureTests(ITestOutputHelper output) : base(output)
        {
        }

        private T DeserializeWith<T>(string json, JsonSerializer serializer)
        {
            using (var sr = new StringReader(json))
            using (var jr = new JsonTextReader(sr))
                return serializer.Deserialize<T>(jr);
        }

        [Fact]
        public void JsonDataTimeTests()
        {
            var serializer = MediaWikiHelper.CreateWikiJsonSerializer();
            var value = DeserializeWith<JValue>("\"2008-08-23T18:05:46Z\"", serializer);
            // https://github.com/CXuesong/WikiClientLibrary/issues/49
            // We want to keep string intact as JValue
            Assert.Equal(JTokenType.String, value.Type);
            // We want to allow it get parsed into DateTime at the same time.
            Assert.Equal(new DateTime(2008, 08, 23, 18, 05, 46, DateTimeKind.Utc),
                DeserializeWith<DateTime>("\"2008-08-23T18:05:46Z\"", serializer));
            Assert.Equal(new DateTimeOffset(2008, 08, 23, 18, 05, 46, TimeSpan.Zero),
                DeserializeWith<DateTimeOffset>("\"2008-08-23T18:05:46Z\"", serializer));
            string[] infinityValues = { "infinite", "indefinite", "infinity", "never" };
            foreach (var iTest in infinityValues)
            {
                Assert.Equal(DateTime.MaxValue, DeserializeWith<DateTime>($"\"{iTest}\"", serializer));
                Assert.Equal(DateTimeOffset.MaxValue, DeserializeWith<DateTimeOffset>($"\"{iTest}\"", serializer));
            }
        }

        [Fact]
        public void WbMonolingualTextCollectionTest()
        {
            var collection = new WbMonolingualTextCollection(new[] { new WbMonolingualText("en", "Wikipedia"), })
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
            collection["ru"] = new[] { "Википедия" };
            Assert.Equal(6, ((ICollection<WbMonolingualText>)collection).Count);
            Assert.True(collection.ContainsLanguage("zh-HANS"));
            Assert.Contains(new WbMonolingualText("zh-hanT", "維基百科"), collection);
            Assert.Equal("ウィキペディア", collection["JA"].Single());
            Assert.Equal(new WbMonolingualText("RU", "Википедия"), collection.TryGetMonolingualTexts("ru").Single());
            ShallowTrace(collection);
        }

        [Fact]
        public void GeoCoordinateRectangleTest()
        {
            var rect = new GeoCoordinateRectangle(365, 50, 20, 30);
            Assert.Equal(385, rect.Right, 8);
            Assert.Equal(20, rect.Bottom, 8);
            Assert.False(rect.IsNormalized);
            Assert.True(rect.IsNormalizable);
            rect.Normalize();
            Assert.Equal(new GeoCoordinateRectangle(5, 50, 20, 30), rect);
            Assert.True(rect.Contains(new GeoCoordinate(25, 370)));
        }

    }
}
