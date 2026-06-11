using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlayerAnalytics.Core.Hooks;
using PlayerAnalytics.Core.Services;
using PlayerAnalytics.Database.Shared;
using PlayerAnalytics.Shared;
using Sharp.Shared;

namespace PlayerAnalytics.Core;

/// <summary>
/// PlayerAnalytics Core Plugin.
///
/// Ported from SourceMod PlayerAnalytics by Dr. McKay / Bara / sneaK
/// (https://github.com/sneak-it/PlayerAnalytics) to ModSharp / CS2 by YappersHQ.
///
/// Lifecycle:
/// - Init()              — registers event hooks
/// - PostInit()          — publishes IPlayerAnalyticsService
/// - OnAllModulesLoaded() — resolves IDatabaseProvider from PlayerAnalytics.Database
/// </summary>
public sealed class ModsharpPlugin : IModSharpModule
{
    public string DisplayName   => "PlayerAnalytics";
    public string DisplayAuthor => "YappersHQ (ported from sneak-it/PlayerAnalytics)";

    private readonly IServiceProvider _provider;
    private readonly ILogger<ModsharpPlugin> _logger;

    public ModsharpPlugin(
        ISharedSystem   sharedSystem,
        string?         dllPath,
        string?         sharpPath,
        Version?        version,
        IConfiguration? coreConfiguration,
        bool            hotReload)
    {
        ArgumentNullException.ThrowIfNull(sharedSystem);
        ArgumentNullException.ThrowIfNull(dllPath);
        ArgumentNullException.ThrowIfNull(sharpPath);

        var loggerFactory = sharedSystem.GetLoggerFactory();

        _ = new InterfaceBridge(dllPath, sharpPath, sharedSystem, loggerFactory);

        var configuration = LoadConfiguration(sharpPath);

        var services = new ServiceCollection();
        services.AddSingleton(sharedSystem);
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(LoggerFactoryLogger<>));
        services.AddSingleton(InterfaceBridge.Instance);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<PlayerAnalyticsService>();
        services.AddSingleton<ConnectionTracker>();

        _provider = services.BuildServiceProvider();
        _logger   = _provider.GetRequiredService<ILogger<ModsharpPlugin>>();
    }

    public bool Init()
    {
        _provider.GetRequiredService<ConnectionTracker>().Init();
        return true;
    }

    public void PostInit()
    {
        // Publish IPlayerAnalyticsService so other plugins can discover it.
        var svc = _provider.GetRequiredService<PlayerAnalyticsService>();
        InterfaceBridge.Instance.SharpModuleManager.RegisterSharpModuleInterface<IPlayerAnalyticsService>(
            this, IPlayerAnalyticsService.Identity, svc);

        _logger.LogInformation("[PlayerAnalytics] IPlayerAnalyticsService published ({Id})", IPlayerAnalyticsService.Identity);
    }

    public void OnAllModulesLoaded()
    {
        InterfaceBridge.Instance.InitOptionalModules();

        // Wire up the database provider — it's published by PlayerAnalytics.Database in PostInit,
        // so it's guaranteed to be available here (OAM fires after all PostInits).
        var dbProvider = InterfaceBridge.Instance.SharpModuleManager
            .GetOptionalSharpModuleInterface<IDatabaseProvider>(IDatabaseProvider.Identity)
            ?.Instance;

        if (dbProvider is null)
        {
            _logger.LogError(
                "[PlayerAnalytics] PlayerAnalytics.Database not loaded — connection logging disabled. " +
                "Make sure PlayerAnalytics.Database.dll is installed.");
        }
        else
        {
            var service = _provider.GetRequiredService<PlayerAnalyticsService>();
            service.SetDatabase(dbProvider);
            _logger.LogInformation("[PlayerAnalytics] Database provider connected");

            // Crash recovery — close sessions a previous crash left open
            var serverId = _provider.GetRequiredService<ConnectionTracker>().ServerId;
            _ = service.CloseOrphanedSessionsAsync(serverId);
        }

        _logger.LogInformation("[PlayerAnalytics] Plugin loaded successfully");
    }

    public void OnLibraryConnected(string name) { }
    public void OnLibraryDisconnect(string name) { }

    public void Shutdown()
    {
        if (_provider is IDisposable d) d.Dispose();
    }

    // -------------------------------------------------------------------------
    // Config
    // -------------------------------------------------------------------------

    private const string DefaultConfig =
        """
        {
            "Analytics": {
                // Human-readable server identifier stored in every connection row.
                // Set this to something meaningful so you can distinguish servers
                // in the database (e.g. "us-east-1", "eu-west", "retake-1").
                "ServerId": "default"
            }
        }
        """;

    private static IConfigurationRoot LoadConfiguration(string sharpPath)
    {
        var configPath = Path.Combine(sharpPath, "configs", "playeranalytics.jsonc");

        if (!File.Exists(configPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(configPath, DefaultConfig);
        }

        return new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();
    }
}

/// <summary>Generic logger adapter so DI can resolve ILogger&lt;T&gt; from ILoggerFactory.</summary>
internal sealed class LoggerFactoryLogger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger _inner = factory.CreateLogger(typeof(T).Name);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}
