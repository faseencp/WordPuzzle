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
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = ex.ToString() });
    }
});

app.MapRoundsEndpoints();
app.MapParticipantsEndpoints();
app.MapResultsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
