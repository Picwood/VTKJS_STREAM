"""Embedded adapter for a bounded material edit using unchanged Prepomac APIs.

The desktop API replaces complete property parts. Map the two browser field patches
to a replacement Elastic part while passing detached copies of all other parts.
This is not a claim that Prepomac has the browser's transport or operation IDs.
"""
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
                         pmx.create.thermal_expansion(1.2e-5, zero_temperature=-40)],
              description='keep this description')


def snapshot():
    material = controller.Model.Materials['ELASTIC']
    properties = []
    for prop in material.Properties:
        kind = prop.GetType().Name
        attribute = {'Elastic': 'YoungsPoissonsTemp', 'Density': 'DensityTemp',
                     'Plastic': 'StressStrainTemp', 'ThermalExpansion': 'ThermalExpansionTemp'}[kind]
        value = {'kind': kind, 'data': [[float(cell.Value) for cell in row] for row in getattr(prop, attribute)]}
        if kind == 'Plastic':
            value['hardening'] = str(prop.Hardening)
        if kind == 'ThermalExpansion':
            value['zeroTemperature'] = float(prop.ZeroTemperature.Value)
        properties.append(value)
    return {'name': str(material.Name), 'description': str(material.Description), 'active': bool(material.Active),
            'temperatureDependent': bool(material.TemperatureDependent), 'properties': properties}


def patch(e=None, nu=None):
    properties = list(materials['ELASTIC'].get_properties())
    for index, prop in enumerate(properties):
        if prop.property_type == 'elastic':
            values = dict(prop.get_values())
            properties[index] = pmx.create.elastic(e if e is not None else values['youngs_modulus'],
                                                  nu if nu is not None else values['poissons_ratio'])
    materials.replace('ELASTIC', properties=properties)


stages = [snapshot()]
patch(e=2000)
stages.append(snapshot())
patch(nu=.2)
stages.append(snapshot())
before = snapshot()
try:
    patch(e=-1)
except Exception as error:
    if type(error).__name__ != 'PythonAPIException':
        raise
else:
    raise RuntimeError('Invalid modulus accepted by the reference.')
if before != snapshot():
    raise RuntimeError('Rejected desktop edit changed material state.')
destination.write_text(json.dumps({'stages': stages, 'rejectedEditPreservedState': True}, indent=2, allow_nan=False) + '\n')
print('TEST PASSED: bounded material command reference')
