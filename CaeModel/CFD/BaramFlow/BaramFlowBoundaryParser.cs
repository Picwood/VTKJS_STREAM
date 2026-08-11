// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CaeModel.CFD.BaramFlow
{
    public class BaramFlowBoundaryParser
    {
        public BaramFlowBoundarySettings Parse(string caseDirectory, BaramFlowBoundarySettings existing = null)
        {
            if (string.IsNullOrWhiteSpace(caseDirectory)) throw new ArgumentException("Case directory is required.", "caseDirectory");
            string constant = Path.Combine(caseDirectory, "constant");
            string boundaryFile = Path.Combine(constant, "polyMesh", "boundary");
            HashSet<string> fsiNames = ReadFsiNames(Path.Combine(constant, "fsiInterfaces"));
            List<PatchData> patches = ReadPatches(boundaryFile);
            Dictionary<string, BaramFlowBoundaryCondition> previous =
                new Dictionary<string, BaramFlowBoundaryCondition>(StringComparer.OrdinalIgnoreCase);
            BaramFlowBoundarySettings result = existing == null ? new BaramFlowBoundarySettings() : existing.Copy();
            if (result.Boundaries != null)
                foreach (BaramFlowBoundaryCondition boundary in result.Boundaries)
                    if (boundary != null && !previous.ContainsKey(boundary.Name)) previous.Add(boundary.Name, boundary);
            List<BaramFlowBoundaryCondition> boundaries = new List<BaramFlowBoundaryCondition>();
            foreach (PatchData patch in patches)
            {
                BaramFlowBoundaryCondition boundary;
                if (previous.TryGetValue(patch.Name, out boundary)) boundary = boundary.Copy();
                else boundary = CreateDefault(patch, fsiNames.Contains(patch.Name));
                boundary.Name = patch.Name;
                boundary.MeshType = patch.Type;
                boundary.FaceCount = patch.NFaces;
                boundary.StartFace = patch.StartFace;
                if (fsiNames.Contains(patch.Name)) boundary.BoundaryType = BaramFlowBoundaryType.FsiInterface;
                boundaries.Add(boundary);
            }
            result.Boundaries = boundaries.ToArray();
            result.Validate();
            return result;
        }

        private static BaramFlowBoundaryCondition CreateDefault(PatchData patch, bool fsi)
        {
            BaramFlowBoundaryCondition boundary = new BaramFlowBoundaryCondition
            {
                Name = patch.Name,
                MeshType = patch.Type,
                FaceCount = patch.NFaces,
                StartFace = patch.StartFace,
                BoundaryType = InferType(patch.Name, patch.Type, patch.PhysicalType, fsi)
            };
            return boundary;
        }

        private static BaramFlowBoundaryType InferType(string name, string meshType, string physicalType, bool fsi)
        {
            if (fsi) return BaramFlowBoundaryType.FsiInterface;
            string normalized = (name ?? "").ToLowerInvariant();
            string physical = (physicalType ?? "").ToLowerInvariant();
            if (HasRole(normalized, "fsi_interface") || HasRole(normalized, "fsiinterface"))
                return BaramFlowBoundaryType.FsiInterface;
            if (HasRole(normalized, "inlet") || physical.Contains("inlet")) return BaramFlowBoundaryType.VelocityInlet;
            if (HasRole(normalized, "outlet") || physical.Contains("outlet")) return BaramFlowBoundaryType.PressureOutlet;
            if (HasRole(normalized, "symmetry") || string.Equals(meshType, "symmetry", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(meshType, "symmetryPlane", StringComparison.OrdinalIgnoreCase)) return BaramFlowBoundaryType.Symmetry;
            return BaramFlowBoundaryType.Wall;
        }

        private static bool HasRole(string name, string role)
        {
            return name == role || name.StartsWith(role + "_", StringComparison.OrdinalIgnoreCase);
        }

        private static List<PatchData> ReadPatches(string fileName)
        {
            List<string> tokens = Tokenize(fileName);
            int index = FindListStart(tokens, "boundary");
            if (index < 0) throw new InvalidOperationException("The OpenFOAM boundary patch list is missing: " + fileName);
            List<PatchData> patches = new List<PatchData>();
            index++;
            while (index < tokens.Count && tokens[index] != ")")
            {
                string name = Unquote(tokens[index++]);
                if (index >= tokens.Count || tokens[index] != "{") continue;
                index++;
                PatchData patch = new PatchData { Name = name, Type = "patch", PhysicalType = "", NFaces = 0, StartFace = -1 };
                while (index < tokens.Count && tokens[index] != "}")
                {
                    string key = tokens[index++];
                    if (index >= tokens.Count) break;
                    if (key == "type") patch.Type = Unquote(tokens[index++]);
                    else if (key == "physicalType") patch.PhysicalType = Unquote(tokens[index++]);
                    else if (key == "nFaces") patch.NFaces = ReadInt(tokens, ref index);
                    else if (key == "startFace") patch.StartFace = ReadInt(tokens, ref index);
                    else SkipValue(tokens, ref index);
                    while (index < tokens.Count && tokens[index] != ";" && tokens[index] != "}") index++;
                    if (index < tokens.Count && tokens[index] == ";") index++;
                }
                if (index < tokens.Count && tokens[index] == "}") index++;
                if (patch.StartFace < 0) throw new InvalidOperationException("Boundary patch " + name + " has no valid startFace.");
                patches.Add(patch);
            }
            if (patches.Count == 0) throw new InvalidOperationException("The OpenFOAM mesh contains no boundary patches.");
            return patches;
        }

        private static HashSet<string> ReadFsiNames(string fileName)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(fileName)) return names;
            List<string> tokens = Tokenize(fileName);
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                if (tokens[i] != "interfaces" || tokens[i + 1] != "(") continue;
                i += 2;
                while (i < tokens.Count && tokens[i] != ")")
                {
                    string name = Unquote(tokens[i++]);
                    if (name != ";") names.Add(name);
                }
                break;
            }
            return names;
        }

        private static void SkipValue(List<string> tokens, ref int index)
        {
            int depth = 0;
            while (index < tokens.Count)
            {
                string token = tokens[index];
                if (token == "(" || token == "{") depth++;
                else if (token == ")" || token == "}")
                {
                    if (depth == 0) return;
                    depth--;
                }
                else if (token == ";" && depth == 0) return;
                index++;
            }
        }

        private static int FindListStart(List<string> tokens, string objectName)
        {
            int start = 0;
            for (int i = 0; i < tokens.Count - 2; i++)
                if (tokens[i] == "object" && Unquote(tokens[i + 1]) == objectName)
                {
                    for (int j = i + 2; j < tokens.Count; j++) if (tokens[j] == "}") { start = j + 1; break; }
                    break;
                }
            for (int i = start; i < tokens.Count - 1; i++)
            {
                int count;
                if (int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) && tokens[i + 1] == "(")
                    return i + 1;
            }
            return -1;
        }

        private static int ReadInt(List<string> tokens, ref int index)
        {
            int value;
            if (index >= tokens.Count || !int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                throw new InvalidOperationException("Invalid integer in the OpenFOAM boundary file.");
            index++;
            return value;
        }

        private static List<string> Tokenize(string fileName)
        {
            if (!File.Exists(fileName)) throw new InvalidOperationException("The OpenFOAM file does not exist: " + fileName);
            string text = File.ReadAllText(fileName);
            List<string> tokens = new List<string>();
            StringBuilder token = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!quoted && c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    Flush(tokens, token); i += 2; while (i < text.Length && text[i] != '\r' && text[i] != '\n') i++; continue;
                }
                if (!quoted && c == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    Flush(tokens, token); i += 2; while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++; i++; continue;
                }
                if (c == '"') { token.Append(c); quoted = !quoted; continue; }
                if (!quoted && char.IsWhiteSpace(c)) { Flush(tokens, token); continue; }
                if (!quoted && (c == '(' || c == ')' || c == '{' || c == '}' || c == ';'))
                { Flush(tokens, token); tokens.Add(c.ToString()); continue; }
                token.Append(c);
            }
            Flush(tokens, token);
            return tokens;
        }

        private static void Flush(List<string> tokens, StringBuilder token)
        {
            if (token.Length == 0) return;
            tokens.Add(token.ToString());
            token.Length = 0;
        }

        private static string Unquote(string value)
        {
            if (!string.IsNullOrEmpty(value) && value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2);
            return value;
        }

        private class PatchData
        {
            public string Name;
            public string Type;
            public string PhysicalType;
            public int NFaces;
            public int StartFace;
        }
    }
}
