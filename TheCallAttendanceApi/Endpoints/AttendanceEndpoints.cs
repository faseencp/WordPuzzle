using Dapper;
using TheCallAttendanceApi.Data;
using TheCallAttendanceApi.Models;

namespace TheCallAttendanceApi.Endpoints;

/// <summary>
/// Two independent tallies for "THE CALL" -- pre-event confirmation and
/// during-event attendance -- deliberately not linked to each other (no
/// personal code matching one to the other, per the chosen design). Each
/// kind allows at most one submission per DeviceId (a random ID the client
/// generates once and keeps in localStorage), enforced with a real unique
/// constraint server-side rather than just hiding the form after one use.
/// No admin auth on any of this -- the dashboard is intentionally public,
/// and submission is meant to be frictionless for participants.
/// </summary>
public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this WebApplication app)
    {
        app.MapPost("/confirmations", (SubmitRequest req, Db db) => Submit("confirmation", req, db));
        app.MapGet("/confirmations/summary", (Db db) => GetSummary("confirmation", db));
        app.MapPost("/attendances", (SubmitRequest req, Db db) => Submit("attendance", req, db));
        app.MapGet("/attendances/summary", (Db db) => GetSummary("attendance", db));
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
    }

    private static async Task<IResult> Submit(string kind, SubmitRequest req, Db db)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest("name is required.");
        if (!Countries.IsValid(req.Country))
            return Results.BadRequest("country must be one of the recognized list.");
        if (string.IsNullOrWhiteSpace(req.DeviceId))
            return Results.BadRequest("deviceId is required.");

        using var conn = db.Open();

        // Unique index on (Kind, DeviceId) is the real enforcement; this
        // pre-check just gives a clean 409 instead of a raw SQL error.
        var already = await conn.QuerySingleOrDefaultAsync<int?>(
            "SELECT TOP 1 Id FROM dbo.AttendanceEntries WHERE Kind = @kind AND DeviceId = @DeviceId",
            new { kind, req.DeviceId });
        if (already != null)
            return Results.Conflict("This device has already submitted.");

        try
        {
            await conn.ExecuteAsync(
                @"INSERT INTO dbo.AttendanceEntries (Kind, Name, Country, DeviceId, SubmittedAtUtc)
                  VALUES (@kind, @Name, @Country, @DeviceId, SYSUTCDATETIME())",
                new { kind, req.Name, req.Country, req.DeviceId });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // Lost a race with another request for the same device -- same
            // outcome as the pre-check catching it.
            return Results.Conflict("This device has already submitted.");
        }

        return Results.Ok(new SubmitResponse(true));
    }

    private static async Task<IResult> GetSummary(string kind, Db db)
    {
        using var conn = db.Open();

        var rows = await conn.QueryAsync<CountrySummary>(
            @"SELECT Country, COUNT(*) AS Count
              FROM dbo.AttendanceEntries
              WHERE Kind = @kind
              GROUP BY Country
              ORDER BY COUNT(*) DESC, Country ASC",
            new { kind });

        var list = rows.ToArray();
        return Results.Ok(new SummaryResponse(list.Sum(r => r.Count), list));
    }
}
