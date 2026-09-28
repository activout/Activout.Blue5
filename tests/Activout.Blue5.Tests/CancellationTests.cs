using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class CancellationTests
{
    [Fact]
    public async Task Token_FlowsToHttpHandler()
    {
        var handler = new HangingHandler();
        var client = new Blue5Client(new HttpClient(handler), new Blue5Options { BaseAddress = new Uri("https://papi.test"), ApiKey = ApiKey });
        using var cts = new CancellationTokenSource();

        var call = client.Products.GetByNumber("A", cts.Token);
        await handler.Started.Task;
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.True(handler.ObservedCancellation);
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ObservedCancellation { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ObservedCancellation = true;
                throw;
            }

            throw new InvalidOperationException("unreachable");
        }
    }

    [Fact]
    public async Task CancellingDuringEnumeration_StopsBeforeNextPage()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, Root + "products/cursor/all").WithContent("""{"limit":100}""").Respond("application/json", CursorPage("c1", Products("A", 2)));
        var second = mock.When(HttpMethod.Post, Root + "products/cursor/all").WithContent("""{"cursor":"c1","limit":100}""").Respond("application/json", CursorPage(null));
        using var cts = new CancellationTokenSource();
        var seen = new List<string>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var product in Client(mock).Products.GetAll(cts.Token))
            {
                seen.Add(product.Number);
                if (seen.Count == 2) await cts.CancelAsync();
            }
        });

        Assert.Equal(["A1", "A2"], seen);
        Assert.Equal(0, mock.GetMatchCount(second));
    }

    [Fact]
    public async Task WithCancellation_FlowsIntoEnumerator()
    {
        var mock = new MockHttpMessageHandler();
        var request = mock.When(HttpMethod.Post, Root + "products/cursor/all").Respond("application/json", CursorPage(null));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Client(mock).Products.GetAll().WithCancellation(cts.Token).GetAsyncEnumerator().MoveNextAsync());
        Assert.Equal(0, mock.GetMatchCount(request));
    }
}
