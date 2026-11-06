using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bookstore.ServiceDefaults.Configuration;

/// <summary>
/// Provides dependency injection registration extensions for Twelve-Factor Bookstore environment-backed configuration.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// Passes the Factor III open-source litmus test ("could this codebase be open-sourced right now without compromising
/// any credentials or environment URLs?") by binding <see cref="BookstoreConfigOptions"/> from runtime environment
/// variables (where .NET translates double-underscore variables such as <c>Bookstore__Postgres__ConnectionString</c>
/// into hierarchical configuration keys <c>Bookstore:Postgres:ConnectionString</c>) and enforcing fail-fast startup
/// validation via <see cref="BookstoreOptionsValidator"/> and <c>ValidateOnStart()</c>. This eliminates static
/// per-environment profile files (<c>dev</c>, <c>staging</c>, <c>prod</c>) that fail to scale as deployments multiply.
/// </remarks>
public static class ConfigServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BookstoreConfigOptions"/> bound to the <see cref="BookstoreConfigOptions.SectionName"/>
    /// configuration section and enables fail-fast startup validation via <see cref="BookstoreOptionsValidator"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register configuration options with.</param>
    /// <param name="configuration">The application <see cref="IConfiguration"/> containing environment-supplied settings.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddBookstoreEnvironmentConfig(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<BookstoreConfigOptions>, BookstoreOptionsValidator>();
        services
            .AddOptions<BookstoreConfigOptions>()
            .Bind(configuration.GetSection(BookstoreConfigOptions.SectionName))
            .ValidateOnStart();

        return services;
    }
}
