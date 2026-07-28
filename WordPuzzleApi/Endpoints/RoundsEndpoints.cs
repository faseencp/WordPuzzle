using Dapper;
using WordPuzzleApi.Data;
using WordPuzzleApi.Models;

namespace WordPuzzleApi.Endpoints;

public static class RoundsEndpoints
{
    // Countdown window between "host clicks Start" and the moment every
    // participant's timer actually begins. Gives every device's poll loop
    // (~1s interval) time to observe the same future StartedAtUtc and count
    // down to it in sync, rather than each starting the instant it hears about it.
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(3);

    // Routes are unprefixed (no leading "/api") because IIS's Application
    // virtual path (Default Web Site → wordpuzzle/api) already supplies that
    // segment — see DEPLOY.md. The frontend's API_BASE ('/wordpuzzle/api')
    // and these bare paths compose to the correct public URL without doubling up.
    public static void MapRoundsEndpoints(this WebApplication app)
    {
        app.MapPost("/rounds", CreateRound);
        app.MapPost("/rounds/{seed}/start", StartRound);
        app.MapGet("/rounds/{seed}/status", GetStatus);
    }

    private static async Task<IResult> CreateRound(CreateRoundRequest req, Db db, IConfiguration config)
    {
        if (!HostAuth.IsValid(config, req.HostKey))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Seed) || req.Words.Length == 0 || req.ParticipantCount <= 0)
            return Results.BadRequest("seed, words, and a positive participantCount are required.");

        using var conn = db.Open();

        var existing = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT Seed FROM dbo.Rounds WHERE Seed = @Seed", new { req.Seed });
        if (existing != null)
            return Results.Conflict("A round with this seed already exists.");

        await conn.ExecuteAsync(
            @"INSERT INTO dbo.Rounds (Seed, Tier, Category, GridSize, WordsCsv, ParticipantCount, Status, CreatedAtUtc)
              VALUES (@Seed, @Tier, @Category, @GridSize, @WordsCsv, @ParticipantCount, 0, SYSUTCDATETIME())",
            new
            {
                req.Seed,
                req.Tier,
                req.Category,
                req.GridSize,
                WordsCsv = string.Join(",", req.Words),
                req.ParticipantCount
            });

        var codes = Enumerable.Range(1, req.ParticipantCount)
            .Select(CodeGenerator.ToLetterCode)
            .ToArray();

        foreach (var code in codes)
        {
            await conn.ExecuteAsync(
                "INSERT INTO dbo.ParticipantCodes (Seed, Code, IsClaimed) VALUES (@Seed, @Code, 0)",
                new { req.Seed, Code = code });
        }

        return Results.Ok(new CreateRoundResponse(req.Seed, codes));
    }

    private static async Task<IResult> StartRound(string seed, StartRoundRequest req, Db db, IConfiguration config)
    {
        if (!HostAuth.IsValid(config, req.HostKey))
            return Results.Unauthorized();

        using var conn = db.Open();

        var round = await conn.QuerySingleOrDefaultAsync<Round>(
            "SELECT * FROM dbo.Rounds WHERE Seed = @seed", new { seed });
        if (round == null)
            return Results.NotFound();

        // Idempotent: if already started, just return the existing signal
        // rather than erroring — a host double-tapping "Start" (or a flaky
        // connection retrying) should not reset the countdown for everyone.
        if (round.StartedAtUtc.HasValue)
            return Results.Ok(new StartRoundResponse(round.StartedAtUtc.Value));

        var startedAt = DateTime.UtcNow.Add(StartDelay);
        await conn.ExecuteAsync(
            "UPDATE dbo.Rounds SET Status = 1, StartedAtUtc = @startedAt WHERE Seed = @seed",
            new { seed, startedAt });

        return Results.Ok(new StartRoundResponse(startedAt));
    }

    private static async Task<IResult> GetStatus(string seed, Db db)
    {
        using var conn = db.Open();
        var round = await conn.QuerySingleOrDefaultAsync<Round>(
            "SELECT * FROM dbo.Rounds WHERE Seed = @seed", new { seed });
        if (round == null)
            return Results.NotFound();

        return Results.Ok(new RoundStatusResponse(round.Status, round.StartedAtUtc, DateTime.UtcNow));
    }
}
