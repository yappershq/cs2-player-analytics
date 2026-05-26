using System.Collections.Generic;
using System.Threading.Tasks;
using PlayerAnalytics.Database.Shared.Entities;

namespace PlayerAnalytics.Shared;

/// <summary>
/// Public API for PlayerAnalytics. Registered by PlayerAnalytics.Core in PostInit;
/// consumers look it up in OnAllModulesLoaded.
///
/// Example:
/// <code>
///   var svc = SharpModuleManager
///       .GetOptionalSharpModuleInterface&lt;IPlayerAnalyticsService&gt;(IPlayerAnalyticsService.Identity)
///       ?.Instance;
/// </code>
/// </summary>
public interface IPlayerAnalyticsService
{
    const string Identity = "PlayerAnalytics.Core";

    /// <summary>
    /// Returns the active connection row ID for a connected player,
    /// or null if the player has not yet been logged (e.g. still loading).
    /// Equivalent to the SourceMod PA_GetConnectionID native.
    /// </summary>
    long? GetConnectionId(string steamId);

    /// <summary>
    /// Retrieves recent connection history for a player (newest-first).
    /// </summary>
    Task<List<ConnectionLogEntity>> GetConnectionHistoryAsync(string steamId, int limit = 20);

    /// <summary>
    /// Retrieves the most recent N connection rows across all players, for admin overview.
    /// </summary>
    Task<List<ConnectionLogEntity>> GetRecentConnectionsAsync(int limit = 50);
}
