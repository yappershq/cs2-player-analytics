using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using PlayerAnalytics.Database.Shared;
using SqlSugar;

namespace PlayerAnalytics.Database.Extensions;

internal static class SugarExtensions
{
    internal static ConnectionConfig BuildConnectionConfig(IConfiguration configuration)
    {
        var dbTypeStr = configuration["Database:Type"] ?? "mysql";
        var host      = configuration["Database:Host"]     ?? "localhost";
        var port      = configuration["Database:Port"]     ?? "3306";
        var database  = configuration["Database:Database"] ?? "player_analytics";
        var user      = configuration["Database:User"]     ?? "root";
        var password  = configuration["Database:Password"] ?? "";

        var dbType = dbTypeStr.ToLowerInvariant() switch
        {
            "mysql"      => DbType.MySql,
            "postgresql" => DbType.PostgreSQL,
            _ => throw new NotSupportedException(
                $"Database type '{dbTypeStr}' is not supported. Supported types: mysql, postgresql"),
        };

        var connectionString = dbType switch
        {
            DbType.MySql       => $"Server={host};Port={port};Database={database};User={user};Password={password};AllowPublicKeyRetrieval=true;",
            DbType.PostgreSQL  => $"Host={host};Port={port};Database={database};Username={user};Password={password};",
            _                  => throw new NotSupportedException($"Database type '{dbTypeStr}' is not supported."),
        };

        return new ConnectionConfig
        {
            DbType               = dbType,
            ConnectionString     = connectionString,
            IsAutoCloseConnection = true,
            InitKeyType          = InitKeyType.Attribute,
            MoreSettings         = new ConnMoreSettings { DisableNvarchar = true },
            LanguageType         = LanguageType.English,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityNameService = (type, entity) =>
                {
                    var attr = type.GetCustomAttribute<DbTableAttribute>();
                    if (attr is not null)
                        entity.DbTableName = attr.TableName;
                },
                EntityService = (prop, column) =>
                {
                    var attr = prop.GetCustomAttribute<DbColumnAttribute>();
                    if (attr is null) return;

                    if (attr.IsPrimaryKey) column.IsPrimarykey  = true;
                    if (attr.IsIdentity)   column.IsIdentity    = true;
                    column.IsNullable = attr.IsNullable;
                    if (attr.IsPrimaryKey || attr.IsIdentity) column.IsNullable = false;
                    if (attr.Length > 0)                      column.Length     = attr.Length;
                    if (!string.IsNullOrEmpty(attr.DataType)) column.DataType   = attr.DataType;
                },
            },
        };
    }
}
