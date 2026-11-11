using System.Formats.Nrbf;
using System.Text;
using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class LegacyPmxConverter
{
    const int MaxCount = 10_000_000;

    internal static FeModel Convert(ClassRecord source, BinaryReader reader, Version version)
    {
        if (source.TypeName.FullName != "CaeModel.FeModel")
            throw new InvalidDataException("Unsupported legacy model record.");
        var unitRecord = Record(source, "_unitSystem");
        UnitSystemType units = EnumValue<UnitSystemType>(Record(unitRecord, "_unitSystemType"));
        var model = new FeModel(Text(source, "_name"), new UnitSystem(units), false);
        var properties = Record(source, "_properties");
        model.Properties.ModelSpace = EnumValue<ModelSpaceEnum>(Record(properties, "ModelSpace"));
        model.Properties.ModelWorkflow = HasField(properties, "ModelWorkflow")
            ? EnumValue<ModelWorkflow>(Record(properties, "ModelWorkflow"))
            : version <= new Version(2, 4, 0) ? ModelWorkflow.FEA
            : throw new InvalidDataException("Missing model workflow.");
        model.Properties.ModelType = EnumValue<ModelType>(Record(properties, "ModelType"));
        model.Properties.AbsoluteZero = properties.GetDouble(Field(properties, "AbsoluteZero"));
        model.Properties.StefanBoltzmann = properties.GetDouble(Field(properties, "StefanBoltzmann"));
        model.Properties.NewtonGravity = properties.GetDouble(Field(properties, "NewtonGravity"));
        if (model.Properties.ModelSpace != ModelSpaceEnum.ThreeD || model.Properties.ModelWorkflow != ModelWorkflow.FEA ||
            model.Properties.ModelType != ModelType.GeneralModel)
            throw new NotSupportedException("Only general 3D FEA models are supported by this conversion slice.");
        ConvertMaterials(model, source);
        ConvertSections(model, source);
        ConvertAmplitudesAndCylinders(model, source);
        ConvertInteractionsAndContacts(model, source);
        ConvertConstraints(model, source);

        RequireEmpty(source, "_initialConditions", "initial conditions");
        RequireEmpty(source, "_distributions", "distributions");
        RequireEmpty(source, "_userViews", "user views");
        ConvertSteps(model, source);
        ConvertUserKeywords(model, source);
        var geometry = RecordOrNull(source, "_geometry");
        if (geometry != null && ActiveModelInventory.DictionaryItems(RecordOrNull(geometry, "_parts")).Any())
            throw new NotSupportedException("Legacy geometry parts are outside the mesh-model conversion slice.");

        int geometryPresent = reader.ReadInt32();
        if (geometryPresent == 1)
        {
            if (Count(reader) != 0 || Count(reader) != 0 || Count(reader, nullable: true) != 0)
                throw new NotSupportedException("Nonempty legacy geometry binary data is outside the conversion slice.");
        }
        else if (geometryPresent != 0) throw new InvalidDataException("Invalid geometry mesh presence flag.");
        var meshMetadata = Record(source, "_mesh");
        model.SetMesh(ReadMesh(reader, version, meshMetadata));
        if (reader.BaseStream.ReadByte() != -1) throw new InvalidDataException("Unexpected trailing model binary data.");
        ConvertCoordinateSystems(model.Mesh, meshMetadata);
        ConvertReferencePoints(model.Mesh, meshMetadata);
        return model;
    }

    static void ConvertUserKeywords(FeModel model, ClassRecord source)
    {
        var converted = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("CalculiX user keywords");
        foreach (var entry in UserKeywordInventory.Read(source))
        {
            var record = entry.Keyword;
            if (!Bool(record, "_active", true) || Bool(record, "_suppressed", false) ||
                ActiveModelInventory.ListItems(RecordOrNull(record, "_keywords")).Any())
                throw new NotSupportedException("Only active, unsuppressed leaf CalculiX user keywords are supported.");
            string data = Text(record, "_data");
            if (string.IsNullOrEmpty(data) || data.Contains('\0') || Encoding.UTF8.GetByteCount(data) > 1024 * 1024)
                throw new NotSupportedException("CalculiX user-keyword text is empty or exceeds the supported bound.");
            converted.Add(entry.Path.ToArray(), new CalculixUserKeyword(data));
        }
        model.CalculixUserKeywords = converted;
    }

    static void ConvertMaterials(FeModel model, ClassRecord source)
    {
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(source, "_materials")))
        {
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing material record.");
            if (record.TypeName.FullName != "CaeModel.Material") throw new NotSupportedException("Unsupported material type.");
            var material = new Material(Text(record, "_name"))
            {
                Active = Bool(record, "_active", true),
                Description = TextOrNull(record, "_description") ?? "",
                TemperatureDependent = Bool(record, "_temperatureDependent", false)
            };
            foreach (var property in ActiveModelInventory.ListItems(Record(record, "_properties")))
            {
                MaterialProperty converted = property.TypeName.FullName switch
                {
                    "CaeModel.Elastic" => new Elastic(EquationTable(property, "_youngsPoissonsTemp",
                        typeof(StringPressureConverter), typeof(StringDoubleConverter), typeof(StringTemperatureConverter))),
                    "CaeModel.Density" => new Density(EquationTable(property, "_densityTemp",
                        typeof(StringDensityConverter), typeof(StringTemperatureConverter))),
                    "CaeModel.Plastic" => new Plastic(EquationTable(property, "_stressStrainTemp",
                        typeof(StringPressureConverter), typeof(StringDoubleConverter), typeof(StringTemperatureConverter)))
                        { Hardening = EnumField<PlasticHardening>(property, "_hardening") },
                    "CaeModel.ThermalExpansion" => new ThermalExpansion(EquationTable(property, "_thermalExpansionTemp",
                        typeof(StringThermalExpansionConverter), typeof(StringTemperatureConverter)))
                        { ZeroTemperature = NumericEquation(property, "_zeroTemperature", typeof(StringTemperatureConverter)) },
                    "CaeModel.ThermalConductivity" => new ThermalConductivity(EquationTable(property, "_thermalConductivityTemp",
                        typeof(StringThermalConductivityConverter), typeof(StringTemperatureConverter))),
                    "CaeModel.SpecificHeat" => new SpecificHeat(EquationTable(property, "_specificHeatTemp",
                        typeof(StringSpecificHeatConverter), typeof(StringTemperatureConverter))),
                    _ => throw new NotSupportedException("Unsupported material property: " + property.TypeName.FullName)
                };
                material.AddProperty(converted);
            }
            model.Materials.Add(material.Name, material);
        }
    }

    static void ConvertSections(FeModel model, ClassRecord source)
    {
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(source, "_sections")))
        {
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing section record.");
            if (record.TypeName.FullName != "CaeModel.SolidSection") throw new NotSupportedException("Unsupported section type.");
            var section = new SolidSection(Text(record, "_name"), Text(record, "_materialName"), Text(record, "_regionName"),
                EnumField<RegionTypeEnum>(record, "_regionType"),
                EquationValue(Record(record, "_thickness"), typeof(StringLengthConverter)), false)
                { Active = Bool(record, "_active", true) };
            model.Sections.Add(section.Name, section);
        }
    }

    static void ConvertAmplitudesAndCylinders(FeModel model, ClassRecord source)
    {
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(source, "_amplitudes")))
        {
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing amplitude record.");
            if (record.TypeName.FullName != "CaeModel.AmplitudeTabular") throw new NotSupportedException("Unsupported amplitude type.");
            var amplitude = new AmplitudeTabular(Text(record, "_name"), DoubleTable(record, "_timeAmplitude", 2))
            {
                Active = Bool(record, "_active", true),
                TimeSpan = EnumField<AmplitudeTimeSpanEnum>(record, "_timeSpan"),
                ShiftX = record.GetDouble(Field(record, "_shiftX")), ShiftY = record.GetDouble(Field(record, "_shiftY"))
            };
            model.Amplitudes.Add(amplitude.Name, amplitude);
        }
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(source, "_cylinders")))
        {
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing cylinder record.");
            if (record.TypeName.FullName != "CaeModel.FeCylinder") throw new NotSupportedException("Unsupported cylinder type.");
            var cylinder = new FeCylinder(Text(record, "_name"))
            {
                Active = Bool(record, "_active", true), Center = DoubleArray(record, "_center", 3),
                Radius = record.GetDouble(Field(record, "_radius")), Direction = DoubleArray(record, "_direction", 3),
                Height = record.GetDouble(Field(record, "_height")), ExtraRadius = record.GetDouble(Field(record, "_extraRadius")),
                Chamfer = DoubleArray(record, "_chamfer", 2), TargetRadius = record.GetDouble(Field(record, "_targetRadius")),
                NumberOfLayers = record.GetInt32(Field(record, "_numberOfLayers")),
                Tolerance = record.GetDouble(Field(record, "_tolerance"))
            };
            if (!cylinder.IsProperlyDefined(out _)) throw new InvalidDataException("Invalid cylinder definition.");
            model.Cylinders.Add(cylinder.Name, cylinder);
        }
    }

    static void ConvertInteractionsAndContacts(FeModel model, ClassRecord source)
    {
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(source, "_surfaceInteractions")))
        {
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing interaction record.");
            if (record.TypeName.FullName != "CaeModel.SurfaceInteraction") throw new NotSupportedException("Unsupported interaction type.");
            var interaction = new SurfaceInteraction(Text(record, "_name")) { Active = Bool(record, "_active", true) };
            foreach (var property in ActiveModelInventory.ListItems(Record(record, "_properties")))
            {
                SurfaceInteractionProperty converted;
                if (property.TypeName.FullName == "CaeModel.SurfaceBehavior")
                {
                    converted = new SurfaceBehavior
                    {
                        PressureOverclosureType = EnumField<PressureOverclosureEnum>(property, "_pressureOverclosureType"),
                        C0 = property.GetDouble(Field(property, "_c0")), P0 = property.GetDouble(Field(property, "_p0")),
                        K = property.GetDouble(Field(property, "_k")), Sinf = property.GetDouble(Field(property, "_sInf")),
                        PressureOverclosure = DoubleTable(property, "_pressureOverclosure", 2)
                    };
                }
                else if (property.TypeName.FullName == "CaeModel.Friction")
                    converted = new Friction(property.GetDouble(Field(property, "_coefficient")), property.GetDouble(Field(property, "_stickSlope")));
                else if (property.TypeName.FullName == "CaeModel.GapConductance")
                    converted = new GapConductance(DoubleTable(property, "_conductnancePressureTemp", 3))
                        { GapConductanceType = EnumField<GapConductanceEnum>(property, "_gapConductanceType") };
                else throw new NotSupportedException("Unsupported surface-interaction property: " + property.TypeName.FullName);
                interaction.AddProperty(converted);
            }
            model.SurfaceInteractions.Add(interaction.Name, interaction);
        }
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(source, "_contactPairs")))
        {
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing contact-pair record.");
            if (record.TypeName.FullName != "CaeModel.ContactPair") throw new NotSupportedException("Unsupported contact-pair type.");
            var contact = new ContactPair(Text(record, "_name"), Text(record, "_surfaceInteractionName"),
                EnumField<ContactPairMethod>(record, "_method"), Bool(record, "_smallSliding", false), Bool(record, "_adjust", false),
                record.GetDouble(Field(record, "_adjustmentSize")), Text(record, "_masterSurfaceName"),
                EnumField<RegionTypeEnum>(record, "_masterRegionType"), Text(record, "_slaveSurfaceName"),
                EnumField<RegionTypeEnum>(record, "_slaveRegionType")) { Active = Bool(record, "_active", true) };
            model.ContactPairs.Add(contact.Name, contact);
        }
    }

    static void ConvertConstraints(FeModel model, ClassRecord source)
    {
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(source, "_constraints")))
        {
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing constraint record.");
            Constraint constraint = record.TypeName.FullName switch
            {
                "CaeModel.Tie" => new Tie(Text(record, "_name"),
                    EquationValue(Record(record, "_positionTolerance"), typeof(StringLengthDefaultConverter), true),
                    Bool(record, "_adjust", true), Text(record, "_masterRegionName"),
                    EnumField<RegionTypeEnum>(record, "_masterRegionType"), Text(record, "_slaveRegionName"),
                    EnumField<RegionTypeEnum>(record, "_slaveRegionType"), false),
                "CaeModel.RigidBody" => new RigidBody(Text(record, "_name"), Text(record, "_masterRegionName"),
                    Text(record, "_slaveRegionName"), EnumField<RegionTypeEnum>(record, "_slaveRegionType"), false),
                "CaeModel.SurfaceSpring" => ConvertSurfaceSpring(record),
                _ => throw new NotSupportedException("Unsupported constraint type: " + record.TypeName.FullName)
            };
            constraint.Active = Bool(record, "_active", true);
            model.Constraints.Add(constraint.Name, constraint);
        }
    }

    static SurfaceSpring ConvertSurfaceSpring(ClassRecord record)
    {
        bool perArea = Bool(record, "_stiffnessPerArea", false);
        Type converter = perArea ? typeof(StringForcePerVolumeConverter) : typeof(StringForcePerLengthConverter);
        var spring = new SurfaceSpring(Text(record, "_name"), Text(record, "_masterRegionName"),
            EnumField<RegionTypeEnum>(record, "_masterRegionType"), false, false) { StiffnessPerArea = perArea };
        spring.K1.SetEquationFromValue(EquationValue(Record(record, "_k1"), converter));
        spring.K2.SetEquationFromValue(EquationValue(Record(record, "_k2"), converter));
        spring.K3.SetEquationFromValue(EquationValue(Record(record, "_k3"), converter));
        return spring;
    }

    static void ConvertReferencePoints(FeMesh mesh, ClassRecord metadata)
    {
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(metadata, "_referencePoints")))
        {
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing reference-point record.");
            if (record.TypeName.FullName != "CaeMesh.FeReferencePoint") throw new NotSupportedException("Unsupported reference-point type.");
            var point = new FeReferencePoint(Text(record, "_name"),
                EquationValue(Record(record, "_x"), typeof(StringLengthConverter)),
                EquationValue(Record(record, "_y"), typeof(StringLengthConverter)),
                EquationValue(Record(record, "_z"), typeof(StringLengthConverter))) { Active = Bool(record, "_active", true) };
            mesh.ReferencePoints.Add(point.Name, point);
        }
    }

    static void ConvertCoordinateSystems(FeMesh mesh, ClassRecord metadata)
    {
        var records = ActiveModelInventory.DictionaryItems(RecordOrNull(metadata, "_coordinateSystems"))
            .Take(1001).ToArray();
        if (records.Length > 1000) throw new NotSupportedException("More than 1000 coordinate systems are not supported.");
        foreach (var record in records)
        {
            var coordinateSystem = ConvertCoordinateSystem(record);
            mesh.CoordinateSystems.Add(coordinateSystem.Name, coordinateSystem);
        }
    }

    internal static bool CanConvertCoordinateSystem(ClassRecord record)
    {
        try { _ = ConvertCoordinateSystem(record); return true; }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException or CaeException)
        { return false; }
    }

    static CoordinateSystem ConvertCoordinateSystem(ClassRecord record)
    {
        if (record.TypeName.FullName != "CaeMesh.CoordinateSystem" || Bool(record, "_twoD", false) ||
            EnumField<CsPointCreatedFromEnum>(record, "_centerCreatedFrom") != CsPointCreatedFromEnum.Coordinates ||
            EnumField<CsPointCreatedFromEnum>(record, "_pointXCreatedFrom") != CsPointCreatedFromEnum.Coordinates ||
            EnumField<CsPointCreatedFromEnum>(record, "_pointXYCreatedFrom") != CsPointCreatedFromEnum.Coordinates ||
            IntArrayOrNull(record, "_centerCreationIds") != null || RecordOrNull(record, "_centerCreationData") != null ||
            IntArrayOrNull(record, "_pointXCreationIds") != null || RecordOrNull(record, "_pointXCreationData") != null ||
            IntArrayOrNull(record, "_pointXYCreationIds") != null || RecordOrNull(record, "_pointXYCreationData") != null)
            throw new NotSupportedException("Only coordinate-defined 3D coordinate systems are supported.");
        var coordinateSystem = new CoordinateSystem(Text(record, "_name"), false)
        {
            Active = Bool(record, "_active", true),
            Type = EnumField<CoordinateSystemTypeEnum>(record, "_type"),
            X1 = NumericEquation(record, "_x1", typeof(StringLengthConverter)),
            Y1 = NumericEquation(record, "_y1", typeof(StringLengthConverter)),
            Z1 = NumericEquation(record, "_z1", typeof(StringLengthConverter)),
            X2 = NumericEquation(record, "_x2", typeof(StringLengthConverter)),
            Y2 = NumericEquation(record, "_y2", typeof(StringLengthConverter)),
            Z2 = NumericEquation(record, "_z2", typeof(StringLengthConverter)),
            X3 = NumericEquation(record, "_x3", typeof(StringLengthConverter)),
            Y3 = NumericEquation(record, "_y3", typeof(StringLengthConverter)),
            Z3 = NumericEquation(record, "_z3", typeof(StringLengthConverter))
        };
        if (!coordinateSystem.IsProperlyDefined(out _)) throw new InvalidDataException("Invalid coordinate-system definition.");
        return coordinateSystem;
    }

    static void ConvertSteps(FeModel model, ClassRecord source)
    {
        foreach (var record in ActiveModelInventory.ListItems(Record(Record(source, "_stepCollection"), "_steps")))
        {
            if (record.TypeName.FullName != "CaeModel.StaticStep") throw new NotSupportedException("Unsupported analysis step type.");
            if (ActiveModelInventory.DictionaryItems(RecordOrNull(record, "_definedFields")).Any())
                throw new NotSupportedException("Defined fields are outside the conversion contract.");
            var controls = RecordOrNull(record, "_stepControls");
            if (controls != null && ActiveModelInventory.ListItems(RecordOrNull(controls, "_parameters")).Any())
                throw new NotSupportedException("Custom step controls are outside the conversion contract.");
            var step = new StaticStep(Text(record, "_name"), false)
            {
                Active = Bool(record, "_active", true), RunAnalysis = Bool(record, "_runAnalysis", true),
                Perturbation = Bool(record, "_perturbation", false), Nlgeom = Bool(record, "_nlgeom", false),
                MaxIncrements = record.GetInt32(Field(record, "_maxIncrements")),
                IncrementationType = EnumField<IncrementationTypeEnum>(record, "_incrementationType"),
                SolverType = EnumField<SolverTypeEnum>(record, "_solverType"),
                OutputFrequency = record.GetInt32(Field(record, "_outputFrequency")),
                TimePeriod = record.GetDouble(Field(record, "_timePeriod")),
                InitialTimeIncrement = record.GetDouble(Field(record, "_initialTimeIncrement")),
                MinTimeIncrement = record.GetDouble(Field(record, "_minTimeIncrement")),
                MaxTimeIncrement = record.GetDouble(Field(record, "_maxTimeIncrement")), Direct = Bool(record, "_direct", false)
            };
            foreach (var item in ActiveModelInventory.DictionaryItems(RecordOrNull(record, "_boundaryConditions"))) step.AddBoundaryCondition(ConvertBoundary(item));
            foreach (var item in ActiveModelInventory.DictionaryItems(RecordOrNull(record, "_loads"))) step.AddLoad(ConvertLoad(item));
            foreach (var item in ActiveModelInventory.DictionaryItems(RecordOrNull(record, "_fieldOutputs"))) step.AddFieldOutput(ConvertField(item));
            foreach (var item in ActiveModelInventory.DictionaryItems(RecordOrNull(record, "_historyOutputs"))) step.AddHistoryOutput(ConvertHistory(item));
            model.StepCollection.AddStep(step);
        }
    }

    static BoundaryCondition ConvertBoundary(ClassRecord record)
    {
        string name = Text(record, "_name"), region = Text(record, "_regionName");
        RegionTypeEnum regionType = EnumField<RegionTypeEnum>(record, "_regionType");
        bool complex = Bool(record, "_complex", false);
        double phase = EquationValue(Record(record, "_phaseDeg"), typeof(StringAngleDegConverter));
        BoundaryCondition boundary;
        if (record.TypeName.FullName == "CaeModel.DisplacementRotation")
            boundary = new DisplacementRotation(name, region, regionType, false, complex, phase)
            {
                U1 = NumericEquation(record, "_u1", typeof(StringLengthDOFConverter), true),
                U2 = NumericEquation(record, "_u2", typeof(StringLengthDOFConverter), true),
                U3 = NumericEquation(record, "_u3", typeof(StringLengthDOFConverter), true),
                UR1 = NumericEquation(record, "_ur1", typeof(StringAngleDOFConverter), true),
                UR2 = NumericEquation(record, "_ur2", typeof(StringAngleDOFConverter), true),
                UR3 = NumericEquation(record, "_ur3", typeof(StringAngleDOFConverter), true)
            };
        else if (record.TypeName.FullName == "CaeModel.FixedBC") boundary = new FixedBC(name, region, regionType, false);
        else throw new NotSupportedException("Unsupported boundary condition: " + record.TypeName.FullName);
        boundary.Active = Bool(record, "_active", true);
        boundary.AmplitudeName = TextOrNull(record, "_amplitudeName");
        boundary.CoordinateSystemName = TextOrNull(record, "_coordinateSystemName");
        return boundary;
    }

    static Load ConvertLoad(ClassRecord record)
    {
        string name = Text(record, "_name"), region = Text(record, "_regionName");
        RegionTypeEnum regionType = EnumField<RegionTypeEnum>(record, "_regionType");
        bool complex = Bool(record, "_complex", false);
        double phase = EquationValue(Record(record, "_phaseDeg"), typeof(StringAngleDegConverter));
        Load load = record.TypeName.FullName switch
        {
            "CaeModel.CLoad" => new CLoad(name, region, regionType,
                EquationValue(Record(record, "_f1"), typeof(StringForceConverter)), EquationValue(Record(record, "_f2"), typeof(StringForceConverter)),
                EquationValue(Record(record, "_f3"), typeof(StringForceConverter)), false, complex, phase)
                { NodeId = record.GetInt32(Field(record, "_nodeId")) },
            "CaeModel.STLoad" => new STLoad(name, region, regionType,
                EquationValue(Record(record, "_f1"), typeof(StringForceConverter)), EquationValue(Record(record, "_f2"), typeof(StringForceConverter)),
                EquationValue(Record(record, "_f3"), typeof(StringForceConverter)), false, complex, phase)
                { DistributionName = TextOrNull(record, "_distributionName") },
            "CaeModel.GravityLoad" => new GravityLoad(name, region, regionType,
                EquationValue(Record(record, "_f1"), typeof(StringAccelerationConverter)), EquationValue(Record(record, "_f2"), typeof(StringAccelerationConverter)),
                EquationValue(Record(record, "_f3"), typeof(StringAccelerationConverter)), false, complex, phase),
            "CaeModel.CentrifLoad" => new CentrifLoad(name, region, regionType,
                new[] { EquationValue(Record(record, "_x"), typeof(StringLengthConverter)), EquationValue(Record(record, "_y"), typeof(StringLengthConverter)), EquationValue(Record(record, "_z"), typeof(StringLengthConverter)) },
                new[] { EquationValue(Record(record, "_n1"), typeof(StringLengthConverter)), EquationValue(Record(record, "_n2"), typeof(StringLengthConverter)), EquationValue(Record(record, "_n3"), typeof(StringLengthConverter)) },
                EquationValue(Record(record, "_rotationalSpeed"), typeof(StringRotationalSpeedConverter)), false,
                Bool(record, "_axisymmetric", false), complex, phase),
            _ => throw new NotSupportedException("Unsupported load: " + record.TypeName.FullName)
        };
        load.Active = Bool(record, "_active", true);
        load.AmplitudeName = TextOrNull(record, "_amplitudeName");
        load.CoordinateSystemName = TextOrNull(record, "_coordinateSystemName");
        return load;
    }

    static FieldOutput ConvertField(ClassRecord record)
    {
        FieldOutput output = record.TypeName.FullName switch
        {
            "CaeModel.NodalFieldOutput" => new NodalFieldOutput(Text(record, "_name"), EnumField<NodalFieldVariable>(record, "_variables")),
            "CaeModel.ElementFieldOutput" => new ElementFieldOutput(Text(record, "_name"), EnumField<ElementFieldVariable>(record, "_variables"))
                { Output = EnumField<ElementFieldOutputOutputEnum>(record, "_output") },
            "CaeModel.ContactFieldOutput" => new ContactFieldOutput(Text(record, "_name"), EnumField<ContactFieldVariable>(record, "_variables")),
            _ => throw new NotSupportedException("Unsupported field output: " + record.TypeName.FullName)
        };
        output.Active = Bool(record, "_active", true); output.LastIterations = Bool(record, "_lastIterations", false);
        output.ContactElements = Bool(record, "_contactElements", false); output.Global = Bool(record, "_global", true);
        return output;
    }

    static HistoryOutput ConvertHistory(ClassRecord record)
    {
        if (record.TypeName.FullName != "CaeModel.NodalHistoryOutput") throw new NotSupportedException("Unsupported history output: " + record.TypeName.FullName);
        return new NodalHistoryOutput(Text(record, "_name"), EnumField<NodalHistoryVariable>(record, "_variables"),
            Text(record, "_regionName"), EnumField<RegionTypeEnum>(record, "_regionType"))
        {
            Active = Bool(record, "_active", true), TotalsType = EnumField<TotalsTypeEnum>(record, "_totals"),
            Global = Bool(record, "_global", true)
        };
    }

    static FeMesh ReadMesh(BinaryReader reader, Version version, ClassRecord metadata)
    {
        if (reader.ReadInt32() != 1) throw new InvalidDataException("Missing analysis mesh binary data.");
        int count = Count(reader);
        var nodes = new Dictionary<int, FeNode>(count);
        for (int i = 0; i < count; i++)
        {
            int id = reader.ReadInt32();
            double x = reader.ReadDouble(), y = reader.ReadDouble(), z = reader.ReadDouble();
            if (id <= 0 || !nodes.TryAdd(id, new FeNode(id, Finite(x, "node coordinate"),
                    Finite(y, "node coordinate"), Finite(z, "node coordinate"))))
                throw new InvalidDataException("Invalid or duplicate mesh node.");
        }
        count = Count(reader);
        var elements = new Dictionary<int, FeElement>(count);
        for (int i = 0; i < count; i++)
        {
            int partId = reader.ReadInt32(), id = reader.ReadInt32(), cell = reader.ReadInt32(), width = Count(reader, 20);
            int expected = cell switch { 10 => 4, 12 => 8, 24 => 10, 26 => 15, 25 => 20, _ => -1 };
            if (partId <= 0 || id <= 0 || width != expected)
                throw new NotSupportedException("Only PMA-supported solid element topologies can be converted.");
            var labels = new int[width];
            for (int j = 0; j < width; j++)
            {
                labels[j] = reader.ReadInt32();
                if (!nodes.ContainsKey(labels[j])) throw new InvalidDataException("Element references a missing node.");
            }
            FeElement element = cell switch
            {
                10 => new LinearTetraElement(id, labels), 12 => new LinearHexaElement(id, labels),
                24 => new ParabolicTetraElement(id, labels), 26 => new ParabolicWedgeElement(id, labels),
                25 => new ParabolicHexaElement(id, labels),
                _ => throw new NotSupportedException()
            };
            element.PartId = partId;
            if (!elements.TryAdd(id, element)) throw new InvalidDataException("Duplicate mesh element ID.");
        }
        int visualizationParts = Count(reader, nullable: true);
        for (int i = 0; i < visualizationParts; i++)
        {
            if (version >= new Version(2, 4, 4))
            {
                int bytes = reader.Read7BitEncodedInt();
                if (bytes is < 0 or > 4096 || reader.ReadBytes(bytes).Length != bytes)
                    throw new InvalidDataException("Invalid visualization part name.");
            }
            SkipVisualization(reader);
        }
        var mesh = new FeMesh(nodes, elements, MeshRepresentation.Mesh);
        ApplyParts(mesh, metadata);
        ApplySetsAndSurfaces(mesh, metadata);
        if (ActiveModelInventory.DictionaryItems(RecordOrNull(metadata, "_meshSetupItems")).Any())
            throw new NotSupportedException("Mesh setup is outside this conversion slice.");
        return mesh;
    }

    static void ApplyParts(FeMesh mesh, ClassRecord metadata)
    {
        var generated = mesh.Parts.Values.Cast<MeshPart>().ToDictionary(part => part.PartId);
        var converted = new List<MeshPart>();
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(metadata, "_parts")))
        {
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing part record.");
            string name = Text(record, "_name");
            int id = record.GetInt32(Field(record, "_partId"));
            if (!generated.TryGetValue(id, out var part) || !Labels(record).ToHashSet().SetEquals(part.Labels))
                throw new InvalidDataException("Part metadata does not match binary mesh membership.");
            part.Name = name; part.Active = Bool(record, "_active", true);
            part.LinearTetraType = EnumField<FeElementTypeLinearTetra>(record, "_linearTetraType");
            part.LinearHexaType = EnumField<FeElementTypeLinearHexa>(record, "_linearHexaType");
            part.ParabolicTetraType = EnumField<FeElementTypeParabolicTetra>(record, "_parabolicTetraType");
            part.ParabolicWedgeType = EnumField<FeElementTypeParabolicWedge>(record, "_parabolicWedgeType");
            part.ParabolicHexaType = EnumField<FeElementTypeParabolicHexa>(record, "_parabolicHexaType");
            converted.Add(part);
        }
        if (converted.Count != generated.Count) throw new InvalidDataException("Part metadata is incomplete.");
        mesh.Parts.Clear();
        foreach (var part in converted) mesh.Parts.Add(part.Name, part);
    }

    static void ApplySetsAndSurfaces(FeMesh mesh, ClassRecord metadata)
    {
        mesh.NodeSets.Clear();
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(metadata, "_nodeSets")))
        {
            string name = pair.GetString("key") ?? throw new InvalidDataException("Missing node-set name.");
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing node-set record.");
            var set = new FeNodeSet(name, Labels(record)) { Active = Bool(record, "_active", true) };
            mesh.NodeSets.Add(name, set);
        }
        mesh.ElementSets.Clear();
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(metadata, "_elementSets")))
        {
            string name = pair.GetString("key") ?? throw new InvalidDataException("Missing element-set name.");
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing element-set record.");
            int[] labels = Labels(record).Distinct().ToArray();
            if (labels.Length == 0) continue;
            var set = new FeElementSet(name, labels, Bool(record, "_createdFromParts", false))
                { Active = Bool(record, "_active", true) };
            mesh.ElementSets.Add(name, set);
        }
        mesh.Surfaces.Clear();
        foreach (var pair in ActiveModelInventory.DictionaryPairs(RecordOrNull(metadata, "_surfaces")))
        {
            string name = pair.GetString("key") ?? throw new InvalidDataException("Missing surface name.");
            var record = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing surface record.");
            var surface = new FeSurface(name) { Active = Bool(record, "_active", true),
                CreatedFrom = EnumField<FeSurfaceCreatedFrom>(record, "_createdFrom"),
                Type = EnumField<FeSurfaceType>(record, "_type"),
                NodeSetName = TextOrNull(record, "_nodeSetName"),
                CreatedFromNodeSetName = TextOrNull(record, "_createdFromNodeSetName"),
                FaceIds = IntArrayOrNull(record, "_faceIds"), Area = record.GetDouble(Field(record, "_area")) };
            foreach (var face in ActiveModelInventory.DictionaryPairs(RecordOrNull(record, "_elementFaces")))
            {
                var key = face.GetClassRecord("key") ?? throw new InvalidDataException("Missing surface face.");
                surface.AddElementFace(EnumValue<FeFaceName>(key), face.GetString("value")
                    ?? throw new InvalidDataException("Missing surface element set."));
            }
            mesh.Surfaces.Add(name, surface);
        }
    }

    static void SkipVisualization(BinaryReader reader)
    {
        int present = reader.ReadInt32();
        if (present == -1) return;
        if (present != 1) throw new InvalidDataException("Invalid visualization flag.");
        SkipArray(reader, true); SkipArray(reader); SkipArray(reader, true); SkipArray(reader, doubles: true); SkipArray(reader);
        SkipArray(reader, true); SkipArray(reader, true); SkipArray(reader, true); SkipArray(reader, true);
        SkipArray(reader, doubles: true); SkipArray(reader); SkipArray(reader);
    }
    static void SkipArray(BinaryReader reader, bool jagged = false, bool doubles = false)
    {
        int count = Count(reader, nullable: true);
        for (int i = 0; i < count; i++)
            if (jagged) SkipArray(reader);
            else if (doubles) reader.ReadDouble();
            else reader.ReadInt32();
    }

    static int Count(BinaryReader reader, int maximum = MaxCount, bool nullable = false)
    {
        int count = reader.ReadInt32();
        if (count < (nullable ? -1 : 0) || count > maximum) throw new InvalidDataException("Mesh count exceeds conversion limits.");
        return count;
    }
    static void RequireEmpty(ClassRecord owner, string field, string description)
    {
        if (ActiveModelInventory.DictionaryItems(RecordOrNull(owner, field)).Any())
            throw new NotSupportedException("This conversion slice does not yet convert " + description + ".");
    }
    static string Field(ClassRecord record, string name)
    {
        if (record.HasMember(name)) return name;
        string[] matches = record.MemberNames.Where(member => member.EndsWith("+" + name, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidDataException("Missing or ambiguous record field: " + name);
    }
    static bool HasField(ClassRecord record, string name) =>
        record.MemberNames.Any(member => member == name || member.EndsWith("+" + name, StringComparison.Ordinal));
    static ClassRecord Record(ClassRecord owner, string field) => owner.GetClassRecord(Field(owner, field))
        ?? throw new InvalidDataException("Missing record: " + field);
    static ClassRecord? RecordOrNull(ClassRecord owner, string field) => owner.HasMember(field) ? owner.GetClassRecord(field) : null;
    static string Text(ClassRecord record, string field) => record.GetString(Field(record, field))
        ?? throw new InvalidDataException("Missing text field: " + field);
    static string? TextOrNull(ClassRecord record, string field) => record.GetString(Field(record, field));
    static bool Bool(ClassRecord record, string field, bool fallback) => record.MemberNames.Any(name => name == field || name.EndsWith("+" + field, StringComparison.Ordinal))
        ? record.GetBoolean(Field(record, field)) : fallback;
    static T EnumField<T>(ClassRecord record, string field) where T : struct, Enum => EnumValue<T>(Record(record, field));
    static T EnumValue<T>(ClassRecord record) where T : struct, Enum
    {
        int value = record.GetInt32(Field(record, "value__"));
        bool valid = Enum.IsDefined(typeof(T), value);
        if (Attribute.IsDefined(typeof(T), typeof(FlagsAttribute)))
        {
            int allowed = Enum.GetValues<T>().Aggregate(0, (mask, item) => mask | System.Convert.ToInt32(item));
            valid = (value & ~allowed) == 0;
        }
        if (!valid) throw new InvalidDataException("Unknown " + typeof(T).Name + " value.");
        return (T)(object)value;
    }
    static int[] Labels(ClassRecord record)
    {
        foreach (string candidate in new[] { "_labels", "FeGroup+_labels", "BasePart+_labels" })
            if (record.HasMember(candidate) && record.GetArrayRecord(candidate) is SZArrayRecord<int> labels && labels.Length <= MaxCount)
                return labels.GetArray();
        throw new InvalidDataException("Missing or oversized labels.");
    }
    static int[]? IntArrayOrNull(ClassRecord record, string field)
    {
        object? raw = record.GetRawValue(Field(record, field));
        if (raw == null) return null;
        if (raw is SZArrayRecord<int> values && values.Length <= MaxCount) return values.GetArray();
        throw new InvalidDataException("Invalid integer array: " + field);
    }
    static EquationContainer NumericEquation(ClassRecord owner, string field, Type converter, bool allowNonFinite = false) =>
        new(converter, EquationValue(Record(owner, field), converter, allowNonFinite));
    static double EquationValue(ClassRecord record, Type converter, bool allowNonFinite = false)
    {
        if (record.TypeName.FullName != "CaeGlobals.EquationContainer")
            throw new InvalidDataException("Expected an equation-container record.");
        double value;
        if (record.GetBoolean(Field(record, "_constant"))) value = record.GetDouble(Field(record, "_constantValue"));
        else
        {
            string expression = record.GetString(Field(record, "_equation"))
                ?? throw new InvalidDataException("Missing numeric expression.");
            if (expression.TrimStart().StartsWith('='))
                throw new NotSupportedException("Parameterized or formula expressions cannot be flattened during conversion.");
            var equation = new EquationContainer(converter, 0);
            equation.SetEquation(expression);
            value = equation.Value;
        }
        if (!double.IsFinite(value) && allowNonFinite) return value;
        return Finite(value, "equation value");
    }
    static double[][] EquationTable(ClassRecord owner, string field, params Type[] converters)
    {
        var array = owner.GetArrayRecord(Field(owner, field)) ?? throw new InvalidDataException("Missing equation table.");
        if (array.Rank != 1 || array.Lengths[0] is <= 0 or > 1_000_000)
            throw new InvalidDataException("Invalid equation table dimensions.");
        Array rows;
        bool equations;
        try { rows = array.GetArray(typeof(EquationContainer[][]), false); equations = true; }
        catch (InvalidOperationException) { rows = array.GetArray(typeof(double[][]), false); equations = false; }
        var values = new double[rows.Length][];
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows.GetValue(i) is not ArrayRecord row || row.Rank != 1 || row.Lengths[0] != converters.Length)
                throw new InvalidDataException("Invalid equation table row.");
            if (equations)
            {
                Array entries = row.GetArray(typeof(EquationContainer[]), false);
                values[i] = new double[entries.Length];
                for (int j = 0; j < entries.Length; j++)
                    values[i][j] = EquationValue(entries.GetValue(j) as ClassRecord
                        ?? throw new InvalidDataException("Invalid equation-table entry."), converters[j]);
            }
            else
            {
                values[i] = (double[])row.GetArray(typeof(double[]), false);
                if (values[i].Any(value => !double.IsFinite(value)))
                    throw new InvalidDataException("Nonfinite material table value.");
            }
        }
        return values;
    }
    static double[] DoubleArray(ClassRecord owner, string field, int width)
    {
        if (owner.GetArrayRecord(Field(owner, field)) is not SZArrayRecord<double> array || array.Length != width)
            throw new InvalidDataException("Invalid numeric vector: " + field);
        double[] values = array.GetArray();
        if (values.Any(value => !double.IsFinite(value))) throw new InvalidDataException("Nonfinite numeric vector.");
        return values;
    }
    static double[][] DoubleTable(ClassRecord owner, string field, int width)
    {
        var array = owner.GetArrayRecord(Field(owner, field)) ?? throw new InvalidDataException("Missing numeric table.");
        if (array.Rank != 1 || array.Lengths[0] is <= 0 or > 1_000_000)
            throw new InvalidDataException("Invalid numeric table dimensions.");
        Array rows = array.GetArray(typeof(double[][]), false);
        var values = new double[rows.Length][];
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows.GetValue(i) is not ArrayRecord row || row.Rank != 1 || row.Lengths[0] != width)
                throw new InvalidDataException("Invalid numeric table row.");
            values[i] = (double[])row.GetArray(typeof(double[]), false);
            if (values[i].Any(value => !double.IsFinite(value))) throw new InvalidDataException("Nonfinite numeric table value.");
        }
        return values;
    }
    static double Finite(double value, string description) => double.IsFinite(value) ? value
        : throw new InvalidDataException("Nonfinite " + description + ".");
}
