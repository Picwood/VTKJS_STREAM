"""Exact, fail-closed structural comparison of versioned semantic snapshots."""
import json
import math


def validate_snapshot(value):
    def require(condition, path, message):
        if not condition:
            raise ValueError(path + ': ' + message)

    def keys(item, expected, path):
        require(isinstance(item, dict) and set(item) == set(expected), path, 'Missing or unexpected snapshot keys.')

    def identifiers(items, path):
        require(isinstance(items, list) and all(type(item) is int and 0 < item <= 2147483647 for item in items), path, 'Expected positive source IDs.')
        require(items == sorted(set(items)), path, 'IDs must be unique and ordered.')

    require(isinstance(value, dict) and value.get('schema') == 'prepomac.parity-snapshot/1', '$.schema', 'Unsupported snapshot schema.')
    kind = value.get('kind')
    require(kind in {'model', 'field'}, '$.kind', 'Unsupported snapshot kind.')
    keys(value, ['schema', 'kind', 'mesh', 'document', 'materials', 'nodeSet'] if kind == 'model' else ['schema', 'kind', 'mesh', 'field'], '$')
    mesh = value['mesh']
    keys(mesh, ['nodes', 'elements'], '$.mesh')
    require(isinstance(mesh['nodes'], list) and 0 < len(mesh['nodes']) <= 100000, '$.mesh.nodes', 'Expected a bounded nonempty mesh.')
    ids = []
    for index, node in enumerate(mesh['nodes']):
        path = f'$.mesh.nodes[{index}]'
        keys(node, ['id', 'coordinates'], path)
        ids.append(node['id'])
        xyz = node['coordinates']
        require(isinstance(xyz, list) and len(xyz) == 3 and all(type(v) in (int, float) and math.isfinite(v) for v in xyz), path + '.coordinates', 'Expected three finite coordinates.')
    identifiers(ids, '$.mesh.nodes')
    node_ids = set(ids)
    require(isinstance(mesh['elements'], list) and 0 < len(mesh['elements']) <= 100000, '$.mesh.elements', 'Expected bounded nonempty elements.')
    element_ids = []
    for index, element in enumerate(mesh['elements']):
        path = f'$.mesh.elements[{index}]'
        keys(element, ['id', 'type', 'nodes'], path)
        element_ids.append(element['id'])
        require(isinstance(element['type'], str) and bool(element['type']), path + '.type', 'Missing concrete element type.')
        require(isinstance(element['nodes'], list) and bool(element['nodes']) and all(type(node) is int and node in node_ids for node in element['nodes']), path + '.nodes', 'Invalid source connectivity.')
    identifiers(element_ids, '$.mesh.elements')
    if kind == 'model':
        keys(value['document'], ['modelSpace', 'unitSystem'], '$.document')
        require(all(isinstance(v, str) and bool(v) for v in value['document'].values()), '$.document', 'Missing model space or units.')
        materials = value['materials']
        require(isinstance(materials, list) and bool(materials), '$.materials', 'Missing elastic fixture material.')
        names = []
        for index, material in enumerate(materials):
            path = f'$.materials[{index}]'
            keys(material, ['name', 'youngsModulus', 'poissonsRatio'], path)
            require(isinstance(material['name'], str) and bool(material['name']), path + '.name', 'Missing material name.')
            names.append(material['name'])
            require(all(type(material[key]) in (int, float) and math.isfinite(material[key]) for key in ['youngsModulus', 'poissonsRatio']), path, 'Expected finite elastic constants.')
        require(names == sorted(set(names)), '$.materials', 'Materials must have unique ordered names.')
        region = value['nodeSet']
        if region is not None:
            keys(region, ['name', 'nodes'], '$.nodeSet')
            require(isinstance(region['name'], str) and bool(region['name']), '$.nodeSet.name', 'Missing region name.')
            identifiers(region['nodes'], '$.nodeSet.nodes')
            require(set(region['nodes']) <= node_ids, '$.nodeSet.nodes', 'Region references unknown nodes.')
    else:
        field = value['field']
        keys(field, ['name', 'component', 'step', 'increment', 'time', 'unit', 'values'], '$.field')
        require(all(isinstance(field[key], str) for key in ['name', 'component', 'unit']) and field['name'] and field['component'], '$.field', 'Missing field/component/unit metadata.')
        require(type(field['step']) is int and type(field['increment']) is int and field['step'] > 0 and field['increment'] > 0, '$.field', 'Invalid frame IDs.')
        require(type(field['time']) in (int, float) and math.isfinite(field['time']), '$.field.time', 'Invalid frame time.')
        require(isinstance(field['values'], list), '$.field.values', 'Missing field values.')
        field_ids = []
        for index, row in enumerate(field['values']):
            keys(row, ['nodeId', 'float32Bits'], f'$.field.values[{index}]')
            field_ids.append(row['nodeId'])
            bits = row['float32Bits']
            require(type(bits) is int and -2147483648 <= bits <= 2147483647, f'$.field.values[{index}].float32Bits', 'Invalid Float32 bit pattern.')
        identifiers(field_ids, '$.field.values')
        require(field_ids == ids, '$.field.values', 'The snapshot must include every source node value.')


def differences(reference, candidate, path='$'):
    result = []
    if isinstance(reference, bool) or reference is None or isinstance(reference, str):
        if type(reference) is not type(candidate) or reference != candidate:
            result.append({'path': path, 'reference': reference, 'candidate': candidate})
    elif isinstance(reference, (int, float)):
        if isinstance(candidate, bool) or not isinstance(candidate, (int, float)) or not math.isfinite(reference) or not math.isfinite(candidate) or reference != candidate:
            result.append({'path': path, 'reference': reference, 'candidate': candidate})
    elif isinstance(reference, list):
        if not isinstance(candidate, list):
            return [{'path': path, 'error': 'Expected an array.'}]
        if len(reference) != len(candidate):
            result.append({'path': path + '.length', 'reference': len(reference), 'candidate': len(candidate)})
        for index, (left, right) in enumerate(zip(reference, candidate)):
            result.extend(differences(left, right, f'{path}[{index}]'))
    elif isinstance(reference, dict):
        if not isinstance(candidate, dict):
            return [{'path': path, 'error': 'Expected an object.'}]
        for key in sorted(set(reference) | set(candidate)):
            if key not in reference or key not in candidate:
                result.append({'path': path + '.' + key, 'error': 'Missing or unexpected key.'})
            else:
                result.extend(differences(reference[key], candidate[key], path + '.' + key))
    else:
        raise ValueError('Unsupported snapshot value: ' + path)
    return result


def compare(reference, candidate):
    validate_snapshot(reference)
    try:
        validate_snapshot(candidate)
    except ValueError as error:
        return {'schema': 'prepomac.parity-comparison/1', 'passed': False,
                'differences': [{'path': str(error).split(':', 1)[0], 'error': str(error)}]}
    return {'schema': 'prepomac.parity-comparison/1', 'passed': not (delta := differences(reference, candidate)), 'differences': delta}


if __name__ == '__main__':
    import argparse
    from pathlib import Path
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path)
    parser.add_argument('candidate', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = compare(json.loads(args.reference.read_text()), json.loads(args.candidate.read_text()))
    if args.output.exists():
        parser.error('Refusing to overwrite a comparison report.')
    args.output.write_text(json.dumps(report, indent=2, allow_nan=False) + '\n')
    raise SystemExit(0 if report['passed'] else 1)
