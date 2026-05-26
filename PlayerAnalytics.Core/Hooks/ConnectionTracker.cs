using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlayerAnalytics.Core.Services;
using PlayerAnalytics.Database.Shared.Entities;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;

namespace PlayerAnalytics.Core.Hooks;

/// <summary>
/// Hooks player connect/disconnect events via <see cref="IClientListener"/> and drives
/// <see cref="PlayerAnalyticsService"/>.
///
/// Registered as a client listener via <c>ClientManager.InstallClientListener(this)</c>
/// in <see cref="Init"/>. Uses <c>OnClientPutInServer</c> (player is fully loaded) for
/// the insert and <c>OnClientDisconnected</c> for the update.
/// </summary>
internal sealed class ConnectionTracker : IClientListener
{
    int IClientListener.ListenerVersion  => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 0;

    private readonly InterfaceBridge            _bridge;
    private readonly PlayerAnalyticsService     _service;
    private readonly ILogger<ConnectionTracker> _logger;
    private readonly string                     _serverId;

    // Per-slot connect timestamps so we can compute duration on disconnect.
    // Indexed by (int)(byte)client.Slot — same double-cast pattern used throughout codebase.
    private readonly DateTime[] _connectTimes = new DateTime[64];

    // Per-slot SteamID64 cache — lets OnClientDisconnected look up the id
    // without needing to call back into ClientManager.
    private readonly ulong[] _steamIdBySlot = new ulong[64];

    // Per-slot player name cache (connection name might vanish during disconnect).
    private readonly string[] _nameBySlot = new string[64];

    public ConnectionTracker(
        InterfaceBridge            bridge,
        PlayerAnalyticsService     service,
        ILogger<ConnectionTracker> logger,
        IConfiguration             config)
    {
        _bridge   = bridge;
        _service  = service;
        _logger   = logger;
        _serverId = config["Analytics:ServerId"] ?? "default";

        Array.Fill(_nameBySlot, string.Empty);
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public void Init()
    {
        _bridge.ClientManager.InstallClientListener(this);
    }

    // -------------------------------------------------------------------------
    // IClientListener
    // -------------------------------------------------------------------------

    public void OnClientConnected(IGameClient client) { }
    public void OnClientSettingChanged(IGameClient client) { }
    public void OnAdminCacheReload() { }
    public void OnClientPostAdminCheck(IGameClient client) { }
    public void OnClientDisconnecting(IGameClient client, NetworkDisconnectionReason reason) { }

    /// <summary>
    /// Fires when the player is fully in-game (post admin check, post spawn). Safest point
    /// to log the connection — steam ID is authenticated, map is known, player count is stable.
    /// </summary>
    public void OnClientPutInServer(IGameClient client)
    {
        if (client.IsFakeClient) return;
        if (!client.IsInGame)    return;

        var slot      = (int)(byte)client.Slot;
        var steamId64 = (ulong)client.SteamId;
        var name      = client.Name ?? string.Empty;
        var connectAt = DateTime.UtcNow;

        _steamIdBySlot[slot]  = steamId64;
        _connectTimes[slot]   = connectAt;
        _nameBySlot[slot]     = name.Length > 128 ? name[..128] : name;

        string currentMap;
        try { currentMap = _bridge.ModSharp.GetMapName() ?? string.Empty; }
        catch { currentMap = string.Empty; }

        var playerCount = CountRealClients();
        var adminGroups = GetAdminGroups(client);
        var ipAddress   = client.GetAddress(withPort: false) ?? string.Empty;

        var entity = new ConnectionLogEntity
        {
            ServerId    = _serverId,
            SteamId     = steamId64.ToString(),
            PlayerName  = _nameBySlot[slot],
            IpAddress   = ipAddress,
            MapName     = currentMap,
            PlayerCount = playerCount,
            AdminGroups = adminGroups,
            ConnectedAt = connectAt,
        };

        // Fire-and-forget — never block the game thread on a DB write.
        _ = _service.LogConnectAsync(entity);
    }

    /// <summary>
    /// Fires after the client has fully disconnected. At this point the slot data is still
    /// valid; we flush the duration update and then clear the slot.
    /// </summary>
    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        if (client.IsFakeClient) return;

        var slot      = (int)(byte)client.Slot;
        var steamId64 = _steamIdBySlot[slot];
        var connAt    = _connectTimes[slot];

        // Clear slot state first so a reconnect within the same tick doesn't double-fire.
        _steamIdBySlot[slot] = 0UL;
        _connectTimes[slot]  = default;
        _nameBySlot[slot]    = string.Empty;

        if (steamId64 == 0UL || connAt == default) return;

        _ = _service.LogDisconnectAsync(steamId64.ToString(), connAt);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private int CountRealClients()
    {
        var count = 0;
        foreach (var c in _bridge.ClientManager.GetGameClients(inGame: true))
        {
            if (!c.IsFakeClient)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Returns comma-separated admin permission groups for the player, or empty string.
    /// Requires AdminManager to be loaded; gracefully returns empty if not.
    /// </summary>
    private string GetAdminGroups(IGameClient client)
    {
        var adminManager = _bridge.AdminManager;
        if (adminManager is null) return string.Empty;

        try
        {
            var admin = adminManager.GetAdmin(client.SteamId);
            if (admin is null) return string.Empty;

            // Emit all granted permissions so operators can filter by any permission in queries.
            var perms = admin.Permissions;
            if (perms.Count == 0) return string.Empty;

            var parts = new List<string>(perms.Count);
            foreach (var p in perms)
                parts.Add(p);

            var result = string.Join(",", parts);
            // Truncate to column length.
            return result.Length > 256 ? result[..256] : result;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[PlayerAnalytics] Failed to get admin data for slot {Slot}", (int)(byte)client.Slot);
            return string.Empty;
        }
    }
}
