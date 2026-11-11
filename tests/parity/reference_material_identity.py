"""Observe actual Prepomac material-rename and section-reference behavior without modifying the oracle."""
import json
import os
from pathlib import Path
from System.Windows.Forms import Application

destination = Path(os.environ['PREPOMAX_PARITY_OUTPUT'])
if destination.exists():
    raise RuntimeError('Refusing to overwrite reference output.')
controller = next(form for form in Application.OpenForms if form.GetType().FullName == 'PrePoMax.FrmMain').Controller
materials = pmx.model.materials
materials.add('ELASTIC', [pmx.create.density(2.5), pmx.create.elastic(1000, 0),
                         pmx.create.plastic([[100, 0, 0], [120, .1, 0]], hardening='Combined'),
                         pmx.create.thermal_expansion(1.2e-5, zero_temperature=-40)], description='keep this description')
materials.add('OTHER', pmx.create.elastic(500, .1))
selection = pmx.create.selection_by_ids('femodel', 'parts', 'parts', [pmx.model.parts.first().id])
pmx.model.sections.add_solid('solid-1', 'ELASTIC', selection)


def entries(collection):
    return collection.values() if isinstance(collection, dict) else collection.Values


def snapshot():
    result = []
    for material in entries(controller.Model.Materials):
        properties = []
        for prop in material.Properties:
            kind = prop.GetType().Name
            attribute = {'Elastic': 'YoungsPoissonsTemp', 'Density': 'DensityTemp', 'Plastic': 'StressStrainTemp',
                         'ThermalExpansion': 'ThermalExpansionTemp'}[kind]
            value = {'kind': kind, 'data': [[float(cell.Value) for cell in row] for row in getattr(prop, attribute)]}
            if kind == 'Plastic':
                value['hardening'] = str(prop.Hardening)
            if kind == 'ThermalExpansion':
                value['zeroTemperature'] = float(prop.ZeroTemperature.Value)
            properties.append(value)
        result.append({'name': str(material.Name), 'description': None if material.Description is None else str(material.Description), 'active': bool(material.Active),
                       'temperatureDependent': bool(material.TemperatureDependent), 'properties': properties})
    return {'materials': result, 'sections': [{'name': str(section.Name), 'material': str(section.MaterialName)}
                                             for section in entries(controller.Model.Sections)]}


stages = [snapshot()]
materials.rename('ELASTIC', 'ALLOY')
stages.append(snapshot())
materials.rename('ALLOY', 'alloy')
stages.append(snapshot())
refusals = []
for name in ['other', 'Bad Name', 'Bad#', '123', ' ALLOY ']:
    before = snapshot()
    try:
        materials.rename('alloy', name)
    except Exception as error:
        if type(error).__name__ != 'PythonAPIException':
            raise
        refusals.append(name)
    else:
        raise RuntimeError('Reference accepted an invalid/colliding name: ' + name)
    if snapshot() != before:
        raise RuntimeError('Rejected reference rename changed state.')
destination.write_text(json.dumps({'stages': stages, 'refusedNames': refusals}, indent=2, allow_nan=False) + '\n')
print('TEST PASSED: bounded material identity reference')
