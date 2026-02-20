using Dapper;
using DigimonBot.Core.Models;
using DigimonBot.Data.Database;

namespace DigimonBot.Data.Repositories.Sqlite;

/// <summary>
/// SQLite 实现的背包仓库
/// </summary>
public class SqliteInventoryRepository : IInventoryRepository
{
    private readonly DatabaseInitializer _database;

    public SqliteInventoryRepository(DatabaseInitializer database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<UserItem>> GetInventoryAsync(string userId)
    {
        using var connection = _database.CreateConnection();
        
        const string sql = @"
            SELECT Id, UserId, ItemId, Quantity, AcquiredAt
            FROM UserInventory
            WHERE UserId = @UserId
            ORDER BY AcquiredAt DESC
        ";
        
        var results = await connection.QueryAsync<UserInventoryRow>(sql, new { UserId = userId });
        
        return results.Select(r => new UserItem
        {
            Id = r.Id,
            UserId = r.UserId,
            ItemId = r.ItemId,
            Quantity = r.Quantity,
            AcquiredAt = DateTime.Parse(r.AcquiredAt)
        }).ToList();
    }

    public async Task<int> GetItemQuantityAsync(string userId, string itemId)
    {
        using var connection = _database.CreateConnection();
        
        const string sql = @"
            SELECT Quantity
            FROM UserInventory
            WHERE UserId = @UserId AND ItemId = @ItemId
        ";
        
        var result = await connection.ExecuteScalarAsync<int?>(sql, new 
        { 
            UserId = userId, 
            ItemId = itemId 
        });
        
        return result ?? 0;
    }

    public async Task AddItemAsync(string userId, string itemId, int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));

        using var connection = _database.CreateConnection();
        
        const string sql = @"
            INSERT INTO UserInventory (UserId, ItemId, Quantity, AcquiredAt)
            VALUES (@UserId, @ItemId, @Quantity, @AcquiredAt)
            ON CONFLICT(UserId, ItemId) DO UPDATE SET
                Quantity = Quantity + @Quantity
        ";
        
        await connection.ExecuteAsync(sql, new
        {
            UserId = userId,
            ItemId = itemId,
            Quantity = quantity,
            AcquiredAt = DateTime.Now.ToString("O")
        });
    }

    public async Task<bool> RemoveItemAsync(string userId, string itemId, int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));

        using var connection = _database.CreateConnection();
        
        // 先检查数量
        const string checkSql = @"
            SELECT Quantity FROM UserInventory 
            WHERE UserId = @UserId AND ItemId = @ItemId
        ";
        
        var currentQty = await connection.ExecuteScalarAsync<int?>(checkSql, new 
        { 
            UserId = userId, 
            ItemId = itemId 
        });
        
        if (currentQty == null || currentQty < quantity)
            return false;

        // 执行扣减
        if (currentQty == quantity)
        {
            // 正好用完，删除记录
            const string deleteSql = @"
                DELETE FROM UserInventory 
                WHERE UserId = @UserId AND ItemId = @ItemId
            ";
            await connection.ExecuteAsync(deleteSql, new { UserId = userId, ItemId = itemId });
        }
        else
        {
            // 扣减数量
            const string updateSql = @"
                UPDATE UserInventory 
                SET Quantity = Quantity - @Quantity
                WHERE UserId = @UserId AND ItemId = @ItemId
            ";
            await connection.ExecuteAsync(updateSql, new 
            { 
                UserId = userId, 
                ItemId = itemId, 
                Quantity = quantity 
            });
        }
        
        return true;
    }

    public async Task<bool> HasItemAsync(string userId, string itemId, int quantity = 1)
    {
        var currentQty = await GetItemQuantityAsync(userId, itemId);
        return currentQty >= quantity;
    }

    public async Task<bool> UseItemAsync(string userId, string itemId)
    {
        return await RemoveItemAsync(userId, itemId, 1);
    }

    /// <summary>
    /// 清理用户的空库存记录（维护用）
    /// </summary>
    public async Task CleanupEmptyInventoryAsync(string userId)
    {
        using var connection = _database.CreateConnection();
        
        const string sql = @"
            DELETE FROM UserInventory 
            WHERE UserId = @UserId AND Quantity <= 0
        ";
        
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }

    /// <summary>
    /// 内部使用的数据库行映射类
    /// </summary>
    private class UserInventoryRow
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public string ItemId { get; set; } = "";
        public int Quantity { get; set; }
        public string AcquiredAt { get; set; } = "";
    }
}
