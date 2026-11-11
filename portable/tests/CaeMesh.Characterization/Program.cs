using CaeMesh;
using CaeGlobals;
using System.Globalization;

int checks = 0;
var originalCulture = CultureInfo.CurrentCulture;
try
{
    foreach (string culture in new[] { "en-US", "it-IT" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        PartCopyChecks.Run(Check);
        GeometryConfigurationChecks.Run(Check);
        MeshRegionChecks.Run(Check);
        GmshSetupChecks.Run(Check);
        GmshTopologyChecks.Run(Check);
        MeshHelperChecks.Run(Check);
        FeMeshChecks.Run(Check);
        VisualizationChecks.Run(Check);
        Solid(typeof(LinearTetraElement), typeof(ParabolicTetraElement),
            new[] { P(0,0,0), P(1,0,0), P(0,1,0), P(0,0,1) },
            new[] { (0,1),(1,2),(2,0),(0,3),(1,3),(2,3) }, 1d/6, P(.25,.25,.25), 4, 10, 24);
        Solid(typeof(LinearHexaElement), typeof(ParabolicHexaElement),
            new[] { P(0,0,0), P(1,0,0), P(1,1,0), P(0,1,0), P(0,0,1), P(1,0,1), P(1,1,1), P(0,1,1) },
            new[] { (0,1),(1,2),(2,3),(3,0),(4,5),(5,6),(6,7),(7,4),(0,4),(1,5),(2,6),(3,7) },
            1, P(.5,.5,.5), 6, 12, 25);
        Solid(typeof(LinearWedgeElement), typeof(ParabolicWedgeElement),
            new[] { P(0,0,0), P(1,0,0), P(0,1,0), P(0,0,1), P(1,0,1), P(0,1,1) },
            new[] { (0,1),(1,2),(2,0),(3,4),(4,5),(5,3),(0,3),(1,4),(2,5) },
            .5, P(1d/3,1d/3,.5), 5, 13, 26);
        Solid(typeof(LinearPyramidElement), typeof(ParabolicPyramidElement),
            new[] { P(-1,-1,0), P(1,-1,0), P(1,1,0), P(-1,1,0), P(0,0,1) },
            new[] { (0,1),(1,2),(2,3),(3,0),(0,4),(1,4),(2,4),(3,4) },
            4d/3, P(0,0,.25), 5, 14, 27);

        var nodes = new Dictionary<int, FeNode> { [10] = new(10,0,0,0), [20] = new(20,3,0,0), [30] = new(30,0,4,0) };
        var triangle = new LinearTriangleElement(1, new[] { 10,20,30 });
        Vector(triangle.GetCG(nodes, out double area), P(1,4d/3,0), "triangle centroid");
        Near(area, 6, "triangle area");
        var beam = new LinearBeamElement(2, new[] { 20,30 });
        Vector(beam.GetCG(nodes, out double length), P(1.5,2,0), "beam centroid");
        Near(length, 5, "beam length");
        var node = new FeNode(123,1,0,0);
        node.Rotate(new Vec3D(0,0,0), new[] { P(0,-1,0),P(1,0,0),P(0,0,1) });
        node.Translate(new Vec3D(2,3,4));
        Vector(node.Coor, P(2,4,4), "node rotation and translation");
        Check(node.Id == 123, "node identity");
    }
    Console.WriteLine($"PASS: {checks} CaeMesh characterization checks.");
}
finally { CultureInfo.CurrentCulture = originalCulture; }

void Solid(Type linear, Type quadratic, double[][] corners, (int,int)[] edges,
           double volume, double[] center, int faceCount, int linearVtk, int quadraticVtk)
{
    foreach (bool curved in new[] { false, true })
    foreach (double scale in new[] { 1d, 2.5 })
    {
        var coordinates = corners.ToList();
        if (curved) foreach (var (a,b) in edges)
            coordinates.Add(corners[a].Zip(corners[b], (x,y) => (x+y)/2).ToArray());
        var nodes = coordinates.Select((p,i) => new FeNode(100+i*3, p.Select(x => x*scale+10).ToArray())).ToDictionary(n => n.Id);
        int[] ids = nodes.Keys.ToArray();
        var element = (FeElement3D)Activator.CreateInstance(curved ? quadratic : linear, 7, 9, ids);
        string label = element.GetType().Name;
        Near(element.GetVolume(nodes), volume*Math.Pow(scale,3), label+" volume");
        Vector(element.GetCG(nodes, out double cgVolume), center.Select(x => x*scale+10).ToArray(), label+" centroid");
        Near(cgVolume, volume*Math.Pow(scale,3), label+" centroid volume");
        Check(element.Dimension == 3 && element.GetVtkCellType() == (curved ? quadraticVtk : linearVtk), label+" cell type");
        int[] vtkOrder = linear == typeof(LinearWedgeElement)
            ? (curved ? new[] {0,2,1,3,5,4,8,7,6,11,10,9,12,14,13} : new[] {0,2,1,3,5,4})
            : Enumerable.Range(0,ids.Length).ToArray();
        Check(element.GetVtkNodeIds().SequenceEqual(vtkOrder.Select(i => ids[i])), label+" VTK connectivity");
        Check(element.GetGmshNodeIds().OrderBy(x=>x).SequenceEqual(ids.OrderBy(x=>x)), label+" Gmsh node membership");
        var faces = element.GetAllVtkCells();
        Check(faces.Length == faceCount, label+" face count");
        double surfaceArea = 0;
        for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
        {
            FeFaceName face = FeElement.FaceNameFromVtkCellId(faceIndex);
            int[] faceIds = element.GetVtkCellFromFaceName(face);
            Check(faceIds.All(nodes.ContainsKey) && faceIds.Distinct().Count() == faceIds.Length, label+" face membership");
            Vec3D a = new(nodes[faceIds[0]].Coor), b = new(nodes[faceIds[1]].Coor), c = new(nodes[faceIds[2]].Coor);
            var normal = Vec3D.CrossProduct(b-a,c-a);
            var bodyCenter = new Vec3D(center.Select(x=>x*scale+10).ToArray());
            Check(Vec3D.DotProduct(normal,a-bodyCenter)>0, label+" outward face normal");
            double[] faceCenter = element.GetFaceCG(face,nodes,out double faceArea);
            Near(element.GetArea(face,nodes),faceArea,label+" face area consistency");
            surfaceArea += faceArea;
            Check(faceArea>0 && faceCenter.All(double.IsFinite),label+" finite face geometry");
        }
        double expectedSurfaceArea = linear == typeof(LinearTetraElement) ? (3+Math.Sqrt(3))/2
            : linear == typeof(LinearHexaElement) ? 6
            : linear == typeof(LinearWedgeElement) ? 3+Math.Sqrt(2)
            : 4+4*Math.Sqrt(2);
        Near(surfaceArea,expectedSurfaceArea*scale*scale,label+" analytic surface area");
        var copy = element.DeepCopy();
        copy.NodeIds[0] = -1;
        Check(element.NodeIds[0] == 100 && copy.Id == 7 && copy.PartId == 9, label+" independent copy");
        element.Mirror(); element.Mirror();
        Check(element.NodeIds.SequenceEqual(ids), label+" mirror round trip");
    }
}
double[] P(double x,double y,double z) => new[] {x,y,z};
void Check(bool value,string message) { if (!value) throw new Exception("FAIL: "+message); checks++; }
void Near(double actual,double expected,string message) => Check(Math.Abs(actual-expected)<=1e-9*Math.Max(1,Math.Abs(expected)),$"{message}: {actual} != {expected}");
void Vector(double[] actual,double[] expected,string message) { for(int i=0;i<3;i++) Near(actual[i],expected[i],message); }
