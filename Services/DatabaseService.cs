
using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Threading.Tasks;
using Serilog;

namespace moji.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly ILogger _logger;
        private const string CONNECTION_STRING = "Data Source={0}";

        public DatabaseService(ILogger logger)
        {
            _logger = logger;
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations.db");
            InitializeDatabase().Wait();
        }

        private async Task InitializeDatabase()
        {
            try
            {
                using var connection = new SqliteConnection(string.Format(CONNECTION_STRING, _dbPath));
                await connection.OpenAsync();

                // 创建翻译历史表
                const string sql = @"
                    CREATE TABLE IF NOT EXISTS Translations (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        OriginalText TEXT NOT NULL,
                        TranslatedText TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL
                    )";

                await connection.ExecuteAsync(sql);
                _logger.Information("数据库初始化成功");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "数据库初始化失败");
                throw;
            }
        }

        public async Task SaveTranslationAsync(string originalText, string translatedText)
        {
            try
            {
                using var connection = new SqliteConnection(string.Format(CONNECTION_STRING, _dbPath));
                const string sql = @"
                    INSERT INTO Translations (OriginalText, TranslatedText, CreatedAt) 
                    VALUES (@OriginalText, @TranslatedText, @CreatedAt)";

                await connection.ExecuteAsync(sql, new { 
                    OriginalText = originalText,
                    TranslatedText = translatedText,
                    CreatedAt = DateTime.Now.ToString("O")
                });
                _logger.Information("翻译记录已保存到数据库");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "保存翻译记录失败");
                throw;
            }
        }
    }
}