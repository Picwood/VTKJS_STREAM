using CaeMesh;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record MeshNodeFrustum(string Mode, double[][] Corners, double[]? SectionPoint = null, double[]? SectionNormal = null);

public static class ProjectMeshNodeFrustum
{
    private sealed record Plane(double[] Normal, double[] Origin)
    {
        public double Value(double[] point) => Enumerable.Range(0,3).Sum(i => Normal[i]*(point[i]-Origin[i]));
    }

    public static int[] Select(string path, MeshNodeFrustum request) => Select(StaticProjectArchive.Open(path).Model.Mesh,request);
    public static int[] Select(FeMesh mesh, MeshNodeFrustum request)
    {
        static bool Finite(double[]? point) => point is {Length:3} && point.All(double.IsFinite);
        if (request?.Corners is not {Length:8} || request.Corners.Any(p => !Finite(p)))
            throw new ArgumentException("A mesh rectangle requires eight finite frustum corners.");
        var corners=request.Corners;
        double[] center=Enumerable.Range(0,3).Select(i => corners.Average(p => p[i])).ToArray();
        double span=Enumerable.Range(0,3).Max(i => corners.Max(p => p[i])-corners.Min(p => p[i]));
        if (!double.IsFinite(span) || span<=0) throw new ArgumentException("The selection frustum is degenerate.");
        double tolerance=span*1e-10;
        int[][] faces=[[0,1,2,3],[4,5,6,7],[0,4,5,1],[1,5,6,2],[2,6,7,3],[3,7,4,0]];
        var planes=new List<Plane>();
        foreach(var face in faces)
        {
            var a=corners[face[0]];
            var u=Enumerable.Range(0,3).Select(i => corners[face[1]][i]-a[i]).ToArray();
            var v=Enumerable.Range(0,3).Select(i => corners[face[2]][i]-a[i]).ToArray();
            double[] n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];
            double length=Math.Sqrt(n.Sum(x => x*x));
            if (!double.IsFinite(length) || length<=span*span*1e-14) throw new ArgumentException("The selection frustum is degenerate.");
            n=n.Select(x => x/length).ToArray();
            var plane=new Plane(n,a);
            if (plane.Value(center)<0) plane=new(n.Select(x => -x).ToArray(),a);
            if (Math.Abs(plane.Value(corners[face[3]]))>tolerance || corners.Any(p => plane.Value(p)<-tolerance) || plane.Value(center)<=tolerance)
                throw new ArgumentException("The selection frustum must be convex and nondegenerate.");
            planes.Add(plane);
        }
        if (request.SectionPoint!=null || request.SectionNormal!=null)
        {
            if (!Finite(request.SectionPoint) || !Finite(request.SectionNormal)) throw new ArgumentException("Invalid section plane.");
            double length=Math.Sqrt(request.SectionNormal!.Sum(x => x*x));
            if (!double.IsFinite(length) || length<=0) throw new ArgumentException("Invalid section plane normal.");
            planes.Add(new(request.SectionNormal!.Select(x => x/length).ToArray(),request.SectionPoint!));
        }
        // Match the original VTK point-frustum query: selection planes are strict;
        // a displayed section plane keeps its nonnegative half-space.
        bool Inside(int id) => planes.Take(6).All(p => p.Value(mesh.Nodes[id].Coor)>0) &&
            (planes.Count==6 || planes[6].Value(mesh.Nodes[id].Coor)>=0);
        var parts=mesh.Parts.Values.Where(p => p.Visible && p.Active).ToArray();
        IEnumerable<int> selected=request.Mode switch
        {
            "node" => parts.SelectMany(p => p.NodeLabels).Where(Inside),
            "element" => parts.SelectMany(p => p.Labels).Select(id => mesh.Elements[id]).Where(e => e.NodeIds.All(Inside)).SelectMany(e => e.NodeIds),
            "part" => parts.Where(p => p.Labels.Any(id => Intersects(mesh,mesh.Elements[id],planes,tolerance))).SelectMany(p => p.NodeLabels),
            _ => throw new ArgumentException("Use Node, Element or Part for rectangle selection; pick a point for edges, faces and angles.")
        };
        var result=selected.Distinct().Take(ProjectMeshNodeSelection.MaximumNodes+1).Order().ToArray();
        if (result.Length>ProjectMeshNodeSelection.MaximumNodes) throw new NotSupportedException("Rectangle selection exceeds 100000 nodes.");
        return result;
    }

    private static bool Intersects(FeMesh mesh,FeElement element,List<Plane> planes,double tolerance)
    {
        if (element.NodeIds.Any(id => planes.All(p => p.Value(mesh.Nodes[id].Coor)>=-tolerance))) return true;
        foreach(var face in element.GetAllVtkCells())
        {
            // Straight-sided quadratic faces use their corner polygon, as in the bounded PMA geometry path.
            int count=face.Length switch {6=>3,8=>4,_=>face.Length};
            var polygon=face.Take(count).Select(id => mesh.Nodes[id].Coor).ToList();
            foreach(var plane in planes)
            {
                if(polygon.Count==0) break;
                var clipped=new List<double[]>();
                var previous=polygon[^1]; double before=plane.Value(previous);
                foreach(var current in polygon)
                {
                    double after=plane.Value(current);
                    if ((before>=-tolerance)!=(after>=-tolerance))
                    {
                        double fraction=before/(before-after);
                        clipped.Add(Enumerable.Range(0,3).Select(i => previous[i]+fraction*(current[i]-previous[i])).ToArray());
                    }
                    if(after>=-tolerance) clipped.Add(current);
                    previous=current; before=after;
                }
                polygon=clipped;
            }
            if(polygon.Count>0) return true;
        }
        return false;
    }
}
