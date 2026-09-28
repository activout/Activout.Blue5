using System.Net;
using System.Text.Json;
using Activout.Blue5.Cli;
using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class CliTests
{
    private readonly MockHttpMessageHandler _mock = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private Task<int> Run(params string[] args)
    {
        var env = new Dictionary<string, string?> { ["BLUE5_BASE_URL"] = "https://papi.test", ["BLUE5_API_KEY"] = ApiKey };
        return new Blue5Cli(_output, _error, _mock, name => env.GetValueOrDefault(name)).Run(args);
    }

    private void ServeShowcase() =>
        _mock.When(HttpMethod.Post, Root + "products/by-numbers").Respond("application/json", Results(Fixture("all-attribute-types.json")));

    [Fact]
    public async Task ProductGet_PrintsDetails()
    {
        ServeShowcase();

        Assert.Equal(0, await Run("product", "get", "SHOW-001"));
        Assert.Contains("Number           SHOW-001", _output.ToString());
        Assert.Contains("Attributes       17", _output.ToString());
    }

    [Fact]
    public async Task ProductGet_Json_UsesPublicModel()
    {
        ServeShowcase();

        Assert.Equal(0, await Run("product", "get", "SHOW-001", "--format", "json"));
        using var json = JsonDocument.Parse(_output.ToString());
        Assert.Equal("Single", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(17, json.RootElement.GetProperty("attributes").GetArrayLength());
    }

    [Fact]
    public async Task ProductGet_NotFound_ExitsOne()
    {
        _mock.When(HttpMethod.Post, Root + "products/by-numbers").Respond("application/json", Results());

        Assert.Equal(1, await Run("product", "get", "nope"));
        Assert.Equal("Product 'nope' not found.", _error.ToString().Trim());
    }

    [Fact]
    public async Task ProductAttributes_PrintsTable()
    {
        ServeShowcase();

        Assert.Equal(0, await Run("product", "attributes", "SHOW-001"));
        var lines = _output.ToString().Split(Environment.NewLine);
        Assert.StartsWith("NUMBER", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("dictionary") && l.EndsWith("First, Second"));
        Assert.Contains(lines, l => l.StartsWith("column") && l.EndsWith("First=First cell; Second=Second cell"));
        Assert.Contains(lines, l => l.StartsWith("multiline") && l.EndsWith("This text has ⏎multiple lines."));
    }

    [Theory]
    [InlineData("multi_select", "Value A\nValue B\n")]
    [InlineData("column", "First\tFirst cell\nSecond\tSecond cell\n")]
    public async Task AttributeGet_PrintsValues(string attribute, string expected)
    {
        ServeShowcase();

        Assert.Equal(0, await Run("attribute", "get", "SHOW-001", attribute));
        Assert.Equal(expected.ReplaceLineEndings(), _output.ToString());
    }

    [Fact]
    public async Task AttributeGet_UnknownAttribute_ExitsOne()
    {
        ServeShowcase();

        Assert.Equal(1, await Run("attribute", "get", "SHOW-001", "nope"));
    }

    [Fact]
    public async Task ExportNdjson_StreamsPageByPage()
    {
        _mock.When(HttpMethod.Post, Root + "products/cursor/all").WithContent("""{"limit":100}""")
            .Respond("application/json", CursorPage("c1", Products("A", 2)));
        _mock.When(HttpMethod.Post, Root + "products/cursor/all").WithContent("""{"cursor":"c1","limit":100}""").Respond(_ =>
        {
            // The first page must already be written before the second page is requested.
            Assert.Equal(2, _output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            return Json(CursorPage(null, Products("B", 1)));
        });

        Assert.Equal(0, await Run("product", "export", "--format", "ndjson"));

        var lines = _output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["A1", "A2", "B1"], lines.Select(l => JsonDocument.Parse(l).RootElement.GetProperty("number").GetString()));
    }

    [Fact]
    public async Task ExportJson_WritesValidArray()
    {
        _mock.When(HttpMethod.Post, Root + "products/cursor/all").Respond("application/json", CursorPage(null, Products("A", 3)));

        Assert.Equal(0, await Run("product", "export", "--format", "json"));

        using var json = JsonDocument.Parse(_output.ToString());
        Assert.Equal(3, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task ProductList_RespectsLimit()
    {
        _mock.When(HttpMethod.Post, Root + "products/cursor/all").Respond("application/json", CursorPage("c1", Products("A", 5)));

        Assert.Equal(0, await Run("product", "list", "--limit", "2", "--format", "ndjson"));
        Assert.Equal(2, _output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task ProductFind_MapsOptionsToQuery()
    {
        _mock.Expect(HttpMethod.Post, Root + "products/list")
            .WithExactQueryString("pageNo=0&itemsOnPage=50")
            .WithContent("""{"labels":["new"],"attributeFilters":[{"number":"colour","value":"red=ish"}],"productTypes":["VARIANT"]}""")
            .Respond("application/json", SearchPage(1, Products("A", 1)));

        Assert.Equal(0, await Run("product", "find", "--label", "new", "--attr", "colour=red=ish", "--type", "variant"));
        _mock.VerifyNoOutstandingExpectation();
        Assert.Contains("A1", _output.ToString());
        Assert.Contains("1 of 1 matches", _error.ToString());
    }

    [Fact]
    public async Task ContextList_PrintsTable()
    {
        _mock.When(HttpMethod.Get, Root + "contexts")
            .Respond("application/json", """[{"context":"en","contextName":"English"},{"context":"l3598","contextName":"Swedish"}]""");

        Assert.Equal(0, await Run("context", "list"));
        Assert.Equal("CONTEXT  NAME\nen       English\nl3598    Swedish\n".ReplaceLineEndings(), _output.ToString());
    }

    [Fact]
    public async Task MissingConfiguration_ExitsTwo()
    {
        var code = await new Blue5Cli(_output, _error, _mock, _ => null).Run(["product", "list"]);

        Assert.Equal(2, code);
        Assert.Contains("BLUE5_BASE_URL", _error.ToString());
    }

    [Fact]
    public async Task ApiKey_IsNeverEchoed_EvenVerbose()
    {
        _mock.When(HttpMethod.Post, Root + "products/cursor/all").Respond(HttpStatusCode.Forbidden, "application/json", """{"message":"Forbidden"}""");

        Assert.Equal(1, await Run("product", "list", "--verbose"));
        Assert.Contains("403", _error.ToString());
        Assert.DoesNotContain(ApiKey, _output.ToString() + _error.ToString());
    }
}
