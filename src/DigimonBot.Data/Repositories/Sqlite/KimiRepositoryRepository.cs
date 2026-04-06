using Dapper;
using DigimonBot.Core.Models.Kimi;
using DigimonBot.Data.Database;
using Microsoft.Data.Sqlite;

namespace DigimonBot.Data.Repositories.Sqlite;

/// <summary>
/// SQLite实现的Kimi仓库数据仓库
/// </summary>
public class KimiRepositoryRepository : IKimiRepositoryRepository
{
    private readonly KimiDatabaseInitializer _database;

    public KimiRepositoryRepository(KimiDatabaseInitializer database)
    {
        _database = database;
    }

    public async Task<KimiRepository> CreateAsync(string name, string path)
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            INSERT INTO KimiRepositories (Name, Path, IsActive, CreatedAt, LastUsedAt, SessionCount)
            VALUES (@Name, @Path, 0, @CreatedAt, NULL, 0);
            SELECT last_insert_rowid();
        ";

        var createdAt = DateTime.UtcNow.ToString("O");
        var id = await connection.ExecuteScalarAsync<int>(sql, new
        {
            Name = name,
            Path = path,
            CreatedAt = createdAt
        });

        return new KimiRepository
        {
            Id = id,
            Name = name,
            Path = path,
            IsActive = false,
            CreatedAt = DateTime.Parse(createdAt),
            LastUsedAt = null,
            SessionCount = 0
        };
    }

    public async Task<KimiRepository?> GetByNameAsync(string name)
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            SELECT Id, Name, Path, IsActive, CreatedAt, LastUsedAt, SessionCount
            FROM KimiRepositories
            WHERE Name = @Name
        ";

        var row = await connection.QueryFirstOrDefaultAsync<KimiRepositoryRow>(sql, new { Name = name });

        if (row == null)
            return null;

        return MapToEntity(row);
    }

    public async Task<IEnumerable<KimiRepository>> GetAllAsync()
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            SELECT Id, Name, Path, IsActive, CreatedAt, LastUsedAt, SessionCount
            FROM KimiRepositories
            ORDER BY CreatedAt DESC
        ";

        var rows = await connection.QueryAsync<KimiRepositoryRow>(sql);

        return rows.Select(MapToEntity);
    }

    public async Task SetActiveAsync(string name)
    {
        using var connection = _database.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            // 先清除所有活动状态
            const string clearSql = "UPDATE KimiRepositories SET IsActive = 0 WHERE IsActive = 1";
            await connection.ExecuteAsync(clearSql, transaction: transaction);

            // 设置指定仓库为活动状态
            const string setSql = "UPDATE KimiRepositories SET IsActive = 1 WHERE Name = @Name";
            await connection.ExecuteAsync(setSql, new { Name = name }, transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<KimiRepository?> GetActiveAsync()
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            SELECT Id, Name, Path, IsActive, CreatedAt, LastUsedAt, SessionCount
            FROM KimiRepositories
            WHERE IsActive = 1
            LIMIT 1
        ";

        var row = await connection.QueryFirstOrDefaultAsync<KimiRepositoryRow>(sql);

        if (row == null)
            return null;

        return MapToEntity(row);
    }

    public async Task UpdateLastUsedAsync(string name)
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            UPDATE KimiRepositories
            SET LastUsedAt = @LastUsedAt
            WHERE Name = @Name
        ";

        await connection.ExecuteAsync(sql, new
        {
            Name = name,
            LastUsedAt = DateTime.UtcNow.ToString("O")
        });
    }

    public async Task IncrementSessionCountAsync(string name)
    {
        using var connection = _database.CreateConnection();

        const string sql = @"
            UPDATE KimiRepositories
            SET SessionCount = SessionCount + 1
            WHERE Name = @Name
        ";

        await connection.ExecuteAsync(sql, new { Name = name });
    }

    private static KimiRepository MapToEntity(KimiRepositoryRow row)
    {
        return new KimiRepository
        {
            Id = row.Id,
            Name = row.Name,
            Path = row.Path,
            IsActive = row.IsActive == 1,
            CreatedAt = ParseDateTime(row.CreatedAt) ?? DateTime.MinValue,
            LastUsedAt = ParseDateTime(row.LastUsedAt),
            SessionCount = row.SessionCount
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
    private class KimiRepositoryRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public int IsActive { get; set; }
        public string CreatedAt { get; set; } = "";
        public string? LastUsedAt { get; set; }
        public int SessionCount { get; set; }
    }
}
