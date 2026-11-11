using PrePoMax.Persistence;

namespace PrePoMax.Application;

/// <summary>Bounded, read-only semantic capture for the parity harness, not a complete model codec.</summary>
public static class ProjectParitySnapshot
{
    public static object Read(string path, string? nodeSet, string? fieldName, string? component, int step, int increment)
    {
        var project = StaticProjectArchive.Open(path);
        var mesh = project.Model.Mesh;
        if (mesh.Nodes.Count > 100000 || mesh.Elements.Count > 100000)
            throw new NotSupportedException("The initial parity snapshot is limited to 100000 nodes/elements.");
        object Mesh(CaeMesh.FeMesh value) => new
        {
            nodes = value.Nodes.Values.OrderBy(node => node.Id).Select(node => new { id = node.Id, coordinates = new[] { node.X, node.Y, node.Z } }).ToArray(),
            elements = value.Elements.Values.OrderBy(element => element.Id).Select(element => new { id = element.Id, type = element.GetType().Name, nodes = element.NodeIds }).ToArray(),
        };
        if (fieldName != null)
        {
            var selected = ProjectResults.List(path).Single(field => field.Name == fieldName && field.Component == component && field.Step == step && field.Increment == increment);
            var values = ProjectResults.Values(path, selected);
            return new
            {
                schema = "prepomac.parity-snapshot/1", kind = "field", mesh = Mesh(values.Mesh),
                field = new
                {
                    name = selected.Name, component = selected.Component, step = selected.Step, increment = selected.Increment,
                    time = selected.Time, unit = values.Units,
                    values = values.Values.OrderBy(pair => pair.Key).Select(pair => new { nodeId = pair.Key, float32Bits = BitConverter.SingleToInt32Bits(pair.Value) }).ToArray(),
                },
            };
        }
        var set = nodeSet == null ? null : mesh.NodeSets[nodeSet];
        return new
        {
            schema = "prepomac.parity-snapshot/1", kind = "model",
            document = new { modelSpace = project.Model.Properties.ModelSpace.ToString(), unitSystem = project.Model.UnitSystem.UnitSystemType.ToString() },
            materials = ProjectMaterials.List(path).OrderBy(value => value.Name, StringComparer.Ordinal)
                .Select(value => new { name = value.Name, youngsModulus = value.YoungsModulus, poissonsRatio = value.PoissonsRatio }).ToArray(),
            mesh = Mesh(mesh),
            nodeSet = set == null ? null : new { name = set.Name, nodes = set.Labels.Order().ToArray() },
        };
    }
}
