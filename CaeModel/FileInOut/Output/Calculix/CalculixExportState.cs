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
using CaeGlobals;
using CaeMesh;
using CaeModel;

namespace FileInOut.Output
{
    // Temporary state used by GetModelKeywords, not a whole-model clone.
    internal sealed class CalculixExportState : IDisposable
    {
        private readonly FeMesh _mesh;
        private readonly CaeGlobals.OrderedDictionary<string, FeSurface> _surfaces;
        private readonly CaeGlobals.OrderedDictionary<string, FeElementSet> _elementSets;
        private readonly Dictionary<MassSection, string> _massElementSets;
        private bool _disposed;

        public Dictionary<int, FeElement> ReplacedElements { get; } = new Dictionary<int, FeElement>();

        public CalculixExportState(FeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            _mesh = model.Mesh;
            _surfaces = _mesh.Surfaces;
            _elementSets = _mesh.ElementSets;
            // Finish every potentially failing copy before changing the caller's mesh.
            var surfaces = _surfaces.CopyValues(surface => new FeSurface(surface));
            var elementSets = _elementSets.CopyValues(set => new FeElementSet(set));
            _massElementSets = new Dictionary<MassSection, string>();
            foreach (Section section in model.Sections.Values)
                if (section is MassSection mass) _massElementSets[mass] = mass.ElementSetName;
            _mesh.Surfaces = surfaces;
            _mesh.ElementSets = elementSets;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _mesh.Surfaces = _surfaces;
            _mesh.ElementSets = _elementSets;
            foreach (var entry in ReplacedElements) _mesh.Elements[entry.Key] = entry.Value;
            foreach (var entry in _massElementSets) entry.Key.ElementSetName = entry.Value;
            _disposed = true;
        }
    }
}
