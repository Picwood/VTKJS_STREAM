using CaeMesh;
using FileInOut.Output;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectMeshNodeSelectionChecks
{
    private static FeMesh Strip(params double[] bottom)
    {
        int columns = bottom.Length;
        int Id(int x, int y, int z) => 1 + x + columns * y + columns * 2 * z;
        var nodes = new Dictionary<int, FeNode>();
        for (int z = 0; z < 2; z++) for (int y = 0; y < 2; y++) for (int x = 0; x < columns; x++)
            nodes.Add(Id(x, y, z), new(Id(x, y, z), x, y, z + bottom[x]));
        var elements = new Dictionary<int, FeElement>();
        for (int x = 0; x < columns - 1; x++) elements.Add(x + 1, new LinearHexaElement(x + 1,
            [Id(x,0,0), Id(x+1,0,0), Id(x+1,1,0), Id(x,1,0), Id(x,0,1), Id(x+1,0,1), Id(x+1,1,1), Id(x,1,1)]));
        return new FeMesh(nodes, elements, MeshRepresentation.Mesh);
    }

    public static void Run(Action<bool,string> check)
    {
        var mesh = Strip(0,0,0);
        MeshNodePick Pick(string mode, double angle = 30, double[]? point = null) => new(mode,1,[1,2,5],point ?? [.2,.1,0],angle);
        check(ProjectMeshNodeSelection.Pick(mesh, Pick("node")).SequenceEqual(new[]{1}), "node-target picking chooses the closest real mesh node");
        check(ProjectMeshNodeSelection.Pick(mesh, Pick("element")).SequenceEqual(new[]{1,2,4,5,7,8,10,11}), "element picking resolves all eight nodes, including nodes outside the picked face");
        check(ProjectMeshNodeSelection.Pick(mesh, Pick("face")).SequenceEqual(Enumerable.Range(1,6)), "Face mode selects the complete multi-element visualization face");
        check(ProjectMeshNodeSelection.Pick(mesh, Pick("face-angle",0)).SequenceEqual(Enumerable.Range(1,6)), "zero-degree Face angle grows across a coplanar mesh face");
        check(ProjectMeshNodeSelection.Pick(mesh, Pick("face-angle",89.9)).Length == 6 &&
            ProjectMeshNodeSelection.Pick(mesh, Pick("face-angle",90)).Length == 12, "Face angle includes the exact threshold and stops below a right-angle corner");
        check(ProjectMeshNodeSelection.Pick(mesh, Pick("edge",point:[.5,0,0])).SequenceEqual(new[]{1,2,3}), "Edge mode resolves a full feature edge across multiple elements");
        check(ProjectMeshNodeSelection.Pick(mesh, Pick("edge-angle",0,[.5,0,0])).SequenceEqual(new[]{1,2,3}), "Edge angle grows along a straight mesh chain");
        var diagonal = ProjectMeshNodeSelection.Pick(mesh, Pick("edge-angle",0,[.5,.5,0])).Select(id => mesh.Nodes[id]).ToArray();
        check(diagonal.All(n => n.X == diagonal[0].X) || diagonal.All(n => n.Y == diagonal[0].Y), "triangulated quad picking resolves a perimeter edge instead of a renderer diagonal");
        check(ProjectMeshNodeSelection.Pick(mesh, Pick("part")).Length == 12, "Part mode resolves the complete part membership");
        var disconnected=MultiPartProjectChecks.Create().Mesh;
        var firstPart=disconnected.Parts.Values.First(); var otherPart=disconnected.Parts.Values.Last();
        disconnected.Elements[2].PartId=firstPart.PartId;
        firstPart.NodeLabels=firstPart.NodeLabels.Concat(otherPart.NodeLabels).ToArray();
        firstPart.Labels=firstPart.Labels.Concat(otherPart.Labels).ToArray();
        disconnected.Parts.Remove(otherPart.Name);
        disconnected.ExtractPartVisualization(firstPart,false,Globals.EdgeAngle);
        check(ProjectMeshNodeSelection.Pick(disconnected,new("face-angle",1,[1,2,3],[.2,.2,0],180)).Length==8 &&
            ProjectMeshNodeSelection.Pick(disconnected,new("part",1,[1,2,3],[.2,.2,0])).Length==16,
            "angle growth stays on the connected island even when one part contains disconnected mesh");
        var curved = Strip(0,.1,.3,.6);
        var curvedSeed = new MeshNodePick("face-angle",1,[1,2,6],[.4,.2,.04],6);
        check(ProjectMeshNodeSelection.Pick(curved,curvedSeed).SequenceEqual(Enumerable.Range(1,8)) &&
            ProjectMeshNodeSelection.Pick(curved,curvedSeed with { Angle=5 }).Length == 4,
            "Face angle uses local neighboring normals rather than comparing every cell with the seed");
        check(ProjectMeshNodeSelection.Pick(curved,curvedSeed with { Mode="edge-angle", Point=[.5,0,.05] }).SequenceEqual(new[]{1,2,3,4}),
            "Edge angle follows gradual mesh curvature in both directions");
        var part = mesh.Parts.Values.Single();
        part.Visible = false;
        check(ProjectMeshNodeSelection.Universe(mesh).Length == 0 && ProjectMeshNodeSelection.Area(mesh,new("node",[1],[1])).Length == 0,
            "All and area selection exclude hidden parts");
        bool hiddenRejected = false;
        try { ProjectMeshNodeSelection.Pick(mesh,Pick("node")); } catch (ArgumentException) { hiddenRejected=true; }
        check(hiddenRejected,"point selection rejects hidden parts");
        part.Visible=true; part.Active=false;
        check(ProjectMeshNodeSelection.Universe(mesh).Length == 0,"All excludes inactive parts");
        part.Active=true;
        check(ProjectMeshNodeSelection.Area(mesh,new("node",[1,2,2,7],[1])).SequenceEqual(new[]{1,2,7}),"area Node mode preserves distinct node IDs");
        check(ProjectMeshNodeSelection.Area(mesh,new("element",[1,2,5,4],[1])).Length==0 &&
            ProjectMeshNodeSelection.Area(mesh,new("element",mesh.Elements[1].NodeIds,[1,2])).Length==8,
            "area Element mode requires complete element membership, not only rendered face nodes");
        check(ProjectMeshNodeSelection.Area(mesh,new("part",[1],[1])).Length==12,"area Part mode expands to the whole selected part");
        foreach(var invalid in new[]{Pick("unknown"),Pick("face-angle",double.NaN),Pick("edge-angle",181),
            Pick("node") with {TriangleNodes=[1,1,2]},Pick("node") with {TriangleNodes=[1,2,999]},Pick("node") with {Point=[double.NaN,0,0]}})
        {
            bool rejected=false;
            try { ProjectMeshNodeSelection.Pick(mesh,invalid); } catch (ArgumentException) { rejected=true; }
            check(rejected,"invalid mesh seed, mode or angle is rejected without changing membership");
        }
        bool unsupported=false;
        try { ProjectMeshNodeSelection.Area(mesh,new("edge",[],[])); } catch(ArgumentException) { unsupported=true; }
        check(unsupported,"area selection explicitly refuses point-only mesh modes");

        double[][] Box(double xmin,double xmax,double ymin,double ymax,double zmin,double zmax) =>
            [[xmin,ymin,zmin],[xmax,ymin,zmin],[xmax,ymax,zmin],[xmin,ymax,zmin],
             [xmin,ymin,zmax],[xmax,ymin,zmax],[xmax,ymax,zmax],[xmin,ymax,zmax]];
        var full=new MeshNodeFrustum("node",Box(-1,3,-1,2,-1,2));
        check(ProjectMeshNodeFrustum.Select(mesh,full).Length==12 &&
            ProjectMeshNodeFrustum.Select(mesh,full with {Mode="element"}).Length==12,"full-mesh rectangle resolves complete elements and all nodes");
        check(ProjectMeshNodeFrustum.Select(mesh,full with {Corners=Box(-.1,.1,-.1,1.1,-.1,1.1)}).Length==4 &&
            ProjectMeshNodeFrustum.Select(mesh,full with {Mode="element",Corners=Box(-.1,.1,-.1,1.1,-.1,1.1)}).Length==0,
            "rectangle Node and Element modes distinguish point inclusion from complete element inclusion");
        check(ProjectMeshNodeFrustum.Select(mesh,full with {Mode="part",Corners=Box(.4,.6,.4,.6,-1,2)}).Length==12,
            "rectangle Part mode detects a face crossing without any element corner inside");
        check(ProjectMeshNodeFrustum.Select(mesh,full with {SectionPoint=[1,0,0],SectionNormal=[1,0,0]}).Length==8,
            "full-mesh rectangles honor the displayed section half-space");
        check(ProjectMeshNodeFrustum.Select(mesh,full with {Corners=Box(0,2,-1,2,-1,2)}).Length==4,
            "rectangle point queries use strict frustum boundaries as in the original VTK selection");
        var gridNodes=new Dictionary<int,FeNode>(); var gridElements=new Dictionary<int,FeElement>();
        int GridId(int x,int y,int z) => 1+x+3*y+9*z;
        for(int z=0;z<3;z++) for(int y=0;y<3;y++) for(int x=0;x<3;x++) gridNodes.Add(GridId(x,y,z),new(GridId(x,y,z),x,y,z));
        for(int z=0;z<2;z++) for(int y=0;y<2;y++) for(int x=0;x<2;x++)
        {
            int id=gridElements.Count+1;
            gridElements.Add(id,new LinearHexaElement(id,[GridId(x,y,z),GridId(x+1,y,z),GridId(x+1,y+1,z),GridId(x,y+1,z),
                GridId(x,y,z+1),GridId(x+1,y,z+1),GridId(x+1,y+1,z+1),GridId(x,y+1,z+1)]));
        }
        var grid=new FeMesh(gridNodes,gridElements,MeshRepresentation.Mesh);
        check(ProjectMeshNodeFrustum.Select(grid,new("node",Box(.9,1.1,.9,1.1,.9,1.1))).SequenceEqual(new[]{14}) &&
            !grid.Parts.Values.Single().Visualization.Cells.SelectMany(c=>c).Contains(14),
            "native mesh rectangles can select an interior node absent from renderer exterior triangles");
        foreach(var invalid in new[]{full with {Mode="face"},full with {Corners=Box(0,0,0,0,0,0)},
            full with {Corners=Box(0,double.NaN,0,1,0,1)},full with {SectionPoint=[0,0,0],SectionNormal=[0,0,0]}})
        {
            bool rejected=false;
            try {ProjectMeshNodeFrustum.Select(mesh,invalid);} catch(ArgumentException) {rejected=true;}
            check(rejected,"invalid or unsupported full-mesh rectangle is refused");
        }

        var quadratic = new Dictionary<int,FeNode>();
        double[][] coordinates = [[0,0,0],[1,0,0],[0,1,0],[0,0,1],[.5,0,0],[.5,.5,0],[0,.5,0],[0,0,.5],[.5,0,.5],[0,.5,.5]];
        for(int i=0;i<coordinates.Length;i++) quadratic.Add(i+1,new(i+1,coordinates[i]));
        var tetra = new FeMesh(quadratic,new Dictionary<int,FeElement>{{1,new ParabolicTetraElement(1,Enumerable.Range(1,10).ToArray())}},MeshRepresentation.Mesh);
        var face = tetra.Parts.Values.Single().Visualization.Cells[0];
        var quadraticPick = new MeshNodePick("face",1,face.Take(3).ToArray(),tetra.Nodes[face[3]].Coor);
        check(ProjectMeshNodeSelection.Pick(tetra,quadraticPick).Length==6 &&
            ProjectMeshNodeSelection.Pick(tetra,quadraticPick with {Mode="element"}).Length==10,
            "quadratic Face and Element modes retain all midside nodes");
        check(ProjectMeshNodeSelection.Pick(tetra,quadraticPick with {Mode="node"}).SequenceEqual(new[]{face[3]}),"quadratic node picking can choose a midside node");
        check(ProjectMeshNodeSelection.Pick(tetra,quadraticPick with {Mode="edge-angle",Angle=0}).Length==3,"quadratic edge growth retains ordered midside nodes");

        string root=Path.Combine(Path.GetTempPath(),"node-selection-"+Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            string source=Path.Combine(root,"source.pma"),edited=Path.Combine(root,"edited.pma"),deck=Path.Combine(root,"edited.inp");
            StaticProjectArchive.Save(source,MultiPartProjectChecks.Create());
            byte[] before=File.ReadAllBytes(source);
            var selectionSession = new ProjectMeshSelectionSession();
            var cached = selectionSession.GetMesh(source);
            check(ReferenceEquals(cached, selectionSession.GetMesh(source)), "repeated picks reuse one validated mesh and its production topology");
            check(!ReferenceEquals(cached, selectionSession.GetMesh(source, restart:true)), "opening a new selection editor refreshes its mesh snapshot");
            check(ProjectMeshNodeSelection.Initial(source,"left").SequenceEqual(new[]{1,4,5,8}) &&
                ProjectMeshNodeSelection.Universe(source).Length==16,"selection sessions initialize full existing sets and include sparse-ID multipart nodes");
            var picked=ProjectMeshNodeSelection.Pick(source,new("face",1,[1,4,5],[0,.2,.2]));
            check(picked.SequenceEqual(new[]{1,4,5,8}),"production topology is available after PMA reopen");
            check(ProjectMeshNodeSelection.Ids(source,"1:4:2,3").SequenceEqual(new[]{1,3}),"ID mode resolves validated ranges and deduplicates membership");
            ProjectNodeSets.AddCopy(source,edited,new NodeSetSettings("picked",SelectedNodes:picked));
            var reopened=StaticProjectArchive.Open(edited);
            var changedMesh = selectionSession.GetMesh(edited);
            check(changedMesh.NodeSets.ContainsKey("picked") && !ReferenceEquals(changedMesh, cached), "selection cache switches with the edited project snapshot");
            File.SetLastWriteTimeUtc(edited, File.GetLastWriteTimeUtc(edited).AddSeconds(2));
            check(!ReferenceEquals(changedMesh, selectionSession.GetMesh(edited)), "selection cache invalidates an externally changed project");
            var retained = selectionSession.GetMesh(edited);
            selectionSession.Clear();
            check(!ReferenceEquals(retained, selectionSession.GetMesh(edited)), "closing a selection session releases its cached mesh");
            check(reopened.Model.Mesh.NodeSets["picked"].Labels.SequenceEqual(picked) && File.ReadAllBytes(source).SequenceEqual(before),"picked node membership survives save/reopen with original project unchanged");
            CalculixFileWriter.WriteActiveModel(deck,reopened.Model,FileInOut.Output.Calculix.ConvertPyramidsToEnum.Wedges);
            check(File.ReadAllText(deck).Contains("Nset=picked"),"production CalculiX export contains the authored NSET");
        }
        finally { Directory.Delete(root,true); }
    }
}
