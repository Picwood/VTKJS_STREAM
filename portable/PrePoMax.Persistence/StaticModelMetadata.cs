using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CaeGlobals;
using CaeMesh;
using CaeMesh.Meshing;
using CaeModel;
using FileInOut.Output;
using FileInOut.Output.Calculix;

namespace PrePoMax.Persistence;

/// <summary>Versioned analysis-definition metadata for a bounded, numeric static model.</summary>
public static class StaticModelMetadata
{
    internal sealed record ArchiveData(byte[] Metadata, byte[] Mesh);
    private sealed record Entity(string Kind, Dictionary<string, JsonElement> Values);
    private readonly record struct Node(int Id, double X, double Y, double Z);
    private readonly record struct Element(int Id, int[] Nodes, string? Kind = null);
    private sealed record PartData(Entity State, int Id, int[] Elements, FeElementTypeLinearHexa HexaType,
        FeElementTypeLinearTetra TetraType = FeElementTypeLinearTetra.None,
        FeElementTypeParabolicTetra ParabolicTetraType = FeElementTypeParabolicTetra.None,
        FeElementTypeParabolicWedge ParabolicWedgeType = FeElementTypeParabolicWedge.None,
        FeElementTypeParabolicHexa ParabolicHexaType = FeElementTypeParabolicHexa.None);
    private sealed record Set(string Name, int[] Labels, bool CreatedFromParts = false);
    private sealed record Surface(string Name, string? NodeSet, string? SourceNodeSet, FeSurfaceType Type, FeSurfaceCreatedFrom CreatedFrom, double Area, int[]? FaceIds, Dictionary<FeFaceName, string>? Faces);
    private sealed record MaterialPropertyData(string Kind, double[][] Data, PlasticHardening? Hardening = null, double? ReferenceTemperature = null);
    private sealed record MaterialData(Entity State, double[][]? Elastic, double[][]? Density = null, bool DensityFirst = false,
        MaterialPropertyData[]? Properties = null);
    private sealed record SurfaceInteractionData(Entity State, Entity[] Properties);
    private sealed record StepData(Entity State, Entity[] Boundaries, Entity[] Loads, Entity[] Fields, Entity[] History);
    private sealed record UserKeywordData(int[] Path, string Data);
    private sealed record Document(int Version, string Name, UnitSystemType Units, Node[] Nodes, Element[] Elements,
        Entity? Part, Set[] NodeSets, Set[] ElementSets, Surface[] Surfaces, MaterialData[] Materials,
        Entity[] Sections, StepData[] Steps, Entity Properties, PartData[]? Parts = null,
        Entity[]? ReferencePoints = null, Entity[]? Amplitudes = null, Entity[]? Cylinders = null,
        SurfaceInteractionData[]? SurfaceInteractions = null, Entity[]? ContactPairs = null,
        Entity[]? Constraints = null, UserKeywordData[]? UserKeywords = null,
        Entity[]? CoordinateSystems = null, Entity[]? MeshSetupItems = null);
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    private static readonly Dictionary<Type, string[]> Fields = new()
    {
        [typeof(Material)] = Split("Name Active Description TemperatureDependent"),
        [typeof(SolidSection)] = Split("Name Active MaterialName RegionName RegionType Thickness"),
        [typeof(StaticStep)] = Split("Name Active RunAnalysis Perturbation Nlgeom MaxIncrements IncrementationType SolverType OutputFrequency TimePeriod InitialTimeIncrement MinTimeIncrement MaxTimeIncrement Direct"),
        [typeof(DisplacementRotation)] = Split("Name Active RegionName RegionType AmplitudeName CoordinateSystemName Complex PhaseDeg U1 U2 U3 UR1 UR2 UR3"),
        [typeof(STLoad)] = Split("Name Active SurfaceName RegionType AmplitudeName CoordinateSystemName Complex PhaseDeg F1 F2 F3 DistributionName"),
        [typeof(DLoad)] = Split("Name Active SurfaceName RegionType AmplitudeName CoordinateSystemName Complex PhaseDeg ElementId ElementFaceName Magnitude DistributionName"),
        [typeof(FixedBC)] = Split("Name Active RegionName RegionType AmplitudeName CoordinateSystemName Complex PhaseDeg"),
        [typeof(CLoad)] = Split("Name Active RegionName RegionType NodeId AmplitudeName CoordinateSystemName Complex PhaseDeg F1 F2 F3"),
        [typeof(GravityLoad)] = Split("Name Active RegionName RegionType AmplitudeName CoordinateSystemName Complex PhaseDeg F1 F2 F3"),
        [typeof(CentrifLoad)] = Split("Name Active RegionName RegionType AmplitudeName CoordinateSystemName Complex PhaseDeg Axisymmetric X Y Z N1 N2 N3 RotationalSpeed"),
        [typeof(NodalFieldOutput)] = Split("Name Active Variables LastIterations ContactElements Global"),
        [typeof(ElementFieldOutput)] = Split("Name Active Variables Output LastIterations ContactElements Global"),
        [typeof(ContactFieldOutput)] = Split("Name Active Variables LastIterations ContactElements Global"),
        [typeof(NodalHistoryOutput)] = Split("Name Active Variables RegionName RegionType TotalsType Global"),
        [typeof(ElementHistoryOutput)] = Split("Name Active Variables RegionName RegionType TotalsType Global"),
        [typeof(ContactHistoryOutput)] = Split("Name Active Variables RegionName RegionType TotalsType Global"),
        [typeof(SectionHistoryOutput)] = Split("Name Active Variables RegionName RegionType TotalsType Global"),
        [typeof(FeReferencePoint)] = Split("Name Active X Y Z"),
        [typeof(CoordinateSystem)] = Split("Name Active Type X1 Y1 Z1 X2 Y2 Z2 X3 Y3 Z3"),
        [typeof(AmplitudeTabular)] = Split("Name Active TimeSpan ShiftX ShiftY TimeAmplitude"),
        [typeof(FeCylinder)] = Split("Name Active Center Radius Direction Height ExtraRadius Chamfer TargetRadius NumberOfLayers Tolerance"),
        [typeof(SurfaceInteraction)] = Split("Name Active"),
        [typeof(SurfaceBehavior)] = Split("PressureOverclosureType C0 P0 K Sinf PressureOverclosure"),
        [typeof(Friction)] = Split("Coefficient StickSlope"),
        [typeof(GapConductance)] = Split("GapConductanceType ConductnancePressureTemp"),
        [typeof(ContactPair)] = Split("Name Active SurfaceInteractionName Method SmallSliding Adjust AdjustmentSize MasterRegionType MasterRegionName SlaveRegionType SlaveRegionName"),
        [typeof(Tie)] = Split("Name Active MasterRegionName MasterRegionType SlaveRegionName SlaveRegionType PositionTolerance Adjust"),
        [typeof(RigidBody)] = Split("Name Active ReferencePointName RegionName RegionType"),
        [typeof(SurfaceSpring)] = Split("Name Active RegionName RegionType StiffnessPerArea K1 K2 K3"),
        [typeof(MeshingParameters)] = Split("Name Active MaxH MinH SecondOrder RelativeSize"),
        [typeof(CadFaceMeshSize)] = Split("Name Active FaceTag MeshSize TransitionDistance SourceSha256 TopologySha256"),
        [typeof(BoxMeshSize)] = Split("Name Active MeshSize XMin YMin ZMin XMax YMax ZMax"),
        [typeof(TetrahedralGmsh)] = Split("Name Active AlgorithmMesh3D OptimizeFirstOrderSolid NumberOfThreads"),
        [typeof(MeshPart)] = Split("Name Active"),
        [typeof(ModelProperties)] = Split("ModelSpace ModelWorkflow ModelType AbsoluteZero StefanBoltzmann NewtonGravity")
    };
    private static string[] Split(string value) => value.Split(' ');
    private static double Number(EquationContainer value)
    {
        if (value.IsEquation()) throw new NotSupportedException("Model metadata requires numeric values, not equations.");
        return value.Value;
    }
    private static double MaterialNumber(EquationContainer value)
    {
        double number = Number(value);
        if (!double.IsFinite(number)) throw new NotSupportedException("Material metadata requires finite numeric values.");
        return number;
    }
    private static double[][] Values(EquationContainer[][] values) => values.Select(row =>
        row.Select(MaterialNumber).ToArray()).ToArray();
    private static bool FiniteTable(double[][]? table, int width) => table is { Length: > 0 } &&
        table.All(row => row != null && row.Length == width && row.All(double.IsFinite));
    private static Entity Capture(object value)
    {
        Type type = value.GetType();
        if (!Fields.TryGetValue(type, out var names)) throw new NotSupportedException("Unsupported model definition: " + type.Name);
        var fields = new Dictionary<string, JsonElement>();
        foreach (string name in names)
        {
            object? item = type.GetProperty(name)!.GetValue(value);
            fields.Add(name, JsonSerializer.SerializeToElement(item is EquationContainer equation ? Number(equation) : item, Options));
        }
        return new Entity(type.Name, fields);
    }
    private static T Apply<T>(Entity entity, T value) where T : notnull
    {
        Type type = value.GetType();
        if (entity == null || entity.Values == null) throw new InvalidDataException("Missing model entity.");
        if (entity.Kind != type.Name || entity.Values.Count != Fields[type].Length ||
            entity.Values.Keys.Except(Fields[type]).Any()) throw new InvalidDataException("Invalid model entity schema.");
        foreach (var field in entity.Values)
        {
            PropertyInfo property = type.GetProperty(field.Key)!;
            if (property.GetValue(value) is EquationContainer equation)
                equation.SetEquationFromValue(field.Value.Deserialize<double>(Options), enableEquationChanged: true);
            else
            {
                object? parsed = field.Value.Deserialize(property.PropertyType, Options);
                if (property.PropertyType.IsEnum && parsed != null)
                {
                    long bits = Convert.ToInt64(parsed);
                    bool valid = Enum.IsDefined(property.PropertyType, parsed);
                    if (Attribute.IsDefined(property.PropertyType, typeof(FlagsAttribute)))
                    {
                        long allowed = Enum.GetValues(property.PropertyType).Cast<object>().Aggregate(0L, (mask, item) => mask | Convert.ToInt64(item));
                        valid = (bits & ~allowed) == 0;
                    }
                    if (!valid) throw new InvalidDataException("Invalid enum value: " + field.Key);
                }
                property.SetValue(value, parsed);
            }
        }
        return value;
    }
    private static string Name(Entity value) => value.Values["Name"].GetString()!;
    internal static string CanonicalInput(string input) => Regex.Replace(input.Replace("\r\n", "\n"),
        @"(?m)^Hash: [^,\n]+, Date: \d{4}-\d{2}-\d{2},", "Hash: <id>, Date: <date>,");
    private static string Deck(FeModel model) => CanonicalInput(
        CalculixFileWriter.GetInputText(model, ConvertPyramidsToEnum.Wedges));

    private static bool SameValue(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is Array leftArray && right is Array rightArray)
            return leftArray.Cast<object?>().SequenceEqual(rightArray.Cast<object?>());
        return Equals(left, right);
    }

    private static bool DefaultExcept(object value, object defaults, params string[] allowed)
    {
        var exceptions = allowed.ToHashSet(StringComparer.Ordinal);
        return value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0 && property.CanRead &&
                !exceptions.Contains(property.Name))
            .All(property => SameValue(property.GetValue(value), property.GetValue(defaults)));
    }

    private static bool ValidMeshSetupItem(MeshSetupItem item) => item switch
    {
        MeshingParameters value when value.GetType() == typeof(MeshingParameters) =>
            value.Active && value.CreationIds == null && value.CreationData == null &&
            double.IsFinite(value.MaxH) && value.MaxH > 0 && double.IsFinite(value.MinH) &&
            value.MinH >= 0 && value.MinH <= value.MaxH && !value.RelativeSize &&
            DefaultExcept(value, new MeshingParameters(value.Name), "Name", "Active", "MaxH", "MinH",
                "SecondOrder", "RelativeSize", "CreationIds", "CreationData"),
        CadFaceMeshSize value => value.Active && value.CreationIds == null && value.CreationData == null &&
            value.IsProperlyDefined() && DefaultExcept(value, new CadFaceMeshSize(value.Name),
                "Name", "Active", "FaceTag", "MeshSize", "TransitionDistance", "SourceSha256", "TopologySha256"),
        BoxMeshSize value => value.Active && value.CreationIds == null && value.CreationData == null &&
            value.IsProperlyDefined() && DefaultExcept(value, new BoxMeshSize(value.Name),
                "Name", "Active", "MeshSize", "XMin", "YMin", "ZMin", "XMax", "YMax", "ZMax"),
        TetrahedralGmsh value when value.GetType() == typeof(TetrahedralGmsh) =>
            value.Active && value.CreationIds == null && value.CreationData == null && value.NumberOfThreads == 1 &&
            value.AlgorithmMesh3D is GmshAlgorithmMesh3DEnum.Delaunay or GmshAlgorithmMesh3DEnum.Frontal or
                GmshAlgorithmMesh3DEnum.HXT &&
            value.OptimizeFirstOrderSolid is GmshOptimizeFirstOrderSolidEnum.None or
                GmshOptimizeFirstOrderSolidEnum.Gmsh &&
            DefaultExcept(value, new TetrahedralGmsh(value.Name), "Name", "Active", "AlgorithmMesh3D",
                "OptimizeFirstOrderSolid", "NumberOfThreads", "CreationIds", "CreationData"),
        _ => false
    };

    private static Entity[] CaptureMeshSetup(FeMesh mesh)
    {
        if (mesh.MeshSetupItems.Count == 0) return [];
        if (mesh.MeshSetupItems.Count is < 2 or > 4 ||
            mesh.MeshSetupItems.Values.Count(item => item is BoxMeshSize) > 1 ||
            mesh.MeshSetupItems.Values.Count(item => item is CadFaceMeshSize) > 1 ||
            mesh.MeshSetupItems.Values.Count(item => item is MeshingParameters) != 1 ||
            mesh.MeshSetupItems.Values.Count(item => item is TetrahedralGmsh) != 1 ||
            mesh.MeshSetupItems.Values.Any(item => !ValidMeshSetupItem(item)) || !ValidBoxLimits(mesh))
            throw new NotSupportedException("Require one bounded Gmsh sizing and tetrahedral setup pair with at most one local box and one source-bound CAD face refinement.");
        return mesh.MeshSetupItems.Values.Select(Capture).ToArray();
    }

    private static void RestoreMeshSetup(Entity[] definitions, FeMesh mesh)
    {
        if (definitions.Length == 0) return;
        if (definitions.Length is < 2 or > 4 ||
            definitions.Count(item => item.Kind == nameof(BoxMeshSize)) > 1 ||
            definitions.Count(item => item.Kind == nameof(CadFaceMeshSize)) > 1 ||
            definitions.Count(item => item.Kind == nameof(MeshingParameters)) != 1 ||
            definitions.Count(item => item.Kind == nameof(TetrahedralGmsh)) != 1)
            throw new InvalidDataException("Invalid Gmsh mesh setup.");
        foreach (var definition in definitions)
        {
            MeshSetupItem item = definition.Kind switch
            {
                nameof(MeshingParameters) => Apply(definition, new MeshingParameters(Name(definition))),
                nameof(CadFaceMeshSize) => Apply(definition, new CadFaceMeshSize(Name(definition))),
                nameof(BoxMeshSize) => Apply(definition, new BoxMeshSize(Name(definition))),
                nameof(TetrahedralGmsh) => Apply(definition, new TetrahedralGmsh(Name(definition))),
                _ => throw new InvalidDataException("Unsupported mesh setup item.")
            };
            if (!ValidMeshSetupItem(item) || mesh.MeshSetupItems.ContainsKey(item.Name))
                throw new InvalidDataException("Invalid Gmsh mesh setup.");
            mesh.MeshSetupItems.Add(item.Name, item);
        }
        if (!ValidBoxLimits(mesh)) throw new InvalidDataException("Local mesh size exceeds global min/max limits.");
    }

    private static bool ValidBoxLimits(FeMesh mesh)
    {
        var box = mesh.MeshSetupItems.Values.OfType<BoxMeshSize>().SingleOrDefault();
        var sizing = mesh.MeshSetupItems.Values.OfType<MeshingParameters>().SingleOrDefault();
        var face = mesh.MeshSetupItems.Values.OfType<CadFaceMeshSize>().SingleOrDefault();
        return (box == null || sizing != null && box.MeshSize >= sizing.MinH && box.MeshSize <= sizing.MaxH) &&
            (face == null || sizing != null && face.MeshSize >= sizing.MinH && face.MeshSize <= sizing.MaxH);
    }

    private static int MeshSetupVersion(FeModel model)
    {
        if (model.Mesh.MeshSetupItems.Values.Any(item => item is CadFaceMeshSize)) return 33;
        if (model.Mesh.MeshSetupItems.Values.Any(item => item is BoxMeshSize)) return 31;
        return model.Mesh.MeshSetupItems.Values.OfType<MeshingParameters>().Any(p => p.SecondOrder) ? 29 : 27;
    }

    private static bool ValidElement(Element element, int version)
    {
        if (element.Id <= 0 || element.Nodes == null || element.Nodes.Distinct().Count() != element.Nodes.Length) return false;
        if (version < 5) return element.Kind == null && (element.Nodes.Length == 8 || version >= 4 && element.Nodes.Length == 4);
        return element.Kind switch
        {
            nameof(LinearTetraElement) => element.Nodes.Length == 4,
            nameof(LinearHexaElement) => element.Nodes.Length == 8,
            nameof(ParabolicTetraElement) => element.Nodes.Length == 10,
            nameof(ParabolicWedgeElement) => element.Nodes.Length == 15,
            nameof(ParabolicHexaElement) => element.Nodes.Length == 20,
            _ => false
        };
    }

    private static FeElement CreateElement(Element element) => element.Kind switch
    {
        nameof(LinearTetraElement) => new LinearTetraElement(element.Id, element.Nodes),
        nameof(LinearHexaElement) or null when element.Nodes.Length == 8 => new LinearHexaElement(element.Id, element.Nodes),
        null when element.Nodes.Length == 4 => new LinearTetraElement(element.Id, element.Nodes),
        nameof(ParabolicTetraElement) => new ParabolicTetraElement(element.Id, element.Nodes),
        nameof(ParabolicWedgeElement) => new ParabolicWedgeElement(element.Id, element.Nodes),
        nameof(ParabolicHexaElement) => new ParabolicHexaElement(element.Id, element.Nodes),
        _ => throw new InvalidDataException("Unsupported element topology.")
    };

    private static Document CaptureDocument(FeModel model, int version, bool externalMesh)
    {
        // V1 is an analysis definition, not UI/geometry/command-history persistence.
        if (model.Geometry.Parts.Count != 0 || model.Mesh.Parts.Count == 0 ||
            model.Parameters.Values.Count != 0 || model.UserViews.Count != 0 ||
            model.Distributions.Count != 0 || model.InitialConditions.Count != 0 ||
            model.Properties.ModelSpace != ModelSpaceEnum.ThreeD)
            throw new NotSupportedException("Model metadata supports 3D analysis parts without auxiliary definitions.");
        var mesh = model.Mesh;
        if (mesh.CoordinateSystems.Count > 1000 || mesh.CoordinateSystems.Values.Any(coordinateSystem =>
                coordinateSystem.GetType() != typeof(CoordinateSystem) || coordinateSystem.TwoD ||
                coordinateSystem.CenterCreatedFrom != CsPointCreatedFromEnum.Coordinates ||
                coordinateSystem.PointXCreatedFrom != CsPointCreatedFromEnum.Coordinates ||
                coordinateSystem.PointXYCreatedFrom != CsPointCreatedFromEnum.Coordinates ||
                coordinateSystem.CenterCreationIds != null || coordinateSystem.CenterCreationData != null ||
                coordinateSystem.PointXCreationIds != null || coordinateSystem.PointXCreationData != null ||
                coordinateSystem.PointXYCreationIds != null || coordinateSystem.PointXYCreationData != null ||
                !Enum.IsDefined(coordinateSystem.Type) || !coordinateSystem.IsProperlyDefined(out _) ||
                new[] { coordinateSystem.X1, coordinateSystem.Y1, coordinateSystem.Z1,
                    coordinateSystem.X2, coordinateSystem.Y2, coordinateSystem.Z2,
                    coordinateSystem.X3, coordinateSystem.Y3, coordinateSystem.Z3 }
                    .Any(value => !double.IsFinite(Number(value)))))
            throw new NotSupportedException("Only bounded, finite, coordinate-defined 3D coordinate systems are supported.");
        bool ValidCoordinateSystemReference(string name) => name == CoordinateSystem.DefaultCoordinateSystemName ||
            mesh.CoordinateSystems.ContainsKey(name);
        if (model.StepCollection.StepsList.Any(step =>
                step.BoundaryConditions.Values.Any(value => !ValidCoordinateSystemReference(value.CoordinateSystemName)) ||
                step.Loads.Values.Any(value => !ValidCoordinateSystemReference(value.CoordinateSystemName))))
            throw new NotSupportedException("A load or boundary condition references a missing coordinate system.");
        if (model.StepCollection.StepsList.SelectMany(step => step.Loads.Values).OfType<DLoad>().Any(pressure =>
                pressure.GetType() != typeof(DLoad) || pressure.RegionType != RegionTypeEnum.SurfaceName ||
                !mesh.Surfaces.TryGetValue(pressure.SurfaceName, out var surface) ||
                surface.Type != FeSurfaceType.Element || surface.ElementFaces is not { Count: > 0 } ||
                pressure.ElementId != -1 || pressure.ElementFaceName != FeFaceName.Empty ||
                pressure.DistributionName != Distribution.DefaultDistributionName ||
                pressure.Magnitude.IsEquation() || !double.IsFinite(pressure.Magnitude.Value)))
            throw new NotSupportedException("Only finite, constant pressures on named element surfaces are supported.");
        if (mesh.ReferencePoints.Values.Any(point => point.GetType() != typeof(FeReferencePoint) ||
                !double.IsFinite(MaterialNumber(point.X)) || !double.IsFinite(MaterialNumber(point.Y)) ||
                !double.IsFinite(MaterialNumber(point.Z))))
            throw new NotSupportedException("Only numeric reference-point coordinates are supported.");
        if (model.Amplitudes.Values.Any(amplitude => amplitude.GetType() != typeof(AmplitudeTabular) ||
                amplitude.TimeAmplitude == null || amplitude.TimeAmplitude.Length == 0 ||
                amplitude.TimeAmplitude.Any(row => row == null || row.Length != 2 || row.Any(value => !double.IsFinite(value))) ||
                !double.IsFinite(amplitude.ShiftX) || !double.IsFinite(amplitude.ShiftY)))
            throw new NotSupportedException("Only finite, nonempty tabular amplitudes are supported.");
        if (model.Cylinders.Values.Any(cylinder => cylinder.GetType() != typeof(FeCylinder) ||
                !cylinder.IsProperlyDefined(out _) || cylinder.Center.Any(value => !double.IsFinite(value)) ||
                cylinder.Direction.Any(value => !double.IsFinite(value)) || cylinder.Chamfer.Any(value => !double.IsFinite(value)) ||
                new[] { cylinder.Radius, cylinder.Height, cylinder.ExtraRadius, cylinder.TargetRadius, cylinder.Tolerance }
                    .Any(value => !double.IsFinite(value))))
            throw new NotSupportedException("Only finite, properly defined cylinders are supported.");
        var interactions = model.SurfaceInteractions.Values.Select(interaction =>
        {
            if (interaction.GetType() != typeof(SurfaceInteraction) ||
                interaction.Properties.GroupBy(property => property.GetType()).Any(group => group.Count() != 1))
                throw new NotSupportedException("Unsupported or duplicate surface-interaction property.");
            foreach (var property in interaction.Properties)
            {
                bool valid = property switch
                {
                    SurfaceBehavior value => Enum.IsDefined(value.PressureOverclosureType) &&
                        (double.IsFinite(value.C0) && value.C0 > 0 || double.IsNaN(value.C0)) &&
                        new[] { value.P0, value.K, value.Sinf }.All(number => double.IsFinite(number) && number > 0) &&
                        FiniteTable(value.PressureOverclosure, 2),
                    Friction value => double.IsFinite(value.Coefficient) && value.Coefficient > 0 &&
                        (double.IsFinite(value.StickSlope) && value.StickSlope > 0 || double.IsNaN(value.StickSlope)),
                    GapConductance value => Enum.IsDefined(value.GapConductanceType) &&
                        FiniteTable(value.ConductnancePressureTemp, 3),
                    _ => false
                };
                if (!valid) throw new NotSupportedException("Invalid surface-interaction property.");
            }
            return new SurfaceInteractionData(Capture(interaction), interaction.Properties.Select(Capture).ToArray());
        }).ToArray();
        if (model.ContactPairs.Values.Any(pair => pair.GetType() != typeof(ContactPair) || !Enum.IsDefined(pair.Method) ||
                pair.MasterRegionType != RegionTypeEnum.SurfaceName || pair.SlaveRegionType != RegionTypeEnum.SurfaceName ||
                !mesh.Surfaces.ContainsKey(pair.MasterRegionName) || !mesh.Surfaces.ContainsKey(pair.SlaveRegionName) ||
                !model.SurfaceInteractions.ContainsKey(pair.SurfaceInteractionName) ||
                !(double.IsNaN(pair.AdjustmentSize) || double.IsFinite(pair.AdjustmentSize) && pair.AdjustmentSize >= 0)))
            throw new NotSupportedException("Only named-surface contact pairs with valid interactions are supported.");
        foreach (var constraint in model.Constraints.Values)
        {
            bool valid = constraint switch
            {
                Tie tie when tie.GetType() == typeof(Tie) => tie.MasterRegionType == RegionTypeEnum.SurfaceName &&
                    tie.SlaveRegionType == RegionTypeEnum.SurfaceName && tie.MasterRegionName != tie.SlaveRegionName &&
                    mesh.Surfaces.ContainsKey(tie.MasterRegionName) && mesh.Surfaces.ContainsKey(tie.SlaveRegionName) &&
                    !tie.PositionTolerance.IsEquation() && (double.IsNaN(tie.PositionTolerance.Value) ||
                        double.IsFinite(tie.PositionTolerance.Value) && tie.PositionTolerance.Value >= 0),
                RigidBody body when body.GetType() == typeof(RigidBody) =>
                    mesh.ReferencePoints.ContainsKey(body.ReferencePointName) &&
                    (body.RegionType == RegionTypeEnum.NodeSetName && mesh.NodeSets.ContainsKey(body.RegionName) ||
                     body.RegionType == RegionTypeEnum.SurfaceName && mesh.Surfaces.ContainsKey(body.RegionName)),
                SurfaceSpring spring when spring.GetType() == typeof(SurfaceSpring) =>
                    spring.RegionType == RegionTypeEnum.SurfaceName && mesh.Surfaces.ContainsKey(spring.RegionName) &&
                    new[] { spring.K1, spring.K2, spring.K3 }.All(value => !value.IsEquation() && double.IsFinite(value.Value)),
                _ => false
            };
            if (!valid) throw new NotSupportedException("Unsupported or invalid constraint definition.");
        }
        Type[] supportedElements = [typeof(LinearHexaElement), typeof(LinearTetraElement), typeof(ParabolicTetraElement),
            typeof(ParabolicWedgeElement), typeof(ParabolicHexaElement)];
        if (mesh.Elements.Values.Any(e => !supportedElements.Contains(e.GetType())))
            throw new NotSupportedException("Model metadata supports linear tetrahedra/hexahedra and quadratic tetrahedra/wedges/hexahedra.");
        var partIds = mesh.Parts.Values.Select(part => part.PartId).ToHashSet();
        if (mesh.ElementSets.Values.Any(set => set.Labels.Length == 0 || set.Labels.Distinct().Count() != set.Labels.Length ||
                set.Labels.Any(id => set.CreatedFromParts ? !partIds.Contains(id) : !mesh.Elements.ContainsKey(id))))
            throw new NotSupportedException("Element sets must contain unique existing element or part IDs.");
        var materials = model.Materials.Values.Select(material =>
        {
            if (material.Properties.Count(p => p is Elastic) != 1 ||
                material.Properties.GroupBy(property => property.GetType()).Any(group => group.Count() != 1))
                throw new NotSupportedException("Exactly one property of each supported material type, including Elastic, is supported.");
            var elastic = material.Properties.OfType<Elastic>().SingleOrDefault();
            var density = material.Properties.OfType<Density>().SingleOrDefault();
            Type[] supported = [typeof(Elastic), typeof(Density), typeof(Plastic), typeof(ThermalExpansion),
                typeof(ThermalConductivity), typeof(SpecificHeat)];
            if (elastic == null || material.Properties.Any(property => !supported.Contains(property.GetType())))
                throw new NotSupportedException("Unsupported material property.");
            var properties = material.Properties.Select(property => property switch
            {
                Elastic value => new MaterialPropertyData(nameof(Elastic), Values(value.YoungsPoissonsTemp)),
                Density value => new MaterialPropertyData(nameof(Density), Values(value.DensityTemp)),
                Plastic value => new MaterialPropertyData(nameof(Plastic), Values(value.StressStrainTemp), value.Hardening),
                ThermalExpansion value => new MaterialPropertyData(nameof(ThermalExpansion), Values(value.ThermalExpansionTemp),
                    ReferenceTemperature: MaterialNumber(value.ZeroTemperature)),
                ThermalConductivity value => new MaterialPropertyData(nameof(ThermalConductivity), Values(value.ThermalConductivityTemp)),
                SpecificHeat value => new MaterialPropertyData(nameof(SpecificHeat), Values(value.SpecificHeatTemp)),
                _ => throw new NotSupportedException("Unsupported material property.")
            }).ToArray();
            return new MaterialData(Capture(material), null, Properties: properties);
        }).ToArray();
        var steps = model.StepCollection.StepsList.Select(CaptureStep).ToArray();
        var userKeywords = (model.CalculixUserKeywords ??
            new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("CalculiX user keywords")).Select(entry =>
        {
            var path = entry.Key;
            var keyword = entry.Value;
            if (path == null || path.Length is 0 or > 64 || path.Any(index => index < 0) ||
                keyword == null || keyword.GetType() != typeof(CalculixUserKeyword) ||
                !keyword.Active || keyword.IsSuppressed || keyword.Keywords.Count != 0 ||
                string.IsNullOrEmpty(keyword.Data) || keyword.Data.Contains('\0') ||
                Encoding.UTF8.GetByteCount(keyword.Data) > 1024 * 1024)
                throw new NotSupportedException("Only bounded active, unsuppressed leaf CalculiX user keywords are supported.");
            return new UserKeywordData(path.ToArray(), keyword.Data);
        }).ToArray();
        if (userKeywords.Length > 1000)
            throw new NotSupportedException("More than 1000 CalculiX user keywords are not supported.");
        var document = new Document(version, model.Name, model.UnitSystem.UnitSystemType,
            externalMesh ? [] : mesh.Nodes.Values.Select(n => new Node(n.Id, n.X, n.Y, n.Z)).ToArray(),
            externalMesh ? [] : mesh.Elements.Values.Select(e => new Element(e.Id, e.NodeIds, e switch {
                LinearTetraElement => nameof(LinearTetraElement), LinearHexaElement => nameof(LinearHexaElement),
                ParabolicTetraElement => nameof(ParabolicTetraElement), ParabolicWedgeElement => nameof(ParabolicWedgeElement),
                ParabolicHexaElement => nameof(ParabolicHexaElement), _ => throw new NotSupportedException("Unsupported element topology.") })).ToArray(), null,
            mesh.NodeSets.Values.Select(s => new Set(s.Name, externalMesh ? [] : s.Labels)).ToArray(),
            mesh.ElementSets.Values.Select(s => new Set(s.Name, externalMesh ? [] : s.Labels, s.CreatedFromParts)).ToArray(),
            mesh.Surfaces.Values.Select(s => new Surface(s.Name, s.NodeSetName, s.CreatedFromNodeSetName, s.Type, s.CreatedFrom, s.Area, externalMesh ? null : s.FaceIds, s.ElementFaces)).ToArray(),
            materials, model.Sections.Values.Select(Capture).ToArray(), steps, Capture(model.Properties), mesh.Parts.Values.Select(part =>
            {
                if (part is not MeshPart meshPart || part.Labels.Any(id => mesh.Elements[id].PartId != part.PartId) ||
                    !part.NodeLabels.ToHashSet().SetEquals(part.Labels.SelectMany(id => mesh.Elements[id].NodeIds)))
                    throw new NotSupportedException("Inconsistent analysis part membership.");
                return new PartData(Capture(part), part.PartId, externalMesh ? [] : part.Labels, meshPart.LinearHexaType, meshPart.LinearTetraType,
                    meshPart.ParabolicTetraType, meshPart.ParabolicWedgeType, meshPart.ParabolicHexaType);
            }).ToArray(), mesh.ReferencePoints.Values.Select(Capture).ToArray(),
            model.Amplitudes.Values.Select(Capture).ToArray(), model.Cylinders.Values.Select(Capture).ToArray(),
            interactions, model.ContactPairs.Values.Select(Capture).ToArray(),
            model.Constraints.Values.Select(Capture).ToArray(), userKeywords,
            mesh.CoordinateSystems.Values.Select(Capture).ToArray(), CaptureMeshSetup(mesh));
        return document;
    }

    private static StepData CaptureStep(Step step)
    {
        if (step.GetType() != typeof(StaticStep) || step.DefinedFields.Count != 0 || step.GetNumberOfStepControls() != 0)
            throw new NotSupportedException("Only static steps without defined fields or custom controls are supported.");
        return new StepData(Capture(step), step.BoundaryConditions.Values.Select(Capture).ToArray(),
            step.Loads.Values.Select(Capture).ToArray(), step.FieldOutputs.Values.Select(Capture).ToArray(),
            step.HistoryOutputs.Values.Select(Capture).ToArray());
    }

    /// <summary>Copies the supported static definitions through the same typed codec as project persistence.</summary>
    public static StaticStep CopyStaticStep(StaticStep step) => RestoreStep(CaptureStep(step), 34, null);

    private static StaticStep RestoreStep(StepData data, int version, FeMesh? mesh)
    {
        var step = Apply(data.State, new StaticStep(Name(data.State), false));
        foreach (var item in data.Boundaries)
        {
            BoundaryCondition boundary = item.Kind switch {
                nameof(DisplacementRotation) => new DisplacementRotation(Name(item), "region", RegionTypeEnum.NodeSetName, false, false, 0),
                nameof(FixedBC) => new FixedBC(Name(item), "region", RegionTypeEnum.NodeSetName, false),
                _ => throw new InvalidDataException("Unsupported boundary condition.") };
            step.AddBoundaryCondition(Apply(item, boundary));
        }
        foreach (var item in data.Loads)
        {
            Load load = item.Kind switch {
                nameof(STLoad) => new STLoad(Name(item), "region", RegionTypeEnum.SurfaceName, 0, 0, 0, false, false, 0),
                nameof(DLoad) when version >= 25 => new DLoad(Name(item), "region", RegionTypeEnum.SurfaceName, 0, false, false, 0),
                nameof(CLoad) => new CLoad(Name(item), 1, 0, 0, 0, false, false, 0),
                nameof(GravityLoad) => new GravityLoad(Name(item), "region", RegionTypeEnum.ElementSetName, false, false, 0),
                nameof(CentrifLoad) => new CentrifLoad(Name(item), "region", RegionTypeEnum.ElementSetName, false, false, false, 0),
                _ => throw new InvalidDataException("Unsupported load.") };
            step.AddLoad(Apply(item, load));
            if (mesh != null && load is DLoad pressure && (pressure.RegionType != RegionTypeEnum.SurfaceName ||
                !mesh.Surfaces.TryGetValue(pressure.SurfaceName, out var surface) ||
                surface.Type != FeSurfaceType.Element || surface.ElementFaces is not { Count: > 0 } ||
                pressure.ElementId != -1 || pressure.ElementFaceName != FeFaceName.Empty ||
                pressure.DistributionName != Distribution.DefaultDistributionName ||
                pressure.Magnitude.IsEquation() || !double.IsFinite(pressure.Magnitude.Value)))
                throw new InvalidDataException("Invalid pressure load.");
        }
        foreach (var item in data.Fields)
        {
            if (item.Kind == nameof(ContactFieldOutput) && version < 19)
                throw new InvalidDataException("Legacy metadata contains contact field output.");
            FieldOutput output = item.Kind switch {
                nameof(NodalFieldOutput) => new NodalFieldOutput(Name(item), NodalFieldVariable.U),
                nameof(ElementFieldOutput) => new ElementFieldOutput(Name(item), ElementFieldVariable.S),
                nameof(ContactFieldOutput) => new ContactFieldOutput(Name(item), ContactFieldVariable.CDIS),
                _ => throw new InvalidDataException("Unsupported field output.") };
            step.AddFieldOutput(Apply(item, output));
        }
        foreach (var item in data.History)
        {
            HistoryOutput output = item.Kind switch {
                nameof(NodalHistoryOutput) => new NodalHistoryOutput(Name(item), NodalHistoryVariable.U, "region", RegionTypeEnum.NodeSetName),
                nameof(ElementHistoryOutput) => new ElementHistoryOutput(Name(item), ElementHistoryVariable.S, "region", RegionTypeEnum.ElementSetName),
                nameof(ContactHistoryOutput) => new ContactHistoryOutput(Name(item), ContactHistoryVariable.CDIS, "region"),
                nameof(SectionHistoryOutput) => new SectionHistoryOutput(Name(item), SectionHistoryVariableEnum.SOF, "region", RegionTypeEnum.SurfaceName),
                _ => throw new InvalidDataException("Unsupported history output.") };
            step.AddHistoryOutput(Apply(item, output));
        }
        return step;
    }

    public static byte[] Serialize(FeModel model)
    {
        var document = CaptureDocument(model, MeshSetupVersion(model), false);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, Options);
        // A second guard against accidentally dropping an analysis-affecting setting.
        if (Deck(model) != Deck(Deserialize(bytes))) throw new NotSupportedException("Model settings exceed metadata; exported input would change.");
        return bytes;
    }

    internal static ArchiveData SerializeArchive(FeModel model)
    {
        var document = CaptureDocument(model, MeshSetupVersion(model) + 1, true);
        byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(document, Options);
        byte[] mesh = WriteExternalMesh(model.Mesh);
        if (metadata.Length > 32 * 1024 * 1024) throw new InvalidDataException("Model metadata exceeds 32 MiB.");
        if (Deck(model) != Deck(Deserialize(metadata, mesh)))
            throw new NotSupportedException("Model settings exceed metadata; exported input would change.");
        return new ArchiveData(metadata, mesh);
    }

    private static byte[] WriteExternalMesh(FeMesh mesh)
    {
        long values = 0;
        void Ensure(int count, int maximum = 10_000_000)
        {
            if (count < 0 || count > maximum || (values = checked(values + count)) > 100_000_000)
                throw new InvalidDataException("External mesh count exceeds limits.");
        }
        Ensure(mesh.Nodes.Count); Ensure(mesh.Elements.Count); Ensure(mesh.Parts.Count); Ensure(mesh.NodeSets.Count);
        Ensure(mesh.ElementSets.Count); Ensure(mesh.Surfaces.Count);
        foreach (var element in mesh.Elements.Values) Ensure(element.NodeIds.Length, 20);
        foreach (var part in mesh.Parts.Values) Ensure(part.Labels.Length);
        foreach (var set in mesh.NodeSets.Values) Ensure(set.Labels.Length);
        foreach (var set in mesh.ElementSets.Values) Ensure(set.Labels.Length);
        foreach (var surface in mesh.Surfaces.Values) if (surface.FaceIds != null) Ensure(surface.FaceIds.Length);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("PMAMESH1"));
        writer.Write(mesh.Nodes.Count);
        foreach (var node in mesh.Nodes.Values)
        {
            writer.Write(node.Id); writer.Write(node.X); writer.Write(node.Y); writer.Write(node.Z);
        }
        writer.Write(mesh.Elements.Count);
        foreach (var element in mesh.Elements.Values)
        {
            writer.Write(element.Id);
            writer.Write(element switch
            {
                LinearTetraElement => (byte)1, LinearHexaElement => (byte)2, ParabolicTetraElement => (byte)3,
                ParabolicWedgeElement => (byte)4, ParabolicHexaElement => (byte)5,
                _ => throw new NotSupportedException("Unsupported element topology.")
            });
            WriteIds(writer, element.NodeIds);
        }
        writer.Write(mesh.Parts.Count);
        foreach (var part in mesh.Parts.Values) { writer.Write(part.PartId); WriteIds(writer, part.Labels); }
        writer.Write(mesh.NodeSets.Count);
        foreach (var set in mesh.NodeSets.Values) { WriteName(writer, set.Name); WriteIds(writer, set.Labels); }
        writer.Write(mesh.ElementSets.Count);
        foreach (var set in mesh.ElementSets.Values) { WriteName(writer, set.Name); WriteIds(writer, set.Labels); }
        writer.Write(mesh.Surfaces.Count);
        foreach (var surface in mesh.Surfaces.Values) { WriteName(writer, surface.Name); WriteIds(writer, surface.FaceIds); }
        writer.Flush();
        if (stream.Length > 768L * 1024 * 1024) throw new InvalidDataException("External mesh payload exceeds 768 MiB.");
        return stream.ToArray();
    }

    private static Document ReadExternalMesh(Document document, byte[] payload)
    {
        if (payload.Length == 0 || payload.Length > 768 * 1024 * 1024) throw new InvalidDataException("Invalid external mesh payload size.");
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (!reader.ReadBytes(8).SequenceEqual(Encoding.ASCII.GetBytes("PMAMESH1")))
            throw new InvalidDataException("Invalid external mesh signature.");
        long values = 0;
        void Account(int count)
        {
            if ((values = checked(values + count)) > 100_000_000)
                throw new InvalidDataException("External mesh count exceeds limits.");
        }
        int Count(int maximum = 10_000_000)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum)
                throw new InvalidDataException("External mesh count exceeds limits.");
            Account(count);
            return count;
        }
        var nodes = new Node[Count()];
        for (int i = 0; i < nodes.Length; i++) nodes[i] = new Node(reader.ReadInt32(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
        var elements = new Element[Count()];
        for (int i = 0; i < elements.Length; i++)
        {
            int id = reader.ReadInt32();
            string kind = reader.ReadByte() switch
            {
                1 => nameof(LinearTetraElement), 2 => nameof(LinearHexaElement), 3 => nameof(ParabolicTetraElement),
                4 => nameof(ParabolicWedgeElement), 5 => nameof(ParabolicHexaElement),
                _ => throw new InvalidDataException("Unknown external element topology.")
            };
            elements[i] = new Element(id, ReadIds(reader, Account, 20)!, kind);
        }
        if (Count() != document.Parts!.Length) throw new InvalidDataException("External part count mismatch.");
        var parts = new PartData[document.Parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (reader.ReadInt32() != document.Parts[i].Id) throw new InvalidDataException("External part identity mismatch.");
            parts[i] = document.Parts[i] with { Elements = ReadIds(reader, Account)! };
        }
        Set[] Sets(Set[] definitions)
        {
            if (Count() != definitions.Length) throw new InvalidDataException("External set count mismatch.");
            var sets = new Set[definitions.Length];
            for (int i = 0; i < sets.Length; i++)
            {
                if (ReadName(reader, Account) != definitions[i].Name) throw new InvalidDataException("External set identity mismatch.");
                sets[i] = definitions[i] with { Labels = ReadIds(reader, Account)! };
            }
            return sets;
        }
        Set[] nodeSets = Sets(document.NodeSets), elementSets = Sets(document.ElementSets);
        if (Count() != document.Surfaces.Length) throw new InvalidDataException("External surface count mismatch.");
        var surfaces = new Surface[document.Surfaces.Length];
        for (int i = 0; i < surfaces.Length; i++)
        {
            if (ReadName(reader, Account) != document.Surfaces[i].Name) throw new InvalidDataException("External surface identity mismatch.");
            surfaces[i] = document.Surfaces[i] with { FaceIds = ReadIds(reader, Account, nullable: true) };
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("Trailing external mesh data.");
        return document with { Nodes = nodes, Elements = elements, Parts = parts, NodeSets = nodeSets, ElementSets = elementSets, Surfaces = surfaces };
    }

    private static void WriteIds(BinaryWriter writer, int[]? ids)
    {
        writer.Write(ids?.Length ?? -1);
        if (ids != null) foreach (int id in ids) writer.Write(id);
    }
    private static int[]? ReadIds(BinaryReader reader, Action<int> account, int maximum = 10_000_000, bool nullable = false)
    {
        int length = reader.ReadInt32();
        if (nullable && length == -1) return null;
        if (length < 0 || length > maximum) throw new InvalidDataException("Invalid external mesh array length.");
        account(length);
        var ids = new int[length];
        for (int i = 0; i < ids.Length; i++) ids[i] = reader.ReadInt32();
        return ids;
    }
    private static void WriteName(BinaryWriter writer, string name)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(name);
        if (bytes.Length is 0 or > 4096) throw new InvalidDataException("Invalid external mesh name.");
        writer.Write(bytes.Length); writer.Write(bytes);
    }
    private static string ReadName(BinaryReader reader, Action<int> account)
    {
        int length = reader.ReadInt32();
        if (length is <= 0 or > 4096) throw new InvalidDataException("Invalid external mesh name length.");
        account(length);
        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException();
        return new UTF8Encoding(false, true).GetString(bytes);
    }

    public static FeModel Deserialize(byte[] bytes, byte[]? externalMesh = null)
    {
        if (bytes.Length > 32 * 1024 * 1024) throw new InvalidDataException("Model metadata exceeds 32 MiB.");
        try
        {
            var d = JsonSerializer.Deserialize<Document>(bytes, Options) ?? throw new InvalidDataException("Missing model metadata.");
            if (d.Version is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27 or 28 or 29 or 30 or 31 or 32 or 33 or 34)) throw new InvalidDataException("Unsupported model metadata version.");
            if (d.Version is 6 or 8 or 10 or 12 or 14 or 16 or 18 or 20 or 22 or 24 or 26 or 28 or 30 or 32 or 34)
            {
                if (externalMesh == null || d.Nodes.Length != 0 || d.Elements.Length != 0 || d.Parts == null ||
                    d.Parts.Any(part => part.Elements.Length != 0) || d.NodeSets.Any(set => set.Labels.Length != 0) ||
                    d.ElementSets.Any(set => set.Labels.Length != 0) || d.Surfaces.Any(surface => surface.FaceIds != null))
                    throw new InvalidDataException("Invalid external mesh metadata.");
                d = ReadExternalMesh(d, externalMesh) with { Version = d.Version switch { 6 => 5, 8 => 7, 10 => 9, 12 => 11, 14 => 13, 16 => 15, 18 => 17, 20 => 19, 22 => 21, 24 => 23, 26 => 25, 30 => 29, 32 => 31, 34 => 33, _ => 27 } };
            }
            else if (externalMesh != null) throw new InvalidDataException("Unexpected external mesh payload.");
            if (d.Version >= 9)
            {
                if (d.ReferencePoints == null || d.Amplitudes == null)
                    throw new InvalidDataException("Missing reference-point or amplitude metadata.");
            }
            else if (d.ReferencePoints != null || d.Amplitudes != null)
                throw new InvalidDataException("Legacy metadata contains current definitions.");
            if (d.Version < 11 && d.ElementSets.Any(set => set.CreatedFromParts))
                throw new InvalidDataException("Legacy metadata contains part-derived element sets.");
            if (d.Version >= 13)
            {
                if (d.Cylinders == null) throw new InvalidDataException("Missing cylinder metadata.");
            }
            else if (d.Cylinders != null) throw new InvalidDataException("Legacy metadata contains cylinders.");
            if (d.Version >= 15)
            {
                if (d.SurfaceInteractions == null || d.ContactPairs == null)
                    throw new InvalidDataException("Missing contact metadata.");
            }
            else if (d.SurfaceInteractions != null || d.ContactPairs != null)
                throw new InvalidDataException("Legacy metadata contains contact definitions.");
            if (d.Version >= 17)
            {
                if (d.Constraints == null) throw new InvalidDataException("Missing constraint metadata.");
            }
            else if (d.Constraints != null) throw new InvalidDataException("Legacy metadata contains constraints.");
            if (d.Version >= 21)
            {
                if (d.UserKeywords == null) throw new InvalidDataException("Missing CalculiX user-keyword metadata.");
            }
            else if (d.UserKeywords != null) throw new InvalidDataException("Legacy metadata contains CalculiX user keywords.");
            if (d.Version >= 23)
            {
                if (d.CoordinateSystems == null) throw new InvalidDataException("Missing coordinate-system metadata.");
            }
            else if (d.CoordinateSystems != null) throw new InvalidDataException("Legacy metadata contains coordinate systems.");
            if (d.Version >= 27)
            {
                if (d.MeshSetupItems == null) throw new InvalidDataException("Missing mesh-setup metadata.");
            }
            else if (d.MeshSetupItems is { Length: > 0 })
                throw new InvalidDataException("Legacy metadata contains mesh setup items.");
            if (string.IsNullOrWhiteSpace(d.Name) || !Enum.IsDefined(d.Units)) throw new InvalidDataException("Invalid model identity/units.");
            if (d.Version == 1 && (d.Materials.Any(m => m.Density != null || m.DensityFirst) ||
                d.Steps.Any(step => step.Loads.Any(load => load.Kind != nameof(STLoad)) ||
                                   step.Boundaries.Any(bc => bc.Kind != nameof(DisplacementRotation)))))
                throw new InvalidDataException("Metadata version 1 contains version 2 definitions.");
            if (d.Version < 25 && d.Steps.Any(step => step.Loads.Any(load => load.Kind == nameof(DLoad))))
                throw new InvalidDataException("Legacy metadata contains pressure loads.");
            var model = new FeModel(d.Name, new UnitSystem(d.Units), false);
            var nodes = d.Nodes.ToDictionary(n => n.Id, n => new FeNode(n.Id, n.X, n.Y, n.Z));
            if (nodes.Values.Any(n => !double.IsFinite(n.X) || !double.IsFinite(n.Y) || !double.IsFinite(n.Z)) ||
                d.Elements.Any(e => !ValidElement(e, d.Version) || e.Nodes.Any(id => !nodes.ContainsKey(id))))
                throw new InvalidDataException("Invalid model mesh.");
            var elements = d.Elements.ToDictionary(e => e.Id, CreateElement);
            if (d.Version >= 3)
            {
                if (d.Part != null || d.Parts == null || d.Parts.Length == 0 ||
                    d.Parts.Select(p => p.Id).Distinct().Count() != d.Parts.Length)
                    throw new InvalidDataException("Invalid part definitions.");
                var assigned = new HashSet<int>();
                foreach (var definition in d.Parts)
                {
                    if (definition.Id <= 0 || definition.Elements.Length == 0 || !Enum.IsDefined(definition.HexaType) || !Enum.IsDefined(definition.TetraType) ||
                        !Enum.IsDefined(definition.ParabolicTetraType) || !Enum.IsDefined(definition.ParabolicWedgeType) || !Enum.IsDefined(definition.ParabolicHexaType))
                        throw new InvalidDataException("Invalid part identity or element type.");
                    foreach (int id in definition.Elements)
                    {
                        if (!elements.TryGetValue(id, out var element) || !assigned.Add(id))
                            throw new InvalidDataException("Invalid or overlapping part membership.");
                        element.PartId = definition.Id;
                    }
                    if (d.Version >= 5 && definition.Elements.Select(id => elements[id]).Any(element => element switch {
                        LinearTetraElement => definition.TetraType == FeElementTypeLinearTetra.None,
                        LinearHexaElement => definition.HexaType == FeElementTypeLinearHexa.None,
                        ParabolicTetraElement => definition.ParabolicTetraType == FeElementTypeParabolicTetra.None,
                        ParabolicWedgeElement => definition.ParabolicWedgeType == FeElementTypeParabolicWedge.None,
                        ParabolicHexaElement => definition.ParabolicHexaType == FeElementTypeParabolicHexa.None,
                        _ => true }))
                        throw new InvalidDataException("Part has no formulation for an assigned element topology.");
                }
                if (assigned.Count != elements.Count) throw new InvalidDataException("Elements missing part assignments.");
            }
            else if (d.Part == null || d.Parts != null) throw new InvalidDataException("Invalid legacy part schema.");
            var mesh = new FeMesh(nodes, elements, MeshRepresentation.Mesh);
            if (d.Version >= 3)
            {
                var parts = mesh.Parts.Values.ToDictionary(part => part.PartId);
                mesh.Parts.Clear();
                foreach (var definition in d.Parts!)
                {
                    var part = (MeshPart)parts[definition.Id];
                    Apply(definition.State, part);
                    part.Labels = definition.Elements.ToArray();
                    part.LinearHexaType = definition.HexaType;
                    if (d.Version >= 4) part.LinearTetraType = definition.TetraType;
                    if (d.Version >= 5)
                    {
                        part.ParabolicTetraType = definition.ParabolicTetraType;
                        part.ParabolicWedgeType = definition.ParabolicWedgeType;
                        part.ParabolicHexaType = definition.ParabolicHexaType;
                    }
                    mesh.Parts.Add(part.Name, part);
                }
            }
            else
            {
                if (mesh.Parts.Count != 1) throw new InvalidDataException("Legacy metadata requires one part.");
                var part = mesh.Parts.Values.Single();
                mesh.Parts.Clear(); Apply(d.Part!, part); mesh.Parts.Add(part.Name, part);
            }
            if (d.Version >= 23)
            {
                if (d.CoordinateSystems!.Length > 1000)
                    throw new InvalidDataException("Too many coordinate systems.");
                foreach (var definition in d.CoordinateSystems)
                {
                    var coordinateSystem = Apply(definition, new CoordinateSystem(Name(definition), false));
                    if (coordinateSystem.TwoD || !Enum.IsDefined(coordinateSystem.Type) ||
                        new[] { coordinateSystem.X1.Value, coordinateSystem.Y1.Value, coordinateSystem.Z1.Value,
                            coordinateSystem.X2.Value, coordinateSystem.Y2.Value, coordinateSystem.Z2.Value,
                            coordinateSystem.X3.Value, coordinateSystem.Y3.Value, coordinateSystem.Z3.Value }
                            .Any(value => !double.IsFinite(value)) ||
                        !coordinateSystem.IsProperlyDefined(out _))
                        throw new InvalidDataException("Invalid coordinate-system definition.");
                    mesh.CoordinateSystems.Add(coordinateSystem.Name, coordinateSystem);
                }
            }
            if (d.Version >= 9)
                foreach (var definition in d.ReferencePoints!)
                {
                    var point = Apply(definition, new FeReferencePoint(Name(definition), 0, 0, 0));
                    if (!double.IsFinite(point.X.Value) || !double.IsFinite(point.Y.Value) || !double.IsFinite(point.Z.Value))
                        throw new InvalidDataException("Invalid reference-point coordinates.");
                    mesh.ReferencePoints.Add(point.Name, point);
                }
            foreach (var set in d.NodeSets) mesh.NodeSets.Add(set.Name, new FeNodeSet(set.Name, set.Labels));
            foreach (var set in d.ElementSets) mesh.ElementSets.Add(set.Name,
                new FeElementSet(set.Name, set.Labels, set.CreatedFromParts));
            foreach (var surface in d.Surfaces)
            {
                var value = new FeSurface(surface.Name) { CreatedFrom = surface.CreatedFrom, Type = surface.Type,
                    NodeSetName = surface.NodeSet, CreatedFromNodeSetName = surface.SourceNodeSet,
                    Area = surface.Area, FaceIds = surface.FaceIds };
                if (surface.Faces != null) foreach (var face in surface.Faces) value.AddElementFace(face.Key, face.Value);
                mesh.Surfaces.Add(value.Name, value);
            }
            if (d.NodeSets.Any(set => set.Labels.Any(id => !nodes.ContainsKey(id))) ||
                d.ElementSets.Any(set => set.Labels.Length == 0 || set.Labels.Distinct().Count() != set.Labels.Length ||
                    set.Labels.Any(id => set.CreatedFromParts ? !mesh.Parts.Values.Any(part => part.PartId == id) : !mesh.Elements.ContainsKey(id))) ||
                d.Surfaces.Any(surface => (surface.NodeSet != null && !mesh.NodeSets.ContainsKey(surface.NodeSet)) ||
                    (surface.SourceNodeSet != null && !mesh.NodeSets.ContainsKey(surface.SourceNodeSet)) ||
                    !Enum.IsDefined(surface.Type) || !Enum.IsDefined(surface.CreatedFrom) ||
                    (surface.Faces?.Any(face => !Enum.IsDefined(face.Key) || !mesh.ElementSets.ContainsKey(face.Value)) ?? false)))
                throw new InvalidDataException("Dangling mesh region reference.");
            if (d.Version >= 27) RestoreMeshSetup(d.MeshSetupItems!, mesh);
            if (d.Version < 29 && mesh.MeshSetupItems.Values.OfType<MeshingParameters>().Any(p => p.SecondOrder))
                throw new InvalidDataException("Quadratic Gmsh setup requires metadata v29 or later.");
            if (d.Version < 31 && mesh.MeshSetupItems.Values.Any(item => item is BoxMeshSize))
                throw new InvalidDataException("Local Gmsh sizing requires metadata v31 or later.");
            if (d.Version < 33 && mesh.MeshSetupItems.Values.Any(item => item is CadFaceMeshSize))
                throw new InvalidDataException("CAD face sizing requires metadata v33 or later.");
            model.SetMesh(mesh); Apply(d.Properties, model.Properties);
            if (model.Properties.ModelSpace != ModelSpaceEnum.ThreeD) throw new InvalidDataException("Metadata requires a 3D model.");
            if (d.Version >= 13)
                foreach (var definition in d.Cylinders!)
                {
                    if (definition == null || definition.Values == null ||
                        definition.Values["Center"].Deserialize<double[]>(Options) is not { Length: 3 } center ||
                        definition.Values["Direction"].Deserialize<double[]>(Options) is not { Length: 3 } direction ||
                        definition.Values["Chamfer"].Deserialize<double[]>(Options) is not { Length: 2 } chamfer ||
                        center.Concat(direction).Concat(chamfer).Any(value => !double.IsFinite(value)))
                        throw new InvalidDataException("Invalid cylinder vectors.");
                    var cylinder = Apply(definition, new FeCylinder(Name(definition)));
                    if (!cylinder.IsProperlyDefined(out _) ||
                        new[] { cylinder.Radius, cylinder.Height, cylinder.ExtraRadius, cylinder.TargetRadius, cylinder.Tolerance }
                            .Any(value => !double.IsFinite(value)))
                        throw new InvalidDataException("Invalid cylinder definition.");
                    model.Cylinders.Add(cylinder.Name, cylinder);
                }
            if (d.Version >= 15)
            {
                foreach (var definition in d.SurfaceInteractions!)
                {
                    var interaction = Apply(definition.State, new SurfaceInteraction(Name(definition.State)));
                    if (definition.Properties == null ||
                        definition.Properties.GroupBy(property => property.Kind).Any(group => group.Count() != 1))
                        throw new InvalidDataException("Invalid surface-interaction property schema.");
                    foreach (var property in definition.Properties)
                    {
                        SurfaceInteractionProperty restored = property.Kind switch
                        {
                            nameof(SurfaceBehavior) => Apply(property, new SurfaceBehavior()),
                            nameof(Friction) => Apply(property, new Friction()),
                            nameof(GapConductance) => Apply(property, new GapConductance()),
                            _ => throw new InvalidDataException("Unsupported surface-interaction property.")
                        };
                        bool valid = restored switch
                        {
                            SurfaceBehavior value => Enum.IsDefined(value.PressureOverclosureType) &&
                                (double.IsFinite(value.C0) && value.C0 > 0 || double.IsNaN(value.C0)) &&
                                new[] { value.P0, value.K, value.Sinf }.All(number => double.IsFinite(number) && number > 0) &&
                                FiniteTable(value.PressureOverclosure, 2),
                            Friction value => double.IsFinite(value.Coefficient) && value.Coefficient > 0 &&
                                (double.IsFinite(value.StickSlope) && value.StickSlope > 0 || double.IsNaN(value.StickSlope)),
                            GapConductance value => Enum.IsDefined(value.GapConductanceType) &&
                                FiniteTable(value.ConductnancePressureTemp, 3),
                            _ => false
                        };
                        if (!valid) throw new InvalidDataException("Invalid surface-interaction property data.");
                        interaction.AddProperty(restored);
                    }
                    model.SurfaceInteractions.Add(interaction.Name, interaction);
                }
                foreach (var definition in d.ContactPairs!)
                {
                    var pair = Apply(definition, new ContactPair(Name(definition), "interaction", "master",
                        RegionTypeEnum.SurfaceName, "slave", RegionTypeEnum.SurfaceName));
                    if (pair.MasterRegionType != RegionTypeEnum.SurfaceName || pair.SlaveRegionType != RegionTypeEnum.SurfaceName ||
                        !mesh.Surfaces.ContainsKey(pair.MasterRegionName) || !mesh.Surfaces.ContainsKey(pair.SlaveRegionName) ||
                        !model.SurfaceInteractions.ContainsKey(pair.SurfaceInteractionName) ||
                        !(double.IsNaN(pair.AdjustmentSize) || double.IsFinite(pair.AdjustmentSize) && pair.AdjustmentSize >= 0))
                        throw new InvalidDataException("Invalid contact-pair definition.");
                    model.ContactPairs.Add(pair.Name, pair);
                }
            }
            if (d.Version >= 17)
                foreach (var definition in d.Constraints!)
                {
                    Constraint constraint = definition.Kind switch
                    {
                        nameof(Tie) => Apply(definition, new Tie(Name(definition), "master",
                            RegionTypeEnum.SurfaceName, "slave", RegionTypeEnum.SurfaceName, false)),
                        nameof(RigidBody) => Apply(definition, new RigidBody(Name(definition), "reference",
                            "region", RegionTypeEnum.NodeSetName, false)),
                        nameof(SurfaceSpring) => Apply(definition, new SurfaceSpring(Name(definition), "surface",
                            RegionTypeEnum.SurfaceName, false, false)),
                        _ => throw new InvalidDataException("Unsupported constraint definition.")
                    };
                    bool valid = constraint switch
                    {
                        Tie tie => tie.MasterRegionType == RegionTypeEnum.SurfaceName &&
                            tie.SlaveRegionType == RegionTypeEnum.SurfaceName && tie.MasterRegionName != tie.SlaveRegionName &&
                            mesh.Surfaces.ContainsKey(tie.MasterRegionName) && mesh.Surfaces.ContainsKey(tie.SlaveRegionName) &&
                            (double.IsNaN(tie.PositionTolerance.Value) ||
                                double.IsFinite(tie.PositionTolerance.Value) && tie.PositionTolerance.Value >= 0),
                        RigidBody body => mesh.ReferencePoints.ContainsKey(body.ReferencePointName) &&
                            (body.RegionType == RegionTypeEnum.NodeSetName && mesh.NodeSets.ContainsKey(body.RegionName) ||
                             body.RegionType == RegionTypeEnum.SurfaceName && mesh.Surfaces.ContainsKey(body.RegionName)),
                        SurfaceSpring spring => spring.RegionType == RegionTypeEnum.SurfaceName &&
                            mesh.Surfaces.ContainsKey(spring.RegionName) &&
                            new[] { spring.K1.Value, spring.K2.Value, spring.K3.Value }.All(double.IsFinite),
                        _ => false
                    };
                    if (!valid) throw new InvalidDataException("Invalid constraint definition.");
                    model.Constraints.Add(constraint.Name, constraint);
                }
            if (d.Version >= 9)
                foreach (var definition in d.Amplitudes!)
                {
                    var amplitude = Apply(definition, new AmplitudeTabular(Name(definition), []));
                    if (!double.IsFinite(amplitude.ShiftX) || !double.IsFinite(amplitude.ShiftY) ||
                        amplitude.TimeAmplitude == null || amplitude.TimeAmplitude.Length == 0 ||
                        amplitude.TimeAmplitude.Any(row => row == null || row.Length != 2 || row.Any(value => !double.IsFinite(value))))
                        throw new InvalidDataException("Invalid tabular amplitude.");
                    model.Amplitudes.Add(amplitude.Name, amplitude);
                }
            foreach (var material in d.Materials)
            {
                var value = Apply(material.State, new Material(Name(material.State)));
                if (d.Version >= 7)
                {
                    if (material.Properties == null || material.Elastic != null || material.Density != null || material.DensityFirst)
                        throw new InvalidDataException("Invalid material property schema.");
                    if (material.Properties.Length == 0 ||
                        material.Properties.GroupBy(property => property.Kind).Any(group => group.Count() != 1) ||
                        material.Properties.Any(property => property.Data == null || property.Data.Length == 0 ||
                            property.Data.Any(row => row == null || row.Any(number => !double.IsFinite(number)))))
                        throw new InvalidDataException("Invalid material property data.");
                    foreach (var property in material.Properties)
                    {
                        int width = property.Kind switch
                        {
                            nameof(Elastic) or nameof(Plastic) => 3,
                            nameof(Density) or nameof(ThermalExpansion) or nameof(ThermalConductivity) or nameof(SpecificHeat) => 2,
                            _ => 0
                        };
                        if (width == 0 || property.Data.Any(row => row.Length != width))
                            throw new InvalidDataException("Invalid material property table width.");
                        MaterialProperty restored = property.Kind switch
                        {
                            nameof(Elastic) when property.Hardening == null && property.ReferenceTemperature == null => new Elastic(property.Data),
                            nameof(Density) when property.Hardening == null && property.ReferenceTemperature == null => new Density(property.Data),
                            nameof(Plastic) when property.Hardening is { } hardening && Enum.IsDefined(hardening) && property.ReferenceTemperature == null =>
                                new Plastic(property.Data) { Hardening = hardening },
                            nameof(ThermalExpansion) when property.Hardening == null && property.ReferenceTemperature is { } reference && double.IsFinite(reference) =>
                                new ThermalExpansion(property.Data) { ZeroTemperature = new EquationContainer(typeof(StringTemperatureConverter), reference) },
                            nameof(ThermalConductivity) when property.Hardening == null && property.ReferenceTemperature == null => new ThermalConductivity(property.Data),
                            nameof(SpecificHeat) when property.Hardening == null && property.ReferenceTemperature == null => new SpecificHeat(property.Data),
                            _ => throw new InvalidDataException("Unsupported material property metadata.")
                        };
                        value.AddProperty(restored);
                    }
                    if (value.Properties.Count(property => property is Elastic) != 1 || value.Properties.Count(property => property is Density) > 1)
                        throw new InvalidDataException("Invalid material property combination.");
                }
                else
                {
                    if (material.Properties != null || material.Elastic == null || material.DensityFirst && material.Density == null)
                        throw new InvalidDataException("Invalid legacy material definition.");
                    if (material.DensityFirst) value.AddProperty(new Density(material.Density));
                    value.AddProperty(new Elastic(material.Elastic));
                    if (material.Density != null && !material.DensityFirst) value.AddProperty(new Density(material.Density));
                }
                model.Materials.Add(value.Name, value);
            }
            foreach (var section in d.Sections)
            {
                var value = Apply(section, new SolidSection(Name(section), "material", "region", RegionTypeEnum.ElementSetName, 1, false));
                model.Sections.Add(value.Name, value);
            }
            foreach (var data in d.Steps)
                model.StepCollection.AddStep(RestoreStep(data, d.Version, mesh));
            if (model.StepCollection.StepsList.Any(step =>
                    step.BoundaryConditions.Values.Any(value => value.CoordinateSystemName != CoordinateSystem.DefaultCoordinateSystemName &&
                        !mesh.CoordinateSystems.ContainsKey(value.CoordinateSystemName)) ||
                    step.Loads.Values.Any(value => value.CoordinateSystemName != CoordinateSystem.DefaultCoordinateSystemName &&
                        !mesh.CoordinateSystems.ContainsKey(value.CoordinateSystemName))))
                throw new InvalidDataException("A load or boundary condition references a missing coordinate system.");
            if (d.Version >= 21)
            {
                if (d.UserKeywords!.Length > 1000)
                    throw new InvalidDataException("Too many CalculiX user keywords.");
                var insertionTree = CalculixFileWriter.GetModelKeywords(model, ConvertPyramidsToEnum.Wedges);
                var restored = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("CalculiX user keywords");
                foreach (var definition in d.UserKeywords)
                {
                    if (definition == null || definition.Path == null || definition.Path.Length is 0 or > 64 ||
                        definition.Path.Any(index => index < 0) || string.IsNullOrEmpty(definition.Data) ||
                        definition.Data.Contains('\0') || Encoding.UTF8.GetByteCount(definition.Data) > 1024 * 1024)
                        throw new InvalidDataException("Invalid CalculiX user keyword.");
                    var keyword = new CalculixUserKeyword(definition.Data);
                    if (!CalculixFileWriter.AddUserKeywordByIndices(insertionTree, definition.Path, keyword.CopyForExport()))
                        throw new InvalidDataException("Invalid CalculiX user-keyword insertion path.");
                    restored.Add(definition.Path.ToArray(), keyword);
                }
                model.CalculixUserKeywords = restored;
            }
            return model;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or CaeException or NullReferenceException or KeyNotFoundException or TargetInvocationException or EndOfStreamException or DecoderFallbackException or OverflowException)
        { throw new InvalidDataException("Invalid static model metadata.", ex); }
    }

    /// <summary>Reads the bounded model identity needed to interpret an independent result payload.</summary>
    public static UnitSystem ReadUnitSystem(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length is 0 or > 32 * 1024 * 1024)
            throw new InvalidDataException("Model metadata exceeds 32 MiB.");
        try
        {
            var d = JsonSerializer.Deserialize<Document>(bytes, Options) ?? throw new InvalidDataException("Missing model metadata.");
            if (d.Version is < 1 or > 34 || string.IsNullOrWhiteSpace(d.Name) || !Enum.IsDefined(d.Units) ||
                d.Nodes == null || d.Elements == null || d.NodeSets == null || d.ElementSets == null ||
                d.Surfaces == null || d.Materials == null || d.Sections == null || d.Steps == null || d.Properties == null)
                throw new InvalidDataException("Invalid model identity or units.");
            return new UnitSystem(d.Units);
        }
        catch (JsonException ex) { throw new InvalidDataException("Invalid model metadata.", ex); }
    }
}
