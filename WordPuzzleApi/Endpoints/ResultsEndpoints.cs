using Dapper;
using WordPuzzleApi.Data;
using WordPuzzleApi.Models;

namespace WordPuzzleApi.Endpoints;

public static class ResultsEndpoints
{
    public static void MapResultsEndpoints(this WebApplication app)
    {
        app.MapPost("/rounds/{seed}/results", SubmitResult);
        app.MapGet("/rounds/{seed}/leaderboard", GetLeaderboard);
    }

    private static async Task<IResult> SubmitResult(string seed, SubmitResultRequest req, Db db)
    {
        using var conn = db.Open();

        var pc = await conn.QuerySingleOrDefaultAsync<ParticipantCode>(
            "SELECT * FROM dbo.ParticipantCodes WHERE Seed = @seed AND Code = @Code",
            new { seed, req.Code });

        if (pc == null || !pc.IsClaimed)
            return Results.BadRequest("Code must be claimed before submitting a result.");

        // First-write-wins: a participant can't watch the live leaderboard and
        // then resubmit an inflated score. If a result already exists for this
        // code, just return its rank rather than overwriting it.
        var existing = await conn.QuerySingleOrDefaultAsync<ResultEntry>(
            "SELECT * FROM dbo.Results WHERE ParticipantCodeId = @Id", new { pc.Id });

        // The stored values (whichever came first) are what rank is computed
        // against — either the just-inserted submission or the earlier one.
        int storedWordsFound, storedTimeSeconds;
        if (existing == null)
        {
            await conn.ExecuteAsync(
                @"INSERT INTO dbo.Results (ParticipantCodeId, Seed, WordsFound, TotalWords, TimeSeconds, SubmittedAtUtc)
                  VALUES (@Id, @seed, @WordsFound, @TotalWords, @TimeSeconds, SYSUTCDATETIME())",
                new { pc.Id, seed, req.WordsFound, req.TotalWords, req.TimeSeconds });
            storedWordsFound = req.WordsFound;
            storedTimeSeconds = req.TimeSeconds;
        }
        else
        {
            storedWordsFound = existing.WordsFound;
            storedTimeSeconds = existing.TimeSeconds;
        }

        var rank = await conn.QuerySingleAsync<int>(
            @"SELECT COUNT(*) + 1 FROM dbo.Results
              WHERE Seed = @seed
                AND (WordsFound > @storedWordsFound
                     OR (WordsFound = @storedWordsFound AND TimeSeconds < @storedTimeSeconds))",
            new { seed, storedWordsFound, storedTimeSeconds });

        return Results.Ok(new SubmitResultResponse(true, rank));
    }

    private static async Task<IResult> GetLeaderboard(string seed, Db db)
    {
        using var conn = db.Open();

        var rows = await conn.QueryAsync<LeaderboardRowRaw>(
            @"SELECT pc.ClaimedUnit AS Unit, r.WordsFound, r.TotalWords, r.TimeSeconds
              FROM dbo.Results r
              JOIN dbo.ParticipantCodes pc ON pc.Id = r.ParticipantCodeId
              WHERE r.Seed = @seed
              ORDER BY r.WordsFound DESC, r.TimeSeconds ASC",
            new { seed });

        var ranked = rows.Select((row, i) => new LeaderboardRow(
            i + 1, row.Unit ?? "", row.WordsFound, row.TotalWords, row.TimeSeconds));

        return Results.Ok(ranked);
    }

    private record LeaderboardRowRaw(string? Unit, int WordsFound, int TotalWords, int TimeSeconds);
}
