using Microsoft.Data.SqlClient;

namespace WordPuzzleApi.Data;

/// <summary>
/// Thin connection factory. Reads the connection string from configuration
/// (set via IIS environment variables in production — see deploy notes),
/// falling back to a local SQL Express instance for `dotnet run` testing.
/// </summary>
public class Db
{
    private readonly string _connectionString;

    public Db(IConfiguration config)
    {
        _connectionString = config["ConnectionStrings:WordPuzzle"]
            ?? Environment.GetEnvironmentVariable("WORDPUZZLE_CONNECTION_STRING")
            ?? @"Server=localhost\SQLEXPRESS;Database=WordPuzzle;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    public SqlConnection Open()
    {
        var conn = new SqlConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
