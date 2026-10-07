using Microsoft.Extensions.DependencyInjection;

namespace Activout.Blue5;

/// <summary>Dependency-injection registration.</summary>
/// <remarks>
/// <para>
/// These are C# 14 extension members on <see cref="IServiceCollection"/>. The API reference generator does not list
/// extension blocks, so they are described here:
/// </para>
/// <list type="bullet">
/// <item><description><c>IHttpClientBuilder AddBlue5(this IServiceCollection services, Blue5Options options)</c>:
/// registers <see cref="Blue5Client"/> with the given options.</description></item>
/// <item><description><c>IHttpClientBuilder AddBlue5(this IServiceCollection services, Func&lt;IServiceProvider, Blue5Options&gt; optionsFactory)</c>:
/// registers <see cref="Blue5Client"/> with options resolved from the container, for example bound from configuration.</description></item>
/// </list>
/// <para>
/// Both register <see cref="Blue5Client"/> as a typed <see cref="HttpClient"/> client and return the
/// <see cref="IHttpClientBuilder"/>, so you can chain your own handlers, or <c>AddBlue5Resilience()</c> from the
/// Activout.Blue5.Resilience package.
/// </para>
/// <example>
/// <code language="csharp">
/// builder.Services
///     .AddBlue5(builder.Configuration.GetSection("Blue5").Get&lt;Blue5Options&gt;()!)
///     .AddBlue5Resilience();   // optional
///
/// // then inject Blue5Client
/// </code>
/// </example>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="Blue5Client"/> as a typed <see cref="HttpClient"/> client. Chain the returned builder,
        /// e.g. with <c>.AddBlue5Resilience()</c> from Activout.Blue5.Resilience.
        /// </summary>
        public IHttpClientBuilder AddBlue5(Blue5Options options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return services.AddBlue5(_ => options);
        }

        /// <summary>Registers <see cref="Blue5Client"/> with options resolved from the container (e.g. bound from configuration).</summary>
        public IHttpClientBuilder AddBlue5(Func<IServiceProvider, Blue5Options> optionsFactory)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(optionsFactory);
            services.AddSingleton(optionsFactory);
            return services.AddHttpClient<Blue5Client>();
        }
    }
}
