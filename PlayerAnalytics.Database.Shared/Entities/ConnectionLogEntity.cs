using System;

namespace PlayerAnalytics.Database.Shared.Entities;

/// <summary>
/// One row per player connection session.
/// Replaces the original SourceMod player_analytics table with a cleaner CS2-native schema.
///
/// Key changes vs upstream:
/// - id is long (64-bit) — avoids int overflow on busy servers.
/// - GeoIP columns dropped — no GeoIP extension in ModSharp/CS2.
/// - os column dropped — client convar detection not available in CS2 server-side API.
/// - html_motd_disabled dropped — MOTD not exposed in CS2.
/// - premium column dropped — SteamWorks extension not available in ModSharp/CS2.
/// - connect_method dropped — cl_connectmethod not exposed via CS2 server API.
/// - Added server_id (string) instead of server_ip so operators can label servers cleanly.
/// - Timestamps are UTC DateTime, not unix ints.
/// - connect_date removed (redundant with connected_at).
/// - flags column replaced with admin_groups (comma-sep group strings) — CS2 uses group-based admin.
/// - duration computed and stored on disconnect, remains nullable until then.
/// </summary>
[DbTable("pa_connections")]
public sealed class ConnectionLogEntity
{
    [DbColumn(IsPrimaryKey = true, IsIdentity = true, IsNullable = false)]
    public long Id { get; set; }

    /// <summary>Operator-defined server label from config (e.g. "us-east-1").</summary>
    [DbColumn(IsNullable = false, Length = 64)]
    public string ServerId { get; set; } = string.Empty;

    [DbColumn(IsNullable = false, Length = 128)]
    public string SteamId { get; set; } = string.Empty;

    [DbColumn(IsNullable = false, Length = 128)]
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>IP address of the connecting client.</summary>
    [DbColumn(IsNullable = false, Length = 64)]
    public string IpAddress { get; set; } = string.Empty;

    [DbColumn(IsNullable = false, Length = 128)]
    public string MapName { get; set; } = string.Empty;

    /// <summary>Number of real (non-bot) players on server at connect time.</summary>
    [DbColumn(IsNullable = false)]
    public int PlayerCount { get; set; }

    /// <summary>Comma-separated admin group names, empty string if not admin.</summary>
    [DbColumn(IsNullable = false, Length = 256)]
    public string AdminGroups { get; set; } = string.Empty;

    [DbColumn(IsNullable = false)]
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null while player is still connected; filled on disconnect.</summary>
    [DbColumn(IsNullable = true)]
    public DateTime? DisconnectedAt { get; set; }

    /// <summary>Session duration in seconds. Null while connected.</summary>
    [DbColumn(IsNullable = true)]
    public int? DurationSeconds { get; set; }
}
