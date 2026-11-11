using System.Text.Json.Nodes;

internal static class MetadataCompatibility
{
    public static void UseLegacyMaterials(JsonNode document)
    {
        foreach (var materialNode in document["Materials"]!.AsArray())
        {
            var material = materialNode!.AsObject();
            var properties = material["Properties"]!.AsArray();
            var elastic = properties.Single(property => property!["Kind"]!.GetValue<string>() == "Elastic")!;
            var density = properties.SingleOrDefault(property => property!["Kind"]!.GetValue<string>() == "Density");
            material["Elastic"] = elastic["Data"]!.DeepClone();
            material["Density"] = density?["Data"]?.DeepClone();
            material["DensityFirst"] = density != null && properties.IndexOf(density) < properties.IndexOf(elastic);
            material.Remove("Properties");
        }
        foreach (var set in document["NodeSets"]!.AsArray().Concat(document["ElementSets"]!.AsArray()))
            set!.AsObject().Remove("CreatedFromParts");
        document.AsObject().Remove("ReferencePoints");
        document.AsObject().Remove("Amplitudes");
        document.AsObject().Remove("Cylinders");
        document.AsObject().Remove("SurfaceInteractions");
        document.AsObject().Remove("ContactPairs");
        document.AsObject().Remove("Constraints");
        document.AsObject().Remove("UserKeywords");
        document.AsObject().Remove("CoordinateSystems");
    }

    public static void RemoveUserKeywords(JsonNode document)
    {
        document.AsObject().Remove("UserKeywords");
        document.AsObject().Remove("CoordinateSystems");
    }
}
