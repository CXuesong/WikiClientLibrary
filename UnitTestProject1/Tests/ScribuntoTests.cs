using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Scribunto;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1.Tests
{
    public class ScribuntoTests : WikiSiteTestsBase
    {

        /// <inheritdoc />
        public ScribuntoTests(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        public async Task TestConsoleAsync(string siteName)
        {
            const string ModuleContent = @"
-- Test module for unit test

local p = {}

p.name = 'WCL test module'

function p.foo()
    return 'Hello, world!'
end

function p.bar(arg)
    return arg[1]
end

return p
";
            var site = await WikiSiteFromNameAsync(siteName);
            var console = new ScribuntoConsole(site);
            await console.ResetAsync(ModuleContent);

            async Task<ScribuntoEvaluationResult> TestEvaluation(string expr, string returnValue = "", string output = "")
            {
                var id = console.SessionId;
                var r = await console.EvaluateAsync(expr);
                Assert.Equal(ScribuntoEvaluationResultType.Normal, r.Type);
                Assert.Equal(returnValue, r.ReturnValue);
                Assert.Equal(output, r.Output);
                Assert.Equal(id, r.SessionId);
                Assert.False(r.IsNewSession);
                return r;
            }
            Assert.NotEqual(0, console.SessionId);
            Assert.NotEqual(0, console.SessionSize);
            Assert.NotEqual(0, console.SessionMaxSize);
            Assert.True(console.SessionSize < console.SessionMaxSize);
            await TestEvaluation("=1+1", "2");
            await TestEvaluation("=[[test]]", "test");
            await TestEvaluation("mw.log(2+1)", "", "3\n");
            await TestEvaluation("local x = 100");
            await TestEvaluation("=x", "100");
            await TestEvaluation("=p", "table");
            await TestEvaluation("=p.name", "WCL test module");
            await TestEvaluation("=p.foo()", "Hello, world!");
            var ex = await Assert.ThrowsAsync<ScribuntoConsoleException>(() => console.EvaluateAsync("=p.bar(nil)"));
            Assert.Equal("scribunto-lua-error-location", ex.ErrorCode);
            var sessionId = console.SessionId;
            var sessionSize = console.SessionSize;
            await console.ResetAsync();
            Assert.Equal(sessionId, console.SessionId);
            Assert.True(sessionSize > console.SessionSize, "SessionSize after ResetAsync should reduce.");
            await TestEvaluation("=x", "nil");
        }

    }
}
