using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlayerAnalytics.Database.Extensions;
using PlayerAnalytics.Database.Provider;
using PlayerAnalytics.Database.Shared;
using PlayerAnalytics.Database.Shared.Entities;
using Sharp.Shared;
using SqlSugar;

namespace PlayerAnalytics.Database;

/// <summary>
/// PlayerAnalytics Database Plugin — centralized database infrastructure.
/// Supports MySQL and PostgreSQL via SqlSugar.
///
/// <para>Registered on <see cref="PostInit"/> via SharpModuleManager so any module
/// can discover it with
/// <c>GetOptionalSharpModuleInterface&lt;IDatabaseProvider&gt;(IDatabaseProvider.Identity)</c>.</para>
///
/// <para>Connection configuration lives in
/// <c>sharp/configs/playeranalytics.database.jsonc</c> — never hardcode credentials.</para>
/// </summary>
public sealed class PlayerAnalyticsDatabasePlugin : IModSharpModule
{
    public string DisplayName   => "PlayerAnalytics Database";
    public string DisplayAuthor => "YappersHQ (ported from sneak-it/PlayerAnalytics)";

    private readonly InterfaceBridge                   _bridge;
    private readonly ILogger<PlayerAnalyticsDatabasePlugin> _logger;
    private readonly ServiceProvider                   _serviceProvider;

    public PlayerAnalyticsDatabasePlugin(
        ISharedSystem   sharedSystem,
        string?         dllPath,
        string?         sharpPath,
        Version?        version,
        IConfiguration? coreConfiguration,
        bool            hotReload)
    {
        ArgumentNullException.ThrowIfNull(sharpPath);

        _bridge = new InterfaceBridge(this, sharedSystem, sharpPath);
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<PlayerAnalyticsDatabasePlugin>();

        var configuration = LoadConfiguration(sharpPath);

        var services = new ServiceCollection();
        services.AddSingleton(_bridge);
        services.AddSingleton(sharedSystem);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLoggerFactory(sharedSystem.GetLoggerFactory());

        var dbType = (configuration["Database:Type"] ?? "mysql").ToLowerInvariant();
        _logger.LogInformation("[PlayerAnalytics.Database] Using SqlSugar ({DbType})", dbType);

        services.AddSingleton<ISqlSugarClient>(_ =>
            new SqlSugarScope(SugarExtensions.BuildConnectionConfig(configuration)));
        services.AddSingleton<IDatabaseProvider, DatabaseProvider>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public bool Init() => true;

    public void PostInit()
    {
        var provider = _serviceProvider.GetRequiredService<IDatabaseProvider>();

        // Create / migrate table schema.
        provider.InitTables(typeof(ConnectionLogEntity));

        // Index steam_id for fast per-player lookups.
        provider.CreateIndex<ConnectionLogEntity>(["SteamId"]);
        // Index connected_at for time-based queries.
        provider.CreateIndex<ConnectionLogEntity>(["ConnectedAt"]);

        _bridge.SharpModuleManager.RegisterSharpModuleInterface<IDatabaseProvider>(
            _bridge.Module, IDatabaseProvider.Identity, provider);

        _logger.LogInformation("[PlayerAnalytics.Database] IDatabaseProvider registered ({Id})", IDatabaseProvider.Identity);
    }

    public void OnAllModulesLoaded() { }
    public void OnLibraryConnected(string name) { }
    public void OnLibraryDisconnect(string name) { }

    public void Shutdown() => _serviceProvider.Dispose();

    // -------------------------------------------------------------------------
    // Configuration bootstrap
    // -------------------------------------------------------------------------

    private const string DefaultConfig =
        """
        {
            "Database": {
                // Supported types: mysql, postgresql (case-insensitive)
                "Type": "mysql",

                "Host": "localhost",
                "Port": 3306,

                // Database name on the server
                "Database": "player_analytics",

                "User": "root",
                "Password": "YOUR_PASSWORD_HERE"
            }
        }
        """;

    private static IConfigurationRoot LoadConfiguration(string sharpPath)
    {
        var configPath = Path.Combine(sharpPath, "configs", "playeranalytics.database.jsonc");

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
