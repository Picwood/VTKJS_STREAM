#!/usr/bin/env python3
"""Exercise the read-only PMX inventory tool with small synthetic NRBF payloads."""
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile
import zipfile
import zlib

ROOT = Path(__file__).resolve().parents[1]
DOTNET = os.environ.get('PREPOMAX_DOTNET') or shutil.which('dotnet') or str(Path.home() / '.local/share/prepomax/dotnet/dotnet')
DLL = ROOT / 'portable/PrePoMax.PmxInspect/bin/Release/net10.0/PrePoMax.PmxInspect.dll'


def pmx(payload, version=b'PrePoMax v2.5.1 dev', declared=None, binary=b''):
    raw = struct.pack('<q', len(payload) if declared is None else declared) + payload + binary
    compressor = zlib.compressobj(wbits=-15)
    compressed = compressor.compress(raw) + compressor.flush()
    return version.ljust(32, b'\0') + struct.pack('<i', len(compressed)) + compressed


def model_payload(size_override=None, regions=None, results=False):
    next_id = 10
    def text(value):
        raw = value.encode()
        assert len(raw) < 128
        return bytes([len(raw)]) + raw
    def obj(name, **members):
        return (name, members)
    def ints(*values):
        return ('__ints__', values)
    def encode(value):
        nonlocal next_id
        next_id += 1
        ident = next_id
        if value is None: return b'\x0a'
        if isinstance(value, str): return b'\x06' + struct.pack('<i', ident) + text(value)
        if isinstance(value, bool): return b'\x08\x01' + bytes([value])
        if isinstance(value, int): return b'\x08\x08' + struct.pack('<i', value)
        if isinstance(value, float): return b'\x08\x06' + struct.pack('<d', value)
        if isinstance(value, list):
            return b'\x10' + struct.pack('<ii', ident, len(value)) + b''.join(encode(v) for v in value)
        name, members = value
        if name == '__ints__':
            return b'\x0f' + struct.pack('<iiB', ident, len(members), 8) + b''.join(struct.pack('<i', v) for v in members)
        return (b'\x05' + struct.pack('<i', ident) + text(name) + struct.pack('<i', len(members))
                + b''.join(text(k) for k in members) + b'\x02' * len(members)
                + struct.pack('<i', 1) + b''.join(encode(v) for v in members.values()))
    def dictionary(*values):
        return obj('CaeGlobals.OrderedDictionary`2', _dictionary=obj('System.Collections.Generic.Dictionary`2',
            KeyValuePairs=[obj('Pair', key=str(i), value=v) for i, v in enumerate(values)]))
    def seq(*values):
        # Spare capacity must not appear as a current definition.
        return obj('System.Collections.Generic.List`1', _size=len(values) if size_override is None else size_override, _items=[*values, None])
    enum = lambda value: obj('Enum', value__=value)
    equation = lambda value: obj('CaeGlobals.EquationContainer', _constant=True, _constantValue=float(value))
    material = obj('CaeModel.Material', _active=False, _properties=seq(obj('CaeModel.Elastic')))
    step = obj('CaeModel.StaticStep', _active=True, _nlgeom=True,
               _loads=dictionary(), _boundaryConditions=dictionary())
    model = obj('CaeModel.FeModel', _name='fixture',
                _unitSystem=obj('CaeGlobals.UnitSystem', _unitSystemType=enum(0)),
                _properties=obj('CaeModel.ModelProperties', ModelSpace=enum(1), ModelWorkflow=enum(0),
                    ModelType=enum(0), AbsoluteZero=-273.15, StefanBoltzmann=5.67e-8, NewtonGravity=6.674e-11),
                _materials=dictionary(material), _sections=dictionary(),
                _constraints=dictionary(), _surfaceInteractions=dictionary(), _contactPairs=dictionary(),
                _amplitudes=dictionary(), _initialConditions=dictionary(),
                _stepCollection=obj('CaeModel.StepCollection', _steps=seq(step)))
    if regions in ('convertible', 'convertible-user-keyword', 'convertible-coordinate-system'):
        part = obj('CaeMesh.MeshPart', _name='part', _active=True, _partId=1, _labels=ints(1),
                   _linearTetraType=enum(10), _linearHexaType=enum(0), _parabolicTetraType=enum(0),
                   _parabolicHexaType=enum(0), _parabolicWedgeType=enum(0))
        contact_output = obj('CaeModel.ContactFieldOutput', _name='contact', _active=False,
                             _variables=enum(3), _lastIterations=True, _contactElements=True, _global=False)
        convertible_step = obj('CaeModel.StaticStep', _name='step', _active=True, _runAnalysis=True,
            _perturbation=False, _nlgeom=True, _maxIncrements=100, _incrementationType=enum(0),
            _solverType=enum(0), _outputFrequency=1, _timePeriod=1.0, _initialTimeIncrement=1.0,
            _minTimeIncrement=1e-5, _maxTimeIncrement=1e30, _direct=False,
            _boundaryConditions=dictionary(), _loads=dictionary(), _fieldOutputs=dictionary(contact_output),
            _historyOutputs=dictionary(), _definedFields=dictionary())
        model[1]['_materials'] = dictionary()
        model[1]['_stepCollection'] = obj('CaeModel.StepCollection', _steps=seq(convertible_step))
        model[1]['_mesh'] = obj('CaeMesh.FeMesh', _parts=dictionary(part), _nodeSets=dictionary(),
            _elementSets=dictionary(), _surfaces=dictionary(), _referencePoints=dictionary(),
            _coordinateSystems=dictionary(), _meshSetupItems=dictionary())
        if regions == 'convertible-user-keyword':
            key = ints(0)
            keyword = obj('FileInOut.Output.Calculix.CalculixUserKeyword', _data='*Preprint, echo=NO',
                          _active=True, _suppressed=False, _keywords=seq())
            model[1]['_calculixUserKeywords'] = obj('CaeGlobals.OrderedDictionary`2',
                _dictionary=obj('System.Collections.Generic.Dictionary`2',
                    KeyValuePairs=[obj('Pair', key=key, value=keyword)]), _list=seq(key))
        if regions == 'convertible-coordinate-system':
            coordinate_system = obj('CaeMesh.CoordinateSystem', _name='fixture-frame', _active=False,
                _type=enum(1), _twoD=False,
                _x1=equation(1), _y1=equation(2), _z1=equation(3),
                _x2=equation(2), _y2=equation(2), _z2=equation(3),
                _x3=equation(1), _y3=equation(3), _z3=equation(3),
                _centerCreatedFrom=enum(0), _pointXCreatedFrom=enum(0), _pointXYCreatedFrom=enum(0),
                _centerCreationIds=None, _centerCreationData=None,
                _pointXCreationIds=None, _pointXCreationData=None,
                _pointXYCreationIds=None, _pointXYCreationData=None)
            model[1]['_mesh'][1]['_coordinateSystems'] = dictionary(coordinate_system)
    elif regions is not None:
        part = obj('CaeMesh.MeshPart', _partId=1, _labels=ints(1), _parabolicTetraType=enum(10),
                   _parabolicHexaType=enum(99 if regions == 'unknown-formulation' else 0),
                   _parabolicWedgeType=enum(0))
        target = 'missing' if regions == 'missing' else '0'
        mesh = obj('CaeMesh.FeMesh', _parts=dictionary(part), _nodeSets=dictionary(obj('NodeSet')),
                   _elementSets=dictionary(obj('ElementSet')),
                   _surfaces=dictionary(obj('Surface', _nodeSetName=target,
                                           _elementFaces=dictionary(target))), _referencePoints=dictionary())
        model[1]['_mesh'] = mesh
        model[1]['_sections'] = dictionary(obj('CaeModel.SolidSection', _active=True,
            _regionType=enum(0), _regionName=target, _materialName=target))
        step[1]['_loads'] = dictionary(obj('CaeModel.CLoad', _active=True,
            _regionType=enum(20 if regions == 'unchecked' else 2), _regionName=target))
    if regions in ('contact-valid', 'contact-missing', 'constraint-unknown'):
        target = 'absent' if regions == 'contact-missing' else '0'
        model[1]['_surfaceInteractions'] = dictionary(obj('Interaction'))
        model[1]['_contactPairs'] = dictionary(obj('CaeModel.ContactPair', _masterRegionType=enum(5),
            _slaveRegionType=enum(5), _masterSurfaceName=target, _slaveSurfaceName='0', _surfaceInteractionName=target))
        model[1]['_constraints'] = dictionary(obj('Unknown' if regions == 'constraint-unknown' else 'CaeModel.Tie',
            _masterRegionType=enum(5), _slaveRegionType=enum(5), _masterRegionName='0', _slaveRegionName=target),
            obj('CaeModel.SurfaceSpring', _masterRegionType=enum(5), _masterRegionName='0'))
    if regions and (regions.startswith('part-') or regions.startswith('face-')):
        model[1]['_geometry'] = None
        for field in ('_nodeSets', '_elementSets', '_surfaces'):
            mesh[1][field] = dictionary()
        if regions == 'part-missing': mesh[1]['_parts'] = dictionary()
        if regions == 'part-mismatch': part[1]['_parabolicTetraType'] = enum(0)
        if regions == 'part-label-mismatch': part[1]['_labels'] = ints(2)
        if regions in ('face-valid', 'face-invalid'):
            element_set = obj('CaeMesh.FeElementSet', _labels=ints(1), _createdFromParts=False)
            mesh[1]['_elementSets'] = dictionary(element_set)
            face_pairs = obj('System.Collections.Generic.Dictionary`2', KeyValuePairs=[
                obj('Pair', key=enum(5 if regions == 'face-invalid' else 4), value='0')])
            mesh[1]['_surfaces'] = dictionary(obj('CaeMesh.FeSurface', _nodeSetName=None, _elementFaces=face_pairs))
    controller_members = dict(_model=model, _commands=[obj('CaeModel.Material', _active=True)])
    if results:
        controller_members['_allResults'] = obj('CaeResults.ResultsCollection', _results=obj(
            'CaeGlobals.OrderedDictionary`2', _dictionary=obj('System.Collections.Generic.Dictionary`2',
                KeyValuePairs=[obj('Pair', key='Case', value=obj('CaeResults.FeResults'))])))
    controller = obj('PrePoMax.Controller', **controller_members)
    # Root id is the next allocated id, 11. Library records are not object ids.
    return (b'\0' + struct.pack('<iiii', 11, -1, 1, 0) + b'\x0c' + struct.pack('<i', 1)
            + text('Fixture') + encode([controller]) + b'\x0b')


def main():
    # SerializedStreamHeader, ArraySingleObject, BinaryObjectString, MessageEnd.
    payload = b'\0' + struct.pack('<iiii', 1, -1, 1, 0)
    payload += b'\x10' + struct.pack('<ii', 1, 1)
    payload += b'\x06' + struct.pack('<i', 2) + b'\x05hello\x0b'
    good = pmx(payload) + b'results-not-decoded'
    cases = [
        ('valid', good, True),
        ('current-model', pmx(model_payload()), True),
        *[(name, pmx(model_payload(regions=name)), True) for name in ('linked', 'missing', 'unchecked', 'unknown-formulation', 'contact-valid', 'contact-missing', 'constraint-unknown')],
        ('invalid-list-size', pmx(model_payload(-1)), False),
        ('oversized-list', pmx(model_payload(100001)), False),
        ('short-header', b'PMX', False),
        ('old-version', pmx(payload, b'PrePoMax v2.0.5'), False),
        ('unknown-header', pmx(payload, b'unknown'), False),
        ('truncated-block', pmx(payload)[:-1], False),
        ('negative-block', good[:32] + struct.pack('<i', -1), False),
        ('compressed-cap', good[:32] + struct.pack('<i', 256 * 1024 * 1024 + 1), False),
        ('record-cap', pmx(b'', declared=128 * 1024 * 1024 + 1), False),
        ('negative-record', pmx(b'', declared=-1), False),
        ('short-record', pmx(payload, declared=len(payload) + 1), False),
        ('invalid-record', pmx(b'not-nrbf'), False),
        ('record-trailing-bytes', pmx(payload + b'junk'), False),
    ]
    with tempfile.TemporaryDirectory(prefix='pmx-inspect-') as directory:
        for name, data, valid in cases:
            source = Path(directory) / (name + '.pmx')
            source.write_bytes(data)
            result = subprocess.run([DOTNET, str(DLL), str(source)], capture_output=True, text=True, timeout=20)
            assert result.returncode == (0 if valid else 1), (name, result.stderr)
            assert source.read_bytes() == data, name + ' modified source'
            if name in ('linked', 'missing', 'unchecked', 'unknown-formulation', 'contact-valid', 'contact-missing', 'constraint-unknown'):
                report = json.loads(result.stdout)['regionMetadata']
                assert report['surfaces'] == 1
                assert len(report['diagnostics']) == {'linked': 0, 'missing': 5, 'unchecked': 1, 'unknown-formulation': 0, 'contact-valid': 0, 'contact-missing': 3, 'constraint-unknown': 1}[name]
                assert report['formulations'][0]['quadraticTetra'] == 'C3D10'
                assert report['formulations'][0]['quadraticHexa'] == ('Unknown:99' if name == 'unknown-formulation' else 'None')
            elif name == 'current-model':
                document = json.loads(result.stdout)
                report = document['activeModel']
                assert report['status'] == 'current-model-collections'
                materials = report['collections']['_materials']
                assert len(materials) == 1 and materials[0]['active'] is False
                assert materials[0]['properties'] == ['CaeModel.Elastic']
                assert len(report['steps']) == 1 and report['steps'][0]['nonlinearGeometry'] is True
                assert document['conversionCapability']['status'] == 'partial-mesh-inspection-required'
            elif valid:
                report = json.loads(result.stdout)
                assert report['recordCount'] == 2 and report['rootRecord'] == 'ArraySingleObject'
                assert report['serializedBytes'] == len(payload)
                assert report['remainingResultsAreaBytes'] == len(b'results-not-decoded')
            else:
                assert not result.stdout and result.stderr.startswith('PMX inspection failed:'), name
        pack = lambda *values: struct.pack('<' + 'i' * len(values), *values)
        raw_deflate = lambda value: zlib.compress(value)[2:-4]
        result_mesh = raw_deflate(pack(0, 0, 0, -1))
        field_data = (pack(1) + b'\x04DISP' + pack(0, -1, -1, 1) + struct.pack('<f', 0.5)
                      + pack(1, 2, 0) + bytes([1, 1, 1, 0]))
        field = (pack(1) + b'\x04DISP' + pack(1, 1) + b'\x02U1' + bytes([0]) + pack(2)
                 + struct.pack('<ff', 1.25, -2.5) + bytes([0]) + pack(0) + bytes([1, 1, 1, 0]))
        field_block = raw_deflate(field_data + field)
        history_entry = raw_deflate(pack(1) + b'\x01E' + pack(1, 2) + struct.pack('<dd', 0.0, 0.5)
                                    + pack(1, 2) + struct.pack('<dd', 10.0, 20.0)
                                    + pack(1, 2, 1, 1) + bytes([0]) + b'\x06@null#')
        history = (pack(1, 1, 7) + b'History' + pack(1, 1, 1, 3) + b'Set' + bytes([0])
                   + pack(1, 1, 1, 2) + b'RF' + pack(1, 1, 1, 1) + b'C' + pack(6) + b'@null#'
                   + pack(1, 1, 1, len(history_entry)) + history_entry + pack(6) + b'@null#')
        result_tail = (pack(1, 4) + b'Case' + pack(1, len(result_mesh)) + result_mesh + pack(1, 1,
                       len(field_block)) + field_block + history)
        source = Path(directory) / 'results.pmx'
        data = pmx(model_payload(results=True)) + result_tail
        source.write_bytes(data)
        result = subprocess.run([DOTNET, str(DLL), str(source), '--results'], capture_output=True, text=True, timeout=20)
        assert result.returncode == 0, result.stderr
        inventory = json.loads(result.stdout)['resultsInventory']
        assert inventory['status'] == 'decoded-results-inventory' and inventory['tailBytes'] == len(result_tail)
        decoded = inventory['results'][0]['fields'][0]['detail']
        assert decoded['name'] == 'DISP' and decoded['StepId'] == 1 and decoded['StepIncrementId'] == 2
        assert decoded['components'][0]['name'] == 'U1' and decoded['components'][0]['values'] == 2
        history_detail = inventory['results'][0]['history']
        assert history_detail['setCount'] == 1
        entry = history_detail['sets'][0]['fields'][0]['components'][0]['entries'][0]['detail']
        assert entry['Name'] == 'E' and entry['timeValues'] == 2 and entry['values'] == 2 and entry['counts'] == 2
        assert source.read_bytes() == data
        truncated = Path(directory) / 'results-truncated.pmx'
        truncated.write_bytes(data[:-1])
        failed = subprocess.run([DOTNET, str(DLL), str(truncated), '--results'], capture_output=True, text=True, timeout=20)
        assert failed.returncode == 1 and failed.stderr.startswith('PMX inspection failed:')
        oversized_field = raw_deflate(field_data + pack(1) + b'\x04DISP' + pack(1, 1)
                                       + b'\x02U1' + bytes([0]) + pack(100_000_001))
        oversized_tail = (pack(1, 4) + b'Case' + pack(1, len(result_mesh)) + result_mesh
                          + pack(1, 1, len(oversized_field)) + oversized_field + pack(-1))
        oversized = Path(directory) / 'results-oversized-component.pmx'
        oversized.write_bytes(pmx(model_payload(results=True)) + oversized_tail)
        failed = subprocess.run([DOTNET, str(DLL), str(oversized), '--results'], capture_output=True, text=True, timeout=20)
        assert failed.returncode == 1 and 'Invalid result component value count' in failed.stderr
    pack = lambda *values: struct.pack('<' + 'i' * len(values), *values)
    nodes = b''.join(struct.pack('<iddd', i + 1, *xyz) for i, xyz in enumerate(
        [(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1)]))
    prefix = pack(0, 1, 4) + nodes
    element = pack(1, 1, 10, 4, 1, 2, 3, 4)
    mesh = prefix + pack(1) + element + pack(0)
    quadratic_nodes = b''.join(struct.pack('<iddd', i, i, 0, 0) for i in range(1, 11))
    quadratic = pack(0, 1, 10) + quadratic_nodes + pack(1, 1, 1, 24, 10, *range(1, 11), 0)
    wedge_nodes = b''.join(struct.pack('<iddd', i, i, 0, 0) for i in range(1, 7))
    unsupported_wedge = pack(0, 1, 6) + wedge_nodes + pack(1, 1, 1, 13, 6, *range(1, 7), 0)
    mesh_cases = [
        ('unsupported-topology', unsupported_wedge, True),
        ('part-valid', quadratic, True),
        ('part-missing', quadratic, False),
        ('part-mismatch', quadratic, False),
        ('part-label-mismatch', quadratic, False),
        ('face-valid', quadratic, True),
        ('face-invalid', quadratic, False),
        ('tetra', mesh, True),
        ('old-layout', mesh, True),
        ('visualization', mesh[:-4] + pack(1) + b'\x04part' + pack(1) + pack(*([-1] * 12)), True),
        ('truncated-visualization', mesh[:-4] + pack(1) + b'\x04part' + pack(1, -1), False),
        ('missing-node', prefix + pack(1) + element[:-4] + pack(99, 0), False),
        ('bad-width', prefix + pack(1, 1, 1, 10, 3, 1, 2, 3, 0), False),
        ('duplicate-element', prefix + pack(2) + element * 2 + pack(0), False),
        ('duplicate-node', pack(0, 1, 2) + nodes[:28] * 2 + pack(0, 0), False),
        ('negative-count', pack(0, 1, -1), False),
        ('huge-count', pack(0, 1, 10000001), False),
        ('truncated-mesh', mesh[:-1], False),
        ('trailing-mesh', mesh + b'x', False),
        ('nonfinite', pack(0, 1, 1, 1) + struct.pack('<ddd', float('nan'), 0, 0) + pack(0, 0), False),
    ]
    with tempfile.TemporaryDirectory(prefix='pmx-mesh-') as directory:
        for name, binary, valid in mesh_cases:
            source = Path(directory) / (name + '.pmx')
            has_metadata = name.startswith('part-') or name.startswith('face-')
            data = pmx(model_payload(regions=name) if has_metadata else payload, version=b'PrePoMax v2.0.6' if name == 'old-layout' else b'PrePoMax v2.5.1', binary=binary)
            source.write_bytes(data)
            result = subprocess.run([DOTNET, str(DLL), str(source), '--mesh'], capture_output=True, text=True, timeout=20)
            assert result.returncode == (0 if valid else 1), (name, result.stderr)
            assert source.read_bytes() == data
            if valid:
                mesh_report = json.loads(result.stdout)['modelBinary']['mesh']
                expected_nodes = 10 if has_metadata else 6 if name == 'unsupported-topology' else 4
                expected_cells = {'24': 1} if has_metadata else {'13': 1} if name == 'unsupported-topology' else {'10': 1}
                assert mesh_report['nodes'] == expected_nodes and mesh_report['elements'] == 1
                assert mesh_report['vtkCellCounts'] == expected_cells
                if name == 'part-valid': assert mesh_report['formulationsChecked'] == 1
                if name == 'part-valid':
                    capability = json.loads(result.stdout)['conversionCapability']
                    assert capability['complete'] is True
                    assert 'mesh.topology' not in [item['code'] for item in capability['blockers']]
                if name == 'unsupported-topology':
                    assert 'mesh.topology' in [item['code'] for item in json.loads(result.stdout)['conversionCapability']['blockers']]
            else:
                assert not result.stdout and result.stderr.startswith('PMX inspection failed:')
        source = Path(directory) / 'convertible.pmx'
        destination = Path(directory) / 'converted.pma'
        data = pmx(model_payload(regions='convertible'), binary=mesh)
        source.write_bytes(data)
        result = subprocess.run([DOTNET, str(DLL), str(source), '--convert', str(destination)],
                                capture_output=True, text=True, timeout=30)
        assert result.returncode == 0, result.stderr
        converted = json.loads(result.stdout)
        assert converted['status'] == 'converted-analysis-model-slice'
        assert converted['nodes'] == 4 and converted['elements'] == 1 and converted['parts'] == 1 and converted['steps'] == 1
        with zipfile.ZipFile(destination) as archive:
            metadata = json.loads(archive.read('model.json'))
            assert metadata['Version'] == 28
            contact = metadata['Steps'][0]['Fields'][0]
            assert contact['Kind'] == 'ContactFieldOutput'
            assert contact['Values']['Variables'] == 3 and contact['Values']['Active'] is False
            assert contact['Values']['LastIterations'] is True and contact['Values']['ContactElements'] is True
            assert contact['Values']['Global'] is False
        assert source.read_bytes() == data and destination.is_file()
        before = destination.read_bytes()
        repeated = subprocess.run([DOTNET, str(DLL), str(source), '--convert', str(destination)],
                                  capture_output=True, text=True, timeout=30)
        assert repeated.returncode == 1 and destination.read_bytes() == before
        source = Path(directory) / 'convertible-user-keyword.pmx'
        destination = Path(directory) / 'converted-user-keyword.pma'
        data = pmx(model_payload(regions='convertible-user-keyword'), binary=mesh)
        source.write_bytes(data)
        result = subprocess.run([DOTNET, str(DLL), str(source), '--convert', str(destination)],
                                capture_output=True, text=True, timeout=30)
        assert result.returncode == 0, result.stderr
        with zipfile.ZipFile(destination) as archive:
            metadata = json.loads(archive.read('model.json'))
            assert metadata['Version'] == 28
            assert metadata['UserKeywords'] == [{'Path': [0], 'Data': '*Preprint, echo=NO'}]
            assert archive.read('analysis.inp').startswith(b'*Preprint, echo=NO\n')
        assert source.read_bytes() == data
        source = Path(directory) / 'convertible-coordinate-system.pmx'
        destination = Path(directory) / 'converted-coordinate-system.pma'
        data = pmx(model_payload(regions='convertible-coordinate-system'), binary=mesh)
        source.write_bytes(data)
        result = subprocess.run([DOTNET, str(DLL), str(source), '--convert', str(destination)],
                                capture_output=True, text=True, timeout=30)
        assert result.returncode == 0, result.stderr
        with zipfile.ZipFile(destination) as archive:
            metadata = json.loads(archive.read('model.json'))
            assert metadata['Version'] == 28
            coordinate_system = metadata['CoordinateSystems'][0]
            assert coordinate_system['Kind'] == 'CoordinateSystem'
            assert coordinate_system['Values']['Name'] == 'fixture-frame'
            assert coordinate_system['Values']['Type'] == 1 and coordinate_system['Values']['Active'] is False
            assert [coordinate_system['Values'][name] for name in ('X1', 'Y1', 'Z1')] == [1, 2, 3]
        assert source.read_bytes() == data
        source = Path(directory) / 'convertible-with-results.pmx'
        destination = Path(directory) / 'converted-with-results.pma'
        data = pmx(model_payload(regions='convertible', results=True), binary=mesh) + result_tail
        source.write_bytes(data)
        result = subprocess.run([DOTNET, str(DLL), str(source), '--convert', str(destination)],
                                capture_output=True, text=True, timeout=30)
        assert result.returncode == 0, result.stderr
        converted = json.loads(result.stdout)
        assert converted['hasBinaryResults'] is True and converted['binaryResultSets'] == 1
        with zipfile.ZipFile(destination) as archive:
            assert 'results.bin' in archive.namelist()
            assert json.loads(archive.read('manifest.json'))['Version'] == 4
        assert source.read_bytes() == data
    print(f'PASS: {len(cases)} record, {len(mesh_cases)} mesh inspection, 3 result inspection, and 5 conversion cases (including coordinate-system, user-keyword, result, and source preservation).')


if __name__ == '__main__':
    main()
