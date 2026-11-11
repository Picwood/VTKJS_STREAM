"""Bounded material creation, section assignment, unused removal and recreation through real Prepomac APIs."""
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
        result.append({'name': str(material.Name), 'description': None if material.Description is None else str(material.Description),
                       'active': bool(material.Active), 'temperatureDependent': bool(material.TemperatureDependent), 'properties': properties})
    return {'materials': result, 'sections': [{'name': str(section.Name), 'material': str(section.MaterialName)}
                                             for section in entries(controller.Model.Sections)]}


stages = [snapshot()]
materials.add('NEW', pmx.create.elastic(700, .2))
stages.append(snapshot())
refused = []
for name in ['Bad Name', 'Bad#', '123', ' NEW ', 'elastic']:
    before = snapshot()
    try:
        materials.add(name, pmx.create.elastic(700, .2))
    except Exception as error:
        if type(error).__name__ != 'PythonAPIException':
            raise
        refused.append(name)
    else:
        raise RuntimeError('Reference accepted an invalid/colliding creation name: ' + name)
    if snapshot() != before:
        raise RuntimeError('Refused creation changed reference state.')
before = snapshot()
try:
    materials.add('INVALID', pmx.create.elastic(-1, .2))
except Exception as error:
    if type(error).__name__ != 'PythonAPIException':
        raise
else:
    raise RuntimeError('Reference accepted a negative modulus.')
if snapshot() != before:
    raise RuntimeError('Refused numeric creation changed reference state.')
pmx.model.sections.replace_solid('solid-1', material_name='NEW')
stages.append(snapshot())
pmx.model.sections.replace_solid('solid-1', material_name='ELASTIC')
stages.append(snapshot())
materials.remove(['NEW'])
stages.append(snapshot())
materials.add('NEW', pmx.create.elastic(800, .25))
stages.append(snapshot())
destination.write_text(json.dumps({'stages': stages, 'refusedNames': refused, 'negativeModulusRefused': True}, indent=2, allow_nan=False) + '\n')
print('TEST PASSED: bounded material lifecycle reference')
