using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class SearchTests
{
    [Fact]
    public async Task Find_SendsPageAsPageNoAndOnlySetFilters()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, Root + "products/list")
            .WithExactQueryString("pageNo=2&itemsOnPage=10&sort=name%3Adesc")
            .WithContent("""{"numbers":["A1"],"labels":["new"],"attributeFilters":[{"number":"colour","value":"red"}],"productTypes":["SINGLE","VARIANT"]}""")
            .Respond("application/json", SearchPage(21, Products("A", 1)));
        var query = new ProductQuery
        {
            Numbers = ["A1"],
            Labels = ["new"],
            AttributeFilters = [new AttributeFilter("colour", "red")],
            ProductTypes = [ProductType.Single, ProductType.Variant],
            Sort = "name:desc",
        };

        var page = await Client(mock).Products.Find(query, page: 2, pageSize: 10);

        mock.VerifyNoOutstandingExpectation();
        Assert.Equal((21L, 2, 10, false), (page.TotalCount, page.Page, page.PageSize, page.HasNextPage));
        Assert.Equal("A1", Assert.Single(page.Items).Number);
    }

    [Fact]
    public async Task Find_DefaultsToFirstPageOfFifty_WithEmptyCriteria()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, Root + "products/list")
            .WithExactQueryString("pageNo=0&itemsOnPage=50")
            .WithContent("{}")
            .Respond("application/json", SearchPage(120, Products("A", 50)));

        var page = await Client(mock).Products.Find(ProductQuery.All);

        Assert.True(page.HasNextPage);
        mock.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task Find_RejectsInvalidPaging(int page, int pageSize) =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Client(new MockHttpMessageHandler()).Products.Find(ProductQuery.All, page, pageSize));

    [Fact]
    public async Task FindAll_WalksPagesUntilTotalCount()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, Root + "products/list").WithQueryString("pageNo", "0").Respond("application/json", SearchPage(120, Products("A", 50)));
        mock.Expect(HttpMethod.Post, Root + "products/list").WithQueryString("pageNo", "1").Respond("application/json", SearchPage(120, Products("B", 50)));
        mock.Expect(HttpMethod.Post, Root + "products/list").WithQueryString("pageNo", "2").Respond("application/json", SearchPage(120, Products("C", 20)));

        var count = await Client(mock).Products.FindAll(new ProductQuery { Labels = ["new"] }).CountAsync();

        Assert.Equal(120, count);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task FindAll_IsLazyAndStopsOnEmptyPage()
    {
        var mock = new MockHttpMessageHandler();
        var first = mock.When(HttpMethod.Post, Root + "products/list").WithQueryString("pageNo", "0").Respond("application/json", SearchPage(500, Products("A", 50)));
        var second = mock.When(HttpMethod.Post, Root + "products/list").WithQueryString("pageNo", "1").Respond("application/json", SearchPage(500));

        Assert.Equal(5, await Client(mock).Products.FindAll(ProductQuery.All).Take(5).CountAsync());
        Assert.Equal((1, 0), (mock.GetMatchCount(first), mock.GetMatchCount(second)));

        Assert.Equal(50, await Client(mock).Products.FindAll(ProductQuery.All).CountAsync());
        Assert.Equal(1, mock.GetMatchCount(second));
    }
}
