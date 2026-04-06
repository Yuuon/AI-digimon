using Microsoft.Data.Sqlite;

namespace DigimonBot.Data.Database;

/// <summary>
/// Kimi数据库初始化器 - 管理仓库和会话表
/// </summary>
public class KimiDatabaseInitializer
{
    private readonly string _connectionString;
    private readonly string _dataDirectory;

    /// <summary>
    /// 构造函数
    /// </summary>
    public KimiDatabaseInitializer(string connectionString)
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
        CreateKimiRepositoriesTable(connection);
        CreateKimiSessionsTable(connection);
    }

    /// <summary>
    /// 创建数据库连接
    /// </summary>
    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    /// <summary>
    /// 创建仓库表
    /// </summary>
    private void CreateKimiRepositoriesTable(SqliteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS KimiRepositories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                Path TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                LastUsedAt TEXT,
                SessionCount INTEGER DEFAULT 0
            );
            
            CREATE INDEX IF NOT EXISTS idx_kimi_repos_name ON KimiRepositories(Name);
            CREATE INDEX IF NOT EXISTS idx_kimi_repos_active ON KimiRepositories(IsActive);
        ";

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 创建会话表
    /// </summary>
    private void CreateKimiSessionsTable(SqliteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS KimiSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepoId INTEGER NOT NULL,
                UserId TEXT NOT NULL,
                Command TEXT NOT NULL,
                DurationMs INTEGER,
                Success INTEGER,
                ExecutedAt TEXT NOT NULL,
                FOREIGN KEY (RepoId) REFERENCES KimiRepositories(Id)
            );
            
            CREATE INDEX IF NOT EXISTS idx_kimi_sessions_repoid ON KimiSessions(RepoId);
            CREATE INDEX IF NOT EXISTS idx_kimi_sessions_userid ON KimiSessions(UserId);
            CREATE INDEX IF NOT EXISTS idx_kimi_sessions_executedat ON KimiSessions(ExecutedAt);
        ";

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 删除所有Kimi数据（重置数据库）
    /// </summary>
    public void ResetDatabase()
    {
        using var connection = CreateConnection();
        connection.Open();

        const string sql = @"
            DELETE FROM KimiSessions;
            DELETE FROM KimiRepositories;
            VACUUM;
        ";

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}
