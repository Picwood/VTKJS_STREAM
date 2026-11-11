using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public static class ProjectDefinitions
{
    public static void RemoveCopy(string source, string destination, string stepName, string name, bool boundary)
    {
        var project = StaticProjectArchive.Open(source);
        var step = project.Model.StepCollection.StepsList.SingleOrDefault(s => s.Name == stepName)
            ?? throw new ArgumentException("Select an existing step.", nameof(stepName));
        if (boundary)
        {
            if (!step.BoundaryConditions.TryGetValue(name, out var definition) || definition is not DisplacementRotation)
                throw new NotSupportedException("Select an existing displacement boundary.");
            step.BoundaryConditions.Remove(name);
        }
        else
        {
            if (!step.Loads.TryGetValue(name, out var definition) || definition is not (CLoad or STLoad))
                throw new NotSupportedException("Select an existing force load.");
            step.Loads.Remove(name);
        }
        ProjectEdits.SaveNew(destination, project);
    }
}
