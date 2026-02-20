using Dapper;
using DigimonBot.Core.Models;
using DigimonBot.Data.Database;

namespace DigimonBot.Data.Repositories.Sqlite;

/// <summary>
/// SQLite 实现的数码宝贝状态仓库
/// </summary>
public class SqliteDigimonStateRepository : IDigimonStateRepository
{
    private readonly DatabaseInitializer _database;
    private readonly IDigimonRepository _digimonRepository;

    public SqliteDigimonStateRepository(
        DatabaseInitializer database, 
        IDigimonRepository digimonRepository)
    {
        _database = database;
        _digimonRepository = digimonRepository;
    }

    public async Task<UserDigimonState> GetOrCreateAsync(
        string userId, 
        string? groupId = null, 
        string? defaultDigimonId = null)
    {
        var existing = await GetAsync(userId, groupId);
        if (existing != null)
            return existing;

        // 获取默认数码宝贝ID
        var digimonId = defaultDigimonId ?? _digimonRepository.GetDefaultEgg().Id;
        
        // 创建新状态
        var newState = UserDigimonState.CreateNew(userId, digimonId, groupId);
        await SaveAsync(newState);
        
        return newState;
    }

    public async Task<UserDigimonState?> GetAsync(string userId, string? groupId = null)
    {
        using var connection = _database.CreateConnection();
        
        // 将 null 转换为空字符串进行查询
        var normalizedGroupId = groupId ?? "";
        
        const string sql = @"
            SELECT UserId, GroupId, CurrentDigimonId, 
                   Courage, Friendship, Love, Knowledge,
                   TotalTokensConsumed, HatchTime, LastInteractionTime
            FROM UserDigimonState 
            WHERE UserId = @UserId AND GroupId = @GroupId
        ";
        
        var result = await connection.QueryFirstOrDefaultAsync<UserDigimonStateRow>(
            sql, 
            new { UserId = userId, GroupId = normalizedGroupId });
        
        if (result == null)
            return null;
            
        return MapToState(result);
    }

    public async Task SaveAsync(UserDigimonState state)
    {
        using var connection = _database.CreateConnection();
        
        // 确保 GroupId 不为 null
        var groupId = state.GroupId ?? "";
        
        // 注：不更新 TotalTokensConsumed，因为它由 RecordConversationAsync 单独管理
        const string sql = @"
            INSERT INTO UserDigimonState (
                UserId, GroupId, CurrentDigimonId, 
                Courage, Friendship, Love, Knowledge,
                TotalTokensConsumed, HatchTime, LastInteractionTime
            )
            VALUES (
                @UserId, @GroupId, @CurrentDigimonId,
                @Courage, @Friendship, @Love, @Knowledge,
                @TotalTokensConsumed, @HatchTime, @LastInteractionTime
            )
            ON CONFLICT(UserId, GroupId) DO UPDATE SET
                CurrentDigimonId = excluded.CurrentDigimonId,
                Courage = excluded.Courage,
                Friendship = excluded.Friendship,
                Love = excluded.Love,
                Knowledge = excluded.Knowledge,
                -- 不更新 TotalTokensConsumed，避免覆盖 RecordConversationAsync 的累加结果
                HatchTime = excluded.HatchTime,
                LastInteractionTime = excluded.LastInteractionTime
        ";
        
        await connection.ExecuteAsync(sql, new
        {
            state.UserId,
            GroupId = groupId,
            state.CurrentDigimonId,
            state.Courage,
            state.Friendship,
            state.Love,
            state.Knowledge,
            state.TotalTokensConsumed,
            HatchTime = state.HatchTime.ToString("O"),
            LastInteractionTime = state.LastInteractionTime.ToString("O")
        });
    }

    public async Task<UserDigimonState> ResetAsync(
        string userId, 
        string? groupId = null, 
        string? defaultDigimonId = null)
    {
        var digimonId = defaultDigimonId ?? _digimonRepository.GetDefaultEgg().Id;
        var normalizedGroupId = groupId ?? "";
        
        // 删除旧记录
        using var connection = _database.CreateConnection();
        
        const string deleteSql = @"
            DELETE FROM UserDigimonState 
            WHERE UserId = @UserId AND GroupId = @GroupId
        ";
        
        await connection.ExecuteAsync(deleteSql, new { UserId = userId, GroupId = normalizedGroupId });
        
        // 创建新状态
        var newState = UserDigimonState.CreateNew(userId, digimonId, normalizedGroupId);
        await SaveAsync(newState);
        
        return newState;
    }

    public async Task UpdateDigimonAsync(string userId, string newDigimonId, string? groupId = null)
    {
        using var connection = _database.CreateConnection();
        
        var normalizedGroupId = groupId ?? "";
        
        const string sql = @"
            UPDATE UserDigimonState 
            SET CurrentDigimonId = @NewDigimonId
            WHERE UserId = @UserId AND GroupId = @GroupId
        ";
        
        await connection.ExecuteAsync(sql, new 
        { 
            UserId = userId, 
            GroupId = normalizedGroupId, 
            NewDigimonId = newDigimonId 
        });
    }

    public async Task UpdateEmotionsAsync(string userId, EmotionValues emotions, string? groupId = null)
    {
        using var connection = _database.CreateConnection();
        
        var normalizedGroupId = groupId ?? "";
        
        const string sql = @"
            UPDATE UserDigimonState 
            SET Courage = @Courage,
                Friendship = @Friendship,
                Love = @Love,
                Knowledge = @Knowledge
            WHERE UserId = @UserId AND GroupId = @GroupId
        ";
        
        await connection.ExecuteAsync(sql, new
        {
            UserId = userId,
            GroupId = normalizedGroupId,
            emotions.Courage,
            emotions.Friendship,
            emotions.Love,
            emotions.Knowledge
        });
    }

    public async Task<int> RecordConversationAsync(
        string userId, 
        int tokensConsumed, 
        string? groupId = null, 
        int goldEarned = 0)
    {
        using var connection = _database.CreateConnection();
        
        var normalizedGroupId = groupId ?? "";
        
        // 更新 Token 消耗和互动时间
        const string sql = @"
            UPDATE UserDigimonState 
            SET TotalTokensConsumed = TotalTokensConsumed + @TokensConsumed,
                LastInteractionTime = @Now
            WHERE UserId = @UserId AND GroupId = @GroupId
        ";
        
        await connection.ExecuteAsync(sql, new
        {
            UserId = userId,
            GroupId = normalizedGroupId,
            TokensConsumed = tokensConsumed,
            Now = DateTime.Now.ToString("O")
        });
        
        // 同时增加金币
        const string goldSql = @"
            INSERT INTO UserEconomy (UserId, Gold, LastDailyReward)
            VALUES (@UserId, @GoldEarned, NULL)
            ON CONFLICT(UserId) DO UPDATE SET
                Gold = Gold + @GoldEarned
        ";
        
        await connection.ExecuteAsync(goldSql, new { UserId = userId, GoldEarned = goldEarned });
        
        return goldEarned;
    }

    public async Task<IReadOnlyList<UserDigimonState>> GetAllAsync()
    {
        using var connection = _database.CreateConnection();
        
        const string sql = @"
            SELECT UserId, GroupId, CurrentDigimonId, 
                   Courage, Friendship, Love, Knowledge,
                   TotalTokensConsumed, HatchTime, LastInteractionTime
            FROM UserDigimonState
        ";
        
        var results = await connection.QueryAsync<UserDigimonStateRow>(sql);
        return results.Select(MapToState).ToList();
    }

    private static UserDigimonState MapToState(UserDigimonStateRow row)
    {
        return new UserDigimonState
        {
            UserId = row.UserId,
            GroupId = row.GroupId,
            CurrentDigimonId = row.CurrentDigimonId,
            Courage = row.Courage,
            Friendship = row.Friendship,
            Love = row.Love,
            Knowledge = row.Knowledge,
            TotalTokensConsumed = row.TotalTokensConsumed,
            HatchTime = DateTime.Parse(row.HatchTime),
            LastInteractionTime = DateTime.Parse(row.LastInteractionTime)
        };
    }

    /// <summary>
    /// 内部使用的数据库行映射类
    /// </summary>
    private class UserDigimonStateRow
    {
        public string UserId { get; set; } = "";
        public string? GroupId { get; set; }
        public string CurrentDigimonId { get; set; } = "";
        public int Courage { get; set; }
        public int Friendship { get; set; }
        public int Love { get; set; }
        public int Knowledge { get; set; }
        public int TotalTokensConsumed { get; set; }
        public string HatchTime { get; set; } = "";
        public string LastInteractionTime { get; set; } = "";
    }
}
