"""Runs inside Prepomac and creates a fresh, disposable automation-test profile.

Never copies the user's profile or prints the generated token. The caller must
put the destination in a private directory and delete it after the cohort.
"""
import clr
import os
from pathlib import Path
from System import Activator

assembly = clr.AddReference('PrePoMax')
destination = Path(os.environ['PREPOMAX_PARITY_SETTINGS'])
if destination.exists():
    raise RuntimeError('Refusing to overwrite a settings profile.')
settings = Activator.CreateInstance(assembly.GetType('PrePoMax.SettingsContainer'))
settings.Automation.AccessToken = assembly.GetType('PrePoMax.Mcp.McpAccessToken').GetMethod('Generate').Invoke(None, None)
settings.Automation.StartWithApplication = False
settings.Solver.WorkDirectoryForSettingsOnly = str(destination.parent)
settings.Solver.CalculixExe = os.environ['PREPOMAX_PARITY_SOLVER']
settings.SaveToFile(str(destination))
os.chmod(destination, 0o600)
print('TEST PASSED: fresh private automation profile written; token omitted')
