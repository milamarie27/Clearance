using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;

namespace OnlineClearanceSystem.Models
{
    public class DbHelper
    {
        private readonly string _connectionString;

        // Instance version (for dependency injection)
        public DbHelper(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        public MySqlConnection GetConnection()
        {
            return new MySqlConnection(_connectionString);
        }

        // ── Static version ─────────────────────────────────────
        // This is what ApiController.cs uses:
        // DbHelper.GetConnection(_config)
        public static MySqlConnection GetConnection(IConfiguration config)
        {
            var connStr = config.GetConnectionString("DefaultConnection")!;
            return new MySqlConnection(connStr);
        }
    }
}