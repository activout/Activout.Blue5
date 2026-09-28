using Microsoft.Extensions.DependencyInjection;

namespace Activout.Blue5;

/// <summary>Dependency-injection registration.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="Blue5Client"/> as a typed <see cref="HttpClient"/> client. Chain the returned builder,
    /// e.g. with <c>.AddBlue5Resilience()</c> from Activout.Blue5.Resilience.
    /// </summary>
    public static IHttpClientBuilder AddBlue5(this IServiceCollection services, Blue5Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.AddBlue5(_ => options);
    }

    /// <summary>Registers <see cref="Blue5Client"/> with options resolved from the container (e.g. bound from configuration).</summary>
    public static IHttpClientBuilder AddBlue5(this IServiceCollection services, Func<IServiceProvider, Blue5Options> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsFactory);
        services.AddSingleton(optionsFactory);
        return services.AddHttpClient<Blue5Client>();
    }
}
