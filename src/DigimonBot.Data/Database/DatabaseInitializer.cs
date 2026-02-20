using Microsoft.Data.Sqlite;

namespace DigimonBot.Data.Database;

/// <summary>
/// 数据库初始化器
/// </summary>
public class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly string _dataDirectory;

    public DatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString;
        
        // 提取数据目录路径
        if (connectionString.StartsWith("Data Source="))
        {
            var dbPath = connectionString["Data Source=".Length..];
            _dataDirectory = Path.GetDirectoryName(dbPath) ?? "Data";
        }
        else
        {
            _dataDirectory = "Data";
        }
    }

    /// <summary>
    /// 初始化数据库（创建表结构）
    /// </summary>
    public void Initialize()
    {
        // 确保数据目录存在
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }

        using var connection = CreateConnection();
        connection.Open();

        // 创建表
        CreateUserEconomyTable(connection);
        CreateUserDigimonStateTable(connection);
        CreateUserInventoryTable(connection);
        CreatePurchaseRecordTable(connection);
        CreateCheckInRecordTable(connection);
    }

    /// <summary>
    /// 创建数据库连接
    /// </summary>
    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private void CreateUserEconomyTable(SqliteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS UserEconomy (
                UserId TEXT PRIMARY KEY NOT NULL,
                Gold INTEGER NOT NULL DEFAULT 0,
                LastDailyReward TEXT
            );
            
            CREATE INDEX IF NOT EXISTS idx_usereconomy_gold ON UserEconomy(Gold);
        ";

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private void CreateUserDigimonStateTable(SqliteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS UserDigimonState (
                UserId TEXT NOT NULL,
                GroupId TEXT NOT NULL DEFAULT '',
                CurrentDigimonId TEXT NOT NULL,
                Courage INTEGER NOT NULL DEFAULT 0,
                Friendship INTEGER NOT NULL DEFAULT 0,
                Love INTEGER NOT NULL DEFAULT 0,
                Knowledge INTEGER NOT NULL DEFAULT 0,
                TotalTokensConsumed INTEGER NOT NULL DEFAULT 0,
                HatchTime TEXT NOT NULL,
                LastInteractionTime TEXT NOT NULL,
                PRIMARY KEY (UserId, GroupId)
            );
            
            CREATE INDEX IF NOT EXISTS idx_digimon_digimonid ON UserDigimonState(CurrentDigimonId);
            CREATE INDEX IF NOT EXISTS idx_digimon_lastinteraction ON UserDigimonState(LastInteractionTime);
        ";

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private void CreateUserInventoryTable(SqliteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS UserInventory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                ItemId TEXT NOT NULL,
                Quantity INTEGER NOT NULL DEFAULT 1 CHECK (Quantity >= 0),
                AcquiredAt TEXT NOT NULL,
                UNIQUE(UserId, ItemId)
            );
            
            CREATE INDEX IF NOT EXISTS idx_inventory_userid ON UserInventory(UserId);
            CREATE INDEX IF NOT EXISTS idx_inventory_itemid ON UserInventory(ItemId);
        ";

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private void CreatePurchaseRecordTable(SqliteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS PurchaseRecord (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                ItemId TEXT NOT NULL,
                Price INTEGER NOT NULL,
                PurchasedAt TEXT NOT NULL
            );
            
            CREATE INDEX IF NOT EXISTS idx_purchase_userid ON PurchaseRecord(UserId);
            CREATE INDEX IF NOT EXISTS idx_purchase_itemid ON PurchaseRecord(ItemId);
        ";

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private void CreateCheckInRecordTable(SqliteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS CheckInRecord (
                UserId TEXT PRIMARY KEY NOT NULL,
                TotalCheckIns INTEGER NOT NULL DEFAULT 0,
                ConsecutiveCheckIns INTEGER NOT NULL DEFAULT 0,
                LastCheckInDate TEXT NOT NULL DEFAULT '',
                HighTierRewards INTEGER NOT NULL DEFAULT 0
            );
            
            CREATE INDEX IF NOT EXISTS idx_checkin_date ON CheckInRecord(LastCheckInDate);
        ";

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 删除所有数据（重置数据库）
    /// </summary>
    public void ResetDatabase()
    {
        using var connection = CreateConnection();
        connection.Open();

        const string sql = @"
            DELETE FROM UserEconomy;
            DELETE FROM UserDigimonState;
            DELETE FROM UserInventory;
            DELETE FROM PurchaseRecord;
            DELETE FROM CheckInRecord;
            VACUUM;
        ";

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}
