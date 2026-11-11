// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CaeModel;
using CaeMesh;
using System.IO;
using FileInOut.Output.Calculix;
using CaeGlobals;
using Microsoft.SqlServer.Server;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Diagnostics.Eventing.Reader;
using System.Security;
using System.Diagnostics;

namespace FileInOut.Output
{
    public static partial class CalculixFileWriter
    {
        static public void Write(string fileName, FeModel model, ConvertPyramidsToEnum convertPyramidsTo,
                                 Dictionary<int, double[]> deformations = null)
        {
            // Copy model
            FeModel copy = model.DeepCopy();
            string[] inactivePartNames = copy.Mesh.GetInactivePartNames();
            copy.Mesh.RemoveParts(inactivePartNames, out _, false);
            //
            List<CalculixKeyword> keywords = GetAllKeywords(copy, convertPyramidsTo, deformations);
            // Write file
            StringBuilder sb = new StringBuilder();
            foreach (var keyword in keywords)
            {
                WriteKeywordRecursively(sb, keyword);
            }
            // Write to file in multiple steps
            int step = 10_000_000;
            int upper;
            using (StreamWriter sw = new StreamWriter(fileName, false))
            {
                for (int i = 0; i < sb.Length; i+=step)
                {
                    upper = Math.Min(sb.Length - i, step);
                    sw.Write(sb.ToString(i, upper));
                }
            }
        }
    }
}
