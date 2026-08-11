// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.IO;

namespace CaeMesh.Meshing.BaramMesh
{
    public class BaramSnapCaseWriter
    {
        private readonly IBaramDictionaryWriter _dictionaryWriter;

        public BaramSnapCaseWriter()
            : this(new OpenFoamDictionaryWriter())
        {
        }

        public BaramSnapCaseWriter(IBaramDictionaryWriter dictionaryWriter)
        {
            if (dictionaryWriter == null) throw new ArgumentNullException("dictionaryWriter");
            _dictionaryWriter = dictionaryWriter;
        }

        public string WriteSnappyHexMeshDict(BaramMeshProject project, BaramBounds bounds)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (project.Configuration == null) throw new InvalidOperationException("The BaramMesh configuration is not set.");
            if (string.IsNullOrWhiteSpace(project.ProjectDirectory))
                throw new InvalidOperationException("The BaramMesh project directory is not set.");

            BaramCaseFileSystem fileSystem = new BaramCaseFileSystem(project.ProjectDirectory);
            fileSystem.CreateCaseDirectories();
            new BaramOpenFoamCaseControlsWriter().WriteMinimalCaseControls(project);

            OpenFoamDictionary dictionary =
                new BaramSnappyHexMeshDictBuilder().Build(project.Configuration, bounds, false, true, false);
            string fileName = Path.Combine(fileSystem.SystemPath, "snappyHexMeshDict");
            _dictionaryWriter.WriteDictionary(fileName, "snappyHexMeshDict", "system", dictionary);
            project.CaseDirectory = fileSystem.CaseRoot;
            return fileName;
        }
    }
}
