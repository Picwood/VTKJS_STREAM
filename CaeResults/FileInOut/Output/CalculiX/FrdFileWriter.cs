// PrePoMax - Copyright (C) 2016-2026 Matej BorovinÅ¡ek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej BorovinÅ¡ek
// Contributors:

using CaeMesh;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CaeResults
{
    public static class FrdFileWriter
    {
        private const int ValuesPerLine = 6;

        public static void WriteDisplayedParts(string fileName, FeResults results, string[] partNames,
                                               string[] fieldNames, bool allStepsAndIncrements,
                                               int stepId, int stepIncrementId, int[] elementIdsToExport = null)
        {
            if (results == null) throw new ArgumentNullException("results");
            if (results.Mesh == null) throw new ArgumentException("The results do not contain a mesh.", "results");
            if (partNames == null || partNames.Length == 0) throw new ArgumentException("No displayed parts were selected.");
            if (fieldNames == null || fieldNames.Length == 0) throw new ArgumentException("No result field was selected.");
            //
            FeMesh mesh = results.Mesh;
            HashSet<int> partElementIds = new HashSet<int>();
            foreach (string partName in partNames)
            {
                if (!mesh.Parts.ContainsKey(partName)) throw new ArgumentException("The part '" + partName + "' does not exist.");
                partElementIds.UnionWith(mesh.Parts[partName].Labels);
            }
            //
            HashSet<int> selectedElementIds;
            if (elementIdsToExport == null) selectedElementIds = partElementIds;
            else
            {
                selectedElementIds = new HashSet<int>(elementIdsToExport);
                selectedElementIds.IntersectWith(partElementIds);
            }
            //
            HashSet<int> selectedNodeIds = new HashSet<int>();
            foreach (int elementId in selectedElementIds)
            {
                if (mesh.Elements.TryGetValue(elementId, out FeElement element))
                    selectedNodeIds.UnionWith(element.NodeIds);
            }
            //
            if (selectedElementIds.Count == 0) throw new ArgumentException("The displayed parts do not contain elements.");
            if (selectedNodeIds.Count == 0) throw new ArgumentException("The displayed parts do not contain nodes.");
            //
            List<FieldData> fieldsToWrite = GetFieldsToWrite(results, fieldNames, allStepsAndIncrements, stepId, stepIncrementId);
            if (fieldsToWrite.Count == 0) throw new ArgumentException("None of the selected fields exist for the selected increment.");
            //
            string directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
            //
            using (StreamWriter writer = new StreamWriter(fileName, false, Encoding.ASCII))
            {
                WriteHeader(writer, results);
                WriteNodes(writer, mesh, selectedNodeIds);
                WriteElements(writer, mesh, selectedElementIds);
                //
                int blockId = 1;
                foreach (FieldData fieldData in fieldsToWrite)
                {
                    WriteField(writer, results, selectedNodeIds, fieldData, blockId++);
                }
            }
        }

        private static List<FieldData> GetFieldsToWrite(FeResults results, string[] fieldNames, bool allStepsAndIncrements,
                                                        int stepId, int stepIncrementId)
        {
            HashSet<string> selected = new HashSet<string>(fieldNames, StringComparer.OrdinalIgnoreCase);
            List<FieldData> fields = new List<FieldData>();
            //
            foreach (FieldData fieldData in results.GetAllFieldData())
            {
                if (!selected.Contains(fieldData.Name)) continue;
                if (!allStepsAndIncrements &&
                    (fieldData.StepId != stepId || fieldData.StepIncrementId != stepIncrementId)) continue;
                if (fields.Any(fd => fd.Name.Equals(fieldData.Name, StringComparison.OrdinalIgnoreCase) &&
                                     fd.StepId == fieldData.StepId &&
                                     fd.StepIncrementId == fieldData.StepIncrementId)) continue;
                //
                fields.Add(new FieldData(fieldData));
            }
            //
            fields.Sort((a, b) =>
            {
                int compare = a.StepId.CompareTo(b.StepId);
                if (compare != 0) return compare;
                compare = a.StepIncrementId.CompareTo(b.StepIncrementId);
                if (compare != 0) return compare;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return fields;
        }

        private static void WriteHeader(StreamWriter writer, FeResults results)
        {
            writer.WriteLine("    1C");
            writer.WriteLine("    1UUSER    PrePoMax custom displayed-results FRD export");
            writer.WriteLine("    1UDATE    " + DateTime.Now.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture));
            writer.WriteLine("    1UTIME    " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            writer.WriteLine("    1UCOMMENTS Hash: " + results.HashName + ", Unit system: " + results.UnitSystem.UnitSystemType);
            writer.WriteLine(" -3");
        }

        private static void WriteNodes(StreamWriter writer, FeMesh mesh, HashSet<int> selectedNodeIds)
        {
            int[] nodeIds = selectedNodeIds.OrderBy(id => id).ToArray();
            writer.WriteLine("    2C" + nodeIds.Length.ToString(CultureInfo.InvariantCulture).PadLeft(12));
            foreach (int nodeId in nodeIds)
            {
                FeNode node = mesh.Nodes[nodeId];
                writer.WriteLine(" -1" + nodeId.ToString(CultureInfo.InvariantCulture).PadLeft(10) +
                                 FormatFloat(node.X) + FormatFloat(node.Y) + FormatFloat(node.Z));
            }
            writer.WriteLine(" -3");
        }

        private static void WriteElements(StreamWriter writer, FeMesh mesh, HashSet<int> selectedElementIds)
        {
            int[] elementIds = selectedElementIds.OrderBy(id => id).ToArray();
            writer.WriteLine("    3C" + elementIds.Length.ToString(CultureInfo.InvariantCulture).PadLeft(12));
            foreach (int elementId in elementIds)
            {
                FeElement element = mesh.Elements[elementId];
                FrdFeDescriptorId descriptor = GetDescriptor(element);
                writer.WriteLine(" -1" + element.Id.ToString(CultureInfo.InvariantCulture).PadLeft(10) +
                                 ((int)descriptor).ToString(CultureInfo.InvariantCulture).PadLeft(7) +
                                 "       0");
                foreach (int[] nodeLine in GetFrdElementNodeLines(element))
                {
                    writer.Write(" -2");
                    foreach (int nodeId in nodeLine)
                        writer.Write(nodeId.ToString(CultureInfo.InvariantCulture).PadLeft(10));
                    writer.WriteLine();
                }
            }
            writer.WriteLine(" -3");
        }

        private static void WriteField(StreamWriter writer, FeResults results, HashSet<int> selectedNodeIds,
                                       FieldData fieldData, int blockId)
        {
            Field field = results.GetField(fieldData);
            string[] componentNames = field.GetComponentNames()
                                           .Where(name => !field.IsComponentInvariant(name))
                                           .ToArray();
            if (componentNames.Length == 0) return;
            //
            int[] nodeIds = selectedNodeIds.OrderBy(id => id).ToArray();
            float[][] values = new float[componentNames.Length][];
            for (int i = 0; i < componentNames.Length; i++)
            {
                FieldData componentFieldData = new FieldData(fieldData);
                componentFieldData.Component = componentNames[i];
                values[i] = results.GetValues(componentFieldData, nodeIds);
                if (values[i] == null)
                    throw new InvalidDataException("Unable to export field " + fieldData.Name +
                                                   " component " + componentNames[i] + ".");
            }
            //
            int incrementId = fieldData.StepIncrementId;
            int stepId = fieldData.StepId;
            int globalId = fieldData.GlobalIncrementId >= 0 ? fieldData.GlobalIncrementId + 100 : blockId + 100;
            int methodId = fieldData.MethodId >= 0 ? fieldData.MethodId : 0;
            //
            writer.WriteLine("    1PSTEP" + blockId.ToString(CultureInfo.InvariantCulture).PadLeft(26) +
                             incrementId.ToString(CultureInfo.InvariantCulture).PadLeft(12) +
                             stepId.ToString(CultureInfo.InvariantCulture).PadLeft(12));
            writer.WriteLine("  100CL" + globalId.ToString(CultureInfo.InvariantCulture).PadLeft(5) +
                             FormatFloat(fieldData.Time) +
                             nodeIds.Length.ToString(CultureInfo.InvariantCulture).PadLeft(12) +
                             methodId.ToString(CultureInfo.InvariantCulture).PadLeft(22) +
                             incrementId.ToString(CultureInfo.InvariantCulture).PadLeft(12));
            writer.WriteLine(" -4  " + fieldData.Name.PadRight(10) +
                             componentNames.Length.ToString(CultureInfo.InvariantCulture).PadLeft(5) +
                             "    1");
            //
            for (int i = 0; i < componentNames.Length; i++)
            {
                writer.WriteLine(" -5  " + ToFrdComponentName(fieldData.Name, componentNames[i]).PadRight(10) +
                                 "    1    2" +
                                 (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(5) +
                                 "    0");
            }
            //
            for (int nodeIndex = 0; nodeIndex < nodeIds.Length; nodeIndex++)
            {
                int componentIndex = 0;
                bool firstLine = true;
                while (componentIndex < componentNames.Length)
                {
                    if (firstLine)
                    {
                        writer.Write(" -1" + nodeIds[nodeIndex].ToString(CultureInfo.InvariantCulture).PadLeft(10));
                        firstLine = false;
                    }
                    else writer.Write(" -2" + "".PadLeft(10));
                    //
                    int count = Math.Min(ValuesPerLine, componentNames.Length - componentIndex);
                    for (int i = 0; i < count; i++)
                        writer.Write(FormatFloat(values[componentIndex++][nodeIndex]));
                    writer.WriteLine();
                }
            }
            writer.WriteLine(" -3");
        }

        private static FrdFeDescriptorId GetDescriptor(FeElement element)
        {
            if (element is LinearBeamElement) return FrdFeDescriptorId.BeamLinear;
            if (element is LinearTriangleElement) return FrdFeDescriptorId.ShellLinearTriangle;
            if (element is LinearQuadrilateralElement) return FrdFeDescriptorId.ShellLinearQuadrilateral;
            if (element is LinearTetraElement) return FrdFeDescriptorId.SolidLinearTetrahedron;
            if (element is LinearWedgeElement) return FrdFeDescriptorId.SolidLinearWedge;
            if (element is LinearHexaElement) return FrdFeDescriptorId.SolidLinearHexahedron;
            if (element is ParabolicTriangleElement) return FrdFeDescriptorId.ShellParabolicTriangle;
            if (element is ParabolicQuadrilateralElement) return FrdFeDescriptorId.ShellParabolicQuadrilateral;
            if (element is ParabolicTetraElement) return FrdFeDescriptorId.SolidParabolicTetrahedron;
            if (element is ParabolicWedgeElement) return FrdFeDescriptorId.SolidParabolicWedge;
            if (element is ParabolicHexaElement) return FrdFeDescriptorId.SolidParabolicHexahedron;
            throw new NotSupportedException("The element type " + element.GetType().Name + " is not supported by FRD export.");
        }

        private static IEnumerable<int[]> GetFrdElementNodeLines(FeElement element)
        {
            int[] n = element.NodeIds;
            if (element is ParabolicWedgeElement)
            {
                yield return new int[] { n[0], n[1], n[2], n[3], n[4], n[5], n[6], n[7], n[8], n[12] };
                yield return new int[] { n[13], n[14], n[9], n[10], n[11] };
            }
            else if (element is ParabolicHexaElement)
            {
                yield return new int[] { n[0], n[1], n[2], n[3], n[4], n[5], n[6], n[7], n[8], n[9] };
                yield return new int[] { n[10], n[11], n[16], n[17], n[18], n[19], n[12], n[13], n[14], n[15] };
            }
            else yield return n;
        }

        private static string ToFrdComponentName(string fieldName, string componentName)
        {
            if (fieldName.Equals(FOFieldNames.Disp, StringComparison.OrdinalIgnoreCase))
            {
                if (componentName == FOComponentNames.U1) return "D1";
                if (componentName == FOComponentNames.U2) return "D2";
                if (componentName == FOComponentNames.U3) return "D3";
            }
            else if (fieldName.Equals(FOFieldNames.Stress, StringComparison.OrdinalIgnoreCase))
            {
                if (componentName == FOComponentNames.S11) return "SXX";
                if (componentName == FOComponentNames.S22) return "SYY";
                if (componentName == FOComponentNames.S33) return "SZZ";
                if (componentName == FOComponentNames.S12) return "SXY";
                if (componentName == FOComponentNames.S23) return "SYZ";
                if (componentName == FOComponentNames.S13) return "SZX";
            }
            else if (fieldName.Equals(FOFieldNames.ToStrain, StringComparison.OrdinalIgnoreCase))
            {
                if (componentName == FOComponentNames.E11) return "EXX";
                if (componentName == FOComponentNames.E22) return "EYY";
                if (componentName == FOComponentNames.E33) return "EZZ";
                if (componentName == FOComponentNames.E12) return "EXY";
                if (componentName == FOComponentNames.E23) return "EYZ";
                if (componentName == FOComponentNames.E13) return "EZX";
            }
            return componentName;
        }

        private static string FormatFloat(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
            return value.ToString("0.00000E+00", CultureInfo.InvariantCulture).PadLeft(12);
        }
    }
}
