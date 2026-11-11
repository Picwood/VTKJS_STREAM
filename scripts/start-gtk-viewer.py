#!/usr/bin/env python3
"""PrePoMax Linux GTK/WebKitGTK host with serialized asynchronous companion IPC."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
import math
import os
from pathlib import Path
import select
import signal
import subprocess
import sys
import time
import urllib.request
from urllib.parse import quote
import gi
sys.dont_write_bytecode = True
from linux_edit_session import EditSession
from linux_node_selection import NodeSelection

gi.require_version('Gtk', '3.0')
gi.require_version('WebKit2', '4.1')
gi.require_foreign('cairo')
from gi.repository import Gtk, WebKit2, GLib, GLibUnix, Gdk, Gio, GdkPixbuf

APPLICATION_ID = 'si.um.fs.prepomax.LinuxViewer'

parser = argparse.ArgumentParser(description=__doc__)
for name in ('dotnet', 'project', 'server', 'client_root'):
    parser.add_argument(name)
parser.add_argument('--companion', help='Published viewer companion DLL (defaults to the checkout build)')
parser.add_argument('--converter', help='Published PrePoMax.PmxInspect DLL (defaults to the checkout build)')
parser.add_argument('--smoke', action='store_true')
parser.add_argument('--smoke-empty', action='store_true', help='Check empty startup and guarded model actions')
parser.add_argument('--smoke-model-tree', action='store_true', help='Navigate real model objects, preview a region and open its selected-object editor')
parser.add_argument('--smoke-edit-session', help='Apply a tree edit, undo/redo and save the session to this new PMA')
parser.add_argument('--smoke-element-sets', help='Author element sets and a section through the reference tree, then save to this new PMA')
parser.add_argument('--smoke-region-maintenance', help='Edit node sets and surfaces through the reference tree, then save this new PMA')
parser.add_argument('--smoke-node-selection', help='Exercise live mesh node-set selection from the tree, then save this new PMA')
parser.add_argument('--smoke-tree-highlight', action='store_true', help='Verify automatic tree node-set highlights without changing the view')
parser.add_argument('--smoke-import-mesh', help='Select this mesh through the import chooser')
parser.add_argument('--smoke-import-output', help='Output project for the import smoke')
parser.add_argument('--smoke-import-failure', action='store_true', help='Expect import refusal and preserve the current project')
parser.add_argument('--smoke-mesh-geometry', help='Select this CAD geometry through the Gmsh chooser')
parser.add_argument('--smoke-mesh-output', help='Output project for the Gmsh geometry smoke')
parser.add_argument('--smoke-cad-topology', action='store_true', help='Inspect the retained CAD entity boundary graph in GTK')
parser.add_argument('--smoke-face-size', action='store_true', help='Select and retain a CAD face refinement in remesh smokes')
parser.add_argument('--smoke-size-box', action='store_true', help='Use a saved local refinement box in CAD meshing smokes')
parser.add_argument('--smoke-second-order', action='store_true', help='Enable quadratic tetrahedra in CAD meshing smokes')
parser.add_argument('--smoke-remesh-output', help='Remesh the open project retained CAD to this new PMA')
parser.add_argument('--smoke-import-pmx', help='Select this PMX through the open chooser')
parser.add_argument('--smoke-pmx-output', help='Output PMA for the PMX conversion smoke')
parser.add_argument('--smoke-pmx-failure', action='store_true', help='Expect PMX conversion refusal and preserve the current project')
parser.add_argument('--smoke-node-set-box', action='store_true', help='Use the maximum-X plane in node-set smoke')
parser.add_argument('--smoke-node-set', help='Create a node set, save and verify reopening')
parser.add_argument('--smoke-surface', help='Create an element surface, save and verify reopening')
parser.add_argument('--smoke-surface-pick', action='store_true', help='Select the smoke surface by a validated renderer click')
parser.add_argument('--smoke-surface-area', action='store_true', help='Select the smoke surface by a validated renderer rectangle')
parser.add_argument('--smoke-surface-angle', action='store_true', help='Grow the smoke surface from a renderer double-click')
parser.add_argument('--smoke-boundary-action', choices=('edit', 'constrain', 'release', 'add', 'remove'), default='edit')
parser.add_argument('--smoke-boundary', help='Edit a prescribed translation, save and reopen a new project')
parser.add_argument('--smoke-fixed-support', help='Create a fixed support, save and reopen a new project')
parser.add_argument('--smoke-load-action', choices=('edit', 'add', 'add-traction', 'remove'), default='edit')
parser.add_argument('--smoke-load', help='Edit the first force load, save a new project and verify reopening')
parser.add_argument('--smoke-pressure', help='Create a surface pressure, save a new project and verify reopening')
parser.add_argument('--smoke-material', help='Edit the first elastic material, save a new project and verify reopening')
parser.add_argument('--smoke-material-action', choices=('edit', 'density', 'remove'), default='edit')
parser.add_argument('--smoke-plasticity', help='Set, edit or remove material plasticity, save a new project and verify reopening')
parser.add_argument('--smoke-plasticity-action', choices=('set', 'edit', 'remove'), default='set')
parser.add_argument('--smoke-gravity', help='Create a gravity load, save a new project and verify reopening')
parser.add_argument('--smoke-gravity-action', choices=('add', 'edit', 'remove'), default='add')
parser.add_argument('--smoke-centrifugal', help='Create a centrifugal load, save a new project and verify reopening')
parser.add_argument('--smoke-centrifugal-action', choices=('add', 'edit', 'remove'), default='add')
parser.add_argument('--smoke-section', help='Save and reopen a solid section operation')
parser.add_argument('--smoke-section-region', help='RegionType:Name selected by the section smoke')
parser.add_argument('--smoke-section-action', choices=('add', 'edit', 'duplicate', 'activate', 'deactivate', 'remove'), default='add')
parser.add_argument('--smoke-material-only', action='store_true', help='Create an elastic material without section assignment')
parser.add_argument('--smoke-step', help='Save and reopen a static step operation')
parser.add_argument('--smoke-step-action', choices=('add', 'edit', 'duplicate', 'activate', 'deactivate', 'remove'), default='add')
parser.add_argument('--smoke-field-output', help='Save and reopen a field-output operation')
parser.add_argument('--smoke-field-output-action', choices=('add', 'edit', 'duplicate', 'propagate', 'activate', 'deactivate', 'remove'), default='add')
parser.add_argument('--smoke-history-output', help='Save and reopen a history-output operation')
parser.add_argument('--smoke-history-output-action', choices=('add', 'edit', 'duplicate', 'propagate', 'activate', 'deactivate', 'remove'), default='add')
parser.add_argument('--smoke-history-results', help='Browse, plot and export history results to this new CSV')
parser.add_argument('--smoke-add-material', help='Create an elastic material, assign it to a solid section and verify reopening')
parser.add_argument('--smoke-assign-material', help='Assign an existing material to a solid section and verify reopening')
parser.add_argument('--smoke-occlusion', action='store_true', help='Cover the native window past both deadlines, then require same-page completion')
parser.add_argument('--smoke-frame-recovery', action='store_true', help='Stall animation callbacks after resources load, then retry')
parser.add_argument('--smoke-bridge-pressure', action='store_true', help='Flood ignored and duplicate notifications before scene acceptance')
parser.add_argument('--view-timeout', type=float, default=60, help='Seconds allowed for a page to load and apply its scene before offering Retry view')
parser.add_argument('--smoke-bootstrap-recovery', action='store_true', help='Reject one page handoff and verify explicit failure and retry')
parser.add_argument('--smoke-startup-recovery', action='store_true', help='Withhold one bootstrap and verify startup timeout and retry')
parser.add_argument('--smoke-timeout', type=float, default=90, help='Smoke deadline in seconds (default: 90)')
parser.add_argument('--smoke-focus', action='store_true', help='Check F6 focus switching across the embedded page boundary')
parser.add_argument('--smoke-accessibility', action='store_true', help='Check native control labels, focus, shortcuts and display scale')
parser.add_argument('--smoke-clipboard', action='store_true', help='Exercise Copy summary using a private GTK selection')
parser.add_argument('--smoke-recovery', action='store_true', help='Terminate WebKit after a stable frame and verify Retry view')
parser.add_argument('--smoke-gpu', action='store_true', help='Require WebGL 2 on a hardware renderer during the normal render/reload smoke')
parser.add_argument('--deformation-scale', type=float, default=0, help='Initial result displacement scale (-1000000 to 1000000)')
parser.add_argument('--solver', help='CalculiX executable; otherwise resolve ccx on PATH')
parser.add_argument('--gmsh', default=os.environ.get('PREPOMAX_GMSH'), help='Gmsh executable; otherwise resolve gmsh on PATH')
parser.add_argument('--job-root', default=str(Path(os.environ.get('XDG_STATE_HOME', str(Path.home() / '.local/state'))) / 'prepomax/jobs'))
parser.add_argument('--smoke-field', help='Select FIELD/COMPONENT after initial render')
parser.add_argument('--smoke-solve', action='store_true', help='Solve after rendering and require a successful result archive')
parser.add_argument('--smoke-cancel', action='store_true', help='Cancel after solver starts and require cancellation')
parser.add_argument('--smoke-switch', help='Second project for native-dialog switching acceptance')
args = parser.parse_args()
if bool(args.smoke_import_mesh) != bool(args.smoke_import_output) or (args.smoke_import_failure and not args.smoke_import_mesh):
    parser.error('import smoke requires both --smoke-import-mesh and --smoke-import-output')
if bool(args.smoke_mesh_geometry) != bool(args.smoke_mesh_output):
    parser.error('Gmsh geometry smoke requires both --smoke-mesh-geometry and --smoke-mesh-output')
if args.smoke_remesh_output and args.project == '-':
    parser.error('retained CAD remesh smoke requires an initial project')
if bool(args.smoke_import_pmx) != bool(args.smoke_pmx_output):
    parser.error('PMX import smoke requires both --smoke-import-pmx and --smoke-pmx-output')
if args.smoke_pmx_failure and not args.smoke_import_pmx:
    parser.error('--smoke-pmx-failure requires a PMX import smoke')
if args.smoke_surface_pick and not args.smoke_surface:
    parser.error('--smoke-surface-pick requires --smoke-surface')
if args.smoke_surface_area and not args.smoke_surface:
    parser.error('--smoke-surface-area requires --smoke-surface')
if args.smoke_surface_angle and not args.smoke_surface:
    parser.error('--smoke-surface-angle requires --smoke-surface')
if sum((args.smoke_surface_pick, args.smoke_surface_area, args.smoke_surface_angle)) > 1:
    parser.error('choose one surface picking smoke mode')
if not math.isfinite(args.view_timeout) or args.view_timeout <= 0:
    parser.error('view timeout must be finite and positive')
if not math.isfinite(args.smoke_timeout) or args.smoke_timeout <= 0:
    parser.error('smoke timeout must be finite and positive')
args.smoke = bool(args.smoke_history_results) or bool(args.smoke_history_output) or bool(args.smoke_field_output) or bool(args.smoke_section) or bool(args.smoke_step) or bool(args.smoke_plasticity) or args.smoke or args.smoke_empty or args.smoke_recovery or args.smoke_clipboard or args.smoke_accessibility or args.smoke_focus or args.smoke_startup_recovery or args.smoke_bootstrap_recovery or args.smoke_bridge_pressure or args.smoke_frame_recovery or args.smoke_occlusion or args.smoke_gpu or bool(args.smoke_material) or bool(args.smoke_add_material) or bool(args.smoke_assign_material) or bool(args.smoke_gravity) or bool(args.smoke_centrifugal) or bool(args.smoke_load) or bool(args.smoke_pressure) or bool(args.smoke_boundary) or bool(args.smoke_fixed_support) or bool(args.smoke_node_set) or bool(args.smoke_surface) or bool(args.smoke_import_mesh) or bool(args.smoke_mesh_geometry) or bool(args.smoke_remesh_output) or bool(args.smoke_import_pmx)
args.smoke = args.smoke or args.smoke_model_tree or bool(args.smoke_edit_session) or bool(args.smoke_element_sets) or bool(args.smoke_region_maintenance)
args.smoke = args.smoke or bool(args.smoke_node_selection)
args.smoke = args.smoke or args.smoke_tree_highlight
if args.smoke_clipboard and (Gdk.Display.get_default() is None or Gdk.Display.get_default().__gtype__.name != 'GdkX11Display'):
    parser.error('--smoke-clipboard requires GDK_BACKEND=x11 for a private test selection')
if not math.isfinite(args.deformation_scale) or abs(args.deformation_scale) > 1000000:
    parser.error('displacement scale must be finite and between -1000000 and 1000000')
repo = Path(__file__).resolve().parent.parent
converter = Path(args.converter).resolve() if args.converter else repo / 'portable/PrePoMax.PmxInspect/bin/Release/net10.0/PrePoMax.PmxInspect.dll'
if not converter.is_file():
    parser.error('PMX converter not found: ' + str(converter))
process = subprocess.Popen([str(Path(args.dotnet).resolve()),
    str(Path(args.companion).resolve() if args.companion else repo / 'portable/PrePoMax.ViewerHost/bin/Release/net10.0/PrePoMax.ViewerHost.dll'),
    ("-" if args.project == "-" else str(Path(args.project).resolve())), str(Path(args.dotnet).resolve()), str(Path(args.server).resolve())],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, start_new_session=True,
    env={**os.environ, 'PREPOMAX_RENDER_CLIENT_ROOT': str(Path(args.client_root).resolve())})

# Exactly one worker owns stdin/stdout: responses cannot be paired with the wrong request.
worker = ThreadPoolExecutor(max_workers=1)
MAX_COMPANION_RESPONSE = 2 * 1024 * 1024

def exchange(request):
    if request is not None:
        process.stdin.write(json.dumps(request) + '\n')
        process.stdin.flush()
    if not select.select([process.stdout], [], [], 40)[0]:
        raise RuntimeError('Viewer companion timed out')
    line = process.stdout.readline(MAX_COMPANION_RESPONSE + 1)
    if not line.endswith('\n'):
        raise RuntimeError('Viewer companion disconnected or returned an oversized message')
    return json.loads(line)


def descendant_render_nodes(root_pid):
    descendants = {root_pid}
    for _ in range(8):
        previous = len(descendants)
        for status in Path('/proc').glob('[0-9]*/status'):
            try:
                parent = next(line for line in status.read_text().splitlines() if line.startswith('PPid:'))
                if int(parent.split()[1]) in descendants:
                    descendants.add(int(status.parent.name))
            except (FileNotFoundError, PermissionError, StopIteration, ValueError):
                continue
        if len(descendants) == previous:
            break
    nodes = set()
    for pid in descendants - {root_pid}:
        try:
            descriptors = (Path('/proc') / str(pid) / 'fd').iterdir()
            for descriptor in descriptors:
                try:
                    target = os.readlink(descriptor)
                    if target.startswith('/dev/dri/renderD'):
                        nodes.add(target)
                except (FileNotFoundError, PermissionError, OSError):
                    continue
        except (FileNotFoundError, PermissionError):
            continue
    return sorted(nodes)


def describe_render_node(node):
    name = Path(node).name
    device = Path('/sys/class/drm') / name / 'device'
    try:
        vendor = (device / 'vendor').read_text().strip()
        driver = (device / 'driver').resolve().name
        return '%s(%s,%s)' % (name, vendor, driver)
    except (FileNotFoundError, PermissionError, OSError):
        return name


SMOKE_SIZE_BOX = {'MeshSize': .16, 'XMin': -.01, 'YMin': -.01, 'ZMin': -.01,
                  'XMax': .35, 'YMax': 1.01, 'ZMax': 1.01}


class LocalSizingBox:
    def __init__(self, parent, stored=None):
        self.enabled = Gtk.CheckButton(label='Refine inside a coordinate box')
        parent.pack_start(self.enabled, False, False, 4)
        self.expander = Gtk.Expander(label='Local refinement size and bounds')
        self.grid = Gtk.Grid(column_spacing=8, row_spacing=4)
        self.entries = {}
        for row, (key, label) in enumerate((('MeshSize', 'Local element size'), ('XMin', 'Minimum X'),
                ('YMin', 'Minimum Y'), ('ZMin', 'Minimum Z'), ('XMax', 'Maximum X'),
                ('YMax', 'Maximum Y'), ('ZMax', 'Maximum Z'))):
            entry = Gtk.Entry()
            entry.set_width_chars(12)
            entry.get_accessible().set_name('Refinement box ' + label.lower())
            column, grid_row = (row % 2) * 2, row // 2
            self.grid.attach(Gtk.Label(label=label, xalign=0), column, grid_row, 1, 1)
            self.grid.attach(entry, column + 1, grid_row, 1, 1)
            self.entries[key] = entry
        self.expander.add(self.grid)
        parent.pack_start(self.expander, False, False, 4)
        self.enabled.connect('toggled', lambda button: self.expander.set_sensitive(button.get_active()))
        self.set_values(stored)

    def set_values(self, values):
        self.enabled.set_active(values is not None)
        self.expander.set_sensitive(values is not None)
        self.expander.set_expanded(values is not None)
        for name, entry in self.entries.items():
            entry.set_text(str(values[name]) if values else '')

    def read(self, minimum, maximum):
        if not self.enabled.get_active():
            return None
        values = {name: float(entry.get_text()) for name, entry in self.entries.items()}
        if (not all(math.isfinite(value) for value in values.values()) or
                not 0 < values['MeshSize'] <= maximum or values['MeshSize'] < (minimum or 0) or
                any(values[axis + 'Min'] >= values[axis + 'Max'] for axis in ('X', 'Y', 'Z'))):
            raise ValueError('Local refinement requires valid bounds/face, a size within global limits and positive transition distance.')
        return values


SMOKE_FACE_SIZE = {'FaceTag': 1, 'MeshSize': .16, 'TransitionDistance': .4}


class FaceSizing:
    def __init__(self, parent, faces, stored=None):
        self.enabled = Gtk.CheckButton(label='Refine near a retained CAD face')
        self.enabled.set_sensitive(bool(faces))
        self.enabled.set_tooltip_text('Available for retained STEP, IGES and BREP topology')
        parent.pack_start(self.enabled, False, False, 4)
        self.expander = Gtk.Expander(label='CAD face refinement and transition')
        grid = Gtk.Grid(column_spacing=8, row_spacing=4)
        self.face = Gtk.ComboBoxText()
        self.face.get_accessible().set_name('CAD face for local refinement')
        for face in faces:
            self.face.append(str(face['Tag']), 'Face %d · bounds %s' % (face['Tag'],
                ', '.join(format(value, '.4g') for value in face['Bounds'])))
        if faces:
            self.face.set_active(0)
        self.size, self.distance = Gtk.Entry(), Gtk.Entry()
        self.size.get_accessible().set_name('CAD face element size')
        self.distance.get_accessible().set_name('CAD face refinement transition distance')
        for row, (label, widget) in enumerate((('CAD face', self.face), ('Local element size', self.size),
                                              ('Transition distance', self.distance))):
            grid.attach(Gtk.Label(label=label, xalign=0), 0, row, 1, 1)
            grid.attach(widget, 1, row, 1, 1)
        self.expander.add(grid)
        parent.pack_start(self.expander, False, False, 4)
        self.enabled.connect('toggled', lambda button: self.expander.set_sensitive(button.get_active()))
        self.set_values(stored)

    def set_values(self, values):
        self.enabled.set_active(values is not None)
        self.expander.set_sensitive(values is not None)
        self.expander.set_expanded(values is not None)
        if values:
            self.face.set_active_id(str(values['FaceTag']))
            self.size.set_text(str(values['MeshSize']))
            self.distance.set_text(str(values['TransitionDistance']))

    def read(self, minimum, maximum):
        if not self.enabled.get_active():
            return None
        selected = self.face.get_active_id()
        size, distance = float(self.size.get_text()), float(self.distance.get_text())
        if (not selected or not math.isfinite(size) or not 0 < size <= maximum or size < (minimum or 0) or
                not math.isfinite(distance) or distance <= 0):
            raise ValueError('Select a CAD face, a size within global limits and a positive transition distance.')
        return {'FaceTag': int(selected), 'MeshSize': size, 'TransitionDistance': distance}


class Viewer:
    def __init__(self, application):
        self.application = application
        self.status = 1
        self.closed = False
        self.address = None
        self.can_solve = False
        self.section_pending = False
        self.section_smoke_stage = 0
        self.section_smoke_expected = None
        self.step_pending = False
        self.step_smoke_stage = 0
        self.step_smoke_expected = None
        self.field_output_pending = False
        self.field_output_smoke_stage = 0
        self.field_output_smoke_expected = None
        self.history_output_pending = False
        self.history_output_smoke_stage = 0
        self.history_output_smoke_expected = None
        self.history_result_pending = False
        self.history_result_smoke_started = False
        self.plasticity_pending = False
        self.plasticity_smoke_stage = 0
        self.plasticity_smoke_expected = None
        self.scene = None
        self.selection_preview_done = None
        self.preview_clear_pending = False
        self.picked_surface_faces = []
        self.node_selection = None
        self.surface_pick_smoke_pending = False
        self.surface_smoke_expected_faces = 1
        self.generation = 0
        self.tree_pending = False
        self.tree_project_generation = 0
        self.tree_updating = False
        self.tree_nodes = {}
        self.tree_iters = {}
        self.tree_preview_serial = 0
        self.tree_highlight_key = None
        self.tree_highlight_token = None
        self.tree_highlight_pending = None
        self.tree_smoke_stage = 0
        self.tree_rename = None
        self.tree_select_after_edit = None
        self.element_set_smoke_stage = 0
        self.region_smoke_stage = 0
        self.session = None
        self.session_tree_changes = {}
        self.session_root = Path(os.environ.get('XDG_STATE_HOME', str(Path.home() / '.local/state'))) / 'prepomax/edit-sessions'
        self.session_smoke_stage = 0
        self.busy = False
        self.opening = True
        self.bootstrap_pending = False
        self.deferred_ready = None
        self.solving = False
        self.job_kind = None
        self.job_pending = False
        self.solve_smoke_started = False
        self.result_smoke_pending = False
        self.cancel_sent = False
        self.fields = []
        self.fields_loading = False
        self.updating_fields = False
        self.field_smoke_selected = False
        self.view_failed = False
        self.recovery_stage = 0
        self.recovery_scene = None
        self.clipboard_requested = False
        self.displayed_field = None
        self.displayed_scale = 0
        self.remesh_pending = False
        self.cad_topology_pending = False
        self.cad_topology = None
        self.geometry_source = None
        self.meshing_setup = None
        self.stage = 0
        self.smoke_started = time.monotonic()
        self.load_stage = 'not-started'
        self.ready_count = 0
        self.bootstrap_count = 0
        self.bootstrap_confirmed = 0
        self.bootstrap_recovery_stage = 0
        self.bootstrap_recovery_scene = None
        self.evaluation_count = 0
        self.page_state = {}
        self.stable_view_generation = None
        self.view_deadline = None
        self.paused_view_remaining = None
        self.startup_recovery_stage = 0
        self.startup_scene = None
        self.dialog = None
        self.conversion_process = None
        self.pmx_smoke_stage = 0
        self.gmsh_smoke_stage = 0
        self.remesh_smoke_stage = 0
        self.native_focus = None
        self.dialog_focus = None
        self.focus_smoke_started = False
        self.occlusion_stage = 0
        self.occlusion_cover = None
        self.occlusion_generation = None
        self.frame_recovery_stage = 0
        self.frame_recovery_scene = None
        self.gpu_reported = False
        manager = WebKit2.UserContentManager()
        manager.register_script_message_handler('prepomaxSelection')
        manager.connect('script-message-received::prepomaxSelection', self.selection_message_received)
        manager.add_style_sheet(WebKit2.UserStyleSheet.new('''
          body { min-width: 0 !important; }
          .toolbar { flex-wrap: wrap !important; }
          .workspace { grid-template-columns: minmax(0, 1fr) !important; }
          .scene-panel, .diagnostics-panel, #cancel, #unload, #switch-field { display: none !important; }
          html.gtk-view-tools .workspace { grid-template-columns: 180px minmax(0, 1fr) !important; }
          html.gtk-view-tools .scene-panel { display: block !important; }
          html.gtk-diagnostics .workspace { grid-template-columns: minmax(0, 1fr) 240px !important; }
          html.gtk-diagnostics .diagnostics-panel { display: block !important; }
          html.gtk-view-tools.gtk-diagnostics .workspace { grid-template-columns: 180px minmax(0, 1fr) 240px !important; }
        ''', WebKit2.UserContentInjectedFrames.TOP_FRAME, WebKit2.UserStyleLevel.USER, None, None))
        manager.add_script(WebKit2.UserScript.new('''(() => {
          const listeners = [], messages = [];
          if (__FRAME_TEST__) {
            const request = window.requestAnimationFrame.bind(window);
            window.requestAnimationFrame = callback => request(time => {
              const progress = document.querySelector('#progress');
              if (window.__gtkBlockFrames && progress && progress.value === progress.max) return;
              callback(time);
            });
          }
          let errors = 0, frame = false, sceneApplied = false, dropped = false;
          window.addEventListener('error', () => { errors++; });
          window.addEventListener('unhandledrejection', () => { errors++; });
          requestAnimationFrame(() => { frame = true; });
          let pressured = false;
          const accepted = new Set(['prepomax.client.ready-v1', 'prepomax.scene.applied-v1',
                                    'prepomax.clipboard.copy-summary-v1', 'prepomax.pick.validated-v1',
                                    'prepomax.area.validated-v1', 'prepomax.node-selection.frustum-v1']);
          const enqueue = text => {
            if (typeof text !== 'string' || text.length > 4 * 1024 * 1024) { dropped = true; return; }
            let type;
            try { type = JSON.parse(text)?.type; } catch { dropped = true; return; }
            if (!accepted.has(type)) return;
            if (!['prepomax.area.validated-v1', 'prepomax.node-selection.frustum-v1'].includes(type) && text.length > 1024) { dropped = true; return; }
            if (['prepomax.pick.validated-v1', 'prepomax.area.validated-v1', 'prepomax.node-selection.frustum-v1'].includes(type)) {
              const handler = window.webkit?.messageHandlers?.prepomaxSelection;
              if (handler) { handler.postMessage(text); return; }
            }
            if (type === 'prepomax.node-selection.frustum-v1') {
              if (messages.length >= 100) { dropped = true; return; }
              messages.push(text); return;
            }
            if (type === 'prepomax.scene.applied-v1') sceneApplied = true;
            if (type === 'prepomax.pick.validated-v1' || type === 'prepomax.area.validated-v1') {
              if (messages.filter(item => ['prepomax.pick.validated-v1', 'prepomax.area.validated-v1'].includes(JSON.parse(item).type)).length >= 100) { dropped = true; return; }
              messages.push(text);
              return;
            }
            // Keep only the latest pending lifecycle message of each supported type.
            const previous = messages.findIndex(item => JSON.parse(item).type === type);
            if (previous !== -1) messages.splice(previous, 1);
            messages.push(text);
          };
          window.chrome = {webview: {
            addEventListener: (type, cb) => { if(type === 'message') listeners.push(cb); },
            postMessage: text => {
              if (__PRESSURE_TEST__) {
                for (let i = 0; i < 64; i++) {
                  enqueue(JSON.stringify({type: 'prepomax.camera.changed-v1', sequence: i}));
                  enqueue(text);
                }
                pressured = true;
              }
              enqueue(text);
            }
          }};
          // Intercept only GTK's existing Copy summary action; other hosts keep their browser implementation.
          document.addEventListener('click', event => {
            if (event.target instanceof Element && event.target.closest('#copy-diagnostics')) {
              event.preventDefault(); event.stopImmediatePropagation();
              window.chrome.webview.postMessage(JSON.stringify({type: 'prepomax.clipboard.copy-summary-v1'}));
            }
          }, true);
          window.__gtkDrain = () => JSON.stringify({messages: messages.splice(0),
            state: {visible: !document.hidden, frame, errors, sceneApplied, dropped, pressured,
              resourcesComplete: !!document.querySelector('#progress') && document.querySelector('#progress').value === document.querySelector('#progress').max,
              errorBanner: !!document.querySelector('#error:not([hidden])'),
              canvas: !!document.querySelector('canvas'),
              stage: ['Creating authenticated local session…', 'Fetching scene manifest…', 'Load failed.']
                .indexOf(document.querySelector('#status')?.textContent)},
            diagnostics: document.querySelector('#diagnostics')?.textContent ?? ''});
          window.__gtkDeliver = data => { listeners.forEach(cb => cb({data})); return listeners.length; };
          window.__gtkClearSelectionMessages = () => {
            for (let i = messages.length - 1; i >= 0; i--) {
              if (['prepomax.pick.validated-v1', 'prepomax.area.validated-v1', 'prepomax.node-selection.frustum-v1']
                .includes(JSON.parse(messages[i]).type)) messages.splice(i, 1);
            }
          };
        })();'''.replace('__FRAME_TEST__', 'true' if args.smoke_frame_recovery else 'false').replace('__PRESSURE_TEST__', 'true' if args.smoke_bridge_pressure else 'false'), WebKit2.UserContentInjectedFrames.TOP_FRAME, WebKit2.UserScriptInjectionTime.START, None, None))
        self.view = WebKit2.WebView(web_context=WebKit2.WebContext.new_ephemeral(), user_content_manager=manager)
        self.view.get_settings().set_enable_webgl(True)
        self.native_visibility = 'unknown'
        self.window = Gtk.ApplicationWindow(application=application, title='PrePoMax')
        self.window.add_events(Gdk.EventMask.VISIBILITY_NOTIFY_MASK)
        self.window.connect('visibility-notify-event', self.visibility_changed)
        self.window.set_default_size(1200, 800)
        layout = Gtk.Box(orientation=Gtk.Orientation.VERTICAL)
        toolbar = Gtk.Box(spacing=12)
        edit_toolbar = Gtk.Box(spacing=8)
        self.open_button = Gtk.Button(label='Open project…')
        self.open_button.set_sensitive(False)
        self.open_button.connect('clicked', lambda *_: self.choose_project())
        self.save_button = Gtk.Button(label='Save')
        self.save_button.connect('clicked', lambda *_: self.save_session())
        self.save_as_button = Gtk.Button(label='Save as…')
        self.save_as_button.connect('clicked', lambda *_: self.save_session(choose=True))
        self.undo_button = Gtk.Button(label='Undo')
        self.undo_button.connect('clicked', lambda *_: self.move_session(-1))
        self.redo_button = Gtk.Button(label='Redo')
        self.redo_button.connect('clicked', lambda *_: self.move_session(1))
        for button in (self.save_button, self.save_as_button, self.undo_button, self.redo_button):
            button.set_sensitive(False)
            toolbar.pack_start(button, False, False, 0)
        self.solve_button = Gtk.Button(label='Solve')
        self.solve_button.set_sensitive(False)
        self.solve_button.connect('clicked', lambda *_: self.solve())
        self.cancel_button = Gtk.Button(label='Cancel analysis')
        self.cancel_button.set_sensitive(False)
        self.cancel_button.connect('clicked', lambda *_: self.cancel_job())
        self.result_button = Gtk.Button(label='Open result project')
        self.result_button.set_sensitive(False)
        self.boundary_button = Gtk.Button(label='Edit displacement…')
        self.boundary_button.get_accessible().set_name('Edit prescribed displacement')
        self.boundary_button.connect('clicked', lambda *_: self.edit_properties(boundary=True))
        edit_toolbar.pack_start(self.boundary_button, False, False, 0)
        self.add_boundary_button = Gtk.Button(label='Add displacement…')
        self.add_boundary_button.get_accessible().set_name('Add displacement boundary')
        self.add_boundary_button.connect('clicked', lambda *_: self.edit_properties(boundary=True, create=True))
        edit_toolbar.pack_start(self.add_boundary_button, False, False, 0)
        self.fixed_support_button = Gtk.Button(label='Add fixed support…')
        self.fixed_support_button.get_accessible().set_name('Add fixed support')
        self.fixed_support_button.connect('clicked', lambda *_: self.add_fixed_support())
        edit_toolbar.pack_start(self.fixed_support_button, False, False, 0)
        self.node_set_button = Gtk.Button(label='Add node set…')
        self.node_set_button.get_accessible().set_name('Add node set')
        self.node_set_button.connect('clicked', lambda *_: self.create_node_set_from_tree())
        edit_toolbar.pack_start(self.node_set_button, False, False, 0)
        self.surface_button = Gtk.Button(label='Add surface…')
        self.surface_button.get_accessible().set_name('Add element surface')
        self.surface_button.connect('clicked', lambda *_: self.add_surface())
        edit_toolbar.pack_start(self.surface_button, False, False, 0)
        edit_toolbar.pack_start(Gtk.Label(label='Grow ≤'), False, False, 0)
        self.surface_angle = Gtk.SpinButton.new_with_range(0, 90, 5)
        self.surface_angle.set_value(30)
        self.surface_angle.set_digits(0)
        self.surface_angle.set_tooltip_text('Double-click a face to grow across connected edges up to this angle')
        self.surface_angle.get_accessible().set_name('Surface growth angle in degrees')
        edit_toolbar.pack_start(self.surface_angle, False, False, 0)
        edit_toolbar.pack_start(Gtk.Label(label='° (double-click)'), False, False, 0)
        self.add_force_button = Gtk.Button(label='Add force / traction…')
        self.add_force_button.get_accessible().set_name('Add force or surface traction')
        self.add_force_button.connect('clicked', lambda *_: self.edit_properties(force=True, create=True))
        edit_toolbar.pack_start(self.add_force_button, False, False, 0)
        self.gravity_button = Gtk.Button(label='Gravity…')
        self.gravity_button.get_accessible().set_name('Add, edit or remove gravity load')
        self.gravity_button.connect('clicked', lambda *_: self.add_gravity())
        edit_toolbar.pack_start(self.gravity_button, False, False, 0)
        self.centrifugal_button = Gtk.Button(label='Centrifugal…')
        self.centrifugal_button.get_accessible().set_name('Add, edit or remove centrifugal load')
        self.centrifugal_button.connect('clicked', lambda *_: self.edit_centrifugal())
        edit_toolbar.pack_start(self.centrifugal_button, False, False, 0)
        self.pressure_button = Gtk.Button(label='Pressure…')
        self.pressure_button.get_accessible().set_name('Add or edit pressure')
        self.pressure_button.connect('clicked', lambda *_: self.edit_pressure())
        edit_toolbar.pack_start(self.pressure_button, False, False, 0)
        self.material_smoke_stage = 0
        self.load_button = Gtk.Button(label='Edit force…')
        self.load_button.get_accessible().set_name('Edit force load')
        self.load_button.connect('clicked', lambda *_: self.edit_properties(True))
        edit_toolbar.pack_start(self.load_button, False, False, 0)
        self.material_button = Gtk.Button(label='Edit material…')
        self.material_button.get_accessible().set_name('Edit elastic material')
        self.material_button.connect('clicked', lambda *_: self.edit_properties())
        edit_toolbar.pack_start(self.material_button, False, False, 0)
        self.plasticity_button = Gtk.Button(label='Plasticity…')
        self.plasticity_button.get_accessible().set_name('Edit material plasticity')
        self.plasticity_button.connect('clicked', lambda *_: self.edit_plasticity())
        edit_toolbar.pack_start(self.plasticity_button, False, False, 0)
        self.add_material_button = Gtk.Button(label='Add material…')
        self.add_material_button.get_accessible().set_name('Add elastic material and assign section')
        self.add_material_button.connect('clicked', lambda *_: self.add_material())
        edit_toolbar.pack_start(self.add_material_button, False, False, 0)
        self.assign_material_button = Gtk.Button(label='Assign material…')
        self.assign_material_button.get_accessible().set_name('Assign existing material to section')
        self.assign_material_button.connect('clicked', lambda *_: self.assign_material())
        edit_toolbar.pack_start(self.assign_material_button, False, False, 0)
        self.steps_button = Gtk.Button(label='Steps…')
        self.steps_button.get_accessible().set_name('Manage analysis steps')
        self.steps_button.set_sensitive(False)
        self.steps_button.connect('clicked', lambda *_: self.edit_steps())
        edit_toolbar.pack_start(self.steps_button, False, False, 0)
        self.sections_button = Gtk.Button(label='Sections…')
        self.sections_button.get_accessible().set_name('Manage solid sections')
        self.sections_button.set_sensitive(False)
        self.sections_button.connect('clicked', lambda *_: self.edit_sections())
        edit_toolbar.pack_start(self.sections_button, False, False, 0)
        self.field_outputs_button = Gtk.Button(label='Field outputs…')
        self.field_outputs_button.get_accessible().set_name('Manage field output requests')
        self.field_outputs_button.set_sensitive(False)
        self.field_outputs_button.connect('clicked', lambda *_: self.edit_field_outputs())
        edit_toolbar.pack_start(self.field_outputs_button, False, False, 0)
        self.history_outputs_button = Gtk.Button(label='History outputs…')
        self.history_outputs_button.get_accessible().set_name('Manage history output requests')
        self.history_outputs_button.set_sensitive(False)
        self.history_outputs_button.connect('clicked', lambda *_: self.edit_history_outputs())
        edit_toolbar.pack_start(self.history_outputs_button, False, False, 0)
        self.import_button = Gtk.Button(label='Import mesh…')
        self.import_button.get_accessible().set_name('Import volume mesh')
        self.import_button.connect('clicked', lambda *_: self.import_mesh())
        toolbar.pack_start(self.import_button, False, False, 0)
        self.mesh_geometry_button = Gtk.Button(label='Mesh CAD…')
        self.mesh_geometry_button.get_accessible().set_name('Mesh CAD geometry with Gmsh')
        self.mesh_geometry_button.set_sensitive(False)
        self.mesh_geometry_button.connect('clicked', lambda *_: self.mesh_geometry())
        toolbar.pack_start(self.mesh_geometry_button, False, False, 0)
        self.remesh_geometry_button = Gtk.Button(label='Remesh CAD…')
        self.remesh_geometry_button.get_accessible().set_name('Remesh retained CAD geometry with Gmsh')
        self.remesh_geometry_button.set_sensitive(False)
        self.remesh_geometry_button.connect('clicked', lambda *_: self.remesh_geometry())
        toolbar.pack_start(self.remesh_geometry_button, False, False, 0)
        self.export_geometry_button = Gtk.Button(label='Export CAD…')
        self.export_geometry_button.get_accessible().set_name('Export retained CAD geometry')
        self.export_geometry_button.set_sensitive(False)
        self.export_geometry_button.connect('clicked', lambda *_: self.export_geometry())
        toolbar.pack_start(self.export_geometry_button, False, False, 0)
        self.cad_topology_button = Gtk.Button(label='Inspect CAD…')
        self.cad_topology_button.get_accessible().set_name('Inspect retained CAD topology')
        self.cad_topology_button.set_sensitive(False)
        self.cad_topology_button.connect('clicked', lambda *_: self.inspect_cad_topology())
        toolbar.pack_start(self.cad_topology_button, False, False, 0)
        self.result_path = None
        self.result_button.connect('clicked', lambda *_: self.open_project(self.result_path))
        self.history_results_button = Gtk.Button(label='History results…')
        self.history_results_button.get_accessible().set_name('Browse history results')
        self.history_results_button.set_sensitive(False)
        self.history_results_button.connect('clicked', lambda *_: self.edit_history_results())
        toolbar.pack_start(self.solve_button, False, False, 0)
        toolbar.pack_start(self.cancel_button, False, False, 0)
        toolbar.pack_start(self.result_button, False, False, 0)
        toolbar.pack_start(self.history_results_button, False, False, 0)
        self.label = Gtk.Label(label='Opening project…')
        toolbar.pack_start(self.open_button, False, False, 8)
        toolbar.reorder_child(self.open_button, 0)
        self.label.set_line_wrap(True)
        self.label.set_max_width_chars(60)
        self.label.set_xalign(0)
        layout.pack_start(toolbar, False, False, 6)
        tools = Gtk.FlowBox()
        tools.set_selection_mode(Gtk.SelectionMode.NONE)
        tools.set_max_children_per_line(7)
        for button in (self.mesh_geometry_button, self.remesh_geometry_button,
                       self.export_geometry_button, self.cad_topology_button):
            toolbar.remove(button)
            tools.add(button)
        for widget in list(edit_toolbar.get_children()):
            edit_toolbar.remove(widget)
            tools.add(widget)
        additional_tools = Gtk.Expander(label='Additional tools')
        additional_tools.add(tools)
        layout.pack_start(additional_tools, False, False, 4)
        layout.pack_start(self.label, False, False, 4)
        self.field_combo = Gtk.ComboBoxText()
        self.field_combo.append_text('Mesh geometry')
        self.field_combo.set_active(0)
        self.field_combo.set_sensitive(False)
        self.field_combo.connect('changed', self.field_changed)
        field_label = Gtk.Label.new_with_mnemonic('_Result field and frame')
        field_label.set_xalign(0)
        field_label.set_mnemonic_widget(self.field_combo)
        layout.pack_start(field_label, False, False, 0)
        layout.pack_start(self.field_combo, False, False, 0)
        deformation = Gtk.Box(spacing=8)
        scale_label = Gtk.Label.new_with_mnemonic('_Displacement scale (0 = undeformed)')
        deformation.pack_start(scale_label, False, False, 8)
        self.scale_control = Gtk.SpinButton.new_with_range(-1000000, 1000000, 1)
        scale_label.set_mnemonic_widget(self.scale_control)
        self.scale_control.set_digits(3)
        self.scale_control.set_value(args.deformation_scale)
        deformation.pack_start(self.scale_control, False, False, 0)
        self.apply_scale = Gtk.Button(label='Apply scale')
        self.apply_scale.connect('clicked', lambda *_: self.field_changed(self.field_combo))
        deformation.pack_start(self.apply_scale, False, False, 0)
        self.view_tools_toggle = Gtk.CheckButton(label='View tools')
        self.view_tools_toggle.connect('toggled', lambda *_: self.apply_view_layout())
        deformation.pack_start(self.view_tools_toggle, False, False, 0)
        self.diagnostics_toggle = Gtk.CheckButton(label='Renderer diagnostics')
        self.diagnostics_toggle.connect('toggled', lambda *_: self.apply_view_layout())
        tools.add(self.diagnostics_toggle)
        layout.pack_start(deformation, False, False, 0)
        recovery = Gtk.Box(spacing=8)
        self.view_error = Gtk.Label(label='')
        self.retry_button = Gtk.Button(label='Retry view')
        self.retry_button.set_sensitive(False)
        self.retry_button.connect('clicked', lambda *_: self.retry_view())
        recovery.pack_start(self.view_error, False, False, 8)
        recovery.pack_start(self.retry_button, False, False, 0)
        layout.pack_start(recovery, False, False, 0)
        workspace = Gtk.Paned(orientation=Gtk.Orientation.HORIZONTAL)
        workspace.set_position(320)
        workspace.pack1(self.build_model_tree(), False, False)
        workspace.pack2(self.view, True, False)
        layout.pack_start(workspace, True, True, 0)
        self.window.add(layout)
        names = ((self.open_button, 'Open project'), (self.solve_button, 'Solve'),
                 (self.cancel_button, 'Cancel analysis'), (self.result_button, 'Open result project'),
                 (self.history_results_button, 'Browse history results'),
                 (self.mesh_geometry_button, 'Mesh CAD geometry with Gmsh'),
                 (self.remesh_geometry_button, 'Remesh retained CAD geometry with Gmsh'),
                 (self.export_geometry_button, 'Export retained CAD geometry'),
                 (self.field_combo, 'Result field and frame'), (self.scale_control, 'Displacement scale'),
                 (self.apply_scale, 'Apply displacement scale'), (self.retry_button, 'Retry view'),
                 (self.view, 'Engineering scene'))
        for widget, name in names:
            widget.get_accessible().set_name(name)
        self.label.get_accessible().set_description('Project and analysis status')
        self.view_error.get_accessible().set_description('View status')
        self.scale_control.get_accessible().set_description('Zero restores undeformed geometry. Select a result frame before applying a scale.')
        self.field_combo.get_accessible().set_description('Choose mesh geometry or a result field, component and frame.')
        self.accelerators = Gtk.AccelGroup()
        self.window.add_accel_group(self.accelerators)
        for widget, shortcut in ((self.open_button, '<Control>o'), (self.solve_button, '<Control>Return'),
                (self.save_button, '<Control>s'), (self.save_as_button, '<Control><Shift>s'),
                (self.undo_button, '<Control>z'), (self.redo_button, '<Control><Shift>z'),
                (self.cancel_button, '<Control><Shift>Escape'), (self.result_button, '<Control><Shift>Return'),
                (self.retry_button, '<Control>r')):
            key, modifiers = Gtk.accelerator_parse(shortcut)
            widget.add_accelerator('clicked', self.accelerators, key, modifiers, Gtk.AccelFlags.VISIBLE)
            widget.set_tooltip_text(Gtk.accelerator_get_label(key, modifiers))
        self.window.connect('key-press-event', self.switch_focus)
        self.view.get_accessible().set_description('F6 switches between the scene controls and native project controls.')
        self.view.set_tooltip_text('F6: switch between scene and project controls')
        self.window.connect('delete-event', self.close_requested)
        self.window.connect('destroy', lambda *_: self.finish(not args.smoke, 'GTK viewer closed'))
        self.view.connect('decide-policy', self.policy)
        self.view.connect('load-changed', self.loading)
        self.view.connect('web-process-terminated', self.web_process_terminated)
        self.view.connect('load-failed', self.load_failed)
        self.window.show_all()
        self.window.present()
        GLib.timeout_add(250, self.poll)
        self.submit(None, self.started)

    def build_model_tree(self):
        pane = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6)
        pane.set_size_request(240, -1)
        pane.pack_start(Gtk.Label(label='Model tree', xalign=0), False, False, 4)
        self.tree_store = Gtk.TreeStore(str, str, bool, str, GdkPixbuf.Pixbuf)
        self.model_tree = Gtk.TreeView(model=self.tree_store)
        self.model_tree.set_headers_visible(False)
        self.model_tree.set_enable_tree_lines(True)
        self.model_tree.get_accessible().set_name('PrePoMax model tree')
        self.model_tree.set_tooltip_column(3)
        cell = Gtk.CellRendererText()
        cell.set_property('ypad', 1)
        icon = Gtk.CellRendererPixbuf(); icon.set_property('ypad', 1)
        column = Gtk.TreeViewColumn('Model objects')
        column.pack_start(icon, False); column.add_attribute(icon, 'pixbuf', 4)
        column.pack_start(cell, True); column.add_attribute(cell, 'text', 0); column.add_attribute(cell, 'strikethrough', 2)
        self.model_tree.append_column(column)
        self.tree_icons = {}
        self.model_tree.get_selection().connect('changed', self.model_tree_selected)
        self.model_tree.connect('row-activated', lambda *_: self.model_tree_action('edit'))
        self.model_tree.connect('button-press-event', self.model_tree_mouse)
        self.model_tree.connect('popup-menu', lambda *_: self.model_tree_menu())
        scroll = Gtk.ScrolledWindow()
        scroll.set_policy(Gtk.PolicyType.AUTOMATIC, Gtk.PolicyType.AUTOMATIC)
        scroll.add(self.model_tree)
        pane.pack_start(scroll, True, True, 0)
        actions = Gtk.Box(spacing=6)
        self.tree_edit_button = Gtk.Button(label='Edit…')
        self.tree_edit_button.set_sensitive(False)
        self.tree_edit_button.connect('clicked', lambda *_: self.model_tree_action('edit'))
        self.tree_preview_button = Gtk.Button(label='Preview region')
        self.tree_preview_button.set_sensitive(False)
        self.tree_preview_button.connect('clicked', lambda *_: self.model_tree_action('preview'))
        actions.pack_start(self.tree_edit_button, False, False, 0)
        actions.pack_start(self.tree_preview_button, False, False, 0)
        pane.pack_start(actions, False, False, 0)
        self.tree_details = Gtk.Label(label='Open or import a project.', xalign=0, wrap=True)
        self.tree_details.set_max_width_chars(38)
        self.tree_details.set_selectable(True)
        pane.pack_start(self.tree_details, False, False, 4)
        return pane

    def model_tree_icon(self, node):
        kind = node['Key'].rsplit('/', 1)[-1] if node['Kind'] == 'collection' else node['Kind']
        files = {'mesh': 'Mesh', 'parts': 'Part', 'part': 'Part', 'node-sets': 'Node_set', 'node-set': 'Node_set',
            'element-sets': 'Element_set', 'element-set': 'Element_set', 'surfaces': 'Surface', 'surface': 'Surface',
            'reference-points': 'Reference_point', 'reference-point': 'Reference_point',
            'cylinders': 'CoordinateSystem', 'cylinder': 'CoordinateSystem',
            'coordinate-systems': 'CoordinateSystem', 'coordinate-system': 'CoordinateSystem',
            'materials': 'Material', 'material': 'Material', 'sections': 'Section', 'section': 'Section',
            'constraints': 'Constraints', 'constraint': 'Constraints', 'contacts': 'Contact',
            'interactions': 'SurfaceInteraction', 'interaction': 'SurfaceInteraction', 'pairs': 'ContactPair', 'contact-pair': 'ContactPair',
            'distributions': 'Distribution', 'distribution': 'Distribution', 'amplitudes': 'Amplitude', 'amplitude': 'Amplitude',
            'initial-conditions': 'Initial_conditions', 'initial-condition': 'Initial_conditions', 'steps': 'Step', 'step': 'Step',
            'analyses': 'Analysis', 'field-outputs': 'Field_output', 'field-output': 'Field_output',
            'history-outputs': 'History_output', 'history-output': 'History_output', 'bcs': 'Bc', 'boundary': 'Bc', 'loads': 'Load', 'load': 'Load'}
        image = files.get(kind)
        if image not in self.tree_icons:
            subfolders = {'CoordinateSystem': 'Mesh', 'Contact': 'Contact', 'ContactPair': 'Contact', 'SurfaceInteraction': 'Contact',
                          'Distribution': 'Distributions', 'Amplitude': 'Amplitude', 'Initial_conditions': 'Initial conditions'}
            candidates = [] if image is None else [repo / 'share/model-tree-icons' / (image + '.ico'),
                repo / 'UserControls/Resources' / (image + '.ico'),
                repo / 'PrePoMax/Icons' / subfolders.get(image, '') / (image + '.ico')]
            picture = None
            for path in candidates:
                if path.is_file():
                    try: picture = GdkPixbuf.Pixbuf.new_from_file_at_scale(str(path), 16, 16, True); break
                    except GLib.Error: pass
            self.tree_icons[image] = picture
        return self.tree_icons[image]

    def start_session(self, path):
        previous = self.session
        self.session = EditSession(path, self.session_root)
        self.session_tree_changes = {}
        if previous and not self.session.source.is_relative_to(previous.directory):
            previous.close(discard=True)
        self.session_title()

    def session_title(self):
        if self.session:
            self.window.set_title(('*' if self.session.dirty else '') + self.session.source.name + ' — PrePoMax')

    def session_work(self, operation, completed):
        self.opening = True
        future = worker.submit(operation)
        def done(result):
            def deliver():
                if self.closed: return False
                self.opening = False
                try: value = result.result()
                except Exception as error:
                    self.label.set_text(str(error))
                    if args.smoke_edit_session or args.smoke_element_sets or args.smoke_region_maintenance: self.finish(False, 'Edit session failed: ' + str(error))
                    return False
                completed(value)
                return False
            GLib.idle_add(deliver)
        future.add_done_callback(done)

    def save_session(self, choose=False, after=None):
        if self.opening or self.solving or self.dialog or not self.session:
            return
        def save(path=None):
            def saved(_):
                self.session_title()
                self.label.set_text('Saved ' + self.session.source.name)
                if after: after()
            self.session_work(lambda: self.session.save(path), saved)
        if not choose:
            save()
            return
        chooser = Gtk.FileChooserNative.new('Save project as a new file', self.window, Gtk.FileChooserAction.SAVE, '_Save', '_Cancel')
        self.dialog = chooser
        chooser.set_modal(True)
        file_filter = Gtk.FileFilter()
        file_filter.set_name('PrePoMax Linux project (*.pma)')
        file_filter.add_pattern('*.pma'); file_filter.add_pattern('*.PMA')
        chooser.add_filter(file_filter)
        chooser.set_current_name(self.session.source.stem + '-edited.pma')
        def chosen(dialog, answer):
            path = dialog.get_filename() if answer == Gtk.ResponseType.ACCEPT else None
            dialog.destroy(); self.dialog = None
            if path: save(path)
        chooser.connect('response', chosen)
        chooser.show()
        smoke_output = args.smoke_edit_session or args.smoke_element_sets or args.smoke_region_maintenance or args.smoke_node_selection
        if smoke_output:
            output = Path(smoke_output).resolve()
            chooser.set_current_folder(str(output.parent)); chooser.set_current_name(output.name)
            GLib.timeout_add(300, lambda: chooser.emit('response', Gtk.ResponseType.ACCEPT) is None and False)

    def guard_unsaved(self, continuation):
        if not self.session or not self.session.dirty:
            continuation()
            return
        dialog = Gtk.MessageDialog(transient_for=self.window, modal=True, message_type=Gtk.MessageType.QUESTION,
                                   text='Save changes to ' + self.session.source.name + '?')
        dialog.format_secondary_text('Your current model contains unsaved edits.')
        dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Discard', Gtk.ResponseType.REJECT, '_Save', Gtk.ResponseType.ACCEPT)
        self.dialog = dialog
        def answered(_, response):
            dialog.destroy(); self.dialog = None
            if response == Gtk.ResponseType.REJECT: continuation()
            elif response == Gtk.ResponseType.ACCEPT: self.save_session(after=continuation)
        dialog.connect('response', answered); dialog.show_all()

    def close_requested(self, *_):
        if self.opening or self.solving or self.dialog:
            return True
        def close():
            if self.session: self.session.close(discard=True)
            self.session = None
            self.window.destroy()
        self.guard_unsaved(close)
        return True

    def move_session(self, offset):
        if not self.session or self.opening or self.solving or self.dialog or self.tree_pending:
            return
        index = self.session.index + offset
        if not 0 <= index < len(self.session.history): return
        change_path = self.session.history[index if offset > 0 else self.session.index]
        rename = self.session_tree_changes.get(str(change_path))
        if rename:
            self.tree_rename = rename if offset > 0 else (rename[1], rename[0])
        def opened(reply):
            if reply.get('ok'):
                self.session.move(index)
                self.session_title()
        self.open_project(str(self.session.history[index]), opened, retain_session=True)

    def apply_session_edit(self, change):
        if self.session is None:
            self.label.set_text('Open a project before editing.')
            return
        def reserved(path):
            self.opening = True
            def saved(reply):
                self.opening = False
                if not reply.get('ok'):
                    self.label.set_text(reply.get('error', 'The model edit could not be applied.'))
                    return
                node = self.selected_model_node()
                renamed = change.get('settings', {}).get('Name')
                original = change.get('output') or change.get('section') or change.get('step') or change.get('set') or change.get('region')
                if node and renamed and renamed != node['Name'] and node['Name'] == original:
                    self.tree_rename = (node['Key'], node['Key'].rsplit('/', 1)[0] + '/' + quote(renamed, safe=''))
                tree_change = self.tree_rename
                if change['command'] in ('add-element-set', 'duplicate-element-set'):
                    self.tree_select_after_edit = 'model/mesh/element-sets/' + quote(
                        change.get('settings', {}).get('Name') or change['name'], safe='')
                for kind, collection in (('node-set', 'node-sets'), ('surface', 'surfaces')):
                    if change['command'] in ('create-' + kind, 'duplicate-' + kind):
                        self.tree_select_after_edit = 'model/mesh/' + collection + '/' + quote(
                            change.get('settings', {}).get('Name') or change['name'], safe='')
                def reopened(result):
                    if result.get('ok'):
                        self.session.commit(path)
                        self.session_tree_changes[str(path)] = tree_change
                        self.session_title()
                self.open_project(str(path), reopened, retain_session=True)
            self.submit({**change, 'path': str(path)}, saved)
        self.session_work(self.session.reserve, reserved)

    def selected_model_node(self):
        store, iterator = self.model_tree.get_selection().get_selected()
        return self.tree_nodes.get(store[iterator][1]) if iterator is not None else None

    def refresh_model_tree(self):
        self.tree_pending = True
        generation = self.tree_project_generation
        rename, self.tree_rename = self.tree_rename, None
        requested_selection, self.tree_select_after_edit = self.tree_select_after_edit, None
        def received(reply):
            if generation != self.tree_project_generation:
                return
            self.tree_pending = False
            if not reply.get('ok'):
                self.tree_updating = True
                self.tree_store.clear()
                self.tree_nodes, self.tree_iters = {}, {}
                self.tree_updating = False
                self.tree_details.set_text(reply.get('error', 'Unable to read the model tree.'))
                return
            selected = self.selected_model_node()
            selected_key = requested_selection or (selected['Key'] if selected else None)
            expanded = {key for key, iterator in self.tree_iters.items()
                        if self.model_tree.row_expanded(self.tree_store.get_path(iterator))}
            if rename:
                old, new = rename
                def remap(key):
                    return new + key[len(old):] if key and (key == old or key.startswith(old + '/')) else key
                selected_key = remap(selected_key)
                expanded = {remap(key) for key in expanded}
            initial = not self.tree_nodes
            self.tree_updating = True
            self.tree_store.clear()
            self.tree_nodes, self.tree_iters = {}, {}
            def append(parent, nodes):
                for node in nodes:
                    key = node['Key']
                    counted = node['Kind'] == 'collection' and bool(node['Children']) and key not in ('model/mesh', 'model/features', 'model/contacts')
                    label = node['Name'] + (' (%d)' % len(node['Children']) if counted else '')
                    iterator = self.tree_store.append(parent, (label, key, node['Active'] is False,
                                                             node['DefinitionType'] + ' · ' + node['Details'], self.model_tree_icon(node)))
                    self.tree_nodes[key], self.tree_iters[key] = node, iterator
                    append(iterator, node['Children'])
            append(None, reply.get('nodes', []))
            # Restore parents first: GTK cannot select a leaf whose ancestors are collapsed.
            for key in sorted(self.tree_iters, key=lambda value: value.count('/')):
                node = self.tree_nodes[key]
                if key in expanded or initial and (key in ('model', 'model/mesh', 'model/steps') or node['Kind'] == 'step'):
                    self.model_tree.expand_row(self.tree_store.get_path(self.tree_iters[key]), False)
            while selected_key and selected_key not in self.tree_iters:
                selected_key = selected_key.rsplit('/', 1)[0] if '/' in selected_key else None
            if selected_key in self.tree_iters:
                path = self.tree_store.get_path(self.tree_iters[selected_key])
                self.model_tree.expand_to_path(path)
                self.model_tree.get_selection().select_iter(self.tree_iters[selected_key])
                self.model_tree.scroll_to_cell(path, None, False, 0, 0)
            self.tree_updating = False
            self.model_tree_selected(self.model_tree.get_selection())
        self.submit({'command': 'model-tree'}, received)

    def model_tree_selected(self, selection):
        if self.tree_updating:
            return
        self.tree_preview_serial += 1
        node = self.selected_model_node()
        self.highlight_selected_node_set()
        self.tree_edit_button.set_sensitive(bool(node and node['Editor']))
        self.tree_preview_button.set_sensitive(bool(node and node['RegionKind']))
        if not node:
            self.tree_details.set_text('Select a model object. Right-click for actions.')
            return
        details = [node['Name'], node['DefinitionType'], node['Details']]
        if node['Active'] is False:
            details.append('Inactive')
        if node['Kind'] == 'collection' and not node['Creator']:
            details.append('Creation is not available here yet.')
        elif node['Kind'] not in ('collection', 'model') and not node['Editor']:
            details.append('Editing is not available for this object yet.')
        self.tree_details.set_text('\n'.join(value for value in details if value))
        if node['RegionKind'] and node['Kind'] != 'node-set' and not args.smoke:
            serial = self.tree_preview_serial
            def preview():
                if serial == self.tree_preview_serial and not self.opening and not self.solving and not self.dialog:
                    self.model_tree_action('preview')
                return False
            GLib.timeout_add(300, preview)

    def highlight_selected_node_set(self):
        if (self.dialog or self.opening or self.solving or self.tree_pending or not self.scene or
            self.view.is_loading() or self.stable_view_generation != self.generation): return
        node = self.selected_model_node()
        key = node['Key'] if node and node['Kind'] == 'node-set' else None
        if self.tree_highlight_key and self.tree_highlight_key != key:
            self.send_tree_highlight([])
            self.tree_highlight_key = None
            self.tree_highlight_token = None
        if key is None: return
        token = (self.generation, self.tree_project_generation, self.tree_preview_serial, key)
        if token in (self.tree_highlight_token, self.tree_highlight_pending): return
        self.tree_highlight_pending = token
        def current():
            selected = self.selected_model_node()
            return (not self.closed and not self.dialog and not self.opening and not self.solving and
                selected and (self.generation,self.tree_project_generation,self.tree_preview_serial,selected['Key']) == token)
        def received(reply):
            if self.tree_highlight_pending == token: self.tree_highlight_pending = None
            if not current(): return
            if not reply.get('ok'):
                self.tree_details.set_text(reply.get('error','Unable to highlight this node set.')); return
            def delivered(view,result,_):
                try: applied = view.evaluate_javascript_finish(result).to_int32() > 0
                except GLib.Error: applied = False
                if current() and applied:
                    self.tree_highlight_key = key; self.tree_highlight_token = token
                    if args.smoke_tree_highlight: self.tree_highlight_smoke_ids = reply['nodes']
                    self.tree_details.set_text(node['Name'] + '\n%d nodes — highlighted in the current 3D view.' % len(reply['nodes']))
            self.tree_highlight_key = key
            self.send_tree_highlight(reply['nodes'], delivered)
        self.submit({'command':'tree-highlight','key':key},received)

    def send_tree_highlight(self, nodes, callback=None):
        message = {'type':'prepomax.selection.updated-v1','item':'node' if nodes else 'none',
            'ids':nodes,'actorNames':[],'faceNodeIds':[],'edgeNodeIds':[]}
        self.view.evaluate_javascript('window.__gtkDeliver(' + json.dumps(message) + ');',-1,None,None,None,callback,None)

    def check_tree_highlight_workflow(self):
        if self.closed: return False
        if (self.opening or self.tree_pending or self.dialog or self.view.is_loading() or self.tree_highlight_smoke_busy or
            self.stable_view_generation != self.generation): return True
        def select(key):
            path = self.tree_store.get_path(self.tree_iters[key])
            self.model_tree.expand_to_path(path); self.model_tree.get_selection().select_path(path)
        if self.tree_highlight_smoke_stage == 0 and args.smoke_field:
            if not self.displayed_field or self.displayed_field['Name']+'/'+self.displayed_field['Component'] != args.smoke_field:
                index = next((i+1 for i,f in enumerate(self.fields) if f['Name']+'/'+f['Component']==args.smoke_field),None)
                if index is None:
                    self.finish(False,'Tree highlight fixture has no requested result field'); return False
                self.field_combo.set_active(index); return True
        self.tree_highlight_smoke_busy = True
        setup = ''
        if self.tree_highlight_smoke_stage == 0:
            setup = "const c=document.querySelector('#camera-view'); c.value='left'; c.dispatchEvent(new Event('change',{bubbles:true})); "
            setup += 'window.__gtkDeliver('+json.dumps({'type':'prepomax.selection.updated-v1','item':'none','ids':[],
                'actorNames':[],'faceNodeIds':[],'edgeNodeIds':[]})+'); '
        script = '(() => {'+setup+"return document.querySelector('#diagnostics').textContent; })()"
        generation = self.generation
        def read(view,result,_):
            self.tree_highlight_smoke_busy = False
            if self.closed or generation != self.generation or self.stable_view_generation != self.generation: return
            try:
                state = json.loads(view.evaluate_javascript_finish(result).to_string())['renderer']
                stage = self.tree_highlight_smoke_stage
                selection = state['selection']
                camera = [state[k] for k in ('cameraPosition','cameraFocalPoint','cameraDirection','cameraViewUp','cameraParallelScale')]
                if stage == 0:
                    self.tree_highlight_smoke_camera = camera
                    self.tree_highlight_smoke_scene = self.scene['SceneId']
                    self.tree_highlight_smoke_generation = self.generation
                    self.tree_highlight_smoke_field = self.displayed_field
                    self.tree_highlight_smoke_scale = self.displayed_scale
                    self.tree_highlight_smoke_stage = 1; select('model/mesh/node-sets/LEFT'); return
                if stage in (1,2,5,6):
                    name = 'LEFT' if stage in (1,5,6) else 'RIGHT'
                    if self.tree_highlight_key != 'model/mesh/node-sets/'+name or self.tree_highlight_token is None: return
                    if selection['item']!='node' or selection['selectedIdCount']!=4 or selection['overlayActors']<1: return
                    expected = [1,4,5,8] if name=='LEFT' else [2,3,6,7]
                    if self.tree_highlight_smoke_ids != expected: raise RuntimeError('Tree highlight membership mismatch')
                elif stage in (3,4):
                    if selection['selectedIdCount']!=0 or selection['overlayActors']!=0: return
                if stage < 6 and (camera != self.tree_highlight_smoke_camera or self.generation != self.tree_highlight_smoke_generation):
                    raise RuntimeError('Selecting a tree node changed the camera or reloaded the view (stage %s)' % stage)
                if self.scene['SceneId'] != self.tree_highlight_smoke_scene or self.displayed_field != self.tree_highlight_smoke_field or self.displayed_scale != self.tree_highlight_smoke_scale:
                    raise RuntimeError('Selecting a tree node changed the displayed field or scene')
                if stage == 1:
                    self.tree_highlight_smoke_stage = 2; select('model/mesh/node-sets/RIGHT')
                elif stage == 2:
                    self.tree_highlight_smoke_stage = 3; select('model/mesh/node-sets')
                elif stage == 3:
                    self.tree_highlight_smoke_stage = -1
                    select('model/mesh/node-sets/LEFT'); select('model/mesh/node-sets/RIGHT'); select('model/mesh/node-sets')
                    self.submit({'command':'tree-highlight','key':'model/mesh/node-sets/LEFT'},lambda _: setattr(self,'tree_highlight_smoke_stage',4))
                elif stage == 4:
                    self.tree_highlight_smoke_stage = 5; select('model/mesh/node-sets/LEFT')
                elif stage == 5:
                    self.tree_highlight_smoke_stage = 6; self.retry_view()
                elif stage == 6:
                    self.finish(True,'PASS: automatic tree highlights, exact membership, switching/clearing, stale-response refusal, camera/field preservation and reapply after reload')
            except (ValueError,KeyError,TypeError,RuntimeError,GLib.Error) as error:
                self.finish(False,'Tree highlighting: '+str(error))
        self.view.evaluate_javascript(script,-1,None,None,None,read,None)
        return True

    def model_tree_mouse(self, tree, event):
        if event.button != 3:
            return False
        hit = tree.get_path_at_pos(int(event.x), int(event.y))
        if hit:
            tree.get_selection().select_path(hit[0])
            return self.model_tree_menu(event)
        return False

    def model_tree_menu(self, event=None):
        node = self.selected_model_node()
        if not node or self.opening or self.solving or self.dialog:
            return False
        menu = Gtk.Menu()
        editable = node['Editor']
        for action, label, enabled in (
                ('add', 'Create…', bool(node['Creator'])), ('edit', 'Edit / rename…', bool(editable)),
                ('preview', 'Preview region', bool(node['RegionKind'])),
                ('assign-section', 'Create section on region…', node['RegionKind'] in ('part', 'element-set')),
                ('duplicate', 'Duplicate…', editable in ('step', 'section', 'field-output', 'history-output', 'element-set', 'node-set', 'surface')),
                ('activate' if node['Active'] is False else 'deactivate',
                 'Activate' if node['Active'] is False else 'Deactivate',
                 editable in ('step', 'section', 'field-output', 'history-output')),
                ('remove', 'Remove', editable in ('step', 'section', 'field-output', 'history-output', 'element-set', 'node-set', 'surface'))):
            item = Gtk.MenuItem(label=label)
            item.set_sensitive(enabled)
            item.connect('activate', lambda _, selected_action=action: self.model_tree_action(selected_action))
            menu.append(item)
        menu.show_all()
        if event:
            menu.popup_at_pointer(event)
        else:
            menu.popup_at_widget(self.model_tree, Gdk.Gravity.CENTER, Gdk.Gravity.CENTER, None)
        return True

    def model_tree_action(self, action):
        node = self.selected_model_node()
        if not node or self.opening or self.solving or self.dialog or self.tree_pending:
            return
        if action == 'assign-section':
            if node['RegionKind'] not in ('part', 'element-set'): return
            generation = self.tree_project_generation
            self.tree_pending = True
            def sections(data):
                if generation != self.tree_project_generation: return
                self.tree_pending = False
                if self.opening or self.solving or self.dialog: return
                if not data.get('ok') or data.get('editingError'):
                    self.tree_details.set_text(data.get('editingError') or data.get('error', 'Section assignment is unavailable.'))
                    return
                region_type = 'PartName' if node['RegionKind'] == 'part' else 'ElementSetName'
                self.section_form('add', None, data, (region_type, node['Name']))
            self.submit({'command': 'section-data'}, sections)
            return
        if action == 'preview':
            if not node['RegionKind']:
                return
            serial, generation = self.tree_preview_serial, self.tree_project_generation
            def resolved(reply):
                if serial != self.tree_preview_serial or generation != self.tree_project_generation or self.dialog or self.opening:
                    return
                if not reply.get('ok'):
                    self.tree_details.set_text(reply.get('error', 'Unable to preview this region.'))
                    return
                selected = reply['selection']
                self.preview_nodes(selected['Nodes'], self.model_tree_previewed, selected['Faces'])
            self.submit({'command': 'tree-selection', 'key': node['Key']}, resolved)
            return
        editor = node['Creator'] if action == 'add' else node['Editor']
        if not editor:
            self.tree_details.set_text('Editing is not available for this object yet.')
            return
        if editor in ('step', 'section', 'field-output', 'history-output'):
            self.edit_tree_definition(node, editor, action)
        elif editor == 'element-set':
            self.edit_element_set(node, action)
        elif editor in ('node-set', 'surface'):
            self.edit_mesh_region(node, editor, action)
        elif editor == 'material':
            if action == 'add': self.add_material()
            else: self.edit_properties(target=node)
        elif editor in ('force', 'boundary'):
            self.edit_properties(force=editor == 'force', boundary=editor == 'boundary', create=action == 'add', target=node)
        elif editor == 'pressure':
            self.edit_pressure(target=node)
        elif editor == 'gravity':
            self.add_gravity(target=node)
        elif editor == 'centrifugal':
            self.edit_centrifugal(target=node)

    def model_tree_previewed(self):
        node = self.selected_model_node()
        if node:
            self.tree_details.set_text(node['Name'] + '\nRegion preview on undeformed mesh.')
        if args.smoke_model_tree and self.tree_smoke_stage == 1:
            self.tree_smoke_stage = 2
            self.check_model_tree_editor()
        if args.smoke_element_sets and self.element_set_smoke_stage == 2:
            self.element_set_smoke_stage = 3
            self.model_tree_action('assign-section')

    def create_node_set_from_tree(self):
        if self.opening or self.solving or self.dialog or self.tree_pending: return
        key = 'model/mesh/node-sets'
        if key not in self.tree_iters: return
        path = self.tree_store.get_path(self.tree_iters[key])
        self.model_tree.expand_to_path(path)
        self.model_tree.get_selection().select_path(path)
        self.model_tree_action('add')

    def edit_mesh_region(self, node, kind, action):
        generation = self.tree_project_generation
        self.tree_pending = True
        request = {'command': 'node-set-data' if kind == 'node-set' else 'surface-editor-data'}
        if action != 'add': request['region'] = node['Name']
        def received(reply):
            if generation != self.tree_project_generation: return
            self.tree_pending = False
            if self.opening or self.solving or self.dialog: return
            if not reply.get('ok'):
                self.label.set_text(reply.get('error', 'Unable to read region data')); return
            data = reply['data']
            if data.get('EditingError'):
                self.label.set_text(data['EditingError']); return
            items = data['Sets'] if kind == 'node-set' else data['Surfaces']
            summary = next((item for item in items if item['Name'] == node['Name']), None)
            if action == 'remove':
                if summary and summary['Dependents']:
                    self.label.set_text('Reassign before removing: ' + '; '.join(summary['Dependents'][:10]))
                else: self.save_edit({'command': 'remove-' + kind, 'region': node['Name']})
                return
            self.mesh_region_form(kind, action, data, summary)
        self.submit(request, received)

    def node_selection_form(self, action, data, summary):
        selected = data.get('Selected')
        dialog = Gtk.Dialog(title=action.capitalize() + ' node set — mesh selection', transient_for=self.window, modal=False)
        dialog.set_default_size(410, 510)
        dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
        self.dialog = dialog
        box = dialog.get_content_area()
        box.set_spacing(5); box.set_border_width(8)
        name = Gtk.Entry(); name.set_max_length(128)
        name.set_text(selected['Name'] if action == 'edit' else selected['Name'][:120] + '-copy' if selected else 'Node_Set')
        name.get_accessible().set_name('Node set name')
        box.pack_start(Gtk.Label(label='Node set name', xalign=0), False, False, 0)
        box.pack_start(name, False, False, 0)
        count = Gtk.Label(label='Loading node membership…', xalign=0)
        box.pack_start(count, False, False, 0)
        controls = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=5)
        box.pack_start(controls, False, False, 0)
        mode = Gtk.ComboBoxText()
        for key, text in (('node','Node'),('element','Element'),('edge','Edge'),('face','Face'),
                          ('edge-angle','Edge angle'),('face-angle','Face angle'),('part','Part'),('id','ID')):
            mode.append(key, text)
        mode.set_active_id('node'); mode.get_accessible().set_name('Mesh selection mode')
        controls.pack_start(mode, False, False, 0)
        angle = Gtk.SpinButton.new_with_range(0, 180, 1); angle.set_value(30); angle.set_digits(1)
        angle.get_accessible().set_name('Selection angle in degrees')
        controls.pack_start(angle, False, False, 0)
        mode.connect('changed', lambda *_: angle.set_sensitive(mode.get_active_id() in ('edge-angle','face-angle')))
        angle.set_sensitive(False)
        rectangle = Gtk.CheckButton(label='Rectangle selection — drag in viewport')
        rectangle.connect('toggled', lambda *_: self.node_selection_refresh())
        controls.pack_start(rectangle, False, False, 0)
        def changed_mode(*_):
            available = mode.get_active_id() in ('node','element','part')
            if not available: rectangle.set_active(False)
            rectangle.set_sensitive(available)
        mode.connect('changed', changed_mode)
        help_text = Gtk.Label(label='Pick in the viewport. Shift adds, Ctrl subtracts, Shift+Ctrl intersects.\n'
            'Face selects the whole mesh face. Angle modes grow from a single click.\n'
            'Enable Rectangle selection for Node, Element or Part selection through the mesh. Switch it off to rotate or pick.', xalign=0, wrap=True)
        help_text.set_max_width_chars(52); controls.pack_start(help_text, False, False, 0)
        ids = Gtk.Entry(); ids.set_max_length(60000); ids.set_placeholder_text('IDs or START:END[:STEP]')
        ids.get_accessible().set_name('Selection node IDs and ranges'); controls.pack_start(ids, False, False, 0)
        row = Gtk.Box(spacing=4)
        for text, operation in (('Replace IDs','replace'),('Add IDs','add'),('Remove IDs','subtract')):
            button = Gtk.Button(label=text)
            button.connect('clicked', lambda _, op=operation: self.node_selection_request({'command':'node-selection-ids', 'ids':ids.get_text()}, op))
            row.pack_start(button, True, True, 0)
        controls.pack_start(row, False, False, 0)
        row = Gtk.Box(spacing=4)
        undo = Gtk.Button(label='Undo selection')
        def undone(*_):
            if self.node_selection is state and not state['busy']:
                state['history'].undo(); self.node_selection_refresh()
        undo.connect('clicked', undone); row.pack_start(undo, True, True, 0)
        clear = Gtk.Button(label='Clear')
        def cleared(*_):
            if self.node_selection is state and not state['busy']:
                state['history'].apply([]); self.node_selection_refresh()
        clear.connect('clicked', cleared); row.pack_start(clear, True, True, 0)
        all_buttons = {}
        for text, op in (('All','replace'),('Invert','invert')):
            button = Gtk.Button(label=text)
            button.connect('clicked', lambda _, operation=op: self.node_selection_request({'command':'node-selection-all'}, operation))
            row.pack_start(button, True, True, 0)
            all_buttons[text] = button
        controls.pack_start(row, False, False, 0)
        box_expander = Gtk.Expander(label='Coordinate box')
        grid = Gtk.Grid(column_spacing=4, row_spacing=4); limits = {}
        for row_index, axis in enumerate(('X','Y','Z')):
            grid.attach(Gtk.Label(label=axis), 0, row_index, 1, 1)
            for col, side in enumerate(('Min','Max'), 1):
                key = axis + side
                entry = Gtk.Entry(); entry.set_width_chars(10); entry.set_text(format(data['Bounds'][key], '.17g'))
                entry.get_accessible().set_name(key); limits[key] = entry
                grid.attach(entry, col, row_index, 1, 1)
        use_box = Gtk.Button(label='Replace from box')
        def boxed(*_):
            try:
                values = {key:float(entry.get_text()) for key,entry in limits.items()}
                if any(not math.isfinite(v) for v in values.values()) or any(values[a+'Min'] > values[a+'Max'] for a in ('X','Y','Z')):
                    raise ValueError('Use finite box limits with minimum <= maximum.')
                self.node_selection_request({'command':'select-node-box', 'box':values}, 'replace')
            except (ValueError, OverflowError) as error: message.set_text(str(error))
        use_box.connect('clicked', boxed); grid.attach(use_box, 0, 3, 3, 1)
        box_expander.add(grid); controls.pack_start(box_expander, False, False, 0)
        text = 'Selection Undo stores the last 32 changes. Apply enters model Undo/Redo.'
        locked = action == 'duplicate' or bool(summary and summary['MembershipLocked'])
        if locked: text = 'Membership is retained. Rename or duplicate this set; edit dependent element surfaces to change their nodes.'
        if summary and summary['Dependents']: text += '\nUsed by: ' + '; '.join(summary['Dependents'][:8])
        message = Gtk.Label(label=text, xalign=0, wrap=True); message.set_max_width_chars(52)
        box.pack_start(message, False, False, 0)
        state = {'dialog':dialog, 'history':NodeSelection(), 'mode':mode, 'angle':angle, 'count':count,
                 'message':message, 'controls':controls, 'undo':undo, 'busy':True, 'locked':locked,
                 'generation':self.tree_project_generation, 'name':name, 'ids':ids, 'revision':0,
                 'clear':clear, 'all':all_buttons['All'], 'invert':all_buttons['Invert'], 'rectangle':rectangle,
                 'pending':[], 'initialized':False}
        self.node_selection = state
        def configured(_, response):
            if self.node_selection is not state: return
            if response == Gtk.ResponseType.ACCEPT:
                if state['busy']: return
                region_name = name.get_text().strip()
                if not region_name or any(item['Name'].lower() == region_name.lower() and
                    not (action == 'edit' and selected and item['Name'] == selected['Name']) for item in data['Sets']):
                    message.set_text('Enter a unique node-set name.'); return
                if not state['history'].nodes:
                    message.set_text('Select at least one mesh node.'); return
                change = {'command':('create' if action == 'add' else action) + '-node-set',
                          'settings':{'Name':region_name, 'SelectedNodes':state['history'].nodes}}
                if selected: change['region'] = selected['Name']
                if action == 'duplicate': change = {'command':'duplicate-node-set', 'region':selected['Name'], 'name':region_name}
            self.node_selection = None
            self.submit({'command':'node-selection-end'}, lambda _: None)
            self.node_selection_highlight([])
            dialog.destroy(); self.dialog = None
            if response == Gtk.ResponseType.ACCEPT: self.save_edit(change)
            else: GLib.idle_add(self.highlight_selected_node_set)
        dialog.connect('response', configured)
        dialog.show_all(); controls.set_sensitive(False); dialog.set_response_sensitive(Gtk.ResponseType.ACCEPT, False)
        def initialize():
            if self.node_selection is not state: return
            request = {'command':'node-selection-initial'}
            if selected: request['region'] = selected['Name']
            self.node_selection_request(request, initial=True)
        self.preview_nodes([], after=initialize)
        if args.smoke_region_maintenance:
            def automate():
                if self.node_selection is not state: return False
                if state['busy'] or self.opening: return True
                if action == 'duplicate': name.set_text('spare-nodes'); dialog.response(Gtk.ResponseType.ACCEPT)
                elif selected['Name'] == 'LEFT':
                    name.set_text('renamed-left'); limits['XMax'].set_text(limits['XMin'].get_text()); use_box.clicked()
                    GLib.timeout_add(100, accept_when_ready)
                else:
                    name.set_text('edited-spare-nodes'); ids.set_text('1,2')
                    self.node_selection_request({'command':'node-selection-ids', 'ids':'1,2'}, 'replace')
                    GLib.timeout_add(100, accept_when_ready)
                return False
            def accept_when_ready():
                if self.node_selection is not state: return False
                if state['busy']: return True
                dialog.response(Gtk.ResponseType.ACCEPT); return False
            GLib.timeout_add(100, automate)

    def node_selection_highlight(self, nodes):
        # Existing embedded renderer API updates highlights without replacing the scene or resetting the camera.
        message = {'type':'prepomax.selection.updated-v1', 'item':'node' if nodes else 'none',
                   'ids':nodes, 'actorNames':[], 'faceNodeIds':[], 'edgeNodeIds':[]}
        script = ('' if self.node_selection else 'window.__gtkClearSelectionMessages?.(); ') + 'window.__gtkDeliver(' + json.dumps(message) + '); window.__gtkDeliver(' + json.dumps({
            'type':'prepomax.node-selection.mode-v1', 'active':self.node_selection is not None,
            'rectangle':bool(self.node_selection and self.node_selection['rectangle'].get_active())}) + ');'
        self.view.evaluate_javascript(script, -1, None, None, None, None, None)

    def node_selection_refresh(self):
        state = self.node_selection
        if state is None: return
        nodes = state['history'].nodes
        state['count'].set_text('%d selected nodes' % len(nodes))
        state['undo'].set_sensitive(len(state['history'].history) > 1)
        state['controls'].set_sensitive(state['initialized'] and not state['busy'] and not state['locked'])
        state['dialog'].set_response_sensitive(Gtk.ResponseType.ACCEPT, state['initialized'] and not state['busy'] and bool(nodes))
        self.node_selection_highlight(nodes)

    def node_selection_request(self, request, operation='replace', initial=False):
        state = self.node_selection
        if state is None or (not initial and state['locked']): return
        if not initial and state['busy']:
            if len(state['pending']) < 32: state['pending'].append((request,operation))
            else: state['message'].set_text('Selection queue is full. Wait for the pending picks.')
            return
        if not initial and not state['initialized']: return
        state['busy'] = True
        state['controls'].set_sensitive(False); state['dialog'].set_response_sensitive(Gtk.ResponseType.ACCEPT, False)
        def received(reply):
            if self.node_selection is not state or state['generation'] != self.tree_project_generation: return
            state['busy'] = False
            if reply.get('ok'):
                try:
                    if initial:
                        state['history'] = NodeSelection(reply['nodes']); state['initialized'] = True
                    else: state['history'].apply(reply['nodes'], operation)
                    state['revision'] += 1
                    state['message'].set_text('Selection ready. Pick again, change mode, undo selection or Apply.')
                except (ValueError, KeyError, TypeError) as error: state['message'].set_text(str(error))
            else: state['message'].set_text(reply.get('error', 'Unable to resolve mesh nodes.'))
            self.node_selection_refresh()
            if state['pending']:
                pending, pending_operation = state['pending'].pop(0)
                self.node_selection_request(pending, pending_operation)
        self.submit(request, received)

    def selection_message_received(self, manager, result):
        if self.closed or self.opening or self.solving or not self.address or self.view.get_uri() != self.address: return
        try:
            text = result.get_js_value().to_string()
            if len(text) > 4 * 1024 * 1024: return
            item = json.loads(text)
            kind = item.get('type')
            if kind == 'prepomax.pick.validated-v1' and len(text) <= 1024: self.accept_face_pick(item)
            elif kind == 'prepomax.area.validated-v1': self.accept_area_pick(item)
            elif kind == 'prepomax.node-selection.frustum-v1': self.accept_node_frustum(item)
        except (ValueError, TypeError, AttributeError): return

    def accept_node_pick(self, item):
        state = self.node_selection
        if state is None or self.opening or self.solving or not item.get('validated') or state['mode'].get_active_id() == 'id': return
        try:
            request = {'command':'node-selection-pick', 'pick':{'Mode':state['mode'].get_active_id(),
                'ElementId':int(item['sourceElementId']), 'TriangleNodes':item['triangleSourceNodeIds'],
                'Point':item['worldPosition'], 'Angle':state['angle'].get_value()}}
            self.node_selection_request(request, item.get('operation','replace'))
        except (KeyError, TypeError, ValueError): state['message'].set_text('The viewport pick is incomplete. Pick a mesh face.')

    def accept_node_area(self, item):
        state = self.node_selection
        if state is None or self.opening or self.solving or not item.get('validated'): return
        try:
            if item.get('locatorValidated'):
                nodes, elements = item['sourceNodeIds'], item['sourceElementIds']
            else:
                nodes = sorted({n for triangle in item['triangles'] for n in triangle['triangleSourceNodeIds']})
                elements = sorted({triangle['sourceElementId'] for triangle in item['triangles']})
            self.node_selection_request({'command':'node-selection-area', 'area':{'Mode':state['mode'].get_active_id(),
                'NodeIds':nodes, 'ElementIds':elements, 'CompletelyInside':bool(item.get('completelyInside'))}}, item.get('operation','replace'))
        except (KeyError, TypeError, ValueError): state['message'].set_text('The viewport rectangle selection is incomplete.')

    def accept_node_frustum(self, item):
        state = self.node_selection
        if state is None or self.opening or self.solving: return
        try:
            frustum = item['frustum']
            section = frustum.get('sectionPlane') or {}
            self.node_selection_request({'command':'node-selection-frustum','frustum':{
                'Mode':state['mode'].get_active_id(),'Corners':frustum['corners'],
                'SectionPoint':section.get('point'),'SectionNormal':section.get('normal')}},item.get('operation','replace'))
        except (KeyError,TypeError,ValueError): state['message'].set_text('The viewport rectangle is incomplete.')

    def check_node_selection_workflow(self):
        if self.closed: return False
        if self.opening or self.tree_pending: return True
        state = self.node_selection
        if state and state['busy']: return True
        stage = self.node_smoke_stage
        def select_tree(key, action):
            path = self.tree_store.get_path(self.tree_iters[key])
            self.model_tree.expand_to_path(path); self.model_tree.get_selection().select_path(path)
            self.model_tree_action(action)
        def require(value, text):
            if not value: raise RuntimeError(text)
        try:
            if stage == 0:
                self.node_smoke_stage=1
                select_tree('model/mesh/node-sets','add')
            elif stage == 1 and state:
                require(not state['dialog'].get_modal(),'Node-set editor must leave viewport interactive')
                state['name'].set_text('picked-support')
                self.node_smoke_scene=self.scene['SceneId']; self.node_smoke_index=0
                self.node_smoke_revision=state['revision']; self.node_smoke_waiting=False
                self.node_smoke_stage=2
            elif stage == 2:
                require(state is not None,'Selection editor closed during picking')
                sequence=[('node',{},1),('element',{},8),('edge',{},2),('face',{},4),
                    ('edge-angle',{},2),('face-angle',{},8),('part',{},8),
                    ('node',{'ctrlKey':True},7),('node',{'shiftKey':True},8),
                    ('node',{'ctrlKey':True,'shiftKey':True},1)]
                if self.node_smoke_waiting:
                    if state['revision']==self.node_smoke_revision: return True
                    mode,modifiers,expected=sequence[self.node_smoke_index]
                    require(len(state['history'].nodes)==expected,'%s pick membership mismatch: %s' % (mode,state['history'].nodes))
                    require(self.scene['SceneId']==self.node_smoke_scene,'Picking replaced the mesh scene/camera')
                    print('PASS: live mesh %s %s selects %d nodes (click-to-check %.0f ms)' % (
                        mode,modifiers,expected,(time.monotonic()-self.node_smoke_clicked)*1000),flush=True)
                    self.node_smoke_index+=1; self.node_smoke_waiting=False
                if self.node_smoke_index==len(sequence):
                    state['undo'].clicked(); require(len(state['history'].nodes)==8,'Selection Undo mismatch')
                    state['clear'].clicked(); require(not state['history'].nodes,'Selection Clear mismatch')
                    state['mode'].set_active_id('element'); self.node_smoke_stage=30
                    self.node_smoke_revision=state['revision']; self.node_selection_smoke_rectangle(); return True
                mode,modifiers,_=sequence[self.node_smoke_index]
                state['mode'].set_active_id(mode); state['angle'].set_value(90 if mode=='face-angle' else 30)
                self.node_smoke_revision=state['revision']; self.node_smoke_waiting=True
                self.node_smoke_clicked=time.monotonic()
                script="""(() => {
                    const viewport=document.querySelector('#viewport');
                    if (!viewport) return false;
                    const b=viewport.getBoundingClientRect();
                    viewport.dispatchEvent(new MouseEvent('click', {bubbles:true, button:0,
                        clientX:b.left+b.width/2, clientY:b.top+b.height/2, ...%s}));
                    return true;
                })()""" % json.dumps(modifiers)
                self.view.evaluate_javascript(script,-1,None,None,None,self.surface_pick_dispatched,None)
            elif stage in (30,31,32):
                if state['revision']==self.node_smoke_revision: return True
                require(len(state['history'].nodes)==8,'Full-mesh rectangle membership mismatch (%s): %s' % (state['mode'].get_active_id(),state['history'].nodes))
                print('PASS: live full-mesh rectangle in %s mode' % state['mode'].get_active_id(),flush=True)
                if stage==32:
                    self.node_smoke_stage=3; state['all'].clicked()
                else:
                    state['mode'].set_active_id('part' if stage==30 else 'node')
                    self.node_smoke_stage=stage+1; self.node_smoke_revision=state['revision']; self.node_selection_smoke_rectangle()
            elif stage == 3:
                require(len(state['history'].nodes)==8,'Visible All mismatch')
                self.node_smoke_stage=4; state['invert'].clicked()
            elif stage == 4:
                require(not state['history'].nodes,'Visible Invert mismatch')
                state['undo'].clicked(); require(len(state['history'].nodes)==8,'Invert Undo mismatch')
                self.node_smoke_stage=33
                self.node_selection_request({'command':'node-selection-ids','ids':'1,4'})
                self.node_selection_request({'command':'node-selection-ids','ids':'5,8'},'add')
            elif stage == 33:
                require(state['history'].nodes==[1,4,5,8],'Queued selection operations were lost or reordered')
                self.node_smoke_stage=5
            elif stage == 5:
                require(state['history'].nodes==[1,4,5,8],'ID selection mismatch')
                self.node_smoke_stage=6; state['dialog'].response(Gtk.ResponseType.ACCEPT)
            elif stage == 6 and not state and 'model/mesh/node-sets/picked-support' in self.tree_nodes:
                self.node_smoke_stage=7; select_tree('model/mesh/node-sets/picked-support','edit')
            elif stage == 7 and state:
                require(state['history'].nodes==[1,4,5,8],'Editing did not initialize complete existing membership')
                self.node_smoke_stage=8; self.node_selection_request({'command':'node-selection-ids','ids':'1,2'})
            elif stage == 8:
                require(state['history'].nodes==[1,2],'Provisional edit mismatch')
                self.node_selection_request({'command':'node-selection-ids','ids':'3,4'})
                state['dialog'].response(Gtk.ResponseType.CANCEL)
                self.node_smoke_stage=9
                def checked(reply):
                    if not reply.get('ok') or reply.get('nodes')!=[1,4,5,8]:
                        self.finish(False,'Cancelling selection changed saved membership'); return
                    self.node_smoke_stage=10
                self.submit({'command':'node-selection-initial','region':'picked-support'},checked)
            elif stage == 10:
                self.node_smoke_stage=11
                self.save_edit({'command':'add-boundary','step':'load-1','name':'picked-bc','region':'picked-support','direction':1,'value':0})
            elif stage == 11 and self.session.dirty:
                self.node_smoke_stage=12; select_tree('model/mesh/node-sets/picked-support','edit')
            elif stage == 12 and state:
                state['name'].set_text('mesh-support'); self.node_smoke_stage=13
                state['dialog'].response(Gtk.ResponseType.ACCEPT)
            elif stage == 13 and 'model/mesh/node-sets/mesh-support' in self.tree_nodes:
                self.node_smoke_stage=14; self.move_session(-1)
            elif stage == 14 and 'model/mesh/node-sets/picked-support' in self.tree_nodes:
                self.node_smoke_stage=15; self.move_session(1)
            elif stage == 15 and 'model/mesh/node-sets/mesh-support' in self.tree_nodes:
                self.node_smoke_stage=16
                self.save_session(choose=True,after=lambda:self.open_project(str(Path(args.smoke_node_selection).resolve())))
            elif stage == 16 and self.session.source==Path(args.smoke_node_selection).resolve() and not self.session.dirty:
                require('model/mesh/node-sets/mesh-support' in self.tree_nodes,'Save/reopen lost selected node set')
                require(Path(args.project).read_bytes()==self.node_smoke_original,'Source project changed')
                self.finish(True,'PASS: live tree node-set modes/modifiers, selection Undo/Clear/All/Invert, edit/cancel, support assignment, rename, model Undo/Redo and Save/reopen')
                return False
        except (KeyError,TypeError,ValueError,RuntimeError) as error:
            self.finish(False,'Node selection workflow: '+str(error)); return False
        return True

    def node_selection_smoke_rectangle(self):
        self.node_selection['rectangle'].set_active(True)
        script="""(() => {
            const viewport=document.querySelector('#viewport');
            const camera=document.querySelector('#camera-view');
            if (!viewport || !camera) return false;
            camera.value='isometric'; camera.dispatchEvent(new Event('change',{bubbles:true}));
            requestAnimationFrame(() => requestAnimationFrame(() => {
                const b=viewport.getBoundingClientRect();
                const start={clientX:b.left+b.width*.001,clientY:b.top+b.height*.001};
                const end={clientX:b.left+b.width*.999,clientY:b.top+b.height*.999};
                const options={bubbles:true,pointerId:7,pointerType:'mouse',isPrimary:true,button:0,buttons:1};
                viewport.dispatchEvent(new PointerEvent('pointerdown',{...options,...start}));
                viewport.dispatchEvent(new PointerEvent('pointermove',{...options,...end}));
                viewport.dispatchEvent(new PointerEvent('pointerup',{...options,...end,buttons:0}));
            }));
            return true;
        })()"""
        self.view.evaluate_javascript(script,-1,None,None,None,self.surface_pick_dispatched,None)

    def mesh_region_form(self, kind, action, data, summary):
        nodal = kind == 'node-set'
        if nodal:
            self.node_selection_form(action, data, summary)
            return
        selected = data.get('Selected')
        title = 'node set' if nodal else 'surface'
        dialog = Gtk.Dialog(title=action.capitalize() + ' ' + title, transient_for=self.window, modal=True)
        self.dialog = dialog
        dialog.set_default_size(560, 420)
        dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
        box = dialog.get_content_area()
        name, members = Gtk.Entry(), Gtk.Entry()
        name.set_max_length(128); members.set_max_length(60000 if nodal else 6000)
        name.set_text(selected['Name'] if action == 'edit' else selected['Name'][:120] + '-copy' if selected else kind)
        if selected:
            members.set_text(selected.get('NodeIds') or '' if nodal else ' '.join('%s:%s' % (face['ElementId'], face['Face']) for face in selected.get('Faces') or []))
        elif not nodal and self.picked_surface_faces:
            members.set_text(' '.join('%s:%s' % (face['ElementId'], face['Face']) for face in self.picked_surface_faces))
        name.get_accessible().set_name('Region name')
        members.get_accessible().set_name('Node IDs / ranges' if nodal else 'Element faces')
        box.pack_start(Gtk.Label(label=title.capitalize() + ' name', xalign=0), False, False, 3)
        box.pack_start(name, False, False, 3)
        keep = Gtk.CheckButton(label='Keep existing membership')
        keep.set_active(bool(selected))
        if selected: box.pack_start(keep, False, False, 3)
        source = Gtk.ComboBoxText()
        source.append_text('Node IDs / ranges'); source.append_text('Coordinate box'); source.set_active(0)
        if nodal: box.pack_start(source, False, False, 3)
        box.pack_start(Gtk.Label(label='IDs or START:END[:STEP] ranges' if nodal else 'Element:face pairs, for example 1:S4 2:S4', xalign=0), False, False, 3)
        box.pack_start(members, False, False, 3)
        if nodal:
            sample = ', '.join(map(str, data['SampleNodeIds'][:32]))
            available_text = '%d nodes; sample IDs: %s' % (data['Nodes'], sample)
        else:
            inventory = data['Inventory']
            sample = ', '.join('%s (%s)' % (item['Id'], '/'.join(item['Faces'])) for item in inventory['Elements'][:20])
            available_text = '%d solid elements; sample faces: %s' % (inventory['Total'], sample)
        available = Gtk.Label(label=available_text, xalign=0)
        available.set_max_width_chars(70); available.set_ellipsize(3); available.set_tooltip_text(available_text)
        box.pack_start(available, False, False, 3)
        limits = {}
        grid = Gtk.Grid(column_spacing=6, row_spacing=4)
        if nodal:
            for row, axis in enumerate(('X', 'Y', 'Z')):
                grid.attach(Gtk.Label(label=axis), 0, row, 1, 1)
                for column, side in enumerate(('Min', 'Max'), 1):
                    key = axis + side
                    entry = Gtk.Entry(); entry.set_width_chars(12)
                    entry.set_text(format(data['Bounds'][key], '.17g'))
                    entry.get_accessible().set_name(key)
                    limits[key] = entry; grid.attach(entry, column, row, 1, 1)
            box.pack_start(Gtk.Label(label='Inclusive minimum / maximum in model length units', xalign=0), False, False, 2)
            box.pack_start(grid, False, False, 4)
        use_view = Gtk.Button(label='Use nodes from selected faces' if nodal else 'Use selected faces')
        box.pack_start(use_view, False, False, 3)
        def use_selection(*_):
            if not self.picked_surface_faces:
                message.set_text('Select mesh faces in the viewport before opening the editor.'); return
            if not nodal:
                members.set_text(' '.join('%s:%s' % (face['ElementId'], face['Face']) for face in self.picked_surface_faces))
                return
            def resolved(reply):
                if self.dialog is not dialog: return
                if reply.get('ok'):
                    source.set_active(0); members.set_text(', '.join(map(str, reply['nodes'])))
                else: message.set_text(reply.get('error', 'Unable to resolve selected face nodes.'))
            self.submit({'command': 'surface-face-nodes', 'faces': self.picked_surface_faces}, resolved)
        use_view.connect('clicked', use_selection)
        preview = Gtk.Button(label='Preview membership')
        box.pack_start(preview, False, False, 3)
        info = 'Apply updates this region and its named references. Results are invalidated.'
        if summary and summary['Dependents']: info += '\nUsed by: ' + '; '.join(summary['Dependents'][:10])
        locked = action == 'duplicate' or bool(nodal and summary and summary['MembershipLocked']) or bool(not nodal and selected and selected['Type'] == 'Node')
        if locked and action != 'duplicate': info += '\nMembership is managed by the dependent surface/node-set workflow. Rename and duplicate remain available.'
        if not nodal and selected and selected['Type'] == 'Node': info += '\nBacking node set: ' + str(selected.get('NodeSetName') or '(unavailable)')
        message = Gtk.Label(label=info, xalign=0, wrap=True); box.pack_start(message, False, False, 5)
        keep.set_sensitive(not locked)
        def sensitivity(*_):
            replacing = not keep.get_active() and not locked
            source.set_sensitive(replacing)
            members.set_sensitive(replacing and (not nodal or source.get_active() == 0))
            grid.set_sensitive(replacing and source.get_active() == 1)
            use_view.set_sensitive(replacing)
        keep.connect('toggled', sensitivity); source.connect('changed', sensitivity); sensitivity()
        def settings():
            region_name = name.get_text().strip()
            items = data['Sets'] if nodal else data['Surfaces']
            if not region_name or any(item['Name'].lower() == region_name.lower() and
                not (action == 'edit' and selected and item['Name'] == selected['Name']) for item in items):
                raise ValueError('Enter a unique region name.')
            result = {'Name': region_name}
            if keep.get_active(): return result
            if nodal:
                if source.get_active() == 1:
                    values = {key: float(entry.get_text()) for key, entry in limits.items()}
                    if not all(math.isfinite(value) for value in values.values()) or any(values[axis+'Min'] > values[axis+'Max'] for axis in ('X','Y','Z')):
                        raise ValueError('Use finite bounds with minimum <= maximum.')
                    result['Box'] = values
                else:
                    text = ' '.join(members.get_text().replace(',', ' ').split())
                    if not text or any(character not in '0123456789: ' for character in text): raise ValueError('Use positive IDs or START:END[:STEP].')
                    result['NodeIds'] = text
            else:
                faces = {}
                for token in members.get_text().replace(',', ' ').split():
                    pair = token.split(':')
                    if len(pair) != 2 or not pair[0].isascii() or not pair[0].isdigit() or pair[1].upper() not in ('S1','S2','S3','S4','S5','S6'):
                        raise ValueError('Use element:face pairs, for example 1:S4.')
                    face = {'ElementId': int(pair[0]), 'Face': pair[1].upper()}
                    faces[(face['ElementId'], face['Face'])] = face
                if not 1 <= len(faces) <= 100: raise ValueError('Choose 1 to 100 solid faces.')
                result['Faces'] = list(faces.values())
            return result
        previewed = {'shown': False}
        def preview_membership(*_):
            try: values = settings()
            except (ValueError, OverflowError) as error: message.set_text(str(error)); return
            request = {'command': 'preview-' + kind, 'settings': values}
            if selected: request['region'] = selected['Name']
            preview.set_sensitive(False)
            def resolved(reply):
                if self.dialog is not dialog: return
                preview.set_sensitive(True)
                if not reply.get('ok'): message.set_text(reply.get('error', 'Unable to preview membership.')); return
                previewed['shown'] = True
                self.preview_nodes(reply['selection']['Nodes'], faces=reply['selection']['Faces'])
            self.submit(request, resolved)
        preview.connect('clicked', preview_membership)
        def configured(_, response):
            if response != Gtk.ResponseType.ACCEPT:
                dialog.destroy(); self.dialog = None
                if previewed['shown']: self.preview_nodes([])
                return
            try: values = settings()
            except (ValueError, OverflowError) as error: message.set_text(str(error)); return
            change = {'command': ('create' if action == 'add' else action) + '-' + kind, 'settings': values}
            if selected: change['region'] = selected['Name']
            if action == 'duplicate': change = {'command': 'duplicate-' + kind, 'region': selected['Name'], 'name': values['Name']}
            dialog.destroy(); self.dialog = None
            self.save_edit(change)
        dialog.connect('response', configured); dialog.show_all()
        if args.smoke_region_maintenance:
            if kind == 'node-set':
                if action == 'duplicate': name.set_text('spare-nodes')
                elif selected['Name'] == 'LEFT':
                    name.set_text('renamed-left'); keep.set_active(False); source.set_active(1)
                    limits['XMax'].set_text(limits['XMin'].get_text())
                else:
                    name.set_text('edited-spare-nodes'); keep.set_active(False); source.set_active(0); members.set_text('1,2')
            else:
                if action == 'duplicate': name.set_text('spare-surface')
                else:
                    name.set_text('renamed-loaded' if selected['Name'] == 'LOADED' else 'edited-spare-surface')
                    keep.set_active(False); members.set_text('1:S4 2:S4' if selected['Name'] == 'LOADED' else '1:S4')
            dialog.emit('response', Gtk.ResponseType.ACCEPT)

    def edit_element_set(self, node, action):
        generation = self.tree_project_generation
        self.tree_pending = True
        request = {'command': 'element-set-data'}
        if action != 'add': request['set'] = node['Name']
        def received(reply):
            if generation != self.tree_project_generation: return
            self.tree_pending = False
            if self.opening or self.solving or self.dialog: return
            if not reply.get('ok') or reply['data'].get('EditingError'):
                self.label.set_text(reply.get('error') or reply['data']['EditingError'])
                return
            data = reply['data']
            summary = next((item for item in data['Sets'] if item['Name'] == node['Name']), None)
            if action == 'remove':
                if summary and summary['Dependents']:
                    self.label.set_text('Reassign before removing: ' + '; '.join(summary['Dependents'][:10]))
                else:
                    self.save_edit({'command': 'remove-element-set', 'set': node['Name']})
                return
            self.element_set_form(action, data, summary)
        self.submit(request, received)

    def element_set_form(self, action, data, summary):
        selected = data.get('Selected')
        dialog = Gtk.Dialog(title={'add': 'Create element set', 'edit': 'Edit element set', 'duplicate': 'Duplicate element set'}[action],
                            transient_for=self.window, modal=True)
        self.dialog = dialog
        dialog.set_default_size(560, 460)
        dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
        box = dialog.get_content_area()
        name = Gtk.Entry(); name.set_max_length(128)
        name.set_text(selected['Name'] if action == 'edit' else selected['Name'][:120] + '-copy' if selected else 'element-set')
        source = Gtk.ComboBoxText()
        modes = (['keep'] if selected else []) + ['ids', 'parts']
        for mode in modes:
            source.append_text({'keep': 'Keep existing membership', 'ids': 'Element IDs / ranges', 'parts': 'Whole mesh parts'}[mode])
        source.set_active(0)
        ids = Gtk.Entry(); ids.set_max_length(60000)
        ids.set_text(selected.get('ElementIds') or '' if selected else '')
        for title, widget in (('Element-set name', name), ('Membership source', source), ('Element IDs / ranges', ids)):
            widget.get_accessible().set_name(title)
            box.pack_start(Gtk.Label(label=title, xalign=0), False, False, 3)
            box.pack_start(widget, False, False, 3)
        box.pack_start(Gtk.Label(label='Examples: 10, 20, 30:100:10. Ranges are inclusive; IDs are validated against this mesh.',
                                wrap=True, xalign=0), False, False, 3)
        part_rows = Gtk.ListStore(bool, str, int)
        for part in data['Parts']:
            part_rows.append((bool(selected and part['Name'] in selected['Parts']), part['Name'], part['Elements']))
        part_tree = Gtk.TreeView(model=part_rows)
        part_tree.get_accessible().set_name('Element-set mesh parts')
        toggle = Gtk.CellRendererToggle()
        toggle.connect('toggled', lambda _, path: part_rows.set_value(part_rows.get_iter(path), 0, not part_rows[path][0]))
        part_tree.append_column(Gtk.TreeViewColumn('Use', toggle, active=0))
        part_tree.append_column(Gtk.TreeViewColumn('Mesh part', Gtk.CellRendererText(), text=1))
        part_tree.append_column(Gtk.TreeViewColumn('Elements', Gtk.CellRendererText(), text=2))
        scroll = Gtk.ScrolledWindow(); scroll.set_min_content_height(100); scroll.add(part_tree)
        box.pack_start(scroll, True, True, 4)
        view_selection = Gtk.Button(label='Use elements from view selection')
        view_selection.get_accessible().set_name('Use elements from view selection')
        box.pack_start(view_selection, False, False, 3)
        def use_picks(*_):
            picked = sorted({face['ElementId'] for face in self.picked_surface_faces})
            if not picked:
                message.set_text('Select mesh faces in the viewport first; their owning elements can populate the set.')
                return
            source.set_active(modes.index('ids'))
            ids.set_text(', '.join(map(str, picked)))
        view_selection.connect('clicked', use_picks)
        preview = Gtk.Button(label='Preview set nodes')
        preview.get_accessible().set_name('Preview element-set nodes')
        box.pack_start(preview, False, False, 3)
        inventory = ', '.join(map(str, data['SampleElementIds']))
        inventory_label = Gtk.Label(label='%d mesh elements. Sample IDs: %s' % (data['Elements'], inventory), xalign=0)
        inventory_label.set_ellipsize(3)
        inventory_label.set_tooltip_text(inventory)
        box.pack_start(inventory_label, False, False, 3)
        info = 'Apply updates the current model. Duplicate IDs are combined; results are invalidated.'
        if summary and summary['Dependents']: info += '\nUsed by: ' + '; '.join(summary['Dependents'][:10])
        if summary and summary['MembershipLocked'] and action != 'duplicate':
            info += '\nThis set backs a surface. Its name can change; membership belongs to the surface workflow.'
        if selected and not selected['CreatedFromParts'] and selected['ElementIds'] is None:
            info += '\nExisting membership is too large for the text field. Keep it to rename or duplicate the complete set.'
        message = Gtk.Label(label=info, xalign=0, wrap=True)
        box.pack_start(message, False, False, 5)
        locked = action == 'duplicate' or bool(summary and summary['MembershipLocked'])
        def sensitivity(*_):
            mode = modes[source.get_active()]
            ids.set_sensitive(mode == 'ids' and not locked)
            part_tree.set_sensitive(mode == 'parts' and not locked)
            view_selection.set_sensitive(not locked)
        source.set_sensitive(not locked)
        source.connect('changed', sensitivity); sensitivity()
        def settings():
            set_name = name.get_text().strip()
            if not set_name or any(item['Name'].lower() == set_name.lower() and
                not (action == 'edit' and item['Name'] == selected['Name']) for item in data['Sets']):
                raise ValueError('Enter a unique element-set name.')
            result = {'Name': set_name}
            mode = modes[source.get_active()]
            if mode == 'ids':
                expression = ' '.join(ids.get_text().replace(',', ' ').split())
                if not expression or any(character not in '0123456789: ' for character in expression):
                    raise ValueError('Enter positive IDs or START:END[:STEP] ranges.')
                result['ElementIds'] = expression
            elif mode == 'parts':
                result['Parts'] = [row[1] for row in part_rows if row[0]]
                if not result['Parts']: raise ValueError('Choose at least one mesh part.')
            return result
        previewed = {'shown': False}
        def preview_set(*_):
            try: values = settings()
            except ValueError as error: message.set_text(str(error)); return
            preview.set_sensitive(False)
            request = {'command': 'preview-element-set', 'settings': values}
            if selected: request['set'] = selected['Name']
            def received(reply):
                if self.dialog is not dialog: return
                preview.set_sensitive(True)
                if not reply.get('ok'): message.set_text(reply.get('error', 'Unable to preview the set.')); return
                previewed['shown'] = True
                self.preview_nodes(reply['selection']['Nodes'])
            self.submit(request, received)
        preview.connect('clicked', preview_set)
        def configured(_, response):
            if response != Gtk.ResponseType.ACCEPT:
                dialog.destroy(); self.dialog = None
                if previewed['shown']: self.preview_nodes([])
                return
            try: values = settings()
            except ValueError as error: message.set_text(str(error)); return
            change = {'command': action + '-element-set', 'settings': values}
            if action != 'add': change['set'] = selected['Name']
            if action == 'duplicate': change = {'command': 'duplicate-element-set', 'set': selected['Name'], 'name': values['Name']}
            dialog.destroy(); self.dialog = None
            self.save_edit(change)
        dialog.connect('response', configured); dialog.show_all()
        if args.smoke_element_sets:
            if action == 'add':
                name.set_text('authored-elements'); ids.set_text(str(data['SampleElementIds'][0]))
            elif action == 'edit':
                name.set_text('renamed-elements'); source.set_active(modes.index('parts'))
                for row in part_rows: row[0] = True
            else: name.set_text('spare-elements')
            dialog.emit('response', Gtk.ResponseType.ACCEPT)

    def edit_tree_definition(self, node, editor, action):
        generation = self.tree_project_generation
        self.tree_pending = True
        def listed(data):
            if generation != self.tree_project_generation:
                return
            self.tree_pending = False
            if self.opening or self.solving or self.dialog:
                return
            if not data.get('ok') or data.get('editingError'):
                self.tree_details.set_text(data.get('editingError') or data.get('error', 'This definition cannot be edited.'))
                return
            collection = 'steps' if editor == 'step' else 'sections' if editor == 'section' else 'outputs'
            selected = next((item for item in data[collection] if item['Name'] == node['Name'] and
                             (editor not in ('field-output', 'history-output') or item['Step'] == node['Step'])), None)
            if action != 'add' and selected is None:
                self.tree_details.set_text('The selected definition no longer exists.')
                return
            if action in ('add', 'edit', 'duplicate'):
                if editor == 'step': self.step_form(action, selected, data['steps'])
                elif editor == 'section': self.section_form(action, selected, data)
                elif editor == 'field-output': self.field_output_form(action, selected, data, node['Step'])
                else: self.history_output_form(action, selected, data, node['Step'])
            else:
                command = ('remove-' + editor) if action == 'remove' else editor + '-active'
                change = {'command': command, 'step' if editor == 'step' else 'section' if editor == 'section' else 'output': node['Name']}
                if editor in ('field-output', 'history-output'): change['step'] = node['Step']
                if action != 'remove': change['active'] = action == 'activate'
                self.save_edit(change)
        self.submit({'command': editor + '-data'}, listed)

    def check_model_tree_editor(self):
        # Select the second step's request: identical names in different steps must not alias.
        outputs = [node for node in self.tree_nodes.values() if node['Kind'] == 'field-output']
        target = outputs[-1]
        path = self.tree_store.get_path(self.tree_iters[target['Key']])
        self.model_tree.expand_to_path(path)
        self.model_tree.get_selection().select_path(path)
        self.model_tree.emit('row-activated', path, self.model_tree.get_column(0))
        def check():
            if self.tree_pending:
                return True
            if not self.dialog:
                return self.finish(False, 'Model tree did not open the selected request editor')
            def widgets(widget):
                yield widget
                if isinstance(widget, Gtk.Container):
                    for child in widget.get_children(): yield from widgets(child)
            combos = [widget for widget in widgets(self.dialog) if isinstance(widget, Gtk.ComboBoxText)]
            entries = [widget for widget in widgets(self.dialog) if isinstance(widget, Gtk.Entry)]
            ok = combos[0].get_active_text() == target['Step'] and entries[0].get_text() == target['Name']
            self.dialog.emit('response', Gtk.ResponseType.CANCEL)
            # Refresh must keep both the expanded branch and selected identity.
            self.refresh_model_tree()
            def refreshed():
                if self.tree_pending: return True
                selected = self.selected_model_node()
                retained = selected and selected['Key'] == target['Key'] and self.model_tree.row_expanded(
                    self.tree_store.get_path(self.tree_iters[target['Key'].rsplit('/', 1)[0]]))
                return self.finish(bool(ok and retained),
                    'PASS: GTK model tree, region preview, selected-step editor and retained tree state' if ok and retained else
                    'Model tree check failed: selected editor=%s retained tree state=%s' % (ok, retained))
            GLib.timeout_add(100, refreshed)
            return False
        GLib.timeout_add(100, check)

    def check_region_maintenance(self):
        if self.opening or self.dialog or self.tree_pending or self.fields_loading:
            GLib.timeout_add(100, lambda: self.stable() is None and False)
            return
        def select(kind, name):
            collection = 'node-sets' if kind == 'node-set' else 'surfaces'
            key = 'model/mesh/' + collection + '/' + quote(name, safe='')
            if key not in self.tree_iters: raise RuntimeError('Region missing from the reference tree: ' + name)
            path = self.tree_store.get_path(self.tree_iters[key]); self.model_tree.expand_to_path(path)
            self.model_tree.get_selection().select_path(path)
        def action(stage, kind, name, operation):
            self.region_smoke_stage = stage
            select(kind, name); self.model_tree_action(operation)
        stage = self.region_smoke_stage
        if stage == 0:
            self.region_smoke_original = Path(args.project).read_bytes()
            action(1, 'node-set', 'LEFT', 'edit')
        elif stage == 1:
            self.region_smoke_stage = 2
            def checked(reply):
                matches = [item for item in reply.get('boundaries', []) if item['Name'] == 'left']
                if not reply.get('ok') or not matches or any(item['Region'] != 'renamed-left' for item in matches):
                    self.finish(False, 'Node-set rename broke the boundary reference'); return
                select('node-set', 'renamed-left'); self.model_tree_action('duplicate')
            self.submit({'command': 'boundary-directions'}, checked)
        elif stage == 2: action(3, 'node-set', 'spare-nodes', 'edit')
        elif stage == 3: action(4, 'node-set', 'edited-spare-nodes', 'remove')
        elif stage == 4: action(5, 'surface', 'LOADED', 'edit')
        elif stage == 5:
            self.region_smoke_stage = 6
            def checked(reply):
                summary = next((item for item in reply.get('data', {}).get('Surfaces', []) if item['Name'] == 'renamed-loaded'), {})
                if not reply.get('ok') or summary.get('Faces') != 2 or summary.get('Nodes') != 8 or not math.isclose(summary.get('Area', 0), 2, rel_tol=1e-12):
                    self.finish(False, 'Surface edit did not rebuild both faces, node membership and area'); return
                def load_checked(loads):
                    if not loads.get('ok') or any(item['Region'] != 'renamed-loaded' for item in loads.get('loads', []) if item['Name'] == 'force'):
                        self.finish(False, 'Surface rename broke the traction reference'); return
                    select('surface', 'renamed-loaded'); self.model_tree_action('duplicate')
                self.submit({'command': 'loads'}, load_checked)
            self.submit({'command': 'surface-editor-data', 'region': 'renamed-loaded'}, checked)
        elif stage == 6: action(7, 'surface', 'spare-surface', 'edit')
        elif stage == 7: action(8, 'surface', 'edited-spare-surface', 'remove')
        elif stage == 8:
            count = len(self.session.history)
            self.region_smoke_stage = 12
            select('node-set', 'renamed-left'); self.model_tree_action('remove')
            def node_blocked():
                if self.tree_pending: return True
                if 'Reassign' not in self.label.get_text() or len(self.session.history) != count:
                    return self.finish(False, 'The node-set tree action removed a referenced set')
                select('surface', 'renamed-loaded'); self.model_tree_action('remove')
                GLib.timeout_add(100, surface_blocked)
                return False
            def surface_blocked():
                if self.tree_pending: return True
                if 'Reassign' not in self.label.get_text() or len(self.session.history) != count:
                    return self.finish(False, 'The surface tree action removed a referenced surface')
                self.region_smoke_stage = 9
                self.move_session(-1)
                return False
            GLib.timeout_add(100, node_blocked)
        elif stage == 9:
            if 'model/mesh/surfaces/edited-spare-surface' not in self.tree_nodes:
                self.finish(False, 'Undo did not restore the removed surface'); return
            self.region_smoke_stage = 10; self.move_session(1)
        elif stage == 10:
            if 'model/mesh/surfaces/edited-spare-surface' in self.tree_nodes:
                self.finish(False, 'Redo did not remove the spare surface'); return
            self.region_smoke_stage = 11
            self.save_session(choose=True, after=lambda: self.open_project(str(Path(args.smoke_region_maintenance).resolve())))
        elif stage == 11:
            self.finish(self.session.source == Path(args.smoke_region_maintenance).resolve() and not self.session.dirty and not self.fields and
                Path(args.project).read_bytes() == self.region_smoke_original and
                'model/mesh/node-sets/renamed-left' in self.tree_nodes and 'model/mesh/surfaces/renamed-loaded' in self.tree_nodes,
                'PASS: reference-tree node/surface edit, membership rebuild, rename references, independent duplicates, removal refusal, undo/redo and save/reopen')

    def check_element_set_workflow(self):
        if self.opening or self.dialog or self.tree_pending or self.fields_loading:
            GLib.timeout_add(100, lambda: self.stable() is None and False)
            return
        def select(name):
            key = 'model/mesh/element-sets/' + quote(name, safe='')
            if key not in self.tree_iters: raise RuntimeError('Authored element set missing from the reference tree')
            path = self.tree_store.get_path(self.tree_iters[key])
            self.model_tree.expand_to_path(path)
            self.model_tree.get_selection().select_path(path)
        stage = self.element_set_smoke_stage
        if stage == 0:
            self.element_set_smoke_original = Path(args.project).read_bytes()
            self.element_set_smoke_stage = 1
            path = self.tree_store.get_path(self.tree_iters['model/mesh/element-sets'])
            self.model_tree.expand_to_path(path)
            self.model_tree.get_selection().select_path(path)
            self.model_tree_action('add')
        elif stage == 1:
            select('authored-elements')
            self.element_set_smoke_stage = 2
            self.tree_preview_button.clicked()
        elif stage == 3:
            select('authored-elements')
            self.element_set_smoke_stage = 4
            self.model_tree_action('edit')
        elif stage == 4:
            if self.selected_model_node()['Name'] != 'renamed-elements':
                self.finish(False, 'Element-set rename lost tree selection'); return
            self.element_set_smoke_stage = 5
            def inspected(reply):
                data = reply.get('data', {})
                selected = data.get('Selected') or {}
                summary = next((item for item in data.get('Sets', []) if item['Name'] == 'renamed-elements'), {})
                if not reply.get('ok') or not selected.get('CreatedFromParts') or summary.get('Elements') != data.get('Elements'):
                    self.finish(False, 'Element-set whole-part membership did not persist'); return
                def section_checked(section_data):
                    assigned = [section for section in section_data.get('sections', []) if section['Name'] == 'authored-section']
                    if not section_data.get('ok') or not assigned or assigned[0]['Region'] != 'renamed-elements':
                        self.finish(False, 'Renaming an element set broke its section assignment'); return
                    self.model_tree_action('duplicate')
                self.submit({'command': 'section-data'}, section_checked)
            self.submit({'command': 'element-set-data', 'set': 'renamed-elements'}, inspected)
        elif stage == 5:
            select('spare-elements')
            self.element_set_smoke_stage = 6
            self.model_tree_action('remove')
        elif stage == 6:
            select('renamed-elements')
            count = len(self.session.history)
            self.element_set_smoke_stage = 7
            self.model_tree_action('remove')
            def blocked():
                if self.tree_pending: return True
                if 'Reassign' not in self.label.get_text() or len(self.session.history) != count:
                    return self.finish(False, 'The tree allowed removing an element set used by a section')
                self.element_set_smoke_stage = 8
                self.move_session(-1)
                return False
            GLib.timeout_add(100, blocked)
        elif stage == 8:
            if 'model/mesh/element-sets/spare-elements' not in self.tree_nodes:
                self.finish(False, 'Undo did not restore the removed element set'); return
            self.element_set_smoke_stage = 9
            self.move_session(1)
        elif stage == 9:
            if 'model/mesh/element-sets/spare-elements' in self.tree_nodes:
                self.finish(False, 'Redo did not remove the spare element set'); return
            self.element_set_smoke_stage = 10
            self.save_session(choose=True, after=lambda: self.open_project(str(Path(args.smoke_element_sets).resolve())))
        elif stage == 10:
            self.finish(self.session.source == Path(args.smoke_element_sets).resolve() and not self.session.dirty and not self.fields and
                Path(args.project).read_bytes() == self.element_set_smoke_original and
                'model/mesh/element-sets/renamed-elements' in self.tree_nodes,
                'PASS: reference tree element-set creation, preview, section assignment, membership edit/rename, duplicate/remove, dependent refusal, undo/redo and save/reopen')

    def check_edit_session(self):
        if self.opening or self.dialog is not None or self.tree_pending or self.fields_loading:
            GLib.timeout_add(100, lambda: self.stable() is None and False)
            return
        if self.session_smoke_stage == 0:
            self.session_original = Path(args.project).read_bytes()
            self.session_target = [node for node in self.tree_nodes.values() if node['Kind'] == 'field-output'][-1]
            path = self.tree_store.get_path(self.tree_iters[self.session_target['Key']])
            self.model_tree.expand_to_path(path)
            self.model_tree.get_selection().select_path(path)
            self.session_smoke_stage = 1
            self.model_tree_action('deactivate')
        elif self.session_smoke_stage in (1, 2, 3):
            node = self.tree_nodes[self.session_target['Key']]
            undo = self.session_smoke_stage == 2
            if (node['Active'] != undo or self.session.dirty == undo or
                    Path(args.project).read_bytes() != self.session_original or bool(self.fields) != undo):
                self.finish(False, 'Session edit/undo/redo changed the wrong definition, source or result state')
                return
            if self.session_smoke_stage == 1:
                self.session_smoke_stage = 2
                self.move_session(-1)
            elif self.session_smoke_stage == 2:
                self.session_smoke_stage = 3
                self.move_session(1)
            else:
                self.session_smoke_stage = 4
                self.model_tree_action('edit')
                def rename():
                    if self.tree_pending: return True
                    if not self.dialog:
                        return self.finish(False, 'Session tree editor did not open for rename')
                    name = self.dialog.get_content_area().get_children()[3]
                    if not isinstance(name, Gtk.Entry):
                        return self.finish(False, 'Session request name entry unavailable')
                    renamed = self.session_target['Name'] + '-renamed'
                    name.set_text(renamed)
                    self.session_target = {**self.session_target, 'Name': renamed,
                        'Key': self.session_target['Key'].rsplit('/', 1)[0] + '/' + quote(renamed, safe='')}
                    self.dialog.emit('response', Gtk.ResponseType.ACCEPT)
                    return False
                GLib.timeout_add(100, rename)
        elif self.session_smoke_stage == 4:
            selected = self.selected_model_node()
            if not selected or selected['Key'] != self.session_target['Key']:
                self.finish(False, 'Renaming a tree object lost the selected identity')
                return
            self.session_smoke_stage = 5
            self.move_session(-1)
        elif self.session_smoke_stage == 5:
            selected = self.selected_model_node()
            if not selected or selected['Name'] != self.session_target['Name'].removesuffix('-renamed'):
                self.finish(False, 'Undo rename lost the selected tree object')
                return
            self.session_smoke_stage = 6
            self.move_session(1)
        elif self.session_smoke_stage == 6:
            if self.selected_model_node()['Key'] != self.session_target['Key']:
                self.finish(False, 'Redo rename lost the selected tree object')
                return
            self.session_smoke_stage = 7
            self.save_session(choose=True, after=lambda: self.open_project(str(Path(args.smoke_edit_session).resolve())))
        elif self.session_smoke_stage == 7:
            node = self.tree_nodes[self.session_target['Key']]
            self.finish(not node['Active'] and not self.session.dirty and not self.fields and
                        self.session.source == Path(args.smoke_edit_session).resolve() and
                        Path(args.project).read_bytes() == self.session_original,
                        'PASS: GTK tree edit in session, undo/redo, result restoration, rename selection, Save as and reopen; original project unchanged')

    def apply_view_layout(self):
        if self.view.get_uri() != self.address or self.closed:
            return
        script = 'document.documentElement.classList.toggle("gtk-view-tools", %s); document.documentElement.classList.toggle("gtk-diagnostics", %s);' % (
            'true' if self.view_tools_toggle.get_active() else 'false',
            'true' if self.diagnostics_toggle.get_active() else 'false')
        self.view.evaluate_javascript(script, -1, None, None, None, None, None)

    def visibility_script(self):
        visible = self.native_visibility != 'fully-obscured'
        return 'window.__gtkDeliver(' + json.dumps({'type': 'prepomax.host.visibility-v1', 'visible': visible}) + '); '

    def visibility_changed(self, window, event):
        if self.closed:
            return False
        was_occluded = self.native_visibility == 'fully-obscured'
        self.native_visibility = event.state.value_nick
        occluded = self.native_visibility == 'fully-obscured'
        if occluded and not was_occluded and self.view_deadline is not None:
            self.paused_view_remaining = max(0, self.view_deadline - time.monotonic())
            self.view_deadline = None
        elif not occluded and self.paused_view_remaining is not None:
            self.view_deadline = time.monotonic() + self.paused_view_remaining
            self.paused_view_remaining = None
        if args.smoke_occlusion and occluded and self.occlusion_stage == 0 and self.occlusion_cover:
            self.occlusion_stage = 1
            print('GTK occlusion: native surface covered; loading scene', flush=True)
            self.view.load_uri(self.address)
        if self.address and not self.opening and not self.view_failed and self.view.get_uri() == self.address:
            self.view.evaluate_javascript('if (window.__gtkDeliver) { ' + self.visibility_script() + ' }',
                                          -1, None, None, None, None, None)
        return False

    def switch_focus(self, window, event):
        if event.keyval != Gdk.KEY_F6 or event.state & (Gdk.ModifierType.CONTROL_MASK | Gdk.ModifierType.MOD1_MASK):
            return False
        if self.dialog or self.opening or self.scene is None:
            return False
        focused = self.window.get_focus()
        if focused == self.view:
            target = self.native_focus
            if target is None or not target.get_sensitive() or not target.get_visible():
                target = next((widget for widget in (self.open_button, self.cancel_button, self.retry_button)
                               if widget.get_sensitive() and widget.get_visible()), None)
            if target is not None:
                target.grab_focus()
        elif not self.view_failed and self.view.get_uri() == self.address:
            self.native_focus = focused
            self.view.grab_focus()
            # Native traversal enters the page at a useful, visible scene control.
            self.view.evaluate_javascript("document.querySelector('#reset-camera:not(:disabled)')?.focus()",
                                          -1, None, None, None, None, None)
        return True

    def check_focus(self):
        if self.focus_smoke_started:
            return
        self.focus_smoke_started = True
        self.window.present()
        self.open_button.grab_focus()
        def enter():
            event = Gdk.Event.new(Gdk.EventType.KEY_PRESS)
            event.keyval = Gdk.KEY_F6
            event.state = Gdk.ModifierType(0)
            self.window.emit('key-press-event', event)
            self.view.evaluate_javascript('document.activeElement.id', -1, None, None, None, inspected, None)
            return False
        def inspected(view, result, _):
            try:
                active = view.evaluate_javascript_finish(result).to_string()
                if self.window.get_focus() != self.view or active != 'reset-camera':
                    raise RuntimeError('F6 did not reach the page camera control')
                self.choose_project()
                if self.dialog is None:
                    raise RuntimeError('Native chooser did not open from the page')
                event = Gdk.Event.new(Gdk.EventType.KEY_PRESS)
                event.keyval = Gdk.KEY_F6
                event.state = Gdk.ModifierType(0)
                if self.switch_focus(self.window, event):
                    raise RuntimeError('F6 switched focus while chooser was open')
                GLib.timeout_add(250, cancel_dialog)
            except Exception as error:
                self.finish(False, 'GTK focus acceptance failed: ' + str(error))
        def cancel_dialog():
            if self.closed:
                return False
            self.dialog.emit('response', Gtk.ResponseType.CANCEL)
            GLib.timeout_add(100, check_cancelled)
            return False
        def check_cancelled():
            if not self.closed:
                self.view.evaluate_javascript('document.activeElement.id', -1, None, None, None, restored, None)
            return False
        def restored(view, result, _):
            try:
                active = view.evaluate_javascript_finish(result).to_string()
                if self.dialog is not None or self.window.get_focus() != self.view or active != 'reset-camera':
                    raise RuntimeError('Chooser cancellation lost embedded control focus')
                event = Gdk.Event.new(Gdk.EventType.KEY_PRESS)
                event.keyval = Gdk.KEY_F6
                event.state = Gdk.ModifierType.SHIFT_MASK
                self.window.emit('key-press-event', event)
                if self.window.get_focus() != self.open_button:
                    raise RuntimeError('Shift+F6 did not restore the native control')
                self.choose_project()
                self.dialog.emit('response', Gtk.ResponseType.CANCEL)
                if self.window.get_focus() != self.open_button:
                    raise RuntimeError('Chooser cancellation lost native control focus')
                self.finish(True, 'PASS: GTK F6 page focus, native chooser cancellation and native focus restoration')
            except Exception as error:
                self.finish(False, 'GTK focus acceptance failed: ' + str(error))
        GLib.timeout_add(100, enter)

    def submit(self, request, callback):
        future = worker.submit(exchange, request)
        def completed(done):
            def deliver():
                if self.closed:
                    return False
                try:
                    callback(done.result())
                except Exception as error:
                    self.finish(False, 'GTK companion communication failed: ' + str(error))
                return False
            GLib.idle_add(deliver)
        future.add_done_callback(completed)

    def started(self, reply):
        self.address, self.scene = reply['address'], reply['scene']
        self.can_solve = bool(reply.get('canSolve'))
        self.geometry_source = reply.get('geometry')
        self.meshing_setup = reply.get('meshingSetup')
        self.cad_topology = reply.get('cadTopology')
        self.opening = False
        self.open_button.set_sensitive(True)
        self.solve_button.set_sensitive(self.can_solve and self.scene is not None)
        if self.scene is None:
            self.label.set_text('Open a project, import a volume mesh, or mesh CAD geometry to begin.')
            if args.smoke_import_mesh or args.smoke_mesh_geometry or args.smoke_import_pmx:
                self.stable()
            elif args.smoke_switch:
                self.stage = 2
                self.choose_project()
                self.dialog.set_filename(str(Path(args.smoke_switch).resolve()))
                def choose_empty_smoke():
                    self.dialog.emit('response', Gtk.ResponseType.ACCEPT)
                    return False
                GLib.timeout_add(500, choose_empty_smoke)
            elif args.smoke_empty:
                def guarded(reply):
                    self.finish(not reply.get('ok') and self.scene is None and not self.solve_button.get_sensitive()
                                and self.open_button.get_sensitive(), 'PASS: empty startup keeps model actions unavailable')
                def rejected(reply):
                    if reply.get('ok') or self.scene is not None:
                        self.finish(False, 'Invalid first project replaced empty state')
                        return
                    self.submit({'command': 'solve'}, guarded)
                self.open_project(str(Path(args.client_root) / '__missing_empty_start__.pma'), rejected)
            return
        self.label.set_text(Path(args.project).name)
        self.start_session(args.project)
        self.refresh_model_tree()
        if args.smoke_occlusion:
            self.occlusion_cover = Gtk.Window(title='PrePoMax temporary occlusion test', transient_for=self.window)
            self.occlusion_cover.add(Gtk.Label(label='Testing covered-window frame suspension; this closes automatically.'))
            display = self.window.get_display()
            monitor = display.get_monitor_at_window(self.window.get_window())
            monitor_index = next(i for i in range(display.get_n_monitors()) if display.get_monitor(i) == monitor)
            self.occlusion_cover.fullscreen_on_monitor(self.window.get_screen(), monitor_index)
            self.occlusion_cover.show_all()
            self.occlusion_cover.present()
        else:
            self.view.load_uri(self.address)
        self.refresh_fields()

    def inspect_cad_topology(self):
        if self.opening or self.solving or self.dialog or self.cad_topology_pending or not self.cad_topology:
            return
        def received(reply):
            self.cad_topology_pending = False
            if not reply.get('ok'):
                self.label.set_text(reply.get('error', 'Unable to inspect CAD topology'))
                if args.smoke_cad_topology:
                    self.finish(False, 'CAD topology request failed')
                return
            topology = reply['topology']
            entities, groups = topology['Entities'], topology['PhysicalGroups']
            names = {(group['Dimension'], group['Tag']): group.get('Name') or str(group['Tag']) for group in groups}
            dialog = Gtk.Dialog(title='Retained CAD topology', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_button('_Close', Gtk.ResponseType.CLOSE)
            dialog.set_default_size(800, 450)
            box = dialog.get_content_area()
            counts = [sum(entity['Dimension'] == dimension for entity in entities) for dimension in range(4)]
            box.pack_start(Gtk.Label(label='%d points · %d curves · %d faces · %d volumes' % tuple(counts), xalign=0), False, False, 8)
            box.pack_start(Gtk.Label(label='Entity tags belong to this retained CAD source. Geometry edits can change them.',
                                    wrap=True, xalign=0), False, False, 4)
            rows = Gtk.ListStore(str, int, str, str, str)
            for entity in entities[:500]:
                rows.append((('Point', 'Curve', 'Face', 'Volume')[entity['Dimension']], entity['Tag'],
                    ', '.join(names[(entity['Dimension'], tag)] for tag in entity['PhysicalTags']),
                    ', '.join(str(tag) for tag in entity['BoundaryTags']),
                    ', '.join(format(value, '.6g') for value in entity['Bounds'])))
            tree = Gtk.TreeView(model=rows)
            tree.get_accessible().set_name('CAD entity boundary graph')
            for column, title in enumerate(('Entity', 'Tag', 'Physical groups', 'Oriented boundary tags', 'Bounds (min XYZ, max XYZ)')):
                tree.append_column(Gtk.TreeViewColumn(title, Gtk.CellRendererText(), text=column))
            scroll = Gtk.ScrolledWindow()
            scroll.add(tree)
            box.pack_start(scroll, True, True, 4)
            if len(entities) > 500:
                box.pack_start(Gtk.Label(label='Showing the first 500 entities. Use CLI cad-topology for the full inventory.'), False, False, 4)
            def closed(*_):
                dialog.destroy()
                self.dialog = None
            dialog.connect('response', closed)
            dialog.show_all()
            if args.smoke_cad_topology:
                ok = (counts == [8, 12, 6, 1] and len(rows) == 27 and topology['Version'] == 1 and
                      topology['SourceSha256'].lower() == self.geometry_source['Sha256'].lower())
                dialog.emit('response', Gtk.ResponseType.CLOSE)
                self.finish(ok, 'PASS: GTK retained CAD topology dialog: 8 points, 12 curves, 6 faces, 1 volume')
        self.cad_topology_pending = True
        self.submit({'command': 'cad-topology'}, received)

    def export_geometry(self):
        if self.opening or self.solving or self.dialog or not self.geometry_source:
            return
        self.dialog = Gtk.FileChooserNative.new('Export retained CAD geometry', self.window,
            Gtk.FileChooserAction.SAVE, '_Export', '_Cancel')
        self.dialog.set_modal(True)
        self.dialog.set_do_overwrite_confirmation(True)
        self.dialog.set_current_name(self.geometry_source['FileName'])
        def destination(chooser, answer):
            path = chooser.get_filename() if answer == Gtk.ResponseType.ACCEPT else None
            chooser.destroy()
            self.dialog = None
            if not path:
                return
            def exported(reply):
                self.label.set_text(('Exported retained CAD geometry to ' + Path(path).name)
                                    if reply.get('ok') else reply.get('error', 'CAD geometry export failed.'))
            self.submit({'command': 'extract-geometry', 'path': path}, exported)
        self.dialog.connect('response', destination)
        self.dialog.show()

    def mesh_geometry(self):
        if self.opening or self.solving or self.dialog:
            return
        self.dialog = Gtk.FileChooserNative.new('Mesh CAD geometry with Gmsh', self.window,
            Gtk.FileChooserAction.OPEN, '_Select', '_Cancel')
        self.dialog.set_modal(True)
        cad_filter = Gtk.FileFilter()
        cad_filter.set_name('CAD solids (*.step, *.stp, *.iges, *.igs, *.brep)')
        for pattern in ('*.step', '*.STEP', '*.stp', '*.STP', '*.iges', '*.IGES', '*.igs', '*.IGS', '*.brep', '*.BREP'):
            cad_filter.add_pattern(pattern)
        self.dialog.add_filter(cad_filter)
        geo_filter = Gtk.FileFilter()
        geo_filter.set_name('Gmsh geometry (*.geo)')
        geo_filter.add_pattern('*.geo')
        self.dialog.add_filter(geo_filter)
        self.dialog.set_filter(cad_filter)
        def chosen(chooser, response):
            path = chooser.get_filename() if response == Gtk.ResponseType.ACCEPT else None
            chooser.destroy()
            self.dialog = None
            if not path:
                return
            dialog = Gtk.Dialog(title='Gmsh volume mesh', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Save new project…', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            box.pack_start(Gtk.Label(label=Path(path).name, xalign=0), False, False, 4)
            youngs, poisson, size, minimum = Gtk.Entry(), Gtk.Entry(), Gtk.Entry(), Gtk.Entry()
            for label, entry in (("Young’s modulus", youngs), ("Poisson’s ratio", poisson),
                                 ("Maximum element size", size), ("Minimum element size (optional)", minimum)):
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                entry.get_accessible().set_name(label)
                box.pack_start(entry, False, False, 4)
            algorithm = Gtk.ComboBoxText()
            for value in ('Automatic', 'Delaunay', 'Frontal', 'HXT'):
                algorithm.append_text(value)
            algorithm.set_active(0)
            algorithm.get_accessible().set_name('Gmsh volume algorithm')
            box.pack_start(Gtk.Label(label='Volume algorithm', xalign=0), False, False, 4)
            box.pack_start(algorithm, False, False, 4)
            optimize = Gtk.CheckButton(label='Optimize tetrahedral quality')
            box.pack_start(optimize, False, False, 4)
            second_order = Gtk.CheckButton(label='Quadratic tetrahedra (straight-sided)')
            box.pack_start(second_order, False, False, 4)
            size_box = LocalSizingBox(box)
            message = Gtk.Label(label='Gmsh creates a tetrahedral volume mesh. Values use the CAD coordinate units. '
                                'One elastic material is assigned to all generated volume parts.', wrap=True)
            box.pack_start(message, False, False, 8)
            def configured(_, answer):
                if answer != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    return
                try:
                    modulus, ratio, maximum = float(youngs.get_text()), float(poisson.get_text()), float(size.get_text())
                    minimum_size = float(minimum.get_text()) if minimum.get_text().strip() else None
                    if (not math.isfinite(modulus) or modulus <= 0 or not math.isfinite(ratio) or not -1 < ratio < .5 or
                            not math.isfinite(maximum) or maximum <= 0 or minimum_size is not None and
                            (not math.isfinite(minimum_size) or minimum_size <= 0 or minimum_size > maximum)):
                        raise ValueError()
                    local_box = size_box.read(minimum_size, maximum)
                except ValueError:
                    message.set_text('Enter finite E > 0, -1 < Poisson’s ratio < 0.5 and valid min/max sizes. Local refinement needs finite increasing bounds and a positive size within those limits.')
                    return
                algorithm_name, optimize_mesh = algorithm.get_active_text().lower(), optimize.get_active()
                dialog.destroy()
                self.dialog = None
                self.save_meshed_geometry(path, modulus, ratio, maximum, minimum_size,
                                           algorithm_name, optimize_mesh, second_order.get_active(), local_box, args.smoke_mesh_output)
            dialog.connect('response', configured)
            dialog.show_all()
            if args.smoke_mesh_geometry:
                youngs.set_text('1000')
                poisson.set_text('0.25')
                size.set_text('0.35')
                second_order.set_active(args.smoke_second_order)
                if args.smoke_size_box:
                    size_box.set_values(SMOKE_SIZE_BOX)
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.dialog.connect('response', chosen)
        self.dialog.show()
        if args.smoke_mesh_geometry:
            self.dialog.set_filename(str(Path(args.smoke_mesh_geometry).resolve()))
            def choose_smoke():
                self.dialog.emit('response', Gtk.ResponseType.ACCEPT)
                return False
            GLib.timeout_add(500, choose_smoke)

    def save_meshed_geometry(self, source, modulus, ratio, maximum, minimum, algorithm, optimize, second_order, size_box, smoke_path=None):
        self.dialog = Gtk.FileChooserNative.new('Save generated mesh as a new project', self.window,
            Gtk.FileChooserAction.SAVE, '_Mesh', '_Cancel')
        self.dialog.set_modal(True)
        self.dialog.set_current_name(Path(source).stem + '-mesh.pma')
        def destination(chooser, answer):
            path = chooser.get_filename() if answer == Gtk.ResponseType.ACCEPT else None
            chooser.destroy()
            self.dialog = None
            if not path:
                return
            self.solving = True
            self.job_kind = 'mesh'
            self.job_pending = True
            self.field_combo.set_sensitive(False)
            self.solve_button.set_sensitive(False)
            self.open_button.set_sensitive(False)
            self.cancel_button.set_label('Cancel meshing')
            self.cancel_button.get_accessible().set_name('Cancel meshing')
            self.cancel_button.set_sensitive(True)
            self.label.set_text('Preparing Gmsh…')
            self.submit({'command': 'mesh-geometry', 'source': source, 'path': path,
                         'youngsModulus': modulus, 'poissonsRatio': ratio,
                         'maximumElementSize': maximum, 'minimumElementSize': minimum,
                         'algorithm': algorithm, 'optimize': optimize, 'secondOrder': second_order, 'sizeBox': size_box, 'gmsh': args.gmsh}, self.mesh_job_updated)
        self.dialog.connect('response', destination)
        self.dialog.show()
        if smoke_path:
            output = Path(smoke_path).resolve()
            self.dialog.set_current_folder(str(output.parent))
            self.dialog.set_current_name(output.name)
            def save_smoke():
                self.dialog.emit('response', Gtk.ResponseType.ACCEPT)
                return False
            GLib.timeout_add(500, save_smoke)

    def remesh_geometry(self):
        if self.opening or self.solving or self.dialog or self.remesh_pending or not self.geometry_source:
            return
        generation = self.generation
        self.remesh_pending = True
        self.submit({'command': 'cad-sizing-faces'}, lambda reply: self.show_remesh_geometry(reply, generation))

    def show_remesh_geometry(self, reply, generation):
        self.remesh_pending = False
        if generation != self.generation or self.opening or self.solving or self.dialog:
            return
        if not reply.get('ok'):
            self.label.set_text(reply.get('error', 'Unable to read CAD faces'))
            if args.smoke_remesh_output:
                self.finish(False, 'Unable to read CAD sizing faces')
            return
        dialog = Gtk.Dialog(title='Remesh retained CAD', transient_for=self.window, modal=True)
        self.dialog = dialog
        dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Save new project…', Gtk.ResponseType.ACCEPT)
        box = dialog.get_content_area()
        box.pack_start(Gtk.Label(label=self.geometry_source['FileName'], xalign=0), False, False, 4)
        maximum, minimum = Gtk.Entry(), Gtk.Entry()
        for label, entry in (("Maximum element size", maximum), ("Minimum element size (optional)", minimum)):
            box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
            entry.get_accessible().set_name(label)
            box.pack_start(entry, False, False, 4)
        algorithm = Gtk.ComboBoxText()
        for value in ('Automatic', 'Delaunay', 'Frontal', 'HXT'):
            algorithm.append_text(value)
        algorithm.set_active(0)
        algorithm.get_accessible().set_name('Gmsh volume algorithm')
        box.pack_start(Gtk.Label(label='Volume algorithm', xalign=0), False, False, 4)
        box.pack_start(algorithm, False, False, 4)
        optimize = Gtk.CheckButton(label='Optimize tetrahedral quality')
        box.pack_start(optimize, False, False, 4)
        second_order = Gtk.CheckButton(label='Quadratic tetrahedra (straight-sided)')
        box.pack_start(second_order, False, False, 4)
        size_box = LocalSizingBox(box, self.meshing_setup.get('SizeBox') if self.meshing_setup else None)
        face_size = FaceSizing(box, reply.get('faces', []), self.meshing_setup.get('FaceSize') if self.meshing_setup else None)
        if self.meshing_setup:
            maximum.set_text(str(self.meshing_setup['MaximumElementSize']))
            if self.meshing_setup.get('MinimumElementSize') is not None:
                minimum.set_text(str(self.meshing_setup['MinimumElementSize']))
            stored_algorithm = self.meshing_setup.get('Algorithm', 'Delaunay').lower()
            algorithm.set_active({'automatic': 0, 'delaunay': 1, 'frontal': 2, 'hxt': 3}.get(stored_algorithm, 1))
            optimize.set_active(bool(self.meshing_setup.get('Optimize')))
            second_order.set_active(bool(self.meshing_setup.get('SecondOrder')))
        message = Gtk.Label(label='This creates a new volume-mesh project from the retained CAD. '
                            'The assigned constant elastic values are retained; loads, supports, result data and other edits are reset.',
                            wrap=True)
        box.pack_start(message, False, False, 8)
        def configured(_, answer):
            if answer != Gtk.ResponseType.ACCEPT:
                dialog.destroy()
                self.dialog = None
                return
            try:
                maximum_size = float(maximum.get_text())
                minimum_size = float(minimum.get_text()) if minimum.get_text().strip() else None
                if (not math.isfinite(maximum_size) or maximum_size <= 0 or minimum_size is not None and
                        (not math.isfinite(minimum_size) or minimum_size <= 0 or minimum_size > maximum_size)):
                    raise ValueError()
                local_box = size_box.read(minimum_size, maximum_size)
                local_face = face_size.read(minimum_size, maximum_size)
            except ValueError:
                message.set_text('Enter valid global sizes. Local refinement requires finite increasing bounds and a positive size within global min/max limits.')
                return
            algorithm_name, optimize_mesh = algorithm.get_active_text().lower(), optimize.get_active()
            dialog.destroy()
            self.dialog = None
            self.save_remeshed_geometry(maximum_size, minimum_size, algorithm_name, optimize_mesh, second_order.get_active(), local_box, local_face)
        dialog.connect('response', configured)
        dialog.show_all()
        if args.smoke_remesh_output:
            if self.meshing_setup and self.meshing_setup.get('SizeBox'):
                if size_box.read(self.meshing_setup.get('MinimumElementSize'),
                                 self.meshing_setup['MaximumElementSize']) != self.meshing_setup['SizeBox']:
                    self.finish(False, 'Stored local refinement box did not prefill the remesh editor')
                    return
            if self.meshing_setup and self.meshing_setup.get('FaceSize'):
                if face_size.read(self.meshing_setup.get('MinimumElementSize'),
                                  self.meshing_setup['MaximumElementSize']) != self.meshing_setup['FaceSize']:
                    self.finish(False, 'Stored CAD face sizing did not prefill the remesh editor')
                    return
            maximum.set_text('0.45')
            minimum.set_text('0.12')
            algorithm.set_active(1)
            optimize.set_active(True)
            second_order.set_active(args.smoke_second_order)
            if args.smoke_size_box:
                size_box.set_values(SMOKE_SIZE_BOX)
            if args.smoke_face_size:
                face_size.set_values(SMOKE_FACE_SIZE)
            dialog.emit('response', Gtk.ResponseType.ACCEPT)

    def save_remeshed_geometry(self, maximum, minimum, algorithm, optimize, second_order, size_box, face_size):
        self.dialog = Gtk.FileChooserNative.new('Save remeshed CAD as a new project', self.window,
            Gtk.FileChooserAction.SAVE, '_Remesh', '_Cancel')
        self.dialog.set_modal(True)
        self.dialog.set_current_name(Path(self.geometry_source['FileName']).stem + '-remesh.pma')
        def destination(chooser, answer):
            path = chooser.get_filename() if answer == Gtk.ResponseType.ACCEPT else None
            chooser.destroy()
            self.dialog = None
            if not path:
                return
            self.solving = True
            self.job_kind = 'remesh'
            self.job_pending = True
            self.field_combo.set_sensitive(False)
            self.solve_button.set_sensitive(False)
            self.open_button.set_sensitive(False)
            self.cancel_button.set_label('Cancel meshing')
            self.cancel_button.get_accessible().set_name('Cancel meshing')
            self.cancel_button.set_sensitive(True)
            self.label.set_text('Preparing retained CAD remesh…')
            self.submit({'command': 'remesh-geometry', 'path': path,
                         'maximumElementSize': maximum, 'minimumElementSize': minimum,
                         'algorithm': algorithm, 'optimize': optimize, 'secondOrder': second_order,
                         'sizeBox': size_box, 'faceSize': face_size, 'gmsh': args.gmsh}, self.mesh_job_updated)
        self.dialog.connect('response', destination)
        self.dialog.show()
        if args.smoke_remesh_output:
            output = Path(args.smoke_remesh_output).resolve()
            self.dialog.set_current_folder(str(output.parent))
            self.dialog.set_current_name(output.name)
            def save_smoke():
                self.dialog.emit('response', Gtk.ResponseType.ACCEPT)
                return False
            GLib.timeout_add(500, save_smoke)

    def import_mesh(self):
        if self.opening or self.solving or self.dialog:
            return
        self.dialog = Gtk.FileChooserNative.new('Import volume mesh', self.window,
            Gtk.FileChooserAction.OPEN, '_Select', '_Cancel')
        self.dialog.set_modal(True)
        inp_filter = Gtk.FileFilter()
        inp_filter.set_name('CalculiX / Abaqus mesh (*.inp)')
        inp_filter.add_pattern('*.inp')
        inp_filter.add_pattern('*.INP')
        self.dialog.add_filter(inp_filter)
        mesh_filter = Gtk.FileFilter()
        mesh_filter.set_name('ASCII Medit volume mesh (*.mesh)')
        mesh_filter.add_pattern('*.mesh')
        self.dialog.add_filter(mesh_filter)
        msh_filter = Gtk.FileFilter()
        msh_filter.set_name('ASCII Gmsh volume mesh (*.msh)')
        msh_filter.add_pattern('*.msh')
        msh_filter.add_pattern('*.MSH')
        self.dialog.add_filter(msh_filter)
        self.dialog.set_filter(inp_filter)
        def chosen(chooser, response):
            path = chooser.get_filename() if response == Gtk.ResponseType.ACCEPT else None
            chooser.destroy()
            self.dialog = None
            if not path:
                return
            dialog = Gtk.Dialog(title='Material for imported mesh', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Save new project…', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            box.pack_start(Gtk.Label(label=Path(path).name, xalign=0), False, False, 4)
            youngs, poisson = Gtk.Entry(), Gtk.Entry()
            for label, entry in (("Young’s modulus", youngs), ("Poisson’s ratio", poisson)):
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                entry.get_accessible().set_name(label)
                box.pack_start(entry, False, False, 4)
            message = Gtk.Label(label='Use units consistent with the mesh coordinates; no unit conversion is performed. '
                                'One elastic material is assigned to all volume parts. Add supports and loads before solving.', wrap=True)
            box.pack_start(message, False, False, 8)
            def configured(_, answer):
                if answer != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    return
                try:
                    modulus, ratio = float(youngs.get_text()), float(poisson.get_text())
                    if not math.isfinite(modulus) or modulus <= 0 or not math.isfinite(ratio) or not -1 < ratio < .5:
                        raise ValueError()
                except ValueError:
                    message.set_text('Enter finite E > 0 and -1 < Poisson’s ratio < 0.5. Use a decimal point.')
                    return
                dialog.destroy()
                self.save_edit({'command': 'import-mesh', 'source': path, 'youngsModulus': modulus,
                                'poissonsRatio': ratio}, args.smoke_import_output)
            dialog.connect('response', configured)
            dialog.show_all()
            if args.smoke_import_mesh:
                youngs.set_text('NaN')
                poisson.set_text('0.25')
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
                if self.dialog is not dialog:
                    self.finish(False, 'Import material dialog accepted a nonfinite modulus')
                    return
                youngs.set_text('1000')
                poisson.set_text('0.25')
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.dialog.connect('response', chosen)
        self.dialog.show()
        if args.smoke_import_mesh:
            if Path(args.smoke_import_mesh).suffix.lower() == '.mesh':
                self.dialog.set_filter(mesh_filter)
            self.dialog.set_filename(str(Path(args.smoke_import_mesh).resolve()))
            def select_smoke():
                self.dialog.emit('response', Gtk.ResponseType.ACCEPT)
                return False
            GLib.timeout_add(500, select_smoke)

    def preview_nodes(self, nodes, after=None, faces=None):
        if self.opening or self.solving:
            if not nodes and self.opening:
                self.preview_clear_pending = True
            return
        preview_dialog = self.dialog
        if preview_dialog:
            preview_dialog.set_response_sensitive(Gtk.ResponseType.ACCEPT, False)
            preview_dialog.set_response_sensitive(Gtk.ResponseType.CANCEL, False)
        self.opening = True
        self.generation += 1
        def published(reply):
            if preview_dialog and self.dialog is preview_dialog:
                preview_dialog.set_response_sensitive(Gtk.ResponseType.ACCEPT, True)
                preview_dialog.set_response_sensitive(Gtk.ResponseType.CANCEL, True)
            if not reply.get('ok'):
                self.opening = False
                self.label.set_text('Could not preview nodes. Select at most 10000 existing nodes.')
                if args.smoke_node_set:
                    self.finish(False, 'Node preview failed')
                return
            if self.preview_clear_pending:
                self.preview_clear_pending = False
                self.opening = False
                self.preview_nodes([])
                return
            self.scene = reply['scene']
            self.displayed_field = None
            self.displayed_scale = 0
            self.field_combo.set_active(0)
            self.selection_preview_done = after
            suffix = f' across {len(faces)} shaded faces' if faces else ''
            self.label.set_text(f'{len(nodes)} selected nodes{suffix} — undeformed mesh preview')
            self.opening = False
            self.view.reload()
        self.submit({'command': 'preview-nodes', 'nodes': nodes, 'faces': faces or []}, published)

    def preview_surface_faces(self, after=None):
        if self.opening or self.solving:
            return
        faces = list(self.picked_surface_faces)
        self.opening = True
        self.generation += 1
        def published(reply):
            self.opening = False
            if not reply.get('ok'):
                self.label.set_text('Could not shade the selected model faces.')
                if args.smoke_surface_pick or args.smoke_surface_area or args.smoke_surface_angle:
                    self.finish(False, 'Renderer face highlight failed')
                return
            self.scene = reply['scene']
            self.displayed_field = None
            self.displayed_scale = 0
            self.field_combo.set_active(0)
            self.selection_preview_done = after
            self.label.set_text(f'{len(faces)} selected surface faces — shaded undeformed preview')
            self.view.reload()
        self.submit({'command': 'preview-faces', 'faces': faces}, published)

    def accept_face_pick(self, item):
        if self.node_selection is not None:
            self.accept_node_pick(item)
            return
        if self.opening or self.solving or not item.get('validated'):
            return
        try:
            element = int(item['sourceElementId'])
            nodes = [int(value) for value in item['triangleSourceNodeIds']]
            operation = item.get('operation', 'replace')
            grow = item.get('growConnected') is True
            if len(nodes) != 3 or operation not in ('replace', 'add', 'subtract', 'intersect'):
                raise ValueError()
        except (KeyError, TypeError, ValueError):
            return
        generation = self.generation
        def resolved(reply):
            if generation != self.generation or not reply.get('ok'):
                if args.smoke_surface_pick or args.smoke_surface_angle:
                    self.finish(False, 'Renderer pick did not resolve to a model face')
                return
            count = self.apply_picked_surface_faces(reply['faces'] if grow else [reply['face']], operation)
            if self.surface_pick_smoke_pending:
                self.surface_pick_smoke_pending = False
                expected = 6 if args.smoke_surface_angle else 1
                if count != expected:
                    self.finish(False, 'Renderer face-pick selection count mismatch')
                else:
                    self.surface_smoke_expected_faces = count
                    self.preview_surface_faces(self.add_surface)
            elif count is not None:
                self.preview_surface_faces()
        command = {'command': 'resolve-picked-face-angle' if grow else 'resolve-picked-face',
                   'element': element, 'nodes': nodes}
        if grow:
            command['angle'] = self.surface_angle.get_value()
        self.submit(command, resolved)

    def accept_area_pick(self, item):
        if self.node_selection is not None:
            self.accept_node_area(item)
            return
        if self.opening or self.solving or not item.get('validated'):
            return
        try:
            operation = item.get('operation', 'replace')
            source = item['triangles']
            if operation not in ('replace', 'add', 'subtract', 'intersect') or not source or len(source) > 1000:
                raise ValueError()
            triangles = [{'ElementId': int(value['sourceElementId']),
                          'Nodes': [int(node) for node in value['triangleSourceNodeIds']]} for value in source]
            if any(len(value['Nodes']) != 3 for value in triangles):
                raise ValueError()
        except (KeyError, TypeError, ValueError):
            if args.smoke_surface_area:
                self.finish(False, 'Renderer area selection was empty or malformed')
            return
        generation = self.generation
        def resolved(reply):
            if generation != self.generation or not reply.get('ok'):
                if args.smoke_surface_area:
                    self.finish(False, 'Renderer area did not resolve to bounded model faces')
                return
            count = self.apply_picked_surface_faces(reply['faces'], operation)
            if self.surface_pick_smoke_pending:
                self.surface_pick_smoke_pending = False
                if not count or (args.smoke_surface_area and count < 2):
                    self.finish(False, 'Renderer area did not resolve multiple model faces')
                else:
                    self.surface_smoke_expected_faces = count
                    self.preview_surface_faces(self.add_surface)
            elif count is not None:
                self.preview_surface_faces()
        self.submit({'command': 'resolve-picked-faces', 'triangles': triangles}, resolved)

    def apply_picked_surface_faces(self, faces, operation):
        incoming = {(value['ElementId'], value['Face']): value for value in faces}
        selected = {(value['ElementId'], value['Face']): value for value in self.picked_surface_faces}
        if operation == 'replace':
            selected = incoming
        elif operation == 'add':
            selected.update(incoming)
        elif operation == 'subtract':
            for key in incoming:
                selected.pop(key, None)
        else:
            selected = {key: selected[key] for key in selected.keys() & incoming.keys()}
        if len(selected) > 100:
            self.label.set_text('Surface pick limit is 100 faces.')
            return None
        self.picked_surface_faces = [selected[value] for value in sorted(selected)]
        self.label.set_text(f'{len(selected)} surface faces selected from the renderer')
        return len(selected)

    def surface_pick_dispatched(self, view, result, _data):
        try:
            if not view.evaluate_javascript_finish(result).to_boolean():
                self.finish(False, 'Renderer viewport was unavailable for face-pick smoke')
        except Exception:
            self.finish(False, 'Could not dispatch renderer face-pick smoke')

    def add_node_set(self):
        if self.opening or self.solving or self.dialog:
            return
        self.opening = True
        def listed(reply):
            self.opening = False
            if not reply.get('ok') or not reply.get('nodes'):
                self.label.set_text('No mesh nodes are available.')
                if args.smoke_node_set:
                    self.finish(False, 'Node inventory unavailable')
                return
            nodes = set(reply['nodes'])
            names = {item['Name'] for item in reply.get('sets', [])}
            dialog = Gtk.Dialog(title='Add node set', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            name, ids = Gtk.Entry(), Gtk.Entry()
            for label, entry in (('New node-set name', name), ('Node IDs (comma or space separated)', ids)):
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                entry.get_accessible().set_name(label)
                box.pack_start(entry, False, False, 4)
            box.pack_start(Gtk.Label(label='Available mesh node IDs', xalign=0), False, False, 4)
            available = Gtk.TextView(editable=False, cursor_visible=False)
            available.set_wrap_mode(Gtk.WrapMode.WORD_CHAR)
            available.get_accessible().set_name('Available mesh node IDs')
            available.get_buffer().set_text(', '.join(map(str, sorted(nodes))))
            scroll = Gtk.ScrolledWindow()
            scroll.set_size_request(440, 120)
            scroll.add(available)
            box.pack_start(scroll, True, True, 4)
            message = Gtk.Label(label='Apply to the current model. Old results are removed. Duplicate IDs are combined.', wrap=True)
            box.pack_start(message, False, False, 4)
            box.pack_start(Gtk.Label(label='Select by coordinate box (model length units; inclusive limits)', xalign=0), False, False, 4)
            limits = {}
            grid = Gtk.Grid(column_spacing=6, row_spacing=4)
            grid.attach(Gtk.Label(label='Minimum'), 1, 0, 1, 1)
            grid.attach(Gtk.Label(label='Maximum'), 2, 0, 1, 1)
            for row, axis in enumerate(('X', 'Y', 'Z'), 1):
                grid.attach(Gtk.Label(label=axis), 0, row, 1, 1)
                for column, side in enumerate(('Min', 'Max'), 1):
                    key = axis + side
                    entry = Gtk.Entry()
                    entry.set_width_chars(12)
                    entry.set_text(format(reply['bounds'][key], '.17g'))
                    entry.get_accessible().set_name(axis + ' ' + side.lower())
                    limits[key] = entry
                    grid.attach(entry, column, row, 1, 1)
            box.pack_start(grid, False, False, 4)
            select_box = Gtk.Button(label='Use nodes inside box')
            box.pack_start(select_box, False, False, 4)
            def select_coordinates(*_):
                try:
                    values = {key: float(entry.get_text()) for key, entry in limits.items()}
                    if not all(math.isfinite(v) for v in values.values()) or any(values[a + 'Min'] > values[a + 'Max'] for a in ('X', 'Y', 'Z')):
                        raise ValueError()
                except ValueError:
                    message.set_text('Enter finite limits with minimum <= maximum. Use a decimal point.')
                    return
                select_box.set_sensitive(False)
                dialog.set_response_sensitive(Gtk.ResponseType.ACCEPT, False)
                def selected(result):
                    if self.dialog is not dialog:
                        return
                    select_box.set_sensitive(True)
                    dialog.set_response_sensitive(Gtk.ResponseType.ACCEPT, True)
                    if not result.get('ok'):
                        message.set_text('Unable to select nodes using these limits.')
                        if args.smoke_node_set_box:
                            self.finish(False, 'Coordinate selection failed')
                        return
                    chosen = result['nodes']
                    ids.set_text(', '.join(map(str, chosen)))
                    message.set_text(f'{len(chosen)} nodes selected. Review the IDs before saving.')
                    if args.smoke_node_set and args.smoke_node_set_box:
                        self.node_set_smoke_nodes = chosen
                        self.preview_nodes(chosen, lambda: dialog.emit('response', Gtk.ResponseType.ACCEPT))
                self.submit({'command': 'select-node-box', 'box': values}, selected)
            select_box.connect('clicked', select_coordinates)
            preview = Gtk.Button(label='Preview selected nodes')
            box.pack_start(preview, False, False, 4)
            preview_state = {'shown': False}
            def preview_selection(*_):
                try:
                    selected = sorted({int(token) for token in ids.get_text().replace(',', ' ').split()})
                    if not set(selected).issubset(nodes) or len(selected) > 10000:
                        raise ValueError()
                except ValueError:
                    message.set_text('Preview accepts at most 10000 existing mesh node IDs.')
                    return
                preview_state['shown'] = True
                self.preview_nodes(selected)
                message.set_text('Pink markers show selected nodes on the undeformed mesh. Interior nodes may be hidden.')
            preview.connect('clicked', preview_selection)
            def responded(_, response):
                if response != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    if preview_state['shown']:
                        self.preview_nodes([])
                    return
                try:
                    selected = sorted({int(token) for token in ids.get_text().replace(',', ' ').split()})
                    if not selected or not set(selected).issubset(nodes):
                        raise ValueError()
                    if not name.get_text().strip() or name.get_text().strip() in names:
                        raise ValueError()
                except ValueError:
                    message.set_text('Enter a unique name and at least one valid mesh node ID.')
                    return
                change = {'command': 'add-node-set', 'name': name.get_text().strip(), 'nodes': selected}
                dialog.destroy()
                self.save_edit(change, args.smoke_node_set)
            dialog.connect('response', responded)
            dialog.show_all()
            if args.smoke_node_set:
                name.set_text('created-node-set')
                if args.smoke_node_set_box:
                    limits['XMin'].set_text(limits['XMax'].get_text())
                    select_box.emit('clicked')
                else:
                    self.node_set_smoke_nodes = sorted(nodes)[:2]
                    ids.set_text(', '.join(map(str, self.node_set_smoke_nodes)))
                    dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.submit({'command': 'node-sets'}, listed)

    def add_surface(self):
        if self.opening or self.solving or self.dialog:
            return
        self.opening = True
        def listed(reply):
            self.opening = False
            inventory = reply.get('inventory', {})
            elements = inventory.get('Elements', [])
            if not reply.get('ok') or not elements:
                self.label.set_text('No supported solid elements are available.')
                if args.smoke_surface:
                    self.finish(False, 'Surface element inventory unavailable')
                return
            names = {item['Name'].lower() for item in reply.get('surfaces', [])}
            dialog = Gtk.Dialog(title='Add element surface', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            name, faces = Gtk.Entry(), Gtk.Entry()
            for label, entry in (('New surface name', name), ('Element faces (element:face, for example 1:S4)', faces)):
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                entry.get_accessible().set_name(label)
                box.pack_start(entry, False, False, 4)
            if self.picked_surface_faces:
                faces.set_text(' '.join(f"{item['ElementId']}:{item['Face']}" for item in self.picked_surface_faces))
            shown = ', '.join(f"{item['Id']} ({'/'.join(item['Faces'])})" for item in elements)
            heading = f"Supported solid elements ({inventory.get('Total', len(elements))} total; first {len(elements)} shown)"
            box.pack_start(Gtk.Label(label=heading, xalign=0), False, False, 4)
            available = Gtk.TextView(editable=False, cursor_visible=False)
            available.set_wrap_mode(Gtk.WrapMode.WORD_CHAR)
            available.get_accessible().set_name('Available solid elements and faces')
            available.get_buffer().set_text(shown)
            scroll = Gtk.ScrolledWindow()
            scroll.set_size_request(520, 120)
            scroll.add(available)
            box.pack_start(scroll, True, True, 4)
            message = Gtk.Label(label='Choose 1 to 100 element faces. Duplicate pairs are combined. Apply to the current model; old results are removed.', wrap=True)
            box.pack_start(message, False, False, 4)
            preview_state = {'shown': False}
            def parse_faces():
                parsed = {}
                for token in faces.get_text().replace(',', ' ').split():
                    parts = token.split(':')
                    if len(parts) != 2:
                        raise ValueError()
                    element_id, face = int(parts[0]), parts[1].upper()
                    if element_id <= 0 or face not in ('S1', 'S2', 'S3', 'S4', 'S5', 'S6'):
                        raise ValueError()
                    parsed[(element_id, face)] = {'ElementId': element_id, 'Face': face}
                if not parsed or len(parsed) > 100:
                    raise ValueError()
                return [parsed[key] for key in sorted(parsed)]
            def preview_selection(*_):
                try:
                    selected = parse_faces()
                except ValueError:
                    message.set_text('Enter 1 to 100 pairs as element:face, using S1 through S6.')
                    return
                def resolved(result):
                    if self.dialog is not dialog:
                        return
                    if not result.get('ok'):
                        message.set_text('A selected element or face is invalid for this solid mesh.')
                        if args.smoke_surface:
                            self.finish(False, 'Surface face validation failed')
                        return
                    preview_state['shown'] = True
                    after = (lambda: dialog.emit('response', Gtk.ResponseType.ACCEPT)) if args.smoke_surface else None
                    self.preview_nodes(result['nodes'], after, selected)
                    message.set_text(f"{len(result['nodes'])} face nodes selected. Pink markers and orange shading preview them on the undeformed mesh.")
                self.submit({'command': 'surface-face-nodes', 'faces': selected}, resolved)
            preview = Gtk.Button(label='Preview selected face nodes')
            preview.connect('clicked', preview_selection)
            box.pack_start(preview, False, False, 4)
            def responded(_, response):
                if response != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    if preview_state['shown']:
                        self.preview_surface_faces()
                    return
                try:
                    selected = parse_faces()
                    surface_name = name.get_text().strip()
                    if not surface_name or surface_name.lower() in names:
                        raise ValueError()
                except ValueError:
                    message.set_text('Enter a unique name and 1 to 100 valid element:face pairs.')
                    return
                change = {'command': 'add-surface', 'name': surface_name, 'faces': selected}
                dialog.destroy()
                self.dialog = None
                self.save_edit(change, args.smoke_surface)
            dialog.connect('response', responded)
            dialog.show_all()
            if args.smoke_surface:
                name.set_text('created-surface')
                if args.smoke_surface_pick or args.smoke_surface_area or args.smoke_surface_angle:
                    if not self.picked_surface_faces:
                        self.finish(False, 'Validated renderer selection did not prefill the surface dialog')
                        return
                else:
                    item = elements[0]
                    if not item['Faces']:
                        self.finish(False, 'First solid element has no supported faces')
                        return
                    faces.set_text(f'{item["Id"]}:{item["Faces"][0]}')
                preview.emit('clicked')
        self.submit({'command': 'surface-data'}, listed)

    def edit_pressure(self, target=None):
        if self.opening or self.solving or self.dialog:
            return
        self.opening = True
        def listed(reply):
            self.opening = False
            existing = [{**item, 'Existing': True} for item in reply.get('pressures', [])]
            targets = [{**item, 'Existing': False} for item in reply.get('targets', [])]
            items = existing + targets
            if target:
                items = [item for item in existing if item['Step'] == target['Step'] and item['Name'] == target['Name']]
            if not reply.get('ok') or not items:
                self.label.set_text('No valid element surfaces are available for pressure.')
                if args.smoke_pressure:
                    self.finish(False, 'Pressure inventory unavailable')
                return
            dialog = Gtk.Dialog(title='Surface pressure', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            selector = Gtk.ComboBoxText()
            for item in items:
                action = 'Edit' if item['Existing'] else 'Add to'
                selector.append_text(f"{action} {item['Step']} / {item['Region']}" + (f" — {item['Name']}" if item['Existing'] else ''))
            name, magnitude = Gtk.Entry(), Gtk.Entry()
            for label, widget in (('Pressure or target', selector), ('Pressure name', name), ('Pressure magnitude', magnitude)):
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                widget.get_accessible().set_name(label)
                box.pack_start(widget, False, False, 4)
            remove = Gtk.CheckButton(label='Remove the selected pressure from this step')
            box.pack_start(remove, False, False, 4)
            message = Gtk.Label(label='Positive pressure acts inward. Use the current model pressure units and a decimal point. Apply to the current model; old results are removed.', wrap=True)
            box.pack_start(message, False, False, 8)
            def selected(*_):
                item = items[selector.get_active()]
                name.set_text(item['Name'] if item['Existing'] else '')
                name.set_sensitive(not item['Existing'])
                magnitude.set_text(format(item['Magnitude'], '.17g'))
                remove.set_active(False)
                remove.set_sensitive(item['Existing'])
            selector.connect('changed', selected)
            selector.set_active(0)
            def responded(_, response):
                if response != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    return
                item = items[selector.get_active()]
                pressure_name = name.get_text().strip()
                try:
                    value = float(magnitude.get_text())
                    if not math.isfinite(value) or not pressure_name:
                        raise ValueError()
                except ValueError:
                    message.set_text('Enter a pressure name and a finite magnitude using a decimal point.')
                    return
                if item['Existing'] and remove.get_active():
                    change = {'command': 'remove-pressure', 'step': item['Step'], 'name': item['Name']}
                elif item['Existing']:
                    change = {'command': 'edit-pressure', 'step': item['Step'], 'name': item['Name'], 'magnitude': value}
                else:
                    change = {'command': 'add-pressure', 'step': item['Step'], 'name': pressure_name,
                              'region': item['Region'], 'magnitude': value}
                dialog.destroy()
                self.save_edit(change, args.smoke_pressure)
            dialog.connect('response', responded)
            dialog.show_all()
            if args.smoke_pressure:
                index = next((i for i, item in enumerate(items) if not item['Existing'] and item['Region'] == 'created-surface'), None)
                if index is None:
                    index = next((i for i, item in enumerate(items) if not item['Existing'] and item['Region'] == 'LOADED'), None)
                if index is None:
                    index = next(i for i, item in enumerate(items) if not item['Existing'])
                selector.set_active(index)
                self.pressure_smoke_region = items[index]['Region']
                name.set_text('added-pressure')
                magnitude.set_text('1')
                self.material_smoke_expected = 1
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.submit({'command': 'pressure-data'}, listed)

    def add_fixed_support(self):
        if self.opening or self.solving or self.dialog:
            return
        self.opening = True
        def listed(reply):
            self.opening = False
            targets = reply.get('targets', [])
            supports = reply.get('supports', [])
            if not reply.get('ok') or not targets:
                self.label.set_text('No nonempty node sets are available for a fixed support.')
                if args.smoke_fixed_support:
                    self.finish(False, 'Fixed-support target inventory unavailable')
                return
            dialog = Gtk.Dialog(title='Add fixed support', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            selector = Gtk.ComboBoxText()
            for item in targets:
                selector.append_text(f"{item['Step']} / {item['Region']}")
            selector.set_active(0)
            name = Gtk.Entry()
            for label, widget in (('Step / node set', selector), ('Fixed-support name', name)):
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                widget.get_accessible().set_name(label)
                box.pack_start(widget, False, False, 4)
            message = Gtk.Label(label='All supported degrees of freedom are fixed on every node in the selected set. Apply to the current model; old results are removed.', wrap=True)
            box.pack_start(message, False, False, 8)
            def responded(_, response):
                if response != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    return
                item = targets[selector.get_active()]
                support_name = name.get_text().strip()
                duplicate = any(value['Step'] == item['Step'] and value['Name'].lower() == support_name.lower()
                                for value in supports)
                if not support_name or duplicate:
                    message.set_text('Enter a unique boundary name within the selected step.')
                    return
                change = {'command': 'add-fixed-support', 'step': item['Step'], 'name': support_name,
                          'region': item['Region']}
                if args.smoke_fixed_support:
                    self.fixed_support_smoke_identity = (item['Step'], support_name, item['Region'])
                dialog.destroy()
                self.dialog = None
                self.save_edit(change, args.smoke_fixed_support)
            dialog.connect('response', responded)
            dialog.show_all()
            if args.smoke_fixed_support:
                index = next((i for i, item in enumerate(targets) if item['Region'] == 'RIGHT'), 0)
                selector.set_active(index)
                name.set_text('added-fixed-support')
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.submit({'command': 'fixed-support-data'}, listed)

    def add_gravity(self, target=None):
        if self.opening or self.solving or self.dialog:
            return
        self.opening = True
        def listed(reply):
            self.opening = False
            existing = [{**item, 'Existing': True} for item in reply.get('loads', [])]
            targets = [{**item, 'Existing': False} for item in reply.get('targets', [])]
            items = existing + targets
            if target:
                items = [item for item in existing if item['Step'] == target['Step'] and item['Name'] == target['Name']]
            if not reply.get('ok') or not items:
                self.label.set_text('No gravity definition or density-backed solid region is available. Add material density first.')
                if args.smoke_gravity:
                    self.finish(False, 'GTK gravity inventory unavailable')
                return
            dialog = Gtk.Dialog(title='Gravity load', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            selector = Gtk.ComboBoxText()
            for item in items:
                action = 'Edit' if item['Existing'] else 'Add to'
                selector.append_text(f"{action} {item['Step']} / {item['Region']} — {item['Kind']}" +
                                     (f" — {item['Name']}" if item['Existing'] else ''))
            name = Gtk.Entry()
            entries = [Gtk.Entry() for _ in range(3)]
            for label, widget in [('Gravity or target', selector), ('Gravity name', name),
                                  ('Acceleration X', entries[0]), ('Acceleration Y', entries[1]), ('Acceleration Z', entries[2])]:
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                widget.get_accessible().set_name(label)
                box.pack_start(widget, False, False, 4)
            remove = Gtk.CheckButton(label='Remove the selected gravity load from this step')
            box.pack_start(remove, False, False, 4)
            message = Gtk.Label(label='The section material must have density. Enter acceleration components in current model units; '
                                      'at least one component must be nonzero. Old results are removed.', wrap=True)
            box.pack_start(message, False, False, 8)
            def selected(*_):
                item = items[selector.get_active()]
                name.set_text(item['Name'] if item['Existing'] else '')
                name.set_sensitive(not item['Existing'])
                for key, entry in zip(('A1', 'A2', 'A3'), entries):
                    entry.set_text(format(item[key], '.17g'))
                remove.set_active(False)
                remove.set_sensitive(item['Existing'])
            selector.connect('changed', selected)
            selector.set_active(0)
            def responded(_, response):
                if response != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    return
                item = items[selector.get_active()]
                if item['Existing'] and remove.get_active():
                    change = {'command': 'remove-gravity', 'step': item['Step'], 'name': item['Name']}
                    if args.smoke_gravity:
                        self.gravity_smoke_identity = (item['Step'], item['Name'], item['Region'])
                    dialog.destroy()
                    self.dialog = None
                    self.save_edit(change, args.smoke_gravity)
                    return
                try:
                    acceleration = [float(entry.get_text()) for entry in entries]
                    if not all(math.isfinite(value) for value in acceleration) or not any(value != 0 for value in acceleration):
                        raise ValueError()
                except ValueError:
                    message.set_text('Enter three finite acceleration components with at least one nonzero value. Use a decimal point.')
                    return
                load_name = name.get_text().strip()
                if not load_name:
                    message.set_text('Enter a unique gravity-load name.')
                    return
                if args.smoke_gravity:
                    self.gravity_smoke_identity = (item['Step'], load_name, item['Region'])
                    self.gravity_smoke_acceleration = acceleration
                change = ({'command': 'edit-gravity', 'step': item['Step'], 'name': item['Name'],
                           'a1': acceleration[0], 'a2': acceleration[1], 'a3': acceleration[2]}
                          if item['Existing'] else
                          {'command': 'add-gravity', 'step': item['Step'], 'name': load_name,
                           'region': item['Region'], 'a1': acceleration[0], 'a2': acceleration[1],
                           'a3': acceleration[2]})
                dialog.destroy()
                self.dialog = None
                self.save_edit(change, args.smoke_gravity)
            dialog.connect('response', responded)
            dialog.show_all()
            if args.smoke_gravity:
                want_existing = args.smoke_gravity_action in ('edit', 'remove')
                index = next((i for i, item in enumerate(items) if item['Existing'] == want_existing and
                              item['Step'] == 'load-1' and item['Region'] == 'SOLID'), None)
                if index is None:
                    self.finish(False, 'Required gravity smoke item is unavailable')
                    return
                selector.set_active(index)
                if not want_existing:
                    name.set_text('added-gravity')
                entries[0].set_text('2' if args.smoke_gravity_action == 'edit' else '1')
                entries[1].set_text('0')
                entries[2].set_text('0')
                if args.smoke_gravity_action == 'remove':
                    remove.set_active(True)
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.submit({'command': 'gravity-data'}, listed)

    def edit_centrifugal(self, target=None):
        if self.opening or self.solving or self.dialog:
            return
        self.opening = True
        def listed(reply):
            self.opening = False
            existing = [{**item, 'Existing': True} for item in reply.get('loads', [])]
            targets = [{**item, 'Existing': False} for item in reply.get('targets', [])]
            items = existing + targets
            if target:
                items = [item for item in existing if item['Step'] == target['Step'] and item['Name'] == target['Name']]
            if not reply.get('ok') or not items:
                self.label.set_text('No centrifugal definition or density-backed 3D solid region is available. Add material density first.')
                if args.smoke_centrifugal:
                    self.finish(False, 'GTK centrifugal inventory unavailable')
                return
            dialog = Gtk.Dialog(title='Centrifugal load', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            selector = Gtk.ComboBoxText()
            for item in items:
                action = 'Edit' if item['Existing'] else 'Add to'
                selector.append_text(f"{action} {item['Step']} / {item['Region']} — {item['Kind']}" +
                                     (f" — {item['Name']}" if item['Existing'] else ''))
            name = Gtk.Entry()
            keys = ('X', 'Y', 'Z', 'N1', 'N2', 'N3', 'RotationalSpeed')
            labels = ('Axis point X', 'Axis point Y', 'Axis point Z',
                      'Axis direction X', 'Axis direction Y', 'Axis direction Z', 'Rotational speed')
            entries = [Gtk.Entry() for _ in keys]
            for label, widget in [('Centrifugal load or target', selector), ('Load name', name), *zip(labels, entries)]:
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 3)
                widget.get_accessible().set_name(label)
                box.pack_start(widget, False, False, 3)
            remove = Gtk.CheckButton(label='Remove the selected centrifugal load from this step')
            box.pack_start(remove, False, False, 3)
            message = Gtk.Label(label='The section material must have density. Enter a finite axis point, a nonzero axis direction and a positive rotational speed. Old results are removed.', wrap=True)
            box.pack_start(message, False, False, 6)
            def selected(*_):
                item = items[selector.get_active()]
                name.set_text(item['Name'] if item['Existing'] else '')
                name.set_sensitive(not item['Existing'])
                for key, entry in zip(keys, entries):
                    entry.set_text(format(item[key], '.17g'))
                remove.set_active(False)
                remove.set_sensitive(item['Existing'])
            selector.connect('changed', selected)
            selector.set_active(0)
            def responded(_, response):
                if response != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    return
                item = items[selector.get_active()]
                if item['Existing'] and remove.get_active():
                    change = {'command': 'remove-centrifugal', 'step': item['Step'], 'name': item['Name']}
                    if args.smoke_centrifugal:
                        self.centrifugal_smoke_identity = (item['Step'], item['Name'], item['Region'])
                    dialog.destroy()
                    self.dialog = None
                    self.save_edit(change, args.smoke_centrifugal)
                    return
                try:
                    values = [float(entry.get_text()) for entry in entries]
                    if (not all(math.isfinite(value) for value in values) or
                            not any(value != 0 for value in values[3:6]) or values[6] <= 0):
                        raise ValueError()
                except ValueError:
                    message.set_text('Enter finite values, a nonzero axis direction and a positive rotational speed. Use a decimal point.')
                    return
                load_name = name.get_text().strip()
                if not load_name:
                    message.set_text('Enter a unique centrifugal-load name.')
                    return
                properties = dict(zip(('x', 'y', 'z', 'n1', 'n2', 'n3', 'rotationalSpeed'), values))
                if args.smoke_centrifugal:
                    self.centrifugal_smoke_identity = (item['Step'], load_name, item['Region'])
                    self.centrifugal_smoke_values = values
                change = ({'command': 'edit-centrifugal', 'step': item['Step'], 'name': item['Name'], **properties}
                          if item['Existing'] else
                          {'command': 'add-centrifugal', 'step': item['Step'], 'name': load_name,
                           'region': item['Region'], **properties})
                dialog.destroy()
                self.dialog = None
                self.save_edit(change, args.smoke_centrifugal)
            dialog.connect('response', responded)
            dialog.show_all()
            if args.smoke_centrifugal:
                want_existing = args.smoke_centrifugal_action in ('edit', 'remove')
                index = next((i for i, item in enumerate(items) if item['Existing'] == want_existing and
                              item['Step'] == 'load-1' and item['Region'] == 'SOLID'), None)
                if index is None:
                    self.finish(False, 'Required centrifugal smoke item is unavailable')
                    return
                selector.set_active(index)
                if not want_existing:
                    name.set_text('added-centrifugal')
                values = ('0', '0', '0', '0', '0', '1',
                          '3' if args.smoke_centrifugal_action == 'edit' else '2')
                for entry, value in zip(entries, values):
                    entry.set_text(value)
                if args.smoke_centrifugal_action == 'remove':
                    remove.set_active(True)
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.submit({'command': 'centrifugal-data'}, listed)

    def edit_history_results(self):
        if self.opening or self.solving or self.dialog or self.history_result_pending or self.scene is None:
            return
        generation = self.generation
        self.history_result_pending = True
        def listed(reply):
            self.history_result_pending = False
            if generation != self.generation or self.opening or self.solving or self.dialog:
                return
            if not reply.get('ok'):
                self.label.set_text(reply.get('error', 'Unable to read history results'))
                if args.smoke_history_results: self.finish(False, self.label.get_text())
                return
            series = reply.get('series', [])
            dialog = Gtk.Dialog(title='History results', transient_for=self.window, modal=True)
            self.dialog = dialog; dialog.add_button('_Close', Gtk.ResponseType.CLOSE); dialog.set_default_size(1050, 460)
            box = dialog.get_content_area()
            diagnostics = reply.get('diagnostics', [])
            message = ('Parsed result histories. Select a component to view its aligned table and plot, or export one result set.'
                       if series else 'This project contains no history results.')
            if diagnostics: message += ' Parser: ' + ' · '.join(diagnostics[:3])
            box.pack_start(Gtk.Label(label=message, xalign=0, wrap=True), False, False, 8)
            rows = Gtk.ListStore(str, str, str, str, int, str)
            for item in series:
                minimum, maximum = item.get('MinimumTime'), item.get('MaximumTime')
                interval = '' if minimum is None else ('%.8g … %.8g' % (minimum, maximum))
                rows.append((item['Set'], item['Field'], item['Component'], ', '.join(item['Entries']),
                             item['Samples'], interval))
            tree = Gtk.TreeView(model=rows); tree.get_accessible().set_name('History result component list')
            for column, title in enumerate(('Result set', 'Field', 'Component', 'Entries', 'Samples', 'Time range')):
                tree.append_column(Gtk.TreeViewColumn(title, Gtk.CellRendererText(), text=column))
            scroll = Gtk.ScrolledWindow(); scroll.add(tree); box.pack_start(scroll, True, True, 4)
            actions = Gtk.Box(spacing=6); box.pack_start(actions, False, False, 6)
            def close(*_): dialog.destroy(); self.dialog = None
            dialog.connect('response', close)
            def selected_series():
                model, iterator = tree.get_selection().get_selected()
                index = model.get_path(iterator).get_indices()[0] if iterator is not None else None
                return series[index] if index is not None else None
            def view(*_):
                selected = selected_series()
                if selected is None: return
                close(); self.history_result_table(selected)
            def export(_, all_sets):
                selected = selected_series()
                if not all_sets and selected is None: return
                close(); self.export_history_results(None if all_sets else [selected['Set']])
            view_button = Gtk.Button(label='View table and plot…'); view_button.set_sensitive(bool(series))
            view_button.get_accessible().set_name('View selected history result table and plot')
            view_button.connect('clicked', view); actions.pack_start(view_button, False, False, 0)
            export_set = Gtk.Button(label='Export selected set…'); export_set.set_sensitive(bool(series))
            export_set.connect('clicked', export, False); actions.pack_start(export_set, False, False, 0)
            export_all = Gtk.Button(label='Export all…'); export_all.set_sensitive(bool(series))
            export_all.connect('clicked', export, True); actions.pack_start(export_all, False, False, 0)
            if series:
                preferred = next((index for index, item in enumerate(series) if item['Component'].upper() == 'U1'), 0)
                tree.get_selection().select_path(preferred)
            dialog.show_all()
            if args.smoke_history_results:
                if not series: self.finish(False, 'GTK history-result smoke found no result series')
                else: view_button.clicked()
        self.submit({'command': 'history-result-data'}, listed)

    def history_result_table(self, series):
        self.history_result_pending = True
        generation = self.generation
        def received(reply):
            self.history_result_pending = False
            if generation != self.generation or self.opening or self.solving or self.dialog: return
            if not reply.get('ok'):
                self.label.set_text(reply.get('error', 'Unable to read history result series'))
                if args.smoke_history_results: self.finish(False, self.label.get_text())
                return
            table = reply['table']; columns, values = table['Columns'], table['Rows']
            dialog = Gtk.Dialog(title='%s · %s · %s' % (table['Set'], table['Field'], table['Component']),
                                transient_for=self.window, modal=True)
            self.dialog = dialog; dialog.add_button('_Close', Gtk.ResponseType.CLOSE); dialog.set_default_size(1000, 700)
            box = dialog.get_content_area()
            plot = Gtk.DrawingArea(); plot.set_size_request(800, 280)
            plot.get_accessible().set_name('History result line plot')
            plot.connect('draw', lambda widget, context: self.draw_history_plot(widget, context, columns, values))
            box.pack_start(plot, False, False, 4)
            rows = Gtk.ListStore(*([str] * len(columns)))
            def display(value):
                if value is None: return ''
                if isinstance(value, (int, float)): return '%.17g' % value
                return str(value)
            for row in values: rows.append(tuple(display(value) for value in row))
            tree = Gtk.TreeView(model=rows); tree.get_accessible().set_name('Aligned history result values')
            for column, title in enumerate(columns):
                tree.append_column(Gtk.TreeViewColumn(title, Gtk.CellRendererText(), text=column))
            scroll = Gtk.ScrolledWindow(); scroll.add(tree); box.pack_start(scroll, True, True, 4)
            def close(*_): dialog.destroy(); self.dialog = None
            dialog.connect('response', close); dialog.show_all()
            if args.smoke_history_results:
                def verify_and_export():
                    try:
                        numeric = [float(value) for row in values for value in row if value is not None]
                        if len(columns) < 2 or not values or not numeric or not all(math.isfinite(value) for value in numeric):
                            raise ValueError('aligned table is empty or non-finite')
                        close()
                        destination = str(Path(args.smoke_history_results).resolve())
                        def exported(result):
                            try:
                                content = Path(destination).read_text(encoding='utf-8') if result.get('ok') else ''
                                ok = result.get('ok') and 'History output component' in content and len(content.splitlines()) >= 3
                                self.finish(ok, 'PASS: GTK history-result catalog, aligned table, line plot and selected-set CSV export')
                            except Exception as error:
                                self.finish(False, 'GTK history-result export validation failed: ' + str(error))
                        self.submit({'command': 'export-history-results', 'path': destination,
                                     'sets': [series['Set']], 'delimiter': ','}, exported)
                    except Exception as error:
                        self.finish(False, 'GTK history-result table validation failed: ' + str(error))
                    return False
                GLib.timeout_add(100, verify_and_export)
        self.submit({'command': 'history-result-series', 'set': series['Set'], 'field': series['Field'],
                     'component': series['Component']}, received)

    def draw_history_plot(self, widget, context, columns, rows):
        width, height = widget.get_allocated_width(), widget.get_allocated_height()
        context.set_source_rgb(1, 1, 1); context.paint()
        curves = []
        for column in range(1, min(len(columns), 13)):
            points = []
            for row in rows:
                if len(row) <= column or row[0] is None or row[column] is None: continue
                x, y = float(row[0]), float(row[column])
                if math.isfinite(x) and math.isfinite(y): points.append((x, y))
            if points: curves.append((column, points))
        if not curves: return False
        xs = [point[0] for _, points in curves for point in points]
        ys = [point[1] for _, points in curves for point in points]
        xmin, xmax, ymin, ymax = min(xs), max(xs), min(ys), max(ys)
        if xmin == xmax: xmin -= .5; xmax += .5
        if ymin == ymax: ymin -= .5 if ymin == 0 else abs(ymin) * .05; ymax += .5 if ymax == 0 else abs(ymax) * .05
        left, top, right, bottom = 70, 20, max(80, width - 20), max(40, height - 45)
        def screen(point):
            return (left + (point[0] - xmin) / (xmax - xmin) * (right - left),
                    bottom - (point[1] - ymin) / (ymax - ymin) * (bottom - top))
        context.set_source_rgb(.25, .25, .25); context.set_line_width(1)
        context.move_to(left, top); context.line_to(left, bottom); context.line_to(right, bottom); context.stroke()
        context.set_font_size(11)
        for x, y, label in ((left, bottom + 18, '%.6g' % xmin), (right - 45, bottom + 18, '%.6g' % xmax),
                            (3, top + 4, '%.6g' % ymax), (3, bottom, '%.6g' % ymin)):
            context.move_to(x, y); context.show_text(label)
        palette = ((.12, .38, .75), (.8, .22, .18), (.1, .58, .28), (.58, .25, .68), (.9, .5, .05), (.1, .65, .7))
        for order, (_, points) in enumerate(curves):
            context.set_source_rgb(*palette[order % len(palette)]); context.set_line_width(1.5)
            for index, point in enumerate(points):
                x, y = screen(point)
                if index: context.line_to(x, y)
                else: context.move_to(x, y)
            context.stroke()
        return False

    def export_history_results(self, sets):
        if self.dialog or self.history_result_pending: return
        dialog = Gtk.FileChooserDialog(title='Export history results', parent=self.window,
            action=Gtk.FileChooserAction.SAVE)
        self.dialog = dialog; dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Export', Gtk.ResponseType.ACCEPT)
        dialog.set_modal(True); dialog.set_current_name('history-results.csv')
        extra = Gtk.Box(spacing=8); extra.pack_start(Gtk.Label(label='Delimiter'), False, False, 0)
        delimiter = Gtk.ComboBoxText()
        for name in ('Comma', 'Semicolon', 'Tab'): delimiter.append_text(name)
        delimiter.set_active(0); extra.pack_start(delimiter, False, False, 0); extra.show_all(); dialog.set_extra_widget(extra)
        def destination(chooser, answer):
            path = chooser.get_filename() if answer == Gtk.ResponseType.ACCEPT else None
            selected_delimiter = delimiter.get_active()
            chooser.destroy(); self.dialog = None
            if not path: return
            separators = (',', ';', '\t')
            self.history_result_pending = True
            def exported(reply):
                self.history_result_pending = False
                self.label.set_text(('Exported history results to ' + Path(path).name)
                                    if reply.get('ok') else reply.get('error', 'History result export failed.'))
            request = {'command': 'export-history-results', 'path': path,
                       'delimiter': separators[selected_delimiter]}
            if sets is not None: request['sets'] = sets
            self.submit(request, exported)
        dialog.connect('response', destination); dialog.show_all()

    def edit_history_outputs(self):
        if self.opening or self.solving or self.dialog or self.history_output_pending or self.scene is None:
            return
        generation = self.generation
        self.history_output_pending = True
        def listed(reply):
            self.history_output_pending = False
            if generation != self.generation or self.opening or self.solving or self.dialog:
                return
            if not reply.get('ok'):
                self.label.set_text(reply.get('error', 'Unable to read history outputs')); return
            outputs, error = reply.get('outputs', []), reply.get('editingError')
            dialog = Gtk.Dialog(title='History output requests', transient_for=self.window, modal=True)
            self.dialog = dialog; dialog.add_button('_Close', Gtk.ResponseType.CLOSE); dialog.set_default_size(1050, 420)
            box = dialog.get_content_area()
            box.pack_start(Gtk.Label(label=error or 'Requests write region histories to the DAT result. Propagate copies the selected request to every later step.',
                                     xalign=0, wrap=True), False, False, 8)
            rows = Gtk.ListStore(str, str, str, str, str, str, str, str)
            for output in outputs:
                rows.append((output['Step'], output['Name'], 'Active' if output['Active'] else 'Inactive', output['Kind'],
                             ', '.join(output['Variables']), output['RegionType'] + ': ' + output['Region'],
                             output['Totals'], 'Yes' if output['Global'] else 'No'))
            tree = Gtk.TreeView(model=rows); tree.get_accessible().set_name('History output request sequence')
            for column, title in enumerate(('Step', 'Request', 'State', 'Type', 'Variables', 'Region', 'Totals', 'Global')):
                tree.append_column(Gtk.TreeViewColumn(title, Gtk.CellRendererText(), text=column))
            scroll = Gtk.ScrolledWindow(); scroll.add(tree); box.pack_start(scroll, True, True, 4)
            actions = Gtk.Box(spacing=6); box.pack_start(actions, False, False, 6); buttons = {}
            def close(*_): dialog.destroy(); self.dialog = None
            dialog.connect('response', close)
            def operate(_, action):
                model, iterator = tree.get_selection().get_selected()
                index = model.get_path(iterator).get_indices()[0] if iterator is not None else None
                selected = outputs[index] if index is not None else None
                if error or action != 'add' and selected is None: return
                if args.smoke_history_output:
                    expected = [dict(item) for item in outputs]
                    if action == 'remove': del expected[index]
                    elif action in ('activate', 'deactivate'): expected[index]['Active'] = action == 'activate'
                    elif action == 'propagate':
                        source_step = reply['steps'].index(selected['Step'])
                        for target_step in reply['steps'][source_step + 1:]:
                            copy = {**selected, 'Step': target_step}
                            existing = next((i for i, item in enumerate(expected)
                                             if item['Step'] == target_step and item['Name'] == selected['Name']), None)
                            if existing is None: expected.append(copy)
                            else: expected[existing] = copy
                    order = {step: i for i, step in enumerate(reply['steps'])}
                    expected.sort(key=lambda item: order[item['Step']]); self.history_output_smoke_expected = expected
                close()
                if action in ('add', 'edit', 'duplicate'): self.history_output_form(action, selected, reply)
                else:
                    command = ('remove-history-output' if action == 'remove' else
                               'propagate-history-output' if action == 'propagate' else 'history-output-active')
                    change = {'command': command, 'step': selected['Step'], 'output': selected['Name']}
                    if action in ('activate', 'deactivate'): change['active'] = action == 'activate'
                    self.save_edit(change, args.smoke_history_output)
            for action, title in (('add', 'Add…'), ('edit', 'Edit…'), ('duplicate', 'Duplicate…'),
                                  ('propagate', 'Propagate'), ('activate', 'Activate'),
                                  ('deactivate', 'Deactivate'), ('remove', 'Remove')):
                button = Gtk.Button(label=title); button.get_accessible().set_name(action.capitalize() + ' history output')
                button.set_sensitive(not error and (bool(reply['steps']) if action == 'add' else bool(outputs)))
                button.connect('clicked', operate, action); actions.pack_start(button, False, False, 0); buttons[action] = button
            if outputs: tree.get_selection().select_path(0)
            dialog.show_all()
            if args.smoke_history_output:
                if error: self.finish(False, error)
                else: buttons[args.smoke_history_output_action].clicked()
        self.submit({'command': 'history-output-data'}, listed)

    def history_output_form(self, action, selected, data, initial_step=None):
        dialog = Gtk.Dialog(title=action.capitalize() + ' history output', transient_for=self.window, modal=True)
        self.dialog = dialog; dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
        box = dialog.get_content_area(); step, name, kind = Gtk.ComboBoxText(), Gtk.Entry(), Gtk.ComboBoxText()
        kinds = ('Nodal', 'Element', 'Contact', 'Section')
        for item in data['steps']: step.append_text(item)
        for item in kinds: kind.append_text(item)
        step.set_active(data['steps'].index(selected['Step'] if selected else initial_step)
                        if selected or initial_step in data['steps'] else 0)
        kind.set_active(kinds.index(selected['Kind']) if selected else 0)
        name.set_text(selected['Name'] if action == 'edit' else selected['Name'][:120] + '-copy' if action == 'duplicate' else 'history-output')
        step.set_sensitive(selected is None); kind.set_sensitive(selected is None)
        for label, widget in (('Analysis step', step), ('Request name', name), ('Output type', kind)):
            widget.get_accessible().set_name(label); box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 3); box.pack_start(widget, False, False, 3)
        variable_box = Gtk.FlowBox(); variable_box.set_selection_mode(Gtk.SelectionMode.NONE); checkboxes = {}
        region = Gtk.ComboBoxText(); region_items = []
        def allowed_region(output_kind, item):
            return ((output_kind == 'Nodal' and item['RegionType'] in ('NodeSetName', 'SurfaceName', 'ReferencePointName')) or
                    (output_kind == 'Element' and item['RegionType'] == 'ElementSetName') or
                    (output_kind == 'Contact' and item['RegionType'] == 'ContactPair') or
                    (output_kind == 'Section' and item['RegionType'] == 'SurfaceName'))
        def rebuild(*_):
            for child in variable_box.get_children(): variable_box.remove(child)
            checkboxes.clear(); region.remove_all(); region_items.clear()
            output_kind = kind.get_active_text() or 'Nodal'
            defaults = {'Nodal': {'U'}, 'Element': {'S'}, 'Contact': {'CDIS'}, 'Section': {'SOF'}}
            selected_variables = set(selected['Variables']) if selected and selected['Kind'] == output_kind else defaults[output_kind]
            for variable in data['variableChoices'][output_kind]:
                button = Gtk.CheckButton(label=variable); button.set_active(variable in selected_variables); checkboxes[variable] = button; variable_box.add(button)
            for item in data['regions']:
                if allowed_region(output_kind, item):
                    region_items.append(item); region.append_text(item['RegionType'] + ': ' + item['Name'] + f" ({item['Entities']})")
            target = next((i for i, item in enumerate(region_items) if selected and item['RegionType'] == selected['RegionType'] and item['Name'] == selected['Region']), 0)
            if region_items: region.set_active(target)
            variable_box.show_all()
        kind.connect('changed', rebuild); rebuild()
        box.pack_start(Gtk.Label(label='Variables', xalign=0), False, False, 3); box.pack_start(variable_box, False, False, 3)
        box.pack_start(Gtk.Label(label='Named region', xalign=0), False, False, 3); box.pack_start(region, False, False, 3)
        totals = Gtk.ComboBoxText()
        for item in ('No', 'Yes', 'Only'): totals.append_text(item)
        totals.set_active(('No', 'Yes', 'Only').index(selected['Totals']) if selected else 0)
        global_output = Gtk.CheckButton(label='Global output'); global_output.set_active(selected['Global'] if selected else True)
        box.pack_start(Gtk.Label(label='Totals', xalign=0), False, False, 3); box.pack_start(totals, False, False, 3); box.pack_start(global_output, False, False, 3)
        def option_sensitivity(*_):
            enabled = kind.get_active_text() != 'Section'; totals.set_sensitive(enabled); global_output.set_sensitive(enabled)
            if not enabled: totals.set_active(0); global_output.set_active(True)
        kind.connect('changed', option_sensitivity); option_sensitivity()
        message = Gtk.Label(label='Select a compatible nonempty named region and at least one original CalculiX history variable. Old results are removed.', xalign=0, wrap=True)
        box.pack_start(message, False, False, 6)
        def configured(_, answer):
            if answer != Gtk.ResponseType.ACCEPT: dialog.destroy(); self.dialog = None; return
            output_name, step_name, output_kind = name.get_text().strip(), step.get_active_text(), kind.get_active_text()
            variables = [value for value, button in checkboxes.items() if button.get_active()]
            item = region_items[region.get_active()] if region.get_active() >= 0 else None
            if not output_name or not variables or item is None:
                message.set_text('Enter a unique name, select a variable and choose a compatible region.'); return
            settings = {'Name': output_name, 'Kind': output_kind, 'Variables': variables,
                        'RegionType': item['RegionType'], 'Region': item['Name'],
                        'Totals': totals.get_active_text(), 'Global': global_output.get_active()}
            if action == 'duplicate':
                change = {'command': 'duplicate-history-output', 'step': selected['Step'], 'output': selected['Name'], 'name': output_name}
                if args.smoke_history_output: self.history_output_smoke_expected.append({**selected, 'Name': output_name})
            else:
                change = {'command': 'add-history-output' if action == 'add' else 'edit-history-output', 'step': step_name, 'settings': settings}
                if action == 'edit': change['output'] = selected['Name']
                if args.smoke_history_output:
                    expected = {**settings, 'Step': step_name, 'Active': selected['Active'] if selected else True}
                    if action == 'add': self.history_output_smoke_expected.append(expected)
                    else: self.history_output_smoke_expected[[item['Step'] + '\0' + item['Name'] for item in data['outputs']]
                                                            .index(selected['Step'] + '\0' + selected['Name'])] = expected
            if args.smoke_history_output:
                order = {value: i for i, value in enumerate(data['steps'])}; self.history_output_smoke_expected.sort(key=lambda item: order[item['Step']])
            dialog.destroy(); self.dialog = None; self.save_edit(change, args.smoke_history_output)
        dialog.connect('response', configured); dialog.show_all()
        if args.smoke_history_output:
            name.set_text('created-history' if action == 'add' else 'renamed-history' if action == 'edit' else 'history-copy')
            if action != 'duplicate':
                totals.set_active(1); global_output.set_active(False)
                if 'RF' in checkboxes: checkboxes['RF'].set_active(True)
            dialog.emit('response', Gtk.ResponseType.ACCEPT)

    def edit_field_outputs(self):
        if self.opening or self.solving or self.dialog or self.field_output_pending or self.scene is None:
            return
        generation = self.generation
        self.field_output_pending = True
        def listed(reply):
            self.field_output_pending = False
            if generation != self.generation or self.opening or self.solving or self.dialog:
                return
            if not reply.get('ok'):
                self.label.set_text(reply.get('error', 'Unable to read field outputs'))
                return
            outputs, error = reply.get('outputs', []), reply.get('editingError')
            dialog = Gtk.Dialog(title='Field output requests', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_button('_Close', Gtk.ResponseType.CLOSE)
            dialog.set_default_size(980, 400)
            box = dialog.get_content_area()
            box.pack_start(Gtk.Label(label=error or 'Requests write result fields for each step. Propagate copies the selected request to every later step.',
                                     xalign=0, wrap=True), False, False, 8)
            rows = Gtk.ListStore(str, str, str, str, str, str, str, str)
            for output in outputs:
                flags = ', '.join(name for enabled, name in ((output['LastIterations'], 'last iterations'),
                    (output['ContactElements'], 'contact elements'), (not output['Global'], 'global=no')) if enabled)
                rows.append((output['Step'], output['Name'], 'Active' if output['Active'] else 'Inactive', output['Kind'],
                             ', '.join(output['Variables']), output['ElementOutput'], flags, ''))
            tree = Gtk.TreeView(model=rows)
            tree.get_accessible().set_name('Field output request sequence')
            for column, title in enumerate(('Step', 'Request', 'State', 'Type', 'Variables', 'Element mode', 'Options')):
                tree.append_column(Gtk.TreeViewColumn(title, Gtk.CellRendererText(), text=column))
            scroll = Gtk.ScrolledWindow(); scroll.add(tree); box.pack_start(scroll, True, True, 4)
            actions = Gtk.Box(spacing=6); box.pack_start(actions, False, False, 6)
            buttons = {}
            def close(*_):
                dialog.destroy(); self.dialog = None
            dialog.connect('response', close)
            def operate(_, action):
                model, iterator = tree.get_selection().get_selected()
                index = model.get_path(iterator).get_indices()[0] if iterator is not None else None
                selected = outputs[index] if index is not None else None
                if error or action != 'add' and selected is None:
                    return
                if args.smoke_field_output:
                    expected = [dict(item) for item in outputs]
                    if action == 'remove':
                        del expected[index]
                    elif action in ('activate', 'deactivate'):
                        expected[index]['Active'] = action == 'activate'
                    elif action == 'propagate':
                        steps = reply['steps']; source_step = steps.index(selected['Step'])
                        for step in steps[source_step + 1:]:
                            copy = {**selected, 'Step': step}
                            existing = next((i for i, item in enumerate(expected)
                                             if item['Step'] == step and item['Name'] == selected['Name']), None)
                            if existing is None:
                                expected.append(copy)
                            else:
                                expected[existing] = copy
                    step_order = {step: index for index, step in enumerate(reply['steps'])}
                    expected.sort(key=lambda item: step_order[item['Step']])
                    self.field_output_smoke_expected = expected
                close()
                if action in ('add', 'edit', 'duplicate'):
                    self.field_output_form(action, selected, reply)
                else:
                    change = {'command': 'remove-field-output' if action == 'remove' else
                                         'propagate-field-output' if action == 'propagate' else 'field-output-active',
                              'step': selected['Step'], 'output': selected['Name']}
                    if action in ('activate', 'deactivate'): change['active'] = action == 'activate'
                    self.save_edit(change, args.smoke_field_output)
            for action, title in (('add', 'Add…'), ('edit', 'Edit…'), ('duplicate', 'Duplicate…'),
                                  ('propagate', 'Propagate'), ('activate', 'Activate'),
                                  ('deactivate', 'Deactivate'), ('remove', 'Remove')):
                button = Gtk.Button(label=title)
                button.get_accessible().set_name(action.capitalize() + ' field output')
                button.set_sensitive(not error and (bool(reply['steps']) if action == 'add' else bool(outputs)))
                button.connect('clicked', operate, action); actions.pack_start(button, False, False, 0); buttons[action] = button
            if outputs: tree.get_selection().select_path(0)
            dialog.show_all()
            if args.smoke_field_output:
                if error: self.finish(False, error)
                else: buttons[args.smoke_field_output_action].clicked()
        self.submit({'command': 'field-output-data'}, listed)

    def field_output_form(self, action, selected, data, initial_step=None):
        dialog = Gtk.Dialog(title=action.capitalize() + ' field output', transient_for=self.window, modal=True)
        self.dialog = dialog
        dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
        box = dialog.get_content_area()
        step, name, kind = Gtk.ComboBoxText(), Gtk.Entry(), Gtk.ComboBoxText()
        for item in data['steps']: step.append_text(item)
        for item in ('Nodal', 'Element', 'Contact'): kind.append_text(item)
        step.set_active(data['steps'].index(selected['Step'] if selected else initial_step)
                        if selected or initial_step in data['steps'] else 0)
        kind.set_active(('Nodal', 'Element', 'Contact').index(selected['Kind']) if selected else 0)
        name.set_text(selected['Name'] if action == 'edit' else
                      selected['Name'][:120] + '-copy' if action == 'duplicate' else 'field-output')
        step.set_sensitive(selected is None); kind.set_sensitive(selected is None)
        for label, widget in (('Analysis step', step), ('Request name', name), ('Output type', kind)):
            widget.get_accessible().set_name(label); box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 3)
            box.pack_start(widget, False, False, 3)
        variable_box = Gtk.FlowBox(); variable_box.set_selection_mode(Gtk.SelectionMode.NONE)
        checkboxes = {}
        def rebuild(*_):
            for child in variable_box.get_children(): variable_box.remove(child)
            checkboxes.clear()
            output_kind = kind.get_active_text() or 'Nodal'
            selected_variables = (set(selected['Variables']) if selected and selected['Kind'] == output_kind else
                                  {'U'} if output_kind == 'Nodal' else
                                  {'S', 'E'} if output_kind == 'Element' else {'CDIS', 'CSTR'})
            for variable in data['variableChoices'][output_kind]:
                button = Gtk.CheckButton(label=variable); button.set_active(variable in selected_variables)
                checkboxes[variable] = button; variable_box.add(button)
            variable_box.show_all()
        kind.connect('changed', rebuild); rebuild()
        box.pack_start(Gtk.Label(label='Variables', xalign=0), False, False, 3); box.pack_start(variable_box, False, False, 3)
        last_iterations, contact_elements, global_output = Gtk.CheckButton(label='Last iterations'), Gtk.CheckButton(label='Contact elements'), Gtk.CheckButton(label='Global output')
        global_output.set_active(selected['Global'] if selected else True)
        last_iterations.set_active(selected['LastIterations'] if selected else False)
        contact_elements.set_active(selected['ContactElements'] if selected else False)
        modes = Gtk.ComboBoxText()
        for item in ('Default', 'TwoD', 'ThreeD'): modes.append_text(item)
        modes.set_active(('Default', 'TwoD', 'ThreeD').index(selected['ElementOutput']) if selected else 0)
        for widget in (last_iterations, contact_elements, global_output): box.pack_start(widget, False, False, 2)
        box.pack_start(Gtk.Label(label='Element output mode', xalign=0), False, False, 3); box.pack_start(modes, False, False, 3)
        def mode_sensitivity(*_): modes.set_sensitive(kind.get_active_text() == 'Element')
        kind.connect('changed', mode_sensitivity); mode_sensitivity()
        message = Gtk.Label(label='Select at least one original CalculiX field variable. Old results are removed.', xalign=0, wrap=True)
        box.pack_start(message, False, False, 6)
        def configured(_, answer):
            if answer != Gtk.ResponseType.ACCEPT:
                dialog.destroy(); self.dialog = None; return
            output_name, step_name, output_kind = name.get_text().strip(), step.get_active_text(), kind.get_active_text()
            variables = [value for value, button in checkboxes.items() if button.get_active()]
            if not output_name or not variables:
                message.set_text('Enter a unique name and select at least one variable.'); return
            settings = {'Name': output_name, 'Kind': output_kind, 'Variables': variables,
                        'LastIterations': last_iterations.get_active(), 'ContactElements': contact_elements.get_active(),
                        'Global': global_output.get_active(), 'ElementOutput': modes.get_active_text() if output_kind == 'Element' else 'Default'}
            if action == 'duplicate':
                change = {'command': 'duplicate-field-output', 'step': selected['Step'], 'output': selected['Name'], 'name': output_name}
                if args.smoke_field_output: self.field_output_smoke_expected.append({**selected, 'Name': output_name})
            else:
                change = {'command': 'add-field-output' if action == 'add' else 'edit-field-output',
                          'step': step_name, 'settings': settings}
                if action == 'edit': change['output'] = selected['Name']
                if args.smoke_field_output:
                    expected = {**settings, 'Step': step_name, 'Active': selected['Active'] if selected else True}
                    if action == 'add': self.field_output_smoke_expected.append(expected)
                    else: self.field_output_smoke_expected[[item['Step'] + '\0' + item['Name'] for item in data['outputs']]
                                                          .index(selected['Step'] + '\0' + selected['Name'])] = expected
            if args.smoke_field_output:
                step_order = {step: index for index, step in enumerate(data['steps'])}
                self.field_output_smoke_expected.sort(key=lambda item: step_order[item['Step']])
            dialog.destroy(); self.dialog = None; self.save_edit(change, args.smoke_field_output)
        dialog.connect('response', configured); dialog.show_all()
        if args.smoke_field_output:
            name.set_text('created-field' if action == 'add' else 'renamed-field' if action == 'edit' else 'field-copy')
            if action != 'duplicate':
                last_iterations.set_active(True); global_output.set_active(False)
                if 'RF' in checkboxes: checkboxes['RF'].set_active(True)
            dialog.emit('response', Gtk.ResponseType.ACCEPT)

    def edit_sections(self):
        if self.opening or self.solving or self.dialog or self.section_pending or self.scene is None:
            return
        generation = self.generation
        self.section_pending = True
        def listed(reply):
            self.section_pending = False
            if generation != self.generation or self.opening or self.solving or self.dialog:
                return
            if not reply.get('ok'):
                self.label.set_text(reply.get('error', 'Unable to read sections'))
                return
            sections = reply.get('sections', [])
            error = reply.get('editingError')
            coverage = reply['coverage']
            dialog = Gtk.Dialog(title='Solid sections', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_button('_Close', Gtk.ResponseType.CLOSE)
            dialog.set_default_size(850, 380)
            box = dialog.get_content_area()
            message = error or ('Active elements: %d · unassigned: %d · overlapping: %d · invalid regions: %d · invalid materials: %d. Later active sections take precedence.' %
                (coverage['ActiveElements'], coverage['UnassignedElements'], coverage['OverlappingElements'], coverage['InvalidRegions'], coverage['InvalidMaterials']))
            box.pack_start(Gtk.Label(label=message, xalign=0, wrap=True), False, False, 8)
            rows = Gtk.ListStore(str, str, str, str, str, int, int, str)
            for section in sections:
                rows.append((section['Name'], 'Active' if section['Active'] else 'Inactive', section['Material'],
                             section['RegionType'], section['Region'], section['Elements'], section['EffectiveElements'], section.get('Error') or ''))
            tree = Gtk.TreeView(model=rows)
            tree.get_accessible().set_name('Solid section assignments')
            for column, title in enumerate(('Section', 'State', 'Material', 'Region type', 'Region', 'Elements', 'Effective', 'Issue')):
                tree.append_column(Gtk.TreeViewColumn(title, Gtk.CellRendererText(), text=column))
            scroll = Gtk.ScrolledWindow(); scroll.add(tree)
            box.pack_start(scroll, True, True, 4)
            actions = Gtk.Box(spacing=6); box.pack_start(actions, False, False, 6)
            buttons = {}
            def close(*_):
                dialog.destroy(); self.dialog = None
            dialog.connect('response', close)
            def operate(_, action):
                selection, iterator = tree.get_selection().get_selected()
                index = selection.get_path(iterator).get_indices()[0] if iterator is not None else None
                section = sections[index] if index is not None else None
                if error or action != 'add' and section is None:
                    return
                if args.smoke_section:
                    expected = [dict(item) for item in sections]
                    if action == 'remove':
                        del expected[index]
                    elif action in ('activate', 'deactivate'):
                        expected[index]['Active'] = action == 'activate'
                    self.section_smoke_expected = expected
                close()
                if action in ('add', 'edit', 'duplicate'):
                    self.section_form(action, section, reply)
                else:
                    change = {'command': 'remove-section' if action == 'remove' else 'section-active', 'section': section['Name']}
                    if action != 'remove': change['active'] = action == 'activate'
                    self.save_edit(change, args.smoke_section)
            for action, title in (('add', 'Add…'), ('edit', 'Edit…'), ('duplicate', 'Duplicate…'),
                                   ('activate', 'Activate'), ('deactivate', 'Deactivate'), ('remove', 'Remove')):
                button = Gtk.Button(label=title)
                button.get_accessible().set_name(action.capitalize() + ' solid section')
                button.set_sensitive(not error and ((bool(reply['materials']) and bool(reply['regions'])) if action == 'add' else bool(sections)))
                button.connect('clicked', operate, action)
                actions.pack_start(button, False, False, 0); buttons[action] = button
            if sections: tree.get_selection().select_path(0)
            dialog.show_all()
            if args.smoke_section:
                if error: self.finish(False, error)
                else: buttons[args.smoke_section_action].clicked()
        self.submit({'command': 'section-data'}, listed)

    def section_form(self, action, selected, data, initial_region=None):
        dialog = Gtk.Dialog(title=action.capitalize() + ' solid section', transient_for=self.window, modal=True)
        self.dialog = dialog
        dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
        box = dialog.get_content_area()
        name, material, region = Gtk.Entry(), Gtk.ComboBoxText(), Gtk.ComboBoxText()
        name.set_text(selected['Name'] if action == 'edit' else 'solid-' + str(len(data['sections']) + 1) if action == 'add'
                      else selected['Name'][:120] + '-copy')
        for index, item in enumerate(data['materials']):
            material.append_text(item['Name'] + ('' if item['Active'] else ' (inactive)'))
        for item in data['regions']:
            region.append_text(item['RegionType'] + ': ' + item['Name'] + ' · %d elements' % item['Elements'])
        material.set_active(next((index for index, item in enumerate(data['materials']) if selected and item['Name'] == selected['Material']), 0))
        preferred_region = (selected['RegionType'], selected['Region']) if selected else initial_region
        region.set_active(next((index for index, item in enumerate(data['regions']) if
            (item['RegionType'], item['Name']) == preferred_region), 0))
        for label, widget in (('Section name', name), ('Material', material), ('Solid region', region)):
            widget.get_accessible().set_name(label)
            box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
            box.pack_start(widget, False, False, 4)
        if action == 'duplicate':
            material.set_sensitive(False); region.set_sensitive(False)
        message = Gtk.Label(label='Assign an existing material to a named part or element set. Overlapping assignments use the later active section. Old results are removed.',
                            xalign=0, wrap=True)
        box.pack_start(message, False, False, 8)
        def configured(_, answer):
            if answer != Gtk.ResponseType.ACCEPT:
                dialog.destroy(); self.dialog = None
                return
            section_name = name.get_text().strip()
            if not section_name or any(item['Name'].lower() == section_name.lower() and
                    not (action == 'edit' and item['Name'] == selected['Name']) for item in data['sections']):
                message.set_text('Enter a unique section name.'); return
            if action == 'duplicate':
                change = {'command': 'duplicate-section', 'section': selected['Name'], 'name': section_name}
                if args.smoke_section: self.section_smoke_expected.append({**selected, 'Name': section_name})
            else:
                if material.get_active() < 0 or region.get_active() < 0:
                    message.set_text('Create a material and choose a solid region first.'); return
                target = data['regions'][region.get_active()]
                settings = {'Name': section_name, 'Material': data['materials'][material.get_active()]['Name'],
                            'RegionType': target['RegionType'], 'Region': target['Name']}
                change = {'command': 'add-section' if action == 'add' else 'edit-section', 'settings': settings}
                if action == 'edit': change['section'] = selected['Name']
                if args.smoke_section:
                    expected = {**settings, 'Active': selected['Active'] if selected else True, 'Elements': target['Elements'], 'Error': None}
                    if action == 'add': self.section_smoke_expected.append(expected)
                    else: self.section_smoke_expected[[item['Name'] for item in data['sections']].index(selected['Name'])] = expected
            dialog.destroy(); self.dialog = None
            self.save_edit(change, args.smoke_section)
        dialog.connect('response', configured); dialog.show_all()
        if args.smoke_element_sets and initial_region:
            if preferred_region != (data['regions'][region.get_active()]['RegionType'], data['regions'][region.get_active()]['Name']):
                self.finish(False, 'The element-set section action did not preselect its region'); return
            name.set_text('authored-section')
            dialog.emit('response', Gtk.ResponseType.ACCEPT)
        if args.smoke_section:
            name.set_text('created-solid' if action == 'add' else 'renamed-solid' if action == 'edit' else 'solid-copy')
            if action == 'edit' and len(data['materials']) > 1: material.set_active(1)
            if args.smoke_section_region and action != 'duplicate':
                region.set_active(next(index for index, item in enumerate(data['regions']) if
                    item['RegionType'] + ':' + item['Name'] == args.smoke_section_region))
            dialog.emit('response', Gtk.ResponseType.ACCEPT)

    def edit_steps(self):
        if self.opening or self.solving or self.dialog or self.step_pending or self.scene is None:
            return
        generation = self.generation
        self.step_pending = True
        def listed(reply):
            self.step_pending = False
            if generation != self.generation or self.opening or self.solving or self.dialog:
                return
            if not reply.get('ok'):
                self.label.set_text(reply.get('error', 'Unable to read analysis steps'))
                return
            steps = reply.get('steps', [])
            editing_error = reply.get('editingError')
            dialog = Gtk.Dialog(title='Analysis steps', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_button('_Close', Gtk.ResponseType.CLOSE)
            dialog.set_default_size(700, 360)
            box = dialog.get_content_area()
            box.pack_start(Gtk.Label(label=editing_error or 'Static analyses run in this order. Apply changes to the current model; use Save to write the project.',
                                    xalign=0, wrap=True), False, False, 8)
            rows = Gtk.ListStore(str, str, str, int, int)
            for step in steps:
                rows.append((step['Name'], 'Active' if step['Active'] else 'Inactive',
                             step['IncrementationType'], step['Boundaries'], step['Loads']))
            tree = Gtk.TreeView(model=rows)
            tree.get_accessible().set_name('Analysis step sequence')
            for column, title in enumerate(('Step', 'State', 'Incrementation', 'Supports', 'Loads')):
                tree.append_column(Gtk.TreeViewColumn(title, Gtk.CellRendererText(), text=column))
            scroll = Gtk.ScrolledWindow()
            scroll.add(tree)
            box.pack_start(scroll, True, True, 4)
            actions = Gtk.Box(spacing=6)
            box.pack_start(actions, False, False, 6)
            buttons = {}
            def close(*_):
                dialog.destroy()
                self.dialog = None
            dialog.connect('response', close)
            def operate(button, action):
                selection, iterator = tree.get_selection().get_selected()
                index = selection.get_path(iterator).get_indices()[0] if iterator is not None else None
                selected = steps[index] if index is not None else None
                if editing_error or action != 'add' and selected is None:
                    return
                if args.smoke_step:
                    expected = [dict(step) for step in steps]
                    if action in ('activate', 'deactivate'):
                        expected[index]['Active'] = action == 'activate'
                    elif action == 'remove':
                        del expected[index]
                    self.step_smoke_expected = expected
                close()
                if action in ('add', 'edit', 'duplicate'):
                    self.step_form(action, selected, steps)
                else:
                    change = {'command': 'remove-step' if action == 'remove' else 'step-active', 'step': selected['Name']}
                    if action != 'remove':
                        change['active'] = action == 'activate'
                    self.save_edit(change, args.smoke_step)
            for action, title in (('add', 'Add…'), ('edit', 'Edit…'), ('duplicate', 'Duplicate…'),
                                   ('activate', 'Activate'), ('deactivate', 'Deactivate'), ('remove', 'Remove')):
                button = Gtk.Button(label=title)
                button.get_accessible().set_name(action.capitalize() + ' analysis step')
                button.connect('clicked', operate, action)
                button.set_sensitive(not editing_error and (action == 'add' or bool(steps)))
                actions.pack_start(button, False, False, 0)
                buttons[action] = button
            if steps:
                tree.get_selection().select_path(0)
            dialog.show_all()
            if args.smoke_step:
                if editing_error:
                    self.finish(False, editing_error)
                else:
                    buttons[args.smoke_step_action].clicked()
        self.submit({'command': 'step-data'}, listed)

    def step_form(self, action, selected, steps):
        dialog = Gtk.Dialog(title={'add': 'Add static step', 'edit': 'Edit static step', 'duplicate': 'Duplicate static step'}[action],
                            transient_for=self.window, modal=True)
        self.dialog = dialog
        dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
        box = dialog.get_content_area()
        name = Gtk.Entry()
        name.get_accessible().set_name('Analysis step name')
        name.set_text(selected['Name'] if action == 'edit' else ('load-' + str(len(steps) + 1) if action == 'add'
                      else selected['Name'][:120] + '-copy'))
        box.pack_start(Gtk.Label(label='Step name', xalign=0), False, False, 4)
        box.pack_start(name, False, False, 4)
        nonlinear = Gtk.CheckButton(label='Geometrically nonlinear (NLGEOM)')
        mode = Gtk.ComboBoxText()
        for value in ('Default', 'Automatic', 'Direct'):
            mode.append_text(value)
        mode.get_accessible().set_name('Static step incrementation')
        entries = {}
        if action != 'duplicate':
            nonlinear.set_active(bool(selected and selected['Nlgeom']))
            box.pack_start(nonlinear, False, False, 4)
            box.pack_start(Gtk.Label(label='Incrementation', xalign=0), False, False, 4)
            box.pack_start(mode, False, False, 4)
            grid = Gtk.Grid(column_spacing=8, row_spacing=4)
            for row, (key, label, default) in enumerate((('TimePeriod', 'Time period', 1),
                    ('InitialTimeIncrement', 'Initial increment', 1), ('MinTimeIncrement', 'Minimum increment', 1e-5),
                    ('MaxTimeIncrement', 'Maximum increment', 1e30), ('MaxIncrements', 'Maximum increments', 100))):
                entry = Gtk.Entry()
                entry.set_text(str(selected[key] if selected else default))
                entry.get_accessible().set_name(label)
                grid.attach(Gtk.Label(label=label, xalign=0), 0, row, 1, 1)
                grid.attach(entry, 1, row, 1, 1)
                entries[key] = entry
            box.pack_start(grid, False, False, 4)
            def changed(*_):
                for key, entry in entries.items():
                    entry.set_sensitive(mode.get_active() != 0 and (mode.get_active() == 1 or key not in ('MinTimeIncrement', 'MaxTimeIncrement')))
            mode.connect('changed', changed)
            mode.set_active(('Default', 'Automatic', 'Direct').index(selected['IncrementationType']) if selected else 0)
        copy_previous = Gtk.CheckButton(label='Copy supports and loads from the last step')
        if action == 'add':
            copy_previous.set_active(True)
            copy_previous.set_sensitive(bool(steps))
            box.pack_start(copy_previous, False, False, 4)
        message = Gtk.Label(label='Old results are removed. New steps use the standard displacement/reaction and strain/stress field outputs.'
                            if action == 'add' else 'Existing step definitions are preserved; old results are removed.', xalign=0, wrap=True)
        box.pack_start(message, False, False, 8)
        def configured(_, answer):
            if answer != Gtk.ResponseType.ACCEPT:
                dialog.destroy(); self.dialog = None
                return
            step_name = name.get_text().strip()
            if not step_name or any(step['Name'].lower() == step_name.lower() and
                                    not (action == 'edit' and step['Name'] == selected['Name']) for step in steps):
                message.set_text('Enter a unique step name.')
                return
            if action == 'duplicate':
                change = {'command': 'duplicate-step', 'step': selected['Name'], 'name': step_name}
                if args.smoke_step:
                    self.step_smoke_expected.append({**selected, 'Name': step_name})
            else:
                try:
                    settings = {key: int(entry.get_text()) if key == 'MaxIncrements' else float(entry.get_text())
                                for key, entry in entries.items()}
                    values = [value for key, value in settings.items() if key != 'MaxIncrements']
                    if (not all(math.isfinite(value) and value > 0 for value in values) or
                            not 1 <= settings['MaxIncrements'] <= 1000000 or
                            not settings['MinTimeIncrement'] <= settings['InitialTimeIncrement'] <= settings['MaxTimeIncrement'] or
                            settings['InitialTimeIncrement'] > settings['TimePeriod']):
                        raise ValueError()
                except ValueError:
                    message.set_text('Use finite positive times, min ≤ initial ≤ max, initial ≤ period, and 1–1,000,000 increments.')
                    return
                settings.update(Name=step_name, Nlgeom=nonlinear.get_active(), IncrementationType=mode.get_active_text())
                change = {'command': 'add-step' if action == 'add' else 'edit-step', 'settings': settings}
                if action == 'add':
                    change['copyPrevious'] = copy_previous.get_active()
                else:
                    change['step'] = selected['Name']
                if args.smoke_step:
                    if action == 'add':
                        previous = steps[-1] if steps and copy_previous.get_active() else {}
                        self.step_smoke_expected.append({**settings, 'Active': True,
                            'Boundaries': previous.get('Boundaries', 0), 'Loads': previous.get('Loads', 0), 'FieldOutputs': 2, 'HistoryOutputs': 0})
                    else:
                        self.step_smoke_expected[[step['Name'] for step in steps].index(selected['Name'])].update(settings)
            dialog.destroy(); self.dialog = None
            self.save_edit(change, args.smoke_step)
        dialog.connect('response', configured)
        dialog.show_all()
        if args.smoke_step:
            name.set_text('load-2' if action == 'add' else 'renamed-step' if action == 'edit' else 'load-copy')
            if action != 'duplicate':
                mode.set_active(1)
                for key, value in {'TimePeriod': 2, 'InitialTimeIncrement': .5, 'MinTimeIncrement': 1e-5,
                                   'MaxTimeIncrement': 1, 'MaxIncrements': 200}.items():
                    entries[key].set_text(str(value))
            dialog.emit('response', Gtk.ResponseType.ACCEPT)

    def edit_plasticity(self):
        if self.opening or self.solving or self.dialog or self.plasticity_pending or self.scene is None:
            return
        self.plasticity_pending = True
        def listed(reply):
            self.plasticity_pending = False
            materials = reply.get('materials', [])
            if not reply.get('ok') or not materials:
                self.label.set_text(reply.get('error', 'No editable constant elastic material is available.'))
                if args.smoke_plasticity:
                    self.finish(False, 'GTK plasticity material list unavailable')
                return
            dialog = Gtk.Dialog(title='Material plasticity', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            material = Gtk.ComboBoxText()
            for item in materials:
                material.append_text(item['Name'])
            hardening = Gtk.ComboBoxText()
            for value in ('Isotropic', 'Kinematic', 'Combined'):
                hardening.append_text(value)
            rows = Gtk.TextView()
            rows.set_monospace(True)
            rows.set_wrap_mode(Gtk.WrapMode.NONE)
            rows.set_size_request(440, 180)
            scroll = Gtk.ScrolledWindow()
            scroll.set_policy(Gtk.PolicyType.AUTOMATIC, Gtk.PolicyType.AUTOMATIC)
            scroll.add(rows)
            remove = Gtk.CheckButton(label='Remove plasticity from this material')
            message = Gtk.Label(label='Enter one yield stress, plastic strain pair per line. The first plastic strain must be zero; later strains must increase. Use current model units. Old results are removed.', wrap=True, xalign=0)
            for label, widget in (('Material', material), ('Hardening rule', hardening),
                                  ('Yield stress, plastic strain', scroll)):
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                widget.get_accessible().set_name(label)
                box.pack_start(widget, widget is scroll, widget is scroll, 4)
            box.pack_start(remove, False, False, 4)
            box.pack_start(message, False, False, 8)

            def selected(*_):
                item = materials[material.get_active()]
                names = ('Isotropic', 'Kinematic', 'Combined')
                hardening.set_active(names.index(item['Hardening']) if item.get('Hardening') in names else 0)
                text = '\n'.join(format(point['YieldStress'], '.17g') + ', ' + format(point['PlasticStrain'], '.17g')
                                 for point in item.get('Points', []))
                rows.get_buffer().set_text(text)
                remove.set_active(False)
                remove.set_sensitive(bool(item.get('Points')))
            material.connect('changed', selected)
            material.set_active(0)

            def configured(_dialog, response):
                if response != Gtk.ResponseType.ACCEPT:
                    dialog.destroy(); self.dialog = None
                    return
                selected_material = materials[material.get_active()]
                if remove.get_active():
                    change = {'command': 'remove-plasticity', 'material': selected_material['Name']}
                    expected = {'Name': selected_material['Name'], 'Hardening': None, 'Points': []}
                else:
                    buffer = rows.get_buffer()
                    text = buffer.get_text(buffer.get_start_iter(), buffer.get_end_iter(), True)
                    try:
                        points = []
                        for line in text.splitlines():
                            if not line.strip():
                                continue
                            values = line.replace(',', ' ').split()
                            if len(values) != 2:
                                raise ValueError()
                            stress, strain = map(float, values)
                            if not math.isfinite(stress) or not math.isfinite(strain):
                                raise ValueError()
                            points.append({'YieldStress': stress, 'PlasticStrain': strain})
                        if not points:
                            raise ValueError()
                    except ValueError:
                        message.set_text('Enter one finite numeric yield stress and plastic strain pair per line.')
                        return
                    change = {'command': 'set-plasticity', 'material': selected_material['Name'],
                              'hardening': hardening.get_active(), 'points': points}
                    expected = {'Name': selected_material['Name'], 'Hardening': hardening.get_active_text(), 'Points': points}
                if args.smoke_plasticity:
                    self.plasticity_smoke_expected = expected
                dialog.destroy(); self.dialog = None
                self.save_edit(change, args.smoke_plasticity)
            dialog.connect('response', configured)
            dialog.show_all()
            if args.smoke_plasticity:
                candidates = [index for index, item in enumerate(materials) if bool(item.get('Points')) == (args.smoke_plasticity_action in ('edit', 'remove'))]
                if not candidates:
                    self.finish(False, 'GTK plasticity smoke requires ' + ('an existing plastic property' if args.smoke_plasticity_action != 'set' else 'an editable material without plasticity'))
                    return
                material.set_active(candidates[0])
                if args.smoke_plasticity_action == 'remove':
                    remove.set_active(True)
                else:
                    hardening.set_active(1 if args.smoke_plasticity_action == 'set' else 2)
                    rows.get_buffer().set_text('100, 0\n120, 0.01\n150, 0.05' if args.smoke_plasticity_action == 'set' else '90, 0\n130, 0.02')
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.submit({'command': 'plasticity-data'}, listed)

    def add_material(self):
        if self.opening or self.solving or self.dialog:
            return
        self.opening = True
        def listed(reply):
            self.opening = False
            sections = reply.get('sections', [])
            materials = {item['Name']: item for item in reply.get('materials', [])}
            if not reply.get('ok'):
                self.label.set_text('Unable to read material and section choices.')
                if args.smoke_add_material:
                    self.finish(False, 'GTK material-assignment list unavailable')
                return
            dialog = Gtk.Dialog(title='Add elastic material', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            selector = Gtk.ComboBoxText()
            selector.append_text('Create without section assignment')
            for item in sections:
                selector.append_text(f"{item['Section']} — {item['Region']} — current: {item['Material']}")
            name = Gtk.Entry()
            modulus = Gtk.Entry()
            ratio = Gtk.Entry()
            for label, widget in (('Assign to section (optional)', selector), ('New material name', name),
                                  ('Young’s modulus', modulus), ('Poisson’s ratio', ratio)):
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                widget.get_accessible().set_name(label)
                box.pack_start(widget, False, False, 4)
            message = Gtk.Label(label='Create one constant elastic material, optionally assigning it to a solid section. '
                                      'Use the current model units and a decimal point. Old results are removed.', wrap=True)
            box.pack_start(message, False, False, 8)
            def selected(*_):
                section = sections[selector.get_active() - 1] if selector.get_active() > 0 else None
                current = materials.get(section['Material']) if section else None
                modulus.set_text(format(current['YoungsModulus'] if current else 1000, '.17g'))
                ratio.set_text(format(current['PoissonsRatio'] if current else 0.3, '.17g'))
            selector.connect('changed', selected)
            selector.set_active(1 if sections and not args.smoke_material_only else 0)
            def responded(_, response):
                if response != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    return
                try:
                    youngs_modulus = float(modulus.get_text())
                    poissons_ratio = float(ratio.get_text())
                    if not (math.isfinite(youngs_modulus) and youngs_modulus > 0 and
                            math.isfinite(poissons_ratio) and -1 < poissons_ratio < 0.5):
                        raise ValueError()
                except ValueError:
                    message.set_text('Enter finite values: Young’s modulus > 0 and -1 < Poisson’s ratio < 0.5. Use a decimal point.')
                    return
                material_name = name.get_text().strip()
                if not material_name:
                    message.set_text('Enter a unique material name.')
                    return
                section = sections[selector.get_active() - 1] if selector.get_active() > 0 else None
                if args.smoke_add_material:
                    self.created_material_smoke = (section['Section'] if section else None, material_name, youngs_modulus, poissons_ratio)
                dialog.destroy()
                self.dialog = None
                self.save_edit({'command': 'add-material', 'section': section['Section'] if section else None, 'name': material_name,
                                'youngsModulus': youngs_modulus, 'poissonsRatio': poissons_ratio}, args.smoke_add_material)
            dialog.connect('response', responded)
            dialog.show_all()
            if args.smoke_add_material:
                name.set_text('created-elastic')
                modulus.set_text(format(float(modulus.get_text()) * 2, '.17g'))
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.submit({'command': 'material-data'}, listed)

    def assign_material(self):
        if self.opening or self.solving or self.dialog:
            return
        self.opening = True
        def listed(reply):
            self.opening = False
            materials = reply.get('materials', [])
            material_names = [item['Name'] for item in materials]
            sections = [item for item in reply.get('sections', [])
                        if any(name != item['Material'] for name in material_names)]
            if not reply.get('ok') or not sections:
                self.label.set_text('No solid section has a different supported material available.')
                if args.smoke_assign_material:
                    self.finish(False, 'GTK material reassignment unavailable')
                return
            dialog = Gtk.Dialog(title='Assign existing material to solid section', transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            section_selector = Gtk.ComboBoxText()
            material_selector = Gtk.ComboBoxText()
            for item in sections:
                section_selector.append_text(f"{item['Section']} — {item['Region']} — current: {item['Material']}")
            for item in materials:
                material_selector.append_text(f"{item['Name']} — E {format(item['YoungsModulus'], '.6g')}, ν {format(item['PoissonsRatio'], '.6g')}")
            for label, widget in (('Solid section', section_selector), ('Existing material', material_selector)):
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                widget.get_accessible().set_name(label)
                box.pack_start(widget, False, False, 4)
            message = Gtk.Label(label='Assign the selected existing material to this solid section. '
                                      'Other sections and materials stay unchanged. Old results are removed.', wrap=True)
            box.pack_start(message, False, False, 8)
            def section_changed(*_):
                current = sections[section_selector.get_active()]['Material']
                material_selector.set_active(next((index for index, name in enumerate(material_names) if name == current), 0))
            section_selector.connect('changed', section_changed)
            section_selector.set_active(0)
            def responded(_, response):
                if response != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    return
                section = sections[section_selector.get_active()]
                material = materials[material_selector.get_active()]
                if section['Material'] == material['Name']:
                    message.set_text('Choose a material different from the section’s current assignment.')
                    return
                if args.smoke_assign_material:
                    self.assigned_material_smoke = (section['Section'], material['Name'])
                dialog.destroy()
                self.dialog = None
                self.save_edit({'command': 'assign-material', 'section': section['Section'], 'name': material['Name']},
                               args.smoke_assign_material)
            dialog.connect('response', responded)
            dialog.show_all()
            if args.smoke_assign_material:
                current = sections[0]['Material']
                material_selector.set_active(next(index for index, name in enumerate(material_names) if name != current))
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.submit({'command': 'material-data'}, listed)

    def edit_properties(self, force=False, boundary=False, create=False, target=None):
        if self.opening or self.solving or self.dialog:
            return
        collection = 'load-targets' if create and force else 'boundary-targets' if create else 'boundary-directions' if boundary else 'loads' if force else 'material-data'
        smoke_path = args.smoke_boundary if boundary else args.smoke_load if force else args.smoke_material
        title = 'Add force or surface traction' if create and force else 'Add displacement boundary' if create else 'Edit prescribed displacement' if boundary else 'Edit force load' if force else 'Edit elastic material'
        keys = ('Value',) if boundary else ('F1', 'F2', 'F3') if force else ('YoungsModulus', 'PoissonsRatio', 'Density')
        labels = ('Prescribed displacement (model length units)',) if boundary else ('Force X', 'Force Y', 'Force Z') if force else ('Young’s modulus', 'Poisson’s ratio', 'Density (blank = none)')
        self.opening = True
        def listed(reply):
            self.opening = False
            items = reply.get('boundaries' if boundary else 'loads' if force else 'materials', [])
            if target:
                items = [item for item in items if (create or item['Name'] == target['Name']) and
                         (not (force or boundary) or item['Step'] == target['Step'])]
            used_materials = {section['Material'] for section in reply.get('sections', [])}
            if not reply.get('ok') or not items:
                self.label.set_text('No supported prescribed translations are available.' if boundary else 'No supported force loads are available.' if force else 'No supported constant elastic materials are available.')
                if smoke_path:
                    self.finish(False, 'GTK property list unavailable')
                return
            dialog = Gtk.Dialog(title=title, transient_for=self.window, modal=True)
            self.dialog = dialog
            dialog.add_buttons('_Cancel', Gtk.ResponseType.CANCEL, '_Apply', Gtk.ResponseType.ACCEPT)
            box = dialog.get_content_area()
            selector = Gtk.ComboBoxText()
            for item in items:
                selector.append_text(f"{item['Step']} / {item['Name']} — {item['Kind']} — {item['Region']}" if force or boundary else item['Name'])
            boundary_name = Gtk.Entry() if create else None
            if boundary_name:
                box.pack_start(Gtk.Label(label='New force name' if force else 'New boundary name', xalign=0), False, False, 4)
                boundary_name.get_accessible().set_name('New force name' if force else 'New boundary name')
                box.pack_start(boundary_name, False, False, 4)
            entries = [Gtk.Entry() for _ in keys]
            for label, widget in [('Boundary / direction' if boundary else 'Load' if force else 'Material', selector), *zip(labels, entries)]:
                box.pack_start(Gtk.Label(label=label, xalign=0), False, False, 4)
                widget.get_accessible().set_name(label)
                box.pack_start(widget, False, False, 4)
            free = Gtk.CheckButton(label='Free direction (no prescribed displacement)') if boundary and not create else None
            if free:
                box.pack_start(free, False, False, 4)
                free.connect('toggled', lambda widget: entries[0].set_sensitive(not widget.get_active()))
            remove = Gtk.CheckButton(label='Remove the entire selected boundary from this step' if boundary else
                                    'Remove the selected force from this step' if force else
                                    'Remove the selected material if no section uses it') if not create else None
            if remove:
                box.pack_start(remove, False, False, 4)
                def removal_changed(widget):
                    for entry in entries:
                        entry.set_sensitive(not widget.get_active() and not (free and free.get_active()))
                    if free:
                        free.set_sensitive(not widget.get_active())
                    if widget.get_active():
                        message.set_text(('The material will be removed from the current model. This succeeds only when no section uses it.'
                                          if not boundary and not force else
                                          'The entire definition will be removed from the current model. Other steps stay unchanged.' +
                                          (' Removing supports can leave a model unconstrained.' if boundary else '')))
                    else:
                        message.set_text('Apply the selected values to the current model. Old results are removed.')
                remove.connect('toggled', removal_changed)
            def force_creation_help(item):
                return ('Surface-traction components are the TOTAL force distributed over the selected surface. ' if item['Kind'] == 'Surface traction (total force)' else
                        'Concentrated-force components apply to EACH node in the selected set. ') + 'Choose a unique load name. '
            message = Gtk.Label(label=(force_creation_help(items[0]) if create and force else 'Choose an existing step, node set and direction, and a unique boundary name. Other directions start free. ' if create else 'Select Free to release a direction, or enter a displacement to constrain it. '
                                       'Keep at least one constrained direction in this boundary. Rotations stay unchanged. ' if boundary else '') +
                                'Use the current model units and a decimal point. Apply to the current model. '
                                'Old results are removed; solve the edited model again.', wrap=True)
            box.pack_start(message, False, False, 8)
            def selected(*_):
                item = items[selector.get_active()]
                for key, entry in zip(keys, entries):
                    entry.set_text('' if key == 'Density' and item[key] is None else format(item[key] if item[key] is not None else 0, '.17g'))
                if free:
                    free.set_active(item['Value'] is None)
                if remove:
                    remove.set_active(False)
                    if not boundary and not force:
                        removable = item['Name'] not in used_materials
                        remove.set_sensitive(removable)
                        remove.set_tooltip_text(None if removable else 'Reassign every section that uses this material before removing it.')
                if create and force:
                    message.set_text(force_creation_help(item) + 'Use the current model force units and a decimal point. Apply to the current model. Old results are removed; solve the edited model again.')
            selector.connect('changed', selected)
            selector.set_active(0)
            def responded(_, response):
                if response != Gtk.ResponseType.ACCEPT:
                    dialog.destroy()
                    self.dialog = None
                    return
                if remove and remove.get_active():
                    item = items[selector.get_active()]
                    change = {'command': 'remove-boundary' if boundary else 'remove-load' if force else 'remove-material',
                              'name': item['Name']}
                    if boundary or force:
                        change['step'] = item['Step']
                    dialog.destroy()
                    self.save_edit(change, smoke_path)
                    return
                try:
                    density = None
                    if not boundary and not force:
                        values = [float(entries[0].get_text()), float(entries[1].get_text())]
                        density = None if not entries[2].get_text().strip() else float(entries[2].get_text())
                    else:
                        values = [0] if free and free.get_active() else [float(entry.get_text()) for entry in entries]
                    if not all(math.isfinite(value) for value in values):
                        raise ValueError()
                    if not force and not boundary and not (values[0] > 0 and -1 < values[1] < 0.5 and
                                                           (density is None or math.isfinite(density) and density > 0)):
                        raise ValueError()
                except ValueError:
                    message.set_text('Enter a finite displacement. Use a decimal point.' if boundary else 'Enter finite force components. Use a decimal point.' if force else
                                     'Enter finite values: Young’s modulus > 0, -1 < Poisson’s ratio < 0.5, and optional density > 0. Use a decimal point.')
                    return
                item = items[selector.get_active()]
                if create and not boundary_name.get_text().strip():
                    message.set_text('Enter a name for the new force.' if force else 'Enter a name for the new boundary.')
                    return
                change = {'command': 'add-load' if create and force else 'add-boundary' if create else 'edit-boundary-direction' if boundary else 'edit-load' if force else 'edit-material', 'name': item['Name']}
                if boundary:
                    change.update(step=item['Step'], direction=item['Direction'], value=None if free and free.get_active() else values[0])
                    if create:
                        change.update(name=boundary_name.get_text().strip(), region=item['Region'])
                elif force:
                    change.update(step=item['Step'], f1=values[0], f2=values[1], f3=values[2])
                    if create:
                        change.update(name=boundary_name.get_text().strip(), region=item['Region'],
                                      surfaceTraction=item['Kind'] == 'Surface traction (total force)')
                else:
                    change.update(youngsModulus=values[0], poissonsRatio=values[1], density=density)
                dialog.destroy()
                self.save_edit(change, smoke_path)
            dialog.connect('response', responded)
            dialog.show_all()
            if smoke_path:
                index = 0
                if boundary and args.smoke_boundary_action == 'constrain':
                    index = next(i for i, item in enumerate(items) if item['Value'] is None)
                elif boundary and args.smoke_boundary_action == 'release':
                    index = next(i for i, item in enumerate(items) if item['Value'] is not None and
                                 sum(other['Step'] == item['Step'] and other['Name'] == item['Name'] and
                                     other['Value'] is not None for other in items) > 1)
                if create:
                    if force and args.smoke_load_action == 'add-traction':
                        index = next((i for i, item in enumerate(items) if item['Region'] == 'LOADED' and item['Kind'] == 'Surface traction (total force)'), 0)
                    else:
                        index = next((i for i, item in enumerate(items) if item['Region'] == 'RIGHT' and (force or item['Direction'] == 1)), 0)
                elif not boundary and not force and args.smoke_material_action == 'remove':
                    index = next((i for i, item in enumerate(items) if item['Name'] == 'created-elastic'), 0)
                    assigned_index = next(i for i, item in enumerate(items) if item['Name'] in used_materials)
                    selector.set_active(assigned_index)
                    if remove.get_sensitive():
                        self.finish(False, 'GTK material editor enabled removal for an assigned material')
                        return
                selector.set_active(index)
                if (not boundary and not force and args.smoke_material_action == 'remove' and
                        not remove.get_sensitive()):
                    self.finish(False, 'GTK material editor disabled removal for an unused material')
                    return
                self.property_smoke_index = index
                if boundary:
                    expected = None if args.smoke_boundary_action == 'release' else (items[index]['Value'] or 0) + (0.01 if args.smoke_boundary_action in ('edit', 'add') else 0)
                    if free:
                        free.set_active(expected is None)
                    if create:
                        boundary_name.set_text('added-displacement')
                else:
                    expected = (items[index][keys[0]] if not force and args.smoke_material_action == 'density'
                                else items[index][keys[0]] * 2 + (1 if force else 0))
                    if create:
                        boundary_name.set_text('added-traction' if force and args.smoke_load_action == 'add-traction' else 'added-force')
                if ((boundary and args.smoke_boundary_action == 'remove') or (force and args.smoke_load_action == 'remove') or
                        (not boundary and not force and args.smoke_material_action == 'remove')):
                    self.removed_smoke_identity = ((items[index]['Step'], items[index]['Name'])
                                                   if boundary or force else items[index]['Name'])
                    remove.set_active(True)
                entries[0].set_text(str(expected or 0))
                self.material_smoke_expected = expected
                if not boundary and not force:
                    self.material_smoke_density_expected = 1 if args.smoke_material_action == 'density' else items[index]['Density']
                    entries[2].set_text('1' if args.smoke_material_action == 'density' else
                                        '' if items[index]['Density'] is None else str(items[index]['Density']))
                dialog.emit('response', Gtk.ResponseType.ACCEPT)
        self.submit({'command': collection}, listed)

    def save_edit(self, change, smoke_path=None):
        importing = change.get('command') == 'import-mesh'
        if not importing and not smoke_path:
            self.dialog = None
            self.apply_session_edit(change)
            return
        self.dialog = Gtk.FileChooserNative.new('Save imported mesh as a new project' if importing else 'Save edited project as a new file', self.window,
            Gtk.FileChooserAction.SAVE, '_Save', '_Cancel')
        self.dialog.set_modal(True)
        self.dialog.set_current_name('imported-project.pma' if importing else 'edited-project.pma')
        def destination(chooser, answer):
            path = chooser.get_filename() if answer == Gtk.ResponseType.ACCEPT else None
            chooser.destroy()
            self.dialog = None
            if not path:
                return
            self.opening = True
            def saved(result):
                self.opening = False
                if importing and args.smoke_import_failure:
                    self.finish(not result.get('ok') and (self.scene['SceneId'] if self.scene else None) == self.import_smoke_scene,
                                'PASS: refused mesh import preserves current scene' if not result.get('ok') else 'Import unexpectedly succeeded')
                    return
                if result.get('ok'):
                    self.material_smoke_stage = 2
                    self.open_project(path)
                else:
                    self.label.set_text('Mesh import failed. Use a supported mesh-only .inp or tetrahedral .mesh file, valid material values and a new filename.' if importing else result.get('error', 'Could not save edit. Choose valid values and a new filename.'))
                    if smoke_path:
                        self.finish(False, 'GTK property save failed')
            self.submit({**change, 'path': path}, saved)
        self.dialog.connect('response', destination)
        self.dialog.show()
        if smoke_path:
            output = Path(smoke_path).resolve()
            self.dialog.set_current_folder(str(output.parent))
            self.dialog.set_current_name(output.name)
            def save_smoke():
                self.dialog.emit('response', Gtk.ResponseType.ACCEPT)
                return False
            GLib.timeout_add(500, save_smoke)

    def choose_project(self):
        if self.opening or self.solving or self.dialog:
            return
        self.dialog_focus = self.window.get_focus()
        self.dialog = Gtk.FileChooserNative.new('Open editable project', self.window,
            Gtk.FileChooserAction.OPEN, '_Open', '_Cancel')
        self.dialog.set_modal(True)
        file_filter = Gtk.FileFilter()
        file_filter.set_name('PrePoMax projects (*.pma, *.pmx)')
        file_filter.add_pattern('*.pma')
        file_filter.add_pattern('*.PMA')
        file_filter.add_pattern('*.pmx')
        file_filter.add_pattern('*.PMX')
        self.dialog.add_filter(file_filter)
        self.dialog.connect('response', self.chosen)
        self.dialog.show()
        if args.smoke_import_pmx and self.pmx_smoke_stage == 1:
            self.dialog.set_filename(str(Path(args.smoke_import_pmx).resolve()))
            GLib.timeout_add(500, lambda: self.dialog.emit('response', Gtk.ResponseType.ACCEPT) is None and False)

    def chosen(self, dialog, response):
        path = dialog.get_filename() if response == Gtk.ResponseType.ACCEPT else None
        dialog.destroy()
        self.dialog = None
        target, self.dialog_focus = self.dialog_focus, None
        if path:
            if Path(path).suffix.lower() == '.pmx':
                self.choose_pmx_destination(path)
            else:
                self.open_project(path)
        elif target is not None and target.is_sensitive() and target.get_visible():
            # Preserve the page's DOM focus when returning from a cancelled chooser.
            self.window.present()
            target.grab_focus()

    def choose_pmx_destination(self, source):
        self.dialog = Gtk.FileChooserNative.new('Convert legacy project to a new portable project', self.window,
            Gtk.FileChooserAction.SAVE, '_Convert', '_Cancel')
        self.dialog.set_modal(True)
        self.dialog.set_current_name(Path(source).stem + '.pma')
        file_filter = Gtk.FileFilter()
        file_filter.set_name('Portable project (*.pma)')
        file_filter.add_pattern('*.pma')
        self.dialog.add_filter(file_filter)
        def destination(chooser, response):
            path = chooser.get_filename() if response == Gtk.ResponseType.ACCEPT else None
            chooser.destroy()
            self.dialog = None
            if path:
                self.convert_pmx(source, path)
        self.dialog.connect('response', destination)
        self.dialog.show()
        if args.smoke_import_pmx and self.pmx_smoke_stage == 1:
            output = Path(args.smoke_pmx_output).resolve()
            self.dialog.set_current_folder(str(output.parent))
            self.dialog.set_current_name(output.name)
            GLib.timeout_add(500, lambda: self.dialog.emit('response', Gtk.ResponseType.ACCEPT) is None and False)

    def convert_pmx(self, source, destination):
        if Path(destination).suffix.lower() != '.pma':
            self.label.set_text('Choose a new filename ending in .pma for the converted project.')
            if args.smoke_import_pmx:
                self.finish(False, 'GTK PMX conversion accepted a non-PMA destination')
            return
        self.opening = True
        self.deferred_ready = None
        self.open_button.set_sensitive(False)
        self.solve_button.set_sensitive(False)
        self.result_button.set_sensitive(False)
        self.label.set_text('Converting legacy project… This can take several minutes for a large model.')
        def run_conversion():
            command = [str(Path(args.dotnet).resolve()), str(converter), str(Path(source).resolve()),
                       '--convert', str(Path(destination).resolve())]
            child = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                     text=True, start_new_session=True)
            self.conversion_process = child
            try:
                stdout, stderr = child.communicate(timeout=900)
                return child.returncode, stdout, stderr
            except subprocess.TimeoutExpired:
                os.killpg(child.pid, signal.SIGKILL)
                stdout, stderr = child.communicate()
                return 124, stdout, stderr
            finally:
                self.conversion_process = None
        future = worker.submit(run_conversion)
        def completed(done):
            def deliver():
                if self.closed:
                    return False
                self.opening = False
                try:
                    returncode, _stdout, _stderr = done.result()
                except Exception:
                    returncode = 1
                if args.smoke_pmx_failure:
                    unchanged = (self.scene['SceneId'] if self.scene else None) == self.pmx_smoke_scene
                    self.finish(returncode != 0 and unchanged and not Path(destination).exists(),
                        'PASS: refused PMX conversion preserves current scene and publishes no PMA')
                    return False
                if returncode == 0 and Path(destination).is_file():
                    if args.smoke_import_pmx:
                        self.pmx_smoke_stage = 2
                    self.open_project(destination)
                else:
                    self.open_button.set_sensitive(True)
                    self.solve_button.set_sensitive(self.can_solve and self.scene is not None)
                    self.label.set_text('Legacy project conversion failed. The PMX may be protected, too old, unsupported, or the destination may already exist.')
                    if args.smoke_import_pmx:
                        self.finish(False, 'GTK PMX conversion failed')
                return False
            GLib.idle_add(deliver)
        future.add_done_callback(completed)

    def open_project(self, path, after=None, retain_session=False, discard_approved=False):
        if self.opening or self.solving or self.node_selection is not None:
            return
        if not retain_session and not discard_approved and self.session and self.session.dirty:
            self.guard_unsaved(lambda: self.open_project(path, after, discard_approved=True))
            return
        self.opening = True
        self.picked_surface_faces = []
        self.deferred_ready = None
        self.generation += 1  # Drop bootstrap callbacks belonging to the previous model.
        self.tree_project_generation += 1
        self.open_button.set_sensitive(False)
        self.solve_button.set_sensitive(False)
        self.result_button.set_sensitive(False)
        self.label.set_text('Opening project…')
        def opened(reply):
            self.opening = False
            self.open_button.set_sensitive(True)
            if reply.get('ok'):
                self.can_solve = bool(reply.get('canSolve'))
            self.solve_button.set_sensitive(self.can_solve and (bool(reply.get('ok')) or self.scene is not None))
            if reply['ok']:
                self.scene = reply['scene']
                self.geometry_source = reply.get('geometry')
                self.meshing_setup = reply.get('meshingSetup')
                self.cad_topology = reply.get('cadTopology')
                self.displayed_field = None
                self.displayed_scale = 0
                self.label.set_text(Path(path).name)
                if not retain_session:
                    self.start_session(path)
                self.refresh_model_tree()
                if self.view.get_uri() == self.address:
                    self.view.reload()
                else:
                    self.view.load_uri(self.address)
                self.refresh_fields()
            else:
                self.label.set_text('Could not complete project opening.')
                if self.scene is not None:
                    self.refresh_model_tree()
                else:
                    self.tree_pending = False
            if after:
                after(reply)
        self.submit({'command': 'open', 'path': path}, opened)

    def refresh_fields(self):
        self.fields_loading = True
        self.field_combo.set_sensitive(False)
        def listed(reply):
            self.fields_loading = False
            if self.opening:
                return
            self.fields = reply.get('fields', [])
            self.updating_fields = True
            self.field_combo.remove_all()
            self.field_combo.append_text('Mesh geometry')
            for field in self.fields:
                self.field_combo.append_text('%s / %s — step %s, increment %s, time %g' % (
                    field['Name'], field['Component'], field['Step'], field['Increment'], field['Time']))
            self.field_combo.set_active(0)
            self.updating_fields = False
            self.field_combo.set_sensitive(bool(self.fields) and not self.solving)
            if not reply['ok']:
                self.label.set_text('Archived results could not be loaded for this mesh.')
        self.submit({'command': 'fields'}, listed)

    def field_changed(self, combo):
        if self.updating_fields or self.opening or self.solving or self.node_selection is not None:
            return
        index = combo.get_active()
        if index < 0:
            return
        field = self.fields[index - 1] if index else None
        scale = self.scale_control.get_value() if field else 0
        self.opening = True
        self.generation += 1
        self.open_button.set_sensitive(False)
        self.solve_button.set_sensitive(False)
        self.field_combo.set_sensitive(False)
        self.label.set_text('Loading result field…' if field else 'Loading mesh…')
        def selected(reply):
            self.opening = False
            self.open_button.set_sensitive(True)
            self.solve_button.set_sensitive(self.can_solve)
            self.field_combo.set_sensitive(bool(self.fields))
            if reply['ok']:
                self.scene = reply['scene']
                self.displayed_field = dict(field) if field else None
                self.displayed_scale = scale
                self.label.set_text(('Displacement scale %g' % scale) if field else 'Mesh geometry')
                self.view.reload()
            else:
                self.label.set_text('Could not display the selected result field.')
                if args.smoke_field:
                    self.finish(False, 'GTK result field selection failed')
        self.submit({'command': 'field', 'field': field, 'deformationScale': scale}, selected)

    def solve(self):
        if self.scene is None or not self.can_solve or self.opening or self.solving or self.node_selection is not None:
            return
        self.solving = True
        self.job_kind = 'solve'
        self.job_pending = True
        self.result_path = None
        self.result_button.set_sensitive(False)
        self.field_combo.set_sensitive(False)
        self.solve_button.set_sensitive(False)
        self.open_button.set_sensitive(False)
        self.cancel_button.set_sensitive(True)
        self.label.set_text('Preparing analysis…')
        self.submit({'command': 'solve', 'jobRoot': str(Path(args.job_root).resolve()), 'solver': args.solver}, self.job_updated)

    def cancel_job(self):
        if self.solving:
            self.cancel_button.set_sensitive(False)
            meshing = self.job_kind in ('mesh', 'remesh')
            self.label.set_text('Cancelling meshing…' if meshing else 'Cancelling analysis…')
            self.submit({'command': 'cancel-mesh' if meshing else 'cancel'}, lambda _reply: None)

    def mesh_job_updated(self, reply):
        self.job_pending = False
        if not reply.get('ok'):
            self.solving = False
            self.job_kind = None
            self.open_button.set_sensitive(True)
            self.cancel_button.set_sensitive(False)
            self.cancel_button.set_label('Cancel analysis')
            self.cancel_button.get_accessible().set_name('Cancel analysis')
            self.label.set_text(reply.get('error') or 'Unable to start Gmsh meshing.')
            if args.smoke_mesh_geometry or args.smoke_remesh_output:
                self.finish(False, 'GTK Gmsh job could not start: ' + (reply.get('error') or 'unknown error'))
            return
        job = reply['job']
        state = job['status']
        self.label.set_text('Gmsh %s — %.1f s' % (state, job['elapsedSeconds']))
        self.label.set_tooltip_text(job.get('sourcePath'))
        if job['active']:
            return
        self.solving = False
        self.job_kind = None
        self.open_button.set_sensitive(True)
        self.cancel_button.set_sensitive(False)
        self.cancel_button.set_label('Cancel analysis')
        self.cancel_button.get_accessible().set_name('Cancel analysis')
        project = job.get('projectPath')
        if state == 'Succeeded' and project:
            if args.smoke_mesh_geometry:
                self.gmsh_smoke_stage = 2
            if args.smoke_remesh_output:
                self.remesh_smoke_stage = 2
            self.open_project(project)
        else:
            self.solve_button.set_sensitive(self.can_solve and self.scene is not None)
            self.field_combo.set_sensitive(bool(self.fields))
            self.label.set_text(job.get('error') or 'Gmsh meshing did not complete: ' + state)
            if args.smoke_mesh_geometry or args.smoke_remesh_output:
                self.finish(False, 'GTK Gmsh acceptance: ' + state)

    def job_updated(self, reply):
        self.job_pending = False
        job = reply['job']
        state = job['status']
        self.label.set_text('%s — %.1f s' % (state, job['elapsedSeconds']))
        self.label.set_tooltip_text(job.get('jobDirectory'))
        if args.smoke_cancel and state == 'Running' and not self.cancel_sent:
            self.cancel_sent = True
            self.cancel_job()
        if job['active']:
            return
        self.solving = False
        self.job_kind = None
        self.field_combo.set_sensitive(bool(self.fields))
        self.solve_button.set_sensitive(self.can_solve)
        self.open_button.set_sensitive(True)
        self.cancel_button.set_sensitive(False)
        self.cancel_button.set_label('Cancel analysis')
        self.cancel_button.get_accessible().set_name('Cancel analysis')
        self.result_path = job.get('archivePath')
        self.result_button.set_sensitive(bool(self.result_path))
        if state == 'Failed':
            self.label.set_text(job['error'])
        if self.solve_smoke_started:
            expected = 'Cancelled' if args.smoke_cancel else 'Succeeded'
            passed = state == expected and (args.smoke_cancel or self.result_path and Path(self.result_path).is_file())
            if passed and not args.smoke_cancel:
                self.result_smoke_pending = True
                self.result_button.clicked()
            else:
                self.finish(bool(passed), 'GTK solve acceptance: ' + state)

    def policy(self, view, decision, kind):
        if kind in (WebKit2.PolicyDecisionType.NAVIGATION_ACTION, WebKit2.PolicyDecisionType.NEW_WINDOW_ACTION):
            if kind == WebKit2.PolicyDecisionType.NEW_WINDOW_ACTION or decision.get_navigation_action().get_request().get_uri() != self.address:
                decision.ignore()
                return True
        return False

    def mark_view_failed(self, message):
        if self.closed:
            return
        self.view_failed = True
        self.paused_view_remaining = None
        self.view_deadline = None
        self.generation += 1
        self.busy = False
        self.deferred_ready = None
        self.view_error.set_text(message)
        self.retry_button.set_sensitive(bool(self.address))

    def web_process_terminated(self, view, reason):
        self.mark_view_failed('The display process stopped. Retry to restore the current scene.')
        if args.smoke_recovery and self.recovery_stage == 1:
            self.recovery_stage = 2
            def retry():
                self.retry_button.clicked()
                return False
            GLib.timeout_add(250, retry)

    def load_failed(self, view, event, uri, error):
        if error.matches(WebKit2.network_error_quark(), WebKit2.NetworkError.CANCELLED):
            return True
        self.mark_view_failed('The scene page could not be loaded. Retry the view.')
        return True  # Keep the application recovery message instead of a browser error document.

    def retry_view(self):
        if not self.address or self.closed or self.opening:
            return
        self.view_failed = False
        self.generation += 1
        self.busy = False
        self.view_error.set_text('Restoring scene…')
        self.retry_button.set_sensitive(False)
        self.view.load_uri(self.address)

    def loading(self, view, event):
        self.load_stage = event.value_nick
        if event == WebKit2.LoadEvent.FINISHED:
            self.apply_view_layout()
        if event == WebKit2.LoadEvent.STARTED:
            self.stable_view_generation = None
            self.paused_view_remaining = args.view_timeout if self.native_visibility == 'fully-obscured' else None
            self.view_deadline = None if self.paused_view_remaining is not None else time.monotonic() + args.view_timeout
            self.generation += 1
            self.view_failed = False
            self.retry_button.set_sensitive(False)

    def bootstrap(self, message):
        if self.opening:
            return
        if self.bootstrap_pending:
            self.deferred_ready = message
            return
        generation = self.generation
        self.bootstrap_pending = True
        def received(reply):
            self.bootstrap_pending = False
            if reply['bootstrap'] and generation == self.generation and self.view.get_uri() == self.address and not self.opening and not self.view_failed:
                if args.smoke_startup_recovery and self.startup_recovery_stage == 0:
                    self.startup_recovery_stage = 1
                    self.startup_scene = self.scene['SceneId']
                    return  # Fault injection: leave this page waiting for bootstrap.
                self.bootstrap_count += 1
                script = self.visibility_script() + 'window.__gtkDeliver(' + reply['bootstrap'] + ')'
                if args.smoke_frame_recovery and self.frame_recovery_stage == 0:
                    self.frame_recovery_stage = 1
                    self.frame_recovery_scene = self.scene['SceneId']
                    script = 'window.__gtkBlockFrames = true; ' + script
                if args.smoke_bootstrap_recovery and self.bootstrap_recovery_stage == 0:
                    self.bootstrap_recovery_stage = 1
                    self.bootstrap_recovery_scene = self.scene['SceneId']
                    script = '(() => { throw new Error("Injected handoff failure"); })()'
                self.view.evaluate_javascript(script, -1, None, None, None, self.bootstrap_delivered, generation)
            if self.deferred_ready:
                deferred = self.deferred_ready
                self.deferred_ready = None
                self.bootstrap(deferred)
        self.submit({'source': self.view.get_uri(), 'topLevel': True, 'message': message}, received)

    def bootstrap_delivered(self, view, result, generation):
        try:
            value = view.evaluate_javascript_finish(result)
            delivered = value.is_number() and value.to_int32() > 0
        except GLib.Error:
            delivered = False
        if self.closed or generation != self.generation or self.opening or view.get_uri() != self.address:
            return  # An old document must not fail the current scene.
        if delivered:
            self.bootstrap_confirmed += 1
            return
        self.mark_view_failed('The scene could not be handed to the display. Retry the view.')
        if args.smoke_bootstrap_recovery and self.bootstrap_recovery_stage == 1:
            if not self.retry_button.get_sensitive():
                self.finish(False, 'GTK handoff failure did not enable Retry view')
                return
            self.bootstrap_recovery_stage = 2
            self.retry_button.clicked()

    def evaluated(self, view, result, generation):
        try:
            value = view.evaluate_javascript_finish(result)
            if generation != self.generation or self.opening or view.get_uri() != self.address:
                return
            self.evaluation_count += 1
            payload = json.loads(value.to_string())
            self.page_state = {key: bool(payload.get('state', {}).get(key)) for key in
                               ('visible', 'frame', 'errors', 'errorBanner', 'canvas', 'sceneApplied', 'dropped', 'resourcesComplete', 'pressured')}
            if args.smoke_occlusion and self.occlusion_stage == 1 and self.page_state.get('resourcesComplete'):
                self.occlusion_stage = 2
                print('GTK occlusion: resources complete; holding cover for 12 seconds', flush=True)
                self.occlusion_generation = self.generation
                def uncover():
                    if self.closed:
                        return False
                    if self.view_failed or self.page_state.get('sceneApplied') or self.native_visibility != 'fully-obscured':
                        self.finish(False, 'GTK occlusion did not preserve a pending scene while covered')
                        return False
                    self.occlusion_stage = 3
                    print('GTK occlusion: removing cover without reload', flush=True)
                    self.occlusion_cover.destroy()
                    self.occlusion_cover = None
                    self.window.present()
                    return False
                GLib.timeout_add(12000, uncover)
            stage = payload.get('state', {}).get('stage')
            self.page_state['stage'] = stage if type(stage) is int and -1 <= stage <= 2 else -1
            try:
                diagnostic = json.loads(payload['diagnostics'])
                self.page_state['geometry'] = bool(diagnostic.get('renderer', {}).get('pipelinePoints'))
                self.page_state['manifest'] = bool(diagnostic.get('actors'))
                self.page_state['transferred'] = bool(diagnostic.get('transferredBytes'))
                if diagnostic.get('recovery', {}).get('lastSafeErrorCode') == 'frame-timeout':
                    self.mark_view_failed('The display did not complete a frame. Retry the view.')
                    if args.smoke_frame_recovery and self.frame_recovery_stage == 1:
                        self.frame_recovery_stage = 2
                        self.retry_button.clicked()
                    return
            except (ValueError, TypeError, AttributeError):
                pass
            for message in payload['messages']:
                item = json.loads(message)
                if item.get('type') == 'prepomax.clipboard.copy-summary-v1':
                    self.copy_summary()
                elif item.get('type') == 'prepomax.pick.validated-v1':
                    self.accept_face_pick(item)
                elif item.get('type') == 'prepomax.area.validated-v1':
                    self.accept_area_pick(item)
                elif item.get('type') == 'prepomax.node-selection.frustum-v1':
                    self.accept_node_frustum(item)
                elif item.get('type') == 'prepomax.client.ready-v1':
                    self.ready_count += 1
                    self.bootstrap(message)
                elif item.get('type') == 'prepomax.scene.applied-v1' and item.get('sceneId') == self.scene['SceneId']:
                    renderer = json.loads(payload['diagnostics'])['renderer']
                    self.validate_gpu(renderer)
                    if (item.get('actorCount'), renderer['pipelinePoints'], renderer['pipelinePolys']) != (
                            self.scene['Actors'], self.scene['Points'], self.scene['Triangles']):
                        raise RuntimeError('GTK topology mismatch')
                    print('GTK stable frame: actors=%d points=%d triangles=%d' % (
                        self.scene['Actors'], self.scene['Points'], self.scene['Triangles']), flush=True)
                    self.stable_view_generation = self.generation
                    self.stable()
        except GLib.Error:
            pass  # Navigation can invalidate an in-flight evaluation.
        except Exception:
            self.finish(False, 'GTK bootstrap or rendering failed')
        finally:
            self.busy = False

    def validate_gpu(self, renderer):
        if not args.smoke_gpu:
            return
        identity = renderer.get('webglRenderer')
        if not renderer.get('webgl2Available') or not isinstance(identity, str) or not identity.strip():
            raise RuntimeError('WebGL 2 hardware identity is unavailable')
        normalized = identity.casefold()
        if any(name in normalized for name in ('swiftshader', 'llvmpipe', 'lavapipe', 'software rasterizer')):
            raise RuntimeError('WebGL is using a software renderer')
        render_nodes = descendant_render_nodes(os.getpid())
        if not render_nodes:
            raise RuntimeError('No WebKit descendant has an open DRM render node')
        if not self.gpu_reported:
            vendor = renderer.get('webglVendor')
            safe_vendor = ''.join(character if character.isprintable() else ' ' for character in str(vendor or 'unknown'))[:160]
            safe_identity = ''.join(character if character.isprintable() else ' ' for character in identity)[:160]
            print('GTK hardware WebGL 2: vendor=%s renderer=%s drm=%s' % (
                safe_vendor, safe_identity, ','.join(describe_render_node(node) for node in render_nodes)), flush=True)
            self.gpu_reported = True

    def copy_summary(self):
        if self.closed or self.opening or self.view_failed or not self.scene or self.view.get_uri() != self.address:
            return
        # Construct from native state; never copy arbitrary page payloads, paths or credentials.
        summary = {'schema': 'prepomax.scene-summary-v1', 'sceneId': self.scene['SceneId'],
                   'actors': self.scene['Actors'], 'points': self.scene['Points'],
                   'triangles': self.scene['Triangles'], 'field': self.displayed_field,
                   'displacementScale': self.displayed_scale}
        text = json.dumps(summary, indent=2, ensure_ascii=False)
        selection = Gdk.Atom.intern('PREPOMAX_TEST_CLIPBOARD_' + str(os.getpid()), False) if args.smoke_clipboard else Gdk.SELECTION_CLIPBOARD
        clipboard = Gtk.Clipboard.get(selection)
        clipboard.set_text(text, -1)
        self.view_error.set_text('Scene summary copied.')
        if args.smoke_clipboard:
            def received(_clipboard, copied, _data):
                try:
                    value = json.loads(copied)
                    ok = value == summary and 'accessToken' not in copied and 'bootstrap' not in copied
                    if args.smoke_field:
                        field = value.get('field') or {}
                        ok = ok and field.get('Name', '') + '/' + field.get('Component', '') == args.smoke_field
                        ok = ok and value['displacementScale'] == args.deformation_scale
                    self.finish(ok, 'PASS: GTK Copy summary native clipboard round trip (private selection)')
                except Exception:
                    self.finish(False, 'GTK clipboard round trip failed')
            clipboard.request_text(received, None)
        else:
            clipboard.store()

    def check_accessibility(self):
        self.window.present()
        self.open_button.grab_focus()
        # Allow GTK to propagate focus through parent containers before traversal.
        GLib.timeout_add(100, self.check_accessibility_focused)

    def check_accessibility_focused(self):
        try:
            for widget, expected in ((self.open_button, 'Open project'), (self.field_combo, 'Result field and frame'),
                                     (self.scale_control, 'Displacement scale'), (self.view, 'Engineering scene')):
                if widget.get_accessible().get_name() != expected:
                    raise RuntimeError('Accessible name mismatch')
            if self.window.get_focus() != self.open_button or not self.open_button.has_focus():
                raise RuntimeError('Native focus unavailable; run this smoke alone in an active desktop session')
            self.window.get_child().child_focus(Gtk.DirectionType.TAB_FORWARD)
            if self.window.get_focus() in (None, self.open_button):
                raise RuntimeError('Tab did not advance focus')
            sensitive = self.solve_button.get_sensitive()
            self.solve_button.set_sensitive(False)
            key, modifiers = Gtk.accelerator_parse('<Control>Return')
            Gtk.accel_groups_activate(self.window, key, modifiers)
            if self.solving:
                raise RuntimeError('Disabled Solve shortcut started a job')
            self.solve_button.set_sensitive(sensitive)
            key, modifiers = Gtk.accelerator_parse('<Control>o')
            if not Gtk.accel_groups_activate(self.window, key, modifiers) or not self.dialog:
                raise RuntimeError('Open shortcut did not open native chooser')
            self.dialog.emit('response', Gtk.ResponseType.CANCEL)
            if self.dialog is not None:
                raise RuntimeError('Native chooser did not cancel')
            scale = self.window.get_scale_factor()
            if scale != int(os.environ.get('GDK_SCALE', '1')):
                raise RuntimeError('Unexpected native display scale')
            for widget in (self.open_button, self.solve_button, self.field_combo, self.scale_control):
                if widget.get_allocated_width() <= 0 or widget.get_allocated_height() <= 0:
                    raise RuntimeError('Control has no layout area')
            self.finish(True, 'PASS: GTK accessible names, Tab focus, guarded shortcuts, native dialog and display scale ' + str(scale))
        except Exception as error:
            self.finish(False, 'GTK keyboard/accessibility acceptance failed: ' + str(error))

    def stable(self):
        self.highlight_selected_node_set()
        if self.selection_preview_done is not None:
            callback, self.selection_preview_done = self.selection_preview_done, None
            callback()
            return
        if args.smoke_tree_highlight:
            if not hasattr(self,'tree_highlight_smoke_stage'):
                self.tree_highlight_smoke_stage = 0
                self.tree_highlight_smoke_busy = False
                GLib.timeout_add(100,self.check_tree_highlight_workflow)
            return
        if args.smoke_node_selection:
            if not hasattr(self, 'node_smoke_stage'):
                self.node_smoke_stage = 0
                self.node_smoke_original = Path(args.project).read_bytes()
                GLib.timeout_add(150, self.check_node_selection_workflow)
            return
        if args.smoke_region_maintenance:
            self.check_region_maintenance()
            return
        if args.smoke_element_sets:
            self.check_element_set_workflow()
            return
        if args.smoke_edit_session:
            self.check_edit_session()
            return
        if args.smoke_model_tree:
            if self.tree_pending:
                GLib.timeout_add(100, lambda: self.stable() is None and False)
            elif self.tree_smoke_stage == 0:
                self.tree_smoke_stage = 1
                regions = [node for node in self.tree_nodes.values() if node['Kind'] == 'node-set']
                if not regions or not any(node['Kind'] == 'field-output' for node in self.tree_nodes.values()):
                    self.finish(False, 'Model tree fixture needs a node set and a field request')
                    return
                target = regions[0]
                path = self.tree_store.get_path(self.tree_iters[target['Key']])
                self.model_tree.expand_to_path(path)
                self.model_tree.get_selection().select_path(path)
                self.tree_preview_button.clicked()
            return
        if args.smoke_history_results:
            if not self.history_result_smoke_started:
                self.history_result_smoke_started = True
                self.edit_history_results()
            return
        if args.smoke_plasticity:
            if self.plasticity_smoke_stage == 0:
                self.plasticity_smoke_stage = 1
                self.edit_plasticity()
            elif self.plasticity_smoke_stage == 1 and self.material_smoke_stage == 2:
                self.plasticity_smoke_stage = 2
                def checked(reply):
                    matches = [item for item in reply.get('materials', [])
                               if item['Name'] == self.plasticity_smoke_expected['Name']]
                    if not reply.get('ok') or matches != [self.plasticity_smoke_expected]:
                        self.finish(False, 'Reopened plasticity differs from the requested operation')
                        return
                    self.submit({'command': 'fields'}, lambda fields: self.finish(
                        fields.get('ok') and not fields.get('fields'),
                        'PASS: GTK plasticity ' + args.smoke_plasticity_action + ', save/reopen and result invalidation'))
                self.submit({'command': 'plasticity-data'}, checked)
            return
        if args.smoke_history_output:
            if self.history_output_smoke_stage == 0:
                self.history_output_smoke_stage = 1
                self.edit_history_outputs()
            elif self.history_output_smoke_stage == 1 and self.material_smoke_stage == 2:
                self.history_output_smoke_stage = 2
                def checked(reply):
                    if not reply.get('ok') or reply.get('outputs') != self.history_output_smoke_expected:
                        self.finish(False, 'Reopened history-output definitions differ from the requested operation'); return
                    self.submit({'command': 'fields'}, lambda fields: self.finish(fields.get('ok') and not fields.get('fields'),
                        'PASS: GTK history output ' + args.smoke_history_output_action + ', save/reopen and result invalidation'))
                self.submit({'command': 'history-output-data'}, checked)
            return
        if args.smoke_field_output:
            if self.field_output_smoke_stage == 0:
                self.field_output_smoke_stage = 1
                self.edit_field_outputs()
            elif self.field_output_smoke_stage == 1 and self.material_smoke_stage == 2:
                self.field_output_smoke_stage = 2
                def checked(reply):
                    if not reply.get('ok') or reply.get('outputs') != self.field_output_smoke_expected:
                        self.finish(False, 'Reopened field-output definitions differ from the requested operation'); return
                    self.submit({'command': 'fields'}, lambda fields: self.finish(fields.get('ok') and not fields.get('fields'),
                        'PASS: GTK field output ' + args.smoke_field_output_action + ', save/reopen and result invalidation'))
                self.submit({'command': 'field-output-data'}, checked)
            return
        if args.smoke_section:
            if self.section_smoke_stage == 0:
                self.section_smoke_stage = 1
                self.edit_sections()
            elif self.section_smoke_stage == 1 and self.material_smoke_stage == 2:
                self.section_smoke_stage = 2
                def checked(reply):
                    def comparable(items):
                        return [{key: value for key, value in item.items() if key != 'EffectiveElements'} for item in items]
                    if not reply.get('ok') or comparable(reply.get('sections', [])) != comparable(self.section_smoke_expected):
                        self.finish(False, 'Reopened solid sections differ from the requested operation'); return
                    if not reply['coverage']['CanSolve'] and self.solve_button.get_sensitive():
                        self.finish(False, 'Solve is enabled with incomplete section assignment'); return
                    self.submit({'command': 'fields'}, lambda fields: self.finish(fields.get('ok') and not fields.get('fields'),
                        'PASS: GTK solid section ' + args.smoke_section_action + ', save/reopen, coverage and result invalidation'))
                self.submit({'command': 'section-data'}, checked)
            return
        if args.smoke_step:
            if self.step_smoke_stage == 0:
                self.step_smoke_stage = 1
                self.edit_steps()
            elif self.step_smoke_stage == 1 and self.material_smoke_stage == 2:
                self.step_smoke_stage = 2
                def checked(reply):
                    if not reply.get('ok') or reply.get('steps') != self.step_smoke_expected:
                        self.finish(False, 'Reopened static step definitions differ from the requested operation')
                        return
                    if not any(step['Active'] for step in reply['steps']) and self.solve_button.get_sensitive():
                        self.finish(False, 'Solve remains enabled without an active analysis step')
                        return
                    self.submit({'command': 'fields'}, lambda fields: self.finish(
                        fields.get('ok') and not fields.get('fields'),
                        'PASS: GTK static step ' + args.smoke_step_action + ', save/reopen and result invalidation'))
                self.submit({'command': 'step-data'}, checked)
            return
        if args.smoke_cad_topology:
            if self.cad_topology:
                self.inspect_cad_topology()
            else:
                self.finish(False, 'Current project has no retained CAD topology')
            return
        if args.smoke_mesh_geometry:
            if self.gmsh_smoke_stage == 0:
                self.gmsh_smoke_stage = 1
                self.mesh_geometry()
            elif self.gmsh_smoke_stage == 2:
                self.gmsh_smoke_stage = 3
                output = Path(args.smoke_mesh_output).resolve()
                def materials_checked(reply):
                    materials = reply.get('materials', [])
                    ok = (output.is_file() and self.scene is not None and self.scene.get('Actors', 0) > 0 and
                          self.scene.get('Points', 0) > 0 and self.scene.get('Triangles', 0) > 0 and
                          self.geometry_source is not None and
                          self.geometry_source.get('FileName') == Path(args.smoke_mesh_geometry).name and
                          self.meshing_setup == {'MaximumElementSize': .35, 'MinimumElementSize': None,
                                                 'Algorithm': 'Delaunay', 'Optimize': False, 'SecondOrder': args.smoke_second_order,
                                                 **({'SizeBox': SMOKE_SIZE_BOX} if args.smoke_size_box else {})} and
                          reply.get('ok') and len(materials) == 1 and
                          materials[0].get('YoungsModulus') == 1000 and materials[0].get('PoissonsRatio') == .25)
                    def surfaces_checked(regions):
                        surfaces = regions.get('surfaces', [])
                        regions_ok = (regions.get('ok') and len(surfaces) == 6 and
                                      all(surface.get('Name', '').startswith('CAD_FACE_') for surface in surfaces))
                        self.submit({'command': 'fields'}, lambda fields: self.finish(
                            ok and regions_ok and fields.get('ok') and not fields.get('fields'),
                            'PASS: GTK STEP chooser, retained CAD, Gmsh regions, PMA publication, reopen and renderer scene'))
                    self.submit({'command': 'surface-data'}, surfaces_checked)
                self.submit({'command': 'materials'}, materials_checked)
            return
        if args.smoke_remesh_output:
            if self.remesh_smoke_stage == 0:
                self.remesh_smoke_stage = 1
                self.remesh_geometry()
            elif self.remesh_smoke_stage == 2:
                self.remesh_smoke_stage = 3
                output = Path(args.smoke_remesh_output).resolve()
                def materials_checked(reply):
                    materials = reply.get('materials', [])
                    ok = (output.is_file() and self.scene is not None and self.scene.get('Actors', 0) > 0 and
                          self.scene.get('Points', 0) > 0 and self.scene.get('Triangles', 0) > 0 and
                          self.geometry_source is not None and
                          self.meshing_setup == {'MaximumElementSize': .45, 'MinimumElementSize': .12,
                                                 'Algorithm': 'Delaunay', 'Optimize': True, 'SecondOrder': args.smoke_second_order,
                                                 **({'SizeBox': SMOKE_SIZE_BOX} if args.smoke_size_box else {}),
                                                 **({'FaceSize': SMOKE_FACE_SIZE} if args.smoke_face_size else {})} and
                          reply.get('ok') and len(materials) == 1 and
                          materials[0].get('YoungsModulus') == 1000 and materials[0].get('PoissonsRatio') == .25)
                    def surfaces_checked(regions):
                        self.finish(ok and regions.get('ok') and len(regions.get('surfaces', [])) == 6,
                            'PASS: GTK retained-CAD controls, remesh publication, material transfer, reopen and renderer scene')
                    self.submit({'command': 'surface-data'}, surfaces_checked)
                self.submit({'command': 'materials'}, materials_checked)
            return
        if args.smoke_import_mesh:
            if self.material_smoke_stage == 0:
                self.material_smoke_stage = 1
                self.import_smoke_scene = self.scene['SceneId'] if self.scene else None
                self.import_mesh()
            elif self.material_smoke_stage == 2:
                self.material_smoke_stage = 3
                def materials_checked(reply):
                    material = reply.get('materials', [])
                    if not reply.get('ok') or len(material) != 1 or material[0]['YoungsModulus'] != 1000 or material[0]['PoissonsRatio'] != .25:
                        self.finish(False, 'Imported material mismatch')
                        return
                    self.submit({'command': 'fields'}, lambda result: self.finish(result.get('ok') and not result.get('fields'),
                        'PASS: GTK mesh chooser, material, new-project save, reopen and empty results'))
                self.submit({'command': 'materials'}, materials_checked)
            return
        if args.smoke_import_pmx:
            if self.pmx_smoke_stage == 0:
                self.pmx_smoke_stage = 1
                self.pmx_smoke_scene = self.scene['SceneId'] if self.scene else None
                self.choose_project()
            elif self.pmx_smoke_stage == 2:
                self.pmx_smoke_stage = 3
                output = Path(args.smoke_pmx_output).resolve()
                self.submit({'command': 'fields'}, lambda reply: self.finish(
                    output.is_file() and reply.get('ok') and self.scene is not None,
                    'PASS: GTK PMX chooser, conversion, PMA open and renderer scene'))
            return
        if args.smoke_node_set:
            if self.material_smoke_stage == 0:
                self.material_smoke_stage = 1
                self.add_node_set()
            elif self.material_smoke_stage == 2:
                self.material_smoke_stage = 3
                def checked(reply):
                    found = [item for item in reply.get('sets', []) if item['Name'] == 'created-node-set']
                    if not found or found[0]['Nodes'] != self.node_set_smoke_nodes:
                        self.finish(False, 'Reopened node-set membership mismatch')
                        return
                    self.submit({'command': 'fields'}, lambda result: self.finish(
                        result.get('ok') and not result.get('fields'), 'PASS: GTK node-set creation, save, reopen and result invalidation'))
                self.submit({'command': 'node-sets'}, checked)
            return

        if args.smoke_surface:
            if self.material_smoke_stage == 0:
                self.material_smoke_stage = 1
                if args.smoke_surface_pick or args.smoke_surface_area or args.smoke_surface_angle:
                    self.surface_pick_smoke_pending = True
                    script = """(() => {
                      const viewport = document.querySelector('#viewport');
                      if (!viewport) return false;
                      const bounds = viewport.getBoundingClientRect();
                      viewport.dispatchEvent(new MouseEvent('click', {bubbles: true,
                        clientX: bounds.left + bounds.width / 2, clientY: bounds.top + bounds.height / 2}));
                      return true;
                    })()"""
                    if args.smoke_surface_area:
                        script = """(() => {
                          const viewport = document.querySelector('#viewport');
                          const camera = document.querySelector('#camera-view');
                          if (!viewport || !camera) return false;
                          camera.value = 'isometric';
                          camera.dispatchEvent(new Event('change', {bubbles: true}));
                          requestAnimationFrame(() => requestAnimationFrame(() => {
                            const bounds = viewport.getBoundingClientRect();
                            const startX = bounds.left + bounds.width * 0.95;
                            const startY = bounds.top + bounds.height * 0.95;
                            const endX = bounds.left + bounds.width * 0.05;
                            const endY = bounds.top + bounds.height * 0.05;
                            const options = {bubbles: true, altKey: true, pointerId: 7, pointerType: 'mouse',
                              isPrimary: true, button: 0, buttons: 1};
                            viewport.dispatchEvent(new PointerEvent('pointerdown', {...options, clientX: startX, clientY: startY}));
                            viewport.dispatchEvent(new PointerEvent('pointermove', {...options, clientX: endX, clientY: endY}));
                            viewport.dispatchEvent(new PointerEvent('pointerup', {...options, clientX: endX, clientY: endY, buttons: 0}));
                          }));
                          return true;
                        })()"""
                    elif args.smoke_surface_angle:
                        self.surface_angle.set_value(90)
                        script = """(() => {
                          const viewport = document.querySelector('#viewport');
                          if (!viewport) return false;
                          const bounds = viewport.getBoundingClientRect();
                          const options = {bubbles: true, clientX: bounds.left + bounds.width / 2,
                            clientY: bounds.top + bounds.height / 2, button: 0};
                          viewport.dispatchEvent(new MouseEvent('click', {...options, detail: 1}));
                          viewport.dispatchEvent(new MouseEvent('click', {...options, detail: 2}));
                          return true;
                        })()"""
                    self.view.evaluate_javascript(script, -1, None, None, None, self.surface_pick_dispatched, None)
                else:
                    self.add_surface()
            elif self.material_smoke_stage == 2:
                self.material_smoke_stage = 3
                def surface_checked(reply):
                    found = [item for item in reply.get('surfaces', []) if item['Name'] == 'created-surface']
                    if (not reply.get('ok') or len(found) != 1 or
                            found[0]['FaceCount'] != self.surface_smoke_expected_faces or found[0]['ElementCount'] < 1):
                        self.finish(False, 'Reopened element-surface mismatch')
                        return
                    self.submit({'command': 'fields'}, lambda result: self.finish(
                        result.get('ok') and not result.get('fields'),
                        f'PASS: GTK {self.surface_smoke_expected_faces}-face surface creation, preview, save, reopen and result invalidation'))
                self.submit({'command': 'surface-data'}, surface_checked)
            return

        if args.smoke_pressure:
            if self.material_smoke_stage == 0:
                self.material_smoke_stage = 1
                self.edit_pressure()
            elif self.material_smoke_stage == 2:
                self.material_smoke_stage = 3
                def pressure_checked(reply):
                    found = [item for item in reply.get('pressures', []) if item['Name'] == 'added-pressure']
                    if not reply.get('ok') or len(found) != 1 or found[0]['Region'] != self.pressure_smoke_region or found[0]['Magnitude'] != self.material_smoke_expected:
                        self.finish(False, 'Reopened pressure mismatch')
                        return
                    self.submit({'command': 'fields'}, lambda result: self.finish(
                        result.get('ok') and not result.get('fields'), 'PASS: GTK pressure creation, save, reopen and result invalidation'))
                self.submit({'command': 'pressures'}, pressure_checked)
            return

        if args.smoke_gravity:
            if self.material_smoke_stage == 0:
                self.material_smoke_stage = 1
                self.add_gravity()
            elif self.material_smoke_stage == 2:
                self.material_smoke_stage = 3
                def gravity_checked(reply):
                    step, name, region = self.gravity_smoke_identity
                    found = [item for item in reply.get('loads', [])
                             if (item['Step'], item['Name'], item['Region']) == (step, name, region)]
                    if args.smoke_gravity_action == 'remove':
                        if not reply.get('ok') or found:
                            self.finish(False, 'Removed gravity load is still present')
                            return
                        self.submit({'command': 'fields'}, lambda result: self.finish(
                            result.get('ok') and not result.get('fields'),
                            'PASS: GTK gravity removal, save, reopen and result invalidation'))
                        return
                    if (not reply.get('ok') or len(found) != 1 or
                            [found[0]['A1'], found[0]['A2'], found[0]['A3']] != self.gravity_smoke_acceleration):
                        self.finish(False, 'Reopened gravity-load mismatch')
                        return
                    self.submit({'command': 'fields'}, lambda result: self.finish(
                        result.get('ok') and not result.get('fields'),
                        ('PASS: GTK gravity edit, save, reopen and result invalidation'
                         if args.smoke_gravity_action == 'edit' else
                         'PASS: GTK density-backed gravity creation, save, reopen and result invalidation')))
                self.submit({'command': 'gravity-data'}, gravity_checked)
            return

        if args.smoke_centrifugal:
            if self.material_smoke_stage == 0:
                self.material_smoke_stage = 1
                self.edit_centrifugal()
            elif self.material_smoke_stage == 2:
                self.material_smoke_stage = 3
                def centrifugal_checked(reply):
                    step, name, region = self.centrifugal_smoke_identity
                    found = [item for item in reply.get('loads', [])
                             if (item['Step'], item['Name'], item['Region']) == (step, name, region)]
                    if args.smoke_centrifugal_action == 'remove':
                        if not reply.get('ok') or found:
                            self.finish(False, 'Removed centrifugal load is still present')
                            return
                        self.submit({'command': 'fields'}, lambda result: self.finish(
                            result.get('ok') and not result.get('fields'),
                            'PASS: GTK centrifugal removal, save, reopen and result invalidation'))
                        return
                    keys = ('X', 'Y', 'Z', 'N1', 'N2', 'N3', 'RotationalSpeed')
                    if (not reply.get('ok') or len(found) != 1 or
                            [found[0][key] for key in keys] != self.centrifugal_smoke_values):
                        self.finish(False, 'Reopened centrifugal-load mismatch')
                        return
                    self.submit({'command': 'fields'}, lambda result: self.finish(
                        result.get('ok') and not result.get('fields'),
                        ('PASS: GTK centrifugal edit, save, reopen and result invalidation'
                         if args.smoke_centrifugal_action == 'edit' else
                         'PASS: GTK density-backed centrifugal creation, save, reopen and result invalidation')))
                self.submit({'command': 'centrifugal-data'}, centrifugal_checked)
            return

        if args.smoke_fixed_support:
            if self.material_smoke_stage == 0:
                self.material_smoke_stage = 1
                self.add_fixed_support()
            elif self.material_smoke_stage == 2:
                self.material_smoke_stage = 3
                def fixed_checked(reply):
                    found = [item for item in reply.get('supports', [])
                             if (item['Step'], item['Name'], item['Region']) == self.fixed_support_smoke_identity]
                    if not reply.get('ok') or len(found) != 1:
                        self.finish(False, 'Reopened fixed-support mismatch')
                        return
                    self.submit({'command': 'fields'}, lambda result: self.finish(
                        result.get('ok') and not result.get('fields'),
                        'PASS: GTK fixed-support creation, save, reopen and result invalidation'))
                self.submit({'command': 'fixed-support-data'}, fixed_checked)
            return

        if args.smoke_add_material:
            if self.material_smoke_stage == 0:
                self.material_smoke_stage = 1
                self.add_material()
            elif self.material_smoke_stage == 2:
                self.material_smoke_stage = 3
                def material_checked(reply):
                    section_name, material_name, modulus, ratio = self.created_material_smoke
                    found_material = [item for item in reply.get('materials', []) if item['Name'] == material_name]
                    found_section = [item for item in reply.get('sections', []) if item['Section'] == section_name]
                    if (not reply.get('ok') or len(found_material) != 1 or
                            found_material[0]['YoungsModulus'] != modulus or found_material[0]['PoissonsRatio'] != ratio or
                            section_name is not None and (len(found_section) != 1 or found_section[0]['Material'] != material_name)):
                        self.finish(False, 'Reopened material or section assignment mismatch')
                        return
                    self.submit({'command': 'fields'}, lambda result: self.finish(
                        result.get('ok') and not result.get('fields'),
                        'PASS: GTK elastic-material creation without section assignment, save/reopen and result invalidation' if section_name is None else
                        'PASS: GTK elastic-material creation, solid-section assignment, save, reopen and result invalidation'))
                self.submit({'command': 'material-data'}, material_checked)
            return

        if args.smoke_assign_material:
            if self.material_smoke_stage == 0:
                self.material_smoke_stage = 1
                self.assign_material()
            elif self.material_smoke_stage == 2:
                self.material_smoke_stage = 3
                def assignment_checked(reply):
                    section_name, material_name = self.assigned_material_smoke
                    found = [item for item in reply.get('sections', []) if item['Section'] == section_name]
                    if not reply.get('ok') or len(found) != 1 or found[0]['Material'] != material_name:
                        self.finish(False, 'Reopened solid-section material assignment mismatch')
                        return
                    self.submit({'command': 'fields'}, lambda result: self.finish(
                        result.get('ok') and not result.get('fields'),
                        'PASS: GTK existing-material reassignment, save, reopen and result invalidation'))
                self.submit({'command': 'material-data'}, assignment_checked)
            return

        if args.smoke_material or args.smoke_load or args.smoke_boundary:
            if self.material_smoke_stage == 0:
                self.material_smoke_stage = 1
                self.edit_properties(bool(args.smoke_load), bool(args.smoke_boundary), (bool(args.smoke_boundary) and args.smoke_boundary_action == 'add') or (bool(args.smoke_load) and args.smoke_load_action in ('add', 'add-traction')))
            elif self.material_smoke_stage == 2:
                def checked(reply):
                    matches = reply.get('boundaries' if args.smoke_boundary else 'loads' if args.smoke_load else 'materials', [])
                    removing_definition = ((args.smoke_load and args.smoke_load_action == 'remove') or
                                           (args.smoke_boundary and args.smoke_boundary_action == 'remove') or
                                           (args.smoke_material and args.smoke_material_action == 'remove'))
                    if removing_definition:
                        still_present = (any(item['Name'] == self.removed_smoke_identity for item in matches)
                                         if args.smoke_material else
                                         any((item['Step'], item['Name']) == self.removed_smoke_identity for item in matches))
                        if not reply.get('ok') or still_present:
                            self.finish(False, 'Removed definition is still present')
                            return
                        self.submit({'command': 'fields'}, lambda result: self.finish(
                            result.get('ok') and not result.get('fields'), 'PASS: GTK definition removal, save, reopen and result invalidation'))
                        return
                    if args.smoke_load and args.smoke_load_action in ('add', 'add-traction'):
                        expected_name = 'added-traction' if args.smoke_load_action == 'add-traction' else 'added-force'
                        matches = [item for item in matches if item['Name'] == expected_name]
                        self.property_smoke_index = 0
                    if args.smoke_boundary and args.smoke_boundary_action == 'add':
                        matches = [item for item in matches if item['Name'] == 'added-displacement']
                        self.property_smoke_index = 0
                    if (not matches or matches[self.property_smoke_index]['Value' if args.smoke_boundary else 'F1' if args.smoke_load else 'YoungsModulus'] != self.material_smoke_expected or
                        args.smoke_material and matches[self.property_smoke_index]['Density'] != self.material_smoke_density_expected or
                        args.smoke_load and args.smoke_load_action == 'add-traction' and
                        (matches[0]['Kind'] != 'Surface traction (total force)' or matches[0]['Region'] != 'LOADED')):
                        self.finish(False, 'GTK reopened property value mismatch')
                        return
                    self.submit({'command': 'fields'}, lambda result: self.finish(
                        result.get('ok') and not result.get('fields'),
                        'PASS: GTK property edit, new-project save, reopen and old-result invalidation'))
                self.material_smoke_stage = 3
                self.submit({'command': 'boundary-directions' if args.smoke_boundary else 'loads' if args.smoke_load else 'materials'}, checked)
            return
        if args.smoke_occlusion:
            passed = (self.occlusion_stage == 3 and self.generation == self.occlusion_generation
                      and self.ready_count == 1 and self.bootstrap_count == 1 and self.bootstrap_confirmed == 1)
            self.finish(passed, 'PASS: GTK real occlusion pauses deadlines; uncover completes same page without retry'
                        if passed else 'GTK occlusion acceptance failed')
            return
        self.view_deadline = None
        self.paused_view_remaining = None
        if args.smoke_frame_recovery:
            passed = self.frame_recovery_stage == 2 and self.scene['SceneId'] == self.frame_recovery_scene
            self.finish(passed, 'PASS: GTK stalled frame rejection, Retry view and real same-scene completion'
                        if passed else 'GTK frame recovery did not exercise the rejected wait')
            return
        if args.smoke_bridge_pressure:
            passed = (self.page_state.get('pressured') and not self.page_state.get('dropped')
                      and self.ready_count == 1 and self.bootstrap_count == 1 and self.bootstrap_confirmed == 1)
            self.finish(bool(passed), 'PASS: GTK ignored/duplicate message bursts preserve bootstrap and scene acknowledgement'
                        if passed else 'GTK bridge pressure acceptance failed')
            return
        if args.smoke_bootstrap_recovery:
            passed = (self.bootstrap_recovery_stage == 2 and self.bootstrap_confirmed > 0
                      and self.scene['SceneId'] == self.bootstrap_recovery_scene)
            self.finish(passed, 'PASS: GTK rejected bootstrap handoff, Retry view and confirmed same-scene delivery'
                        if passed else 'GTK bootstrap recovery failed')
            return
        if args.smoke_startup_recovery:
            passed = self.startup_recovery_stage == 2 and self.scene['SceneId'] == self.startup_scene
            self.finish(passed, 'PASS: GTK startup deadline, Retry view and same-scene restoration' if passed
                        else 'GTK startup recovery did not exercise timeout and retry')
            return
        self.view_failed = False
        self.view_error.set_text('')
        self.retry_button.set_sensitive(False)
        if args.smoke_focus:
            self.check_focus()
            return
        if args.smoke_accessibility:
            self.check_accessibility()
            return
        if args.smoke_clipboard and (not args.smoke_field or self.field_smoke_selected):
            if not self.clipboard_requested:
                self.clipboard_requested = True
                self.view.evaluate_javascript("document.querySelector('#copy-diagnostics').click()", -1, None, None, None, None, None)
            return
        if args.smoke_recovery and (not args.smoke_field or self.field_smoke_selected):
            if self.recovery_stage == 0:
                self.recovery_stage = 1
                self.recovery_scene = self.scene['SceneId']
                self.view.terminate_web_process()
            elif self.recovery_stage == 2:
                self.finish(self.scene['SceneId'] == self.recovery_scene,
                    'PASS: GTK WebKit process termination, Retry view and same-scene restoration')
            return
        if args.smoke_field:
            if self.field_smoke_selected:
                if os.environ.get('PREPOMAX_BROWSER_SCREENSHOT'):
                    self.view.get_snapshot(WebKit2.SnapshotRegion.VISIBLE, WebKit2.SnapshotOptions.NONE, None, self.captured, None)
                else:
                    self.finish(True, 'PASS: GTK field/frame selection and contour scene acknowledgement')
            else:
                def select_field():
                    if self.fields_loading:
                        return True
                    for index, field in enumerate(self.fields):
                        if field['Name'] + '/' + field['Component'] == args.smoke_field:
                            self.field_smoke_selected = True
                            self.field_combo.set_active(index + 1)
                            return False
                    return self.finish(False, 'Requested GTK result field is unavailable')
                GLib.timeout_add(100, select_field)
            return
        if self.result_smoke_pending:
            self.finish(True, 'PASS: GTK native solve and result-project reopening')
            return
        if args.smoke_solve or args.smoke_cancel:
            if not self.solve_smoke_started:
                self.solve_smoke_started = True
                self.solve_button.clicked()
            return
        if not args.smoke:
            return
        if self.stage == 0:
            self.stage = 1
            self.window.resize(900, 600)
            self.view.reload()
        elif self.stage == 1 and args.smoke_switch:
            self.stage = 2
            # Open and cancel a real native dialog, then test invalid-path recovery.
            self.choose_project()
            self.dialog.emit('response', Gtk.ResponseType.CANCEL)
            old_scene = self.scene['SceneId']
            def rejected(reply):
                if reply['ok'] or self.scene['SceneId'] != old_scene:
                    self.finish(False, 'GTK invalid project replaced the current scene')
                    return
                self.choose_project()
                self.dialog.set_filename(str(Path(args.smoke_switch).resolve()))
                # Allow GTK to resolve the selected path before native response dispatch.
                def accept():
                    self.dialog.emit('response', Gtk.ResponseType.ACCEPT)
                    return False
                GLib.timeout_add(500, accept)
            self.open_project(str(Path(args.smoke_switch).parent / '__missing_project__.pma'), rejected)
        elif self.stage in (1, 2):
            self.stage = 3
            if os.environ.get('PREPOMAX_BROWSER_SCREENSHOT'):
                self.view.get_snapshot(WebKit2.SnapshotRegion.VISIBLE, WebKit2.SnapshotOptions.NONE, None, self.captured, None)
            else:
                self.finish(True, 'PASS: GTK asynchronous bootstrap, reload and project workflow')

    def captured(self, view, result, _data):
        try:
            view.get_snapshot_finish(result).write_to_png(os.environ['PREPOMAX_BROWSER_SCREENSHOT'])
            self.finish(True, 'PASS: GTK asynchronous bootstrap, reload and project workflow')
        except Exception:
            self.finish(False, 'GTK snapshot failed')

    def poll(self):
        if self.closed:
            return False
        self.open_button.set_sensitive(not self.opening and not self.solving and self.dialog is None)
        self.field_combo.set_sensitive(bool(self.fields) and not self.opening and not self.solving and self.node_selection is None)
        self.result_button.set_sensitive(bool(self.result_path) and not self.opening and not self.solving and self.node_selection is None)
        tree_ready = self.scene is not None and not self.opening and not self.solving and self.dialog is None and not self.tree_pending
        self.save_button.set_sensitive(bool(tree_ready and self.session and self.session.dirty))
        self.save_as_button.set_sensitive(bool(tree_ready and self.session))
        self.undo_button.set_sensitive(bool(tree_ready and self.session and self.session.index > 0))
        self.redo_button.set_sensitive(bool(tree_ready and self.session and self.session.index + 1 < len(self.session.history)))
        self.model_tree.set_sensitive(tree_ready)
        node = self.selected_model_node()
        self.tree_edit_button.set_sensitive(bool(tree_ready and node and node['Editor']))
        self.tree_preview_button.set_sensitive(bool(tree_ready and node and node['RegionKind']))
        self.history_results_button.set_sensitive(self.scene is not None and bool(self.address) and not self.history_result_pending and
                                                  not self.opening and not self.solving and self.dialog is None)
        self.field_outputs_button.set_sensitive(self.scene is not None and bool(self.address) and not self.field_output_pending and
                                                not self.opening and not self.solving and self.dialog is None)
        self.history_outputs_button.set_sensitive(self.scene is not None and bool(self.address) and not self.history_output_pending and
                                                  not self.opening and not self.solving and self.dialog is None)
        self.sections_button.set_sensitive(self.scene is not None and bool(self.address) and not self.section_pending and
                                           not self.opening and not self.solving and self.dialog is None)
        self.steps_button.set_sensitive(self.scene is not None and bool(self.address) and not self.step_pending and
                                        not self.opening and not self.solving and self.dialog is None)
        self.solve_button.set_sensitive(self.can_solve and self.scene is not None and not self.opening and
                                        not self.solving and self.dialog is None)
        self.import_button.set_sensitive(bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.mesh_geometry_button.set_sensitive(bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.remesh_geometry_button.set_sensitive(self.geometry_source is not None and not self.remesh_pending and not self.opening and not self.solving and self.dialog is None)
        self.export_geometry_button.set_sensitive(self.geometry_source is not None and not self.opening and not self.solving and self.dialog is None)
        self.cad_topology_button.set_sensitive(self.cad_topology is not None and not self.cad_topology_pending and not self.opening and not self.solving and self.dialog is None)
        self.add_force_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.gravity_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.centrifugal_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.node_set_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.surface_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.surface_angle.set_sensitive(self.scene is not None and not self.opening and not self.solving and self.dialog is None)
        self.pressure_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.add_boundary_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.fixed_support_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.boundary_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.load_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.material_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.plasticity_button.set_sensitive(self.scene is not None and bool(self.address) and not self.plasticity_pending and not self.opening and not self.solving and self.dialog is None)
        self.add_material_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        self.assign_material_button.set_sensitive(self.scene is not None and bool(self.address) and not self.opening and not self.solving and self.dialog is None)
        can_scale = bool(self.address) and not self.opening and not self.solving and self.node_selection is None and self.field_combo.get_active() > 0
        self.scale_control.set_sensitive(can_scale)
        self.apply_scale.set_sensitive(can_scale)
        if self.scene is None:
            self.solve_button.set_sensitive(False)
        if process.poll() is not None:
            return self.finish(False, 'GTK companion exited unexpectedly')
        if (args.smoke or args.smoke_solve or args.smoke_cancel or args.smoke_field or args.smoke_recovery) and time.monotonic() - self.smoke_started > args.smoke_timeout:
            return self.finish(False, 'GTK stable-frame timeout: backend=%s load=%s mapped=%s active=%s windowState=%s visibility=%s clock=%s opening=%s failed=%s evaluations=%d ready=%d bootstrap=%d confirmed=%d page=%s' % (
                Gdk.Display.get_default().__gtype__.name, self.load_stage, self.view.get_mapped(), self.window.is_active(), int(self.window.get_window().get_state()), self.native_visibility, self.view.get_frame_clock().get_frame_counter(),
                self.opening, self.view_failed, self.evaluation_count, self.ready_count, self.bootstrap_count, self.bootstrap_confirmed, json.dumps(self.page_state, sort_keys=True)))
        if self.view_deadline is not None and time.monotonic() >= self.view_deadline and not self.opening:
            self.mark_view_failed('The scene did not finish loading. Retry the view to try again.')
            if args.smoke_startup_recovery and self.startup_recovery_stage == 1:
                if not self.retry_button.get_sensitive():
                    return self.finish(False, 'GTK startup timeout did not enable Retry view')
                self.startup_recovery_stage = 2
                self.retry_button.clicked()
        if self.solving and not self.job_pending:
            self.job_pending = True
            if self.job_kind in ('mesh', 'remesh'):
                self.submit({'command': 'mesh-job-status'}, self.mesh_job_updated)
            else:
                self.submit({'command': 'job-status'}, self.job_updated)
        if self.address and not self.view_failed and not self.busy and not self.opening and self.view.get_uri() == self.address and not self.view.is_loading():
            self.busy = True
            self.view.evaluate_javascript('window.__gtkDrain()', -1, None, None, None, self.evaluated, self.generation)
        return True

    def finish(self, ok, message):
        if not self.closed:
            capture = os.environ.get('PREPOMAX_WORKSPACE_SCREENSHOT')
            if ok and args.smoke and capture:
                picture = Gdk.pixbuf_get_from_window(self.window.get_window(), 0, 0,
                    self.window.get_allocated_width(), self.window.get_allocated_height())
                if picture is not None:
                    picture.savev(capture, 'png', [], [])
            self.closed = True
            self.status = 0 if ok else 1
            if self.occlusion_cover:
                self.occlusion_cover.destroy()
                self.occlusion_cover = None
            if self.dialog:
                self.dialog.destroy()
            conversion = self.conversion_process
            if conversion is not None and conversion.poll() is None:
                try:
                    os.killpg(conversion.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            print(message, flush=True)
            if self.session:
                self.session.close()
                if self.session.dirty:
                    print('Unsaved model snapshots retained in ' + str(self.session.directory), flush=True)
            self.application.quit()
        return False

application = Gtk.Application(application_id=APPLICATION_ID, flags=Gio.ApplicationFlags.NON_UNIQUE)
viewer = None

def activate(app):
    global viewer
    if viewer is None:
        viewer = Viewer(app)
    else:
        viewer.window.present()

def interrupted():
    if viewer is not None:
        return viewer.finish(False, 'GTK interrupted')
    application.quit()
    return False

application.connect('activate', activate)
GLibUnix.signal_add(GLib.PRIORITY_DEFAULT, signal.SIGINT, interrupted)
GLibUnix.signal_add(GLib.PRIORITY_DEFAULT, signal.SIGTERM, interrupted)
run_status = 1
try:
    run_status = application.run([sys.argv[0]])
finally:
    if viewer is not None:
        viewer.closed = True
    # Finish queued I/O away from GTK; kill the owned group if it cannot stop promptly.
    worker.shutdown(wait=False, cancel_futures=True)
    if process.poll() is None:
        try:
            process.stdin.close()
            process.wait(timeout=5)
        except (subprocess.TimeoutExpired, BrokenPipeError):
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
    if viewer is not None:
        viewer.window.destroy()
        if process.returncode != 0:
            print('GTK companion exited with status ' + str(process.returncode), flush=True)
            viewer.status = 1
if viewer is not None and viewer.status == 0 and args.smoke:
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    try:
        opener.open(viewer.address.split('?')[0] + 'healthz', timeout=2).close()
        print('FAIL: GTK renderer listener remained open')
        viewer.status = 1
    except OSError:
        print('PASS: GTK companion exit closed renderer listener')
sys.exit(viewer.status if viewer is not None else (run_status or 1))
