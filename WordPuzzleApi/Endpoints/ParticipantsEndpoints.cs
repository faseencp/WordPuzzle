using Dapper;
using WordPuzzleApi.Data;
using WordPuzzleApi.Models;

namespace WordPuzzleApi.Endpoints;

public static class ParticipantsEndpoints
{
    public static void MapParticipantsEndpoints(this WebApplication app)
    {
        app.MapPost("/rounds/{seed}/claim", Claim);
    }

    private static async Task<IResult> Claim(string seed, ClaimRequest req, Db db)
    {
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Unit))
            return Results.BadRequest("code and unit are required.");

        using var conn = db.Open();

        var pc = await conn.QuerySingleOrDefaultAsync<ParticipantCode>(
            "SELECT * FROM dbo.ParticipantCodes WHERE Seed = @seed AND Code = @Code",
            new { seed, req.Code });

        if (pc == null)
            return Results.NotFound("Unknown code for this round.");

        // Guard the claim itself with "WHERE IsClaimed = 0" (not just the earlier
        // read) so two simultaneous claims of the same code can't both succeed.
        var rowsUpdated = await conn.ExecuteAsync(
            @"UPDATE dbo.ParticipantCodes
              SET IsClaimed = 1, ClaimedUnit = @Unit, ClaimedName = @Name, ClaimedAtUtc = SYSUTCDATETIME()
              WHERE Id = @Id AND IsClaimed = 0",
            new { req.Unit, req.Name, pc.Id });

        if (rowsUpdated == 0)
            return Results.Conflict("This code has already been claimed.");

        return Results.Ok(new ClaimResponse(true, pc.Id));
    }
}
