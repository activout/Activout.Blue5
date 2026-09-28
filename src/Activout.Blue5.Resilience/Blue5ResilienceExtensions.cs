using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Activout.Blue5.Resilience;

/// <summary>Opt-in retry/backoff for Blue5 HTTP requests.</summary>
public static class Blue5ResilienceExtensions
{
    /// <param name="builder">The builder returned by <c>AddBlue5</c>.</param>
    extension(IHttpClientBuilder builder)
    {
        /// <summary>
        /// Adds a retry pipeline following Bluestone's published guidance: retries HTTP 429, 500, 502, 503, 504 and
        /// network failures (<see cref="HttpRequestException"/>) with exponential backoff (1 s, factor 2) and jitter,
        /// up to 5 attempts in total. PAPI's POST endpoints are read-only queries, so retrying them is safe.
        /// </summary>
        /// <param name="configure">Adjusts the defaults, e.g. <c>MaxRetryAttempts</c> or <c>Delay</c>.</param>
        public IHttpClientBuilder AddBlue5Resilience(Action<HttpRetryStrategyOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.AddResilienceHandler("blue5", pipeline =>
            {
                var retry = new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 4,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome)),
                };
                configure?.Invoke(retry);
                pipeline.AddRetry(retry);
            });
            return builder;
        }
    }

    private static bool IsTransient(Outcome<HttpResponseMessage> outcome) => outcome switch
    {
        { Exception: HttpRequestException } => true,
        { Result.StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout } => true,
        _ => false,
    };
}
