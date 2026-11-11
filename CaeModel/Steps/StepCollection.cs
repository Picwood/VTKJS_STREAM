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
using CaeGlobals;

namespace CaeModel
{
    [Serializable]
    public partial class StepCollection
    {
        // Variables                                                                                                                
        private List<Step> _steps;
        private bool _onlyCheckModel;


        // Properties                                                                                                               
        public List<Step> StepsList { get { return _steps; } }
        public bool OnlyCheckModel { get { return _onlyCheckModel; } }


        // Constructors                                                                                                             
        public StepCollection()
        {
            _steps = new List<Step>();
            _onlyCheckModel = false;
        }


        // Static Methods                                                                                                           
        public bool MultiRegionSelectionExists(string stepName, IMultiRegion newRegion)
        {
            List<Step> prevSteps = new List<Step>();
            foreach (var step in _steps)
            {
                if (step.Name != stepName) prevSteps.Add(step);
                else break;
            }
            //
            List<IMultiRegion> existingRegions = new List<IMultiRegion>();
            foreach (var step in prevSteps)
            {
                if (newRegion is HistoryOutput) existingRegions.AddRange(step.HistoryOutputs.Values);
                else if (newRegion is BoundaryCondition) existingRegions.AddRange(step.BoundaryConditions.Values);
                else if (newRegion is Load) existingRegions.AddRange(step.Loads.Values);
                else if (newRegion is DefinedField) existingRegions.AddRange(step.DefinedFields.Values);
                else throw new NotSupportedException();
            }
            //
            return MultiRegionSelectionExists(existingRegions, newRegion);
        }
        private bool MultiRegionSelectionExists(List<IMultiRegion> existingRegions, IMultiRegion newRegion)
        {
            foreach (var existingRegion in existingRegions)
            {
                if (IsSelectionRegionEqual(existingRegion, newRegion))
                {
                    newRegion.RegionName = existingRegion.RegionName;
                    newRegion.RegionType = existingRegion.RegionType;
                    return true;
                }
            }
            return false;
        }
        public static bool MultiRegionChanged(IMultiRegion oldRegion, IMultiRegion newRegion)
        {
            if (IsSelectionRegionEqual(oldRegion, newRegion))
            {
                // Region remained the same
                newRegion.RegionName = oldRegion.RegionName;
                newRegion.RegionType = oldRegion.RegionType;
                return false;
            }
            else
            {
                // Region changed
                return true;
            }
        }
        private static bool IsSelectionRegionEqual(IMultiRegion existingRegion, IMultiRegion newRegion)
        {
            // IsRegionNameSelection(newRegion.RegionName) is used for Propagate
            if ((newRegion.RegionType == RegionTypeEnum.Selection || IsRegionNameSelection(newRegion.RegionName)) &&
                existingRegion.CreationIds != null && newRegion.CreationIds != null &&
                IsRegionNameSelection(existingRegion.RegionName) &&
                newRegion.CreationIds.Length == existingRegion.CreationIds.Length &&
                newRegion.GetType() == existingRegion.GetType() &&
                newRegion.CreationIds.Except(existingRegion.CreationIds).Count() == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private static bool IsRegionNameSelection(string regionName)
        {
            return regionName.StartsWith(CaeMesh.Globals.InternalSelectionName);
        }


        // Methods                                                                                                                  
        public void AddStep(Step step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            _steps.Add(step);
        }
        public Step GetStep(string stepName)
        {
            foreach (var step in _steps)
            {
                if (step.Name == stepName) return step;
            }
            return null;
        }
        public string[] GetStepNames()
        {
            List<string> names = new List<string>();
            foreach (var step in _steps) names.Add(step.Name);
            return names.ToArray();
        }
        public int GetNumberOfActiveSteps()
        {
            int count = 0;
            foreach (var entry in _steps)
            {
                if (entry.Active) count++;
            }
            return count;
        }
        public void ReplaceStep(string oldStepName, Step newStep)
        {
            List<Step> newSteps = new List<Step>();
            foreach (var step in _steps)
            {
                if (step.Name == oldStepName) newSteps.Add(newStep);
                else newSteps.Add(step);
            }
            _steps = newSteps;
        }
        public Step RemoveStep(string stepName)
        {
            Step stepToRemove = null;
            foreach (var step in _steps)
            {
                if (step.Name == stepName)
                {
                    stepToRemove = step;
                    break;
                }
            }
            if (stepToRemove != null) _steps.Remove(stepToRemove);
            //
            return stepToRemove;
        }
        public string[] GetNextStepNames(string stepName)
        {
            List<string> stepNames = null;
            foreach (var step in _steps)
            {
                if (stepNames != null) stepNames.Add(step.Name);
                if (step.Name == stepName) stepNames = new List<string>();
            }
            if (stepNames.Count > 0) return stepNames.ToArray();
            else return new string[0];
        }
        public CaeGlobals.OrderedDictionary<int, double> GetStepIdDuration()
        {
            int count = 1;  // start at 1
            CaeGlobals.OrderedDictionary<int, double> stepIdDuration =
                new CaeGlobals.OrderedDictionary<int, double>("Step id - duration");
            //
            foreach (var step in _steps)
            {
                if (step is StaticStep ss) stepIdDuration.Add(count++, ss.TimePeriod);
                else if (step is BoundaryDisplacementStep) continue;
                else if (step is FrequencyStep fs) stepIdDuration.Add(count++, 0);
                else if (step is BuckleStep bs) stepIdDuration.Add(count++, 0);
                else throw new NotImplementedException();
            }
            //
            return stepIdDuration;
        }
        // Run or check analysis
        public void SetRunAnalysis()
        {
            _onlyCheckModel = false;
            foreach (Step step in _steps) step.RunAnalysis = true;
        }
        public void SetCheckModel()
        {
            _onlyCheckModel = true;
            foreach (Step step in _steps) step.RunAnalysis = false;
        }
        // History
        public string[] GetStepHistoryOutputNames(string stepName)
        {
            foreach (var step in _steps)
            {
                if (step.Name == stepName) return step.HistoryOutputs.Keys.ToArray();
            }
            return null;
        }
        public void AddHistoryOutput(HistoryOutput historyOutput, string stepName)
        {
            foreach (var step in _steps)
            {
                if (step.Name == stepName) step.AddHistoryOutput(historyOutput);
            }
        }
        public Dictionary<string, int> GetHistoryOutputRegionsCount()
        {
            Dictionary<string, int> regionsCount = new Dictionary<string, int>();
            foreach (var step in _steps)
            {
                foreach (var entry in step.HistoryOutputs)
                {
                    if (entry.Value.RegionName != null)
                    {
                        if (regionsCount.ContainsKey(entry.Value.RegionName)) regionsCount[entry.Value.RegionName]++;
                        else regionsCount.Add(entry.Value.RegionName, 1);
                    }
                }
            }
            return regionsCount;
        }
        public Step GetHistoryOutputStep(HistoryOutput historyOutput)
        {
            HistoryOutput existing;
            foreach (var step in _steps)
            {
                if (step.HistoryOutputs.TryGetValue(historyOutput.Name, out existing) && existing == historyOutput)
                    return step;
            }
            return null;
        }
        // Field
        public void AddFieldOutput(FieldOutput fieldOutput, string stepName)
        {
            foreach (var step in _steps)
            {
                if (step.Name == stepName) step.AddFieldOutput(fieldOutput);
            }
        }
        public string[] GetStepFieldOutputNames(string stepName)
        {
            foreach (var step in _steps)
            {
                if (step.Name == stepName) return step.FieldOutputs.Keys.ToArray();
            }
            return null;
        }
        public Step GetFiledOutputStep(FieldOutput fieldOutput)
        {
            FieldOutput existing;
            foreach (var step in _steps)
            {
                if (step.FieldOutputs.TryGetValue(fieldOutput.Name, out existing) && existing == fieldOutput)
                    return step;
            }
            return null;
        }
        // Boundary condition
        public string[] GetAllBoundaryConditionNames()
        {
            HashSet<string> allNames = new HashSet<string>();
            foreach (var step in _steps) allNames.UnionWith(step.BoundaryConditions.Keys);
            return allNames.ToArray();
        }
        public BoundaryCondition[] GetAllBoundaryConditions()
        {
            HashSet<BoundaryCondition> allBCs = new HashSet<BoundaryCondition>();
            foreach (var step in _steps) allBCs.UnionWith(step.BoundaryConditions.Values);
            return allBCs.ToArray();
        }
        public string[] GetStepBoundaryConditionNames(string stepName)
        {
            foreach (var step in _steps)
            {
                if (step.Name == stepName) return step.BoundaryConditions.Keys.ToArray();
            }
            return null;
        }
        public Dictionary<string, int> GetBoundaryConditionRegionsCount()
        {
            Dictionary<string, int> regionsCount = new Dictionary<string, int>();
            foreach (var step in _steps)
            {
                foreach (var entry in step.BoundaryConditions)
                {
                    if (entry.Value.RegionName != null)
                    {
                        if (regionsCount.ContainsKey(entry.Value.RegionName)) regionsCount[entry.Value.RegionName]++;
                        else regionsCount.Add(entry.Value.RegionName, 1);
                    }
                }
            }
            return regionsCount;
        }
        public Step GetBoundaryConditionStep(BoundaryCondition boundaryCondition)
        {
            BoundaryCondition existing;
            foreach (var step in _steps)
            {
                if (step.BoundaryConditions.TryGetValue(boundaryCondition.Name, out existing) && existing == boundaryCondition)
                    return step;
            }
            return null;
        }
        public string[] GetAllLoadNames()
        {
            HashSet<string> allNames = new HashSet<string>();
            foreach (var step in _steps) allNames.UnionWith(step.Loads.Keys);
            return allNames.ToArray();
        }
        public Load[] GetAllLoads()
        {
            HashSet<Load> allLoads = new HashSet<Load>();
            foreach (var step in _steps) allLoads.UnionWith(step.Loads.Values);
            return allLoads.ToArray();
        }
        public string[] GetStepLoadNames(string stepName)
        {
            foreach (var step in _steps)
            {
                if (step.Name == stepName) return step.Loads.Keys.ToArray();
            }
            return null;
        }
        public Dictionary<string, int> GetLoadRegionsCount()
        {
            Dictionary<string, int> regionsCount = new Dictionary<string, int>();
            foreach (var step in _steps)
            {
                foreach (var entry in step.Loads)
                {
                    if (entry.Value.RegionName != null)
                    {
                        if (regionsCount.ContainsKey(entry.Value.RegionName)) regionsCount[entry.Value.RegionName]++;
                        else regionsCount.Add(entry.Value.RegionName, 1);
                    }
                }
            }
            return regionsCount;
        }
        public Step GetLoadStep(Load load)
        {
            Load existing;
            foreach (var step in _steps)
            {
                if (step.Loads.TryGetValue(load.Name, out existing) && existing == load)
                    return step;
            }
            return null;
        }
        public HashSet<Type> GetAllLoadTypes()
        {
            HashSet<Type> loadTypes = new HashSet<Type>();
            foreach (var _entry in _steps)
            {
                foreach (var loadEntry in _entry.Loads) loadTypes.Add(loadEntry.Value.GetType());
            }
            return loadTypes;
        }
        public bool IsActiveRadiationLoadDefined()
        {
            foreach (var _entry in _steps)
            {
                foreach (var loadEntry in _entry.Loads)
                {
                    if (loadEntry.Value is RadiationHeatTransfer rht && rht.Active) return true;
                }
            }
            //
            return false;
        }
        public int GetNumberOfCentrifLoads()
        {
            int count = 0;
            foreach (var stepEntry in _steps)
            {
                foreach (var loadEntry in stepEntry.Loads)
                {
                    if (loadEntry.Value is CentrifLoad cf) count++;
                }
            }
            return count;
        }
        // Defined field
        public string[] GetAllDefinedFieldNames()
        {
            HashSet<string> allNames = new HashSet<string>();
            foreach (var step in _steps) allNames.UnionWith(step.DefinedFields.Keys);
            return allNames.ToArray();
        }
        public string[] GetStepDefinedFieldNames(string stepName)
        {
            foreach (var step in _steps)
            {
                if (step.Name == stepName) return step.DefinedFields.Keys.ToArray();
            }
            return null;
        }
        public Dictionary<string, int> GetDefinedFieldRegionsCount()
        {
            Dictionary<string, int> regionsCount = new Dictionary<string, int>();
            foreach (var step in _steps)
            {
                foreach (var entry in step.DefinedFields)
                {
                    if (entry.Value.RegionName != null)
                    {
                        if (regionsCount.ContainsKey(entry.Value.RegionName)) regionsCount[entry.Value.RegionName]++;
                        else regionsCount.Add(entry.Value.RegionName, 1);
                    }
                }
            }
            return regionsCount;
        }
        public Step GetDefinedFieldStep(DefinedField definedField)
        {
            DefinedField existing;
            foreach (var step in _steps)
            {
                if (step.DefinedFields.TryGetValue(definedField.Name, out existing) && existing == definedField)
                    return step;
            }
            return null;
        }
        // Wear
        public int[] GetSlipWearStepIds()
        {
            int count = 1;  // start at 1
            List<int> stepIds = new List<int>();
            foreach (var step in _steps)
            {
                if (step is SlipWearStep sws) stepIds.Add(count);
                count++;
            }
            return stepIds.ToArray();
        }
        public BoundaryDisplacementStep GetBoundaryDisplacementStep()
        {
            int count = 0;
            BoundaryDisplacementStep boundaryDisplacementStep = null;
            foreach (var step in _steps)
            {
                if (step is BoundaryDisplacementStep bds)
                {
                    boundaryDisplacementStep = bds;
                    count++;
                }
            }
            //
            if (count > 1) throw new CaeException("More than one boundary displacement step defined.");
            else if (count == 1) return boundaryDisplacementStep;
            else return null;
        }
        public bool AreContactHistoryOutputsDefined()
        {
            bool defined;
            foreach (var step in _steps)
            {
                if (step is BoundaryDisplacementStep) continue;
                //
                defined = false;
                foreach (var entry in step.HistoryOutputs)
                {
                    if (entry.Value.Active &&
                        entry.Value is ContactHistoryOutput cho &&
                        cho.Variables.HasFlag(ContactHistoryVariable.CDIS))
                    {
                        defined = true;
                        break;
                    }
                }
                if (!defined) return false;
            }
            return true;
        }
    }
}
