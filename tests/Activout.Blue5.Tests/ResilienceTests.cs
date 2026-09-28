using System.Net;
using Activout.Blue5.Resilience;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class ResilienceTests
{
    private const string Url = Root + "products/by-numbers";

    private static (Blue5Client Client, ServiceProvider Provider) ResilientClient(MockHttpMessageHandler mock)
    {
        var services = new ServiceCollection();
        services.AddBlue5(new Blue5Options { BaseAddress = new Uri("https://papi.test"), ApiKey = ApiKey })
            .AddBlue5Resilience(o => o.Delay = TimeSpan.Zero)
            .ConfigurePrimaryHttpMessageHandler(() => mock);
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<Blue5Client>(), provider);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task TransientStatus_IsRetried(HttpStatusCode status)
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, Url).Respond(status);
        mock.Expect(HttpMethod.Post, Url).WithContent("""{"numbers":["A"]}""").Respond("application/json", Results(Product("A")));
        var (client, provider) = ResilientClient(mock);
        await using var _ = provider;

        Assert.NotNull(await client.Products.GetByNumber("A"));
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task NetworkFailure_IsRetried()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, Url).Throw(new HttpRequestException("connection reset"));
        mock.Expect(HttpMethod.Post, Url).Respond("application/json", Results(Product("A")));
        var (client, provider) = ResilientClient(mock);
        await using var _ = provider;

        Assert.NotNull(await client.Products.GetByNumber("A"));
    }

    [Fact]
    public async Task GivesUpAfterFiveAttempts()
    {
        var mock = new MockHttpMessageHandler();
        var request = mock.When(HttpMethod.Post, Url).Respond(HttpStatusCode.TooManyRequests);
        var (client, provider) = ResilientClient(mock);
        await using var _ = provider;

        var ex = await Assert.ThrowsAsync<BluestoneException>(() => client.Products.GetByNumber("A"));

        Assert.True(ex.IsRateLimited);
        Assert.Equal(5, mock.GetMatchCount(request));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.NotImplemented)]
    public async Task NonTransientStatus_IsNotRetried(HttpStatusCode status)
    {
        var mock = new MockHttpMessageHandler();
        var request = mock.When(HttpMethod.Post, Url).Respond(status);
        var (client, provider) = ResilientClient(mock);
        await using var _ = provider;

        await Assert.ThrowsAsync<BluestoneException>(() => client.Products.GetByNumber("A"));
        Assert.Equal(1, mock.GetMatchCount(request));
    }

    [Fact]
    public async Task WithoutResiliencePackage_CoreDoesNotRetry()
    {
        var mock = new MockHttpMessageHandler();
        var request = mock.When(HttpMethod.Post, Url).Respond(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<BluestoneException>(() => Client(mock).Products.GetByNumber("A"));
        Assert.Equal(1, mock.GetMatchCount(request));
    }
}
