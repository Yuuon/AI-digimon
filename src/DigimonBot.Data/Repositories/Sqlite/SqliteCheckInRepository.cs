using Dapper;
using DigimonBot.Core.Models;
using DigimonBot.Data.Database;

namespace DigimonBot.Data.Repositories.Sqlite;

/// <summary>
/// SQLite 签到记录仓库实现
/// </summary>
public class SqliteCheckInRepository : ICheckInRepository
{
    private readonly DatabaseInitializer _database;

    public SqliteCheckInRepository(DatabaseInitializer database)
    {
        _database = database;
    }

    public async Task<CheckInRecord?> GetAsync(string userId)
    {
        using var connection = _database.CreateConnection();
        
        const string sql = @"
            SELECT UserId, TotalCheckIns, ConsecutiveCheckIns, LastCheckInDate, HighTierRewards
            FROM CheckInRecord
            WHERE UserId = @UserId
        ";
        
        return await connection.QueryFirstOrDefaultAsync<CheckInRecord>(sql, new { UserId = userId });
    }

    public async Task<CheckInRecord> GetOrCreateAsync(string userId)
    {
        var existing = await GetAsync(userId);
        if (existing != null)
            return existing;

        // 创建新记录
        var newRecord = new CheckInRecord
        {
            UserId = userId,
            TotalCheckIns = 0,
            ConsecutiveCheckIns = 0,
            LastCheckInDate = "",
            HighTierRewards = 0
        };

        using var connection = _database.CreateConnection();
        
        const string sql = @"
            INSERT INTO CheckInRecord (UserId, TotalCheckIns, ConsecutiveCheckIns, LastCheckInDate, HighTierRewards)
            VALUES (@UserId, @TotalCheckIns, @ConsecutiveCheckIns, @LastCheckInDate, @HighTierRewards)
        ";
        
        await connection.ExecuteAsync(sql, newRecord);
        return newRecord;
    }

    public async Task UpdateAsync(CheckInRecord record)
    {
        using var connection = _database.CreateConnection();
        
        const string sql = @"
            UPDATE CheckInRecord
            SET TotalCheckIns = @TotalCheckIns,
                ConsecutiveCheckIns = @ConsecutiveCheckIns,
                LastCheckInDate = @LastCheckInDate,
                HighTierRewards = @HighTierRewards
            WHERE UserId = @UserId
        ";
        
        await connection.ExecuteAsync(sql, record);
    }

    public async Task<bool> HasCheckedInTodayAsync(string userId)
    {
        var record = await GetAsync(userId);
        if (record == null)
            return false;
        
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        return record.LastCheckInDate == today;
    }

    public async Task<CheckInRecord> CheckInAsync(string userId)
    {
        var record = await GetOrCreateAsync(userId);
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        
        // 检查今天是否已经签到
        if (record.LastCheckInDate == today)
        {
            return record; // 今天已签到，返回当前记录
        }
        
        // 更新连续签到天数
        if (record.LastCheckInDate == yesterday)
        {
            // 昨天签到过，连续签到+1
            record.ConsecutiveCheckIns++;
        }
        else
        {
            // 昨天没签到，连续签到重置为1
            record.ConsecutiveCheckIns = 1;
        }
        
        // 更新总签到天数
        record.TotalCheckIns++;
        record.LastCheckInDate = today;
        
        await UpdateAsync(record);
        return record;
    }
}
