using System;
using System.Diagnostics;
namespace CaeMesh
{
    public partial class FeMesh
    {
        public static event Action<string> Warning;
        static partial void ReportWarning(string message)
        {
            Action<string> handler = Warning;
            if (handler != null) handler(message);
            else Trace.TraceWarning(message);
        }
    }
}
