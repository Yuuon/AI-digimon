using Dapper;
using DigimonBot.Core.Models;
using DigimonBot.Data.Database;
using Microsoft.Data.Sqlite;

namespace DigimonBot.Data.Repositories.Sqlite;

/// <summary>
/// SQLite 实现的用户经济数据仓库
/// </summary>
public class SqliteUserDataRepository : IUserDataRepository
{
    private readonly DatabaseInitializer _database;

    public SqliteUserDataRepository(DatabaseInitializer database)
    {
        _database = database;
    }

    public async Task<UserEconomy?> GetAsync(string userId)
    {
        using var connection = _database.CreateConnection();
        
        const string sql = @"
            SELECT UserId, Gold, LastDailyReward 
            FROM UserEconomy 
            WHERE UserId = @UserId
        ";
        
        var result = await connection.QueryFirstOrDefaultAsync<UserEconomyRow>(sql, new { UserId = userId });
        
        if (result == null)
            return null;
            
        return new UserEconomy
        {
            UserId = result.UserId,
            Gold = result.Gold,
            LastDailyReward = ParseDateTime(result.LastDailyReward)
        };
    }

    public async Task<UserEconomy> GetOrCreateAsync(string userId)
    {
        var existing = await GetAsync(userId);
        if (existing != null)
            return existing;

        // 创建新用户
        using var connection = _database.CreateConnection();
        
        const string sql = @"
            INSERT INTO UserEconomy (UserId, Gold, LastDailyReward)
            VALUES (@UserId, 0, NULL)
            ON CONFLICT(UserId) DO NOTHING
        ";
        
        await connection.ExecuteAsync(sql, new { UserId = userId });
        
        return new UserEconomy
        {
            UserId = userId,
            Gold = 0
        };
    }

    public async Task AddGoldAsync(string userId, int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        // 先确保用户存在
        await GetOrCreateAsync(userId);

        using var connection = _database.CreateConnection();
        
        const string sql = @"
            UPDATE UserEconomy 
            SET Gold = Gold + @Amount 
            WHERE UserId = @UserId
        ";
        
        await connection.ExecuteAsync(sql, new { UserId = userId, Amount = amount });
    }

    public async Task<bool> DeductGoldAsync(string userId, int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        using var connection = _database.CreateConnection();
        
        // 使用原子操作检查并扣减
        const string sql = @"
            UPDATE UserEconomy 
            SET Gold = Gold - @Amount 
            WHERE UserId = @UserId AND Gold >= @Amount
        ";
        
        var affected = await connection.ExecuteAsync(sql, new { UserId = userId, Amount = amount });
        return affected > 0;
    }

    public async Task SetGoldAsync(string userId, long amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        // 先确保用户存在
        await GetOrCreateAsync(userId);

        using var connection = _database.CreateConnection();
        
        const string sql = @"
            UPDATE UserEconomy 
            SET Gold = @Amount 
            WHERE UserId = @UserId
        ";
        
        await connection.ExecuteAsync(sql, new { UserId = userId, Amount = amount });
    }

    public async Task UpdateDailyRewardTimeAsync(string userId, DateTime time)
    {
        // 先确保用户存在
        await GetOrCreateAsync(userId);

        using var connection = _database.CreateConnection();
        
        const string sql = @"
            UPDATE UserEconomy 
            SET LastDailyReward = @Time 
            WHERE UserId = @UserId
        ";
        
        await connection.ExecuteAsync(sql, new { UserId = userId, Time = time.ToString("O") });
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
    private class UserEconomyRow
    {
        public string UserId { get; set; } = "";
        public long Gold { get; set; }
        public string? LastDailyReward { get; set; }
    }
}
