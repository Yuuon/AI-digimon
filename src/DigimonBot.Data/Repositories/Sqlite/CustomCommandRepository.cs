using System.Text.Json;
using Dapper;
using DigimonBot.Core.Models;
using DigimonBot.Data.Database;

namespace DigimonBot.Data.Repositories.Sqlite;

/// <summary>
/// SQLite实现的自定义命令仓库
/// </summary>
public class CustomCommandRepository : ICustomCommandRepository
{
    private readonly KimiDatabaseInitializer _database;

    public CustomCommandRepository(KimiDatabaseInitializer database)
    {
        _database = database;
    }

    public async Task<CustomCommand> CreateAsync(CustomCommand command)
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            INSERT INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, LastUsedAt, UseCount)
            VALUES (@Name, @Aliases, @BinaryPath, @OwnerUserId, @RequiresWhitelist, @Description, @CreatedAt, @LastUsedAt, @UseCount);
            SELECT last_insert_rowid();
        ";

        var createdAt = DateTime.UtcNow.ToString("O");
        var id = await connection.ExecuteScalarAsync<int>(sql, new
        {
            Name = command.Name,
            Aliases = JsonSerializer.Serialize(command.Aliases),
            BinaryPath = command.BinaryPath,
            OwnerUserId = command.OwnerUserId,
            RequiresWhitelist = command.RequiresWhitelist ? 1 : 0,
            Description = command.Description,
            CreatedAt = createdAt,
            LastUsedAt = (string?)null,
            UseCount = 0
        });

        command.Id = id;
        command.CreatedAt = DateTime.Parse(createdAt);
        command.LastUsedAt = null;
        command.UseCount = 0;

        return command;
    }

    public async Task<CustomCommand?> GetByNameAsync(string name)
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            SELECT Id, Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, LastUsedAt, UseCount
            FROM CustomCommands
            WHERE Name = @Name
        ";

        var row = await connection.QueryFirstOrDefaultAsync<CustomCommandRow>(sql, new { Name = name });

        if (row == null)
            return null;

        return MapToEntity(row);
    }

    public async Task<CustomCommand?> GetByAliasAsync(string alias)
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            SELECT Id, Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, LastUsedAt, UseCount
            FROM CustomCommands
            WHERE Aliases LIKE '%""' || @Alias || '""%'
        ";

        var row = await connection.QueryFirstOrDefaultAsync<CustomCommandRow>(sql, new { Alias = alias });

        if (row == null)
            return null;

        return MapToEntity(row);
    }

    public async Task<bool> ExistsAsync(string name, string[]? aliases = null)
    {
        using var connection = _database.CreateConnection();

        // Check if name exists
        const string nameSql = @"
            SELECT COUNT(1) FROM CustomCommands WHERE Name = @Name
        ";

        var count = await connection.ExecuteScalarAsync<int>(nameSql, new { Name = name });
        if (count > 0)
            return true;

        // Check if any alias conflicts with existing names or aliases
        if (aliases != null && aliases.Length > 0)
        {
            foreach (var alias in aliases)
            {
                // Check if alias matches an existing command name
                var nameCount = await connection.ExecuteScalarAsync<int>(nameSql, new { Name = alias });
                if (nameCount > 0)
                    return true;

                // Check if alias matches an existing alias
                const string aliasSql = @"
                    SELECT COUNT(1) FROM CustomCommands WHERE Aliases LIKE '%""' || @Alias || '""%'
                ";

                var aliasCount = await connection.ExecuteScalarAsync<int>(aliasSql, new { Alias = alias });
                if (aliasCount > 0)
                    return true;
            }
        }

        return false;
    }

    public async Task<IEnumerable<CustomCommand>> ListAsync()
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            SELECT Id, Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, LastUsedAt, UseCount
            FROM CustomCommands
            ORDER BY CreatedAt DESC
        ";

        var rows = await connection.QueryAsync<CustomCommandRow>(sql);

        return rows.Select(MapToEntity);
    }

    public async Task UpdateUsageAsync(int id)
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            UPDATE CustomCommands
            SET UseCount = UseCount + 1, LastUsedAt = @LastUsedAt
            WHERE Id = @Id
        ";

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            LastUsedAt = DateTime.UtcNow.ToString("O")
        });
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            DELETE FROM CustomCommands WHERE Id = @Id
        ";

        await connection.ExecuteAsync(sql, new { Id = id });
    }

    private static CustomCommand MapToEntity(CustomCommandRow row)
    {
        string[] aliases = Array.Empty<string>();
        if (!string.IsNullOrEmpty(row.Aliases))
        {
            try
            {
                aliases = JsonSerializer.Deserialize<string[]>(row.Aliases) ?? Array.Empty<string>();
            }
            catch
            {
                aliases = Array.Empty<string>();
            }
        }

        return new CustomCommand
        {
            Id = row.Id,
            Name = row.Name,
            Aliases = aliases,
            BinaryPath = row.BinaryPath,
            OwnerUserId = row.OwnerUserId,
            RequiresWhitelist = row.RequiresWhitelist == 1,
            Description = row.Description ?? "",
            CreatedAt = ParseDateTime(row.CreatedAt) ?? DateTime.MinValue,
            LastUsedAt = ParseDateTime(row.LastUsedAt),
            UseCount = row.UseCount
        };
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (DateTime.TryParse(value, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// 内部使用的数据库行映射类
    /// </summary>
    private class CustomCommandRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Aliases { get; set; }
        public string BinaryPath { get; set; } = "";
        public string OwnerUserId { get; set; } = "";
        public int RequiresWhitelist { get; set; }
        public string? Description { get; set; }
        public string CreatedAt { get; set; } = "";
        public string? LastUsedAt { get; set; }
        public int UseCount { get; set; }
    }
}
