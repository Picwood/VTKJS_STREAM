using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using PrePoMax.Application;
using PrePoMax.Web;

var builder = WebApplication.CreateBuilder(args);
string RequiredPath(string name) => Path.GetFullPath(builder.Configuration[name] ?? throw new InvalidOperationException($"Set {name}."));
string rendererAssembly = RequiredPath("PREPOMAX_WEB_RENDER_SERVER");
string clientRoot = RequiredPath("PREPOMAX_WEB_CLIENT_ROOT");
string dotnet = Path.GetFullPath(builder.Configuration["PREPOMAX_WEB_DOTNET"] ?? "/usr/bin/dotnet");
if (!File.Exists(rendererAssembly) || !File.Exists(Path.Combine(clientRoot, "workspace.html")))
    throw new InvalidOperationException("Build the renderer and browser workspace before starting the web host.");
int port = int.Parse(builder.Configuration["PREPOMAX_WEB_PORT"] ?? "5180", System.Globalization.CultureInfo.InvariantCulture);
if (port is < 1024 or > 65535) throw new InvalidOperationException("PREPOMAX_WEB_PORT must be 1024–65535.");
// Keep the first pilot private to the workstation. A hosted release needs identity, quotas and durable storage.
builder.WebHost.ConfigureKestrel(options => { options.ListenLocalhost(port); options.Limits.MaxRequestBodySize = 64L * 1024 * 1024; });
builder.Services.AddSingleton<WorkspaceStore>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<WorkspaceStore>());
var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; worker-src 'self' blob:; frame-ancestors 'none'";
    context.Response.Headers.CacheControl = "no-store";
    // Prevent DNS rebinding of this cookie-owned local pilot.
    if (context.Request.Host.Host is not ("localhost" or "127.0.0.1" or "[::1]"))
    { context.Response.StatusCode = 403; return; }
    try
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            string origin = context.Request.Headers.Origin.ToString();
            if (context.Request.Headers["X-Workspace-Request"] != "1" ||
                origin.Length > 0 && origin != $"{context.Request.Scheme}://{context.Request.Host}")
                throw new WorkspaceException(403, "Use this workspace's own browser page to make changes.");
        }
        await next(context);
    }
    catch (Exception error) when (!context.Response.HasStarted)
    {
        if (error is OperationCanceledException && context.RequestAborted.IsCancellationRequested) return;
        int status = error switch
        {
            WorkspaceException known => known.Status,
            ProjectCommandConflictException => 409,
            BadHttpRequestException bad => bad.StatusCode,
            JsonException or ArgumentException or InvalidDataException or NotSupportedException or FormatException => 400,
            _ => 500,
        };
        // Production core diagnostics may contain server paths. Keep the public boundary path-neutral.
        string message = error is WorkspaceException or ProjectCommandConflictException ? error.Message : status == 400
            ? "The operation was rejected. Check the supported format, names, regions and numeric values; the current project is unchanged."
            : status == 413 ? "The upload exceeds 64 MiB." : "The operation failed. The workspace has not accepted a new project revision.";
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { message });
    }
});

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Path == "/api/workspace" && HttpMethods.IsPost(context.Request.Method))
    { await next(context); return; }
    var workspace = context.RequestServices.GetRequiredService<WorkspaceStore>().Find(context.Request.Cookies[WorkspaceStore.Cookie])
        ?? throw new WorkspaceException(401, "Your workspace has expired. Reload to start a new one; download projects before closing the pilot.");
    await workspace.Gate.WaitAsync(context.RequestAborted);
    try
    {
        if (workspace.Expires <= DateTimeOffset.UtcNow) throw new WorkspaceException(401, "The workspace has expired.");
        context.Items[typeof(Workspace)] = workspace;
        // Typed commands check their source revision after checking for an identical retry.
        if (!HttpMethods.IsGet(context.Request.Method) && context.Request.Path != "/api/workspace/commands/material" &&
            context.Request.Path != "/api/workspace/commands/material/rename" &&
            context.Request.Path != "/api/workspace/commands/material/create" &&
            context.Request.Path != "/api/workspace/commands/material/remove" &&
            context.Request.Path != "/api/workspace/commands/material/assign" &&
            context.Request.Headers.IfMatch.ToString() != workspace.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture))
            throw new WorkspaceException(409, "The project changed in another tab. Refresh the workspace before editing.");
        await next(context);
    }
    finally { workspace.Gate.Release(); }
});

app.MapPost("/api/workspace", (HttpContext context, WorkspaceStore store) =>
{
    var existing = store.Find(context.Request.Cookies[WorkspaceStore.Cookie]);
    if (existing == null)
    {
        var created = store.Create();
        context.Response.Cookies.Append(WorkspaceStore.Cookie, created.Token, new CookieOptions
        { HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = context.Request.IsHttps, Path = "/", MaxAge = TimeSpan.FromHours(8), IsEssential = true });
    }
    return Results.NoContent();
});

Workspace Current(HttpContext context) => (Workspace)context.Items[typeof(Workspace)]!;
app.MapParityEndpoints();
app.MapGet("/api/workspace", (HttpContext context) => Results.Ok(Current(context).Describe()));
app.MapGet("/api/workspace/commands", (HttpContext context) => Results.Ok(Current(context).CommandHistory()));
IResult ApplyCommand<TReceipt>(HttpContext context, IProjectCommand command, Func<Workspace, TReceipt> operation)
{
    if (context.Request.Headers.IfMatch.ToString() != command.ExpectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture))
        throw new WorkspaceException(409, "The command revision must match the request revision.");
    var workspace = Current(context);
    var receipt = operation(workspace);
    return Results.Ok(new { revision = workspace.Revision, receipt });
}
app.MapPost("/api/workspace/commands/material", (HttpContext context, MaterialElasticCommand command) =>
    ApplyCommand(context, command, workspace => workspace.Execute(command)));
app.MapPost("/api/workspace/commands/material/rename", (HttpContext context, MaterialRenameCommand command) =>
    ApplyCommand(context, command, workspace => workspace.Rename(command)));
app.MapPost("/api/workspace/commands/material/create", (HttpContext context, MaterialCreateCommand command) =>
    ApplyCommand(context, command, workspace => workspace.Create(command)));
app.MapPost("/api/workspace/commands/material/remove", (HttpContext context, MaterialRemoveCommand command) =>
    ApplyCommand(context, command, workspace => workspace.Remove(command)));
app.MapPost("/api/workspace/commands/material/assign", (HttpContext context, MaterialAssignCommand command) =>
    ApplyCommand(context, command, workspace => workspace.Assign(command)));
app.MapGet("/api/workspace/selection", (HttpContext context, string key) =>
    Results.Ok(ProjectModelTree.Select(Current(context).RequireProject(), key)));

app.MapPost("/api/workspace/import", async (HttpContext context) =>
{
    var workspace = Current(context);
    string extension = context.Request.Headers["X-File-Extension"].ToString().ToLowerInvariant();
    if (extension is not (".pma" or ".inp" or ".mesh" or ".msh"))
        throw new WorkspaceException(400, "Open a portable PMA project or import a mesh-only INP, Medit MESH or ASCII Gmsh MSH 2.2 file.");
    string upload = workspace.NewPath(extension), candidate = workspace.NewPath();
    try
    {
        await ReceiveAsync(context, upload);
        if (extension == ".pma")
        {
            using (var zip = ZipFile.OpenRead(upload))
                if (zip.Entries.Count > 16 || zip.Entries.Sum(entry => entry.Length) > 128L * 1024 * 1024)
                    throw new WorkspaceException(400, "Pilot projects support at most 128 MiB of expanded archive payloads.");
            File.Move(upload, candidate);
        }
        else
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (!double.TryParse(context.Request.Headers["X-Youngs-Modulus"], culture, out double e) ||
                !double.TryParse(context.Request.Headers["X-Poissons-Ratio"], culture, out double nu))
                throw new WorkspaceException(400, "Supply an elastic modulus and Poisson ratio for the imported mesh.");
            ProjectMeshImport.Import(upload, candidate, e, nu);
        }
        workspace.Commit(candidate);
        return Results.Ok(new { revision = workspace.Revision });
    }
    finally
    {
        if (File.Exists(upload)) File.Delete(upload);
        if (candidate != workspace.ProjectPath && File.Exists(candidate)) File.Delete(candidate);
    }
});

app.MapPost("/api/workspace/edit", (HttpContext context, EditRequest edit) =>
{
    var workspace = Current(context);
    if (edit.Action == "material-save")
    {
        var receipt = workspace.Execute(new MaterialElasticCommand(Guid.NewGuid(), workspace.Revision, edit.Name!, edit.E, edit.Nu));
        return Results.Ok(new { revision = workspace.Revision, receipt });
    }
    if (edit.Action == "material-add")
    {
        var receipt = workspace.Create(new MaterialCreateCommand(Guid.NewGuid(), workspace.Revision, edit.Name!,
            edit.E ?? throw new ArgumentException("Supply E."), edit.Nu ?? throw new ArgumentException("Supply nu.")));
        return Results.Ok(new { revision = workspace.Revision, receipt });
    }
    if (edit.Action == "section-assign")
    {
        var receipt = workspace.Assign(new MaterialAssignCommand(Guid.NewGuid(), workspace.Revision, edit.Name!, workspace.MaterialId(edit.Region)));
        return Results.Ok(new { revision = workspace.Revision, receipt });
    }
    string candidate = workspace.NewPath();
    try { workspace.Edit(edit, candidate); workspace.Commit(candidate); }
    finally { if (candidate != workspace.ProjectPath && File.Exists(candidate)) File.Delete(candidate); }
    return Results.Ok(new { revision = workspace.Revision });
});

app.MapPost("/api/workspace/{operation:regex(^(undo|redo)$)}", (HttpContext context, string operation) =>
{
    var workspace = Current(context);
    workspace.MoveHistory(operation == "redo");
    return Results.Ok(new { revision = workspace.Revision });
});

app.MapPost("/api/workspace/results", async (HttpContext context) =>
{
    var workspace = Current(context);
    string source = workspace.RequireProject(), upload = workspace.NewPath(".frd"), candidate = workspace.NewPath();
    try
    {
        await ReceiveAsync(context, upload);
        ProjectExternalResults.AttachCopy(source, candidate, upload);
        workspace.Commit(candidate);
        return Results.Ok(new { revision = workspace.Revision });
    }
    finally
    {
        if (File.Exists(upload)) File.Delete(upload);
        if (candidate != workspace.ProjectPath && File.Exists(candidate)) File.Delete(candidate);
    }
});

app.MapGet("/api/workspace/download/{kind:regex(^(project|input)$)}", async (HttpContext context, string kind) =>
{
    var workspace = Current(context);
    string source = workspace.RequireProject();
    context.Response.ContentType = "application/octet-stream";
    context.Response.Headers.ContentDisposition = $"attachment; filename=analysis-r{workspace.Revision}.{(kind == "project" ? "pma" : "inp")}";
    if (kind == "project") { await context.Response.SendFileAsync(source, context.RequestAborted); return; }
    string temporary = workspace.NewPath(".inp");
    try { ProjectService.Export(source, temporary); await context.Response.SendFileAsync(temporary, context.RequestAborted); }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
});

app.MapPost("/api/workspace/scene", async (HttpContext context, SceneRequest request) =>
    Results.Ok(await Current(context).PublishAsync(dotnet, rendererAssembly, request, context.RequestAborted)));

async Task StreamScene(HttpContext context, string sceneId, string? resourceId)
{
    var workspace = Current(context);
    if (workspace.Renderer == null || workspace.Scene?.SceneId != sceneId)
        throw new WorkspaceException(404, "This scene is no longer current in this workspace.");
    using var response = await workspace.Renderer.ReadSceneAsync(sceneId, resourceId, context.RequestAborted);
    context.Response.StatusCode = (int)response.StatusCode;
    context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
    context.Response.ContentLength = response.Content.Headers.ContentLength;
    await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
}
app.MapGet("/api/v1/scenes/{sceneId}/manifest", (HttpContext context, string sceneId) => StreamScene(context, sceneId, null));
app.MapGet("/api/v1/scenes/{sceneId}/resources/{resourceId}", (HttpContext context, string sceneId, string resourceId) => StreamScene(context, sceneId, resourceId));
// Deliberately no catch-all renderer proxy: host control, session creation and commands stay private.
app.MapGet("/", () => Results.Redirect("/workspace.html"));
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(clientRoot) });
app.Run();

static async Task ReceiveAsync(HttpContext context, string destination)
{
    const long maximum = 64L * 1024 * 1024;
    if (context.Request.ContentLength is > maximum) throw new WorkspaceException(413, "The upload exceeds 64 MiB.");
    await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    byte[] buffer = new byte[64 * 1024];
    long total = 0;
    int count;
    while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) != 0)
    {
        total += count;
        if (total > maximum) throw new WorkspaceException(413, "The upload exceeds 64 MiB.");
        await output.WriteAsync(buffer.AsMemory(0, count), context.RequestAborted);
    }
    if (total == 0) throw new WorkspaceException(400, "Select a nonempty file.");
}
