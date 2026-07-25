using WordPuzzleApi.Data;
using WordPuzzleApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Db>();

var app = builder.Build();

// TEMPORARY diagnostic middleware — surfaces the real exception in the HTTP
// response instead of a bare 500, since IIS stdout logging under the
// in-process hosting model wasn't producing log files during deployment.
// Remove this once the 500 on POST /rounds is diagnosed and fixed.
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        // Console.WriteLine first and unconditionally — this must land in the
        // stdout log regardless of whether the HTTP response write below
        // succeeds, so we can tell the two failure modes apart.
        Console.WriteLine("=== UNHANDLED EXCEPTION in " + context.Request.Path + " ===");
        Console.WriteLine("HasStarted: " + context.Response.HasStarted);
        Console.WriteLine(ex.ToString());
        Console.Out.Flush();

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = ex.ToString() });
        }
    }
});

app.MapRoundsEndpoints();
app.MapParticipantsEndpoints();
app.MapResultsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
