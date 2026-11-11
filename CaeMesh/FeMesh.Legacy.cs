// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using CaeGlobals;
using System.IO;

namespace CaeMesh
{
    public partial class FeMesh
    {
        public FeMesh DeepCopy()
        {
            try
            {
                FeMesh mesh = this.DeepClone();
                //
                using (MemoryStream ms = new MemoryStream())
                {
                    using (BinaryWriter bw = new BinaryWriter(ms))
                    {
                        WriteToBinaryWriter(this, bw);
                        //
                        bw.Flush();
                        bw.BaseStream.Seek(0, SeekOrigin.Begin);
                        using (BinaryReader br = new BinaryReader(bw.BaseStream))
                        {
                            ReadFromBinaryReader(mesh, br, -1);
                        }
                    }
                }
                //
                return mesh;
            }
            catch
            {
                return null;
            }
        }
        static partial void ReportWarning(string message)
        {
            MessageBoxes.ShowWarning(message);
        }
    }
}
