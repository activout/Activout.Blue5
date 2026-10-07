using Activout.Blue5.Attributes;
using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class DictionaryValuesTests
{
    private const string CursorUrl = Root + "attributes/dictionary/def%201/values/cursor/all";

    [Fact]
    public async Task GetDictionaryValues_FollowsCursorAndMapsValues()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, CursorUrl).WithHeaders("context", "sv").WithContent("""{"limit":100}""").Respond("application/json",
            """{"nextCursor":"c1","results":[{"id":"v1","definitionId":"def 1","number":"red","value":"Red","metadata":"#ff0000"}]}""");
        mock.Expect(HttpMethod.Post, CursorUrl).WithContent("""{"cursor":"c1","limit":100}""").Respond("application/json",
            """{"nextCursor":"c2","results":[{"id":"v2","definitionId":"def 1"}]}""");
        mock.Expect(HttpMethod.Post, CursorUrl).WithContent("""{"cursor":"c2","limit":100}""").Respond("application/json",
            """{"nextCursor":null,"results":[]}""");

        var values = await Client(mock, "sv").GetDictionaryValues("def 1").ToListAsync();

        Assert.Equal([new SelectOption("v1", "red", "Red", "#ff0000"), new SelectOption("v2", null, null)], values);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetDictionaryValues_MakesNoRequestUntilEnumerated()
    {
        var mock = new MockHttpMessageHandler();
        var request = mock.When(HttpMethod.Post, CursorUrl).Respond("application/json", CursorPage(null));

        _ = Client(mock).GetDictionaryValues("def 1");

        Assert.Equal(0, mock.GetMatchCount(request));
    }
}
