using PrePoMax.Application;

namespace PrePoMax.Web;

internal static class ParityEndpoints
{
    internal static void MapParityEndpoints(this WebApplication app)
    {
        // No arbitrary code execution. The ordinary workspace ownership/lock middleware
        // applies; this bounded diagnostic query is absent from normal product launches.
        if (Environment.GetEnvironmentVariable("PREPOMAX_WEB_PARITY") != "1") return;
        app.MapGet("/api/parity/snapshot", (HttpContext context, string? nodeSet, string? fieldName,
            string? component, int step = 1, int increment = 1) =>
        {
            var workspace = (Workspace)context.Items[typeof(Workspace)]!;
            return Results.Ok(ProjectParitySnapshot.Read(workspace.RequireProject(), nodeSet, fieldName, component, step, increment));
        });
    }
}
