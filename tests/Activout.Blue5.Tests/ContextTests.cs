using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class ContextTests
{
    [Fact]
    public async Task GetContexts_GetsV1ContextsAndMapsThem()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, Root + "contexts")
            .WithHeaders("x-api-key", ApiKey)
            .Respond("application/json",
                """[{"context":"en","contextName":"English"},{"context":"no","contextName":"Norwegian","future":1},{"context":"sv"}]""");

        var contexts = await Client(mock).GetContexts();

        mock.VerifyNoOutstandingExpectation();
        Assert.Equal([new ContextInfo("en", "English"), new ContextInfo("no", "Norwegian"), new ContextInfo("sv", null)], contexts);
    }
}
