using Npgsql;

namespace PaymentsService.Data
{
    public static class Db
    {
        public static string GetConnectionString(IConfiguration config)
        {
            string? cs = config.GetConnectionString("Db");
            if (!string.IsNullOrWhiteSpace(cs))
            {
                return cs;
            }

            string? dbUrl = config["DATABASE_URL"];
            if (!string.IsNullOrWhiteSpace(dbUrl))
            {
                return new NpgsqlConnectionStringBuilder(dbUrl) { Pooling = true }.ConnectionString;
            }

            throw new InvalidOperationException(
                "DB connection string is required. Set ConnectionStrings__Db or DATABASE_URL.");
        }
    }
}