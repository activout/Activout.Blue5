using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Activout.Blue5.Internal;

/// <summary>Minimal JSON-over-HttpClient helper for PAPI. One instance per context; the HttpClient is shared.</summary>
internal sealed class Papi(HttpClient http, Uri root, string apiKey, string context)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Context => context;

    public Papi WithContext(string newContext) => new(http, root, apiKey, newContext);

    public Task<T> Get<T>(string path, CancellationToken cancellationToken) =>
        Send<T>(new HttpRequestMessage(HttpMethod.Get, new Uri(root, path)), cancellationToken);

    public Task<T> Post<T>(string path, object body, CancellationToken cancellationToken) =>
        Send<T>(new HttpRequestMessage(HttpMethod.Post, new Uri(root, path))
        {
            Content = JsonContent.Create(body, body.GetType(), options: Json),
        }, cancellationToken);

    private async Task<T> Send<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("context", context);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await ToException(request, response, cancellationToken);
            }

            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
                   ?? throw new JsonException($"PAPI {request.Method} {request.RequestUri!.AbsolutePath} returned an empty body.");
        }
    }

    private static async Task<BluestoneException> ToException(
        HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ErrorDto? error = null;
        try
        {
            error = JsonSerializer.Deserialize<ErrorDto>(body, Json);
        }
        catch (JsonException)
        {
            // Not a PAPI error body (e.g. a gateway HTML page); fall back to the raw text below.
        }

        var papiMessage = string.IsNullOrEmpty(error?.Message) ? null : error.Message;
        var detail = papiMessage ?? (body.Length > 200 ? body[..200] + "…" : body);
        var status = (int)response.StatusCode;
        var message = $"PAPI {request.Method} {request.RequestUri!.AbsolutePath} failed with {status}{(string.IsNullOrEmpty(response.ReasonPhrase) ? "" : " " + response.ReasonPhrase)}"
                      + (string.IsNullOrWhiteSpace(detail) ? "." : $": {detail}")
                      + (error?.EntityId is { } id ? $" (entity {id})" : "");
        var requestId = response.Headers.TryGetValues("x-request-tracker", out var values) ? values.FirstOrDefault() : null;
        return new BluestoneException(response.StatusCode, message, papiMessage, error?.EntityId, error?.EntityIds, requestId);
    }
}
