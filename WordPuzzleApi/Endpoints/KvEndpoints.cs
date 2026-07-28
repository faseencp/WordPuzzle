using Dapper;
using WordPuzzleApi.Data;
using WordPuzzleApi.Models;

namespace WordPuzzleApi.Endpoints;

/// <summary>
/// Generic string key/value store. Originally the Earth Map Challenge page
/// called a fictional `window.storage.get(key)/set(key, value)` API that only
/// exists in whatever tool generated the file -- this backs the same shape
/// with a real table, so the frontend only needed its two storage functions
/// swapped to call here instead of touching any of its actual game logic.
/// No auth: matches the original file's admin tab, which likewise had no
/// access control -- this isn't a new gap introduced here.
/// </summary>
public static class KvEndpoints
{
    public static void MapKvEndpoints(this WebApplication app)
    {
        app.MapGet("/kv/{key}", GetValue);
        app.MapPut("/kv/{key}", SetValue);
    }

    private static async Task<IResult> GetValue(string key, Db db)
    {
        using var conn = db.Open();
        var value = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT Value FROM dbo.KeyValueStore WHERE [Key] = @key", new { key });

        return value == null ? Results.NotFound() : Results.Ok(new KvGetResponse(value));
    }

    private static async Task<IResult> SetValue(string key, KvSetRequest req, Db db)
    {
        using var conn = db.Open();

        // MERGE (not update-then-insert-if-missing) so two concurrent writers
        // for the same key can't both see "doesn't exist yet" and collide on
        // the primary key -- the leaderboard key in particular can get
        // simultaneous writes from multiple players finishing at once.
        await conn.ExecuteAsync(
            @"MERGE dbo.KeyValueStore AS target
              USING (SELECT @key AS [Key]) AS source
              ON target.[Key] = source.[Key]
              WHEN MATCHED THEN UPDATE SET Value = @Value, UpdatedAtUtc = SYSUTCDATETIME()
              WHEN NOT MATCHED THEN INSERT ([Key], Value, UpdatedAtUtc) VALUES (@key, @Value, SYSUTCDATETIME());",
            new { key, req.Value });

        return Results.Ok();
    }
}
