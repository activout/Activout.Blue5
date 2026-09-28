using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class CursorTests
{
    private const string CursorUrl = Root + "products/cursor/all";

    [Fact]
    public async Task GetAll_FollowsCursorUntilPapiReturnsNullCursorAndNoResults()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, CursorUrl).WithContent("""{"limit":100}""").Respond("application/json", CursorPage("c1", Products("A", 2)));
        mock.Expect(HttpMethod.Post, CursorUrl).WithContent("""{"cursor":"c1","limit":100}""").Respond("application/json", CursorPage("c2", Products("B", 1)));
        // Observed PAPI behaviour: the last data page still carries a cursor; the end is an empty page.
        mock.Expect(HttpMethod.Post, CursorUrl).WithContent("""{"cursor":"c2","limit":100}""").Respond("application/json", CursorPage(null));

        var numbers = await Client(mock).Products.GetAll().Select(p => p.Number).ToListAsync();

        Assert.Equal(["A1", "A2", "B1"], numbers);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetAll_StopsOnNullCursor_EvenWithResults()
    {
        var mock = new MockHttpMessageHandler();
        var request = mock.When(HttpMethod.Post, CursorUrl).Respond("application/json", CursorPage(null, Products("A", 2)));

        Assert.Equal(2, await Client(mock).Products.GetAll().CountAsync());
        Assert.Equal(1, mock.GetMatchCount(request));
    }

    [Fact]
    public async Task GetAll_IsLazy_TakingFromFirstPageMakesOneRequest()
    {
        var mock = new MockHttpMessageHandler();
        var first = mock.When(HttpMethod.Post, CursorUrl).WithContent("""{"limit":100}""").Respond("application/json", CursorPage("c1", Products("A", 3)));
        var second = mock.When(HttpMethod.Post, CursorUrl).WithContent("""{"cursor":"c1","limit":100}""").Respond("application/json", CursorPage(null));

        var numbers = await Client(mock).Products.GetAll()
            .Where(p => p.Number != "A1")
            .Select(p => p.Number)
            .Take(2)
            .ToListAsync();

        Assert.Equal(["A2", "A3"], numbers);
        Assert.Equal((1, 0), (mock.GetMatchCount(first), mock.GetMatchCount(second)));
    }

    [Fact]
    public async Task GetAll_MakesNoRequestUntilEnumerated()
    {
        var mock = new MockHttpMessageHandler();
        var request = mock.When(HttpMethod.Post, CursorUrl).Respond("application/json", CursorPage(null));

        _ = Client(mock).Products.GetAll();

        Assert.Equal(0, mock.GetMatchCount(request));
    }
}
