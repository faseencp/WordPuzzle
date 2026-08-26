using TheCallAttendanceApi.Data;
using TheCallAttendanceApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Db>();

var app = builder.Build();

// Logs unhandled exceptions to stdout without leaking details to callers.
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

app.MapAttendanceEndpoints();

app.Run();
