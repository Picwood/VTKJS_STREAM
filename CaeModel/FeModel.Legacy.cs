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
using CaeMesh;
using CaeGlobals;
using System.Runtime.Serialization;
using Calculix = FileInOut.Output.Calculix;
using System.IO;
using System.Drawing;
using System.Data;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using static System.Collections.Specialized.BitVector32;
using System.Net.NetworkInformation;
using System.Xml.Linq;
using FileInOut.Input;
using CaeResults;
using System.CodeDom;
using System.Collections.Concurrent;

namespace CaeModel
{
    public partial class FeModel
    {
        static partial void ReportWarning(string message) => MessageBoxes.ShowWarning(message);
        static partial void ReportParameterError(ref bool handled)
        {
            MessageBoxes.ShowError("The parameters could not be evaluated. Check the parameter equations.");
            handled = true;
        }
        public FeModel(FeModel model)
        {
            FeModel copy = model.DeepCopy();
            _name = copy._name;
            _hashName = copy._hashName;
            _geometry = copy._geometry;
            _mesh = copy._mesh;
            _userViews = copy._userViews;
            _parameters = copy._parameters;
            _cylinders = copy._cylinders;
            _materials = copy._materials;
            _sections = copy._sections;
            _constraints = copy._constraints;
            _surfaceInteractions = copy._surfaceInteractions;
            _contactPairs = copy._contactPairs;
            _distributions = copy._distributions;
            _amplitudes = copy._amplitudes;
            _initialConditions = copy._initialConditions;
            _stepCollection = copy._stepCollection;
            _calculixUserKeywords = copy._calculixUserKeywords;
            _properties = copy._properties;
            _unitSystem = copy._unitSystem;
        }
        public void UpdateUserKeywordStates(bool remove, out int suppresedCount, out int unsuppresedCount)
        {
            FileInOut.Output.CalculixFileWriter.UpdateUserKeywordStates(this, remove, out suppresedCount, out unsuppresedCount);
        }
        public string[] ImportGeometryFromBrepFile(string visFileName, string brepFileName)
        {
            FeMesh mesh = VisFileReader.Read(visFileName);
            //
            if (mesh.Parts.GetValueByIndex(0) is GeometryPart gp)
            {
                gp.CADFileDataFromFile(brepFileName);
                gp.SmoothShaded = true;
            }
            //
            string[] addedPartNames = ImportGeometry(mesh, GetReservedPartNames());
            //
            return addedPartNames;
        }
        public void ImportMeshFromVolFile(string fileName)
        {
            FeMesh mesh = VolFileReader.Read(fileName, ElementsToImport.Shell | ElementsToImport.Solid);
            //
            ImportMesh(mesh, GetReservedPartNames(), true, false);
        }
        public void ImportGeneratedMeshFromMeshFile(string fileName, BasePart part, bool convertToSecondOrder,
                                                    bool splitCompoundMesh, bool mergeCompoundParts)
        {
            ElementsToImport elementsToImport;
            GeometryPart subPart;
            if (part.PartType == PartType.SolidAsShell) elementsToImport = ElementsToImport.Solid;
            else if (part.PartType == PartType.Shell) elementsToImport = ElementsToImport.Shell;
            else if (part.PartType == PartType.Compound)
            {
                subPart = _geometry.Parts[(part as CompoundGeometryPart).SubPartNames[0]] as GeometryPart;
                if (subPart.PartType == PartType.SolidAsShell) elementsToImport = ElementsToImport.Solid;
                else if (subPart.PartType == PartType.Shell) elementsToImport = ElementsToImport.Shell;
                else throw new NotSupportedException();
            }
            else throw new NotSupportedException();
            // Get part names - determine, if one part or multiple parts are needed
            string[] prevPartNames;
            if (part is CompoundGeometryPart cgp)
            {
                if (mergeCompoundParts) prevPartNames = new string[] { part.Name };
                else prevPartNames = cgp.SubPartNames.ToArray();
            }
            else prevPartNames = new string[] { part.Name };
            // Called after meshing in PrePoMax - the parts are sorted by id
            FeMesh mesh;
            if (Path.GetExtension(fileName) == ".vol")
                mesh = VolFileReader.Read(fileName, elementsToImport, convertToSecondOrder);
            else if (Path.GetExtension(fileName) == ".mesh")
                mesh = MmgFileReader.Read(fileName, elementsToImport, MeshRepresentation.Mesh, convertToSecondOrder);
            else if (Path.GetExtension(fileName) == ".inp")
                mesh = InpFileReader.ReadMesh(fileName, elementsToImport, convertToSecondOrder);
            else throw new NotSupportedException();
            // Split compound mesh
            if (splitCompoundMesh) mesh.SplitSolidCompoundMesh();
            // Merge parts if only one part is expected
            string[] partNames = mesh.Parts.Keys.ToArray();
            if (prevPartNames.Length != partNames.Length)
            {
                if (prevPartNames.Length == 1 && partNames.Length > 1)
                {
                    mesh.MergeMeshParts(mesh.Parts.Keys.ToArray(), out _, out _);
                    partNames = mesh.Parts.Keys.ToArray();
                }
                else throw new NotSupportedException();
            }
            // Rename the imported part/s
            BasePart[] importedParts = new BasePart[partNames.Length];
            for (int i = 0; i < partNames.Length; i++)
            {
                importedParts[i] = mesh.Parts[partNames[i]];
                importedParts[i].Name = prevPartNames[i];
            }
            mesh.Parts.Clear();
            for (int i = 0; i < partNames.Length; i++) mesh.Parts.Add(importedParts[i].Name, importedParts[i]);
            //
            ImportMesh(mesh, null, false);
            // Recolor parts at the end of the import
            foreach (var importedPart in importedParts)
            {
                if (_geometry.Parts.ContainsKey(importedPart.Name) && _mesh.Parts.ContainsKey(importedPart.Name))
                    _mesh.Parts[importedPart.Name].Color = _geometry.Parts[importedPart.Name].Color;
            }
        }
        public void ImportGeneratedRemeshFromMeshFile(string fileName, int[] elementIds, BasePart part,
                                                      bool convertToSecondOrder, Dictionary<(int, int), FeNode> midNodes)
        {
            // Remove elements from the mesh
            HashSet<int> possiblyUnrefNodeIds = new HashSet<int>();
            foreach (var elId in elementIds) possiblyUnrefNodeIds.UnionWith(_mesh.Elements[elId].NodeIds);  // contains midside nodes
            HashSet<int> removedNodeIds =
                _mesh.RemoveElementsByIds(new HashSet<int>(elementIds), possiblyUnrefNodeIds, false, false, true);
            HashSet<int> borderNodeIds = new HashSet<int>(possiblyUnrefNodeIds.Except(removedNodeIds));     // contains midside nodes
            Dictionary<int, FeNode> borderNodes = new Dictionary<int, FeNode>();
            foreach (var ndId in borderNodeIds) borderNodes.Add(ndId, _mesh.Nodes[ndId]);                   // contains midside nodes
            HashSet<int> remainingNodeIds = new HashSet<int>(_mesh.Nodes.Keys.Except(removedNodeIds));
            // Read the mmg file and renumber nodes and elements
            double epsilon = 1E-6;
            double max = part.BoundingBox.GetDiagonal();
            Dictionary<string, Dictionary<int, int>> partNameNewSurfIdOldSurfId = new Dictionary<string, Dictionary<int, int>>();
            Dictionary<string, Dictionary<int, int>> partNameNewEdgeIdOldEdgeId = new Dictionary<string, Dictionary<int, int>>();
            FeMesh mmgMesh = MmgFileReader.Read(fileName, ElementsToImport.Shell,
                                                                MeshRepresentation.Mesh, convertToSecondOrder,
                                                                _mesh.MaxNodeId + 1, _mesh.MaxElementId + 1,
                                                                borderNodes, midNodes,
                                                                epsilon * max,
                                                                partNameNewSurfIdOldSurfId, partNameNewEdgeIdOldEdgeId);
            // Get surface nodes before modification
            Dictionary<int, HashSet<int>> surfaceIdNodeIds = part.Visualization.GetSurfaceIdNodeIds();
            foreach (var entry in surfaceIdNodeIds) entry.Value.ExceptWith(removedNodeIds);
            // Get edge nodes before modification
            Dictionary<int, HashSet<int>> edgeIdNodeIds = part.Visualization.GetEdgeIdNodeIds();
            foreach (var entry in edgeIdNodeIds) entry.Value.ExceptWith(removedNodeIds);
            // Add elements to mesh                                                                                     
            FeElement element;
            HashSet<Type> elementTypes = new HashSet<Type>();
            foreach (var entry in mmgMesh.Elements)
            {
                element = entry.Value;
                element.PartId = part.PartId;
                elementTypes.Add(element.GetType());
                _mesh.Elements.Add(entry.Key, element);
            }
            // Add elements to part
            HashSet<int> newPartElementIds = new HashSet<int>(part.Labels);
            newPartElementIds.UnionWith(mmgMesh.Elements.Keys);
            part.Labels = newPartElementIds.ToArray();
            Array.Sort(part.Labels);
            // Add element types to part
            if (part is MeshPart mp) mp.AddElementTypes(elementTypes.ToArray());
            // Add nodes to mesh                                                                                        
            foreach (var entry in mmgMesh.Nodes)
            {
                if (!_mesh.Nodes.ContainsKey(entry.Key)) _mesh.Nodes.Add(entry.Key, entry.Value);
            }
            // Add nodes to part
            HashSet<int> newPartNodeIds = new HashSet<int>(part.NodeLabels);
            newPartNodeIds.UnionWith(mmgMesh.Nodes.Keys);
            part.NodeLabels = newPartNodeIds.ToArray();
            Array.Sort(part.NodeLabels);
            // Update node ids
            _mesh.UpdateMaxNodeAndElementIds();
            _mesh.UpdateNodeIdElementIds();
            // Model vertices                                                                                           
            HashSet<int> vertexNodeIds = new HashSet<int>();
            // Get vertices from part - only those that were not removed
            foreach (var nodeId in part.Visualization.VertexNodeIds)
            {
                if (!removedNodeIds.Contains(nodeId)) vertexNodeIds.Add(nodeId);
            }
            // Get vertices from mmgPart
            foreach (var entry in mmgMesh.Parts) vertexNodeIds.UnionWith(entry.Value.Visualization.VertexNodeIds);
            // Model edges                                                                                              
            int elementId = mmgMesh.MaxElementId + 1;
            LinearBeamElement beamElement;
            List<FeElement1D> edgeElements = new List<FeElement1D>();
            // Get model edges from part - only edges that are not completely removed
            int[] key;
            CompareIntArray comparer = new CompareIntArray();
            HashSet<int[]> edgeKeys = new HashSet<int[]>(comparer);
            //
            foreach (var edgeCell in part.Visualization.EdgeCells)
            {
                if (remainingNodeIds.Contains(edgeCell[0]) && remainingNodeIds.Contains(edgeCell[1]))
                {
                    key = Tools.GetSortedKey(edgeCell[0], edgeCell[1]);
                    if (!edgeKeys.Contains(key))
                    {
                        beamElement = new LinearBeamElement(elementId++, key);
                        edgeElements.Add(beamElement);
                        edgeKeys.Add(key);
                    }
                }
            }
            // Get model edges from mmgPart
            foreach (var entry in mmgMesh.Parts)
            {
                foreach (var edgeCell in entry.Value.Visualization.EdgeCells)
                {
                    key = Tools.GetSortedKey(edgeCell[0], edgeCell[1]);
                    if (!edgeKeys.Contains(key))
                    {
                        beamElement = new LinearBeamElement(elementId++, key);
                        edgeElements.Add(beamElement);
                        edgeKeys.Add(key);
                    }
                }
            }
            // Add edge elements to mesh
            foreach (var edgeElement in edgeElements) _mesh.Elements.Add(edgeElement.Id, edgeElement);
            // Extract visualization                                                                                    
            _mesh.ExtractShellPartVisualization(part, false, -1);
            //
            _mesh.ConvertLineFeElementsToEdges(vertexNodeIds, false, part.Name);
            // Renumber surfaces                                                                                        
            BasePart mmgPart;
            Dictionary<int, HashSet<int>> itemIdNodeIds;
            foreach (var partNewSurfIdOldSurfId in partNameNewSurfIdOldSurfId)
            {
                mmgPart = mmgMesh.Parts[partNewSurfIdOldSurfId.Key];
                itemIdNodeIds = mmgPart.Visualization.GetSurfaceIdNodeIds();
                //
                foreach (var newSurfIdOldSurfId in partNewSurfIdOldSurfId.Value)
                {
                    surfaceIdNodeIds[newSurfIdOldSurfId.Value].UnionWith(itemIdNodeIds[newSurfIdOldSurfId.Key]);
                    surfaceIdNodeIds[newSurfIdOldSurfId.Value].IntersectWith(part.NodeLabels);
                }
            }
            _mesh.RenumberVisualizationSurfaces(part, surfaceIdNodeIds);
            // Renumber edges                                                                                           
            foreach (var partNewEdgeIdOldEdgeId in partNameNewEdgeIdOldEdgeId)
            {
                if (mmgMesh.Parts.TryGetValue(partNewEdgeIdOldEdgeId.Key, out mmgPart))
                {
                    itemIdNodeIds = mmgPart.Visualization.GetEdgeIdNodeIds();
                    //
                    foreach (var newEdgeIdOldEdgeId in partNewEdgeIdOldEdgeId.Value)
                    {
                        edgeIdNodeIds[newEdgeIdOldEdgeId.Value].UnionWith(itemIdNodeIds[newEdgeIdOldEdgeId.Key]);
                        edgeIdNodeIds[newEdgeIdOldEdgeId.Value].IntersectWith(part.NodeLabels);
                    }
                }
            }
            _mesh.RenumberPartVisualizationEdges(part, edgeIdNodeIds);
            //
            _mesh.RemoveElementsByType<FeElement1D>();
            //
            _mesh.ComputeVolumeArea(part);
            //
            UpdateMeshPartsElementTypes(false);
        }
        public List<string> ImportModelFromInpFile(string fileName, Action<string> WriteDataToOutput)
        {
            CaeGlobals.OrderedDictionary<int[], Calculix.CalculixUserKeyword> indexedUserKeywords;
            InpFileReader.Read(fileName,
                               ElementsToImport.Solid | ElementsToImport.Shell, 
                               this,
                               WriteDataToOutput,
                               out indexedUserKeywords);
            //
            CalculixUserKeywords = indexedUserKeywords;
            //
            return InpFileReader.Errors;
        }
        public List<string> ImportMaterialsFromInpFile(string fileName, Action<string> WriteDataToOutput)
        {
            FeModel model = new FeModel("Imported Materials", _unitSystem, false);
            model.Properties.ModelSpace = _properties.ModelSpace;
            // Add existing model materials to account for indexing of material user keywords
            string existingMaterialName;
            foreach (var entry in _materials)
            {
                existingMaterialName = "_internal_for_indices_" + entry.Key;
                model.Materials.Add(existingMaterialName, entry.Value.DeepClone());
            }
            List<string> existingMaterialNames = model.Materials.Keys.ToList();
            //
            InpFileReader.Read(fileName,
                               ElementsToImport.Solid | ElementsToImport.Shell,
                               model,
                               WriteDataToOutput,
                               out CaeGlobals.OrderedDictionary<int[], Calculix.CalculixUserKeyword>  indexedUserKeywords);
            //
            CalculixUserKeywords = indexedUserKeywords;
            // Remove existing model materials
            foreach (var materialName in existingMaterialNames) model.Materials.Remove(materialName);
            //
            string name;
            foreach (var entry in model.Materials)
            {
                name = entry.Key;
                if (_materials.ContainsKey(name))
                {
                    name += "_Imported";
                    if (_materials.ContainsKey(name))
                    {
                        name = _materials.GetNextNumberedKey(name);
                    }
                }
                entry.Value.Name = name;
                _materials.Add(name, entry.Value);
            }
            //
            return InpFileReader.Errors;
        }
        public void ImportMeshFromUnvFile(string fileName)
        {
            FeMesh mesh = UnvFileReader.Read(fileName, ElementsToImport.Shell | ElementsToImport.Solid);
            //
            ImportMesh(mesh, GetReservedPartNames(), true, false);
        }
        public void ImportMeshFromObjFile(string fileName)
        {
            FeMesh mesh = ObjFileReader.Read(fileName);
            //
            ImportMesh(mesh, GetReservedPartNames(), true, false);
        }
        public void UpdateGeometryPartMassPropertiesFromVisFile(string visFileName, string partName)
        {
            FeMesh mesh = VisFileReader.Read(visFileName);
            //
            if (mesh.Parts.Count == 1)
            {
                BasePart part = _geometry.Parts[partName];
                BasePart updatedPart = mesh.Parts.First().Value;
                //part.Visualization = updatedPart.Visualization;
                part.MassProperties = updatedPart.MassProperties;
            }
        }
        public FeModel PrepareBdmModel(Dictionary<int, double[]> deformations)
        {
            // Mesh
            FeModel bdmModel = new FeModel("BDMmodel", _unitSystem, false);
            bdmModel.Properties = _properties;
            bdmModel.SetMesh(_mesh.DeepCopy());
            // Materials
            Material materialElastic = new Material("Elastic");
            materialElastic.AddProperty(new Elastic(new double[][] { new double[] { 1000, 0, 0 } }));
            bdmModel._materials.Add(materialElastic.Name, materialElastic);
            // Sections
            Section section;
            foreach (var entry in _sections)
            {
                section = entry.Value.DeepClone();
                section.MaterialName = materialElastic.Name;
                bdmModel.Sections.Add(section.Name, section);
            }
            // Constraints
            HashSet<int> constrainedNodeIds = new HashSet<int>();
            Constraint constraint;
            foreach (var entry in _constraints)
            {
                constraint = entry.Value.DeepClone();
                bdmModel.Constraints.Add(constraint.Name, constraint);
                //
                if (constraint is RigidBody rb)
                {
                    if (rb.SlaveRegionType == RegionTypeEnum.Selection)
                        constrainedNodeIds.UnionWith(rb.SlaveCreationIds);
                    else if (rb.SlaveRegionType == RegionTypeEnum.NodeSetName)
                        constrainedNodeIds.UnionWith(_mesh.NodeSets[rb.SlaveRegionName].Labels);
                    else if (rb.SlaveRegionType == RegionTypeEnum.SurfaceName)
                        constrainedNodeIds.UnionWith(_mesh.NodeSets[_mesh.Surfaces[rb.SlaveRegionName].NodeSetName].Labels);
                }
            }
            // Distributions
            Distribution distribution;
            foreach (var entry in _distributions)
            {
                distribution = entry.Value.DeepClone();
                bdmModel.Distributions.Add(distribution.Name, distribution);
            }
            // Amplitudes
            Amplitude amplitude;
            foreach (var entry in _amplitudes)
            {
                amplitude = entry.Value.DeepClone();
                bdmModel.Amplitudes.Add(amplitude.Name, amplitude);
            }
            // Steps
            StaticStep staticStep = new StaticStep("Step-1");
            bdmModel._stepCollection.AddStep(staticStep, false);
            BoundaryDisplacementStep boundaryDisplacementStep = _stepCollection.GetBoundaryDisplacementStep();
            if (boundaryDisplacementStep == null)
                throw new CaeException("The boundary displacement step is missing.");
            // Add existing boundary conditions
            bool twoD = false;
            BoundaryCondition bc;
            foreach (var entry in boundaryDisplacementStep.BoundaryConditions)
            {
                bc = entry.Value.DeepClone();
                staticStep.AddBoundaryCondition(bc);
                twoD = bc.TwoD;
            }
            // Create BDM boundary conditions
            string name;
            double[] xyz;
            FeNodeSet nodeSet;
            DisplacementRotation displacementRotation;
            //
            if (deformations != null)
            {
                foreach (var entry in deformations)
                {
                    if (!constrainedNodeIds.Contains(entry.Key))
                    {
                        // Node set
                        name = bdmModel.Mesh.NodeSets.GetNextNumberedKey("BDM", "_" + entry.Key);
                        nodeSet = new FeNodeSet(name, new int[] { entry.Key });
                        bdmModel.Mesh.NodeSets.Add(nodeSet.Name, nodeSet);
                        // Boundary condition
                        xyz = entry.Value;
                        name = staticStep.BoundaryConditions.GetNextNumberedKey("BDM-" + entry.Key);
                        displacementRotation = new DisplacementRotation(name, nodeSet.Name, RegionTypeEnum.NodeSetName, twoD,
                                                                        false, 0);
                        if (xyz[0] != 0) displacementRotation.U1.SetEquationFromValue(xyz[0]);
                        if (xyz[1] != 0) displacementRotation.U2.SetEquationFromValue(xyz[1]);
                        if (xyz[2] != 0) displacementRotation.U3.SetEquationFromValue(xyz[2]);
                        staticStep.AddBoundaryCondition(displacementRotation);
                    }
                }
            }
            //
            return bdmModel;
        }
        public FeModel DeepCopy()
        {
            try
            {
                FeModel model = this.DeepClone();
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
                            ReadFromBinaryReader(model, br, -1);
                        }
                    }
                }
                //
                return model;
            }
            catch
            {
                return null;
            }
        }


    }
}
