using System.Globalization;
using CaeJob;
using CaeJob.Native;
using CaeMesh;
using CaeMesh.Meshing;
using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public enum ProjectGmshStatus { Prepared, Running, Importing, Succeeded, Failed, Cancelled, TimedOut }
public enum ProjectGmshAlgorithm { Automatic, Delaunay, Frontal, Hxt }

public sealed record ProjectGmshSizeBox(double MeshSize, double XMin, double YMin, double ZMin,
                                        double XMax, double YMax, double ZMax)
{
    internal BoxMeshSize ToMeshSetup() => new("GmshLocalBox")
    {
        MeshSize = MeshSize, XMin = XMin, YMin = YMin, ZMin = ZMin,
        XMax = XMax, YMax = YMax, ZMax = ZMax
    };
    internal static ProjectGmshSizeBox FromMeshSetup(BoxMeshSize box) =>
        new(box.MeshSize, box.XMin, box.YMin, box.ZMin, box.XMax, box.YMax, box.ZMax);
}

public sealed record ProjectGmshFaceSize(int FaceTag, double MeshSize, double TransitionDistance);

public sealed record ProjectGmshOptions(double MaximumElementSize, double? MinimumElementSize = null,
                                        ProjectGmshAlgorithm Algorithm = ProjectGmshAlgorithm.Automatic,
                                        bool Optimize = false, bool SecondOrder = false, ProjectGmshSizeBox? SizeBox = null,
                                        ProjectGmshFaceSize? FaceSize = null)
{
    internal void Validate()
    {
        if (!double.IsFinite(MaximumElementSize) || MaximumElementSize <= 0)
            throw new ArgumentException("Require a finite maximum element size greater than zero.");
        if (MinimumElementSize is { } minimum &&
            (!double.IsFinite(minimum) || minimum <= 0 || minimum > MaximumElementSize))
            throw new ArgumentException("Minimum element size must be finite, positive and no greater than the maximum.");
        if (SizeBox is { } box && (!box.ToMeshSetup().IsProperlyDefined() ||
            box.MeshSize > MaximumElementSize || box.MeshSize < (MinimumElementSize ?? 0)))
            throw new ArgumentException("Local box requires finite increasing bounds and a positive size within global min/max limits.");
        if (FaceSize is { } face && (face.FaceTag <= 0 || !double.IsFinite(face.MeshSize) || face.MeshSize <= 0 ||
            face.MeshSize > MaximumElementSize || face.MeshSize < (MinimumElementSize ?? 0) ||
            !double.IsFinite(face.TransitionDistance) || face.TransitionDistance <= 0))
            throw new ArgumentException("Select a positive CAD face tag, a size within global min/max limits and a finite positive transition distance.");
        if (!Enum.IsDefined(Algorithm)) throw new ArgumentException("Select a supported Gmsh algorithm.");
    }
}

public sealed record ProjectGmshResult(ProjectGmshStatus Status, string? ProjectPath,
                                       NativeJobResult? Mesher, Exception? Error);

internal static class ProjectGmshSettings
{
    internal const string ParametersName = "GmshSizing";
    internal const string TetrahedralName = "TetrahedralGmsh";

    internal static void Apply(FeModel model, ProjectGmshOptions options, ProjectGeometrySource? geometry)
    {
        options.Validate();
        if (model.Mesh.MeshSetupItems.Count != 0)
            throw new NotSupportedException("The mesh already contains setup items.");
        var parameters = new MeshingParameters(ParametersName)
        {
            MaxH = options.MaximumElementSize,
            MinH = options.MinimumElementSize ?? 0,
            SecondOrder = options.SecondOrder,
            RelativeSize = false
        };
        var tetrahedral = new TetrahedralGmsh(TetrahedralName)
        {
            AlgorithmMesh3D = options.Algorithm switch
            {
                ProjectGmshAlgorithm.Automatic or ProjectGmshAlgorithm.Delaunay => GmshAlgorithmMesh3DEnum.Delaunay,
                ProjectGmshAlgorithm.Frontal => GmshAlgorithmMesh3DEnum.Frontal,
                ProjectGmshAlgorithm.Hxt => GmshAlgorithmMesh3DEnum.HXT,
                _ => throw new ArgumentException("Select a supported Gmsh algorithm.")
            },
            OptimizeFirstOrderSolid = options.Optimize
                ? GmshOptimizeFirstOrderSolidEnum.Gmsh : GmshOptimizeFirstOrderSolidEnum.None,
            NumberOfThreads = 1
        };
        model.Mesh.MeshSetupItems.Add(parameters.Name, parameters);
        model.Mesh.MeshSetupItems.Add(tetrahedral.Name, tetrahedral);
        if (options.SizeBox is { } box)
        {
            var setup = box.ToMeshSetup();
            model.Mesh.MeshSetupItems.Add(setup.Name, setup);
        }
        if (options.FaceSize is { } face)
        {
            var topology = geometry?.Topology ?? throw new InvalidDataException("CAD face sizing requires retained topology.");
            var setup = new CadFaceMeshSize("GmshFaceSizing")
            {
                FaceTag = face.FaceTag, MeshSize = face.MeshSize, TransitionDistance = face.TransitionDistance,
                SourceSha256 = topology.SourceSha256, TopologySha256 = CadFaceSizingBinding.Fingerprint(topology, geometry!.Data)
            };
            model.Mesh.MeshSetupItems.Add(setup.Name, setup);
            CadFaceSizingBinding.Validate(model, geometry);
        }
    }

    internal static ProjectGmshOptions? TryRead(FeModel model)
    {
        if (model.Mesh.MeshSetupItems.Count == 0) return null;
        if (model.Mesh.MeshSetupItems.Count is < 2 or > 4 ||
            model.Mesh.MeshSetupItems.Values.Count(item => item is BoxMeshSize or CadFaceMeshSize) != model.Mesh.MeshSetupItems.Count - 2 ||
            model.Mesh.MeshSetupItems.Values.Count(item => item is BoxMeshSize) > 1 ||
            model.Mesh.MeshSetupItems.Values.Count(item => item is CadFaceMeshSize) > 1 ||
            model.Mesh.MeshSetupItems.Values.OfType<MeshingParameters>().SingleOrDefault() is not { } parameters ||
            model.Mesh.MeshSetupItems.Values.OfType<TetrahedralGmsh>().SingleOrDefault() is not { } tetrahedral)
            throw new InvalidDataException("The project does not contain a supported global Gmsh setup.");
        var algorithm = tetrahedral.AlgorithmMesh3D switch
        {
            GmshAlgorithmMesh3DEnum.Delaunay => ProjectGmshAlgorithm.Delaunay,
            GmshAlgorithmMesh3DEnum.Frontal => ProjectGmshAlgorithm.Frontal,
            GmshAlgorithmMesh3DEnum.HXT => ProjectGmshAlgorithm.Hxt,
            _ => throw new InvalidDataException("The project contains an unsupported Gmsh volume algorithm.")
        };
        return new(parameters.MaxH, parameters.MinH == 0 ? null : parameters.MinH, algorithm,
            tetrahedral.OptimizeFirstOrderSolid == GmshOptimizeFirstOrderSolidEnum.Gmsh, parameters.SecondOrder,
            model.Mesh.MeshSetupItems.Values.OfType<BoxMeshSize>().SingleOrDefault() is { } box
                ? ProjectGmshSizeBox.FromMeshSetup(box) : null,
            model.Mesh.MeshSetupItems.Values.OfType<CadFaceMeshSize>().SingleOrDefault() is { } face
                ? new(face.FaceTag, face.MeshSize, face.TransitionDistance) : null);
    }
}

/// <summary>One-shot distribution-Gmsh CAD meshing job that publishes a new editable project.</summary>
public sealed class ProjectGmshJob
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".step", ".stp", ".iges", ".igs", ".brep", ".geo" };
    private readonly string _source;
    private readonly string _destination;
    private readonly double _youngsModulus;
    private readonly double _poissonsRatio;
    private readonly NativeJob _mesher;
    private readonly string _temporaryDirectory;
    private readonly string _meshPath;
    private readonly string _topologyPath;
    private readonly string _stagedProjectPath;
    private readonly ProjectGeometrySource _geometrySource;
    private readonly ProjectGmshOptions _options;
    private readonly string? _expectedTopologyFingerprint;
    private int _started;
    private int _status = (int)ProjectGmshStatus.Prepared;

    public ProjectGmshStatus Status => (ProjectGmshStatus)Volatile.Read(ref _status);
    public NativeProcessProgress Progress => _mesher.Progress;
    public TimeSpan MesherElapsed => _mesher.Elapsed;
    public string SourcePath => _source;
    public string DestinationPath => _destination;

    internal ProjectGmshJob(string sourceDescription, ProjectGeometrySource geometrySource, string destination,
                            double youngsModulus, double poissonsRatio, ProjectGmshOptions options, string gmsh)
    {
        _source = Path.GetFullPath(sourceDescription);
        _destination = Path.GetFullPath(destination);
        string extension = Path.GetExtension(geometrySource.FileName);
        if (!SupportedExtensions.Contains(extension))
            throw new NotSupportedException("Select STEP, IGES, BREP or Gmsh GEO geometry.");
        if (geometrySource.FileName != Path.GetFileName(geometrySource.FileName) ||
            geometrySource.Data.LongLength is 0 or > 128L * 1024 * 1024)
            throw new NotSupportedException("CAD geometry must have a safe filename and be nonempty and at most 128 MiB.");
        if (!Path.GetExtension(_destination).Equals(".pma", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The new project must end in .pma.");
        if (File.Exists(_destination)) throw new IOException("The destination project already exists.");
        if (!Directory.Exists(Path.GetDirectoryName(_destination)))
            throw new DirectoryNotFoundException("The destination directory does not exist.");
        if (!double.IsFinite(youngsModulus) || youngsModulus <= 0 ||
            !double.IsFinite(poissonsRatio) || poissonsRatio <= -1 || poissonsRatio >= .5)
            throw new ArgumentException("Require finite E > 0 and -1 < nu < 0.5.");
        options.Validate();
        if (options.FaceSize is { } face)
        {
            if (!CadFaceSizingBinding.SupportsSource(geometrySource.FileName) || geometrySource.Topology is not { } retained)
                throw new NotSupportedException("Face refinement requires a retained STEP, IGES or BREP topology. Mesh the CAD first, then select a face for remeshing.");
            if (!retained.Entities.Any(entity => entity.Dimension == 2 && entity.Tag == face.FaceTag))
                throw new ArgumentException("The selected face does not exist in the retained CAD topology.");
            _expectedTopologyFingerprint = CadFaceSizingBinding.Fingerprint(retained, geometrySource.Data);
        }
        _youngsModulus = youngsModulus;
        _poissonsRatio = poissonsRatio;
        _options = options;
        byte[] sourceBytes = geometrySource.Data.ToArray();
        _geometrySource = new(geometrySource.FileName, sourceBytes);
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "prepomax-gmsh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
        string geometryPath = Path.Combine(_temporaryDirectory, "source" + extension.ToLowerInvariant());
        File.WriteAllBytes(geometryPath, sourceBytes);
        _meshPath = Path.Combine(_temporaryDirectory, "volume.msh");
        _stagedProjectPath = Path.Combine(Path.GetDirectoryName(_destination)!, "." + Path.GetFileNameWithoutExtension(_destination) +
            ".prepomax-" + Guid.NewGuid().ToString("N") + ".pma");
        string meshingInput = Path.Combine(_temporaryDirectory, "mesh-driver.geo");
        _topologyPath = Path.Combine(_temporaryDirectory, "topology.msh");
        string load = extension.Equals(".geo", StringComparison.OrdinalIgnoreCase) ? "Include" : "Merge";
        // Only our fixed basename and validated invariant numbers enter this GEO program.
        string script = (load == "Merge" ? "SetFactory(\"OpenCASCADE\");\n" : "") + load + " \"source" +
            extension.ToLowerInvariant() + "\";\n";
        var localFields = new List<string>();
        if (options.SizeBox is { } local)
        {
            localFields.Add("prepomaxSizeField");
            script += "prepomaxSizeField = newf;\nField[prepomaxSizeField] = Box;\n";
            foreach (var setting in new (string Name, double Value)[]
            {
                ("VIn", local.MeshSize), ("VOut", options.MaximumElementSize),
                ("XMin", local.XMin), ("YMin", local.YMin), ("ZMin", local.ZMin),
                ("XMax", local.XMax), ("YMax", local.YMax), ("ZMax", local.ZMax)
            }) script += "Field[prepomaxSizeField]." + setting.Name + " = " + Number(setting.Value) + ";\n";
        }
        if (options.FaceSize is { } faceControl)
        {
            script += "prepomaxFaceDistance = newf;\nField[prepomaxFaceDistance] = Distance;\n" +
                "Field[prepomaxFaceDistance].SurfacesList = {" + faceControl.FaceTag.ToString(CultureInfo.InvariantCulture) + "};\n" +
                "Field[prepomaxFaceDistance].Sampling = 50;\n" +
                "prepomaxFaceThreshold = newf;\nField[prepomaxFaceThreshold] = Threshold;\n" +
                "Field[prepomaxFaceThreshold].InField = prepomaxFaceDistance;\n" +
                "Field[prepomaxFaceThreshold].SizeMin = " + Number(faceControl.MeshSize) + ";\n" +
                "Field[prepomaxFaceThreshold].SizeMax = " + Number(options.MaximumElementSize) + ";\n" +
                "Field[prepomaxFaceThreshold].DistMin = 0;\n" +
                "Field[prepomaxFaceThreshold].DistMax = " + Number(faceControl.TransitionDistance) + ";\n";
            localFields.Add("prepomaxFaceThreshold");
        }
        if (localFields.Count > 0)
        {
            script += "prepomaxCombinedSize = newf;\nField[prepomaxCombinedSize] = Min;\n" +
                "Field[prepomaxCombinedSize].FieldsList = {" + string.Join(",", localFields) + "};\n" +
                "Background Field = prepomaxCombinedSize;\nMesh.MeshSizeExtendFromBoundary = 0;\n" +
                "Mesh.MeshSizeFromPoints = 0;\nMesh.MeshSizeFromCurvature = 0;\n" +
                "Mesh.MeshSizeMin = " + Number(options.MinimumElementSize ?? 0) + ";\n" +
                "Mesh.MeshSizeMax = " + Number(options.MaximumElementSize) + ";\nMesh.MeshSizeFactor = 1;\n";
        }
        // Save the synchronized CAD boundary graph before generating any mesh.
        script += "Mesh.MshFileVersion = 4.1;\nMesh.Binary = 0;\nMesh.SaveWithoutOrphans = 0;\n" +
            "Mesh.ScalingFactor = 1;\nSave \"topology.msh\";\nMesh.MshFileVersion = 2.2;\n";
        File.WriteAllText(meshingInput, script);
        var arguments = new List<string> { meshingInput, "-3", "-format", "msh2", "-o", _meshPath,
            "-order", options.SecondOrder ? "2" : "1", "-nt", "1", "-clmax", Number(options.MaximumElementSize), "-v", "2" };
        if (options.MinimumElementSize is { } minimum)
        {
            arguments.Add("-clmin");
            arguments.Add(Number(minimum));
        }
        string? algorithm = options.Algorithm switch
        {
            ProjectGmshAlgorithm.Automatic => null,
            ProjectGmshAlgorithm.Delaunay => "del3d",
            ProjectGmshAlgorithm.Frontal => "front3d",
            ProjectGmshAlgorithm.Hxt => "hxt",
            _ => throw new ArgumentException("Select a supported Gmsh algorithm.")
        };
        if (algorithm != null)
        {
            arguments.Add("-algo");
            arguments.Add(algorithm);
        }
        if (options.Optimize) arguments.Add("-optimize");
        // Straight-sided quadratic elements preserve positive affine tetrahedral Jacobians.
        arguments.AddRange(["-setnumber", "Mesh.SecondOrderLinear", "1"]);
        _mesher = new NativeJob(gmsh, arguments, _temporaryDirectory,
            TimeSpan.FromMinutes(5), captureLimit: 256 * 1024);
    }

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public async Task<ProjectGmshResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("A Gmsh job can only run once.");
        NativeJobResult? mesher = null;
        ProjectGmshResult Complete(ProjectGmshStatus status, string? path, Exception? error)
        {
            Volatile.Write(ref _status, (int)status);
            return new(status, path, mesher, error);
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref _status, (int)ProjectGmshStatus.Running);
            mesher = await _mesher.RunAsync(cancellationToken).ConfigureAwait(false);
            if (mesher.Status != JobStatus.OK || mesher.Process?.ExitCode != 0)
                return Complete(mesher.Status switch
                {
                    JobStatus.Killed => ProjectGmshStatus.Cancelled,
                    JobStatus.TimedOut => ProjectGmshStatus.TimedOut,
                    _ => ProjectGmshStatus.Failed
                }, null, mesher.Error);
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(_meshPath))
                return Complete(ProjectGmshStatus.Failed, null,
                    new InvalidDataException("Gmsh did not produce a volume mesh."));
            Volatile.Write(ref _status, (int)ProjectGmshStatus.Importing);
            if (!File.Exists(_topologyPath) || new FileInfo(_topologyPath).Length > CadTopologyCodec.MaximumBytes)
                throw new InvalidDataException("Gmsh did not produce a bounded CAD topology snapshot.");
            var topology = CadTopologyCodec.ReadGmsh(File.ReadAllBytes(_topologyPath), _geometrySource.Data);
            if (_expectedTopologyFingerprint != null &&
                CadFaceSizingBinding.Fingerprint(topology, _geometrySource.Data) != _expectedTopologyFingerprint)
                throw new InvalidDataException("CAD topology changed during remeshing. Capture a fresh project and select the face again.");
            ProjectMeshImport.Import(_meshPath, _stagedProjectPath, _youngsModulus, _poissonsRatio,
                _geometrySource with { Topology = topology }, _options);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(_stagedProjectPath, _destination, false);
            return Complete(ProjectGmshStatus.Succeeded, _destination, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Complete(ProjectGmshStatus.Cancelled, null, null);
        }
        catch (Exception ex)
        {
            return Complete(ProjectGmshStatus.Failed, null, ex);
        }
        finally
        {
            try { Directory.Delete(_temporaryDirectory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            try { File.Delete(_stagedProjectPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

public static class ProjectGmshImport
{
    public static ProjectGmshJob Prepare(string source, string destination, double youngsModulus,
                                         double poissonsRatio, double maximumElementSize,
                                         string? configuredGmsh = null)
        => Prepare(source, destination, youngsModulus, poissonsRatio,
                   new ProjectGmshOptions(maximumElementSize), configuredGmsh);

    public static ProjectGmshJob Prepare(string source, string destination, double youngsModulus,
                                         double poissonsRatio, ProjectGmshOptions options,
                                         string? configuredGmsh = null)
    {
        string fullSource = Path.GetFullPath(source);
        if (!File.Exists(fullSource)) throw new FileNotFoundException("CAD geometry was not found.", fullSource);
        if (new FileInfo(fullSource).Length is 0 or > 128 * 1024 * 1024)
            throw new NotSupportedException("CAD geometry must be nonempty and at most 128 MiB.");
        var geometry = new ProjectGeometrySource(Path.GetFileName(fullSource), File.ReadAllBytes(fullSource));
        return new(fullSource, geometry, destination, youngsModulus, poissonsRatio, options,
                   NativeTool.Resolve("gmsh", configuredGmsh));
    }
}

public static class ProjectGmshRemesh
{
    public static ProjectGmshJob Prepare(string sourceProject, string destination,
                                         string? configuredGmsh = null)
    {
        string fullSource = Path.GetFullPath(sourceProject);
        var project = StaticProjectArchive.Open(fullSource);
        var options = ProjectGmshSettings.TryRead(project.Model) ??
            throw new NotSupportedException("This project has no retained Gmsh setup. Specify remesh controls once first.");
        return Prepare(fullSource, project, destination, options, configuredGmsh);
    }

    public static ProjectGmshJob Prepare(string sourceProject, string destination, ProjectGmshOptions options,
                                         string? configuredGmsh = null)
    {
        string fullSource = Path.GetFullPath(sourceProject);
        var project = StaticProjectArchive.Open(fullSource);
        return Prepare(fullSource, project, destination, options, configuredGmsh);
    }

    private static ProjectGmshJob Prepare(string fullSource, ReopenedStaticProject project, string destination,
                                          ProjectGmshOptions options, string? configuredGmsh)
    {
        var geometry = project.GeometrySource ??
            throw new NotSupportedException("This project has no retained CAD geometry to remesh.");
        string[] assigned = project.Model.Sections.Values.OfType<SolidSection>()
            .Select(section => section.MaterialName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (assigned.Length != 1 || !project.Model.Materials.TryGetValue(assigned[0], out var material) ||
            material.TemperatureDependent || material.Properties.OfType<Elastic>().Count() != 1)
            throw new NotSupportedException("Remeshing requires one constant elastic material assigned to all solid sections.");
        var elastic = material.Properties.OfType<Elastic>().Single();
        if (elastic.YoungsPoissonsTemp.Length != 1)
            throw new NotSupportedException("Remeshing does not support a temperature-dependent elastic table.");
        double youngsModulus = elastic.YoungsPoissonsTemp[0][0].Value;
        double poissonsRatio = elastic.YoungsPoissonsTemp[0][1].Value;
        return new(fullSource, geometry, destination, youngsModulus, poissonsRatio, options,
                   NativeTool.Resolve("gmsh", configuredGmsh));
    }
}
