using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PlayerAnalytics.Database.Extensions;

internal static class DependencyInjectionExtensions
{
    /// <summary>
    /// Registers <see cref="ILoggerFactory"/> and the open-generic <see cref="ILogger{T}"/>
    /// using an already-constructed factory instance (avoids the <c>AddLogging(Action)</c>
    /// overload which conflicts with <c>IConfigurationBuilder.Add</c>).
    /// </summary>
    public static IServiceCollection AddLoggerFactory(
        this IServiceCollection services,
        ILoggerFactory          factory)
    {
        services.AddSingleton(factory);
        services.Add(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));
        return services;
    }
}
