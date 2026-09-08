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

namespace PrePoMax
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using vtkControl;

    public sealed class SelectionCache
    {
        private sealed class Entry
        {
            public vtkMaxActor[] Actors;
            public vtkMaxActorData[] ActorData;
            public uint MemoryKB;
            public DateTime LastUsed;
        }


        // Variables                                                                                                                
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private uint _currentMemoryKB;
        private readonly uint _maxMemoryKB;


        // Properties                                                                                                               
        public uint CurrentSizeMB => _currentMemoryKB / 1000;
        public uint MaxSizeMB => _maxMemoryKB / 1000;
        public int Count => _entries.Count;


        // Constructors                                                                                                             
        public SelectionCache(uint maxSizeMB)
        {
            _maxMemoryKB = maxSizeMB * 1000;
        }


        // Methods                                                                                                                  
        public void Clear()
        {
            _entries.Clear();
            _currentMemoryKB = 0;
        }
        public bool TryGet(string key, out vtkMaxActor[] actors)
        {
            if (_entries.TryGetValue(key, out var entry) && entry.Actors != null)
            {
                entry.LastUsed = DateTime.UtcNow;
                actors = entry.Actors;
                return true;
            }
            //
            actors = null;
            return false;
        }
        public void Add(string key, vtkMaxActor[] actors)
        {
            if (actors == null || actors.Length == 0) return;
            //
            uint entryMemoryKB = CalculateMemoryKB(actors);
            // Item too large to ever fit → reject
            AddEntry(key, new Entry { Actors = actors, MemoryKB = entryMemoryKB, LastUsed = DateTime.UtcNow });
        }
        public bool TryGetActorData(string key, out vtkMaxActorData[] actorData)
        {
            if (_entries.TryGetValue(key, out var entry) && entry.ActorData != null)
            {
                entry.LastUsed = DateTime.UtcNow;
                actorData = entry.ActorData;
                return true;
            }
            //
            actorData = null;
            return false;
        }
        public void AddActorData(string key, vtkMaxActorData[] actorData)
        {
            if (actorData == null || actorData.Length == 0) return;
            uint entryMemoryKB = CalculateMemoryKB(actorData);
            AddEntry(key, new Entry { ActorData = actorData, MemoryKB = entryMemoryKB, LastUsed = DateTime.UtcNow });
        }
        private void AddEntry(string key, Entry entry)
        {
            if (entry.MemoryKB > _maxMemoryKB) return;
            if (_entries.TryGetValue(key, out Entry previous))
            {
                _currentMemoryKB -= previous.MemoryKB;
                _entries.Remove(key);
            }
            EvictUntilFits(entry.MemoryKB);
            _entries[key] = entry;
            _currentMemoryKB += entry.MemoryKB;
        }
        private uint CalculateMemoryKB(vtkMaxActor[] actors)
        {
            uint sum = 0;
            foreach (var actor in actors) sum += actor.MemorySize; // assumed KB
            //
            return sum;
        }
        private uint CalculateMemoryKB(vtkMaxActorData[] actors)
        {
            ulong bytes = 0;
            foreach (vtkMaxActorData actor in actors)
            {
                if (actor == null) continue;
                bytes += EstimateMemoryBytes(actor.Geometry);
                bytes += EstimateMemoryBytes(actor.ModelEdges);
                bytes += EstimateMemoryBytes(actor.ElementEdges);
                bytes += EstimateMemoryBytes(actor.CellLocator);
            }
            return (uint)Math.Min(uint.MaxValue, Math.Max(1UL, (bytes + 1023UL) / 1024UL));
        }
        private ulong EstimateMemoryBytes(CaeGlobals.PartExchangeData part)
        {
            if (part == null) return 0;
            ulong bytes = 0;
            if (part.Nodes != null)
            {
                bytes += (ulong)(part.Nodes.Ids == null ? 0 : part.Nodes.Ids.Length) * sizeof(int);
                bytes += (ulong)(part.Nodes.Values == null ? 0 : part.Nodes.Values.Length) * sizeof(float);
                if (part.Nodes.Coor != null)
                    foreach (double[] values in part.Nodes.Coor)
                        bytes += (ulong)(values == null ? 0 : values.Length) * sizeof(double);
                if (part.Nodes.Normals != null)
                    foreach (double[] values in part.Nodes.Normals)
                        bytes += (ulong)(values == null ? 0 : values.Length) * sizeof(double);
            }
            if (part.Cells != null)
            {
                bytes += (ulong)(part.Cells.Ids == null ? 0 : part.Cells.Ids.Length) * sizeof(int);
                bytes += (ulong)(part.Cells.Types == null ? 0 : part.Cells.Types.Length) * sizeof(int);
                bytes += (ulong)(part.Cells.Values == null ? 0 : part.Cells.Values.Length) * sizeof(float);
                if (part.Cells.CellNodeIds != null)
                    foreach (int[] values in part.Cells.CellNodeIds)
                        bytes += (ulong)(values == null ? 0 : values.Length) * sizeof(int);
            }
            return bytes;
        }
        private void EvictUntilFits(uint incomingKB)
        {
            while (_currentMemoryKB + incomingKB > _maxMemoryKB && _entries.Count > 0)
            {
                // Oldest first, prefer removing large entries
                var victim = _entries
                    .OrderBy(e => e.Value.LastUsed)
                    .ThenByDescending(e => e.Value.MemoryKB)
                    .First();
                //
                _currentMemoryKB -= victim.Value.MemoryKB;
                _entries.Remove(victim.Key);
            }
        }
    }

}
