namespace MssqlExporter;

public static class Endpoints
{
    public static void MapExporterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Redirect("/metrics"));
        app.MapGet("/healthz", () => Results.Json(new { status = "ok" }));
    }
}
