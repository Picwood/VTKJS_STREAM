// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace CaeModel.CFD.BaramFlow
{
    [Serializable]
    public enum BaramFlowSection
    {
        Case,
        General,
        Models,
        Materials,
        CellZoneConditions,
        BoundaryConditions,
        DynamicMesh,
        ReferenceValues,
        NumericalConditions,
        Monitors,
        Initialization,
        RunConditions,
        Run,
        ReducedOrderModel,
        Scaffolds,
        Graphics,
        Reports
    }

    [Serializable]
    public class BaramFlowProject
    {
        public const string StateFileName = "prepomax-baramflow.xml";

        public string ProjectDirectory { get; private set; }
        public string CaseDirectory { get; private set; }
        public string PolyMeshDirectory { get; private set; }
        public BaramFlowSection CurrentSection { get; set; }
        [OptionalField] private BaramFlowGeneralSettings _general;
        [OptionalField] private BaramFlowModelsSettings _models;
        [OptionalField] private BaramFlowMaterialSettings _material;
        [OptionalField] private BaramFlowBoundarySettings _boundarySettings;
        [OptionalField] private BaramFlowNumericalSettings _numerical;
        [OptionalField] private BaramFlowRunSettings _runConditions;

        public BaramFlowGeneralSettings General
        {
            get
            {
                if (_general == null) _general = new BaramFlowGeneralSettings();
                return _general;
            }
        }
        public BaramFlowModelsSettings Models
        {
            get
            {
                if (_models == null) _models = new BaramFlowModelsSettings();
                return _models;
            }
        }
        public BaramFlowMaterialSettings Material
        {
            get
            {
                if (_material == null) _material = new BaramFlowMaterialSettings();
                return _material;
            }
        }
        public BaramFlowBoundarySettings BoundaryConditions
        {
            get
            {
                if (_boundarySettings == null) _boundarySettings = new BaramFlowBoundaryParser().Parse(CaseDirectory);
                return _boundarySettings;
            }
        }
        public BaramFlowNumericalSettings NumericalConditions
        {
            get
            {
                if (_numerical == null) _numerical = new BaramFlowNumericalSettings();
                return _numerical;
            }
        }
        public BaramFlowRunSettings RunConditions
        {
            get
            {
                if (_runConditions == null) _runConditions = new BaramFlowRunSettings();
                return _runConditions;
            }
        }

        private BaramFlowProject()
        {
        }

        public static BaramFlowProject Open(string projectDirectory)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory))
                throw new ArgumentException("The BaramFlow project directory is required.", "projectDirectory");
            //
            projectDirectory = Path.GetFullPath(projectDirectory);
            if (!Directory.Exists(projectDirectory))
                throw new InvalidOperationException("The BaramFlow project directory does not exist: " + projectDirectory);
            if (!projectDirectory.EndsWith(".bf", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select a BaramFlow project directory with the .bf extension.");
            //
            string caseDirectory = Path.Combine(projectDirectory, "case");
            string polyMeshDirectory = Path.Combine(caseDirectory, "constant", "polyMesh");
            ValidatePolyMesh(polyMeshDirectory);
            //
            BaramFlowProject project = new BaramFlowProject
            {
                ProjectDirectory = projectDirectory,
                CaseDirectory = caseDirectory,
                PolyMeshDirectory = polyMeshDirectory,
                CurrentSection = BaramFlowSection.General,
                _general = new BaramFlowGeneralSettings(),
                _models = new BaramFlowModelsSettings(),
                _material = new BaramFlowMaterialSettings(),
                _boundarySettings = new BaramFlowBoundaryParser().Parse(caseDirectory),
                _numerical = new BaramFlowNumericalSettings(),
                _runConditions = new BaramFlowRunSettings()
            };
            project.LoadState();
            return project;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ProjectDirectory) ||
                string.IsNullOrWhiteSpace(CaseDirectory) ||
                string.IsNullOrWhiteSpace(PolyMeshDirectory))
                throw new InvalidOperationException("The BaramFlow project paths are incomplete.");
            ValidatePolyMesh(PolyMeshDirectory);
            General.Validate();
            Models.Validate(General);
            Material.Validate(General);
            BoundaryConditions.Validate();
            NumericalConditions.Validate(General, Models);
            RunConditions.Validate(General);
        }

        public void SetGeneral(BaramFlowGeneralSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            BaramFlowGeneralSettings copy = settings.Copy();
            copy.Normalize();
            copy.Validate();
            BaramFlowModelsSettings models = Models.Copy();
            models.Normalize(copy);
            models.Validate(copy);
            BaramFlowMaterialSettings material = Material.Copy();
            material.Normalize(copy);
            material.Validate(copy);
            BaramFlowRunSettings runConditions = RunConditions.Copy();
            runConditions.Normalize(copy);
            runConditions.Validate(copy);
            _general = copy;
            _models = models;
            _material = material;
            _runConditions = runConditions;
            CurrentSection = BaramFlowSection.General;
            SaveState();
        }

        public void SetModels(BaramFlowModelsSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            BaramFlowModelsSettings copy = settings.Copy();
            copy.Normalize(General);
            copy.Validate(General);
            _models = copy;
            CurrentSection = BaramFlowSection.Models;
            SaveState();
        }

        public void SetMaterial(BaramFlowMaterialSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            BaramFlowMaterialSettings copy = settings.Copy();
            copy.Normalize(General);
            copy.Validate(General);
            _material = copy;
            CurrentSection = BaramFlowSection.Materials;
            SaveState();
        }

        public void SetBoundaryConditions(BaramFlowBoundarySettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            _boundarySettings = new BaramFlowBoundaryParser().Parse(CaseDirectory, settings);
            CurrentSection = BaramFlowSection.BoundaryConditions;
            SaveState();
        }

        public void RefreshBoundaryConditions()
        {
            _boundarySettings = new BaramFlowBoundaryParser().Parse(CaseDirectory, _boundarySettings);
        }

        public void SetNumericalConditions(BaramFlowNumericalSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            BaramFlowNumericalSettings copy = settings.Copy();
            copy.Validate(General, Models);
            _numerical = copy;
            CurrentSection = BaramFlowSection.NumericalConditions;
            SaveState();
        }

        public void SetRunConditions(BaramFlowRunSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            BaramFlowRunSettings copy = settings.Copy();
            copy.Normalize(General);
            copy.Validate(General);
            _runConditions = copy;
            CurrentSection = BaramFlowSection.RunConditions;
            SaveState();
        }

        public static void ValidatePolyMesh(string polyMeshDirectory)
        {
            if (string.IsNullOrWhiteSpace(polyMeshDirectory) ||
                !File.Exists(Path.Combine(polyMeshDirectory, "points")) ||
                !File.Exists(Path.Combine(polyMeshDirectory, "faces")) ||
                !File.Exists(Path.Combine(polyMeshDirectory, "owner")) ||
                !File.Exists(Path.Combine(polyMeshDirectory, "boundary")))
                throw new InvalidOperationException("The BaramFlow project does not contain a complete case/constant/polyMesh.");
        }

        private void LoadState()
        {
            string fileName = Path.Combine(ProjectDirectory, StateFileName);
            if (!File.Exists(fileName)) return;
            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(fileName);
                XmlElement general = document.SelectSingleNode("/baramFlowProject/general") as XmlElement;
                if (general != null)
                {
                    BaramFlowGeneralSettings settings = new BaramFlowGeneralSettings();
                    settings.SolverType = ParseEnum(general.GetAttribute("solverType"), settings.SolverType);
                    settings.FlowType = ParseEnum(general.GetAttribute("flowType"), settings.FlowType);
                    settings.TimeMode = ParseEnum(general.GetAttribute("timeMode"), settings.TimeMode);
                    XmlElement gravity = general.SelectSingleNode("gravity") as XmlElement;
                    if (gravity != null)
                    {
                        settings.GravityX = ParseDouble(gravity.GetAttribute("x"), settings.GravityX);
                        settings.GravityY = ParseDouble(gravity.GetAttribute("y"), settings.GravityY);
                        settings.GravityZ = ParseDouble(gravity.GetAttribute("z"), settings.GravityZ);
                    }
                    XmlElement operating = general.SelectSingleNode("operatingConditions") as XmlElement;
                    if (operating != null)
                        settings.OperatingPressure =
                            ParseDouble(operating.GetAttribute("pressure"), settings.OperatingPressure);
                    settings.Normalize();
                    settings.Validate();
                    _general = settings;
                }
                LoadModels(document.SelectSingleNode("/baramFlowProject/models") as XmlElement);
                LoadMaterial(document.SelectSingleNode("/baramFlowProject/material") as XmlElement);
                LoadBoundaryConditions(document.SelectSingleNode("/baramFlowProject/boundaryConditions") as XmlElement);
                LoadNumericalConditions(document.SelectSingleNode("/baramFlowProject/numericalConditions") as XmlElement);
                LoadRunConditions(document.SelectSingleNode("/baramFlowProject/runConditions") as XmlElement);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("The BaramFlow project state file is invalid: " + fileName, ex);
            }
        }

        private void SaveState()
        {
            Validate();
            string fileName = Path.Combine(ProjectDirectory, StateFileName);
            XmlWriterSettings writerSettings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false
            };
            using (XmlWriter writer = XmlWriter.Create(fileName, writerSettings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("baramFlowProject");
                writer.WriteAttributeString("version", "6");
                writer.WriteStartElement("general");
                writer.WriteAttributeString("solverType", General.SolverType.ToString());
                writer.WriteAttributeString("flowType", General.FlowType.ToString());
                writer.WriteAttributeString("timeMode", General.TimeMode.ToString());
                writer.WriteStartElement("gravity");
                writer.WriteAttributeString("x", FormatDouble(General.GravityX));
                writer.WriteAttributeString("y", FormatDouble(General.GravityY));
                writer.WriteAttributeString("z", FormatDouble(General.GravityZ));
                writer.WriteEndElement();
                writer.WriteStartElement("operatingConditions");
                writer.WriteAttributeString("pressure", FormatDouble(General.OperatingPressure));
                writer.WriteEndElement();
                writer.WriteEndElement();
                WriteModels(writer);
                WriteMaterial(writer);
                WriteBoundaryConditions(writer);
                WriteNumericalConditions(writer);
                WriteRunConditions(writer);
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private void LoadModels(XmlElement element)
        {
            if (element == null) return;
            BaramFlowModelsSettings settings = new BaramFlowModelsSettings();
            settings.EnergyEnabled = ParseBool(element.GetAttribute("energyEnabled"), settings.EnergyEnabled);
            settings.TurbulenceModel = ParseEnum(element.GetAttribute("turbulenceModel"), settings.TurbulenceModel);
            XmlElement kEpsilon = element.SelectSingleNode("kEpsilon") as XmlElement;
            if (kEpsilon != null)
            {
                settings.KEpsilonModel = ParseEnum(kEpsilon.GetAttribute("model"), settings.KEpsilonModel);
                settings.NearWallTreatment = ParseEnum(kEpsilon.GetAttribute("nearWallTreatment"), settings.NearWallTreatment);
                settings.EnhancedWallThreshold = ParseDouble(kEpsilon.GetAttribute("threshold"), settings.EnhancedWallThreshold);
                settings.EnhancedWallBlendingWidth = ParseDouble(kEpsilon.GetAttribute("blendingWidth"), settings.EnhancedWallBlendingWidth);
            }
            XmlElement transport = element.SelectSingleNode("transport") as XmlElement;
            if (transport != null)
            {
                settings.EnergyPrandtlNumber = ParseDouble(transport.GetAttribute("energyPrandtl"), settings.EnergyPrandtlNumber);
                settings.WallPrandtlNumber = ParseDouble(transport.GetAttribute("wallPrandtl"), settings.WallPrandtlNumber);
                settings.TurbulentSchmidtNumber = ParseDouble(transport.GetAttribute("turbulentSchmidt"), settings.TurbulentSchmidtNumber);
            }
            XmlElement des = element.SelectSingleNode("des") as XmlElement;
            if (des != null)
            {
                settings.DesRansModel = ParseEnum(des.GetAttribute("ransModel"), settings.DesRansModel);
                settings.DesLowReDamping = ParseBool(des.GetAttribute("lowReDamping"), settings.DesLowReDamping);
                settings.DelayedDes = ParseBool(des.GetAttribute("delayed"), settings.DelayedDes);
                settings.ShieldingFunction = ParseEnum(des.GetAttribute("shielding"), settings.ShieldingFunction);
                settings.DesLengthScale = ParseEnum(des.GetAttribute("lengthScale"), settings.DesLengthScale);
                settings.DesCoefficient = ParseDouble(des.GetAttribute("coefficient"), settings.DesCoefficient);
                settings.DesKOmegaCoefficient = ParseDouble(des.GetAttribute("kOmegaCoefficient"), settings.DesKOmegaCoefficient);
                settings.DesKEpsilonCoefficient = ParseDouble(des.GetAttribute("kEpsilonCoefficient"), settings.DesKEpsilonCoefficient);
            }
            XmlElement les = element.SelectSingleNode("les") as XmlElement;
            if (les != null)
            {
                settings.LesSubgridScaleModel = ParseEnum(les.GetAttribute("subgridScale"), settings.LesSubgridScaleModel);
                settings.LesLengthScale = ParseEnum(les.GetAttribute("lengthScale"), settings.LesLengthScale);
                settings.LesCk = ParseDouble(les.GetAttribute("ck"), settings.LesCk);
                settings.LesCe = ParseDouble(les.GetAttribute("ce"), settings.LesCe);
                settings.LesCw = ParseDouble(les.GetAttribute("cw"), settings.LesCw);
            }
            settings.Normalize(General);
            settings.Validate(General);
            _models = settings;
        }

        private void WriteModels(XmlWriter writer)
        {
            writer.WriteStartElement("models");
            writer.WriteAttributeString("energyEnabled", Models.EnergyEnabled.ToString().ToLowerInvariant());
            writer.WriteAttributeString("turbulenceModel", Models.TurbulenceModel.ToString());
            writer.WriteStartElement("kEpsilon");
            writer.WriteAttributeString("model", Models.KEpsilonModel.ToString());
            writer.WriteAttributeString("nearWallTreatment", Models.NearWallTreatment.ToString());
            writer.WriteAttributeString("threshold", FormatDouble(Models.EnhancedWallThreshold));
            writer.WriteAttributeString("blendingWidth", FormatDouble(Models.EnhancedWallBlendingWidth));
            writer.WriteEndElement();
            writer.WriteStartElement("transport");
            writer.WriteAttributeString("energyPrandtl", FormatDouble(Models.EnergyPrandtlNumber));
            writer.WriteAttributeString("wallPrandtl", FormatDouble(Models.WallPrandtlNumber));
            writer.WriteAttributeString("turbulentSchmidt", FormatDouble(Models.TurbulentSchmidtNumber));
            writer.WriteEndElement();
            writer.WriteStartElement("des");
            writer.WriteAttributeString("ransModel", Models.DesRansModel.ToString());
            writer.WriteAttributeString("lowReDamping", Models.DesLowReDamping.ToString().ToLowerInvariant());
            writer.WriteAttributeString("delayed", Models.DelayedDes.ToString().ToLowerInvariant());
            writer.WriteAttributeString("shielding", Models.ShieldingFunction.ToString());
            writer.WriteAttributeString("lengthScale", Models.DesLengthScale.ToString());
            writer.WriteAttributeString("coefficient", FormatDouble(Models.DesCoefficient));
            writer.WriteAttributeString("kOmegaCoefficient", FormatDouble(Models.DesKOmegaCoefficient));
            writer.WriteAttributeString("kEpsilonCoefficient", FormatDouble(Models.DesKEpsilonCoefficient));
            writer.WriteEndElement();
            writer.WriteStartElement("les");
            writer.WriteAttributeString("subgridScale", Models.LesSubgridScaleModel.ToString());
            writer.WriteAttributeString("lengthScale", Models.LesLengthScale.ToString());
            writer.WriteAttributeString("ck", FormatDouble(Models.LesCk));
            writer.WriteAttributeString("ce", FormatDouble(Models.LesCe));
            writer.WriteAttributeString("cw", FormatDouble(Models.LesCw));
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private void LoadMaterial(XmlElement element)
        {
            BaramFlowMaterialSettings settings = new BaramFlowMaterialSettings();
            if (element != null)
            {
                string name = element.GetAttribute("name");
                if (!string.IsNullOrWhiteSpace(name)) settings.Name = name;
                settings.Phase = ParseEnum(element.GetAttribute("phase"), settings.Phase);
                settings.DensityModel = ParseEnum(element.GetAttribute("densityModel"), settings.DensityModel);
                settings.Density = ParseDouble(element.GetAttribute("density"), settings.Density);
                settings.DynamicViscosity =
                    ParseDouble(element.GetAttribute("dynamicViscosity"), settings.DynamicViscosity);
                settings.SpecificHeat = ParseDouble(element.GetAttribute("specificHeat"), settings.SpecificHeat);
                settings.ThermalConductivity =
                    ParseDouble(element.GetAttribute("thermalConductivity"), settings.ThermalConductivity);
                settings.MolecularWeight = ParseDouble(element.GetAttribute("molecularWeight"), settings.MolecularWeight);
            }
            settings.Normalize(General);
            settings.Validate(General);
            _material = settings;
        }

        private void LoadBoundaryConditions(XmlElement element)
        {
            if (element == null)
            {
                RefreshBoundaryConditions();
                return;
            }
            BaramFlowBoundarySettings settings = new BaramFlowBoundarySettings();
            settings.InitialGaugePressure = ParseDouble(element.GetAttribute("initialGaugePressure"), settings.InitialGaugePressure);
            settings.InitialVelocityX = ParseDouble(element.GetAttribute("initialVelocityX"), settings.InitialVelocityX);
            settings.InitialVelocityY = ParseDouble(element.GetAttribute("initialVelocityY"), settings.InitialVelocityY);
            settings.InitialVelocityZ = ParseDouble(element.GetAttribute("initialVelocityZ"), settings.InitialVelocityZ);
            settings.InitialTemperature = ParseDouble(element.GetAttribute("initialTemperature"), settings.InitialTemperature);
            settings.InitialTurbulenceIntensity =
                ParseDouble(element.GetAttribute("initialTurbulenceIntensity"), settings.InitialTurbulenceIntensity);
            settings.InitialTurbulentViscosityRatio =
                ParseDouble(element.GetAttribute("initialTurbulentViscosityRatio"), settings.InitialTurbulentViscosityRatio);
            System.Collections.Generic.List<BaramFlowBoundaryCondition> boundaries =
                new System.Collections.Generic.List<BaramFlowBoundaryCondition>();
            foreach (XmlElement child in element.SelectNodes("boundary"))
            {
                BaramFlowBoundaryCondition boundary = new BaramFlowBoundaryCondition();
                boundary.Name = child.GetAttribute("name");
                boundary.MeshType = child.GetAttribute("meshType");
                boundary.FaceCount = ParseInt(child.GetAttribute("faceCount"), boundary.FaceCount);
                boundary.StartFace = ParseInt(child.GetAttribute("startFace"), boundary.StartFace);
                boundary.BoundaryType = ParseEnum(child.GetAttribute("type"), boundary.BoundaryType);
                boundary.VelocityX = ParseDouble(child.GetAttribute("velocityX"), boundary.VelocityX);
                boundary.VelocityY = ParseDouble(child.GetAttribute("velocityY"), boundary.VelocityY);
                boundary.VelocityZ = ParseDouble(child.GetAttribute("velocityZ"), boundary.VelocityZ);
                boundary.GaugePressure = ParseDouble(child.GetAttribute("gaugePressure"), boundary.GaugePressure);
                boundary.Temperature = ParseDouble(child.GetAttribute("temperature"), boundary.Temperature);
                boundary.TurbulenceIntensity =
                    ParseDouble(child.GetAttribute("turbulenceIntensity"), boundary.TurbulenceIntensity);
                boundary.TurbulentViscosityRatio =
                    ParseDouble(child.GetAttribute("turbulentViscosityRatio"), boundary.TurbulentViscosityRatio);
                boundaries.Add(boundary);
            }
            settings.Boundaries = boundaries.ToArray();
            _boundarySettings = new BaramFlowBoundaryParser().Parse(CaseDirectory, settings);
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (_general == null) _general = new BaramFlowGeneralSettings();
            _general.Normalize();
            if (_models == null) _models = new BaramFlowModelsSettings();
            _models.Normalize(_general);
            if (_material == null) _material = new BaramFlowMaterialSettings();
            _material.Normalize(_general);
            if (_numerical == null) _numerical = new BaramFlowNumericalSettings();
            if (_runConditions == null) _runConditions = new BaramFlowRunSettings();
            _runConditions.Normalize(_general);
            if (!string.IsNullOrWhiteSpace(CaseDirectory) && Directory.Exists(CaseDirectory))
                _boundarySettings = new BaramFlowBoundaryParser().Parse(CaseDirectory, _boundarySettings);
        }

        private void LoadNumericalConditions(XmlElement element)
        {
            BaramFlowNumericalSettings settings = new BaramFlowNumericalSettings();
            if (element != null)
            {
                settings.PressureVelocityCoupling =
                    ParseEnum(element.GetAttribute("pressureVelocityCoupling"), settings.PressureVelocityCoupling);
                settings.UseMomentumPredictor =
                    ParseBool(element.GetAttribute("useMomentumPredictor"), settings.UseMomentumPredictor);
                XmlElement density = element.SelectSingleNode("densityBased") as XmlElement;
                if (density != null)
                {
                    settings.DensityFlux = ParseEnum(density.GetAttribute("flux"), settings.DensityFlux);
                    settings.EntropyFixCoefficient =
                        ParseDouble(density.GetAttribute("entropyFix"), settings.EntropyFixCoefficient);
                    settings.CutOffMachNumber = ParseDouble(density.GetAttribute("cutOffMach"), settings.CutOffMachNumber);
                }
                XmlElement discretization = element.SelectSingleNode("discretization") as XmlElement;
                if (discretization != null)
                {
                    settings.TimeDiscretization =
                        ParseEnum(discretization.GetAttribute("time"), settings.TimeDiscretization);
                    settings.PressureInterpolation =
                        ParseEnum(discretization.GetAttribute("pressure"), settings.PressureInterpolation);
                    settings.MomentumDiscretization =
                        ParseEnum(discretization.GetAttribute("momentum"), settings.MomentumDiscretization);
                    settings.EnergyDiscretization =
                        ParseEnum(discretization.GetAttribute("energy"), settings.EnergyDiscretization);
                    settings.TurbulenceDiscretization =
                        ParseEnum(discretization.GetAttribute("turbulence"), settings.TurbulenceDiscretization);
                    settings.HighOrderTermRelaxationEnabled =
                        ParseBool(discretization.GetAttribute("highOrderRelaxation"),
                                  settings.HighOrderTermRelaxationEnabled);
                    settings.HighOrderTermRelaxationFactor =
                        ParseDouble(discretization.GetAttribute("highOrderFactor"),
                                    settings.HighOrderTermRelaxationFactor);
                }
                XmlElement algorithms = element.SelectSingleNode("algorithms") as XmlElement;
                if (algorithms != null)
                {
                    settings.MaximumIterationsPerTimeStep =
                        ParseInt(algorithms.GetAttribute("maxIterationsPerTimeStep"),
                                 settings.MaximumIterationsPerTimeStep);
                    settings.NumberOfCorrectors =
                        ParseInt(algorithms.GetAttribute("correctors"), settings.NumberOfCorrectors);
                    settings.NumberOfNonOrthogonalCorrectors =
                        ParseInt(algorithms.GetAttribute("nonOrthogonalCorrectors"),
                                 settings.NumberOfNonOrthogonalCorrectors);
                }
                XmlElement relaxation = element.SelectSingleNode("relaxation") as XmlElement;
                if (relaxation != null)
                {
                    settings.PressureRelaxation = ParseDouble(relaxation.GetAttribute("pressure"), settings.PressureRelaxation);
                    settings.PressureFinalRelaxation = ParseDouble(relaxation.GetAttribute("pressureFinal"), settings.PressureFinalRelaxation);
                    settings.DensityRelaxation = ParseDouble(relaxation.GetAttribute("density"), settings.DensityRelaxation);
                    settings.DensityFinalRelaxation = ParseDouble(relaxation.GetAttribute("densityFinal"), settings.DensityFinalRelaxation);
                    settings.MomentumRelaxation = ParseDouble(relaxation.GetAttribute("momentum"), settings.MomentumRelaxation);
                    settings.MomentumFinalRelaxation = ParseDouble(relaxation.GetAttribute("momentumFinal"), settings.MomentumFinalRelaxation);
                    settings.EnergyRelaxation = ParseDouble(relaxation.GetAttribute("energy"), settings.EnergyRelaxation);
                    settings.EnergyFinalRelaxation = ParseDouble(relaxation.GetAttribute("energyFinal"), settings.EnergyFinalRelaxation);
                    settings.TurbulenceRelaxation = ParseDouble(relaxation.GetAttribute("turbulence"), settings.TurbulenceRelaxation);
                    settings.TurbulenceFinalRelaxation = ParseDouble(relaxation.GetAttribute("turbulenceFinal"), settings.TurbulenceFinalRelaxation);
                }
                XmlElement convergence = element.SelectSingleNode("convergence") as XmlElement;
                if (convergence != null)
                {
                    double[] values = LoadTolerance(convergence, "pressure", settings.PressureAbsoluteTolerance,
                                                    settings.PressureRelativeTolerance);
                    settings.PressureAbsoluteTolerance = values[0];
                    settings.PressureRelativeTolerance = values[1];
                    values = LoadTolerance(convergence, "density", settings.DensityAbsoluteTolerance,
                                           settings.DensityRelativeTolerance);
                    settings.DensityAbsoluteTolerance = values[0];
                    settings.DensityRelativeTolerance = values[1];
                    values = LoadTolerance(convergence, "momentum", settings.MomentumAbsoluteTolerance,
                                           settings.MomentumRelativeTolerance);
                    settings.MomentumAbsoluteTolerance = values[0];
                    settings.MomentumRelativeTolerance = values[1];
                    values = LoadTolerance(convergence, "energy", settings.EnergyAbsoluteTolerance,
                                           settings.EnergyRelativeTolerance);
                    settings.EnergyAbsoluteTolerance = values[0];
                    settings.EnergyRelativeTolerance = values[1];
                    values = LoadTolerance(convergence, "turbulence", settings.TurbulenceAbsoluteTolerance,
                                           settings.TurbulenceRelativeTolerance);
                    settings.TurbulenceAbsoluteTolerance = values[0];
                    settings.TurbulenceRelativeTolerance = values[1];
                }
            }
            settings.Validate(General, Models);
            _numerical = settings;
        }

        private void WriteNumericalConditions(XmlWriter writer)
        {
            BaramFlowNumericalSettings settings = NumericalConditions;
            writer.WriteStartElement("numericalConditions");
            writer.WriteAttributeString("pressureVelocityCoupling", settings.PressureVelocityCoupling.ToString());
            writer.WriteAttributeString("useMomentumPredictor", settings.UseMomentumPredictor.ToString().ToLowerInvariant());
            writer.WriteStartElement("densityBased");
            writer.WriteAttributeString("flux", settings.DensityFlux.ToString());
            writer.WriteAttributeString("entropyFix", FormatDouble(settings.EntropyFixCoefficient));
            writer.WriteAttributeString("cutOffMach", FormatDouble(settings.CutOffMachNumber));
            writer.WriteEndElement();
            writer.WriteStartElement("discretization");
            writer.WriteAttributeString("time", settings.TimeDiscretization.ToString());
            writer.WriteAttributeString("pressure", settings.PressureInterpolation.ToString());
            writer.WriteAttributeString("momentum", settings.MomentumDiscretization.ToString());
            writer.WriteAttributeString("energy", settings.EnergyDiscretization.ToString());
            writer.WriteAttributeString("turbulence", settings.TurbulenceDiscretization.ToString());
            writer.WriteAttributeString("highOrderRelaxation", settings.HighOrderTermRelaxationEnabled.ToString().ToLowerInvariant());
            writer.WriteAttributeString("highOrderFactor", FormatDouble(settings.HighOrderTermRelaxationFactor));
            writer.WriteEndElement();
            writer.WriteStartElement("algorithms");
            writer.WriteAttributeString("maxIterationsPerTimeStep", settings.MaximumIterationsPerTimeStep.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("correctors", settings.NumberOfCorrectors.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("nonOrthogonalCorrectors", settings.NumberOfNonOrthogonalCorrectors.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            writer.WriteStartElement("relaxation");
            WriteAttribute(writer, "pressure", settings.PressureRelaxation);
            WriteAttribute(writer, "pressureFinal", settings.PressureFinalRelaxation);
            WriteAttribute(writer, "density", settings.DensityRelaxation);
            WriteAttribute(writer, "densityFinal", settings.DensityFinalRelaxation);
            WriteAttribute(writer, "momentum", settings.MomentumRelaxation);
            WriteAttribute(writer, "momentumFinal", settings.MomentumFinalRelaxation);
            WriteAttribute(writer, "energy", settings.EnergyRelaxation);
            WriteAttribute(writer, "energyFinal", settings.EnergyFinalRelaxation);
            WriteAttribute(writer, "turbulence", settings.TurbulenceRelaxation);
            WriteAttribute(writer, "turbulenceFinal", settings.TurbulenceFinalRelaxation);
            writer.WriteEndElement();
            writer.WriteStartElement("convergence");
            WriteTolerance(writer, "pressure", settings.PressureAbsoluteTolerance, settings.PressureRelativeTolerance);
            WriteTolerance(writer, "density", settings.DensityAbsoluteTolerance, settings.DensityRelativeTolerance);
            WriteTolerance(writer, "momentum", settings.MomentumAbsoluteTolerance, settings.MomentumRelativeTolerance);
            WriteTolerance(writer, "energy", settings.EnergyAbsoluteTolerance, settings.EnergyRelativeTolerance);
            WriteTolerance(writer, "turbulence", settings.TurbulenceAbsoluteTolerance, settings.TurbulenceRelativeTolerance);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private void LoadRunConditions(XmlElement element)
        {
            BaramFlowRunSettings settings = new BaramFlowRunSettings();
            if (element != null)
            {
                settings.NumberOfIterations = ParseInt(element.GetAttribute("numberOfIterations"), settings.NumberOfIterations);
                settings.TimeSteppingMethod = ParseEnum(element.GetAttribute("timeSteppingMethod"), settings.TimeSteppingMethod);
                settings.TimeStepSize = ParseDouble(element.GetAttribute("timeStepSize"), settings.TimeStepSize);
                settings.MaximumCourantNumber = ParseDouble(element.GetAttribute("maximumCourantNumber"), settings.MaximumCourantNumber);
                settings.MaximumDiffusionNumber = ParseDouble(element.GetAttribute("maximumDiffusionNumber"), settings.MaximumDiffusionNumber);
                settings.EndTime = ParseDouble(element.GetAttribute("endTime"), settings.EndTime);
                settings.ReportIntervalSteps = ParseInt(element.GetAttribute("reportIntervalSteps"), settings.ReportIntervalSteps);
                settings.ReportIntervalSeconds = ParseDouble(element.GetAttribute("reportIntervalSeconds"), settings.ReportIntervalSeconds);
                settings.RetainOnlyMostRecentFiles = ParseBool(element.GetAttribute("retainOnlyMostRecentFiles"), settings.RetainOnlyMostRecentFiles);
                settings.MaximumNumberOfDataFiles = ParseInt(element.GetAttribute("maximumNumberOfDataFiles"), settings.MaximumNumberOfDataFiles);
                settings.DataWriteFormat = ParseEnum(element.GetAttribute("dataWriteFormat"), settings.DataWriteFormat);
                settings.DataWritePrecision = ParseInt(element.GetAttribute("dataWritePrecision"), settings.DataWritePrecision);
                settings.TimePrecision = ParseInt(element.GetAttribute("timePrecision"), settings.TimePrecision);
            }
            settings.Normalize(General);
            settings.Validate(General);
            _runConditions = settings;
        }

        private void WriteRunConditions(XmlWriter writer)
        {
            BaramFlowRunSettings settings = RunConditions;
            writer.WriteStartElement("runConditions");
            writer.WriteAttributeString("numberOfIterations", settings.NumberOfIterations.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("timeSteppingMethod", settings.TimeSteppingMethod.ToString());
            writer.WriteAttributeString("timeStepSize", FormatDouble(settings.TimeStepSize));
            writer.WriteAttributeString("maximumCourantNumber", FormatDouble(settings.MaximumCourantNumber));
            writer.WriteAttributeString("maximumDiffusionNumber", FormatDouble(settings.MaximumDiffusionNumber));
            writer.WriteAttributeString("endTime", FormatDouble(settings.EndTime));
            writer.WriteAttributeString("reportIntervalSteps", settings.ReportIntervalSteps.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("reportIntervalSeconds", FormatDouble(settings.ReportIntervalSeconds));
            writer.WriteAttributeString("retainOnlyMostRecentFiles", settings.RetainOnlyMostRecentFiles.ToString().ToLowerInvariant());
            writer.WriteAttributeString("maximumNumberOfDataFiles", settings.MaximumNumberOfDataFiles.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("dataWriteFormat", settings.DataWriteFormat.ToString());
            writer.WriteAttributeString("dataWritePrecision", settings.DataWritePrecision.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("timePrecision", settings.TimePrecision.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private static double[] LoadTolerance(XmlElement parent, string name, double absolute, double relative)
        {
            XmlElement element = parent.SelectSingleNode(name) as XmlElement;
            if (element == null) return new double[] { absolute, relative };
            absolute = ParseDouble(element.GetAttribute("absolute"), absolute);
            relative = ParseDouble(element.GetAttribute("relative"), relative);
            return new double[] { absolute, relative };
        }

        private static void WriteTolerance(XmlWriter writer, string name, double absolute, double relative)
        {
            writer.WriteStartElement(name);
            WriteAttribute(writer, "absolute", absolute);
            WriteAttribute(writer, "relative", relative);
            writer.WriteEndElement();
        }

        private static void WriteAttribute(XmlWriter writer, string name, double value)
        {
            writer.WriteAttributeString(name, FormatDouble(value));
        }

        private void WriteMaterial(XmlWriter writer)
        {
            writer.WriteStartElement("material");
            writer.WriteAttributeString("name", Material.Name);
            writer.WriteAttributeString("phase", Material.Phase.ToString());
            writer.WriteAttributeString("densityModel", Material.DensityModel.ToString());
            writer.WriteAttributeString("density", FormatDouble(Material.Density));
            writer.WriteAttributeString("dynamicViscosity", FormatDouble(Material.DynamicViscosity));
            writer.WriteAttributeString("specificHeat", FormatDouble(Material.SpecificHeat));
            writer.WriteAttributeString("thermalConductivity", FormatDouble(Material.ThermalConductivity));
            writer.WriteAttributeString("molecularWeight", FormatDouble(Material.MolecularWeight));
            writer.WriteEndElement();
        }

        private void WriteBoundaryConditions(XmlWriter writer)
        {
            writer.WriteStartElement("boundaryConditions");
            writer.WriteAttributeString("initialGaugePressure", FormatDouble(BoundaryConditions.InitialGaugePressure));
            writer.WriteAttributeString("initialVelocityX", FormatDouble(BoundaryConditions.InitialVelocityX));
            writer.WriteAttributeString("initialVelocityY", FormatDouble(BoundaryConditions.InitialVelocityY));
            writer.WriteAttributeString("initialVelocityZ", FormatDouble(BoundaryConditions.InitialVelocityZ));
            writer.WriteAttributeString("initialTemperature", FormatDouble(BoundaryConditions.InitialTemperature));
            writer.WriteAttributeString("initialTurbulenceIntensity", FormatDouble(BoundaryConditions.InitialTurbulenceIntensity));
            writer.WriteAttributeString("initialTurbulentViscosityRatio",
                                        FormatDouble(BoundaryConditions.InitialTurbulentViscosityRatio));
            foreach (BaramFlowBoundaryCondition boundary in BoundaryConditions.Boundaries)
            {
                writer.WriteStartElement("boundary");
                writer.WriteAttributeString("name", boundary.Name);
                writer.WriteAttributeString("meshType", boundary.MeshType);
                writer.WriteAttributeString("faceCount", boundary.FaceCount.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("startFace", boundary.StartFace.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("type", boundary.BoundaryType.ToString());
                writer.WriteAttributeString("velocityX", FormatDouble(boundary.VelocityX));
                writer.WriteAttributeString("velocityY", FormatDouble(boundary.VelocityY));
                writer.WriteAttributeString("velocityZ", FormatDouble(boundary.VelocityZ));
                writer.WriteAttributeString("gaugePressure", FormatDouble(boundary.GaugePressure));
                writer.WriteAttributeString("temperature", FormatDouble(boundary.Temperature));
                writer.WriteAttributeString("turbulenceIntensity", FormatDouble(boundary.TurbulenceIntensity));
                writer.WriteAttributeString("turbulentViscosityRatio",
                                            FormatDouble(boundary.TurbulentViscosityRatio));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static T ParseEnum<T>(string text, T defaultValue) where T : struct
        {
            T value;
            return Enum.TryParse(text, true, out value) ? value : defaultValue;
        }

        private static bool ParseBool(string text, bool defaultValue)
        {
            bool value;
            return bool.TryParse(text, out value) ? value : defaultValue;
        }

        private static double ParseDouble(string text, double defaultValue)
        {
            double value;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
            return defaultValue;
        }

        private static int ParseInt(string text, int defaultValue)
        {
            int value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("G15", CultureInfo.InvariantCulture);
        }
    }
}
