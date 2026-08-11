// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.IO;

namespace CaeMesh.Meshing.BaramMesh
{
    public class BaramBaseGridCaseWriter
    {
        private readonly IBaramDictionaryWriter _dictionaryWriter;

        public BaramBaseGridCaseWriter()
            : this(new OpenFoamDictionaryWriter())
        {
        }

        public BaramBaseGridCaseWriter(IBaramDictionaryWriter dictionaryWriter)
        {
            if (dictionaryWriter == null) throw new ArgumentNullException("dictionaryWriter");
            _dictionaryWriter = dictionaryWriter;
        }

        public string WriteBlockMeshDict(BaramMeshProject project, BaramBounds bounds)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (project.Configuration == null) throw new InvalidOperationException("The BaramMesh configuration is not set.");
            if (string.IsNullOrWhiteSpace(project.ProjectDirectory))
                throw new InvalidOperationException("The BaramMesh project directory is not set.");

            BaramCaseFileSystem fileSystem = new BaramCaseFileSystem(project.ProjectDirectory);
            fileSystem.CreateCaseDirectories();
            new BaramOpenFoamCaseControlsWriter().WriteMinimalCaseControls(project);

            BaramBlockMeshDictBuilder builder = new BaramBlockMeshDictBuilder();
            OpenFoamDictionary dictionary = builder.Build(project.Configuration, bounds);

            string blockMeshDictFileName = Path.Combine(fileSystem.SystemPath, "blockMeshDict");
            _dictionaryWriter.WriteDictionary(blockMeshDictFileName, "blockMeshDict", "system", dictionary);

            File.WriteAllText(fileSystem.GetFoamFilePath(), "");
            project.CaseDirectory = fileSystem.CaseRoot;

            return blockMeshDictFileName;
        }
    }
}
