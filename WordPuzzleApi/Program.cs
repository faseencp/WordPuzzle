using WordPuzzleApi.Data;
using WordPuzzleApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Db>();

var app = builder.Build();

app.MapRoundsEndpoints();
app.MapParticipantsEndpoints();
app.MapResultsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
