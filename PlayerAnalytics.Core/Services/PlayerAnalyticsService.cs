using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlayerAnalytics.Database.Shared;
using PlayerAnalytics.Database.Shared.Entities;
using PlayerAnalytics.Shared;

namespace PlayerAnalytics.Core.Services;

/// <summary>
/// Implements <see cref="IPlayerAnalyticsService"/>.
/// Tracks in-flight row IDs for connected players so callers can retrieve the connection ID
/// without another DB round-trip (mirrors SourceMod PA_GetConnectionID native).
/// </summary>
internal sealed class PlayerAnalyticsService : IPlayerAnalyticsService
{
    private readonly ILogger<PlayerAnalyticsService> _logger;

    // steamId → active connection row id (set after INSERT completes).
    private readonly ConcurrentDictionary<string, long> _activeConnections
        = new(StringComparer.OrdinalIgnoreCase);

    // Lazy — set when IDatabaseProvider becomes available in OnAllModulesLoaded.
    private IDatabaseProvider? _db;

    public PlayerAnalyticsService(ILogger<PlayerAnalyticsService> logger)
    {
        _logger = logger;
    }

    internal void SetDatabase(IDatabaseProvider db)
    {
        _db = db;
    }

    // -------------------------------------------------------------------------
    // IPlayerAnalyticsService
    // -------------------------------------------------------------------------

    public long? GetConnectionId(string steamId)
        => _activeConnections.TryGetValue(steamId, out var id) ? id : null;

    public async Task<List<ConnectionLogEntity>> GetConnectionHistoryAsync(string steamId, int limit = 20)
    {
        if (_db is null) return [];
        return await _db.Queryable<ConnectionLogEntity>()
            .Where(e => e.SteamId == steamId)
            .OrderByDescending(e => e.ConnectedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<ConnectionLogEntity>> GetRecentConnectionsAsync(int limit = 50)
    {
        if (_db is null) return [];
        return await _db.Queryable<ConnectionLogEntity>()
            .OrderByDescending(e => e.ConnectedAt)
            .Take(limit)
            .ToListAsync();
    }

    // -------------------------------------------------------------------------
    // Internal API (called by ConnectionTracker)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Inserts a new connection row and caches the returned id.
    /// </summary>
    internal async Task LogConnectAsync(ConnectionLogEntity entity)
    {
        if (_db is null)
        {
            _logger.LogWarning("[PlayerAnalytics] DB not ready — skipping connect log for {SteamId}", entity.SteamId);
            return;
        }

        try
        {
            var id = await _db.InsertReturnIdentityAsync(entity);
            entity.Id = id;
            _activeConnections[entity.SteamId] = id;
            _logger.LogDebug("[PlayerAnalytics] Logged connect {SteamId} row={Id}", entity.SteamId, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlayerAnalytics] Failed to insert connection row for {SteamId}", entity.SteamId);
        }
    }

    /// <summary>
    /// Updates the disconnect time and duration for an existing row.
    /// </summary>
    internal async Task LogDisconnectAsync(string steamId, DateTime connectedAt)
    {
        if (_db is null) return;
        if (!_activeConnections.TryRemove(steamId, out var rowId)) return;

        var now      = DateTime.UtcNow;
        var duration = (int)(now - connectedAt).TotalSeconds;

        try
        {
            // Manual find + update pattern (avoids compound-key upsert issues).
            var row = await _db.Queryable<ConnectionLogEntity>()
                .Where(e => e.Id == rowId)
                .FirstOrDefaultAsync();

            if (row is null) return;

            row.DisconnectedAt = now;
            row.DurationSeconds = duration;

            await _db.UpdateColumnsAsync(row, e => new { e.DisconnectedAt, e.DurationSeconds });
            _logger.LogDebug("[PlayerAnalytics] Logged disconnect {SteamId} row={Id} duration={Duration}s",
                steamId, rowId, duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlayerAnalytics] Failed to update disconnect for {SteamId} row={Id}", steamId, rowId);
        }
    }
}
