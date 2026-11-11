"""Runs inside the unchanged Prepomac application; writes a bounded semantic snapshot.

Public API operations exercise region validation. The production mesh is read
through the same read-only CLR access used by Prepomac's custom verifiers.
"""
import json
import os
from pathlib import Path
import struct

from System.Windows.Forms import Application

form = next(item for item in Application.OpenForms if item.GetType().FullName == 'PrePoMax.FrmMain')
controller = form.Controller
mode = os.environ['PREPOMAX_PARITY_MODE']
destination = Path(os.environ['PREPOMAX_PARITY_OUTPUT'])
if destination.exists():
    raise RuntimeError('Refusing to overwrite a reference snapshot.')


def entries(collection):
    # Prepomac's registered dictionary encoder exposes some CLR maps as Python dicts.
    return collection.values() if isinstance(collection, dict) else collection.Values


def mesh_snapshot(mesh, node_data=None):
    nodes = [{'id': int(node.Id), 'coordinates': [float(node.X), float(node.Y), float(node.Z)]}
             for node in sorted(entries(mesh.Nodes), key=lambda item: int(item.Id))]
    if node_data is not None:
        # The desktop viewport deforms CurrentResult.Mesh in place. The public result
        # API deliberately returns undeformed coordinates; compare that domain state.
        ids = list(node_data.ids)
        coordinates = list(node_data.coordinates)
        nodes = sorted([{'id': int(node), 'coordinates': [float(v) for v in xyz]}
                        for node, xyz in zip(ids, coordinates)], key=lambda item: item['id'])
    return {
        'nodes': nodes,
        'elements': [{'id': int(element.Id), 'type': element.GetType().Name, 'nodes': [int(node) for node in element.NodeIds]}
                     for element in sorted(entries(mesh.Elements), key=lambda item: int(item.Id))],
    }


if mode == 'model':
    pmx.model.materials.add('ELASTIC', pmx.create.elastic(1000, 0))
    selection = pmx.create.selection_by_ids('femodel', 'nodes', 'nodes', [10, 30])
    pmx.model.node_sets.add('PARITY_SELECTED', selection)
    original = mesh_snapshot(controller.Model.Mesh)
    refused = False
    try:
        bad = pmx.create.selection_by_ids('femodel', 'nodes', 'nodes', [999999])
        pmx.model.node_sets.add('PARITY_INVALID', bad)
    except Exception as error:
        if type(error).__name__ != 'PythonAPIException':
            raise
        refused = True
    if not refused or original != mesh_snapshot(controller.Model.Mesh) or pmx.model.node_sets.contains('PARITY_INVALID'):
        raise RuntimeError('Invalid membership was accepted or changed the model.')
    snapshot = {
        'schema': 'prepomac.parity-snapshot/1', 'kind': 'model',
        'document': {'modelSpace': str(pmx.model.properties.model_space), 'unitSystem': str(pmx.model.properties.unit_system)},
        'materials': [{'name': str(material.Name),
                       'youngsModulus': float(prop.YoungsPoissonsTemp[0][0].Value),
                       'poissonsRatio': float(prop.YoungsPoissonsTemp[0][1].Value)}
                      for material in sorted(entries(controller.Model.Materials), key=lambda item: str(item.Name))
                      for prop in material.Properties if prop.GetType().Name == 'Elastic'],
        'mesh': original,
        'nodeSet': {'name': 'PARITY_SELECTED', 'nodes': sorted(int(node) for node in pmx.model.node_sets['PARITY_SELECTED'].get_node_ids())},
    }
elif mode == 'field':
    result = pmx.results.current
    step = result.steps[1]
    increment = step.increments[1]
    data = increment.fields['DISP'].components['U1'].get_data()
    ids = list(data.ids)
    values = list(data.values)
    snapshot = {
        'schema': 'prepomac.parity-snapshot/1', 'kind': 'field',
        'mesh': mesh_snapshot(controller.CurrentResult.Mesh, result.mesh.nodes.get_data()),
        'field': {'name': 'DISP', 'component': 'U1', 'step': 1, 'increment': 1,
                  'time': float(increment.step_time), 'unit': str(data.unit),
                  'values': sorted([{'nodeId': int(node), 'float32Bits': struct.unpack('<i', struct.pack('<f', float(value)))[0]}
                                    for node, value in zip(ids, values)], key=lambda row: row['nodeId'])},
    }
else:
    raise RuntimeError('Unknown parity snapshot mode.')
destination.write_text(json.dumps(snapshot, indent=2, allow_nan=False) + '\n')
print('TEST PASSED: bounded reference snapshot ' + mode)
