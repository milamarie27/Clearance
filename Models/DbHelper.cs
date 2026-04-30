using MySql.Data.MySqlClient;

namespace OnlineClearanceSystem.Data
{
    public static class DbHelper
    {
        public static MySqlConnection GetConnection(IConfiguration config)
        {
            var connStr = config.GetConnectionString("DefaultConnection");
            return new MySqlConnection(connStr);
        }
    }
}