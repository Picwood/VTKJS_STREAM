using System.Buffers.Binary;
using System.Text;
using PrePoMax.Render.Contracts;
using PrePoMax.Render.Server;

namespace PrePoMax.Render.Server.Tests;

public sealed class PickGeometryTests
{
    [Fact]
    public async Task PickUsesEncodedTriangleBoundsAtLargeScaleAndStillRejectsForgedHits()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen:true))
        {
            writer.Write("PMXSCN1\0"u8); writer.Write(2);
            foreach (double z in new[] {0.123456789, 1e8})
            {
                var name = Encoding.UTF8.GetBytes("part-" + z.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.Write(name.Length); writer.Write(name); writer.Write(unchecked((int)0xff6699cc));
                writer.Write(new byte[]{1,1,1,0}); writer.Write(.2); writer.Write(.8); writer.Write(1f); writer.Write(3f);
                writer.Write(3); writer.Write(1);
                for(int i=0;i<3;i++)
                {
                    writer.Write(101+i); writer.Write(i==1?1d:0d); writer.Write(i==2?1d:0d); writer.Write(z); writer.Write(0f);
                }
                writer.Write(1001); writer.Write(0u); writer.Write(1u); writer.Write(2u);
            }
        }
        stream.Position=0;
        var scene=await HostSceneUploadReader.ReadAsync(stream,stream.Length,HostSceneUploadReader.DefaultMaximumBytes,CancellationToken.None);
        var state=new SceneState(); state.Replace(scene);
        var actor=scene.Manifest.Actors[0]; var chunk=Assert.Single(actor.Chunks);
        byte[] encoded=new byte[4];
        scene.Resources[chunk.Resources.Positions.ResourceId].Read(2*sizeof(float),encoded);
        double zHit=scene.Manifest.WorldOrigin[2]+BinaryPrimitives.ReadSingleLittleEndian(encoded);
        Assert.True(Math.Abs(zHit-chunk.Bounds[4])>1e-4); // Previously rejected despite being the uploaded rendered plane.
        var request=new PickResolveRequest(actor.ActorId,chunk.ChunkId,"triangle",0,1001,101,
            [.2,.2,zHit],[320,240],[0,0,-1],.01,"element");
        Assert.True(state.ResolvePick(request).Succeeded);
        Assert.False(state.ResolvePick(request with {WorldPosition=[100,.2,zHit]}).Succeeded);
        Assert.False(state.ResolvePick(request with {WorldPosition=[.2,.2,zHit+100]}).Succeeded);
        Assert.False(state.ResolvePick(request with {WorldPosition=[.2,.2,zHit+.01]}).Succeeded);
        Assert.False(state.ResolvePick(request with {SourceElementId=999}).Succeeded);
        Assert.False(state.ResolvePick(request with {CandidateSourceNodeId=999}).Succeeded);
    }
}
