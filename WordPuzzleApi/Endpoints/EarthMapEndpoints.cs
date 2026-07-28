using Dapper;
using WordPuzzleApi.Data;
using WordPuzzleApi.Models;

namespace WordPuzzleApi.Endpoints;

/// <summary>
/// Earth Map Challenge participant codes + live leaderboard. There's no
/// per-instance URL/seed for this file the way word search has one -- every
/// participant always plays at the same /earthmap/ link, pulling a random
/// subset of the admin-managed question pool (still stored via KvEndpoints).
/// BatchKey stands in for that: each "Generate Codes" click starts a new
/// batch, and the "current" batch is simply whichever was created most
/// recently, which also gives each new batch a clean leaderboard.
/// </summary>
public static class EarthMapEndpoints
{
    public static void MapEarthMapEndpoints(this WebApplication app)
    {
        app.MapPost("/earthmap/batches", CreateBatch);
        app.MapGet("/earthmap/current-batch", GetCurrentBatch);
        app.MapPost("/earthmap/batches/{batchKey}/claim", Claim);
        app.MapPost("/earthmap/batches/{batchKey}/results", SubmitResult);
        app.MapGet("/earthmap/batches/{batchKey}/leaderboard", GetLeaderboard);
        app.MapPost("/earthmap/batches/{batchKey}/clear-results", ClearResults);
        app.MapPost("/admin/login", AdminLogin);
    }

    private static IResult AdminLogin(AdminLoginRequest req, IConfiguration config)
    {
        return EarthMapAuth.IsValid(config, req.Username, req.Password)
            ? Results.Ok(new AdminLoginResponse(true))
            : Results.Unauthorized();
    }

    private static async Task<IResult> CreateBatch(CreateBatchRequest req, Db db, IConfiguration config)
    {
        if (!EarthMapAuth.IsValid(config, req.Username, req.Password))
            return Results.Unauthorized();

        if (req.ParticipantCount <= 0)
            return Results.BadRequest("A positive participantCount is required.");

        using var conn = db.Open();

        var batchKey = "em" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        await conn.ExecuteAsync(
            "INSERT INTO dbo.EarthMapBatches (BatchKey, ParticipantCount, CreatedAtUtc) VALUES (@batchKey, @ParticipantCount, SYSUTCDATETIME())",
            new { batchKey, req.ParticipantCount });

        var codes = Enumerable.Range(1, req.ParticipantCount)
            .Select(CodeGenerator.ToLetterCode)
            .ToArray();

        foreach (var code in codes)
        {
            await conn.ExecuteAsync(
                "INSERT INTO dbo.EarthMapCodes (BatchKey, Code, IsClaimed) VALUES (@batchKey, @code, 0)",
                new { batchKey, code });
        }

        return Results.Ok(new CreateBatchResponse(batchKey, codes));
    }

    private static async Task<IResult> GetCurrentBatch(Db db)
    {
        using var conn = db.Open();
        var batchKey = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT TOP 1 BatchKey FROM dbo.EarthMapBatches ORDER BY CreatedAtUtc DESC");

        return Results.Ok(new CurrentBatchResponse(batchKey));
    }

    private static async Task<IResult> Claim(string batchKey, EarthMapClaimRequest req, Db db)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return Results.BadRequest("code is required.");

        using var conn = db.Open();

        var code = await conn.QuerySingleOrDefaultAsync<EarthMapCode>(
            "SELECT * FROM dbo.EarthMapCodes WHERE BatchKey = @batchKey AND Code = @Code",
            new { batchKey, req.Code });

        if (code == null)
            return Results.NotFound("Unknown code for this competition.");

        // WHERE IsClaimed = 0 on the write itself (not just the read above)
        // so two simultaneous claims of the same code can't both succeed.
        var rowsUpdated = await conn.ExecuteAsync(
            "UPDATE dbo.EarthMapCodes SET IsClaimed = 1, ClaimedAtUtc = SYSUTCDATETIME() WHERE Id = @Id AND IsClaimed = 0",
            new { code.Id });

        if (rowsUpdated == 0)
            return Results.Conflict("This code has already been claimed.");

        return Results.Ok(new EarthMapClaimResponse(true, code.Id));
    }

    private static async Task<IResult> SubmitResult(string batchKey, SubmitEarthMapResultRequest req, Db db)
    {
        using var conn = db.Open();

        var code = await conn.QuerySingleOrDefaultAsync<EarthMapCode>(
            "SELECT * FROM dbo.EarthMapCodes WHERE BatchKey = @batchKey AND Code = @Code",
            new { batchKey, req.Code });

        if (code == null || !code.IsClaimed)
            return Results.BadRequest("Code must be claimed before submitting a result.");

        // First-write-wins, same as word search -- a participant can't watch
        // the live leaderboard and then resubmit a better score.
        var existing = await conn.QuerySingleOrDefaultAsync<int?>(
            "SELECT Score FROM dbo.EarthMapResults WHERE EarthMapCodeId = @Id", new { code.Id });

        int storedScore;
        if (existing == null)
        {
            await conn.ExecuteAsync(
                @"INSERT INTO dbo.EarthMapResults (EarthMapCodeId, BatchKey, Score, LocationCount, SubmittedAtUtc)
                  VALUES (@Id, @batchKey, @Score, @LocationCount, SYSUTCDATETIME())",
                new { code.Id, batchKey, req.Score, req.LocationCount });
            storedScore = req.Score;
        }
        else
        {
            storedScore = existing.Value;
        }

        var rank = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) + 1 FROM dbo.EarthMapResults WHERE BatchKey = @batchKey AND Score > @storedScore",
            new { batchKey, storedScore });

        return Results.Ok(new SubmitEarthMapResultResponse(true, rank));
    }

    private static async Task<IResult> GetLeaderboard(string batchKey, Db db)
    {
        using var conn = db.Open();

        var rows = await conn.QueryAsync<LeaderboardRowRaw>(
            @"SELECT ec.Code, r.Score, r.LocationCount, r.SubmittedAtUtc
              FROM dbo.EarthMapResults r
              JOIN dbo.EarthMapCodes ec ON ec.Id = r.EarthMapCodeId
              WHERE r.BatchKey = @batchKey
              ORDER BY r.Score DESC",
            new { batchKey });

        var ranked = rows.Select((row, i) => new EarthMapLeaderboardRow(i + 1, row.Code, row.Score, row.LocationCount, row.SubmittedAtUtc));
        return Results.Ok(ranked);
    }

    private record LeaderboardRowRaw(string Code, int Score, int LocationCount, DateTime SubmittedAtUtc);

    // Clears results for the CURRENT batch only, not code claims or history
    // from older batches -- "clear leaderboard" without wiping who's already
    // claimed a code in an in-progress competition.
    private static async Task<IResult> ClearResults(string batchKey, ClearResultsRequest req, Db db, IConfiguration config)
    {
        if (!EarthMapAuth.IsValid(config, req.Username, req.Password))
            return Results.Unauthorized();

        using var conn = db.Open();
        await conn.ExecuteAsync("DELETE FROM dbo.EarthMapResults WHERE BatchKey = @batchKey", new { batchKey });
        return Results.Ok();
    }
}
