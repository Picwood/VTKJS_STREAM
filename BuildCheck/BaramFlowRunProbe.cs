using CaeMesh.Meshing.BaramMesh;
using CaeModel.CFD.BaramFlow;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml;

internal static class BaramFlowRunProbe
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static string Text(BaramFlowRunSettings run, BaramFlowGeneralSettings general)
    {
        OpenFoamDictionary dictionary = new BaramFlowRunCaseWriter().BuildControlDict(run, general);
        return new OpenFoamDictionaryWriter().BuildDictionaryText("controlDict", "system", dictionary);
    }

    private static void Contains(string text, string expected)
    {
        Require(text.Contains(expected), "Missing controlDict entry: " + expected);
    }

    private static string CreateProject()
    {
        string project = Path.Combine(Path.GetTempPath(), "PrePoMax-BaramFlow-RunProbe-" + Guid.NewGuid().ToString("N") + ".bf");
        string polyMesh = Path.Combine(project, "case", "constant", "polyMesh");
        Directory.CreateDirectory(polyMesh);
        File.WriteAllText(Path.Combine(polyMesh, "points"), "0\n(\n)\n");
        File.WriteAllText(Path.Combine(polyMesh, "faces"), "0\n(\n)\n");
        File.WriteAllText(Path.Combine(polyMesh, "owner"), "0\n(\n)\n");
        File.WriteAllText(Path.Combine(polyMesh, "boundary"),
            "FoamFile\n{\n version 2.0;\n format ascii;\n class polyBoundaryMesh;\n object boundary;\n}\n1\n(\nwall\n{\n type wall;\n nFaces 0;\n startFace 0;\n}\n)\n");
        return project;
    }

    public static int Main()
    {
        BaramFlowRunSettings run = new BaramFlowRunSettings();
        BaramFlowGeneralSettings general = new BaramFlowGeneralSettings();
        string steady = Text(run, general);
        Contains(steady, "application buoyantSimpleNFoam;");
        Contains(steady, "endTime 1000;");
        Contains(steady, "deltaT 1;");
        Contains(steady, "writeControl runTime;");
        Contains(steady, "writeInterval 100;");

        general.TimeMode = BaramFlowTimeMode.Transient;
        run.TimeStepSize = 0.025;
        run.EndTime = 12.5;
        run.ReportIntervalSeconds = 0.25;
        string fixedTransient = Text(run, general);
        Contains(fixedTransient, "application buoyantPimpleNFoam;");
        Contains(fixedTransient, "endTime 12.5;");
        Contains(fixedTransient, "deltaT 0.025");
        Contains(fixedTransient, "writeControl runTime;");
        Contains(fixedTransient, "writeInterval 0.25");
        Contains(fixedTransient, "adjustTimeStep no;");

        run.TimeSteppingMethod = BaramFlowTimeSteppingMethod.Adaptive;
        run.MaximumCourantNumber = 2.5;
        run.MaximumDiffusionNumber = 9;
        string adaptive = Text(run, general);
        Contains(adaptive, "deltaT 0.001;");
        Contains(adaptive, "writeControl adjustableRunTime;");
        Contains(adaptive, "adjustTimeStep yes;");
        Contains(adaptive, "maxCo 2.5");
        Contains(adaptive, "maxDi 9;");

        general.SolverType = BaramFlowSolverType.DensityBased;
        general.FlowType = BaramFlowFlowType.Compressible;
        string density = Text(run, general);
        Contains(density, "application UTSLAeroFoam;");
        Contains(density, "deltaT 0.025");
        Contains(density, "adjustTimeStep no;");

        string projectDirectory = CreateProject();
        try
        {
            BaramFlowProject project = BaramFlowProject.Open(projectDirectory);
            BaramFlowRunSettings persisted = project.RunConditions.Copy();
            persisted.NumberOfIterations = 4321;
            persisted.DataWriteFormat = BaramFlowDataWriteFormat.Ascii;
            persisted.DataWritePrecision = 14;
            persisted.RetainOnlyMostRecentFiles = true;
            persisted.MaximumNumberOfDataFiles = 3;
            project.SetRunConditions(persisted);
            BaramFlowProject reopened = BaramFlowProject.Open(projectDirectory);
            Require(reopened.RunConditions.NumberOfIterations == 4321, "Run iteration persistence failed.");
            Require(reopened.RunConditions.DataWriteFormat == BaramFlowDataWriteFormat.Ascii, "Run format persistence failed.");
            Require(reopened.RunConditions.MaximumNumberOfDataFiles == 3, "Run retention persistence failed.");

            string control = new BaramFlowRunCaseWriter().Write(reopened, reopened.RunConditions);
            string written = File.ReadAllText(control);
            Contains(written, "writeFormat ascii;");
            Contains(written, "writePrecision 14;");
            Contains(written, "purgeWrite 3;");
            File.WriteAllText(control, "RUN_WRITER_OWNS_THIS_FILE");
            new BaramFlowGeneralCaseWriter().Write(reopened, reopened.General);
            Require(File.ReadAllText(control) == "RUN_WRITER_OWNS_THIS_FILE", "General overwrote controlDict.");

            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, reopened);
                stream.Position = 0;
                BaramFlowProject clone = (BaramFlowProject)formatter.Deserialize(stream);
                Require(clone.RunConditions.NumberOfIterations == 4321, "Binary run-state round trip failed.");
            }

            XmlDocument state = new XmlDocument();
            string stateFile = Path.Combine(projectDirectory, "prepomax-baramflow.xml");
            state.Load(stateFile);
            XmlElement root = state.DocumentElement;
            root.SetAttribute("version", "5");
            XmlNode oldRun = root.SelectSingleNode("runConditions");
            root.RemoveChild(oldRun);
            state.Save(stateFile);
            BaramFlowProject migrated = BaramFlowProject.Open(projectDirectory);
            Require(migrated.RunConditions.NumberOfIterations == 1000, "Version-5 default migration failed.");
        }
        finally
        {
            if (Directory.Exists(projectDirectory)) Directory.Delete(projectDirectory, true);
        }

        Console.WriteLine("BaramFlow Run Conditions probe passed.");
        return 0;
    }
}
