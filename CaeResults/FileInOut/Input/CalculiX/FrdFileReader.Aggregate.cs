namespace CaeResults
{
    public static partial class FrdFileReader
    {
        public static FeResults Read(string fileName)
        {
            FeResults result = null;
            ReadData(fileName, data =>
            {
                result = new FeResults(fileName, null);
                result.HashName = data.HashName;
                result.DateTime = data.DateTime;
                result.UnitSystem = data.UnitSystem;
                foreach (var entry in data.Fields) result.AddField(entry.Key, entry.Value);
                result.Preprocess();
                result.SetMesh(data.Mesh, data.NodeIdsLookUp);
                // SetMesh copies the mesh without sets. Apply FRD material and gap
                // sets to that result mesh, preserving the desktop import sequence.
                data.Mesh = result.Mesh;
            });
            return result;
        }
    }
}
