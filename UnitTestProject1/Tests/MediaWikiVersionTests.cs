using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WikiClientLibrary;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{
    public class MediaWikiVersionTests : UnitTestsBase
    {

        /// <inheritdoc />
        public MediaWikiVersionTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void PerformanceTest()
        {
            const int INSTANCE_COUNT = 100000;
            var rnd = new Random();
            var inputs = new (int, int, int, int)[INSTANCE_COUNT];
            var sysVersions = new Version[INSTANCE_COUNT];
            var mwVersions = new MediaWikiVersion[INSTANCE_COUNT];
            for (int i = 0; i < INSTANCE_COUNT; i++)
                inputs[i] = (rnd.Next(32768), rnd.Next(32768), rnd.Next(32768), rnd.Next(0x0FFF));
            GC.Collect();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < INSTANCE_COUNT; i++)
            {
                var (a, b, c, d) = inputs[i];
                sysVersions[i] = new Version(a, b, c, d);
            }
            Output.WriteLine("System.Version used {0}.", sw.Elapsed);
            sysVersions = null;
            GC.Collect();
            sw.Restart();
            for (int i = 0; i < INSTANCE_COUNT; i++)
            {
                var (a, b, c, d) = inputs[i];
                mwVersions[i] = new MediaWikiVersion(a, b, c);
            }
            Output.WriteLine("MediaWikiVersion used {0}.", sw.Elapsed);
        }

        [Fact]
        public void ConstructionTest()
        {
            var v = new MediaWikiVersion(1, 29, 0, MediaWikiDevChannel.Wmf, 12);
            Assert.Equal(1, v.Major);
            Assert.Equal(29, v.Minor);
            Assert.Equal(0, v.Revision);
            Assert.Equal(MediaWikiDevChannel.Wmf, v.DevChannel);
            Assert.Equal(12, v.DevVersion);
            v = new MediaWikiVersion(32767, 32767, 32767, MediaWikiDevChannel.RC, 0x0FFF);
            Assert.Throws<ArgumentOutOfRangeException>(() => new MediaWikiVersion(32768, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MediaWikiVersion(0, 32768, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MediaWikiVersion(0, 0, 32768));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MediaWikiVersion(0, 0, 0, MediaWikiDevChannel.RC, 0x1000));
            Assert.Throws<ArgumentException>(() => new MediaWikiVersion(0, 0, 0, MediaWikiDevChannel.None, 1));
        }

        [Fact]
        public void ParseTest()
        {
            Assert.Equal(new MediaWikiVersion(1, 29, 0, MediaWikiDevChannel.Wmf, 12), MediaWikiVersion.Parse("1.29.0-wmf.12"));
            Assert.Equal(new MediaWikiVersion(1, 0), MediaWikiVersion.Parse("1"));
            Assert.Equal(new MediaWikiVersion(1, 28), MediaWikiVersion.Parse("1. 28"));
            Assert.Equal(new MediaWikiVersion(1, 28, 0), MediaWikiVersion.Parse("1.28.0"));
            Assert.Equal(new MediaWikiVersion(1, 28, 7), MediaWikiVersion.Parse("1.28.7"));
            Assert.Equal(new MediaWikiVersion(1, 28, 0, MediaWikiDevChannel.Beta), MediaWikiVersion.Parse("1.28.0-beTa"));
            Assert.Equal(new MediaWikiVersion(1, 28, 0, MediaWikiDevChannel.Wmf, 17), MediaWikiVersion.Parse("1.28.0-wmf.17"));
            Assert.Equal(new MediaWikiVersion(1, 28, 0, MediaWikiDevChannel.Wmf, 17), MediaWikiVersion.Parse("1.28.0-wmf-17"));
            Assert.Equal(new MediaWikiVersion(1, 28, 0, MediaWikiDevChannel.Wmf, 17), MediaWikiVersion.Parse("1.28.0-wmf17"));
            Assert.Equal(new MediaWikiVersion(1, 28, 7), MediaWikiVersion.Parse("  1.28. 7  "));
            // Unrecognizable DevVersion prefix: <space>wmf<space>
            Assert.Throws<FormatException>(() => MediaWikiVersion.Parse("  1.28.7 - wmf .12"));
        }

        [Fact]
        public void ComparisonTest()
        {
            Assert.True(MediaWikiVersion.Parse("1.0") > MediaWikiVersion.Zero);
            Assert.True(MediaWikiVersion.Parse("1.29.1") > MediaWikiVersion.Parse("1.29"));
            Assert.True(MediaWikiVersion.Parse("1.29.2") > MediaWikiVersion.Parse("1.29.1"));
            Assert.True(MediaWikiVersion.Parse("1.28") > MediaWikiVersion.Parse("1.28-wmf"));
            Assert.True(MediaWikiVersion.Parse("1.27") < MediaWikiVersion.Parse("1.28-wmf"));
            Assert.True(MediaWikiVersion.Parse("1.28-wmf.1") > MediaWikiVersion.Parse("1.28-wmf"));
            Assert.True(MediaWikiVersion.Parse("1.28-wmf.23") > MediaWikiVersion.Parse("1.28-wmf.22"));
            Assert.True(MediaWikiVersion.Parse("1.28-alpha.1") > MediaWikiVersion.Parse("1.28-wmf.3"));
            Assert.True(MediaWikiVersion.Parse("1.28-beta.1") > MediaWikiVersion.Parse("1.28-alpha.3"));
            Assert.True(MediaWikiVersion.Parse("1.28-rc.1") > MediaWikiVersion.Parse("1.28-beta.3"));
        }

    }
}
