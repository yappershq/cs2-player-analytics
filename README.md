<div align="center">
  <h1><strong>PlayerAnalytics</strong></h1>
  <p>Passive per-session connection logging for CS2 servers running ModSharp.</p>
</div>

<p align="center">
  <a href="https://github.com/Kxnrl/modsharp-public"><img src="https://img.shields.io/badge/framework-ModSharp-5865F2?logo=github" alt="ModSharp"></a>
  <img src="https://img.shields.io/badge/game-CS2-orange" alt="CS2">
  <img src="https://img.shields.io/github/stars/yappershq/cs2-player-analytics?style=flat&logo=github" alt="Stars">
</p>

---

A ModSharp / CS2 port of [PlayerAnalytics](https://github.com/sneak-it/PlayerAnalytics) by Dr. McKay / Bara / sneaK. It records one row per player session — connect time, map, IP, name, admin groups, player count and session duration — for every non-bot player, into MySQL or PostgreSQL. There are no player-facing commands; it just collects data other tools (or your own SQL) can read.

The plugin ships as two modules plus a shared interface:

| Module | Role |
|--------|------|
| `PlayerAnalytics.Database` | SqlSugar provider (MySQL / PostgreSQL). Creates/migrates the table and publishes `IDatabaseProvider`. |
| `PlayerAnalytics.Core` | Hooks client connect/disconnect, writes the rows, publishes `IPlayerAnalyticsService`. |

## 🚀 Install

Copy the build output into your ModSharp install (`<sharp>` = your `sharp` directory):

| From | To |
|------|----|
| `.build/modules/PlayerAnalytics.Database/` | `<sharp>/modules/PlayerAnalytics.Database/` |
| `.build/modules/PlayerAnalytics.Core/` | `<sharp>/modules/PlayerAnalytics.Core/` |
| `.build/shared/` | `<sharp>/shared/` |

Both config files (see below) are auto-created with defaults on first run. Then create the database and point the config at it:

```sql
CREATE DATABASE player_analytics CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Load order is resolved automatically: `PlayerAnalytics.Database` publishes `IDatabaseProvider` in `PostInit`, and `PlayerAnalytics.Core` consumes it in `OnAllModulesLoaded`. If the Database module isn't installed, Core logs an error and connection logging is disabled.

## ⚙️ Configuration

Both files live in `<sharp>/configs/` and are written with defaults on first run.

### `playeranalytics.database.jsonc`

| Setting | Default | Meaning |
|---------|---------|---------|
| `Database:Type` | `mysql` | Database engine: `mysql` or `postgresql` (case-insensitive). |
| `Database:Host` | `localhost` | Database host. |
| `Database:Port` | `3306` | Database port. |
| `Database:Database` | `player_analytics` | Database name. |
| `Database:User` | `root` | Database user. |
| `Database:Password` | _(placeholder)_ | Database password — set before first connect. |

### `playeranalytics.jsonc`

| Setting | Default | Meaning |
|---------|---------|---------|
| `Analytics:ServerId` | `default` | Human-readable server label stored in every row, so you can distinguish servers in SQL (e.g. `us-east-1`, `retake-1`). |

## 🔧 How it works

On client connect, Core inserts a row into `pa_connections` with the connect timestamp, map, IP, name, current real-player count and the player's admin groups (from AdminManager, when present). On disconnect it fills in `disconnected_at` and `duration_seconds` for that row. On load it also closes any sessions a previous crash left open for this `ServerId`. All timestamps are UTC.

### Table `pa_connections`

| Column | Type | Notes |
|--------|------|-------|
| `id` | BIGINT PK AUTO_INCREMENT | 64-bit to avoid overflow on busy servers. |
| `server_id` | VARCHAR(64) | The `ServerId` from config. |
| `steam_id` | VARCHAR(128) | SteamID64 as string. |
| `player_name` | VARCHAR(128) | Name at connect time. |
| `ip_address` | VARCHAR(64) | IPv4/IPv6 address (no port). |
| `map_name` | VARCHAR(128) | Map active at connect time. |
| `player_count` | INT | Real (non-bot) clients at connect time. |
| `admin_groups` | VARCHAR(256) | Comma-separated permissions from AdminManager; empty for non-admins. |
| `connected_at` | DATETIME | UTC connect timestamp. |
| `disconnected_at` | DATETIME NULL | UTC disconnect timestamp (null while connected). |
| `duration_seconds` | INT NULL | Session length (null while connected). |

Indexed on `steam_id` and `connected_at`.

### Differences from the SourceMod original

Some upstream columns require SourceMod extensions or client-side data that CS2 / ModSharp does not expose server-side, so they were dropped: GeoIP (`city`, `region`, `country*`), `os`, `html_motd_disabled`, Steam Prime `premium`, and `connect_method`. The upstream `server_ip` is replaced by the operator-defined `server_id`, the unix `connect_time` / `connect_date` columns are replaced by the `connected_at` DateTime, and SourceMod admin `flags` are replaced by ModSharp permission strings in `admin_groups`. The `PA_OnConnectionLogged` forward is replaced by the public interface below.

## 🧩 Public API

Other plugins consume `IPlayerAnalyticsService` (resolve in `OnAllModulesLoaded`):

```csharp
var pa = sharpModuleManager
    .GetOptionalSharpModuleInterface<IPlayerAnalyticsService>(IPlayerAnalyticsService.Identity)
    ?.Instance;

long? rowId = pa?.GetConnectionId(steamId);                          // active session row, or null
var history = await pa.GetConnectionHistoryAsync(steamId, limit: 20); // newest-first
var recent  = await pa.GetRecentConnectionsAsync(limit: 50);          // recent rows across all players
```

`GetConnectionId` is the equivalent of the SourceMod `PA_GetConnectionID` native. Reference `PlayerAnalytics.Shared` (and `PlayerAnalytics.Database.Shared` for the `ConnectionLogEntity` type) from consuming plugins.

## 📦 Build

```bash
dotnet build PlayerAnalytics.slnx -c Release
```

Outputs the two module folders under `.build/modules/` and the shared interface assemblies under `.build/shared/`.

## 🙏 Credits

Port of [sneak-it/PlayerAnalytics](https://github.com/sneak-it/PlayerAnalytics) by **Dr. McKay / Bara / sneaK**. CS2 / ModSharp port by **YappersHQ**.

---

<div align="center">
  <p>Made with ❤️ by <a href="https://github.com/yappershq">yappershq</a></p>
  <p>⭐ Star this repo if you find it useful!</p>
</div>
