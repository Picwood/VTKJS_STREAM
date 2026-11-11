using CaeGlobals;
using CaeMesh;
using CaeModel;

namespace PrePoMax.Application;

public sealed record SurfaceSettings(string Name, ProjectElementFace[]? Faces = null);
public sealed record SurfaceSummary(string Name, string Type, int Faces, int Nodes, double Area, string[] Dependents);
public sealed record SurfaceDetails(string Name, string Type, ProjectElementFace[]? Faces, string? NodeSetName);
public sealed record SurfaceEditorData(SurfaceSummary[] Surfaces, SurfaceDetails? Selected, ProjectSurfaceInventory Inventory, string? EditingError);

public static partial class ProjectSurfaces
{
    public static ModelTreeSelection Preview(string path, SurfaceSettings settings, string? previousName = null)
    {
        ProjectRegionReferences.ValidateName(settings.Name);
        var mesh = PrePoMax.Persistence.StaticProjectArchive.Open(path).Model.Mesh;
        if (settings.Faces != null)
        {
            var faces = Normalize(mesh, settings.Faces).Select(face => new ProjectElementFace(face.ElementId, face.Face.ToString())).ToArray();
            return new(faces.SelectMany(face => mesh.Elements[face.ElementId].GetNodeIdsFromFaceName(Enum.Parse<FeFaceName>(face.Face))).Distinct().Order().ToArray(), faces);
        }
        if (previousName == null) throw new ArgumentException("Select element faces.");
        var previous = mesh.Surfaces.Values.Single(surface => surface.Name.Equals(previousName, StringComparison.OrdinalIgnoreCase));
        if (previous.Type == FeSurfaceType.Node)
        {
            var nodes = mesh.NodeSets[previous.NodeSetName].Labels;
            if (nodes.Length > 10000) throw new NotSupportedException("Preview supports at most 10000 nodes.");
            return new(nodes.ToArray(), []);
        }
        var saved = Faces(mesh, previous).Take(MaximumFaces + 1).ToArray();
        if (saved.Length > MaximumFaces) throw new NotSupportedException("Preview supports at most 100 faces; the complete surface can still be retained or duplicated.");
        return new(saved.SelectMany(face => mesh.Elements[face.ElementId].GetNodeIdsFromFaceName(Enum.Parse<FeFaceName>(face.Face))).Distinct().Order().ToArray(), saved);
    }

    public static SurfaceEditorData Read(string path, string? name = null)
    {
        var model = PrePoMax.Persistence.StaticProjectArchive.Open(path).Model;
        var mesh = model.Mesh;
        var references = ProjectRegionReferences.Collect(model, RegionTypeEnum.SurfaceName);
        var selected = name == null ? null : Find(model, name);
        ProjectElementFace[]? selectedFaces = selected == null || selected.Type != FeSurfaceType.Element ? null : Faces(mesh, selected).Take(MaximumFaces + 1).ToArray();
        if (selectedFaces?.Length > MaximumFaces) selectedFaces = null;
        var elements = mesh.Elements.Values.Where(element => element.Dimension == 3).OrderBy(element => element.Id).ToArray();
        return new(mesh.Surfaces.Values.Where(surface => !surface.Internal).Select(surface =>
        {
            int count = 0;
            int nodes;
            if (surface.Type == FeSurfaceType.Element)
            {
                var membership = Faces(mesh, surface).ToArray();
                count = membership.Length;
                nodes = membership.SelectMany(face => mesh.Elements[face.ElementId].GetNodeIdsFromFaceName(Enum.Parse<FeFaceName>(face.Face))).Distinct().Count();
            }
            else nodes = mesh.NodeSets[surface.NodeSetName].Labels.Length;
            return new SurfaceSummary(surface.Name, surface.Type.ToString(), count, nodes, surface.Area,
                (references.GetValueOrDefault(surface.Name) ?? []).Select(use => use.Description).Distinct().ToArray());
        }).ToArray(), selected == null ? null : new(selected.Name, selected.Type.ToString(), selectedFaces, selected.NodeSetName),
            new(elements.Length, elements.Take(1000).Select(element => new ProjectSurfaceElement(element.Id,
                ValidFaces(element).Select(face => face.ToString()).ToArray())).ToArray()), ProjectRegionReferences.EditingError(model));
    }

    public static void SaveCopy(string source, string destination, string name, SurfaceSettings settings)
    {
        var project = ProjectRegionReferences.Editable(source);
        var model = project.Model;
        var previous = Find(model, name);
        ProjectRegionReferences.ValidateName(settings.Name);
        Unique(model, settings.Name, previous);
        FeSurface replacement;
        if (settings.Faces == null) replacement = new FeSurface(previous) { Name = settings.Name };
        else
        {
            if (previous.Type != FeSurfaceType.Element) throw new NotSupportedException("Edit this node surface through its node set; its name can still be changed here.");
            replacement = Build(model.Mesh, settings.Name, Normalize(model.Mesh, settings.Faces)
                .Select(face => new ProjectElementFace(face.ElementId, face.Face.ToString())));
            replacement.Active = previous.Active;
        }
        var uses = ProjectRegionReferences.Collect(model, RegionTypeEnum.SurfaceName).GetValueOrDefault(previous.Name) ?? [];
        model.Mesh.Surfaces.Replace(previous.Name, replacement.Name, replacement);
        foreach (var use in uses) use.Rename(replacement.Name);
        // Backing regions may be shared or user-owned. Retain them until ownership is persisted.
        ProjectEdits.SaveNew(destination, project);
    }

    public static void DuplicateCopy(string source, string destination, string name, string newName)
    {
        var project = ProjectRegionReferences.Editable(source);
        var model = project.Model;
        var previous = Find(model, name);
        ProjectRegionReferences.ValidateName(newName);
        Unique(model, newName);
        FeSurface duplicate;
        if (previous.Type == FeSurfaceType.Element) duplicate = Build(model.Mesh, newName, Faces(model.Mesh, previous));
        else
        {
            string nodes = AddNodes(model.Mesh, model.Mesh.NodeSets[previous.NodeSetName].Labels);
            duplicate = new FeSurface(newName, nodes) { Type = FeSurfaceType.Node, NodeSetName = nodes };
        }
        duplicate.Active = previous.Active;
        model.Mesh.Surfaces.Add(duplicate.Name, duplicate);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemoveCopy(string source, string destination, string name)
    {
        var project = ProjectRegionReferences.Editable(source);
        var surface = Find(project.Model, name);
        ProjectRegionReferences.RequireUnused(ProjectRegionReferences.Collect(project.Model, RegionTypeEnum.SurfaceName).GetValueOrDefault(surface.Name) ?? []);
        project.Model.Mesh.Surfaces.Remove(surface.Name);
        ProjectEdits.SaveNew(destination, project);
    }

    private static FeSurface Build(FeMesh mesh, string name, IEnumerable<ProjectElementFace> membership)
    {
        var faces = membership.Distinct().ToArray();
        if (faces.Length == 0) throw new ArgumentException("Select at least one solid element face.");
        var surface = new FeSurface(name) { CreatedFrom = FeSurfaceCreatedFrom.Faces, SurfaceFaceTypes = FeSurfaceFaceTypes.SolidFaces };
        var nodes = new HashSet<int>();
        double area = 0;
        foreach (var face in faces)
        {
            var element = mesh.Elements[face.ElementId];
            var label = Enum.Parse<FeFaceName>(face.Face);
            nodes.UnionWith(element.GetNodeIdsFromFaceName(label));
            double faceArea = element.GetArea(label, mesh.Nodes);
            if (!double.IsFinite(faceArea) || faceArea <= 0) throw new ArgumentException("A selected face has invalid area.");
            area += faceArea;
        }
        if (!double.IsFinite(area)) throw new ArgumentException("The selected surface area is not finite.");
        foreach (var group in faces.GroupBy(face => face.Face))
        {
            int suffix = 1;
            string setName;
            do setName = $"__surface_{suffix++}_{group.Key}";
            while (mesh.ElementSets.Keys.Any(existing => existing.Equals(setName, StringComparison.OrdinalIgnoreCase)) ||
                   mesh.Parts.Keys.Any(existing => existing.Equals(setName, StringComparison.OrdinalIgnoreCase)));
            mesh.ElementSets.Add(setName, new FeElementSet(setName, group.Select(face => face.ElementId).Distinct().Order().ToArray()));
            surface.AddElementFace(Enum.Parse<FeFaceName>(group.Key), setName);
        }
        surface.NodeSetName = AddNodes(mesh, nodes);
        surface.Area = area;
        return surface;
    }

    private static string AddNodes(FeMesh mesh, IEnumerable<int> nodes)
    {
        int suffix = 1;
        string name;
        do name = "__surface_nodes_" + suffix++;
        while (mesh.NodeSets.Keys.Any(existing => existing.Equals(name, StringComparison.OrdinalIgnoreCase)));
        var set = new FeNodeSet(name, nodes.Distinct().Order().ToArray());
        mesh.UpdateNodeSetCenterOfGravity(set);
        mesh.NodeSets.Add(name, set);
        return name;
    }

    private static IEnumerable<ProjectElementFace> Faces(FeMesh mesh, FeSurface surface)
    {
        if (surface.ElementFaces == null) yield break;
        foreach (var entry in surface.ElementFaces)
            foreach (int id in ProjectElementSets.Resolve(mesh, mesh.ElementSets[entry.Value]))
                yield return new(id, entry.Key.ToString());
    }
    private static FeSurface Find(FeModel model, string name) => model.Mesh.Surfaces.Values.SingleOrDefault(surface =>
        surface.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !surface.Internal) ?? throw new ArgumentException("Select an existing surface.");
    private static void Unique(FeModel model, string name, FeSurface? except = null)
    {
        if (model.Mesh.Surfaces.Values.Any(surface => !ReferenceEquals(surface, except) && surface.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Surface names must be unique, ignoring case.");
    }
}
