import './workspace.css';
import { ChunkLoader } from './chunkLoader';
import { validateManifest } from './manifestValidator';
import { decodeResource, verifySha256 } from './resourceDecoder';
import { VtkSceneView, type CameraView } from './vtkSceneView';
import type { ResourceDescriptor } from './protocol';

interface TreeNode {
  key: string; kind: string; name: string; step: string | null; details: string;
  editor: string | null; creator: string | null; regionKind: string | null; children: TreeNode[];
}
interface Material { id: string; name: string; youngsModulus: number; poissonsRatio: number }
interface Force { name: string; step: string; region: string; f1: number; f2: number; f3: number }
interface ResultField { name: string; component: string; step: number; increment: number; time: number }
interface State {
  revision: number; hasProject: boolean; canUndo?: boolean; canRedo?: boolean;
  summary?: { name: string; nodes: number; elements: number; nodeSets: string[]; canSolve: boolean };
  tree?: TreeNode[]; materials?: Material[]; sections?: { section: string; material: string }[];
  steps?: { name: string }[]; forces?: Force[]; fields?: ResultField[];
  materialIdentities?: { id: string; name: string }[];
}

function element<T extends HTMLElement = HTMLElement>(id: string): T {
  const value = document.getElementById(id);
  if (!value) throw new Error(`Missing workspace element ${id}.`);
  return value as T;
}
const action = element<HTMLSelectElement>('edit-action');
const fieldSelect = element<HTMLSelectElement>('result-field');
const status = element<HTMLOutputElement>('web-status');
const viewport = element('web-viewport');
const progress = element<HTMLProgressElement>('web-progress');
let state: State = { revision: 0, hasProject: false };
let view: VtkSceneView | null = null;
let selectedKey: string | null = null;
let displayingField = false;
let busy = false;
let loading: AbortController | null = null;

async function api(path: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers);
  if (init.method && init.method !== 'GET') {
    headers.set('X-Workspace-Request', '1');
    headers.set('If-Match', String(state.revision));
  }
  const response = await fetch(path, { ...init, headers, credentials: 'same-origin' });
  if (!response.ok) {
    const error = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(error?.message ?? `Request failed (${response.status}).`);
  }
  return response;
}
async function json(path: string, value: object): Promise<unknown> {
  return (await api(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(value) })).json();
}
function reportError(message: string): void {
  element('web-error').textContent = message;
  element('web-error').hidden = false;
}
async function run(task: () => Promise<void>): Promise<void> {
  if (busy) return;
  busy = true;
  document.body.setAttribute('aria-busy', 'true');
  document.querySelectorAll<HTMLElement>('.web-header,.project-bar,.web-workspace,dialog').forEach(node => { node.inert = true; });
  element('web-error').hidden = true;
  try { await task(); }
  catch (error) { reportError(error instanceof Error ? error.message : String(error)); status.textContent = 'Operation stopped. Your last accepted project revision is available.'; }
  finally {
    busy = false;
    progress.hidden = true;
    document.body.setAttribute('aria-busy', 'false');
    document.querySelectorAll<HTMLElement>('.web-header,.project-bar,.web-workspace,dialog').forEach(node => { node.inert = false; });
  }
}
function tab(results: boolean): void {
  element('prepare-panel').hidden = results;
  element('results-panel').hidden = !results;
  element('prepare-tab').classList.toggle('active', !results);
  element('results-tab').classList.toggle('active', results);
}
element('prepare-tab').onclick = () => tab(false);
element('results-tab').onclick = () => tab(true);
const importDialog = element<HTMLDialogElement>('import-dialog');
element('open-project').onclick = element('empty-open').onclick = () => importDialog.showModal();
element('cancel-import').onclick = () => importDialog.close();

async function refresh(): Promise<void> {
  state = await (await api('/api/workspace')).json() as State;
  const summary = state.summary;
  element('project-name').textContent = summary?.name ?? 'Untitled workspace';
  element('project-summary').textContent = summary ? `${summary.nodes.toLocaleString()} nodes · ${summary.elements.toLocaleString()} elements` : 'Open a project or import a volume mesh to begin.';
  element('revision').textContent = state.hasProject ? `Revision ${state.revision}` : 'No project';
  for (const id of ['save-project', 'export-input', 'apply-edit', 'edit-action', 'result-file', 'result-field', 'deformation', 'show-field'])
    (element(id) as HTMLButtonElement | HTMLInputElement).disabled = !state.hasProject;
  element<HTMLButtonElement>('undo').disabled = !state.canUndo;
  element<HTMLButtonElement>('redo').disabled = !state.canRedo;
  const oldField = fieldSelect.value;
  fieldSelect.replaceChildren(new Option('Mesh geometry', ''));
  (state.fields ?? []).forEach((field, index) => fieldSelect.add(new Option(`${field.name} · ${field.component} · step ${field.step}, inc ${field.increment} · t=${field.time}`, String(index))));
  if (Array.from(fieldSelect.options).some(option => option.value === oldField)) fieldSelect.value = oldField;
  const expanded = new Set(Array.from(element('model-tree').querySelectorAll<HTMLDetailsElement>('details[open]')).map(node => node.dataset.key));
  const tree = element('model-tree');
  const initial = tree.querySelector('details') === null;
  tree.replaceChildren();
  function append(nodes: TreeNode[], parent: HTMLElement): void {
    for (const node of nodes) {
      if (node.kind === 'collection' || node.children.length > 0 || node.kind === 'model') {
        const details = document.createElement('details');
        details.dataset.key = node.key;
        details.open = initial ? ['model', 'model/mesh', 'model/materials', 'model/steps'].includes(node.key) : expanded.has(node.key);
        const title = document.createElement('summary'); title.textContent = node.name;
        title.addEventListener('click', () => { if (node.creator) inspect(node); });
        details.append(title); append(node.children, details); parent.append(details);
      } else {
        const button = document.createElement('button'); button.textContent = node.name;
        button.classList.toggle('selected', node.key === selectedKey);
        button.onclick = () => void run(async () => {
          inspect(node);
          if (node.regionKind && view) {
            const selection = await (await api('/api/workspace/selection?key=' + encodeURIComponent(node.key))).json() as { nodes: number[] };
            view.setSelection('node', selection.nodes, [], [], []);
          }
        });
        parent.append(button);
      }
    }
  }
  append(state.tree ?? [], tree);
  if (selectedKey) {
    const walk = (nodes: TreeNode[]): TreeNode[] => nodes.flatMap(node => [node, ...walk(node.children)]);
    const selected = walk(state.tree ?? []).find(node => node.key === selectedKey);
    if (selected) inspect(selected, false);
    else { selectedKey = null; element('object-info').textContent = 'Select an object in the model tree to inspect it.'; renderEditor(); }
  } else renderEditor();
}

function inspect(node: TreeNode, navigate = true): void {
  selectedKey = node.key;
  if (navigate) tab(false);
  element('model-tree').querySelectorAll('button').forEach(button => button.classList.toggle('selected', button.textContent === node.name));
  const title = document.createElement('h2'); title.textContent = node.name;
  const details = document.createElement('p'); details.className = 'muted'; details.textContent = node.details || node.kind;
  element('object-info').replaceChildren(title, details);
  const target = node.editor === 'material' ? 'material-save' : node.kind === 'section' ? 'section-assign' : node.editor === 'force' ? 'force-save'
    : node.creator === 'node-set' ? 'nodeset-add' : node.creator === 'material' ? 'material-add' : node.creator === 'step' ? 'step-add' : null;
  if (target) { action.value = target; renderEditor(node); }
  else {
    const note = document.createElement('p'); note.className = 'hint'; note.textContent = 'This object is inspectable. Its full editor is not available in this web pilot.';
    element('object-info').append(note);
    renderEditor();
  }
}

function renderEditor(selected?: TreeNode): void {
  const parent = element('editor-fields'); parent.replaceChildren();
  function input(name: string, label: string, value: string | number = '', type = 'text'): HTMLInputElement {
    const wrapper = document.createElement('label'); wrapper.className = 'field'; wrapper.textContent = label;
    const control = document.createElement('input'); control.name = name; control.required = true; control.type = type; control.value = String(value);
    if (type === 'number') control.step = 'any'; else control.maxLength = 128;
    wrapper.append(control); parent.append(wrapper); return control;
  }
  function select(name: string, label: string, options: string[], value?: string): HTMLSelectElement {
    const wrapper = document.createElement('label'); wrapper.className = 'field'; wrapper.textContent = label;
    const control = document.createElement('select'); control.name = name; control.required = true;
    options.forEach(option => control.add(new Option(option, option))); if (value) control.value = value;
    wrapper.append(control); parent.append(wrapper); return control;
  }
  const kind = action.value;
  if (kind === 'material-remove') {
    const assigned = new Set((state.sections ?? []).map(item => item.material.toLowerCase()));
    const unused = (state.materialIdentities ?? []).filter(item => !assigned.has(item.name.toLowerCase()));
    select('name', 'Unused material', unused.map(item => item.name), selected?.name);
    const hint = document.createElement('p'); hint.className = 'hint';
    hint.textContent = unused.length ? 'Remove this unused material. Undo restores it.' : 'All materials are assigned. Reassign their sections before removal.';
    parent.append(hint);
  } else if (kind === 'material-rename') {
    const materials = state.materialIdentities ?? [];
    select('name', 'Material', materials.map(item => item.name), selected?.name);
    input('newName', 'New material name', 'Renamed-material');
  } else if (kind === 'material-add' || kind === 'material-save') {
    const material = state.materials?.find(item => item.name === selected?.name) ?? state.materials?.[0];
    if (kind === 'material-save') {
      const control = select('name', 'Material', (state.materials ?? []).map(item => item.name), material?.name);
      control.onchange = () => {
        const item = state.materials?.find(value => value.name === control.value);
        if (item) { (parent.querySelector('[name=e]') as HTMLInputElement).value = String(item.youngsModulus); (parent.querySelector('[name=nu]') as HTMLInputElement).value = String(item.poissonsRatio); }
      };
    } else input('name', 'Material name', 'New-material');
    input('e', 'Elastic modulus', kind === 'material-save' ? material?.youngsModulus ?? 210000 : 210000, 'number');
    input('nu', 'Poisson ratio', kind === 'material-save' ? material?.poissonsRatio ?? .3 : .3, 'number');
  } else if (kind === 'section-assign') {
    select('name', 'Solid section', (state.sections ?? []).map(item => item.section), selected?.name);
    select('region', 'Material', (state.materials ?? []).map(item => item.name));
  } else if (kind === 'step-add') input('name', 'Static step name', 'New-step');
  else if (kind === 'nodeset-add') {
    input('name', 'Node set name', 'New-set');
    const label = document.createElement('label'); label.className = 'field'; label.textContent = 'Node IDs (comma or space separated)';
    const ids = document.createElement('textarea'); ids.name = 'nodes'; ids.required = true; ids.maxLength = 1000000; label.append(ids); parent.append(label);
  } else {
    const force = state.forces?.find(item => item.name === selected?.name && item.step === selected?.step) ?? state.forces?.[0];
    const step = select('step', 'Analysis step', (state.steps ?? []).map(item => item.name), selected?.step ?? force?.step);
    if (kind === 'force-save') {
      const names = select('name', 'Force', []);
      const updateForce = () => {
        const value = state.forces?.find(item => item.name === names.value && item.step === step.value);
        if (value) for (const key of ['f1', 'f2', 'f3'] as const) (parent.querySelector(`[name=${key}]`) as HTMLInputElement).value = String(value[key]);
      };
      const updateNames = () => {
        names.replaceChildren(...(state.forces ?? []).filter(item => item.step === step.value).map(item => new Option(item.name, item.name)));
        if (force?.step === step.value) names.value = force.name;
        updateForce();
      };
      for (const key of ['f1', 'f2', 'f3'] as const) input(key, key.toUpperCase(), force?.[key] ?? 0, 'number');
      step.onchange = updateNames; names.onchange = updateForce; updateNames();
    } else {
      input('name', kind === 'support-add' ? 'Support name' : 'Force name', kind === 'support-add' ? 'Fixed-support' : 'Nodal-force');
      select('region', 'Node set', state.summary?.nodeSets ?? []);
      if (kind === 'force-add') {
        for (const key of ['f1', 'f2', 'f3']) input(key, `${key.toUpperCase()} (per node)`, 0, 'number');
      }
    }
  }
}
action.onchange = () => renderEditor();

element<HTMLFormElement>('edit-form').onsubmit = event => {
  event.preventDefault();
  void run(async () => {
    status.textContent = 'Validating model edit…';
    const data = new FormData(element<HTMLFormElement>('edit-form'));
    const edit: Record<string, unknown> = { action: action.value };
    for (const [name, value] of data.entries()) {
      if (name === 'nodes') {
        const tokens = String(value).trim().split(/[\s,;]+/);
        if (tokens.some(token => !/^\d+$/.test(token))) throw new Error('Enter whole, positive node IDs separated by commas or spaces.');
        edit.nodes = tokens.map(Number);
      } else edit[name] = ['e', 'nu', 'f1', 'f2', 'f3'].includes(name) ? Number(value) : String(value);
    }
    if (edit.action === 'material-add') {
      await json('/api/workspace/commands/material/create', {
        operationId: crypto.randomUUID(), expectedRevision: state.revision,
        name: edit.name, youngsModulus: edit.e, poissonsRatio: edit.nu,
      });
    } else if (edit.action === 'material-save') {
      const material = state.materials?.find(item => item.name === edit.name);
      if (!material) throw new Error('Refresh and select an existing material.');
      await json('/api/workspace/commands/material', {
        operationId: crypto.randomUUID(), expectedRevision: state.revision,
        materialId: material.id, youngsModulus: edit.e, poissonsRatio: edit.nu,
      });
    } else if (edit.action === 'material-rename') {
      const material = state.materialIdentities?.find(item => item.name === edit.name);
      if (!material) throw new Error('Refresh and select an existing material.');
      await json('/api/workspace/commands/material/rename', {
        operationId: crypto.randomUUID(), expectedRevision: state.revision,
        materialId: material.id, newName: edit.newName,
      });
      selectedKey = null;
    } else if (edit.action === 'material-remove' || edit.action === 'section-assign') {
      const material = state.materialIdentities?.find(item => item.name === (edit.action === 'material-remove' ? edit.name : edit.region));
      if (!material) throw new Error('Refresh and select an existing material.');
      const command = { operationId: crypto.randomUUID(), expectedRevision: state.revision, materialId: material.id };
      if (edit.action === 'material-remove') {
        await json('/api/workspace/commands/material/remove', command);
        selectedKey = null;
      } else await json('/api/workspace/commands/material/assign', { ...command, sectionName: edit.name });
    } else await json('/api/workspace/edit', edit);
    fieldSelect.value = '';
    await refresh(); await displayScene(); status.textContent = `Saved model revision ${state.revision}. Previous results cleared.`;
  });
};
element<HTMLFormElement>('import-form').onsubmit = event => {
  event.preventDefault();
  const file = element<HTMLInputElement>('import-file').files?.[0]; if (!file) return;
  importDialog.close();
  void run(async () => {
    status.textContent = `Uploading ${file.name}…`;
    await api('/api/workspace/import', { method: 'POST', headers: {
      'Content-Type': 'application/octet-stream', 'X-File-Extension': file.name.slice(file.name.lastIndexOf('.')),
      'X-Youngs-Modulus': element<HTMLInputElement>('import-e').value, 'X-Poissons-Ratio': element<HTMLInputElement>('import-nu').value,
    }, body: file });
    selectedKey = null; fieldSelect.value = '';
    await refresh(); await displayScene(); status.textContent = 'Project opened. Select an object to inspect or edit it.';
  });
};
element<HTMLInputElement>('result-file').onchange = () => {
  const control = element<HTMLInputElement>('result-file'); const file = control.files?.[0]; if (!file) return;
  void run(async () => {
    try {
      status.textContent = 'Uploading results and checking the result mesh…';
      await api('/api/workspace/results', { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: file });
      await refresh(); fieldSelect.value = state.fields?.length ? '0' : '';
      await displayScene(); status.textContent = 'External results attached. Select a field and frame to explore.';
    } finally { control.value = ''; }
  });
};
for (const operation of ['undo', 'redo']) element(operation).onclick = () => void run(async () => {
  await api('/api/workspace/' + operation, { method: 'POST' });
  fieldSelect.value = ''; await refresh(); await displayScene(); status.textContent = `Restored project revision ${state.revision}.`;
});
element('refresh').onclick = () => void run(async () => { await refresh(); if (state.hasProject) await displayScene(); status.textContent = 'Workspace refreshed.'; });
element('show-field').onclick = () => void run(displayScene);
element('fit').onclick = () => view?.resetCamera();
element<HTMLSelectElement>('camera').onchange = event => view?.setCameraView((event.target as HTMLSelectElement).value as CameraView);
element<HTMLInputElement>('scalar-colors').onchange = event => {
  const visible = (event.target as HTMLInputElement).checked;
  view?.setContoursVisible(visible);
  const legend = viewport.querySelector<HTMLElement>('.scalar-legend');
  if (legend) legend.hidden = !visible || !displayingField;
};

async function download(kind: string): Promise<void> {
  const response = await api('/api/workspace/download/' + kind);
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement('a'); link.href = url; link.download = `analysis-r${state.revision}.${kind === 'project' ? 'pma' : 'inp'}`;
  document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 60000);
  status.textContent = kind === 'project' ? 'Project downloaded. Reopen the PMA to continue later.' : 'Input downloaded. Run CalculiX externally, then upload its FRD under Results.';
}
element('save-project').onclick = () => void run(() => download('project'));
element('export-input').onclick = () => void run(() => download('input'));

async function displayScene(): Promise<void> {
  loading?.abort(); loading = new AbortController();
  const signal = loading.signal;
  status.textContent = 'Preparing the 3D scene…';
  view?.delete(); view = null; displayingField = false; viewport.replaceChildren();
  const field = fieldSelect.value === '' ? null : Number(fieldSelect.value);
  const deformationScale = field === null ? 0 : Number(element<HTMLInputElement>('deformation').value);
  const scene = await json('/api/workspace/scene', { revision: state.revision, field, deformationScale }) as { sceneId: string; points: number; triangles: number };
  const manifest = validateManifest(await (await api(`/api/v1/scenes/${scene.sceneId}/manifest`)).json());
  view = new VtkSceneView(viewport, reportError);
  const activeView = view;
  activeView.setParallelProjection(true); activeView.setWorldOrigin(manifest.worldOrigin); activeView.setField(manifest.field);
  activeView.setContoursVisible(element<HTMLInputElement>('scalar-colors').checked);
  displayingField = field !== null;
  activeView.setScalarLegend({ type: 'prepomax.scalar-legend.updated-v1', visible: displayingField && element<HTMLInputElement>('scalar-colors').checked,
    fieldName: manifest.field.name, componentName: manifest.field.component, unit: manifest.field.units,
    complexComponent: '', minMaxType: 'Automatic', numberFormat: 'G4', drawBackground: true, drawBorder: false,
    minimum: manifest.field.displayedRange[0], maximum: manifest.field.displayedRange[1],
    colors: [[.23, .30, .75], [.55, .69, .99], [.86, .86, .86], [.96, .6, .48], [.71, .02, .15]],
  });
  let first = true;
  const loader = new ChunkLoader({ getResource: async (descriptor: ResourceDescriptor, stop?: AbortSignal) => {
    const url = new URL(descriptor.uri, window.location.origin);
    if (url.origin !== window.location.origin || !url.pathname.startsWith(`/api/v1/scenes/${scene.sceneId}/resources/`) || url.search || url.hash)
      throw new Error('The renderer returned an invalid resource address.');
    const bytes = await (await api(url.pathname, { signal: stop ?? null })).arrayBuffer();
    await verifySha256(descriptor, bytes); return decodeResource(descriptor, bytes);
  } });
  progress.hidden = false;
  await loader.load(manifest, {
    onGeometry: geometry => { activeView.addGeometry(geometry); if (first) { activeView.resetCamera(); first = false; } },
    onAttributes: attributes => activeView.addAttributes(attributes),
    onProgress: value => { progress.max = value.totalResources; progress.value = value.completedResources; status.textContent = `Loading 3D resources ${value.completedResources}/${value.totalResources}…`; },
  }, signal);
  activeView.setCameraView(element<HTMLSelectElement>('camera').value as CameraView);
  element<HTMLButtonElement>('fit').disabled = false; element<HTMLSelectElement>('camera').disabled = false;
  element('scene-counts').textContent = `${scene.points.toLocaleString()} points · ${scene.triangles.toLocaleString()} triangles`;
  element('field-summary').textContent = field !== null ? `Range ${manifest.field.displayedRange.map(value => value.toPrecision(5)).join(' to ')} ${manifest.field.units}` : 'Undeformed mesh geometry';
  status.textContent = field !== null ? 'Result field displayed.' : 'Mesh geometry displayed.';
}
window.addEventListener('beforeunload', () => { loading?.abort(); view?.delete(); });
void run(async () => { await api('/api/workspace', { method: 'POST' }); await refresh(); if (state.hasProject) await displayScene(); else status.textContent = 'Workspace ready. Open a project or import a volume mesh.'; });
