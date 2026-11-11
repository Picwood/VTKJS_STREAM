using System.Text;
using System.Security.Cryptography;
using CaeJob.Native;
using FileInOut.Output;
using FileInOut.Output.Calculix;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record ProjectSummary(string Name, int Nodes, int Elements, string[] Parts,
                                    string[] Materials, string[] NodeSets, string[] ElementSets,
                                    string[] Surfaces, bool HasFrd, bool HasDat, bool HasBinaryResults,
                                    ProjectGeometrySummary? GeometrySource, ProjectGmshSetupSummary? MeshingSetup,
                                    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
                                    ProjectCadTopologySummary? CadTopology = null, int ActiveAnalysisSteps = 0, SectionCoverage? SectionAssignments = null, bool CanSolve = false);
public sealed record ProjectCadTopologySummary(int Points, int Curves, int Surfaces, int Volumes, int PhysicalGroups);
public sealed record ProjectGeometrySummary(string FileName, long Bytes, string Sha256);
public sealed record ProjectGmshSetupSummary(double MaximumElementSize, double? MinimumElementSize,
                                             string Algorithm, bool Optimize, bool SecondOrder,
                                             [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
                                             ProjectGmshSizeBox? SizeBox = null,
                                             [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
                                             ProjectGmshFaceSize? FaceSize = null);

/// <summary>UI-independent operations on the supported editable archive schema.</summary>
public static class ProjectService
{
    public static ProjectSummary Inspect(string path)
    {
        var project = StaticProjectArchive.Open(path);
        var coverage = ProjectSections.Coverage(project.Model);
        int activeSteps = project.Model.StepCollection.StepsList.Count(step => step.Active && step.RunAnalysis);
        return new(project.Model.Name, project.Model.Mesh.Nodes.Count, project.Model.Mesh.Elements.Count,
                   project.Model.Mesh.Parts.Keys.ToArray(), project.Model.Materials.Keys.ToArray(),
                   project.Model.Mesh.NodeSets.Keys.ToArray(), project.Model.Mesh.ElementSets.Keys.ToArray(),
                   project.Model.Mesh.Surfaces.Keys.ToArray(),
                   project.Frd != null, project.Dat != null, project.BinaryResults != null,
                   project.GeometrySource is { } geometry ? new(geometry.FileName, geometry.Data.LongLength,
                       Convert.ToHexString(SHA256.HashData(geometry.Data)).ToLowerInvariant()) : null,
                   ProjectGmshSettings.TryRead(project.Model) is { } setup
                       ? new(setup.MaximumElementSize, setup.MinimumElementSize, setup.Algorithm.ToString(), setup.Optimize, setup.SecondOrder, setup.SizeBox, setup.FaceSize)
                       : null,
                   project.GeometrySource?.Topology is { } topology ? new(
                       topology.Entities.Count(entity => entity.Dimension == 0), topology.Entities.Count(entity => entity.Dimension == 1),
                       topology.Entities.Count(entity => entity.Dimension == 2), topology.Entities.Count(entity => entity.Dimension == 3),
                       topology.PhysicalGroups.Length) : null,
                   activeSteps, coverage, activeSteps > 0 && coverage.CanSolve);
    }

    public static CadEntity[] CadSizingFaces(string projectPath)
    {
        var geometry = StaticProjectArchive.Open(projectPath).GeometrySource;
        if (geometry?.Topology == null || !CadFaceSizingBinding.SupportsSource(geometry.FileName)) return [];
        return geometry.Topology.Entities.Where(entity => entity.Dimension == 2).ToArray();
    }

    public static ProjectCadTopology CadTopology(string projectPath) =>
        StaticProjectArchive.Open(projectPath).GeometrySource?.Topology ??
            throw new NotSupportedException("This project has no retained CAD topology. Remesh its retained geometry to capture it.");

    public static void Export(string projectPath, string outputPath)
    {
        var project = StaticProjectArchive.Open(projectPath);
        string input = Input(project);
        using var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(input);
    }

    public static void ExportGeometry(string projectPath, string outputPath)
    {
        var geometry = StaticProjectArchive.Open(projectPath).GeometrySource ??
            throw new InvalidDataException("Project contains no retained geometry source.");
        string destination = Path.GetFullPath(outputPath);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".geometry-" + Guid.NewGuid().ToString("N") + ".tmp");
        if (File.Exists(destination)) throw new IOException("Geometry destination already exists.");
        try
        {
            File.WriteAllBytes(temporary, geometry.Data);
            File.Move(temporary, destination, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>Loads an independent project snapshot and prepares an isolated job directory.
    /// Preparation performs synchronous file I/O; GUI callers should dispatch it off the UI thread.</summary>
    public static ProjectSolveJob PrepareSolve(string projectPath, string jobRoot,
                                              string? solverPath = null, TimeSpan? timeout = null)
    {
        var duration = timeout ?? TimeSpan.FromMinutes(30);
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        var project = StaticProjectArchive.Open(projectPath);
        if (!project.Model.StepCollection.StepsList.Any(step => step.Active && step.RunAnalysis))
            throw new NotSupportedException("Add or activate an analysis step before solving.");
        if (!ProjectSections.Coverage(project.Model).CanSolve)
            throw new NotSupportedException("Assign active solid sections and active materials to every active element before solving.");
        string input = Input(project);
        string solver = NativeTool.Resolve("ccx", solverPath);
        string directory = Path.Combine(Path.GetFullPath(jobRoot), "job-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "analysis.inp"), input, new UTF8Encoding(false));
        return new ProjectSolveJob(project, directory, solver, duration);
    }

    private static string Input(ReopenedStaticProject project) =>
        CalculixFileWriter.GetInputText(project.Model, ConvertPyramidsToEnum.Wedges);
}
