using Microsoft.Data.SqlClient;

namespace TheCallAttendanceApi.Data;

/// <summary>
/// Connection factory. Reads the connection string from configuration/
/// environment (set via IIS environment variables in production), falling
/// back to a local SQL Express instance for `dotnet run` testing.
/// </summary>
public class Db
{
    private readonly string _connectionString;

    public Db(IConfiguration config)
    {
        _connectionString = config["ConnectionStrings:TheCallAttendance"]
            ?? Environment.GetEnvironmentVariable("THECALL_CONNECTION_STRING")
            ?? @"Server=localhost\SQLEXPRESS;Database=TheCallAttendance;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    public SqlConnection Open()
    {
        var conn = new SqlConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
