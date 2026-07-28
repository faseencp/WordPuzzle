using WordPuzzleApi.Data;
using WordPuzzleApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Db>();

var app = builder.Build();

// Logs unhandled exceptions to the (now-confirmed-working) stdout log without
// leaking exception details to callers — the earlier diagnostic version that
// echoed the exception in the HTTP response was for finding the
// InvariantGlobalization/SqlClient bug during deployment and has served its purpose.
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        Console.WriteLine("=== UNHANDLED EXCEPTION in " + context.Request.Path + " ===");
        Console.WriteLine(ex.ToString());
        Console.Out.Flush();

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
        }
    }
});

app.MapRoundsEndpoints();
app.MapParticipantsEndpoints();
app.MapResultsEndpoints();
app.MapKvEndpoints();
app.MapEarthMapEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
