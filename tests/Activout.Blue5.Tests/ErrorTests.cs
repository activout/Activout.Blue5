using System.Net;
using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class ErrorTests
{
    [Fact]
    public async Task PapiErrorBody_IsParsed()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, Root + "products/by-numbers").Respond(_ =>
        {
            var response = Json("""{"message":"Given context not found.","entityId":"xx"}""", HttpStatusCode.NotFound);
            response.Headers.Add("x-request-tracker", "req-1");
            return response;
        });

        var ex = await Assert.ThrowsAsync<BluestoneException>(() => Client(mock, "xx").Products.GetByNumber("A"));

        Assert.Equal((HttpStatusCode.NotFound, "Given context not found.", "xx", "req-1"), (ex.StatusCode, ex.PapiMessage, ex.EntityId, ex.RequestId));
        Assert.Equal("PAPI POST /v1/products/by-numbers failed with 404 Not Found: Given context not found. (entity xx)", ex.Message);
    }

    [Fact]
    public async Task EntityIds_AreParsed()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, Root + "products/by-numbers")
            .Respond(HttpStatusCode.BadRequest, "application/json", """{"status":400,"message":"bad","entityIds":["a","b"]}""");

        var ex = await Assert.ThrowsAsync<BluestoneException>(() => Client(mock).Products.GetByNumber("A"));

        Assert.Equal(["a", "b"], ex.EntityIds);
    }

    [Fact]
    public async Task TooManyRequests_IsRepresented_AndCoreDoesNotRetry()
    {
        var mock = new MockHttpMessageHandler();
        var request = mock.When(HttpMethod.Post, Root + "products/cursor/all")
            .Respond(HttpStatusCode.TooManyRequests, "application/json", """{"message":"Too Many Requests"}""");

        var ex = await Assert.ThrowsAsync<BluestoneException>(async () => await Client(mock).Products.GetAll().ToListAsync());

        Assert.True(ex.IsRateLimited);
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Equal(1, mock.GetMatchCount(request));
    }

    [Fact]
    public async Task Http2ResponseWithoutReasonPhrase_HasCleanMessage()
    {
        // Observed: PAPI answers over HTTP/2 (no reason phrase) with a bare 429, no Retry-After or request id.
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, Root + "products/list").Respond(_ =>
        {
            var response = Json("""{"message":"Too Many Requests"}""", HttpStatusCode.TooManyRequests);
            response.ReasonPhrase = ""; // what SocketsHttpHandler reports over HTTP/2
            return response;
        });

        var ex = await Assert.ThrowsAsync<BluestoneException>(() => Client(mock).Products.Find(ProductQuery.All));

        Assert.Equal("PAPI POST /v1/products/list failed with 429: Too Many Requests", ex.Message);
        Assert.True(ex.IsRateLimited);
        Assert.Null(ex.RequestId);
    }

    [Fact]
    public async Task NonJsonErrorBody_IsIncludedInMessage()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, Root + "products/by-numbers").Respond(HttpStatusCode.BadGateway, "text/html", "<html>Bad gateway</html>");

        var ex = await Assert.ThrowsAsync<BluestoneException>(() => Client(mock).Products.GetByNumber("A"));

        Assert.Null(ex.PapiMessage);
        Assert.EndsWith(": <html>Bad gateway</html>", ex.Message);
    }

    [Fact]
    public async Task Forbidden_DoesNotLeakApiKey()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, Root + "products/by-numbers").Respond(HttpStatusCode.Forbidden, "application/json", """{"message":"Forbidden"}""");

        var ex = await Assert.ThrowsAsync<BluestoneException>(() => Client(mock).Products.GetByNumber("A"));

        Assert.DoesNotContain(ApiKey, ex.ToString());
    }
}
