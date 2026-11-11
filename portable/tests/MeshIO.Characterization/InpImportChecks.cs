using CaeMesh;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class InpImportChecks
{
    const string Tetra = "*Node,nset=ALLNODES\n10,0,0,0\n20,1.D0,0,0\n30,0,1,0\n40,0,0,1\n*Element,type=C3D4,elset=BODY\n100,10,20,30,40\n*Nset,nset=face\n20,30,40\n*Nset,nset=FACE\n10\n*Elset,elset=generated,generate\n100,100,1\n*Surface,name=LOADED,type=element\nBODY,S2\n*Surface,name=MIXED,type=ELEMENT\n100,S3\ngenerated,S3\n";
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "inp import é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "mesh.INP"), output = Path.Combine(root, "mesh.pma");
            File.WriteAllText(source, Tetra);
            ProjectMeshImport.Import(source, output, 1000, .25);
            var model = StaticProjectArchive.Open(output).Model;
            check(model.Mesh.Nodes.Keys.Order().SequenceEqual(new[] { 10, 20, 30, 40 }) && model.Mesh.Elements.ContainsKey(100), "INP preserves sparse node and element IDs");
            check(model.Mesh.NodeSets["face"].Labels.SequenceEqual(new[] { 10, 20, 30, 40 }), "INP merges repeated set names case-insensitively");
            check(model.Mesh.NodeSets["ALLNODES"].Labels.Length == 4 && model.Mesh.NodeSets.ContainsKey("ALLNODES_"), "INP preserves input ALLNODES without collision");
            check(model.Mesh.ElementSets["BODY"].Labels.SequenceEqual(new[] { 100 }) && model.Mesh.ElementSets["generated"].Labels.SequenceEqual(new[] { 100 }), "INP retains implicit and generated element sets");
            check(model.Mesh.Surfaces["LOADED"].ElementFaces.Single().Key == FeFaceName.S2 &&
                model.Mesh.Surfaces["LOADED"].ElementFaces.Single().Value == "BODY", "INP retains named-set element surface");
            string mixedSet = model.Mesh.Surfaces["MIXED"].ElementFaces[FeFaceName.S3];
            check(model.Mesh.ElementSets[mixedSet].Labels.SequenceEqual(new[] { 100 }), "INP merges repeated surface-face sources into an internal element set");
            check(ProjectLoads.CreationTargets(output).Any(target => target.Step == "load-1" && target.Region == "LOADED" &&
                target.Kind == "Surface traction (total force)"), "INP element surface is available as a traction creation target");
            string tractionProject = Path.Combine(root, "mesh-traction.pma");
            ProjectLoads.AddCopy(output, tractionProject, "load-1", "imported-traction", "LOADED",
                ForceLoadCreationKind.SurfaceTraction, 1, -2, 3);
            var importedTraction = ProjectLoads.List(tractionProject).Single(load => load.Name == "imported-traction");
            check(importedTraction.Region == "LOADED" && importedTraction.Kind == "Surface traction (total force)" &&
                importedTraction.F1 == 1 && importedTraction.F2 == -2 && importedTraction.F3 == 3,
                "INP surface creates a persisted total-force traction without altering the source project");
            check(model.Mesh.Nodes[20].X == 1 && File.ReadAllText(source) == Tetra, "INP accepts Fortran exponent without changing input");
            foreach (string type in new[] { "C3D8", "C3D8R" })
            {
                string hexa = "*Node\n1,0,0,0\n2,1,0,0\n3,1,1,0\n4,0,1,0\n5,0,0,1\n6,1,0,1\n7,1,1,1\n8,0,1,1\n*Element,type=" + type + "\n9,1,2,3,4,\n5,6,7,8\n*Nset,nset=generated,generate\n1,7,2\n";
                File.WriteAllText(source, hexa);
                string saved = Path.Combine(root, type + ".pma");
                ProjectMeshImport.Import(source, saved, 1000, 0);
                var restored = StaticProjectArchive.Open(saved).Model.Mesh;
                check(restored.Elements[9] is LinearHexaElement && ((MeshPart)restored.Parts.Values.Single()).LinearHexaType.ToString() == type, "INP retains hexahedral formulation and continuation");
                check(restored.NodeSets["generated"].Labels.SequenceEqual(new[] { 1, 3, 5, 7 }), "INP generated set honors increment");
            }
            foreach (string invalid in new[] { Tetra.Replace("C3D4", "C3D10"), Tetra.Replace("100,10,20,30,40", "100,10,20,30,99"),
                Tetra.Replace("100,10,20,30,40", "100,10,10,30,40"), Tetra.Replace("1.D0", "NaN"), Tetra + "*Include,input=other.inp\n",
                Tetra + "*Material,name=steel\n", Tetra.Replace("100,100,1", "100,1,1"), Tetra.Replace("100,100,1", "100,100,0"),
                Tetra.Replace("100,10,20,30,40", "100,10,20"), Tetra + "*Nset,nset=bad\n99\n", Tetra + "*Node\n10,0,0,0\n",
                Tetra + "*Surface,name=bad,type=node\nBODY,S1\n", Tetra + "*Surface,name=bad,type=element\nmissing,S1\n",
                Tetra + "*Surface,name=bad,type=element\nBODY,S5\n", Tetra + "*Surface,name=bad,type=element\nBODY,7\n" })
            {
                File.WriteAllText(source, invalid);
                string bad = Path.Combine(root, "bad.pma");
                bool rejected = false;
                try { ProjectMeshImport.Import(source, bad, 1000, 0); }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(bad), "invalid or unsupported INP produces no archive");
            }
            string sub = Path.Combine(root, "sub folder");
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(sub, "nodes.inc"), Tetra.Split("*Element")[0]);
            File.WriteAllText(Path.Combine(sub, "mesh.inc"), "*Include,input=nodes.inc\n*Element" + Tetra.Split("*Element")[1]);
            string includes = "*Include,input=\"sub folder\\mesh.inc\"\n*Nset,nset=combined\nfuture,20\n*Nset,nset=future\nFACE\n*Elset,elset=combinedElements\nBODY,generated\n";
            File.WriteAllText(source, includes);
            string includedOutput = Path.Combine(root, "included.pma");
            ProjectMeshImport.Import(source, includedOutput, 1000, 0);
            var includedMesh = StaticProjectArchive.Open(includedOutput).Model.Mesh;
            check(includedMesh.Nodes.Count == 4 && includedMesh.Elements.Count == 1, "INP nested includes resolve relative to their containing file");
            check(includedMesh.NodeSets["combined"].Labels.SequenceEqual(new[] { 10, 20, 30, 40 }), "INP forward and case-insensitive node set references resolve");
            check(includedMesh.ElementSets["combinedElements"].Labels.SequenceEqual(new[] { 100 }), "INP element set references deduplicate members");
            check(File.ReadAllText(source) == includes, "INP include source stays unchanged");
            foreach (string invalid in new[] { Tetra + "*Nset,nset=a\nb\n*Nset,nset=b\na\n", Tetra + "*Nset,nset=a\nunknown\n",
                Tetra + "*Elset,elset=a\na\n", "*Include,input=mesh.INP\n", "*Include,input=missing.inc\n",
                "*Include,input=\"sub folder/mesh.inc\",unsupported=yes\n" })
            {
                File.WriteAllText(source, invalid);
                string bad = Path.Combine(root, "bad-includes.pma");
                bool rejected = false;
                try { ProjectMeshImport.Import(source, bad, 1000, 0); }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(bad), "INP invalid includes or set dependencies produce no archive");
            }
            File.WriteAllText(source, Tetra);
            byte[] original = File.ReadAllBytes(output);
            bool overwrite = false;
            try { ProjectMeshImport.Import(source, output, 1000, 0); } catch (IOException) { overwrite = true; }
            check(overwrite && File.ReadAllBytes(output).SequenceEqual(original), "INP import refuses overwrite");
        }
        finally { Directory.Delete(root, true); }
    }
}
