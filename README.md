# cs2-player-analytics

A CS2 ModSharp port of [PlayerAnalytics](https://github.com/sneak-it/PlayerAnalytics) by Dr. McKay / Bara / sneaK.

Logs per-session connection data for every non-bot player (connect time, map, IP, admin groups, duration) into MySQL or PostgreSQL.

---

## Architecture

Three-tier ModSharp plugin structure:

| Project | Description |
|---|---|
| `PlayerAnalytics.Database.Shared` | ORM-agnostic DB interfaces + entity definitions. Reference this from external plugins that query the data. |
| `PlayerAnalytics.Database` | SqlSugar provider implementation (MySQL / PostgreSQL). Registers `IDatabaseProvider` via SharpModuleManager. |
| `PlayerAnalytics.Shared` | Public `IPlayerAnalyticsService` interface for cross-plugin access. |
| `PlayerAnalytics.Core` | Main plugin — hooks client connect/disconnect, writes rows. |

---

## Database Schema

Table: **`pa_connections`**

| Column | Type | Notes |
|---|---|---|
| `id` | BIGINT PK AUTO_INCREMENT | 64-bit avoids int overflow on busy servers |
| `server_id` | VARCHAR(64) | Human-readable server label from config |
| `steam_id` | VARCHAR(128) | SteamID64 as string |
| `player_name` | VARCHAR(128) | Player name at connect time |
| `ip_address` | VARCHAR(64) | IPv4/IPv6 address (no port) |
| `map_name` | VARCHAR(128) | Map active at connect time |
| `player_count` | INT | Real (non-bot) clients at connect time |
| `admin_groups` | VARCHAR(256) | Comma-separated permissions from AdminManager |
| `admin_groups` | VARCHAR(256) | Empty string for non-admins |
| `connected_at` | DATETIME | UTC connect timestamp |
| `disconnected_at` | DATETIME NULL | UTC disconnect timestamp (null while connected) |
| `duration_seconds` | INT NULL | Session length in seconds (null while connected) |

Indexes: `steam_id`, `connected_at`.

### Changes vs upstream SourceMod schema

| Upstream column | Status | Reason |
|---|---|---|
| `city`, `region`, `country`, `country_code`, `country_code3` | **Dropped** | No GeoIP extension available in ModSharp/CS2 |
| `os` | **Dropped** | Client convar OS detection not exposed in CS2 server-side API |
| `html_motd_disabled` | **Dropped** | MOTD not exposed in CS2 |
| `premium` | **Dropped** | SteamWorks extension not available in ModSharp/CS2 |
| `connect_method` | **Dropped** | `cl_connectmethod` convar not accessible server-side in CS2 |
| `server_ip` | Replaced by `server_id` | Operator-defined label is more useful than raw IP |
| `connect_date` | Removed (redundant) | `connected_at` (full UTC datetime) subsumes it |
| `flags` | Replaced by `admin_groups` | CS2/ModSharp uses permission strings, not legacy SourceMod flag chars |
| `connect_time` (unix int) | Replaced by `connected_at` (DateTime) | Cleaner SQL queries, timezone-safe |
| `duration` (updated in-place) | Same logic, updated on disconnect | Preserved |

---

## ConVars / Configuration

### `sharp/configs/playeranalytics.database.jsonc`

```jsonc
{
    "Database": {
        // Supported types: mysql, postgresql
        "Type": "mysql",
        "Host": "localhost",
        "Port": 3306,
        "Database": "player_analytics",
        "User": "root",
        "Password": "YOUR_PASSWORD_HERE"
    }
}
```

### `sharp/configs/playeranalytics.jsonc`

```jsonc
{
    "Analytics": {
        // Identifies this server in the database — use something meaningful
        // so you can distinguish servers in SQL queries.
        "ServerId": "default"
    }
}
```

Both config files are auto-created with defaults on first run.

---

## Commands

None exposed to players. This is a passive data-collection plugin.

External plugins can call `IPlayerAnalyticsService.GetConnectionId(steamId)` to retrieve the active row ID for a connected player (equivalent to the SourceMod `PA_GetConnectionID` native).

---

## Deployment

1. Build:
   ```bash
   unset version && dotnet build PlayerAnalytics.slnx -c Release
   ```

2. Copy `.build/modules/PlayerAnalytics.Database/` and `.build/modules/PlayerAnalytics.Core/` to your server's `sharp/modules/` directory.

3. Create the database and user:
   ```sql
   CREATE DATABASE player_analytics CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   CREATE USER 'pa_user'@'localhost' IDENTIFIED BY 'your_password';
   GRANT ALL ON player_analytics.* TO 'pa_user'@'localhost';
   ```

4. Edit `sharp/configs/playeranalytics.database.jsonc` with your credentials.

5. Set `ServerId` in `sharp/configs/playeranalytics.jsonc`.

6. Load order: `PlayerAnalytics.Database` must load before `PlayerAnalytics.Core`. ModSharp resolves this via SharpModuleManager — Database publishes `IDatabaseProvider` in `PostInit`, Core consumes it in `OnAllModulesLoaded`.

---

## Dropped Features

The following SourceMod features were not ported because the required APIs do not exist in ModSharp/CS2:

- **GeoIP lookup** — `GeoipCode2`, `GeoipCity`, etc. require a SourceMod GeoIP extension. No equivalent in ModSharp.
- **OS detection** — Required querying OS-specific client ConVars via gamedata. Not accessible in CS2's server-side plugin API.
- **MOTD disabled check** — CS2 does not expose `cl_disablehtmlmotd` server-side.
- **Steam Prime status** — Required the SteamWorks SourceMod extension. Not available in ModSharp.
- **Connect method** — `cl_connectmethod` not exposed via CS2 server API.
- **PA_OnConnectionLogged forward** — Replaced by `IPlayerAnalyticsService` interface; other plugins consume via SharpModuleManager.

---

## Credits

Original plugin: [PlayerAnalytics](https://github.com/sneak-it/PlayerAnalytics) by **Dr. McKay / Bara / sneaK**  
CS2/ModSharp port: **YappersHQ**
