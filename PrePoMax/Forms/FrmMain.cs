// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using CaeGlobals;
using CaeJob;
using CaeMesh;
using CaeMesh.Meshing.BaramMesh;
using CaeModel;
using CaeModel.CFD.BaramFlow;
using CaeResults;
using FileInOut.Output;
using PrePoMax.Commands;
using PrePoMax.Fatigue;
using PrePoMax.Forms;
using PrePoMax.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using UserControls;
using vtkControl;
using static System.ComponentModel.Design.ObjectSelectorEditor;


namespace PrePoMax
{
    public partial class FrmMain : MainMouseWheelManagedForm
    {
        // Variables                                                                                                                
        #region Variables ##########################################################################################################
        //
        FrmSplash _splash;
        //
        private vtkControl.vtkControl _vtk;
        private VtkJsRendererControl _vtkJs;
        private System.Windows.Forms.Timer _vtkJsSmokeFieldTimer;
        private System.Windows.Forms.Timer _vtkJsSmokePickTimer;
        private System.Windows.Forms.Timer _vtkJsSmokeAnnotationTimer;
        private System.Windows.Forms.Timer _vtkJsSmokeLargeSelectionTimer;
        private System.Windows.Forms.Timer _vtkJsSmokeAnimationTimer;
        private System.Windows.Forms.Timer _vtkJsSmokeCameraTimer;
        private System.Windows.Forms.Timer _vtkJsSmokeBoxZoomTimer;
        private System.Windows.Forms.Timer _vtkJsSmokeVisibilityCameraTimer;
        private System.Windows.Forms.Timer _vtkJsSmokeExplodedPreviewTimer;
        private System.Windows.Forms.Timer _vtkJsSmokeSectionViewTimer;
        private int _vtkJsSmokeReadyTicks;
        private double[] _vtkJsCameraPosition;
        private double[] _vtkJsCameraFocalPoint;
        private double[] _vtkJsCameraViewUp;
        private double _vtkJsCameraParallelScale;
        private double _vtkJsBoxZoomStartScale;
        private bool _vtkJsCameraCommandPending;
        private long _vtkJsCameraRevision;
        private ModelTree _modelTree;
        private Controller _controller;
        private CommandLineOptions _cmdOptions;
        private string[] outputLines;
        private AdvisorControl _advisorControl;
        private KeyboardHook _keyboardHook;
        private Dictionary<ViewGeometryModelResults, int> _selectedSymbolIndex;
        private Stack<bool[]> _prevMenuStates;
        private bool _closeAfterRegeneration;
        private float _prevScaleFactor;
        private bool _suppressSymbolChanged;
        private ToolStripMenuItem _tsmiImportCylinderDefinitionsJson;
        private ToolStripMenuItem _tsmiSRSuntour;
        private ToolStripMenuItem _tsmiSRSuntourImportMesh;
        private ToolStripMenuItem _tsmiSRSuntourAssignCylinders;
        private ToolStripMenuItem _tsmiSRSuntourCreateContacts;
        private ToolStripMenuItem _tsmiCheckContactPair;
        private FatigueSetup _fatigueSetup;
        private const string FatigueCalculationStateText = "Calculating fatigue...";
        private const string BaramMeshGeometryNodePrefix = "BaramMeshGeometry:";
        //
        private Point _formLocation;
        private List<Form> _allForms;
        private FrmSelectEntity _frmSelectEntity;
        private FrmSelectGeometry _frmSelectGeometry;
        private FrmSelectItemSet _frmSelectItemSet;
        private FrmUserVew _frmUserView;
        private FrmSectionView _frmSectionView;
        private FrmExplodedView _frmExplodedView;
        private FrmFollowerView _frmFollowerView;
        private FrmNewModel _frmNewModel;
        private FrmExportFileWithProperties _frmExportFileWithProperties;
        private FrmEditCommands _frmEditCommands;
        private FrmRegenerate _frmRegenerate;
        private FrmAnalyzeGeometry _frmAnalyzeGeometry;
        private FrmMeshSetupItem _frmMeshSetupItem;
        private FrmModelProperties _frmModelProperties;
        private FrmCalculixKeywordEditor _frmCalculixKeywordEditor;
        private FrmBoundaryLayer _frmBoundaryLayer;
        private FrmRemeshingParameters _frmRemeshingParameters;
        private FrmThickenShellMesh _frmThickenShellMesh;
        private FrmSplitPartMeshUsingSurface _frmSplitPartMeshUsingSurface;
        private FrmMergeCoincidentNodes _frmMergeCoincidentNodes;
        private FrmElementQuality _frmElementQuality;
        private FrmPartProperties _frmPartProperties;
        private FrmTranslate _frmTranslate;
        private FrmRotate _frmRotate;
        private FrmMirror _frmMirror;
        private FrmScale _frmScale;
        private FrmNodeSet _frmNodeSet;
        private FrmElementSet _frmElementSet;
        private FrmSurface _frmSurface;
        private FrmReferencePoint _frmReferencePoint;
        private FrmCylinder _frmCylinder;
        private FrmCoordinateSystem _frmCoordinateSystem;
        private FrmMaterial _frmMaterial;
        private FrmSection _frmSection;
        private FrmConstraint _frmConstraint;
        private FrmSurfaceInteraction _frmSurfaceInteraction;
        private FrmContactPair _frmContactPair;
        private FrmSearchContactPairs _frmSearchContactPairs;
        private FrmInitialCondition _frmInitialCondition;
        private FrmDistribution _frmDistribution;
        private FrmAmplitude _frmAmplitude;
        private FrmStep _frmStep;
        private FrmStepControls _frmStepControls;
        private FrmHistoryOutput _frmHistoryOutput;
        private FrmFieldOutput _frmFieldOutput;
        private FrmBC _frmBoundaryCondition;
        private FrmLoad _frmLoad;
        private FrmDefinedField _frmDefinedField;
        private FrmSettings _frmSettings;
        private FrmQuery _frmQuery;
        private FrmFind _frmFind;
        private FrmAnalysis _frmAnalysis;
        private FrmMonitor _frmMonitor;
        private FrmAnimation _frmAnimation;
        private FrmResultFieldOutput _frmResultFieldOutput;
        private FrmResultHistoryOutput _frmResultHistoryOutput;
        private FrmViewResultHistoryOutput _frmViewResultHistoryOutput;
        private FrmTransformation _frmTransformation;
        private const string StanchionInsertCylinderName = "Stanchion_Insert";
        private const string SteertubeInsertCylinderName = "Steertube_Insert";
        private const string TopBushingCylinderName = "Top_Bushing";
        private const string BottomBushingCylinderName = "Bottom_Bushing";
        private const string SRSuntourSurfaceInteractionName = "SRSuntour_Surface_Interaction";
        //
        private sealed class SweepMeshPythonResult
        {
            public string Status { get; set; }
            public string OutputPath { get; set; }
            public string Error { get; set; }
            public bool ReportFound { get; set; }
        }
        private sealed class CylindricalExtrusionPythonResult
        {
            public string Status { get; set; }
            public string OutputPath { get; set; }
            public string DeltaInpPath { get; set; }
            public string PayloadPath { get; set; }
            public string Error { get; set; }
            public bool ReportFound { get; set; }
        }
        [DataContract]
        private sealed class CylindricalExtrusionPayload
        {
            [DataMember(Name = "existing_element_sets")]
            public List<CylindricalExtrusionElementSetPayload> ExistingElementSets { get; set; }
            [DataMember(Name = "existing_surfaces")]
            public List<CylindricalExtrusionSurfacePayload> ExistingSurfaces { get; set; }
            [DataMember(Name = "generated_element_sets")]
            public List<CylindricalExtrusionElementSetPayload> GeneratedElementSets { get; set; }
            [DataMember(Name = "generated_surfaces")]
            public List<CylindricalExtrusionSurfacePayload> GeneratedSurfaces { get; set; }
        }
        [DataContract]
        private sealed class CylindricalExtrusionElementSetPayload
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }
            [DataMember(Name = "elements")]
            public List<int> Elements { get; set; }
        }
        [DataContract]
        private sealed class CylindricalExtrusionSurfacePayload
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }
            [DataMember(Name = "faces")]
            public List<CylindricalExtrusionSurfaceFacePayload> Faces { get; set; }
        }
        [DataContract]
        private sealed class CylindricalExtrusionSurfaceFacePayload
        {
            [DataMember(Name = "element_set_name")]
            public string ElementSetName { get; set; }
            [DataMember(Name = "face_name")]
            public string FaceName { get; set; }
        }
        private sealed class BoundaryFaceData
        {
            public int ElementId { get; set; }
            public FeFaceName FaceName { get; set; }
            public int[] NodeIds { get; set; }
            public bool IsBoundary { get; set; }
        }
        private sealed class CylindricalExtrusionFaceData
        {
            public int ElementId { get; set; }
            public FeFaceName FaceName { get; set; }
            public int[] NodeIds { get; set; }
            public bool IsParabolic { get; set; }
        }
        [DataContract]
        private sealed class CylinderDefinitionsPayload
        {
            [DataMember(Name = "cylinders")]
            public List<CylinderDefinitionPayload> Cylinders { get; set; }
        }
        [DataContract]
        private sealed class CylinderDefinitionPayload
        {
            [DataMember(Name = "name", EmitDefaultValue = false)]
            public string Name { get; set; }
            [DataMember(Name = "center")]
            public double[] Center { get; set; }
            [DataMember(Name = "radius")]
            public double Radius { get; set; }
            [DataMember(Name = "direction")]
            public double[] Direction { get; set; }
            [DataMember(Name = "height")]
            public double Height { get; set; }
            [DataMember(Name = "extra_radius", EmitDefaultValue = false)]
            public double? ExtraRadius { get; set; }
            [DataMember(Name = "chamfer", EmitDefaultValue = false)]
            public double[] Chamfer { get; set; }
            [DataMember(Name = "target_radius", EmitDefaultValue = false)]
            public double? TargetRadius { get; set; }
            [DataMember(Name = "num_layers", EmitDefaultValue = false)]
            public int? NumberOfLayers { get; set; }
            [DataMember(Name = "tolerance", EmitDefaultValue = false)]
            public double? Tolerance { get; set; }
        }
        //
        #endregion  ################################################################################################################

        #region Properties #########################################################################################################
        public Controller Controller { get { return _controller; } set { _controller = value; } }
        public bool IsVtkJsRendererActive { get { return _vtkJs != null; } }
        public ViewGeometryModelResults GetCurrentView()
        {
            return _controller.CurrentView;
        }
        public void SetCurrentView(ViewGeometryModelResults view)
        {
            // This gets called from: _controller.CurrentView
            InvokeIfRequired(() =>
            {
                if (view == ViewGeometryModelResults.Geometry)
                {
                    _modelTree.SetGeometryTab();
                    if (_controller.Model != null) UpdateUnitSystem(_controller.Model.UnitSystem);
                }
                else if (view == ViewGeometryModelResults.Model)
                {
                    _modelTree.SetModelTab();
                    if (_controller.Model != null) UpdateUnitSystem(_controller.Model.UnitSystem);
                }
                else if (view == ViewGeometryModelResults.Results)
                {
                    _modelTree.SetResultsTab();
                    if (_controller.CurrentResult != null) UpdateUnitSystem(_controller.CurrentResult.UnitSystem);
                    InitializeResultWidgetPositions();
                }
                else if (view == ViewGeometryModelResults.Fatigue)
                {
                    _modelTree.SetFatigueTab();
                    if (_controller.CurrentResult != null) UpdateUnitSystem(_controller.CurrentResult.UnitSystem);
                    else if (_controller.Model != null) UpdateUnitSystem(_controller.Model.UnitSystem);
                }
                else if (view == ViewGeometryModelResults.BaramMesh)
                {
                    _modelTree.SetBaramMeshTab();
                    if (_controller.Model != null) UpdateUnitSystem(_controller.Model.UnitSystem);
                }
                else if (view == ViewGeometryModelResults.BaramFlow)
                {
                    _modelTree.SetBaramFlowTab();
                    if (_controller.Model != null) UpdateUnitSystem(_controller.Model.UnitSystem);
                }
                else throw new NotSupportedException();
                //
                if (_advisorControl != null &&
                    view != ViewGeometryModelResults.Fatigue &&
                    view != ViewGeometryModelResults.BaramMesh &&
                    view != ViewGeometryModelResults.BaramFlow)
                {
                    ViewType viewType = GetViewType(view);
                    //
                    _advisorControl.PrepareControls(viewType);
                }
                //
                SetMenuAndToolStripVisibility();
                // This calls the saved action
                SetCurrentEdgesVisibilities(_controller.CurrentEdgesVisibility);    // highlights selected buttons
                //
                this.ActiveControl = null;
            });
        }
        public void SetCurrentEdgesVisibilities(vtkControl.vtkEdgesVisibility edgesVisibility)
        {
            InvokeIfRequired(() =>
            {
                // Highlight selected buttons
                tsbShowWireframeEdges.Checked = edgesVisibility == vtkControl.vtkEdgesVisibility.Wireframe;
                tsbShowElementEdges.Checked = edgesVisibility == vtkControl.vtkEdgesVisibility.ElementEdges;
                tsbShowModelEdges.Checked = edgesVisibility == vtkControl.vtkEdgesVisibility.ModelEdges;
                tsbShowNoEdges.Checked = edgesVisibility == vtkControl.vtkEdgesVisibility.NoEdges;
                //
                _vtk.EdgesVisibility = edgesVisibility;
                //
                UpdateHighlight();
            });
        }
        public bool ScreenUpdating { get { return _modelTree.ScreenUpdating; } set { _modelTree.ScreenUpdating = value; } }
        public bool RenderingOn { get { return _vtk.RenderingOn; } set { _vtk.RenderingOn = value; } }
        private ViewType GetViewType(ViewGeometryModelResults view)
        {
            ViewType viewType;
            if (view == ViewGeometryModelResults.Geometry) viewType = ViewType.Geometry;
            else if (view == ViewGeometryModelResults.Model) viewType = ViewType.Model;
            else if (view == ViewGeometryModelResults.Results) viewType = ViewType.Results;
            else if (view == ViewGeometryModelResults.Fatigue) viewType = ViewType.Fatigue;
            else if (view == ViewGeometryModelResults.BaramMesh) viewType = ViewType.BaramMesh;
            else if (view == ViewGeometryModelResults.BaramFlow) viewType = ViewType.BaramFlow;
            else throw new NotSupportedException();
            return viewType;
        }

        #endregion  ################################################################################################################


        // Constructors                                                                                                             
        public FrmMain(CommandLineOptions cmdOptions)
        {
            // Initialize               
            InitializeComponent();
            //SettingsContainer settings = new SettingsContainer();
            //settings.LoadFromFile();
            //if (settings.General.Maximized)
            //{
            //    Rectangle resolution = Screen.FromControl(this).Bounds;
            //    this.Location = new Point(0, 0);
            //    this.Size = new Size(resolution.Width, resolution.Height);
            //    this.WindowState = FormWindowState.Maximized;
            //}
            _vtk = null;
            _controller = null;
            _modelTree = null;
            _fatigueSetup = FatigueSetup.CreateDefault();
            _prevMenuStates = new Stack<bool[]>();
            _closeAfterRegeneration = false;
            _cmdOptions = cmdOptions;
            CommandLineOptions.CheckForErrors(_cmdOptions); // make sure options are compatible
            //
            MessageBoxes.ParentForm = this;
            //
            if (_cmdOptions.ShowGui == "No")
            {
                MessageBoxes.WriteDataToOutput = WriteDataToOutput;
                AutoClosingMessageBox.WriteDataToOutput = WriteDataToOutput;
            }
        }
        // Event handling                                                                                                           
        private void FrmMain_Load(object sender, EventArgs e)
        {
            if (TestWriteAccess() == false)
            {
                MessageBoxes.ShowError("PrePoMax has no write access for the folder: " + Application.StartupPath +
                                       Environment.NewLine + Environment.NewLine +
                                       "To run PrePoMax, move the base PrePoMax folder to another, non-protected folder.");
                Close();
                return;
            }
            //
            //SettingsContainer settings = new SettingsContainer();
            //settings.LoadFromFile();
            //if (settings.General.Maximized)
            //{
            //    Rectangle resolution = Screen.FromControl(this).Bounds;
            //    this.Location = new Point(0, 0);
            //    this.Size = new Size(resolution.Width, resolution.Height);
            //    this.WindowState = FormWindowState.Maximized;
            //}
            Text = Globals.ProgramName;
            this.TopMost = true;
            //
            if (_cmdOptions.ShowGui == "Yes")
            {
                _splash = new FrmSplash { TopMost = true };
                Task.Run(() => _splash.ShowDialog());
            }
            //
            try
            {
                // Edit annotation text box
                panelControl.Controls.Remove(aeAnnotationTextEditor);
                this.Controls.Add(aeAnnotationTextEditor);
                // Vtk
                _vtk = new vtkControl.vtkControl();
                panelControl.Parent.Controls.Add(_vtk);
                if (IsVtkJsRendererEnabled())
                {
                    _vtkJs = new VtkJsRendererControl();
                    _vtkJs.ValidatedPick += VtkJs_ValidatedPick;
                    _vtkJs.ValidatedArea += VtkJs_ValidatedArea;
                    _vtkJs.AnnotationEdited += VtkJs_AnnotationEdited;
                    _vtkJs.AnnotationActionRequested += VtkJs_AnnotationActionRequested;
                    _vtkJs.CameraChanged += VtkJs_CameraChanged;
                    _vtkJs.BoxZoomEnded += VtkJs_BoxZoomEnded;
                    panelControl.Parent.Controls.Add(_vtkJs);
                    _vtkJs.BringToFront();
                }
                panelControl.SendToBack();
                // Menu
                tsmiColorAnnotations.DropDown.Closing += DropDown_Closing;
                InitializeSRSuntourMenu();
                InitializeCompatibilityExportMenu();
                InitializeDisplayedFrdExportMenu();
                InitializeContactValidationMenu();
                // Tree
                _modelTree = new ModelTree();
                // Controller
                _controller = new Controller(this);
                SetFatigueSetup(_controller.FatigueSetup);
                //
                _modelTree.Name = "modelTree";
                splitContainer1.Panel1.Controls.Add(this._modelTree);
                _modelTree.Dock = DockStyle.Fill;
                _modelTree.TabIndex = 0;
                _modelTree.RegenerateTreeCallBack = RegenerateTreeCallback;
                _modelTree.CheckItemName = _controller.CheckItemName;
                //
                _modelTree.GeometryMeshResultsEvent += ModelTree_ViewEvent;
                _modelTree.SelectEvent += ModelTree_SelectEvent;
                _modelTree.ClearSelectionEvent += Clear3DSelection;
                _modelTree.CreateEvent += ModelTree_CreateEvent;
                _modelTree.EditEvent += ModelTree_EditEvent;
                _modelTree.RenameEvent += ModelTree_RenameEvent;
                _modelTree.SetPartColorEvent += ModelTree_SetPartColorEvent;
                _modelTree.ResetPartColorEvent += ModelTree_ResetPartColorEvent;
                _modelTree.EditStepControlsEvent += EditStepControls;
                _modelTree.QueryEvent += ModelTree_QueryEvent;
                _modelTree.DuplicateEvent += ModelTree_DuplicateEvent;
                _modelTree.PropagateEvent += ModelTree_PropagateEvent;
                _modelTree.PreviewEvent += ModelTree_PreviewEvent;
                _modelTree.CreateCompoundPart += CreateAndImportCompoundPart;
                _modelTree.SwapPartGeometries += SwapPartGeometries;
                _modelTree.PreviewEdgeMesh += PreviewEdgeMeshAsync;
                _modelTree.CreateMeshEvent += CreatePartMeshes;
                _modelTree.CopyGeometryToResultsEvent += CopyGeometryPartsToResults;
                _modelTree.EditCalculixKeywords += EditCalculiXKeywords;
                _modelTree.MergeParts += MergeModelParts;
                _modelTree.MergeResultParts += MergeResultParts;
                _modelTree.ConvertElementSetsToMeshParts += ConvertElementSetsToMeshParts;
                _modelTree.DeleteElementsInElementSets += DeleteElementsInElementSets;
                _modelTree.MaterialLibrary += ShowMaterialLibrary;
                _modelTree.SearchContactPairs += SearchContactPairs;
                _modelTree.CheckContactPairs += ModelTree_CheckContactPairs;
                _modelTree.SwapMasterSlave += ModelTree_SwapMasterSlave;
                _modelTree.MergeByMasterSlave += ModelTree_MergeByMasterSlave;
                _modelTree.HideShowEvent += ModelTree_HideShowEvent;
                _modelTree.SetTransparencyEvent += ModelTree_SetTransparencyEvent;
                _modelTree.ColorContoursVisibilityEvent += ModelTree_ColorContoursVisibilityEvent;
                _modelTree.RunEvent += RunAnalysis;
                _modelTree.RunFatigueEvent += RunFatigueAnalysis;
                _modelTree.BaramMeshActionEvent += ModelTree_BaramMeshActionEvent;
                _modelTree.BaramMeshRunEvent += ModelTree_BaramMeshRunEvent;
                _modelTree.BaramMeshRenameEvent += ModelTree_BaramMeshRenameEvent;
                _modelTree.BaramMeshSelectEvent += ModelTree_BaramMeshSelectEvent;
                _modelTree.BaramMeshAssignEvent += ModelTree_BaramMeshAssignEvent;
                _modelTree.BaramMeshAssignRemainingWallsEvent += ModelTree_BaramMeshAssignRemainingWallsEvent;
                _modelTree.BaramMeshGroupWallsEvent += ModelTree_BaramMeshGroupWallsEvent;
                _modelTree.BaramFlowActionEvent += ModelTree_BaramFlowActionEvent;
                _modelTree.EditFatigueSetupEvent += EditFatigueSetup;
                _modelTree.CheckModelEvent += CheckModel;
                _modelTree.MonitorEvent += MonitorAnalysis;
                _modelTree.ResultsEvent += ResultsAnalysis;
                _modelTree.KillEvent += KillAnalysis;
                _modelTree.ActivateDeactivateEvent += ModelTree_ActivateDeactivateEvent;
                _modelTree.DeleteEvent += ModelTree_DeleteEvent;
                _modelTree.FieldDataSelectEvent += ModelTree_FieldDataSelectEvent;
                _modelTree.RenderingOff += () => _vtk.RenderingOn = false;
                _modelTree.RenderingOn += () => _vtk.RenderingOn = true;
                // Strip menus
                tsFile.Location = new Point(0, 0);
                tsViews.Location = new Point(tsFile.Left + tsFile.Width, 0);
                tsSymbols.Location = new Point(tsViews.Left + tsViews.Width, 0);
                tsResultDeformation.Location = new Point(0, tsFile.Height);
                tsResults.Location = new Point(tsResultDeformation.Left + tsResultDeformation.Width, tsFile.Height);
                tscbSymbols.SelectedIndexChanged += tscbSymbols_SelectedIndexChanged;
                // Vtk
                _vtk.OnMouseLeftButtonUpSelection += SelectPointOrArea;
                _vtk.Controller_GetAnnotationText += _controller.GetAnnotationText;
                _vtk.Controller_GetNodeActorData = _controller.GetNodeActorData;
                _vtk.Controller_GetCellActorData = _controller.GetCellActorData;
                _vtk.Controller_GetCellFaceActorData = _controller.GetCellFaceActorData;
                _vtk.Controller_GetEdgeActorData = _controller.GetEdgeActorData;
                _vtk.Controller_GetSurfaceEdgesActorDataFromElementId = _controller.GetSurfaceEdgesActorDataFromElementId;
                _vtk.Controller_GetSurfaceEdgesActorDataFromNodeAndElementIds =
                    _controller.GetSurfaceEdgesActorDataFromNodeAndElementIds;
                _vtk.Controller_GetPartActorData = _controller.GetPartActorData;
                _vtk.Controller_GetGeometryActorData = _controller.GetGeometryActorData;
                _vtk.Controller_GetGeometryVertexActorData = _controller.GetGeometryVertexActorData;
                _vtk.Controller_ActorsPicked = SelectBaseParts;
                _vtk.Form_ShowColorBarSettings = ShowColorBarSettings;
                _vtk.Form_ShowLegendSettings = ShowLegendSettings;
                _vtk.Form_ShowStatusBlockSettings = ShowStatusBlockSettings;
                _vtk.Form_EndEditArrowWidget = EndEditArrowAnnotation;
                _vtk.Form_WidgetPicked = AnnotationPicked;
                _vtk.Form_BoxZoomEnded = BoxZoomEnded;
                // Forms
                _formLocation = new Point(100, 100);
                _allForms = new List<Form>();
                //
                _frmSelectEntity = new FrmSelectEntity(_controller);
                AddFormToAllForms(_frmSelectEntity);
                //
                _frmSelectGeometry = new FrmSelectGeometry(_controller);
                AddFormToAllForms(_frmSelectGeometry);
                //
                _frmSelectItemSet = new FrmSelectItemSet(_controller);
                AddFormToAllForms(_frmSelectItemSet);
                //
                _frmUserView = new FrmUserVew(_controller);
                AddFormToAllForms(_frmUserView);
                //
                _frmSectionView = new FrmSectionView(_controller);
                AddFormToAllForms(_frmSectionView);
                //
                _frmExplodedView = new FrmExplodedView(_controller);
                _frmExplodedView.Clear3D = Clear3DSelection;
                AddFormToAllForms(_frmExplodedView);
                //
                _frmFollowerView = new FrmFollowerView(_controller);
                AddFormToAllForms(_frmFollowerView);
                //
                _frmNewModel = new FrmNewModel(_controller);
                AddFormToAllForms(_frmNewModel);
                //
                _frmExportFileWithProperties = new FrmExportFileWithProperties(_controller);
                _frmExportFileWithProperties.ExportUsingFileProperties = ExportUsingFileProperties;
                AddFormToAllForms(_frmExportFileWithProperties);
                //
                _frmEditCommands = new FrmEditCommands(_controller);
                AddFormToAllForms(_frmEditCommands);
                //
                _frmRegenerate = new FrmRegenerate();
                AddFormToAllForms(_frmRegenerate);
                //
                _frmAnalyzeGeometry = new FrmAnalyzeGeometry(_controller);
                AddFormToAllForms(_frmAnalyzeGeometry);
                //
                _frmMeshSetupItem = new FrmMeshSetupItem(_controller);
                _frmMeshSetupItem.PreviewEdgeMeshAsync = PreviewEdgeMeshAsync;
                AddFormToAllForms(_frmMeshSetupItem);
                //
                _frmModelProperties = new FrmModelProperties(_controller);
                AddFormToAllForms(_frmModelProperties);
                //
                _frmBoundaryLayer = new FrmBoundaryLayer(_controller);
                AddFormToAllForms(_frmBoundaryLayer);
                //
                _frmRemeshingParameters = new FrmRemeshingParameters(_controller);
                AddFormToAllForms(_frmRemeshingParameters);
                //
                _frmThickenShellMesh = new FrmThickenShellMesh(_controller);
                AddFormToAllForms(_frmThickenShellMesh);
                //
                _frmSplitPartMeshUsingSurface = new FrmSplitPartMeshUsingSurface(_controller);
                _frmSplitPartMeshUsingSurface.SplitPartMeshUsingSurface += SplitPartMeshUsingSurface;
                AddFormToAllForms(_frmSplitPartMeshUsingSurface);
                //
                _frmMergeCoincidentNodes = new FrmMergeCoincidentNodes(_controller);
                AddFormToAllForms(_frmMergeCoincidentNodes);
                //
                _frmElementQuality = new FrmElementQuality(_controller);
                AddFormToAllForms(_frmElementQuality);
                //
                _frmPartProperties = new FrmPartProperties(_controller);
                AddFormToAllForms(_frmPartProperties);
                //
                _frmTranslate = new FrmTranslate(_controller);
                AddFormToAllForms(_frmTranslate);
                //
                _frmRotate = new FrmRotate(_controller);
                AddFormToAllForms(_frmRotate);
                //
                _frmMirror = new FrmMirror(_controller);
                AddFormToAllForms(_frmMirror);
                //
                _frmScale = new FrmScale(_controller);
                _frmScale.ScaleGeometryPartsAsync = ScaleGeometryPartsAsync;
                AddFormToAllForms(_frmScale);
                //
                _frmNodeSet = new FrmNodeSet(_controller);
                AddFormToAllForms(_frmNodeSet);
                //
                _frmElementSet = new FrmElementSet(_controller);
                AddFormToAllForms(_frmElementSet);
                //
                _frmSurface = new FrmSurface(_controller);
                AddFormToAllForms(_frmSurface);
                //
                _frmReferencePoint = new FrmReferencePoint(_controller);
                AddFormToAllForms(_frmReferencePoint);
                //
                _frmCylinder = new FrmCylinder(_controller);
                AddFormToAllForms(_frmCylinder);
                //
                _frmCoordinateSystem = new FrmCoordinateSystem(_controller);
                AddFormToAllForms(_frmCoordinateSystem);
                //
                _frmMaterial = new FrmMaterial(_controller);
                AddFormToAllForms(_frmMaterial);
                //
                _frmSection = new FrmSection(_controller);
                AddFormToAllForms(_frmSection);
                //
                _frmConstraint = new FrmConstraint(_controller);
                AddFormToAllForms(_frmConstraint);
                //
                _frmSurfaceInteraction = new FrmSurfaceInteraction(_controller);
                AddFormToAllForms(_frmSurfaceInteraction);
                //
                _frmContactPair = new FrmContactPair(_controller);
                AddFormToAllForms(_frmContactPair);
                //
                _frmSearchContactPairs = new FrmSearchContactPairs(_controller);
                AddFormToAllForms(_frmSearchContactPairs);
                //
                _frmInitialCondition = new FrmInitialCondition(_controller);
                AddFormToAllForms(_frmInitialCondition);
                //
                _frmDistribution = new FrmDistribution(_controller);
                AddFormToAllForms(_frmDistribution);
                //
                _frmAmplitude = new FrmAmplitude(_controller);
                AddFormToAllForms(_frmAmplitude);
                //
                _frmStep = new FrmStep(_controller);
                AddFormToAllForms(_frmStep);
                //
                _frmStepControls = new FrmStepControls(_controller);
                AddFormToAllForms(_frmStepControls);
                //
                _frmHistoryOutput = new FrmHistoryOutput(_controller);
                AddFormToAllForms(_frmHistoryOutput);
                //
                _frmFieldOutput = new FrmFieldOutput(_controller);
                AddFormToAllForms(_frmFieldOutput);
                //
                _frmBoundaryCondition = new FrmBC(_controller);
                AddFormToAllForms(_frmBoundaryCondition);
                //
                _frmLoad = new FrmLoad(_controller);
                AddFormToAllForms(_frmLoad);
                //
                _frmDefinedField = new FrmDefinedField(_controller);
                AddFormToAllForms(_frmDefinedField);
                //
                _frmAnalysis = new FrmAnalysis(_controller);
                AddFormToAllForms(_frmAnalysis);
                //
                _frmMonitor = new FrmMonitor(_controller);
                _frmMonitor.KillJob += KillAnalysis;
                _frmMonitor.Results += ResultsAnalysis;
                AddFormToAllForms(_frmMonitor);
                //
                _frmSettings = new FrmSettings();
                _frmSettings.UpdateSettings += UpdateSettings;
                AddFormToAllForms(_frmSettings);
                //
                _frmQuery = new FrmQuery();
                _frmQuery.Form_WriteDataToOutput = WriteDataToOutput;
                _frmQuery.Form_RemoveAnnotations = tsbRemoveAnnotations_Click;
                AddFormToAllForms(_frmQuery);
                //
                _frmFind = new FrmFind();
                _frmFind.Form_RemoveAnnotations = tsbRemoveAnnotations_Click;
                AddFormToAllForms(_frmFind);
                //
                _frmAnimation = new FrmAnimation();
                _frmAnimation.Form_ControlsEnable = SetMenuAndToolStripVisibilityByAnimation;
                AddFormToAllForms(_frmAnimation);
                //
                _frmResultFieldOutput = new FrmResultFieldOutput(_controller);
                AddFormToAllForms(_frmResultFieldOutput);
                //
                _frmResultHistoryOutput = new FrmResultHistoryOutput(_controller);
                AddFormToAllForms(_frmResultHistoryOutput);
                //
                _frmViewResultHistoryOutput = new FrmViewResultHistoryOutput(_controller);
                AddFormToAllForms(_frmViewResultHistoryOutput);
                //
                _frmTransformation = new FrmTransformation(_controller);
                AddFormToAllForms(_frmTransformation);
                // Deformation toolstrip
                InitializeDeformationComboBoxes();
                InitializeComplexComboBoxes();
                // Converters
                tstbDeformationFactor.UnitConverter = new StringDoubleConverter();
                tstbAngle.UnitConverter = new StringAngleDegConverter();
                // Create the Keyboard Hook
                _keyboardHook = new KeyboardHook();
                // Capture the events
                _keyboardHook.KeyDown += KeyboardHook_KeyDown;
                // Install the hook
                _keyboardHook.Install();
            }
            catch
            {
                // If no error the splash is closed latter
                _splash?.BeginInvoke((MethodInvoker)delegate () { _splash.Close(); });
            }
            finally
            {
                // Set form size if visible - after top most
                if (_cmdOptions.ShowGui == "Yes") _controller.Settings.General.ApplyFormSize(this);
            }
            //
            if (!Debugger.IsAttached)
            {
                tsmiTest.Visible = false;
                tsmiCropStlPartWithCylinder.Visible = false;
                tsmiCropStlPartWithCube.Visible = false;
            }
        }
        //
        private async void FrmMain_Shown(object sender, EventArgs e)
        {
            // Set vtk control size
            UpdateVtkControlSize();
            Application.DoEvents(); // draws the menus
            //
            _vtk.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            // Set pass through control for the mouse wheel event
            this.PassThroughControl = _vtk;
            // Vtk
            // Reduce flicker
            _vtk.Visible = true;
            _vtk.ResetScaleBarPosition();   // start after the vtk size is set
            _vtk.SetZoomFactor(1000);       // set starting zoom larger that the object
            _controller.Redraw();
            // Move _vtk to the edge of the screen to hide the black background
            int left = _vtk.Left;
            _vtk.Left = _vtk.Width + 4;
            Application.DoEvents(); // draws the vtk
            _vtk.Visible = false;
            _vtk.Left = left;
            if (_vtkJs != null)
            {
                await _vtkJs.StartAsync();
                _vtkJs.BringToFront();
            }
            // Close splash 
            if (_splash != null) _splash.BeginInvoke((MethodInvoker)delegate () { _splash.Close(); });
            // At the end when vtk is loaded open the file
            string fileName = null;
            UnitSystemType unitSystemType = UnitSystemType.Undefined;
            //
            try
            {
                // Regeneration
                if (_cmdOptions.RegenerationFileName != null)
                {
                    WriteDataToOutput("Starting regeneration");
                    //
                    fileName = _cmdOptions.RegenerationFileName;
                    //
                    if (fileName == null)
                        throw new CaeException("The regeneration file name is null.");
                    else if (!File.Exists(fileName))
                        throw new CaeException("The regeneration file " + fileName + " does not exist.");
                    //
                    _controller.RegenerationWorkDirectory = _cmdOptions.WorkDirectory;
                    // Open
                    await Task.Run(() => OpenAsync(fileName, _controller.Open));
                    //
                    if (_cmdOptions.ParametersExportFileName != null)
                    {
                        // Parameters export
                        await Task.Run(() => _controller.ExportParametersToFile(_cmdOptions.ParametersExportFileName));
                    }
                    else
                    {
                        // Parameters override
                        if (_cmdOptions.Parameters != null)
                            _controller.AddOverriddenParametersFromString(_cmdOptions.Parameters);
                        // Regenerate
                        await Task.Run(() => _controller.RegenerateHistoryCommands(false, false, _cmdOptions.GetRegenerateType()));
                        // Check for errors
                        if (_controller.GetCommandCollectionErrors() != null && _controller.GetCommandCollectionErrors().Count > 0)
                            throw new CaeException("Failed to regenerate some commands.");
                        // Overwrite
                        if (_cmdOptions.Overwrite == "Yes")
                        {
                            WriteDataToOutput("Overwrite: " + fileName);
                            _controller.SaveToPmx(fileName);
                        }
                    }
                    // Exit
                    if (_cmdOptions.ExitAfterRegeneration == "Yes")
                    {
                        _closeAfterRegeneration = true;
                        WriteDataToOutput("Close application.");
                        Close();
                    }
                }
                else
                {
                    this.Activate();    // must be here - otherwise the main form is hidden when started from debugger
                    this.TopMost = false;
                    //
                    string parameters = null;
                    // Try to recover unsaved progress due to crushed PrePoMax
                    if (File.Exists(_controller.GetHistoryFileNameBin()))
                    {
                        if (MessageBoxes.ShowWarningQuestionOKCancel("A recovery file from a previous PrePoMax session exists. " +
                                                                     "Would you like to try to recover it?") == DialogResult.OK)
                        {
                            fileName = _controller.GetHistoryFileNameBin();
                            //
                            bool regenerateAll = MessageBoxes.ShowQuestionYesNo("Regenerate All",
                                "Select Yes to regenerate all commands or No to regenerate FE model commands only. " +
                                "All commands can be regenerated later using Edit → Regenerate All.") == DialogResult.Yes;
                            if (regenerateAll) parameters = Globals.RegenerateAll;
                        }
                    }
                    if (fileName == null)
                    {
                        // Open file from exe arguments
                        if (_cmdOptions.FileName != null)
                        {
                            fileName = _cmdOptions.FileName;
                            //
                            if (_cmdOptions.UnitSystem != null)
                            {
                                if (!Enum.TryParse(_cmdOptions.UnitSystem.ToUpper(), out unitSystemType))
                                    throw new CaeException("The unit system type " + _cmdOptions.UnitSystem + " is not supported.");
                                parameters = _cmdOptions.UnitSystem;
                            }
                        }
                        // Check for open last file
                        else if (_controller.Settings.General.OpenLastFile) fileName = _controller.OpenedFileName;
                    }
                    //
                    if (File.Exists(fileName))
                    {
                        fileName = Path.GetFullPath(fileName);  // change local file name to global
                        string extension = Path.GetExtension(fileName).ToLower();
                        HashSet<string> importExtensions = GetFileImportExtensions();
                        //
                        if (extension == ".pmx" || extension == ".pmh" || extension == ".frd")
                            await Task.Run(() => OpenAsync(fileName, _controller.Open, true, null, parameters));
                        else if (importExtensions.Contains(extension))
                        {
                            // Create new model
                            if (New(ModelSpaceEnum.ThreeD, unitSystemType))
                            {
                                // Import
                                await _controller.ImportFileAsync(fileName, false);
                                // Set to null, otherwise the previous OpenedFileName gets overwritten on Save
                                _controller.OpenedFileName = null;
                            }
                        }
                        else MessageBoxes.ShowError("The file name extension is not supported.");
                        //
                        SetFrontBackView(false, true);
                    }
                    else
                    {
                        _controller.CurrentView = ViewGeometryModelResults.Geometry;
                        //
                        UpdateRecentFilesThreadSafe();
                    }
                }
            }
            catch (Exception ex)
            {
                // Regeneration
                if (_controller.BatchRegenerationMode)
                {
                    throw new CaeException(ex.Message, ex);
                }
                else
                {
                    ExceptionTools.Show(this, ex);
                    _controller.ModelChanged = false;   // hide messageBox
                    tsmiNew_Click(null, null);
                }
            }
            finally
            {
                this.Activate();    // must be here - otherwise the main form is hidden when started from debugger
                this.TopMost = false;
                //
                if (FirstRunChecker.IsFirstRun(Application.StartupPath))
                {
                    if (_controller.BatchRegenerationMode) WriteDataToOutput("Starting regeneration");
                    else _controller.Settings.CheckWorkingDirectory();
                }
            }
            StartVtkJsSmokeFieldSwitch();
            StartVtkJsSmokePick();
            StartVtkJsSmokeAnnotation();
            StartVtkJsSmokeLargeSelection();
            StartVtkJsSmokeAnimation();
            StartVtkJsSmokeCamera();
            StartVtkJsSmokeBoxZoom();
            StartVtkJsSmokeVisibilityCamera();
            StartVtkJsSmokeExplodedPreview();
            StartVtkJsSmokeSectionView();
        }

        private void StartVtkJsSmokeFieldSwitch()
        {
            string requested = Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_FIELD");
            if (_vtkJs == null || string.IsNullOrWhiteSpace(requested)) return;
            string[] parts = requested.Split(new[] { '/' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return;
            string fieldName = parts[0].Trim();
            string componentName = parts[1].Trim();
            string triggerFile = Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_FIELD_TRIGGER");
            if (fieldName.Length == 0 || componentName.Length == 0) return;

            _vtkJsSmokeReadyTicks = 0;
            _vtkJsSmokeFieldTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _vtkJsSmokeFieldTimer.Tick += (sender, args) =>
            {
                if (_vtkJs == null || !_vtkJs.HasPublishedTopology || _controller.CurrentResult == null ||
                    _controller.CurrentFieldData == null) return;
                string[] componentNames = _controller.CurrentResult.GetFieldComponentNames(fieldName);
                if (componentNames == null || !componentNames.Contains(componentName))
                {
                    _vtkJs.WriteAcceptanceTrace("field switch unavailable requested='" + requested +
                                                "' components='" + string.Join(",", componentNames ?? new string[0]) + "'");
                    _vtkJsSmokeFieldTimer.Stop();
                    _vtkJsSmokeFieldTimer.Dispose();
                    _vtkJsSmokeFieldTimer = null;
                    return;
                }
                // The process smoke creates this file only after capturing its initial manifest baseline.
                if (!string.IsNullOrWhiteSpace(triggerFile) && !File.Exists(triggerFile)) return;
                if (string.IsNullOrWhiteSpace(triggerFile) && ++_vtkJsSmokeReadyTicks < 15) return;
                _vtkJsSmokeFieldTimer.Stop();
                _vtkJsSmokeFieldTimer.Dispose();
                _vtkJsSmokeFieldTimer = null;
                _vtkJs.WriteAcceptanceTrace("switching field requested='" + requested + "'");
                SetFieldData(fieldName, componentName,
                             _controller.CurrentFieldData.StepId,
                             _controller.CurrentFieldData.StepIncrementId);
            };
            _vtkJsSmokeFieldTimer.Start();
        }

        private void StartVtkJsSmokePick()
        {
            string triggerFile = Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_PICK_TRIGGER");
            if (_vtkJs == null || string.IsNullOrWhiteSpace(triggerFile)) return;
            _vtkJsSmokePickTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _vtkJsSmokePickTimer.Tick += (sender, args) =>
            {
                if (!_vtkJs.HasPublishedTopology || !_vtkJs.IsBrowserReady ||
                    _controller.CurrentResult == null || !File.Exists(triggerFile)) return;
                _vtkJsSmokePickTimer.Stop();
                _vtkJsSmokePickTimer.Dispose();
                _vtkJsSmokePickTimer = null;
                try
                {
                    string pickItem = Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_PICK_ITEM");
                    _vtkJs.WriteAcceptanceTrace("pick mode requested='" + pickItem + "'");
                    if (string.Equals(pickItem, "Surface", StringComparison.OrdinalIgnoreCase))
                        _controller.SetSelectItemToSurface();
                    else if (string.Equals(pickItem, "Geometry", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(pickItem, "GeometryPoint", StringComparison.OrdinalIgnoreCase))
                        _controller.SetSelectItemToGeometry();
                    else if (string.Equals(pickItem, "GeometryEdge", StringComparison.OrdinalIgnoreCase))
                        _controller.SetSelectItemToGeometryEdge();
                    else _controller.SetSelectItemToElement();
                    _vtkJs.TriggerAcceptancePick();
                }
                catch (Exception exception)
                {
                    _vtkJs.WriteAcceptanceTrace("pick mode setup failed: " + exception.Message);
                }
            };
            _vtkJsSmokePickTimer.Start();
        }

        private void StartVtkJsSmokeAnnotation()
        {
            bool exerciseRegular = string.Equals(Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_ANNOTATION"),
                                                 "1", StringComparison.Ordinal);
            bool exerciseExtrema = string.Equals(Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_EXTREMA"),
                                                 "1", StringComparison.Ordinal);
            bool exerciseInteraction = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_ANNOTATION_INTERACTION"),
                "1", StringComparison.Ordinal);
            if (_vtkJs == null || (!exerciseRegular && !exerciseExtrema && !exerciseInteraction)) return;
            _vtkJsSmokeAnnotationTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _vtkJsSmokeAnnotationTimer.Tick += (sender, args) =>
            {
                if (!_vtkJs.HasPublishedTopology || !_vtkJs.IsBrowserReady ||
                    _controller.CurrentResult == null || _controller.CurrentFieldData == null ||
                    _controller.DisplayedMesh == null) return;
                string requestedField = Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_FIELD");
                if (exerciseExtrema && !string.IsNullOrWhiteSpace(requestedField))
                {
                    string[] requestedParts = requestedField.Split(new[] { '/' }, 2,
                                                                   StringSplitOptions.RemoveEmptyEntries);
                    if (requestedParts.Length == 2 &&
                        (!string.Equals(_controller.CurrentFieldData.Name, requestedParts[0].Trim(), StringComparison.Ordinal) ||
                         !string.Equals(_controller.CurrentFieldData.Component, requestedParts[1].Trim(), StringComparison.Ordinal)))
                        return;
                }
                if ((exerciseRegular || exerciseInteraction) && _controller.DisplayedMesh.Nodes.Count == 0) return;
                _vtkJsSmokeAnnotationTimer.Stop();
                _vtkJsSmokeAnnotationTimer.Dispose();
                _vtkJsSmokeAnnotationTimer = null;
                if (exerciseRegular)
                {
                    CaeMesh.FeNode node = _controller.DisplayedMesh.Nodes.Values.First();
                    _controller.Annotations.AddMeasureAnnotation("VTK.js annotation smoke", node.Coor);
                    _vtkJs.WriteAcceptanceTrace("annotation requested");
                    _vtkJs.TraceAcceptanceAnnotationIdentity();
                }
                if (exerciseExtrema)
                {
                    string numberFormat = _controller.Settings.Annotations.GetNumberFormat();
                    _vtkJs.SetAnnotation(AnnotationContainer.MinAnnotationName, "", numberFormat, new double[3],
                                         true, true, true);
                    _vtkJs.SetAnnotation(AnnotationContainer.MaxAnnotationName, "", numberFormat, new double[3],
                                         true, true, true);
                    _vtkJs.WriteAcceptanceTrace("extrema requested field='" +
                                                _controller.CurrentFieldData.Name + "/" +
                                                _controller.CurrentFieldData.Component + "'");
                    _vtkJs.TraceAcceptanceExtrema();
                }
                if (exerciseInteraction)
                {
                    CaeMesh.FeNode node = _controller.DisplayedMesh.Nodes.Values.First();
                    string name = _controller.Annotations.GetFreeAnnotationName();
                    _controller.Annotations.AddAnnotation(new NodeAnnotation(name, node.Id));
                    _vtkJs.TriggerAcceptanceAnnotationInteraction(name);
                }
            };
            _vtkJsSmokeAnnotationTimer.Start();
        }
        private void StartVtkJsSmokeLargeSelection()
        {
            bool requested = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_LARGE_SELECTION"),
                "1", StringComparison.Ordinal);
            int capacityCount;
            if (!int.TryParse(Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_SELECTION_CAPACITY"),
                              out capacityCount)) capacityCount = 0;
            if (capacityCount < 0 || capacityCount > 8000000) capacityCount = 0;
            if (_vtkJs == null || (!requested && capacityCount == 0)) return;
            _vtkJsSmokeLargeSelectionTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _vtkJsSmokeLargeSelectionTimer.Tick += (sender, args) =>
            {
                if (!_vtkJs.HasPublishedTopology || !_vtkJs.IsBrowserReady || _controller.DisplayedMesh == null ||
                    (capacityCount == 0 && _controller.DisplayedMesh.Nodes.Count <= 100000)) return;
                _vtkJsSmokeLargeSelectionTimer.Stop();
                _vtkJsSmokeLargeSelectionTimer.Dispose();
                _vtkJsSmokeLargeSelectionTimer = null;
                int[] nodeIds;
                if (capacityCount > 0)
                {
                    nodeIds = Enumerable.Range(1, capacityCount).ToArray();
                    _vtkJs.SetSelection("node", nodeIds, new string[0]);
                }
                else
                {
                    nodeIds = _controller.DisplayedMesh.Nodes.Keys.ToArray();
                    _controller.SetSelectItemToNode();
                    SelectionChanged(nodeIds);
                }
                _vtkJs.WriteAcceptanceTrace("large selection requested ids=" + nodeIds.Length);
                _vtkJs.TraceAcceptanceLargeSelection(nodeIds.Length);
            };
            _vtkJsSmokeLargeSelectionTimer.Start();
        }
        private void StartVtkJsSmokeAnimation()
        {
            bool requested = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_ANIMATION"),
                "1", StringComparison.Ordinal);
            string triggerFile = Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_ANIMATION_TRIGGER");
            string frameTriggerFile = Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_ANIMATION_FRAME_TRIGGER");
            string animationMode = Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_ANIMATION_MODE") ??
                                   "ScaleFactor";
            bool sequenceRequested = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_ANIMATION_SEQUENCE"),
                "1", StringComparison.Ordinal);
            if (_vtkJs == null || !requested) return;
            bool animationPrepared = false;
            int targetFrame = 1;
            int animationFrameCount = 0;
            int lastRequestedFrame = -1;
            _vtkJsSmokeAnimationTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _vtkJsSmokeAnimationTimer.Tick += (sender, args) =>
            {
                if (!_vtkJs.HasPublishedTopology || !_vtkJs.IsBrowserReady ||
                    _controller.CurrentResult == null || _controller.CurrentFieldData == null ||
                    (!string.IsNullOrWhiteSpace(triggerFile) && !File.Exists(triggerFile))) return;
                if (!animationPrepared)
                {
                    try
                    {
                        int frameCount;
                        bool accepted;
                        if (string.Equals(animationMode, "ScaleFactor", StringComparison.OrdinalIgnoreCase))
                        {
                            frameCount = 3;
                            accepted = _controller.DrawScaleFactorAnimation(frameCount);
                        }
                        else if (string.Equals(animationMode, "TimeIncrements", StringComparison.OrdinalIgnoreCase))
                            accepted = _controller.DrawTimeIncrementAnimation(out frameCount);
                        else if (string.Equals(animationMode, "Harmonic", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!_controller.CurrentResult.ContainsComplexResults())
                                throw new InvalidOperationException(
                                    "The current result does not contain complex data required for harmonic animation.");
                            frameCount = 4;
                            accepted = _controller.DrawHarmonicAnimation(frameCount);
                        }
                        else throw new InvalidOperationException("Unknown animation smoke mode '" + animationMode + "'.");
                        _vtkJs.WriteAcceptanceTrace("animation setup requested type=" + animationMode +
                                                    " frames=" + frameCount);
                        if (!accepted)
                            throw new InvalidOperationException("PrePoMax rejected the " + animationMode + " animation.");
                        if (frameCount < 2)
                            throw new InvalidOperationException("The result contains only " + frameCount +
                                                                " frame; at least two are required.");
                        targetFrame = 1;
                        animationFrameCount = frameCount;
                        animationPrepared = true;
                    }
                    catch (Exception exception)
                    {
                        _vtkJs.WriteAcceptanceTrace("animation setup failed: " + exception.Message);
                        _vtkJsSmokeAnimationTimer.Stop();
                        _vtkJsSmokeAnimationTimer.Dispose();
                        _vtkJsSmokeAnimationTimer = null;
                    }
                    return;
                }
                if (!string.IsNullOrWhiteSpace(frameTriggerFile) && !File.Exists(frameTriggerFile)) return;
                try
                {
                    if (!string.IsNullOrWhiteSpace(frameTriggerFile))
                    {
                        string requestedText = File.ReadAllText(frameTriggerFile).Trim();
                        if (requestedText.Length > 0 && !int.TryParse(requestedText, out targetFrame))
                            throw new InvalidOperationException("Animation frame trigger is not an integer.");
                    }
                    if (targetFrame < 0 || targetFrame >= animationFrameCount)
                        throw new InvalidOperationException("Animation target frame " + targetFrame +
                                                            " is outside the available range.");
                    if (targetFrame == lastRequestedFrame) return;
                    SetAnimationFrame(targetFrame, false);
                    _vtkJs.WriteAcceptanceTrace("animation target requested type=" + animationMode +
                                                " frame=" + targetFrame);
                    lastRequestedFrame = targetFrame;
                    if (!sequenceRequested)
                    {
                        _vtkJsSmokeAnimationTimer.Stop();
                        _vtkJsSmokeAnimationTimer.Dispose();
                        _vtkJsSmokeAnimationTimer = null;
                    }
                }
                catch (Exception exception)
                {
                    _vtkJs.WriteAcceptanceTrace("animation setup failed: " + exception.Message);
                }
            };
            _vtkJsSmokeAnimationTimer.Start();
        }
        private void StartVtkJsSmokeCamera()
        {
            bool requested = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_CAMERA"),
                "1", StringComparison.Ordinal);
            if (_vtkJs == null || !requested) return;
            int cameraStage = 0;
            double retainedScale = 0;
            _vtkJsSmokeCameraTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _vtkJsSmokeCameraTimer.Tick += (sender, args) =>
            {
                if (!_vtkJs.HasPublishedTopology || !_vtkJs.IsBrowserReady || !HasRetainedVtkJsCamera() ||
                    _vtkJsCameraCommandPending) return;
                if (cameraStage == 0)
                {
                    GetViewParameters(out double[] position, out double[] focalPoint, out double[] viewUp,
                                      out retainedScale);
                    SetViewParameters(false, position, focalPoint, viewUp, retainedScale);
                }
                else if (cameraStage == 1) SetNormalView(false);
                else if (cameraStage == 2) SetVerticalView(false);
                else if (cameraStage == 3) AdjustVtkJsCameraDistanceAndClipping("acceptance-adjust");
                else
                {
                    _vtkJsSmokeCameraTimer.Stop();
                    _vtkJsSmokeCameraTimer.Dispose();
                    _vtkJsSmokeCameraTimer = null;
                    _vtkJs.WriteAcceptanceTrace("camera retained roundtrip scale=" +
                        retainedScale.ToString(CultureInfo.InvariantCulture));
                    return;
                }
                cameraStage++;
            };
            _vtkJsSmokeCameraTimer.Start();
        }

        private void StartVtkJsSmokeBoxZoom()
        {
            bool requested = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_BOX_ZOOM"),
                "1", StringComparison.Ordinal);
            if (_vtkJs == null || !requested) return;
            int stableCameraTicks = 0;
            double observedScale = 0;
            _vtkJsSmokeBoxZoomTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _vtkJsSmokeBoxZoomTimer.Tick += (sender, args) =>
            {
                if (!_vtkJs.HasPublishedTopology || !_vtkJs.IsBrowserReady || !HasRetainedVtkJsCamera() ||
                    _vtkJsCameraCommandPending)
                {
                    stableCameraTicks = 0;
                    return;
                }
                double scaleTolerance = Math.Max(1E-12, Math.Abs(_vtkJsCameraParallelScale) * 1E-9);
                if (Math.Abs(_vtkJsCameraParallelScale - observedScale) > scaleTolerance)
                {
                    observedScale = _vtkJsCameraParallelScale;
                    stableCameraTicks = 0;
                    return;
                }
                if (++stableCameraTicks < 5) return;
                _vtkJsSmokeBoxZoomTimer.Stop();
                _vtkJsSmokeBoxZoomTimer.Dispose();
                _vtkJsSmokeBoxZoomTimer = null;
                StartBoxZoom();
                _vtkJs.TriggerAcceptanceBoxZoom();
            };
            _vtkJsSmokeBoxZoomTimer.Start();
        }

        private void StartVtkJsSmokeVisibilityCamera()
        {
            bool requested = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_VISIBILITY_CAMERA"),
                "1", StringComparison.Ordinal);
            if (_vtkJs == null || !requested) return;
            int stage = 0;
            int stableCameraTicks = 0;
            double observedScale = 0;
            long requestedCameraRevision = 0;
            string actorName = null;
            _vtkJsSmokeVisibilityCameraTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _vtkJsSmokeVisibilityCameraTimer.Tick += (sender, args) =>
            {
                if (!_vtkJs.HasPublishedTopology || !_vtkJs.IsBrowserReady || !HasRetainedVtkJsCamera() ||
                    _vtkJsCameraCommandPending) return;
                if (stage == 0)
                {
                    double tolerance = Math.Max(1E-12, Math.Abs(_vtkJsCameraParallelScale) * 1E-9);
                    if (Math.Abs(_vtkJsCameraParallelScale - observedScale) > tolerance)
                    {
                        observedScale = _vtkJsCameraParallelScale;
                        stableCameraTicks = 0;
                        return;
                    }
                    if (++stableCameraTicks < 5) return;
                    actorName = _vtkJs.GetCapturedActorNames()
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
                    if (actorName == null)
                    {
                        _vtkJs.WriteAcceptanceTrace("visibility camera setup failed: no actor");
                        _vtkJsSmokeVisibilityCameraTimer.Stop();
                        _vtkJsSmokeVisibilityCameraTimer.Dispose();
                        _vtkJsSmokeVisibilityCameraTimer = null;
                        return;
                    }
                    requestedCameraRevision = _vtkJsCameraRevision;
                    HideActors(new[] { actorName }, false);
                    _vtkJs.WriteAcceptanceTrace("visibility camera hide requested actor='" + actorName + "'");
                    stage = 1;
                }
                else if (stage == 1 && _vtkJsCameraRevision > requestedCameraRevision)
                {
                    requestedCameraRevision = _vtkJsCameraRevision;
                    ShowActors(new[] { actorName }, false);
                    _vtkJs.WriteAcceptanceTrace("visibility camera show requested actor='" + actorName + "'");
                    stage = 2;
                }
                else if (stage == 2 && _vtkJsCameraRevision > requestedCameraRevision)
                {
                    _vtkJsSmokeVisibilityCameraTimer.Stop();
                    _vtkJsSmokeVisibilityCameraTimer.Dispose();
                    _vtkJsSmokeVisibilityCameraTimer = null;
                    _vtkJs.WriteAcceptanceTrace("visibility camera completed actor='" + actorName + "'");
                }
            };
            _vtkJsSmokeVisibilityCameraTimer.Start();
        }
        private void StartVtkJsSmokeExplodedPreview()
        {
            bool requested = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_EXPLODED_PREVIEW"),
                "1", StringComparison.Ordinal);
            if (_vtkJs == null || !requested) return;
            int stage = 0;
            int waitTicks = 0;
            string actorName = null;
            bool previewAcknowledged = false;
            bool exerciseControllerLifecycle = false;
            _vtkJsSmokeExplodedPreviewTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _vtkJsSmokeExplodedPreviewTimer.Tick += (sender, args) =>
            {
                if (!_vtkJs.HasPublishedTopology || !_vtkJs.IsBrowserReady) return;
                if (stage == 0)
                {
                    if (_controller.DisplayedMesh != null && _controller.DisplayedMesh.Parts.Count > 1)
                    {
                        exerciseControllerLifecycle = true;
                        _controller.TurnExplodedViewOnOff(true, 200);
                        _vtkJs.WriteAcceptanceTrace("exploded lifecycle activation requested");
                        stage = 10;
                        return;
                    }
                    actorName = _vtkJs.GetCapturedActorNames().FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
                    if (actorName == null) return;
                    PreviewExplodedView(new Dictionary<string, double[]>
                    {
                        { actorName, new double[] { 0.25, 0, 0 } }
                    }, false, 0, () =>
                    {
                        previewAcknowledged = true;
                        _vtkJs.WriteAcceptanceTrace("exploded preview handshake acknowledged actor='" + actorName + "'");
                    });
                    _vtkJs.WriteAcceptanceTrace("exploded preview requested actor='" + actorName + "'");
                    stage = 1;
                }
                else if (stage == 1 && previewAcknowledged && ++waitTicks >= 10)
                {
                    RemovePreviewedExplodedView(new[] { actorName });
                    _vtkJs.WriteAcceptanceTrace("exploded preview completed actor='" + actorName + "'");
                    _vtkJsSmokeExplodedPreviewTimer.Stop();
                    _vtkJsSmokeExplodedPreviewTimer.Dispose();
                    _vtkJsSmokeExplodedPreviewTimer = null;
                }
                else if (stage == 10 && exerciseControllerLifecycle && _controller.IsExplodedViewActive() &&
                         !_controller.IsExplodedViewTransitionActive())
                {
                    _vtkJs.WriteAcceptanceTrace("exploded lifecycle activation completed");
                    _controller.TurnExplodedViewOnOff(true, 200);
                    _vtkJs.WriteAcceptanceTrace("exploded lifecycle deactivation requested");
                    stage = 11;
                }
                else if (stage == 11 && !_controller.IsExplodedViewActive() &&
                         !_controller.IsExplodedViewTransitionActive())
                {
                    _vtkJs.WriteAcceptanceTrace("exploded lifecycle deactivation completed");
                    _vtkJsSmokeExplodedPreviewTimer.Stop();
                    _vtkJsSmokeExplodedPreviewTimer.Dispose();
                    _vtkJsSmokeExplodedPreviewTimer = null;
                }
            };
            _vtkJsSmokeExplodedPreviewTimer.Start();
        }
        private void StartVtkJsSmokeSectionView()
        {
            bool requested = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_SECTION_VIEW"),
                "1", StringComparison.Ordinal);
            if (_vtkJs == null) return;
            if (!requested) return;
            _vtkJs.WriteAcceptanceTrace("section view smoke armed");
            int stage = 0;
            int waitTicks = 0;
            double[] point = null;
            _vtkJsSmokeSectionViewTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _vtkJsSmokeSectionViewTimer.Tick += (sender, args) =>
            {
                // The VTK.js section builder consumes the published locator topology. Model-only INP
                // startup can publish that topology before Controller.DisplayedMesh is available.
                if (!_vtkJs.HasPublishedTopology || !_vtkJs.IsBrowserReady) return;
                if (stage == 0)
                {
                    point = _controller.GetSectionViewBBCenter().Coor;
                    _controller.CreateSectionView(point, new double[] { 1, 0, 0 }, false, Color.Orange);
                    _vtkJs.WriteAcceptanceTrace("section view create requested");
                    stage = 1;
                }
                else if (stage == 1 && ++waitTicks >= 20)
                {
                    _controller.UpdateSectionView(point, new double[] { 0, 1, 0 }, true, Color.Empty);
                    _vtkJs.WriteAcceptanceTrace("section view update requested");
                    waitTicks = 0;
                    stage = 2;
                }
                else if (stage == 2 && ++waitTicks >= 20)
                {
                    _controller.RemoveSectionView();
                    _vtkJs.WriteAcceptanceTrace("section view lifecycle completed");
                    _vtkJsSmokeSectionViewTimer.Stop();
                    _vtkJsSmokeSectionViewTimer.Dispose();
                    _vtkJsSmokeSectionViewTimer = null;
                }
            };
            _vtkJsSmokeSectionViewTimer.Start();
        }
        private void FrmMain_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized && _frmAnimation != null && _frmAnimation.Visible)
                _frmAnimation.UpdateAnimation();
            //
            if (Debugger.IsAttached)
            {
                WriteDataToOutput(Width + " : " + Height);
            }
        }
        private async void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_vtkJsSmokeFieldTimer != null)
            {
                _vtkJsSmokeFieldTimer.Stop();
                _vtkJsSmokeFieldTimer.Dispose();
                _vtkJsSmokeFieldTimer = null;
            }
            if (_vtkJsSmokePickTimer != null)
            {
                _vtkJsSmokePickTimer.Stop();
                _vtkJsSmokePickTimer.Dispose();
                _vtkJsSmokePickTimer = null;
            }
            if (_vtkJsSmokeAnnotationTimer != null)
            {
                _vtkJsSmokeAnnotationTimer.Stop();
                _vtkJsSmokeAnnotationTimer.Dispose();
                _vtkJsSmokeAnnotationTimer = null;
            }
            if (_vtkJsSmokeLargeSelectionTimer != null)
            {
                _vtkJsSmokeLargeSelectionTimer.Stop();
                _vtkJsSmokeLargeSelectionTimer.Dispose();
                _vtkJsSmokeLargeSelectionTimer = null;
            }
            if (_vtkJsSmokeAnimationTimer != null)
            {
                _vtkJsSmokeAnimationTimer.Stop();
                _vtkJsSmokeAnimationTimer.Dispose();
                _vtkJsSmokeAnimationTimer = null;
            }
            if (_vtkJsSmokeCameraTimer != null)
            {
                _vtkJsSmokeCameraTimer.Stop();
                _vtkJsSmokeCameraTimer.Dispose();
                _vtkJsSmokeCameraTimer = null;
            }
            if (_vtkJsSmokeBoxZoomTimer != null)
            {
                _vtkJsSmokeBoxZoomTimer.Stop();
                _vtkJsSmokeBoxZoomTimer.Dispose();
                _vtkJsSmokeBoxZoomTimer = null;
            }
            if (_vtkJsSmokeVisibilityCameraTimer != null)
            {
                _vtkJsSmokeVisibilityCameraTimer.Stop();
                _vtkJsSmokeVisibilityCameraTimer.Dispose();
                _vtkJsSmokeVisibilityCameraTimer = null;
            }
            if (_vtkJsSmokeExplodedPreviewTimer != null)
            {
                _vtkJsSmokeExplodedPreviewTimer.Stop();
                _vtkJsSmokeExplodedPreviewTimer.Dispose();
                _vtkJsSmokeExplodedPreviewTimer = null;
            }
            if (_vtkJsSmokeSectionViewTimer != null)
            {
                _vtkJsSmokeSectionViewTimer.Stop();
                _vtkJsSmokeSectionViewTimer.Dispose();
                _vtkJsSmokeSectionViewTimer = null;
            }
            string error = null;
            try
            {
                e.Cancel = false;   // close the form
                DialogResult response = DialogResult.None;
                // No write access
                if (_controller == null) return;
                // Analysis in progress - first check: it has to kill running analyses
                foreach (var entry in _controller.Jobs)
                {
                    if (entry.Value.JobStatus == JobStatus.Running)
                    {
                        if (!_controller.BatchRegenerationMode)
                            response = MessageBoxes.ShowWarningQuestionOKCancel("There is an analysis running. " +
                                "Closing will kill the analysis. Close anyway?");
                        else response = DialogResult.OK;
                        //
                        if (response == DialogResult.Cancel) e.Cancel = true;
                        else if (response == DialogResult.OK)
                        {
                            _controller.KillAllJobs();
                            error = "The analysis was killed by the user.";
                            throw new CaeException(error);
                        }
                        break;
                    }
                }
                // Regeneration in progress
                if (_controller.BatchRegenerationMode && !_closeAfterRegeneration)
                {
                    error = "PrePoMax closed by the user.";
                    throw new CaeException(error);
                }
                // Saving in progress
                if (tsslState.Text != Globals.ReadyText)
                {
                    if (!_controller.BatchRegenerationMode)
                        response = MessageBoxes.ShowWarningQuestionOKCancel("There is a task running. Close anyway?");
                    else response = DialogResult.OK;
                    //
                    if (response == DialogResult.Cancel) e.Cancel = true;
                    else if (response == DialogResult.OK && _controller.SavingFile)
                    {
                        // Wait for saving to finish
                        while (_controller.SavingFile) Thread.Sleep(100);
                    }
                }
                // Model changed
                else if (_controller.ModelChanged)
                {
                    if (!_controller.BatchRegenerationMode)
                        response = MessageBoxes.ShowWarningQuestionYesNoCancel("Save file before closing?");
                    else response = DialogResult.No;
                    //
                    if (response == DialogResult.Yes)
                    {
                        e.Cancel = true;                                // stop the form from closing before saving
                        await Task.Run(() => _controller.Save());       // save
                        Close();                                        // close the control
                    }
                    else if (response == DialogResult.Cancel) e.Cancel = true;
                }
                // Save form size and location and delete history files
                if (e.Cancel == false && _controller != null)
                {
                    if (_vtkJs != null)
                    {
                        await _vtkJs.StopAsync();
                        _vtkJs.Dispose();
                        _vtkJs = null;
                    }
                    if (!_controller.BatchRegenerationMode)
                    {
                        SettingsContainer settings = _controller.Settings;  // get a clone
                        settings.General.SaveFormSize(this);                // save form size
                        _controller.Settings = settings;                    // update values and save to file
                        //
                        _controller.DeleteHistoryFiles();
                        //
                        _vtk.Clear();
                        _vtk.Dispose();
                        _vtk = null;
                    }
                }
            }
            catch
            { 
                if (error != null) throw new CaeException(error);
            }
        }
        private void FrmMain_Move(object sender, EventArgs e)
        {
            if (_allForms == null) return;

            foreach (Form form in _allForms)
            {
                if (form.Visible)
                {
                    //form.Location = new Point(Left + _formLocation.X, Top + _formLocation.Y);
                }
            }
        }
        private void UpdateVtkControlSize()
        {
            // Update vtk control size
            Rectangle bounds = panelControl.Bounds;
            bounds.Inflate(-2, -2);
            if (_vtk.Bounds != bounds) _vtk.Bounds = bounds;
        }
        private static bool IsVtkJsRendererEnabled()
        {
            return string.Equals(Environment.GetEnvironmentVariable("PREPOMAX_RENDERER"), "vtkjs",
                                 StringComparison.OrdinalIgnoreCase);
        }
        private void RemoveRendererControls(Control parent)
        {
            parent.Controls.Remove(_vtk);
            if (_vtkJs != null) parent.Controls.Remove(_vtkJs);
        }
        private void AddRendererControls(Control parent)
        {
            parent.Controls.Add(_vtk);
            if (_vtkJs != null)
            {
                parent.Controls.Add(_vtkJs);
                _vtkJs.BringToFront();
            }
        }
        private bool TestWriteAccess()
        {
            try
            {
                string fileName = Tools.GetNonExistentRandomFileName(Application.StartupPath, ".test");
                File.WriteAllText(fileName, "");
                //
                File.Delete(fileName);
                //
                return true;
            }
            catch
            {
                return false;
            }
        }
        // Forms
        private void itemForm_VisibleChanged(object sender, EventArgs e)
        {
            Form form = sender as Form;
            bool menusActive = !form.Visible;
            //
            if (form is FrmSelectItemSet) return;
            //
            if (form is FrmAnimation)
            {
                SetMenuAndToolStripVisibilityByAnimation(!form.Visible);
            }
            else
            {
                SetMenuAndToolStripVisibilityByItemForm(menusActive);
            }
            // This gets also called from item selection form: by angle, by edge ...
            if (menusActive)
            {
                UpdateHighlightFromTree();
                SaveFormLocation(form);
                //
                _controller.SetSelectByToDefault();
                //
                Focus();
            }
        }
        private void itemForm_Move(object sender, EventArgs e)
        {
            Form form = sender as Form;
            //Size screenSize =  Screen.GetWorkingArea(form).Size;
            //if (form.Left < 0) form.Left = 0;
            //else if (form.Left + form.Width > screenSize.Width) form.Left = screenSize.Width - form.Width;
            //if (form.Top < 0) form.Top = 0;
            //else if (form.Top + form.Height > screenSize.Height) form.Top = screenSize.Height - form.Height;
            SaveFormLocation(form);
        }
        // Keyboard
        private void KeyboardHook_KeyDown(KeyboardHook.VKeys vKey)
        {
            if (this == ActiveForm && !tbOutput.Focused && !_modelTree.Focused)
            {
                Keys key = (Keys)vKey;
                //
                if (key == Keys.Escape)
                {
                    if (!_vtk.IsBoxSelectionActive) CloseAllForms();
                }
                // Model tree
                else if (_modelTree.ActiveControl == null || !_modelTree.ActiveControl.Focused)
                {
                    Control focusedControl = FindFocusedControl(this);
                    // Check for toolstrip
                    if (focusedControl != null && focusedControl.Parent is ToolStripFocus) { }
                    // Check for search box
                    else if (_modelTree.ActiveControl != null && _modelTree.ActiveControl is SearchTextBox) { }
                    // Check for annotation editor
                    else if (aeAnnotationTextEditor.Visible) { }
                    // Forward to tree
                    else
                    {
                        if (_modelTree.EditModeActive) { }
                        else
                        {
                            _modelTree.cltv_KeyDown(this, new KeyEventArgs(key));
                            _vtk.Focus();   // this prevents that the coders lab tree calls _modelTree.cltv_KeyDown again
                        }
                    }
                }
            }
        }
        public static Control FindFocusedControl(Control control)
        {
            var container = control as IContainerControl;
            while (container != null)
            {
                control = container.ActiveControl;
                container = control as IContainerControl;
            }
            return control;
        }
        //
        private void timerOutput_Tick(object sender, EventArgs e)
        {
            tbOutput.Lines = outputLines;
            tbOutput.SelectionStart = tbOutput.Text.Length;
            tbOutput.ScrollToCaret();
            timerOutput.Stop();
        }

        #region ModelTree Events ###################################################################################################
        //
        internal void ModelTree_ViewEvent(ViewType viewType)
        {
            try
            {
                if ((viewType == ViewType.Geometry && GetCurrentView() == ViewGeometryModelResults.Geometry) ||
                    (viewType == ViewType.Model && GetCurrentView() == ViewGeometryModelResults.Model) ||
                    (viewType == ViewType.Results && GetCurrentView() == ViewGeometryModelResults.Results) ||
                    (viewType == ViewType.Fatigue && GetCurrentView() == ViewGeometryModelResults.Fatigue) ||
                    (viewType == ViewType.BaramMesh && GetCurrentView() == ViewGeometryModelResults.BaramMesh) ||
                    (viewType == ViewType.BaramFlow && GetCurrentView() == ViewGeometryModelResults.BaramFlow)) return;
                //
                CloseAllForms();
                _controller.SelectBy = vtkSelectBy.Default;
                //
                if (viewType == ViewType.Geometry) _controller.CurrentView = ViewGeometryModelResults.Geometry;
                else if (viewType == ViewType.Model) _controller.CurrentView = ViewGeometryModelResults.Model;
                else if (viewType == ViewType.Results) _controller.CurrentView = ViewGeometryModelResults.Results;
                else if (viewType == ViewType.Fatigue) _controller.CurrentView = ViewGeometryModelResults.Fatigue;
                else if (viewType == ViewType.BaramMesh) _controller.CurrentView = ViewGeometryModelResults.BaramMesh;
                else if (viewType == ViewType.BaramFlow) _controller.CurrentView = ViewGeometryModelResults.BaramFlow;
                else throw new NotSupportedException();
                //
                if (viewType != ViewType.Fatigue && viewType != ViewType.BaramMesh && viewType != ViewType.BaramFlow)
                    _advisorControl?.PrepareControls(viewType);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void ModelTree_SelectEvent(NamedClass[] items, bool mouseOver)
        {
            try
            {
                _controller.Highlight3DObjects(items, mouseOver);
            }
            catch { }
        }
        private void ModelTree_BaramMeshActionEvent(string nodeName)
        {
            try
            {
                if (IsBaramMeshGeometryNodeName(nodeName))
                {
                    RenameBaramMeshGeometryFromTree(nodeName, null);
                }
                else if (nodeName == "Case")
                {
                    CreateBaramMeshCaseFromTree();
                }
                else if (nodeName == "Geometry")
                {
                    OpenBaramMeshGeometryEditor(true);
                }
                else if (nodeName == "Castellation")
                {
                    OpenBaramMeshCastellationEditor();
                }
                else if (nodeName == "Snap")
                {
                    OpenBaramMeshSnapEditor();
                }
                else if (nodeName == "Export")
                {
                    OpenBaramMeshExportEditor();
                }
                else
                {
                    SetBaramMeshWorkflowStep(nodeName);
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void ModelTree_BaramMeshRunEvent(string nodeName)
        {
            try
            {
                if (nodeName == "Castellation") RunBaramMeshCastellationFromTree();
                else if (nodeName == "Snap") RunBaramMeshSnapFromTree();
                else ModelTree_BaramMeshActionEvent(nodeName);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void ModelTree_BaramMeshRenameEvent(string nodeName, string newName)
        {
            try
            {
                RenameBaramMeshGeometryFromTree(nodeName, newName);
            }
            catch (Exception ex)
            {
                UpdateBaramMeshGeometryTree();
                ExceptionTools.Show(this, ex);
            }
        }
        private void ModelTree_BaramMeshSelectEvent(string[] nodeNames)
        {
            try
            {
                List<int> ids = new List<int>();
                if (nodeNames != null)
                {
                    foreach (string nodeName in nodeNames)
                    {
                        int id;
                        if (TryGetBaramMeshGeometryId(nodeName, out id)) ids.Add(id);
                    }
                }
                _controller.HighlightBaramMeshGeometryVertices(ids.ToArray());
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void ModelTree_BaramMeshAssignEvent(string[] nodeNames, string assignment)
        {
            try
            {
                List<int> ids = new List<int>();
                if (nodeNames != null)
                {
                    foreach (string nodeName in nodeNames)
                    {
                        int id;
                        if (TryGetBaramMeshGeometryId(nodeName, out id)) ids.Add(id);
                    }
                }
                AssignBaramMeshSurfaces(ids.ToArray(), assignment);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void ModelTree_BaramMeshAssignRemainingWallsEvent()
        {
            try
            {
                AssignRemainingBaramMeshWalls();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void ModelTree_BaramMeshGroupWallsEvent()
        {
            try
            {
                GroupBaramMeshWalls();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void ModelTree_BaramFlowActionEvent(string nodeName)
        {
            try
            {
                if (nodeName == "Case")
                {
                    OpenBaramFlowProjectFromTree();
                    return;
                }
                //
                BaramFlowProject project = _controller.GetBaramFlowProject();
                if (project == null)
                {
                    MessageBoxes.ShowWarning("Open a BaramFlow project before editing solver settings.");
                    return;
                }
                //
                BaramFlowSection section = GetBaramFlowSection(nodeName);
                _controller.SetBaramFlowSectionCommand(section);
                WriteDataToOutput("BaramFlow section: " + nodeName);
                if (section == BaramFlowSection.General) EditBaramFlowGeneral();
                else if (section == BaramFlowSection.Models) EditBaramFlowModels();
                else if (section == BaramFlowSection.Materials) EditBaramFlowMaterial();
                else if (section == BaramFlowSection.BoundaryConditions) EditBaramFlowBoundaryConditions();
                else if (section == BaramFlowSection.NumericalConditions) EditBaramFlowNumericalConditions();
                else if (section == BaramFlowSection.RunConditions) EditBaramFlowRunConditions();
                else if (section == BaramFlowSection.Run) RunBaramFlowOpenFoamAsync();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void OpenBaramFlowProjectFromTree()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a BaramFlow .bf project directory.";
                dialog.ShowNewFolderButton = false;
                //
                BaramFlowProject project = _controller.GetBaramFlowProject();
                if (project != null && Directory.Exists(project.ProjectDirectory))
                    dialog.SelectedPath = project.ProjectDirectory;
                else
                {
                    string workDirectory = _controller.Settings.GetWorkDirectory();
                    if (!string.IsNullOrWhiteSpace(workDirectory) && Directory.Exists(workDirectory))
                        dialog.SelectedPath = workDirectory;
                }
                //
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _controller.OpenBaramFlowProjectCommand(dialog.SelectedPath);
                if (_controller.CurrentView == ViewGeometryModelResults.BaramFlow)
                    _controller.DrawBaramFlow(true);
            }
        }
        private static BaramFlowSection GetBaramFlowSection(string nodeName)
        {
            if (nodeName == "General") return BaramFlowSection.General;
            if (nodeName == "Models") return BaramFlowSection.Models;
            if (nodeName == "Materials") return BaramFlowSection.Materials;
            if (nodeName == "Cell Zone Conditions") return BaramFlowSection.CellZoneConditions;
            if (nodeName == "Boundary Conditions") return BaramFlowSection.BoundaryConditions;
            if (nodeName == "Dynamic Mesh") return BaramFlowSection.DynamicMesh;
            if (nodeName == "Reference Values") return BaramFlowSection.ReferenceValues;
            if (nodeName == "Numerical Conditions") return BaramFlowSection.NumericalConditions;
            if (nodeName == "Monitors") return BaramFlowSection.Monitors;
            if (nodeName == "Initialization") return BaramFlowSection.Initialization;
            if (nodeName == "Run Conditions") return BaramFlowSection.RunConditions;
            if (nodeName == "Run") return BaramFlowSection.Run;
            if (nodeName == "Reduced Order Model") return BaramFlowSection.ReducedOrderModel;
            if (nodeName == "Scaffolds") return BaramFlowSection.Scaffolds;
            if (nodeName == "Graphics") return BaramFlowSection.Graphics;
            if (nodeName == "Reports") return BaramFlowSection.Reports;
            throw new NotSupportedException("Unsupported BaramFlow section: " + nodeName);
        }
        private void EditBaramFlowGeneral()
        {
            BaramFlowProject project = _controller.GetBaramFlowProject();
            if (project == null)
                throw new InvalidOperationException("Open a BaramFlow project before editing General settings.");
            using (FrmBaramFlowGeneral form = new FrmBaramFlowGeneral(project.General))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.UpdateBaramFlowGeneralSettingsCommand(form.Settings);
            }
        }
        private void EditBaramFlowModels()
        {
            BaramFlowProject project = _controller.GetBaramFlowProject();
            if (project == null)
                throw new InvalidOperationException("Open a BaramFlow project before editing Models settings.");
            using (FrmBaramFlowModels form = new FrmBaramFlowModels(project.General, project.Models))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.UpdateBaramFlowModelsSettingsCommand(form.Settings);
            }
        }
        private void EditBaramFlowMaterial()
        {
            BaramFlowProject project = _controller.GetBaramFlowProject();
            if (project == null)
                throw new InvalidOperationException("Open a BaramFlow project before editing Material settings.");
            using (FrmBaramFlowMaterial form = new FrmBaramFlowMaterial(project.General, project.Material))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.UpdateBaramFlowMaterialSettingsCommand(form.Settings);
            }
        }
        private void EditBaramFlowBoundaryConditions()
        {
            BaramFlowProject project = _controller.GetBaramFlowProject();
            if (project == null)
                throw new InvalidOperationException("Open a BaramFlow project before editing Boundary Conditions.");
            project.RefreshBoundaryConditions();
            using (FrmBaramFlowBoundaryConditions form =
                   new FrmBaramFlowBoundaryConditions(project.BoundaryConditions))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.UpdateBaramFlowBoundarySettingsCommand(form.Settings);
            }
        }
        private void EditBaramFlowNumericalConditions()
        {
            BaramFlowProject project = _controller.GetBaramFlowProject();
            if (project == null)
                throw new InvalidOperationException("Open a BaramFlow project before editing Numerical Conditions.");
            using (FrmBaramFlowNumericalConditions form =
                   new FrmBaramFlowNumericalConditions(project.General, project.Models, project.NumericalConditions))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.UpdateBaramFlowNumericalSettingsCommand(form.Settings);
            }
        }
        private void EditBaramFlowRunConditions()
        {
            BaramFlowProject project = _controller.GetBaramFlowProject();
            if (project == null)
                throw new InvalidOperationException("Open a BaramFlow project before editing Run Conditions.");
            using (FrmBaramFlowRunConditions form =
                   new FrmBaramFlowRunConditions(project.General, project.RunConditions))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.UpdateBaramFlowRunSettingsCommand(form.Settings);
            }
        }
        private async void RunBaramFlowOpenFoamAsync()
        {
            if (!SetStateWorking(Globals.SolvingText, true))
            {
                MessageBoxes.ShowWarning("Another task is already running.");
                return;
            }
            try
            {
                await Task.Run(() => _controller.RunBaramFlowOpenFoam());
            }
            catch (Exception ex)
            {
                WriteDataToOutput("BaramFlow OpenFOAM execution failed: " + ex.Message);
                MessageBoxes.ShowError(ex.Message);
            }
            finally
            {
                SetStateReady(Globals.SolvingText);
            }
        }
        private void CreateBaramMeshCaseFromTree()
        {
            if (!_controller.ModelInitialized)
            {
                MessageBoxes.ShowWarning("Create or open a model before creating a BaramMesh case.");
                return;
            }
            //
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the BaramMesh case directory.";
                dialog.ShowNewFolderButton = true;
                //
                BaramMeshProject project = _controller.GetBaramMeshProject();
                if (project != null && Directory.Exists(project.ProjectDirectory))
                    dialog.SelectedPath = project.ProjectDirectory;
                else
                {
                    string workDirectory = _controller.Settings.GetWorkDirectory();
                    if (!string.IsNullOrWhiteSpace(workDirectory) && Directory.Exists(workDirectory))
                        dialog.SelectedPath = workDirectory;
                }
                //
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                //
                _controller.CreateBaramMeshCaseCommand(dialog.SelectedPath);
                UpdateBaramMeshGeometryTree();
                WriteDataToOutput("BaramMesh case directory: " + dialog.SelectedPath);
                OpenBaramMeshGeometryEditor(true);
            }
        }
        private void OpenBaramMeshGeometryEditor(bool seedModelIfEmpty)
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before editing geometry.");
                return;
            }
            //
            BaramMeshConfiguration configuration = project.Configuration != null
                                                   ? project.Configuration.DeepClone()
                                                   : new BaramMeshConfiguration();
            BaramBounds bounds = _controller.GetBaramMeshDefaultBounds();
            string[] partNames = _controller.GetBaramMeshGeometryPartNames();
            using (FrmBaramMeshGeometry form = new FrmBaramMeshGeometry(configuration, bounds, seedModelIfEmpty, partNames,
                                                                        project.ProjectDirectory))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.UpdateBaramMeshConfigurationCommand(form.Configuration);
                _controller.ExportBaramMeshGeometryCommand();
                UpdateBaramMeshGeometryTree();
                if (_controller.CurrentView == ViewGeometryModelResults.BaramMesh) _controller.Redraw(true);
                WriteDataToOutput("BaramMesh geometry definitions: " + form.Configuration.Geometry.Count);
            }
        }
        private void OpenBaramMeshBaseGridEditor()
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before editing the base grid.");
                return;
            }
            //
            BaramMeshConfiguration configuration = project.Configuration != null
                                                   ? project.Configuration.DeepClone()
                                                   : new BaramMeshConfiguration();
            BaramBounds bounds = _controller.GetBaramMeshDefaultBounds();
            using (FrmBaramMeshBaseGrid form = new FrmBaramMeshBaseGrid(configuration, bounds))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.UpdateBaramMeshConfigurationCommand(form.Configuration);
                _controller.WriteBaramMeshBaseGridCommand();
                UpdateBaramMeshGeometryTree();
                if (_controller.CurrentView == ViewGeometryModelResults.BaramMesh) _controller.Redraw(true);
                WriteDataToOutput("BaramMesh base grid dictionary: " + _controller.GetBaramMeshBlockMeshDictFileName());
                if (form.RunOpenFoam) RunBaramMeshOpenFoamStepAsync(BaramStep.BaseGrid);
            }
        }
        private void OpenBaramMeshCastellationEditor()
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before editing castellation.");
                return;
            }
            //
            BaramMeshConfiguration configuration = project.Configuration != null
                                                   ? project.Configuration.DeepClone()
                                                   : new BaramMeshConfiguration();
            using (FrmBaramMeshCastellation form = new FrmBaramMeshCastellation(configuration))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.UpdateBaramMeshConfigurationCommand(form.Configuration);
                _controller.WriteBaramMeshCastellationCommand();
                UpdateBaramMeshGeometryTree();
                if (_controller.CurrentView == ViewGeometryModelResults.BaramMesh) _controller.Redraw(true);
                WriteDataToOutput("BaramMesh castellation dictionary: " +
                                  _controller.GetBaramMeshSnappyHexMeshDictFileName());
                if (form.RunOpenFoam) RunBaramMeshOpenFoamStepAsync(BaramStep.Castellation);
            }
        }
        private void RunBaramMeshCastellationFromTree()
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before running castellation.");
                return;
            }
            if (project.Configuration == null)
            {
                MessageBoxes.ShowWarning("Define BaramMesh geometry before running castellation.");
                return;
            }
            //
            _controller.WriteBaramMeshCastellationCommand();
            UpdateBaramMeshGeometryTree();
            if (_controller.CurrentView == ViewGeometryModelResults.BaramMesh) _controller.Redraw(true);
            WriteDataToOutput("BaramMesh castellation dictionary: " +
                              _controller.GetBaramMeshSnappyHexMeshDictFileName());
            WriteDataToOutput("BaramMesh Castellation run requested.");
            RunBaramMeshOpenFoamStepAsync(BaramStep.Castellation);
        }
        private void OpenBaramMeshSnapEditor()
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before editing snap.");
                return;
            }
            BaramMeshConfiguration configuration = project.Configuration != null
                                                   ? project.Configuration.DeepClone()
                                                   : new BaramMeshConfiguration();
            using (FrmBaramMeshSnap form = new FrmBaramMeshSnap(configuration))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.UpdateBaramMeshConfigurationCommand(form.Configuration);
                _controller.WriteBaramMeshSnapCommand();
                UpdateBaramMeshGeometryTree();
                if (_controller.CurrentView == ViewGeometryModelResults.BaramMesh) _controller.Redraw(true);
                WriteDataToOutput("BaramMesh snap dictionary: " +
                                  _controller.GetBaramMeshSnappyHexMeshDictFileName());
                if (form.RunOpenFoam) RunBaramMeshOpenFoamStepAsync(BaramStep.Snap);
            }
        }
        private void RunBaramMeshSnapFromTree()
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before running snap.");
                return;
            }
            if (project.Configuration == null)
            {
                MessageBoxes.ShowWarning("Define the BaramMesh configuration before running snap.");
                return;
            }
            _controller.WriteBaramMeshSnapCommand();
            UpdateBaramMeshGeometryTree();
            WriteDataToOutput("BaramMesh snap dictionary: " +
                              _controller.GetBaramMeshSnappyHexMeshDictFileName());
            WriteDataToOutput("BaramMesh Snap run requested.");
            RunBaramMeshOpenFoamStepAsync(BaramStep.Snap);
        }
        private void OpenBaramMeshExportEditor()
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before exporting it.");
                return;
            }
            if (project.Configuration == null)
            {
                MessageBoxes.ShowWarning("Define the BaramMesh configuration before exporting it.");
                return;
            }
            //
            using (FrmBaramMeshExport form = new FrmBaramMeshExport(project.ProjectDirectory))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _controller.ExportBaramMeshCaseCommand(form.ProjectDirectory);
                UpdateBaramMeshGeometryTree();
            }
        }
        private async void RunBaramMeshOpenFoamStepAsync(BaramStep step)
        {
            if (!SetStateWorking(Globals.MeshingText, true))
            {
                MessageBoxes.ShowWarning("Another task is already running.");
                return;
            }
            try
            {
                await Task.Run(() => _controller.RunBaramMeshOpenFoamStep(step));
            }
            catch (Exception ex)
            {
                WriteDataToOutput("BaramMesh OpenFOAM execution failed: " + ex.Message);
                MessageBoxes.ShowError(ex.Message);
            }
            finally
            {
                SetStateReady(Globals.MeshingText);
                if (_controller.CurrentView == ViewGeometryModelResults.BaramMesh) _controller.DrawBaramMesh(true);
            }
        }
        private bool IsBaramMeshGeometryNodeName(string nodeName)
        {
            return !string.IsNullOrWhiteSpace(nodeName) &&
                   nodeName.StartsWith(BaramMeshGeometryNodePrefix, StringComparison.Ordinal);
        }
        private bool TryGetBaramMeshGeometryId(string nodeName, out int id)
        {
            id = 0;
            if (!IsBaramMeshGeometryNodeName(nodeName)) return false;
            string text = nodeName.Substring(BaramMeshGeometryNodePrefix.Length);
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        }
        private void RenameBaramMeshGeometryFromTree(string nodeName, string requestedName)
        {
            int id;
            if (!TryGetBaramMeshGeometryId(nodeName, out id)) return;
            //
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null || project.Configuration == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before renaming geometry.");
                return;
            }
            //
            BaramMeshConfiguration configuration = project.Configuration.DeepClone();
            BaramGeometry geometry;
            if (!configuration.Geometry.TryGetValue(id, out geometry))
            {
                MessageBoxes.ShowWarning("The selected BaramMesh geometry entry no longer exists.");
                UpdateBaramMeshGeometryTree();
                return;
            }
            //
            string newName = string.IsNullOrWhiteSpace(requestedName)
                                 ? PromptBaramMeshGeometryName(geometry.Name)
                                 : requestedName;
            if (string.IsNullOrWhiteSpace(newName)) return;
            const string fsiDisplaySuffix = " [FSI Interface]";
            if (geometry.BoundaryRole == BaramBoundaryRole.FsiInterface &&
                newName.EndsWith(fsiDisplaySuffix, StringComparison.OrdinalIgnoreCase))
                newName = newName.Substring(0, newName.Length - fsiDisplaySuffix.Length);
            newName = BaramGeometryFactory.SanitizeGeometryName(newName);
            //
            foreach (KeyValuePair<int, BaramGeometry> entry in configuration.Geometry)
            {
                if (entry.Key != id && string.Equals(entry.Value.Name, newName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Duplicate BaramMesh geometry name: " + newName);
            }
            //
            geometry.Name = newName;
            configuration.Geometry[id] = geometry;
            _controller.UpdateBaramMeshConfigurationCommand(configuration);
            _controller.ExportBaramMeshGeometryCommand();
            UpdateBaramMeshGeometryTree();
            if (_controller.CurrentView == ViewGeometryModelResults.BaramMesh) _controller.Redraw(true);
            WriteDataToOutput("Rename BaramMesh geometry: " + newName);
        }
        private void AssignBaramMeshSurfaces(int[] ids, string assignment)
        {
            if (ids == null || ids.Length == 0) return;
            //
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null || project.Configuration == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before assigning BaramMesh surfaces.");
                return;
            }
            //
            string baseName = NormalizeBaramMeshBoundaryAssignment(assignment);
            BaramBoundaryRole boundaryRole = GetBaramMeshBoundaryRole(baseName);
            BaramMeshConfiguration configuration = project.Configuration.DeepClone();
            foreach (int id in ids.Distinct())
            {
                BaramGeometry geometry;
                if (!configuration.Geometry.TryGetValue(id, out geometry)) continue;
                if (geometry.GeometryType != BaramGeometryType.Surface) continue;
                if (IsBaramMeshBaseGridGeometry(id, geometry, configuration)) continue;
                //
                geometry.Name = GetUniqueBaramMeshGeometryName(configuration, baseName, id);
                geometry.CfdType = BaramCfdType.Boundary;
                geometry.BoundaryRole = boundaryRole;
                configuration.Geometry[id] = geometry;
            }
            ApplyBaramMeshConfigurationFromTree(configuration, "Assign BaramMesh surface: " + baseName);
        }
        private void AssignRemainingBaramMeshWalls()
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null || project.Configuration == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before assigning BaramMesh surfaces.");
                return;
            }
            //
            BaramMeshConfiguration configuration = project.Configuration.DeepClone();
            int count = 0;
            foreach (int id in configuration.Geometry.Keys.ToArray())
            {
                BaramGeometry geometry = configuration.Geometry[id];
                if (geometry.GeometryType != BaramGeometryType.Surface) continue;
                if (IsBaramMeshBaseGridGeometry(id, geometry, configuration)) continue;
                if (GetEffectiveBaramMeshBoundaryRole(geometry) != BaramBoundaryRole.Unassigned) continue;
                //
                geometry.Name = GetUniqueBaramMeshGeometryName(configuration, "wall", id);
                geometry.CfdType = BaramCfdType.Boundary;
                geometry.BoundaryRole = BaramBoundaryRole.Wall;
                configuration.Geometry[id] = geometry;
                count++;
            }
            if (count == 0)
            {
                MessageBoxes.ShowWarning("There are no unassigned BaramMesh surfaces.");
                return;
            }
            ApplyBaramMeshConfigurationFromTree(configuration, "Assign remaining BaramMesh surfaces as walls: " + count.ToString());
        }
        private void GroupBaramMeshWalls()
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null || project.Configuration == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before grouping BaramMesh surfaces.");
                return;
            }
            if (string.IsNullOrWhiteSpace(project.ProjectDirectory))
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before grouping wall surfaces.");
                return;
            }
            //
            BaramMeshConfiguration configuration = project.Configuration.DeepClone();
            List<int> wallIds = new List<int>();
            List<string> fileNames = new List<string>();
            foreach (KeyValuePair<int, BaramGeometry> entry in configuration.Geometry)
            {
                BaramGeometry geometry = entry.Value;
                if (geometry.GeometryType != BaramGeometryType.Surface) continue;
                if (IsBaramMeshBaseGridGeometry(entry.Key, geometry, configuration)) continue;
                if (GetEffectiveBaramMeshBoundaryRole(geometry) != BaramBoundaryRole.Wall) continue;
                //
                string fileName;
                if (!BaramGeometryFactory.TryGetExternalStlFileName(geometry.Path, out fileName))
                    throw new InvalidOperationException("Only external STL wall surfaces can be grouped.");
                if (!File.Exists(fileName)) throw new FileNotFoundException("The STL file does not exist.", fileName);
                //
                wallIds.Add(entry.Key);
                fileNames.Add(fileName);
            }
            if (wallIds.Count < 2)
            {
                MessageBoxes.ShowWarning("At least two wall surfaces are required for grouping.");
                return;
            }
            //
            string target = Path.Combine(project.ProjectDirectory, "geometry", "assigned");
            AssignedSplitStlFile file = BaramStlFeatureSplitter.WriteMergedStlFiles(fileNames, "wall", target);
            foreach (int id in wallIds) configuration.Geometry.Remove(id);
            int mergedId = BaramGeometryFactory.AddImportedStlSurface(configuration, file.FileName, file.Name);
            configuration.Geometry[mergedId].BoundaryRole = BaramBoundaryRole.Wall;
            ApplyBaramMeshConfigurationFromTree(configuration, "Group BaramMesh wall surfaces: " + wallIds.Count.ToString());
        }
        private void ApplyBaramMeshConfigurationFromTree(BaramMeshConfiguration configuration, string output)
        {
            _controller.UpdateBaramMeshConfigurationCommand(configuration);
            _controller.ExportBaramMeshGeometryCommand();
            UpdateBaramMeshGeometryTree();
            if (_controller.CurrentView == ViewGeometryModelResults.BaramMesh) _controller.Redraw(true);
            WriteDataToOutput(output);
        }
        private string NormalizeBaramMeshBoundaryAssignment(string assignment)
        {
            string name = BaramGeometryFactory.SanitizeGeometryName(assignment).ToLowerInvariant();
            if (name == "inlet" || name == "outlet" || name == "symmetry" || name == "wall" ||
                name == "fsi_interface") return name;
            throw new InvalidOperationException("Unsupported BaramMesh surface assignment: " + assignment);
        }
        private BaramBoundaryRole GetBaramMeshBoundaryRole(string assignment)
        {
            switch (NormalizeBaramMeshBoundaryAssignment(assignment))
            {
                case "inlet": return BaramBoundaryRole.Inlet;
                case "outlet": return BaramBoundaryRole.Outlet;
                case "symmetry": return BaramBoundaryRole.Symmetry;
                case "wall": return BaramBoundaryRole.Wall;
                case "fsi_interface": return BaramBoundaryRole.FsiInterface;
                default: throw new InvalidOperationException("Unsupported BaramMesh surface assignment: " + assignment);
            }
        }
        private string GetUniqueBaramMeshGeometryName(BaramMeshConfiguration configuration, string baseName, int ignoredId)
        {
            baseName = BaramGeometryFactory.SanitizeGeometryName(baseName);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "wall";
            //
            string name = baseName;
            int suffix = 1;
            while (BaramMeshGeometryNameExists(configuration, name, ignoredId))
            {
                name = baseName + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }
            return name;
        }
        private bool BaramMeshGeometryNameExists(BaramMeshConfiguration configuration, string name, int ignoredId)
        {
            foreach (KeyValuePair<int, BaramGeometry> entry in configuration.Geometry)
            {
                if (entry.Key == ignoredId) continue;
                if (string.Equals(entry.Value.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        private BaramBoundaryRole GetEffectiveBaramMeshBoundaryRole(BaramGeometry geometry)
        {
            if (geometry == null) return BaramBoundaryRole.Unassigned;
            if (geometry.BoundaryRole != BaramBoundaryRole.Unassigned) return geometry.BoundaryRole;
            if (IsSpecificBaramMeshBoundaryName(geometry.Name, "inlet")) return BaramBoundaryRole.Inlet;
            if (IsSpecificBaramMeshBoundaryName(geometry.Name, "outlet")) return BaramBoundaryRole.Outlet;
            if (IsSpecificBaramMeshBoundaryName(geometry.Name, "symmetry")) return BaramBoundaryRole.Symmetry;
            if (IsSpecificBaramMeshBoundaryName(geometry.Name, "wall")) return BaramBoundaryRole.Wall;
            if (IsSpecificBaramMeshBoundaryName(geometry.Name, "fsi_interface")) return BaramBoundaryRole.FsiInterface;
            return BaramBoundaryRole.Unassigned;
        }
        private bool IsSpecificBaramMeshBoundaryName(string name, string boundaryName)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = BaramGeometryFactory.SanitizeGeometryName(name).ToLowerInvariant();
            boundaryName = BaramGeometryFactory.SanitizeGeometryName(boundaryName).ToLowerInvariant();
            return name == boundaryName || name.StartsWith(boundaryName + "_", StringComparison.OrdinalIgnoreCase);
        }
        private string PromptBaramMeshGeometryName(string currentName)
        {
            using (Form form = new Form())
            using (TextBox textBox = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                form.Text = "BaramMesh Geometry";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(320, 86);
                //
                Label label = new Label();
                label.Text = "Surface name";
                label.AutoSize = true;
                label.Location = new Point(10, 13);
                form.Controls.Add(label);
                //
                textBox.Text = BaramGeometryFactory.SanitizeGeometryName(currentName);
                textBox.Location = new Point(100, 10);
                textBox.Width = 205;
                form.Controls.Add(textBox);
                //
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.Location = new Point(149, 50);
                ok.Width = 75;
                form.Controls.Add(ok);
                //
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Location = new Point(230, 50);
                cancel.Width = 75;
                form.Controls.Add(cancel);
                //
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog(this) != DialogResult.OK) return null;
                return textBox.Text;
            }
        }
        private void UpdateBaramMeshGeometryTree()
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            List<KeyValuePair<int, string>> names = new List<KeyValuePair<int, string>>();
            if (project != null && project.Configuration != null)
            {
                foreach (KeyValuePair<int, BaramGeometry> entry in project.Configuration.Geometry.OrderBy(kvp => kvp.Key))
                {
                    if (IsBaramMeshBaseGridGeometry(entry.Key, entry.Value, project.Configuration)) continue;
                    if (entry.Value.GeometryType == BaramGeometryType.Surface)
                    {
                        string displayName = entry.Value.Name;
                        if (GetEffectiveBaramMeshBoundaryRole(entry.Value) == BaramBoundaryRole.FsiInterface)
                            displayName += " [FSI Interface]";
                        names.Add(new KeyValuePair<int, string>(entry.Key, displayName));
                    }
                }
            }
            _modelTree.UpdateBaramMeshGeometryNodes(names);
        }
        private bool IsBaramMeshBaseGridGeometry(int id, BaramGeometry geometry, BaramMeshConfiguration configuration)
        {
            if (geometry == null || configuration == null || configuration.BaseGrid == null ||
                !configuration.BaseGrid.BoundingHex6.HasValue)
                return false;
            //
            int boundingHex6 = configuration.BaseGrid.BoundingHex6.Value;
            if (id == boundingHex6) return true;
            return geometry.VolumeId.HasValue && geometry.VolumeId.Value == boundingHex6;
        }
        private void SetBaramMeshWorkflowStep(string nodeName)
        {
            BaramMeshProject project = _controller.GetBaramMeshProject();
            if (project == null)
            {
                MessageBoxes.ShowWarning("Create a BaramMesh case before editing workflow steps.");
                return;
            }
            //
            BaramStep step;
            if (nodeName == "Geometry") step = BaramStep.Geometry;
            else if (nodeName == "Base Grid") step = BaramStep.BaseGrid;
            else if (nodeName == "Castellation") step = BaramStep.Castellation;
            else if (nodeName == "Snap") step = BaramStep.Snap;
            else if (nodeName == "Boundary Layer") step = BaramStep.BoundaryLayer;
            else if (nodeName == "Export") step = BaramStep.Export;
            else throw new NotSupportedException();
            //
            BaramMeshConfiguration configuration = project.Configuration != null
                                                   ? project.Configuration.DeepClone()
                                                   : new BaramMeshConfiguration();
            configuration.Step = step;
            if (step == BaramStep.BaseGrid)
            {
                OpenBaramMeshBaseGridEditor();
            }
            else if (step == BaramStep.Castellation)
            {
                OpenBaramMeshCastellationEditor();
            }
            else if (step == BaramStep.Snap)
            {
                OpenBaramMeshSnapEditor();
            }
            else if (step == BaramStep.Export)
            {
                OpenBaramMeshExportEditor();
            }
            else
            {
                _controller.UpdateBaramMeshConfigurationCommand(configuration);
                WriteDataToOutput("BaramMesh workflow step: " + nodeName);
            }
        }
        //
        private void ModelTree_CreateEvent(string nodeName, string stepName)
        {
            if (_controller.Model.Geometry != null && _controller.CurrentView == ViewGeometryModelResults.Geometry)
            {
                if (nodeName == _modelTree.MeshSetupItemsName) tsmiCreateMeshSetupItem_Click(null, null);
            }
            else if (_controller.Model != null && _controller.CurrentView == ViewGeometryModelResults.Model)
            {
                if (nodeName == _modelTree.ModelCylindersName) tsmiCreateModelCylinder_Click(null, null);
                else if (_controller.Model.Mesh == null) return;
                // _controller.Model.Mesh defines the unit system and must
                else if (nodeName == _modelTree.NodeSetsName) tsmiCreateNodeSet_Click(null, null);
                else if (nodeName == _modelTree.ElementSetsName) tsmiCreateElementSet_Click(null, null);
                else if (nodeName == _modelTree.SurfacesName) tsmiCreateSurface_Click(null, null);
                else if (nodeName == _modelTree.ModelReferencePointsName) tsmiCreateModelReferencePoint_Click(null, null);
                else if (nodeName == _modelTree.ModelCoordinateSystemsName) tsmiCreateModelCoordinateSystem_Click(null, null);
                else if (nodeName == _modelTree.MaterialsName) tsmiCreateMaterial_Click(null, null);
                else if (nodeName == _modelTree.SectionsName) tsmiCreateSection_Click(null, null);
                else if (nodeName == _modelTree.ConstraintsName) tsmiCreateConstraint_Click(null, null);
                else if (nodeName == _modelTree.SurfaceInteractionsName) tsmiCreateSurfaceInteraction_Click(null, null);
                else if (nodeName == _modelTree.ContactPairsName) tsmiCreateContactPair_Click(null, null);
                else if (nodeName == _modelTree.DistributionsName) tsmiCreateDistribution_Click(null, null);
                else if (nodeName == _modelTree.AmplitudesName) tsmiCreateAmplitude_Click(null, null);
                else if (nodeName == _modelTree.InitialConditionsName) tsmiCreateInitialCondition_Click(null, null);
                else if (nodeName == _modelTree.StepsName) tsmiCreateStep_Click(null, null);
                else if (nodeName == _modelTree.ModelHistoryOutputsName && stepName != null) CreateHistoryOutput(stepName);
                else if (nodeName == _modelTree.ModelFieldOutputsName && stepName != null) CreateFieldOutput(stepName);
                else if (nodeName == _modelTree.BoundaryConditionsName && stepName != null) CreateBoundaryCondition(stepName);
                else if (nodeName == _modelTree.LoadsName && stepName != null) CreateLoad(stepName);
                else if (nodeName == _modelTree.DefinedFieldsName && stepName != null) CreateDefinedField(stepName);
                else if (nodeName == _modelTree.AnalysesName) tsmiCreateAnalysis_Click(null, null);
            }
            else if (_controller.CurrentResult != null && _controller.CurrentResult.Mesh != null &&
                     _controller.CurrentView == ViewGeometryModelResults.Results)
            {
                if (nodeName == _modelTree.ResultReferencePointsName) tsmiCreateResultReferencePoint_Click(null, null);
                else if (nodeName == _modelTree.ResultCoordinateSystemsName) tsmiCreateResultCoordinateSystem_Click(null, null);
                else if (nodeName == _modelTree.ResultFieldOutputsName) tsmiCreateResultFieldOutput_Click(null, null);
                else if (nodeName == _modelTree.ResultHistoryOutputsName) tsmiCreateResultHistoryOutput_Click(null, null);
            }
        }
        private void ModelTree_EditEvent(NamedClass namedClass, string stepName)
        {
            // Geometry
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
            {
                if (namedClass is GeometryPart) EditGeometryPart(namedClass.Name);
                else if (namedClass is MeshSetupItem) EditMeshSetupItem(namedClass.Name);
            }
            // Model
            else if (_controller.CurrentView == ViewGeometryModelResults.Model)
            {
                if (namedClass is EmptyNamedClass) // empty named class is used to transfer the name only
                {
                    if (namedClass.Name == typeof(FeModel).ToString()) tsmiEditModel_Click(null, null);
                }
                else if (namedClass is MeshPart) EditModelPart(namedClass.Name);
                else if (namedClass is FeNodeSet) EditNodeSet(namedClass.Name);
                else if (namedClass is FeElementSet) EditElementSet(namedClass.Name);
                else if (namedClass is FeSurface) EditSurface(namedClass.Name);
                else if (namedClass is FeReferencePoint) EditModelReferencePoint(namedClass.Name);
                else if (namedClass is FeCylinder) EditModelCylinder(namedClass.Name);
                else if (namedClass is CoordinateSystem) EditModelCoordinateSystem(namedClass.Name);
                else if (namedClass is Material) EditMaterial(namedClass.Name);
                else if (namedClass is Section) EditSection(namedClass.Name);
                else if (namedClass is CaeModel.Constraint) EditConstraint(namedClass.Name);
                else if (namedClass is SurfaceInteraction) EditSurfaceInteraction(namedClass.Name);
                else if (namedClass is ContactPair) EditContactPair(namedClass.Name);
                else if (namedClass is Distribution) EditDistribution(namedClass.Name);
                else if (namedClass is Amplitude) EditAmplitude(namedClass.Name);
                else if (namedClass is InitialCondition) EditInitialCondition(namedClass.Name);
                else if (namedClass is Step) EditStep(namedClass.Name);
                else if (namedClass is HistoryOutput) EditHistoryOutput(stepName, namedClass.Name);
                else if (namedClass is FieldOutput) EditFieldOutput(stepName, namedClass.Name);
                else if (namedClass is BoundaryCondition) EditBoundaryCondition(stepName, namedClass.Name);
                else if (namedClass is Load) EditLoad(stepName, namedClass.Name);
                else if (namedClass is DefinedField) EditDefinedField(stepName, namedClass.Name);
                else if (namedClass is AnalysisJob) EditAnalysis(namedClass.Name);
            }
            // Results
            else if (_controller.CurrentView == ViewGeometryModelResults.Results)
            {
                if (namedClass is ResultPart || namedClass is GeometryPart) EditResultPart(namedClass.Name);
                else if (namedClass is FeReferencePoint) EditResultReferencePoint(namedClass.Name);
                else if (namedClass is CoordinateSystem) EditResultCoordinateSystem(namedClass.Name);
                else if (namedClass is ResultFieldOutput rfo) EditResultFieldOutput(rfo.Name);
                else if (namedClass is ResultHistoryOutput rho) EditResultHistoryOutput(rho.Name);
                else if (namedClass is HistoryResultData hd) ViewResultHistoryOutputData(hd);
                else if (namedClass is FieldData) ShowLegendSettings();
            }
        }
        private void ModelTree_RenameEvent(NamedClass item, string newName, string stepName)
        {
            _controller.RenameCommand(item, newName, stepName);
        }
        private void ModelTree_QueryEvent()
        {
            tsmiQuery_Click(null, null);
        }
        private void ModelTree_DuplicateEvent(NamedClass[] items, string[] stepNames)
        {
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
            {
                ApplyActionOnItems<MeshSetupItem>(items, DuplicateMeshSetupItems);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Model)
            {
                ApplyActionOnItems<FeNodeSet>(items, DuplicateNodeSets);
                ApplyActionOnItems<FeElementSet>(items, DuplicateElementSets);
                ApplyActionOnItems<FeSurface>(items, DuplicateSurfaces);
                ApplyActionOnItems<FeReferencePoint>(items, DuplicateModelReferencePoints);
                ApplyActionOnItems<FeCylinder>(items, DuplicateModelCylinders);
                ApplyActionOnItems<CoordinateSystem>(items, DuplicateModelCoordinateSystems);
                //
                ApplyActionOnItems<Material>(items, DuplicateMaterials);
                ApplyActionOnItems<Section>(items, DuplicateSections);
                ApplyActionOnItems<CaeModel.Constraint>(items, DuplicateConstraints);
                ApplyActionOnItems<SurfaceInteraction>(items, DuplicateSurfaceInteractions);
                ApplyActionOnItems<ContactPair>(items, DuplicateContactPairs);
                ApplyActionOnItems<Distribution>(items, DuplicateDistributions);
                ApplyActionOnItems<Amplitude>(items, DuplicateAmplitudes);
                ApplyActionOnItems<InitialCondition>(items, DuplicateInitialConditions);
                //
                ApplyActionOnItems<Step>(items, DuplicateSteps);
                ApplyActionOnItemsInStep<HistoryOutput>(items, stepNames, DuplicateHistoryOutputs);
                ApplyActionOnItemsInStep<FieldOutput>(items, stepNames, DuplicateFieldOutputs);
                ApplyActionOnItemsInStep<BoundaryCondition>(items, stepNames, DuplicateBoundaryConditions);
                ApplyActionOnItemsInStep<Load>(items, stepNames, DuplicateLoads);
                ApplyActionOnItemsInStep<DefinedField>(items, stepNames, DuplicateDefinedFields);
                //
                ApplyActionOnItems<AnalysisJob>(items, DuplicateAnalyses);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Results)
            {
                ApplyActionOnItems<FeReferencePoint>(items, DuplicateResultReferencePoints);
                ApplyActionOnItems<CoordinateSystem>(items, DuplicateResultCoordinateSystems);
            }
        }
        private void ModelTree_PropagateEvent(NamedClass[] items, string[] stepNames)
        {
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
            {
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Model)
            {
                ApplyActionOnItemsInStep<HistoryOutput>(items, stepNames, PropagateHistoryOutput);
                ApplyActionOnItemsInStep<FieldOutput>(items, stepNames, PropagateFieldOutput);
                ApplyActionOnItemsInStep<BoundaryCondition>(items, stepNames, PropagateBoundaryCondition);
                ApplyActionOnItemsInStep<Load>(items, stepNames, PropagateLoad);
                ApplyActionOnItemsInStep<DefinedField>(items, stepNames, PropagateDefinedField);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Results)
            {
            }
        }
        private void ModelTree_PreviewEvent(NamedClass[] items, string[] stepNames)
        {
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
            {
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Model)
            {
                ApplyActionOnItems<InitialCondition>(items, PreviewInitialConditions);
                ApplyActionOnItemsInStep<BoundaryCondition>(items, stepNames, PreviewBoundaryCondition);
                ApplyActionOnItemsInStep<Load>(items, stepNames, PreviewLoad);
                ApplyActionOnItemsInStep<DefinedField>(items, stepNames, PreviewDefinedField);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Results)
            {
            }
        }
        //
        private void ModelTree_SwapMasterSlave(NamedClass[] items)
        {
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
            { }
            else if (_controller.CurrentView == ViewGeometryModelResults.Model)
            {
                ApplyActionOnItems<CaeModel.Constraint>(items, SwapMasterSlaveConstraints);
                ApplyActionOnItems<ContactPair>(items, SwapMasterSlaveContactPairs);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Results)
            { }
        }
        private void ModelTree_MergeByMasterSlave(NamedClass[] items)
        {
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
            { }
            else if (_controller.CurrentView == ViewGeometryModelResults.Model)
            {
                ApplyActionOnItems<CaeModel.Constraint>(items, MergeByMasterSlaveConstraints);
                ApplyActionOnItems<ContactPair>(items, MergeByMasterSlaveContactPairs);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Results)
            { }
        }
        private void ModelTree_CheckContactPairs(NamedClass[] items)
        {
            if (_controller.CurrentView != ViewGeometryModelResults.Model) return;
            //
            List<string> contactPairNames = new List<string>();
            if (items != null)
            {
                foreach (NamedClass item in items)
                {
                    if (item is ContactPair) contactPairNames.Add(item.Name);
                }
            }
            //
            CheckContactPairs(contactPairNames.ToArray());
        }
        //
        private void ModelTree_HideShowEvent(NamedClass[] items, HideShowOperation operation, string[] stepNames)
        {
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
            {
                HideShowItems<GeometryPart>(items, operation, HideGeometryParts, ShowGeometryParts, ShowOnlyGeometryParts);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Model)
            {
                HideShowItems<MeshPart>(items, operation, HideModelParts, ShowModelParts, ShowOnlyModelParts);
                HideShowItems<FeReferencePoint>(items, operation, HideModelReferencePoints,
                                                ShowModelReferencePoints, ShowOnlyModelReferencePoints);
                HideShowItems<CoordinateSystem>(items, operation, HideModelCoordinateSystems,
                                                ShowModelCoordinateSystems, ShowOnlyModelCoordinateSystems);
                HideShowItems<CaeModel.Constraint>(items, operation, HideConstraints, ShowConstraints, ShowOnlyConstraints);
                HideShowItems<ContactPair>(items, operation, HideContactPairs, ShowContactPairs, ShowOnlyContactPairs);
                HideShowItems<InitialCondition>(items, operation, HideInitialConditions, ShowInitialConditions,
                                                ShowOnlyInitialConditions);
                HideShowStepItems<BoundaryCondition>(items, operation, stepNames, HideBoundaryConditions,
                                                     ShowBoundaryConditions, ShowOnlyBoundaryConditions);
                HideShowStepItems<Load>(items, operation, stepNames, HideLoads, ShowLoads, ShowOnlyLoads);
                HideShowStepItems<DefinedField>(items, operation, stepNames, HideDefinedFields,
                                                ShowDefinedFields, ShowOnlyDefinedFields);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Results)
            {
                HideShowItems<ResultPart>(items, operation, HideResultParts, ShowResultParts, ShowOnlyResultParts);
                HideShowItems<GeometryPart>(items, operation, HideResultParts, ShowResultParts, ShowOnlyResultParts);
                HideShowItems<FeReferencePoint>(items, operation, HideResultReferencePoints,
                                                ShowResultReferencePoints, ShowOnlyResultReferencePoints);
                HideShowItems<CoordinateSystem>(items, operation, HideResultCoordinateSystems,
                                                ShowResultCoordinateSystems, ShowOnlyResultCoordinateSystems);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Fatigue)
            {
                HideShowItems<ResultPart>(items, operation, HideResultParts, ShowResultParts, ShowOnlyResultParts);
                HideShowItems<GeometryPart>(items, operation, HideResultParts, ShowResultParts, ShowOnlyResultParts);
            }
        }
        private void ModelTree_SetPartColorEvent(string[] partNames)
        {
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry) SetColorForGeometryParts(partNames);
            else if (_controller.CurrentView == ViewGeometryModelResults.Model) SetColorForModelParts(partNames);
            else if (_controller.CurrentView == ViewGeometryModelResults.Results) SetColorForResultParts(partNames);
            else if (_controller.CurrentView == ViewGeometryModelResults.Fatigue) SetColorForResultParts(partNames);
        }
        private void ModelTree_ResetPartColorEvent(string[] partNames)
        {
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry) ResetColorForGeometryParts(partNames);
            else if (_controller.CurrentView == ViewGeometryModelResults.Model) ResetColorForModelParts(partNames);
            else if (_controller.CurrentView == ViewGeometryModelResults.Results) ResetColorForResultParts(partNames);
            else if (_controller.CurrentView == ViewGeometryModelResults.Fatigue) ResetColorForResultParts(partNames);
        }
        private void ModelTree_SetTransparencyEvent(string[] partNames)
        {
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry) SetTransparencyForGeometryParts(partNames);
            else if (_controller.CurrentView == ViewGeometryModelResults.Model) SetTransparencyForModelParts(partNames);
            else if (_controller.CurrentView == ViewGeometryModelResults.Results) SetTransparencyForResultParts(partNames);
            else if (_controller.CurrentView == ViewGeometryModelResults.Fatigue) SetTransparencyForResultParts(partNames);
        }
        private void ModelTree_ColorContoursVisibilityEvent(NamedClass[] items, bool colorContours)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < items.Length; i++)
            {
                names.Add(items[i].Name);
            }
            //
            if (names.Count > 0)
            {
                if (colorContours) ColorContoursOnResultPart(names.ToArray());
                else ColorContoursOffResultPart(names.ToArray());
            }
        }
        //
        private void ModelTree_DeleteEvent(NamedClass[] items, string[] parentNames)
        {
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
            {
                ApplyActionOnItems<MeshSetupItem>(items, DeleteMeshSetupItems);
                // At last delete the parts
                ApplyActionOnItems<GeometryPart>(items, DeleteGeometryParts);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Model)
            {
                ApplyActionOnItems<FeNodeSet>(items, DeleteNodeSets);
                ApplyActionOnItems<FeElementSet>(items, DeleteElementSets);
                ApplyActionOnItems<FeSurface>(items, DeleteSurfaces);
                ApplyActionOnItems<FeReferencePoint>(items, DeleteModelReferencePoints);
                ApplyActionOnItems<FeCylinder>(items, DeleteModelCylinders);
                ApplyActionOnItems<CoordinateSystem>(items, DeleteModelCoordinateSystems);
                ApplyActionOnItems<Material>(items, DeleteMaterials);
                ApplyActionOnItems<Section>(items, DeleteSections);
                ApplyActionOnItems<CaeModel.Constraint>(items, DeleteConstraints);
                ApplyActionOnItems<SurfaceInteraction>(items, DeleteSurfaceInteractions);
                ApplyActionOnItems<ContactPair>(items, DeleteContactPairs);
                ApplyActionOnItems<Distribution>(items, DeleteDistributions);
                ApplyActionOnItems<Amplitude>(items, DeleteAmplitudes);
                ApplyActionOnItems<InitialCondition>(items, DeleteInitialConditions);
                // First delete step items and then steps
                ApplyActionOnItemsInStep<HistoryOutput>(items, parentNames, DeleteHistoryOutputs);
                ApplyActionOnItemsInStep<FieldOutput>(items, parentNames, DeleteFieldOutputs);
                ApplyActionOnItemsInStep<BoundaryCondition>(items, parentNames, DeleteBoundaryConditions);
                ApplyActionOnItemsInStep<Load>(items, parentNames, DeleteLoads);
                ApplyActionOnItemsInStep<DefinedField>(items, parentNames, DeleteDefinedFields);
                ApplyActionOnItems<Step>(items, DeleteSteps);
                //
                ApplyActionOnItems<AnalysisJob>(items, DeleteAnalyses);
                // At last delete the parts
                ApplyActionOnItems<MeshPart>(items, DeleteModelParts);
            }
            else if (_controller.CurrentView == ViewGeometryModelResults.Results)
            {
                ApplyActionOnItems<ResultPart>(items, DeleteResultParts);
                ApplyActionOnItems<GeometryPart>(items, DeleteResultParts);
                ApplyActionOnItems<FeReferencePoint>(items, DeleteResultReferencePoints);
                ApplyActionOnItems<CoordinateSystem>(items, DeleteResultCoordinateSystems);
                // First delete components and then field outputs
                ApplyActionOnItemsInStep<FieldData>(items, parentNames, DeleteResultFieldOutputComponents);
                ApplyActionOnItems<Field>(items, DeleteResultFieldOutputs);
                ApplyActionOnItems<ResultFieldOutput>(items, DeleteResultFieldOutputs);
                //
                DeleteResultHistoryComponents(items);
                ApplyActionOnItemsInStep<HistoryResultField>(items, parentNames, DeleteResultHistoryFields);
                ApplyActionOnItems<HistoryResultSet>(items, DeleteResultHistoryOutputs);
                ApplyActionOnItems<ResultHistoryOutput>(items, DeleteResultHistoryOutputs);
            }
        }
        private void ModelTree_ActivateDeactivateEvent(NamedClass[] items, bool activate, string[] stepNames)
        {
            _controller.ActivateDeactivateMultipleCommand(items, activate, stepNames);
        }
        private void ModelTree_FieldDataSelectEvent(string[] obj)
        {
            try
            {
                SetFieldData(obj[0], obj[1], GetCurrentFieldOutputStepId(), GetCurrentFieldOutputStepIncrementId());
            }
            catch { }
        }
        //                                                                                                                          
        private void HideShowItems<T>(NamedClass[] items, HideShowOperation operation, Action<string[]> Hide,
                                      Action<string[]> Show, Action<string[]> ShowOnly)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] is T) names.Add(items[i].Name);
            }
            if (names.Count > 0)
            {
                if (operation == HideShowOperation.Hide) Hide(names.ToArray());
                else if (operation == HideShowOperation.Show) Show(names.ToArray());
                else if (operation == HideShowOperation.ShowOnly) ShowOnly(names.ToArray());
                else throw new NotSupportedException();
            }
        }
        private void HideShowStepItems<T>(NamedClass[] items, HideShowOperation operation, string[] stepNames,
                                          Action<string, string[]> Hide,
                                          Action<string, string[]> Show,
                                          Action<string, string[]> ShowOnly)
        {
            Dictionary<string, List<string>> stepItems = new Dictionary<string, List<string>>();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] is T)
                {
                    if (stepItems.ContainsKey(stepNames[i])) stepItems[stepNames[i]].Add(items[i].Name);
                    else stepItems.Add(stepNames[i], new List<string>() { items[i].Name });
                }
            }
            if (stepItems.Count > 0)
            {
                foreach (var entry in stepItems)
                {
                    if (operation == HideShowOperation.Hide) Hide(entry.Key, entry.Value.ToArray());
                    else if (operation == HideShowOperation.Show) Show(entry.Key, entry.Value.ToArray());
                    else if (operation == HideShowOperation.ShowOnly) ShowOnly(entry.Key, entry.Value.ToArray());
                    else throw new NotSupportedException();
                }
            }
        }
        private void ApplyActionOnItems<T>(NamedClass[] items, Action<string[]> Action)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] is T) names.Add(items[i].Name);
            }
            if (names.Count > 0) Action(names.ToArray());
        }
        private void ApplyActionOnItemsInStep<T>(NamedClass[] items, string[] steps, Action<string, string> Action)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] is T) Action(steps[i], items[i].Name);
            }
        }
        private void ApplyActionOnItemsInStep<T>(NamedClass[] items, string[] parentNames, Action<string, string[]> Action)
        {
            List<string> itemList;
            Dictionary<string, List<string>> parentItems = new Dictionary<string, List<string>>();
            //
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] is T)
                {
                    if (parentItems.TryGetValue(parentNames[i], out itemList)) itemList.Add(items[i].Name);
                    else parentItems.Add(parentNames[i], new List<string>() { items[i].Name });
                }
            }
            if (parentItems.Count > 0)
            {
                foreach (var entry in parentItems)
                {
                    Action(entry.Key, entry.Value.ToArray());
                }
            }
        }
        //                                                                                                                          

        #endregion #################################################################################################################


        // Menus                                                                                                                    
        private void SetMenuAndToolStripVisibility()
        {
            if (IsStateRegeneratingOrUndoing()) return;
            //
            InvokeIfRequired(() =>
            {
                //                      Disable                                                         
                // Main menu
                foreach (ToolStripMenuItem item in menuStripMain.Items) item.Enabled = false;
                tsmiFile.Enabled = true;
                tsmiResults.Enabled = true;
                tsmiTools.Enabled = true;
                tsmiHelp.Enabled = true;
                // File menu
                foreach (ToolStripItem item in tsmiFile.DropDownItems) item.Enabled = false;
                // View menu
                tsmiFollowerView.Enabled = false;
                tsbFollowerView.Enabled = false;
                // Tools menu
                tsmiParameters.Enabled = false;
                tsmiQuery.Enabled = false;
                tsmiFind.Enabled = false;
                // Results menu
                foreach (ToolStripItem item in tsmiResults.DropDownItems) item.Enabled = false;
                tsmiConvertOdbToFrd.Enabled = true;
                // Toolbar File
                tsbImport.Enabled = false;
                tsbSave.Enabled = false;
                // Toolbar View
                tsViews.DisableMouseButtons = true;
                // Toolbar Symbols
                tslSymbols.Enabled = false;
                tscbSymbols.Enabled = false;
                UpdateSymbolsList();
                // Toolbar Results
                tsResultDeformation.Enabled = false;
                tsResults.Enabled = false;
                // Vtk
                bool vtkVisible = false;
                // Tree
                SetStateWorking(Globals.RenderingText);
                //                      Enable                                                          
                if (_controller.ModelInitialized || _controller.ResultsInitialized)
                {
                    // Main menu
                    tsmiEdit.Enabled = true;
                    // File menu
                    foreach (ToolStripItem item in tsmiFile.DropDownItems) item.Enabled = true;
                    tsmiImportFile.Enabled = _controller.ModelInitialized;
                    tsmiSweepMeshPython.Enabled = _controller.ModelInitialized;
                    tsmiCylindricalExtrusionPython.Enabled = _controller.ModelInitialized;
                    tsmiAutoCreateCylindricalTies.Enabled = _controller.ModelInitialized;
                    SetSRSuntourMenuEnabled(true);
                    // Tools menu
                    tsmiParameters.Enabled = true;
                    tsmiQuery.Enabled = true;
                    tsmiFind.Enabled = true;
                    // Toolbar File
                    tsbImport.Enabled = _controller.ModelInitialized;
                    tsbSave.Enabled = true;
                }
                else
                {
                    // File menu
                    tsmiNew.Enabled = true;
                    tsmiOpen.Enabled = true;
                    tsmiRunHistoryFile.Enabled = true;
                    tsmiOpenRecent.Enabled = true;
                    tsmiExit.Enabled = true;
                    tsmiAutoCreateCylindricalTies.Enabled = false;
                    SetSRSuntourMenuEnabled(false);
                }
                //
                bool setGeometryView = _controller.CurrentView == ViewGeometryModelResults.Geometry;
                bool setModelView = _controller.CurrentView == ViewGeometryModelResults.Model;
                bool setResultsView = _controller.CurrentView == ViewGeometryModelResults.Results;
                bool setFatigueView = _controller.CurrentView == ViewGeometryModelResults.Fatigue;
                bool setBaramMeshView = _controller.CurrentView == ViewGeometryModelResults.BaramMesh;
                bool setBaramFlowView = _controller.CurrentView == ViewGeometryModelResults.BaramFlow;
                bool setEmptyView = (setGeometryView && !_controller.ModelInitialized) ||
                                    (setModelView && !_controller.ModelInitialized) ||
                                    (setResultsView && !_controller.ResultsInitialized);

                // Only for individual views !!!
                if (setEmptyView) { }
                else if (setGeometryView)
                {
                    // Main menu
                    tsmiView.Enabled = true;
                    tsmiGeometry.Enabled = true;
                    tsmiMesh.Enabled = true;
                    // Toolbar View
                    tsViews.DisableMouseButtons = false;
                    // Vtk
                    vtkVisible = true;
                }
                else if (setModelView)
                {
                    // Main menu
                    tsmiView.Enabled = true;
                    tsmiModel.Enabled = true;
                    tsmiProperty.Enabled = true;
                    tsmiInteraction.Enabled = true;
                    tsmiInitialCondition.Enabled = true;
                    tsmiDistribution.Enabled = true;
                    tsmiAmplitude.Enabled = true;
                    tsmiStepMenu.Enabled = true;
                    tsmiBC.Enabled = true;
                    tsmiLoad.Enabled = true;
                    tsmiAnalysis.Enabled = true;
                    // Toolbar View
                    tsViews.DisableMouseButtons = false;
                    // Toolbar Symbols
                    tslSymbols.Enabled = true;
                    tscbSymbols.Enabled = true;
                    // Vtk
                    vtkVisible = true;
                }
                else if (setResultsView)
                {
                    // Main menu
                    tsmiView.Enabled = true;
                    // View menu
                    tsmiFollowerView.Enabled = true;
                    tsbFollowerView.Enabled = true;
                    // Results menu
                    foreach (ToolStripItem item in tsmiResults.DropDownItems) item.Enabled = true;
                    // Toolbar View
                    tsViews.DisableMouseButtons = false;
                    // Toolbar Symbols
                    tslSymbols.Enabled = true;
                    tscbSymbols.Enabled = true;
                    // Toolbar Results
                    tsResultDeformation.Enabled = true;
                    tsResults.Enabled = true;
                    // Vtk
                    vtkVisible = true;
                }
                else if (setFatigueView)
                {
                    // File menu
                    tsmiOpen.Enabled = true;
                    tsmiOpenRecent.Enabled = true;
                    tsmiSave.Enabled = _controller.ModelInitialized || _controller.ResultsInitialized;
                    tsmiSaveAs.Enabled = _controller.ModelInitialized || _controller.ResultsInitialized;
                    tsmiExit.Enabled = true;
                    // Main menu
                    tsmiView.Enabled = true;
                    // Toolbar View
                    tsViews.DisableMouseButtons = false;
                    // Vtk
                    vtkVisible = true;
                }
                else if (setBaramMeshView)
                {
                    // File menu
                    tsmiOpen.Enabled = true;
                    tsmiOpenRecent.Enabled = true;
                    tsmiSave.Enabled = _controller.ModelInitialized || _controller.ResultsInitialized;
                    tsmiSaveAs.Enabled = _controller.ModelInitialized || _controller.ResultsInitialized;
                    tsmiExit.Enabled = true;
                    // Main menu
                    tsmiView.Enabled = true;
                    // Toolbar View
                    tsViews.DisableMouseButtons = false;
                    // Vtk
                    vtkVisible = true;
                }
                else if (setBaramFlowView)
                {
                    // File menu
                    tsmiOpen.Enabled = true;
                    tsmiOpenRecent.Enabled = true;
                    tsmiSave.Enabled = _controller.ModelInitialized || _controller.ResultsInitialized;
                    tsmiSaveAs.Enabled = _controller.ModelInitialized || _controller.ResultsInitialized;
                    tsmiExit.Enabled = true;
                    // Main menu
                    tsmiView.Enabled = true;
                    // Toolbar View
                    tsViews.DisableMouseButtons = false;
                    // Vtk
                    vtkVisible = true;
                }
                // Tree
                SetStateReady(Globals.RenderingText);
                //                      Buttons                                                         
                bool usesDisplayedMesh = setGeometryView || setModelView || setResultsView || setFatigueView;
                tsbSectionView.Checked = usesDisplayedMesh && _controller.IsSectionViewActive();
                tsbExplodedView.Checked = usesDisplayedMesh && _controller.IsExplodedViewActive();
                tsbFollowerView.Checked = usesDisplayedMesh && _controller.IsFollowerViewActive();
                tsbTransformation.Checked = usesDisplayedMesh && _controller.AreTransformationsActive();
                //                      Icons                                                           
                UpdateResultsTypeIconStates();
                //
                UpdateComplexControlStates();
                //
                _vtk.Visible = vtkVisible;
            });
        }
        private void SetMenuAndToolStripVisibilityByItemForm(bool menusActive)
        {
            _modelTree.DisableMouse = !menusActive;
            menuStripMain.DisableMouseButtons = !menusActive;
            tsFile.DisableMouseButtons = !menusActive;
            tsSymbols.Enabled = menusActive; // changing the symbols clears the selection - unwanted during selection
        }
        private void SetMenuAndToolStripVisibilityBySetState(bool menusActive)
        {
            // Do not use Enable here - use DisableMouseButtons
            _modelTree.DisableMouse = !menusActive;
            menuStripMain.DisableMouseButtons = !menusActive;
            tsFile.DisableMouseButtons = !menusActive;
            tsViews.DisableMouseButtons = !menusActive;
            tsResultDeformation.DisableMouseButtons = !menusActive;
            tsResults.DisableMouseButtons = !menusActive;
        }
        private void SetMenuAndToolStripVisibilityByAnimation(bool menusActive)
        {
            _modelTree.DisableMouse = !menusActive;
            menuStripMain.DisableMouseButtons = !menusActive;
            // DisableMouseButtons does not work for combo boxes so use .Enabled
            tsFile.Enabled = menusActive;
            tsSymbols.Enabled = menusActive;
            tsResultDeformation.Enabled = menusActive;
            //
            tsbAnimate.Enabled = menusActive;   // first - removes the selection
            tsResults.Enabled = menusActive;    // second
            // Individual buttons
            tsbSectionView.Enabled = menusActive;
            tsbExplodedView.Enabled = menusActive;
            tsbFollowerView.Enabled = menusActive;
            tsbQuery.Enabled = menusActive;
            tsbRemoveAnnotations.Enabled = menusActive;
            tsbShowAllParts.Enabled = menusActive;
            tsbHideAllParts.Enabled = menusActive;
            tsbInvertVisibleParts.Enabled = menusActive;
            //
            if (menusActive) UpdateComplexControlStates();
            
        }
        private void PushMenuStates()
        {
            List<bool> states = new List<bool>();
            states.Add(_modelTree.DisableMouse);                    // SetMenuAndToolStripVisibilityBySetState
            states.Add(menuStripMain.DisableMouseButtons);          // SetMenuAndToolStripVisibilityBySetState
            states.Add(tsFile.DisableMouseButtons);                 // SetMenuAndToolStripVisibilityBySetState
            states.Add(tsViews.DisableMouseButtons);                // SetMenuAndToolStripVisibilityBySetState
            states.Add(tsResultDeformation.DisableMouseButtons);    // SetMenuAndToolStripVisibilityBySetState
            states.Add(tsResults.DisableMouseButtons);              // SetMenuAndToolStripVisibilityBySetState
            states.Add(tsSymbols.Enabled);                          // SetMenuAndToolStripVisibilityByItemForm
            //
            _prevMenuStates.Push(states.ToArray());
        }
        private void PopMenuStates()
        {
            if (_prevMenuStates != null && _prevMenuStates.Count > 0)
            {
                int n = 0;
                bool[] states = _prevMenuStates.Pop();
                //
                _modelTree.DisableMouse = states[n++];
                menuStripMain.DisableMouseButtons = states[n++];
                tsFile.DisableMouseButtons = states[n++];
                tsViews.DisableMouseButtons = states[n++];
                tsResultDeformation.DisableMouseButtons = states[n++];
                tsResults.DisableMouseButtons = states[n++];
                tsSymbols.Enabled = states[n++];
            }
            //else throw new NotSupportedException();
        }

        #region File menu ##########################################################################################################
        internal void tsmiNew_Click(object sender, EventArgs e)
        {
            New(ModelSpaceEnum.Undefined, UnitSystemType.Undefined);
        }
        private void tsmiOpen_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = GetFileOpenFilter();
                    openFileDialog.FileName = "";
                    if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        TryOpen(openFileDialog.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiRunHistoryFile_Click(object sender, EventArgs e)
        {
            // This adds commands to the history - it is not the same as Open
            try
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "PrePoMax history|*.pmh";
                    openFileDialog.FileName = "";
                    if (openFileDialog.ShowDialog(this) == DialogResult.OK) RunHistoryFile(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        internal void tsmiImportFile_Click(object sender, EventArgs e)
        {
            ImportFile(false);
        }
        private async void tsmiSweepMeshPython_Click(object sender, EventArgs e)
        {
            try
            {
                await RunSweepMeshPythonAsync();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private async void tsmiCylindricalExtrusionPython_Click(object sender, EventArgs e)
        {
            try
            {
                await RunCylindricalExtrusionCSharpAsync();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSRSuntourImportMesh_Click(object sender, EventArgs e)
        {
            ImportFile(false);
        }
        private void tsmiSRSuntourAssignCylinders_Click(object sender, EventArgs e)
        {
            try
            {
                int renamed;
                int mirrored;
                AssignSRSuntourCylinderNamesAndMirrors(out renamed, out mirrored);
                MessageBoxes.ShowInfo("Assigned " + renamed.ToString() + " SRSuntour cylinder name(s)." +
                                      Environment.NewLine +
                                      "Created " + mirrored.ToString() + " mirrored cylinder(s).",
                                      "SRSuntour");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSRSuntourCreateContacts_Click(object sender, EventArgs e)
        {
            try
            {
                int skippedExisting;
                int missingSurfaces;
                int created = CreateSRSuntourContactDefinitions(out skippedExisting, out missingSurfaces);
                MessageBoxes.ShowInfo("Created " + created.ToString() + " SRSuntour contact pair(s)." +
                                      Environment.NewLine +
                                      skippedExisting.ToString() + " matching contact pair(s) already existed." +
                                      Environment.NewLine +
                                      missingSurfaces.ToString() + " cylinder(s) had missing stanchion or outer surfaces.",
                                      "SRSuntour");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiImportCylinderDefinitionsJson_Click(object sender, EventArgs e)
        {
            try
            {
                ImportCylinderDefinitionsFromJson();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        internal async void tsmiSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_controller.ModelInitialized && !_controller.ResultsInitialized)
                    throw new CaeException("There is no model or results to save.");
                //
                SetStateWorking(Globals.SavingText);
                await Task.Run(() => _controller.Save());
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.SavingText);
            }
        }
        private async void tsmiSaveAs_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_controller.ModelInitialized && !_controller.ResultsInitialized)
                    throw new CaeException("There is no model or results to save.");
                //
                SetStateWorking(Globals.SavingAsText);
                await Task.Run(() => _controller.SaveAs());
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.SavingAsText);
            }
        }
        private async void tsmiExportPmx240_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_controller.ModelInitialized && !_controller.ResultsInitialized)
                    throw new CaeException("There is no model or results to export.");
                //
                string fileName = GetFileNameToExportPmx240();
                if (fileName == null) return;
                //
                SetStateWorking(Globals.SavingAsText);
                await Task.Run(() => _controller.ExportPmxCompatibleWith_2_4_0(fileName));
                MessageBoxes.ShowInfo("Exported PrePoMax 2.4.0 compatible project." + Environment.NewLine +
                                      "Cylinder definitions and results are not included in this export.",
                                      "Export");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.SavingAsText);
            }
        }
        //
        private void tsmiExportToStep_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentView = ViewGeometryModelResults.Geometry;
                //
                if (_controller.Model.Geometry != null && _controller.Model.Geometry.Parts != null)
                {
                    SelectMultipleEntities("Parts", _controller.GetCADGeometryParts(), SaveCADPartsAsStep);
                }
                else throw new CaeException("No geometry to export.");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiExportToBrep_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentView = ViewGeometryModelResults.Geometry;
                //
                if (_controller.Model.Geometry != null && _controller.Model.Geometry.Parts != null)
                {
                    SelectMultipleEntities("Parts", _controller.GetCADGeometryParts(), SaveCADPartsAsBrep);
                }
                else throw new CaeException("No geometry to export.");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiExportTo3mf_Click(object sender, EventArgs e)
        {
            try
            {
                ShowForm(_frmExportFileWithProperties, _frmExportFileWithProperties.Text, null);
                //
                _frmExportFileWithProperties.ExportFileProperties = new Export3mfFileProperties();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiExportToStereolithography_Click(object sender, EventArgs e)
        {
            try
            {
                ViewGeometryModelResults currentView = GetCurrentView();
                if (currentView == ViewGeometryModelResults.Geometry)
                {
                    if (_controller.Model.Geometry != null && _controller.Model.Geometry.Parts != null)
                    {
                        SelectMultipleEntities("Parts", _controller.GetGeometryParts(), SavePartsAsStl);
                    }
                    else throw new CaeException("No geometry parts to export.");
                }
                else if (currentView == ViewGeometryModelResults.Model)
                {
                    if (_controller.Model.Mesh != null && _controller.Model.Mesh.Parts != null)
                    {
                        SelectMultipleEntities("Parts", _controller.GetModelParts(), SavePartsAsStl);
                    }
                    else throw new CaeException("No mesh parts to export.");
                }
                else MessageBoxes.ShowError("To export the results use the option Export → Deformed visualization.");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiExportToCalculix_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentView = ViewGeometryModelResults.Model;
                //
                if (CheckValidity())
                {
                    using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                    {
                        saveFileDialog.Filter = "Calculix files | *.inp";
                        if (_controller.OpenedFileName != null)
                            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_controller.OpenedFileName) + ".inp";
                        if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                        {
                            // the filter adds the extension to the file name
                            SetStateWorking(Globals.ExportingText);
                            _controller.ExportToCalculixCommand(saveFileDialog.FileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        private void tsmiExportToAbaqus_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentView = ViewGeometryModelResults.Model;
                //
                if (CheckValidity())
                {
                    using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                    {
                        saveFileDialog.Filter = "Abaqus files | *.inp";
                        if (_controller.OpenedFileName != null)
                            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_controller.OpenedFileName) + ".inp";
                        if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                        {
                            // the filter adds the extension to the file name
                            SetStateWorking(Globals.ExportingText);
                            //_controller.ExportToCalculix(saveFileDialog.FileName);
                            _controller.ExportToAbaqusCommand(saveFileDialog.FileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        private void tsmiExportToGmshMesh_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentView = ViewGeometryModelResults.Model;
                //
                if (_controller.Model.Mesh != null && _controller.Model.Mesh.Parts != null)
                {
                    SavePartsAsGmshMesh();
                }
                else throw new CaeException("No mesh to export.");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiExportToMmgMesh_Click(object sender, EventArgs e)
        {
            try
            {
                //_controller.CurrentView = ViewGeometryModelResults.Geometry;
                //
                if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
                {
                    if (_controller.Model.Geometry != null && _controller.Model.Geometry.Parts != null)
                    {
                        SelectMultipleEntities("Parts", _controller.GetGeometryParts(), SavePartsAsMmgMesh);
                    }
                    else throw new CaeException("No geometry to export.");
                }
                else if (_controller.CurrentView == ViewGeometryModelResults.Model)
                {
                    if (_controller.Model.Mesh != null && _controller.Model.Mesh.Parts != null)
                    {
                        SelectMultipleEntities("Parts", _controller.GetModelParts(), SavePartsAsMmgMesh);
                    }
                    else throw new CaeException("No mesh to export.");
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiExportToDeformedInp_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentView = ViewGeometryModelResults.Results;
                //
                if (_controller.CurrentResult.Mesh != null && _controller.CurrentResult.Mesh.Parts != null)
                {
                    SelectMultipleEntities("Parts", _controller.GetResultParts(), SaveDeformedPartsAsInp);
                }
                else MessageBoxes.ShowError("There is no mesh to export.");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }

        }
        private void tsmiExportToDeformedStl_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentView = ViewGeometryModelResults.Results;
                //
                if (_controller.CurrentResult.Mesh != null && _controller.CurrentResult.Mesh.Parts != null)
                {
                    SelectMultipleEntities("Parts", _controller.GetResultParts(), SavePartsAsStl);
                }
                else MessageBoxes.ShowError("There is no mesh to export.");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private async void tsmiExportDisplayedFrd_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentView = ViewGeometryModelResults.Results;
                if (_controller.CurrentResult == null || _controller.CurrentResult.Mesh == null)
                    throw new CaeException("There are no results to export.");
                //
                string[] visiblePartNames = _controller.CurrentResult.Mesh.GetVisibleAndActivePartNames();
                if (visiblePartNames == null || visiblePartNames.Length == 0)
                    throw new CaeException("There are no displayed result parts to export.");
                //
                string[] supportedFieldNames = new string[]
                {
                    FOFieldNames.Disp,
                    FOFieldNames.Stress,
                    FOFieldNames.ToStrain,
                    FOFieldNames.Forc,
                    FOFieldNames.Contact
                };
                HashSet<string> existingFieldNames = new HashSet<string>(_controller.GetResultFieldOutputNames(),
                                                                          StringComparer.OrdinalIgnoreCase);
                string[] fieldNames = supportedFieldNames.Where(name => existingFieldNames.Contains(name)).ToArray();
                if (fieldNames.Length == 0)
                    throw new CaeException("None of the supported fields exist in the current results.");
                //
                int[] stepIds = _controller.GetResultStepIDs();
                using (FrmExportDisplayedFrd form = new FrmExportDisplayedFrd(fieldNames, stepIds,
                                                                              _controller.GetResultStepIncrementIds))
                {
                    if (form.ShowDialog(this) != DialogResult.OK) return;
                    using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                    {
                        saveFileDialog.Filter = "Calculix result files | *.frd";
                        string baseFileName = _controller.CurrentResult.FileName;
                        if (string.IsNullOrEmpty(baseFileName)) baseFileName = _controller.OpenedFileName;
                        if (!string.IsNullOrEmpty(baseFileName))
                            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(baseFileName) + "_displayed.frd";
                        //
                        if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                        {
                            SetStateWorking(Globals.ExportingText);
                            await Task.Run(() => _controller.ExportDisplayedResultsToFrd(saveFileDialog.FileName,
                                                                                         form.SelectedFieldNames,
                                                                                         form.AllStepsAndIncrements,
                                                                                         form.SelectedStepId,
                                                                                         form.SelectedStepIncrementId));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        private async void ExportUsingFileProperties(ExportFileProperties properties)
        {
            try
            {
                SetStateWorking(Globals.ExportingText);
                await Task.Run(() => _controller.ExportUsingFileProperties(properties));
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        //
        private bool New(ModelSpaceEnum modelSpace, UnitSystemType unitSystemType)
        {
            try
            {
                TopMost = true;    // fix the problem with Solidworks opened in the background
                //
                if ((_controller.ModelChanged || _controller.ModelInitialized || _controller.ResultsInitialized) &&
                    MessageBoxes.ShowWarningQuestionOKCancel("OK to close the current model?") != DialogResult.OK) return false;
                //
                _controller.DeInitialize();
                SetMenuAndToolStripVisibility();
                //
                bool update = false;
                // The model space and the unit system are undefined
                if (modelSpace == ModelSpaceEnum.Undefined || unitSystemType == UnitSystemType.Undefined)
                {
                    if (SelectNewModelProperties(true))
                    {
                        _controller.New();
                        SetNewModelProperties();
                        update = true;
                    }
                }
                else
                {
                    _controller.New();
                    _controller.SetNewModelPropertiesCommand(modelSpace, unitSystemType);
                    update = true;
                }
                //
                if (update)
                {
                    SetMenuAndToolStripVisibility();
                    //
                    _controller.ModelChanged = false; // must be here since adding a unit system changes the model
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                TopMost = false;
            }
            return true;
        }
        private bool CheckBeforeOpen(string fileName)
        {
            if (!File.Exists(fileName)) return false;
            //
            if (_controller.ModelChanged)
            {
                string extension = Path.GetExtension(fileName).ToLower();
                if (extension == ".pmx" || extension == ".pmh")
                {
                    if (MessageBoxes.ShowWarningQuestionOKCancel("OK to close the current model?") != DialogResult.OK)
                        return false;
                }
                else if ((extension == ".frd" || extension == ".foam") && _controller.AllResults.ContainsResult(fileName))
                {
                    if (MessageBoxes.ShowWarningQuestionOKCancel("OK to reopen the existing results?") != DialogResult.OK)
                        return false;
                    //
                    _selectedSymbolIndex.Remove(ViewGeometryModelResults.Results);
                }
            }
            return true;
        }
        private async void TryOpen(string fileName)
        {
            try
            {
                string parameters = Globals.FromFileOpenMenu;
                if (CheckBeforeOpen(fileName))
                    await OpenAsync(fileName, _controller.OpenFileCommand, true, null, parameters);
                //
                string extension = Path.GetExtension(fileName).ToLower();
                if (extension == ".frd") RunHistoryPostprocessing();    // must be here
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
                _controller.ModelChanged = false;   // hide message box
                tsmiNew_Click(null, null);
            }
        }
        private async Task OpenAsync(string fileName, Action<string, string> ActionOnFile, bool resetCamera = true,
                                     Action callback = null, string parameters = null)
        {
            bool stateSet = false;
            string stateText = Globals.OpeningText;
            try
            {
                string extension = Path.GetExtension(fileName).ToLower();
                if (extension == ".pmh") stateText = Globals.RegeneratingText;
                //
                if (SetStateWorking(stateText) || IsStateRegeneratingOrUndoing())
                {
                    stateSet = true;
                    await Task.Run(() => Open(fileName, ActionOnFile, resetCamera, parameters));
                    callback?.Invoke(); // close monitor window
                }
                else MessageBoxes.ShowWarning("Another task is already running.");
                // If the model space or the unit system are undefined
                if (_controller.ModelInitialized) IfNeededSelectAndSetNewModelProperties();
                if (_controller.ResultsInitialized) SelectResultsUnitSystem(parameters);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                if (stateSet) SetStateReady(stateText);
                SetMenuAndToolStripVisibility();
            }
        }
        public void Open(string fileName, Action<string, string> ActionOnFile, bool resetCamera = true, string parameters = null)
        {
            ActionOnFile(fileName, parameters);
            //
            UpdateResultMenus();
            //
            if (_controller.CurrentView == ViewGeometryModelResults.Geometry) _controller.DrawGeometry(resetCamera);
            else if (_controller.CurrentView == ViewGeometryModelResults.Model) _controller.DrawModel(resetCamera);
            else if (_controller.CurrentView == ViewGeometryModelResults.BaramMesh) _controller.DrawBaramMesh(resetCamera);
            else if (_controller.CurrentView == ViewGeometryModelResults.BaramFlow) _controller.DrawBaramFlow(resetCamera);
            else if (_controller.CurrentView == ViewGeometryModelResults.Results)
            {
                // Set the representation which also calls Draw
                _controller.ViewResultsType = ViewResultsTypeEnum.ColorContours;  // Draw
                //
                if (resetCamera) tsmiFrontView_Click(null, null);
            }
            else throw new NotSupportedException();
        }
        private void UpdateResultMenus()
        {
            if (_controller.CurrentResult != null && _controller.CurrentResult.Mesh != null)
            {
                SetResultNames();
                // Reset the previous step and increment
                SetAllStepAndIncrementIds(true);
                // Set last increment
                SetDefaultStepAndIncrementIds();
                // Show the selection in the results tree
                SelectFirstComponentOfFirstFieldOutput();
            }
        }
        public async void RunHistoryFile(string fileName)
        {
            // This adds commands to the history - it is not the same as Open
            try
            {
                CloseAllForms();
                Application.DoEvents();
                SetStateWorking(Globals.RegeneratingText);
                _modelTree.ScreenUpdating = false;
                await Task.Run(() => _controller.RunHistoryFile(fileName));
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.RegeneratingText);
                _modelTree.ScreenUpdating = true;
                RegenerateTree();
                //
                SetMenuAndToolStripVisibility();
                // During regeneration the tree is empty
                if (_controller.CurrentResult != null) SelectFirstComponentOfFirstFieldOutput();
                //
                SetZoomToFit(true);
            }
        }
        private async void ImportFile(bool onlyMaterials)
        {
            try
            {
                if (!_controller.ModelInitialized)
                    throw new CaeException("There is no model to import into. First create a new model.");
                // If the model space or the unit system are undefined
                IfNeededSelectAndSetNewModelProperties();
                //
                string filter = GetFileImportFilter(onlyMaterials);
                string[] files = GetFileNamesToImport(filter);
                //
                if (files != null && files.Length > 0)
                {
                    _controller.ClearErrors();
                    //
                    SetStateWorking(Globals.ImportingText);
                    foreach (var file in files)
                    {
                        await _controller.ImportFileAsync(file, onlyMaterials);
                    }
                    SetFrontBackView(true, true);   // animate must be true in order for the scale bar to work correctly
                    //
                    int numErrors = _controller.GetNumberOfErrors();
                    if (numErrors > 0)
                    {
                        _controller.OutputErrors();
                        string message = "There were errors while importing the file/files.";
                        WriteDataToOutput(message);
                        AutoClosingMessageBox.ShowError(message, 3000);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ImportingText);
            }
        }
        private async Task RunSweepMeshPythonAsync()
        {
            if (!_controller.ModelInitialized)
                throw new CaeException("There is no model to import into. First create a new model.");
            // If the model space or the unit system are undefined
            IfNeededSelectAndSetNewModelProperties();
            //
            bool stateSet = false;
            try
            {
                if (SetStateWorking(Globals.MeshingText))
                {
                    stateSet = true;
                    //
                    string scriptFileName = GetSweepMeshPythonScriptFileName();
                    double[] stanchionTop = TryGetStanchionInsertTopCenter();
                    //
                    _controller.ClearErrors();
                    WriteDataToOutput("");
                    WriteDataToOutput("Starting Python sweep mesh tool...");
                    WriteDataToOutput("Sweep mesh script: " + scriptFileName);
                    if (stanchionTop != null)
                    {
                        WriteDataToOutput(string.Format("Using stanchion auto-position target: X={0:G17}, Y={1:G17}, Z={2:G17}",
                                                        stanchionTop[0], stanchionTop[1], stanchionTop[2]));
                    }
                    //
                    SweepMeshPythonResult result = await Task.Run(() => RunSweepMeshPythonTool(scriptFileName, stanchionTop));
                    //
                    if (result != null && result.Status == "completed")
                    {
                        if (string.IsNullOrWhiteSpace(result.OutputPath) || !File.Exists(result.OutputPath))
                            throw new CaeException("The Python sweep mesh tool reported success but the output .inp file was not found.");
                        //
                        WriteDataToOutput("Sweep mesh generated: " + result.OutputPath);
                        await ImportGeneratedInpFileAsync(result.OutputPath);
                    }
                    else if (result != null && result.Status == "failed")
                    {
                        string message = "The Python sweep mesh tool failed.";
                        if (!string.IsNullOrWhiteSpace(result.Error)) message += Environment.NewLine + result.Error;
                        throw new CaeException(message);
                    }
                    else WriteDataToOutput("Python sweep mesh was cancelled.");
                }
                else MessageBoxes.ShowWarning("Another task is already running.");
            }
            finally
            {
                if (stateSet) SetStateReady(Globals.MeshingText);
            }
        }
        private async Task RunCylindricalExtrusionPythonAsync()
        {
            if (!_controller.ModelInitialized)
                throw new CaeException("There is no model to import into. First create a new model.");
            // If the model space or the unit system are undefined
            IfNeededSelectAndSetNewModelProperties();
            //
            bool stateSet = false;
            string tempDirectory = Path.Combine(Path.GetTempPath(), "PrePoMax");
            string id = Guid.NewGuid().ToString("N");
            string exportInpFileName = Path.Combine(tempDirectory, "cylindrical_extrusion_" + id + "_input.inp");
            string outputInpFileName = Path.Combine(tempDirectory, "cylindrical_extrusion_" + id + "_output.inp");
            string deltaInpFileName = Path.Combine(tempDirectory, "cylindrical_extrusion_" + id + "_delta.inp");
            string payloadFileName = Path.Combine(tempDirectory, "cylindrical_extrusion_" + id + "_payload.json");
            string cylinderJsonFileName = Path.Combine(tempDirectory, "cylindrical_extrusion_" + id + "_cylinders.json");
            //
            try
            {
                if (SetStateWorking(Globals.MeshingText))
                {
                    stateSet = true;
                    Directory.CreateDirectory(tempDirectory);
                    //
                    string scriptFileName = GetCylindricalExtrusionPythonScriptFileName();
                    //
                    _controller.ClearErrors();
                    WriteDataToOutput("");
                    WriteDataToOutput("Exporting current mesh for Python cylindrical extrusion...");
                    _controller.ExportToCalculix(exportInpFileName);
                    string modelCylinderJsonFileName = PrepareCylinderDefinitionsJsonFile(cylinderJsonFileName);
                    if (!string.IsNullOrWhiteSpace(modelCylinderJsonFileName))
                        WriteDataToOutput("Using cylinder definitions from model cylinders: " + modelCylinderJsonFileName);
                    WriteDataToOutput("Starting Python cylindrical extrusion tool...");
                    WriteDataToOutput("Cylindrical extrusion script: " + scriptFileName);
                    //
                    CylindricalExtrusionPythonResult result = await Task.Run(() =>
                        RunCylindricalExtrusionPythonTool(scriptFileName, exportInpFileName, outputInpFileName,
                                                          deltaInpFileName, payloadFileName,
                                                          modelCylinderJsonFileName));
                    //
                    if (result != null && result.Status == "completed")
                    {
                        if (string.IsNullOrWhiteSpace(result.DeltaInpPath) || !File.Exists(result.DeltaInpPath))
                            throw new CaeException("The Python cylindrical extrusion tool reported success but the delta .inp file was not found.");
                        if (string.IsNullOrWhiteSpace(result.PayloadPath) || !File.Exists(result.PayloadPath))
                            throw new CaeException("The Python cylindrical extrusion tool reported success but the surface payload file was not found.");
                        //
                        CylindricalExtrusionPayload payload = ReadCylindricalExtrusionPayload(result.PayloadPath);
                        if (payload == null)
                            throw new CaeException("The cylindrical extrusion surface payload could not be read.");
                        //
                        WriteDataToOutput("Importing cylindrical extrusion surfaces on the existing mesh...");
                        ImportCylindricalExtrusionPayload(payload, false);
                        WriteDataToOutput("Importing cylindrical extrusion delta mesh...");
                        await _controller.ImportFileAsync(result.DeltaInpPath, false);
                        WriteDataToOutput("Importing cylindrical extrusion surfaces on the extruded mesh...");
                        ImportCylindricalExtrusionPayload(payload, true);
                        SetFrontBackView(true, true);
                    }
                    else if (result != null && result.Status == "failed")
                    {
                        string message = "The Python cylindrical extrusion tool failed.";
                        if (!string.IsNullOrWhiteSpace(result.Error)) message += Environment.NewLine + result.Error;
                        throw new CaeException(message);
                    }
                    else WriteDataToOutput("Python cylindrical extrusion was cancelled.");
                }
                else MessageBoxes.ShowWarning("Another task is already running.");
            }
            finally
            {
                if (stateSet) SetStateReady(Globals.MeshingText);
                DeleteIfExists(exportInpFileName);
                DeleteIfExists(outputInpFileName);
                DeleteIfExists(deltaInpFileName);
                DeleteIfExists(payloadFileName);
                DeleteIfExists(cylinderJsonFileName);
            }
        }
        private async Task RunCylindricalExtrusionCSharpAsync()
        {
            if (!_controller.ModelInitialized)
                throw new CaeException("There is no model to import into. First create a new model.");
            IfNeededSelectAndSetNewModelProperties();
            //
            bool stateSet = false;
            try
            {
                if (SetStateWorking(Globals.MeshingText))
                {
                    stateSet = true;
                    _controller.ClearErrors();
                    WriteDataToOutput("");
                    WriteDataToOutput("Starting C# cylindrical extrusion...");
                    await Task.Yield();
                    int createdElements = CreateCylindricalExtrusionFromModelCylinders();
                    _controller.FinalizeDirectModelMeshEdit();
                    WriteDataToOutput("C# cylindrical extrusion created " + createdElements.ToString() + " element(s).");
                    int skippedExistingTies;
                    int missingInnerSurfaces;
                    int createdTies = _controller.AutoCreateCylindricalSurfaceTies(out skippedExistingTies,
                                                                                   out missingInnerSurfaces);
                    WriteDataToOutput("Auto-created " + createdTies.ToString() + " cylindrical tie constraint(s).");
                    if (skippedExistingTies > 0)
                        WriteDataToOutput("Skipped " + skippedExistingTies.ToString() + " existing cylindrical tie(s).");
                    if (missingInnerSurfaces > 0)
                        WriteDataToOutput("Skipped " + missingInnerSurfaces.ToString() +
                                          " base surface(s) without matching inner surface.");
                    SetFrontBackView(true, true);
                }
                else MessageBoxes.ShowWarning("Another task is already running.");
            }
            finally
            {
                if (stateSet) SetStateReady(Globals.MeshingText);
            }
        }
        private int CreateCylindricalExtrusionFromModelCylinders()
        {
            FeMesh mesh = _controller.DisplayedMesh;
            if (mesh == null || mesh.Nodes == null || mesh.Elements == null || mesh.Parts == null)
                throw new CaeException("There is no model mesh.");
            //
            FeCylinder[] cylinders = _controller.GetAllModelCylinders();
            if (cylinders == null || cylinders.Length == 0)
                throw new CaeException("No model cylinders are defined.");
            //
            HashSet<int> sourceElementIds = new HashSet<int>(mesh.Elements.Keys);
            Dictionary<string, BoundaryFaceData> boundaryFaces = GetBoundaryFaces(mesh, sourceElementIds);
            if (boundaryFaces.Count == 0) throw new CaeException("No boundary faces were found on the current mesh.");
            //
            HashSet<string> reservedNames = new HashSet<string>(_controller.GetAllMeshEntityNames() ?? new string[0],
                                                                StringComparer.OrdinalIgnoreCase);
            int nextNodeId = mesh.MaxNodeId + 1;
            int nextElementId = mesh.MaxElementId + 1;
            int nextPartId = mesh.Parts.Count == 0 ? 1 : mesh.Parts.Values.Max(part => part.PartId) + 1;
            int createdElements = 0;
            //
            for (int i = 0; i < cylinders.Length; i++)
            {
                FeCylinder cylinder = cylinders[i];
                if (cylinder == null) continue;
                string error;
                if (!cylinder.IsProperlyDefined(out error))
                {
                    WriteDataToOutput("Skipping cylinder '" + cylinder.Name + "': " + error);
                    continue;
                }
                if (Math.Abs(cylinder.TargetRadius - (cylinder.Radius + cylinder.ExtraRadius)) <= cylinder.Tolerance)
                {
                    WriteDataToOutput("Skipping cylinder '" + cylinder.Name + "': target radius equals actual radius.");
                    continue;
                }
                //
                List<CylindricalExtrusionFaceData> cylinderFaces = GetCylindricalExtrusionFaces(mesh, boundaryFaces, cylinder);
                if (cylinderFaces.Count == 0)
                {
                    WriteDataToOutput("No cylindrical extrusion faces were found for cylinder '" + cylinder.Name + "'.");
                    continue;
                }
                //
                string prefix = GetUniqueCylinderSurfacePrefix("CYL_" + (i + 1).ToString("00"), reservedNames);
                CreateSurfaceFromExtrusionFaces(mesh, cylinderFaces, prefix + "_base", reservedNames, false);
                int partElementStartCount = createdElements;
                CreateCylindricalExtrusionElements(mesh, cylinder, cylinderFaces, prefix, reservedNames,
                                                   ref nextNodeId, ref nextElementId, ref nextPartId,
                                                   ref createdElements);
                WriteDataToOutput("Cylinder '" + cylinder.Name + "' created " +
                                  (createdElements - partElementStartCount).ToString() + " extrusion element(s).");
            }
            return createdElements;
        }
        private string GetUniqueCylinderSurfacePrefix(string preferredPrefix, HashSet<string> reservedNames)
        {
            string prefix = preferredPrefix;
            int index = 1;
            while (reservedNames.Any(name => name.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                                             name.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase)))
            {
                prefix = preferredPrefix + "-" + index.ToString();
                index++;
            }
            reservedNames.Add(prefix);
            return prefix;
        }
        private List<CylindricalExtrusionFaceData> GetCylindricalExtrusionFaces(FeMesh mesh,
                                                                                Dictionary<string, BoundaryFaceData> boundaryFaces,
                                                                                FeCylinder cylinder)
        {
            List<CylindricalExtrusionFaceData> faces = new List<CylindricalExtrusionFaceData>();
            double[] axisDirection = cylinder.GetNormalizedDirection();
            double[] axisCenter = cylinder.Center;
            double halfHeight = 0.5 * cylinder.Height;
            double axialTolerance = Math.Max(cylinder.Tolerance, 1E-6);
            double actualRadius = cylinder.Radius + cylinder.ExtraRadius;
            double radialTolerance = GetCylinderRadiusTolerance(cylinder, actualRadius);
            //
            foreach (BoundaryFaceData boundaryFace in boundaryFaces.Values)
            {
                if (!IsFaceWithinCylinderBand(mesh, boundaryFace, axisCenter, axisDirection, halfHeight, axialTolerance))
                    continue;
                if (!IsFaceOnOuterCylinderRadius(mesh, boundaryFace, axisCenter, axisDirection, actualRadius,
                                                 radialTolerance))
                    continue;
                if (!IsFaceOnCylinderSide(mesh, boundaryFace, axisCenter, axisDirection)) continue;
                //
                bool parabolic = boundaryFace.NodeIds.Length == 6 || boundaryFace.NodeIds.Length == 8;
                if (boundaryFace.NodeIds.Length == 3 || boundaryFace.NodeIds.Length == 4 || parabolic)
                {
                    faces.Add(new CylindricalExtrusionFaceData()
                    {
                        ElementId = boundaryFace.ElementId,
                        FaceName = boundaryFace.FaceName,
                        NodeIds = boundaryFace.NodeIds,
                        IsParabolic = parabolic
                    });
                }
            }
            return faces;
        }
        private void CreateCylindricalExtrusionElements(FeMesh mesh,
                                                        FeCylinder cylinder,
                                                        List<CylindricalExtrusionFaceData> cylinderFaces,
                                                        string prefix,
                                                        HashSet<string> reservedNames,
                                                        ref int nextNodeId,
                                                        ref int nextElementId,
                                                        ref int nextPartId,
                                                        ref int createdElements)
        {
            int layers = Math.Max(cylinder.NumberOfLayers, 1);
            double actualRadius = cylinder.Radius + cylinder.ExtraRadius;
            double targetRadius = cylinder.TargetRadius;
            double extrusionDistance = targetRadius - actualRadius;
            double[] axisCenter = cylinder.Center;
            double[] axisDirection = cylinder.GetNormalizedDirection();
            Dictionary<string, int> generatedNodeIds = new Dictionary<string, int>();
            List<int> partElementIds = new List<int>();
            HashSet<int> partNodeIds = new HashSet<int>();
            Dictionary<FeFaceName, HashSet<int>> innerFaces = new Dictionary<FeFaceName, HashSet<int>>();
            Dictionary<FeFaceName, HashSet<int>> outerFaces = new Dictionary<FeFaceName, HashSet<int>>();
            //
            for (int layer = 0; layer < layers; layer++)
            {
                foreach (CylindricalExtrusionFaceData face in cylinderFaces)
                {
                    int[] elementNodeIds = CreateCylindricalExtrusionElementNodeIds(mesh, face, axisCenter, axisDirection,
                                                                                    extrusionDistance, layers, layer,
                                                                                    generatedNodeIds, ref nextNodeId);
                    FeElement element;
                    if (elementNodeIds.Length == 8) element = new LinearHexaElement(nextElementId, elementNodeIds);
                    else if (elementNodeIds.Length == 20) element = new ParabolicHexaElement(nextElementId, elementNodeIds);
                    else if (elementNodeIds.Length == 6) element = new LinearWedgeElement(nextElementId, elementNodeIds);
                    else if (elementNodeIds.Length == 15) element = new ParabolicWedgeElement(nextElementId, elementNodeIds);
                    else continue;
                    //
                    if (element is FeElement3D element3D && element3D.GetVolume(mesh.Nodes) < 0) element3D.Mirror();
                    element.PartId = nextPartId;
                    mesh.Elements.Add(element.Id, element);
                    partElementIds.Add(element.Id);
                    foreach (int nodeId in element.NodeIds) partNodeIds.Add(nodeId);
                    AddSurfaceElementFace(innerFaces, FeFaceName.S1, element.Id);
                    AddSurfaceElementFace(outerFaces, FeFaceName.S2, element.Id);
                    nextElementId++;
                    createdElements++;
                }
            }
            //
            if (partElementIds.Count == 0) return;
            Type[] elementTypes = partElementIds.Select(id => mesh.Elements[id].GetType()).Distinct().ToArray();
            string elementTypeName = GetAbaqusElementTypeName(elementTypes);
            string partName = GetUniqueMeshEntityName(prefix + "_" + elementTypeName + "_EXTRUSION", reservedNames);
            MeshPart part = new MeshPart(partName, nextPartId, partNodeIds.OrderBy(id => id).ToArray(),
                                         partElementIds.OrderBy(id => id).ToArray(),
                                         elementTypes.Length > 0 ? elementTypes : new Type[] { typeof(ParabolicHexaElement) });
            mesh.Parts.Add(part.Name, part);
            nextPartId++;
            //
            CreateSurfaceFromElementFaces(prefix + "_" + elementTypeName + "_inner", innerFaces, reservedNames);
            CreateSurfaceFromElementFaces(prefix + "_" + elementTypeName + "_outer", outerFaces, reservedNames);
        }
        private string GetAbaqusElementTypeName(Type[] elementTypes)
        {
            if (elementTypes == null || elementTypes.Length == 0) return "C3D";
            if (elementTypes.Length > 1) return "MIXED";
            Type elementType = elementTypes[0];
            if (elementType == typeof(LinearHexaElement)) return "C3D8";
            else if (elementType == typeof(ParabolicHexaElement)) return "C3D20R";
            else if (elementType == typeof(LinearWedgeElement)) return "C3D6";
            else if (elementType == typeof(ParabolicWedgeElement)) return "C3D15";
            else return "C3D";
        }
        private int[] CreateCylindricalExtrusionElementNodeIds(FeMesh mesh,
                                                               CylindricalExtrusionFaceData face,
                                                               double[] axisCenter,
                                                               double[] axisDirection,
                                                               double extrusionDistance,
                                                               int layers,
                                                               int layer,
                                                               Dictionary<string, int> generatedNodeIds,
                                                               ref int nextNodeId)
        {
            int faceNodeCount = face.NodeIds.Length;
            int cornerCount = faceNodeCount == 3 || faceNodeCount == 6 ? 3 : 4;
            int[] innerCorners = new int[cornerCount];
            int[] outerCorners = new int[cornerCount];
            for (int i = 0; i < cornerCount; i++)
            {
                innerCorners[i] = GetExtrudedLayerNodeId(mesh, face.NodeIds[i], layer, layers, extrusionDistance,
                                                         axisCenter, axisDirection, generatedNodeIds, ref nextNodeId);
                outerCorners[i] = GetExtrudedLayerNodeId(mesh, face.NodeIds[i], layer + 1, layers, extrusionDistance,
                                                         axisCenter, axisDirection, generatedNodeIds, ref nextNodeId);
            }
            if (!face.IsParabolic) return innerCorners.Concat(outerCorners).ToArray();
            //
            int[] innerFaceMids = new int[cornerCount];
            int[] outerFaceMids = new int[cornerCount];
            int[] throughMids = new int[cornerCount];
            for (int i = 0; i < cornerCount; i++)
            {
                int midNodeId = face.NodeIds[cornerCount + i];
                innerFaceMids[i] = GetExtrudedLayerNodeId(mesh, midNodeId, layer, layers, extrusionDistance,
                                                          axisCenter, axisDirection, generatedNodeIds, ref nextNodeId);
                outerFaceMids[i] = GetExtrudedLayerNodeId(mesh, midNodeId, layer + 1, layers, extrusionDistance,
                                                          axisCenter, axisDirection, generatedNodeIds, ref nextNodeId);
                throughMids[i] = GetExtrudedHalfLayerNodeId(mesh, face.NodeIds[i], layer, layers, extrusionDistance,
                                                            axisCenter, axisDirection, generatedNodeIds, ref nextNodeId);
            }
            return innerCorners.Concat(outerCorners).Concat(innerFaceMids).Concat(outerFaceMids).Concat(throughMids).ToArray();
        }
        private int GetExtrudedLayerNodeId(FeMesh mesh,
                                           int sourceNodeId,
                                           int layer,
                                           int layers,
                                           double extrusionDistance,
                                           double[] axisCenter,
                                           double[] axisDirection,
                                           Dictionary<string, int> generatedNodeIds,
                                           ref int nextNodeId)
        {
            string key = sourceNodeId.ToString() + "_" + layer.ToString();
            if (generatedNodeIds.TryGetValue(key, out int nodeId)) return nodeId;
            if (layer == 0)
                return CreateCopiedNode(mesh, sourceNodeId, key, generatedNodeIds, ref nextNodeId);
            double fraction = (double)layer / layers;
            return CreateExtrudedNode(mesh, sourceNodeId, fraction, extrusionDistance, axisCenter, axisDirection,
                                      key, generatedNodeIds, ref nextNodeId);
        }
        private int CreateCopiedNode(FeMesh mesh,
                                     int sourceNodeId,
                                     string key,
                                     Dictionary<string, int> generatedNodeIds,
                                     ref int nextNodeId)
        {
            FeNode sourceNode = mesh.Nodes[sourceNodeId];
            int nodeId = nextNodeId++;
            mesh.Nodes.Add(nodeId, new FeNode(nodeId, sourceNode.Coor.ToArray()));
            generatedNodeIds.Add(key, nodeId);
            return nodeId;
        }
        private int GetExtrudedHalfLayerNodeId(FeMesh mesh,
                                               int sourceNodeId,
                                               int layer,
                                               int layers,
                                               double extrusionDistance,
                                               double[] axisCenter,
                                               double[] axisDirection,
                                               Dictionary<string, int> generatedNodeIds,
                                               ref int nextNodeId)
        {
            string key = sourceNodeId.ToString() + "_" + layer.ToString() + "_half";
            if (generatedNodeIds.TryGetValue(key, out int nodeId)) return nodeId;
            double fraction = (layer + 0.5) / layers;
            return CreateExtrudedNode(mesh, sourceNodeId, fraction, extrusionDistance, axisCenter, axisDirection,
                                      key, generatedNodeIds, ref nextNodeId);
        }
        private int CreateExtrudedNode(FeMesh mesh,
                                       int sourceNodeId,
                                       double fraction,
                                       double extrusionDistance,
                                       double[] axisCenter,
                                       double[] axisDirection,
                                       string key,
                                       Dictionary<string, int> generatedNodeIds,
                                       ref int nextNodeId)
        {
            FeNode sourceNode = mesh.Nodes[sourceNodeId];
            GetPointAxialAndRadialCoordinates(sourceNode.Coor, axisCenter, axisDirection, out double axialCoordinate,
                                              out double radialDistance);
            double[] axisPoint = new double[]
            {
                axisCenter[0] + axisDirection[0] * axialCoordinate,
                axisCenter[1] + axisDirection[1] * axialCoordinate,
                axisCenter[2] + axisDirection[2] * axialCoordinate
            };
            double[] radialVector = SubtractVectors(sourceNode.Coor, axisPoint);
            double radialLength = GetVectorLength(radialVector);
            if (radialLength <= 1E-12) radialVector = new double[] { 1, 0, 0 };
            else radialVector = ScaleVector(radialVector, 1.0 / radialLength);
            double newRadius = radialDistance + extrusionDistance * fraction;
            double[] newCoor = new double[]
            {
                axisPoint[0] + radialVector[0] * newRadius,
                axisPoint[1] + radialVector[1] * newRadius,
                axisPoint[2] + radialVector[2] * newRadius
            };
            int nodeId = nextNodeId++;
            mesh.Nodes.Add(nodeId, new FeNode(nodeId, newCoor));
            generatedNodeIds.Add(key, nodeId);
            return nodeId;
        }
        private void CreateSurfaceFromExtrusionFaces(FeMesh mesh,
                                                     List<CylindricalExtrusionFaceData> faces,
                                                     string preferredName,
                                                     HashSet<string> reservedNames,
                                                     bool update)
        {
            Dictionary<FeFaceName, HashSet<int>> faceNameElementIds = new Dictionary<FeFaceName, HashSet<int>>();
            foreach (CylindricalExtrusionFaceData face in faces)
                AddSurfaceElementFace(faceNameElementIds, face.FaceName, face.ElementId);
            CreateSurfaceFromElementFaces(preferredName, faceNameElementIds, reservedNames, update);
        }
        private void CreateSurfaceFromElementFaces(string preferredName,
                                                   Dictionary<FeFaceName, HashSet<int>> faceNameElementIds,
                                                   HashSet<string> reservedNames,
                                                   bool update = false)
        {
            string surfaceName = GetUniqueMeshEntityName(preferredName, reservedNames);
            FeSurface surface = new FeSurface(surfaceName);
            surface.CreatedFrom = FeSurfaceCreatedFrom.Faces;
            foreach (var entry in faceNameElementIds.OrderBy(entry => entry.Key))
            {
                string elementSetName = GetUniqueMeshEntityName(surfaceName + "_" + entry.Key, reservedNames);
                FeElementSet elementSet = new FeElementSet(elementSetName, entry.Value.OrderBy(id => id).ToArray());
                elementSet.Internal = true;
                _controller.AddElementSet(elementSet);
                surface.AddElementFace(entry.Key, elementSetName);
            }
            _controller.AddImportedSurface(surface, update);
        }
        private void AddSurfaceElementFace(Dictionary<FeFaceName, HashSet<int>> faceNameElementIds,
                                           FeFaceName faceName,
                                           int elementId)
        {
            if (!faceNameElementIds.TryGetValue(faceName, out HashSet<int> elementIds))
            {
                elementIds = new HashSet<int>();
                faceNameElementIds.Add(faceName, elementIds);
            }
            elementIds.Add(elementId);
        }
        private async Task ImportGeneratedInpFileAsync(string fileName)
        {
            HashSet<string> existingPartNames = new HashSet<string>(_controller.GetModelPartNames() ?? new string[0],
                                                                    StringComparer.OrdinalIgnoreCase);
            WriteDataToOutput("Importing generated .inp mesh...");
            //
            await _controller.ImportFileAsync(fileName, false);
            string[] currentPartNames = _controller.GetModelPartNames() ?? new string[0];
            string[] importedPartNames = currentPartNames.Where(partName => !existingPartNames.Contains(partName)).ToArray();
            if (importedPartNames.Length == 0) importedPartNames = currentPartNames;
            importedPartNames = MirrorSweepStanchionPartsIfNeeded(importedPartNames);
            string[] createdSurfaceNames = CreateStanchionSurfacesFromModelCylinders(importedPartNames);
            if (createdSurfaceNames.Length > 0)
                WriteDataToOutput("Created stanchion surfaces: " + string.Join(", ", createdSurfaceNames));
            SetFrontBackView(true, true);   // animate must be true in order for the scale bar to work correctly
            //
            int numErrors = _controller.GetNumberOfErrors();
            if (numErrors > 0)
            {
                _controller.OutputErrors();
                string message = "There were errors while importing the generated .inp file.";
                WriteDataToOutput(message);
                AutoClosingMessageBox.ShowError(message, 3000);
            }
        }
        private string[] MirrorSweepStanchionPartsIfNeeded(string[] importedPartNames)
        {
            if (importedPartNames == null || importedPartNames.Length == 0) return importedPartNames;
            FeMesh mesh = _controller.DisplayedMesh;
            if (mesh == null || mesh.Nodes == null || mesh.Parts == null) return importedPartNames;
            //
            FeCylinder[] stanchionCylinders = (_controller.GetAllModelCylinders() ?? new FeCylinder[0])
                .Where(cylinder => cylinder != null &&
                                   NormalizeEntityLookupName(cylinder.Name).StartsWith(
                                       NormalizeEntityLookupName(StanchionInsertCylinderName)))
                .ToArray();
            if (stanchionCylinders.Length < 2) return importedPartNames;
            //
            double xTolerance = GetSRSuntourXDetectionTolerance(stanchionCylinders);
            bool hasNegativeStanchion = stanchionCylinders.Any(cylinder => cylinder.Center[0] < -xTolerance);
            bool hasPositiveStanchion = stanchionCylinders.Any(cylinder => cylinder.Center[0] > xTolerance);
            if (!hasNegativeStanchion || !hasPositiveStanchion) return importedPartNames;
            //
            GetPartXExtents(mesh, importedPartNames, out double minX, out double maxX);
            bool importedOnNegativeSide = minX < -xTolerance;
            bool importedOnPositiveSide = maxX > xTolerance;
            if (importedOnNegativeSide && importedOnPositiveSide) return importedPartNames;
            if (!importedOnNegativeSide && !importedOnPositiveSide) return importedPartNames;
            //
            string[] beforePartNames = _controller.GetModelPartNames() ?? new string[0];
            HashSet<string> beforePartNameSet = new HashSet<string>(beforePartNames, StringComparer.OrdinalIgnoreCase);
            _controller.MirrorModelPartsCommand(importedPartNames, new double[] { 0, 0, 0 }, new double[] { 1, 0, 0 }, true);
            string[] afterPartNames = _controller.GetModelPartNames() ?? new string[0];
            string[] mirroredPartNames = afterPartNames.Where(partName => !beforePartNameSet.Contains(partName)).ToArray();
            //
            if (mirroredPartNames.Length > 0)
            {
                WriteDataToOutput("Created mirrored stanchion part(s): " + string.Join(", ", mirroredPartNames));
                return importedPartNames.Concat(mirroredPartNames).ToArray();
            }
            return importedPartNames;
        }
        private void GetPartXExtents(FeMesh mesh, string[] partNames, out double minX, out double maxX)
        {
            minX = double.MaxValue;
            maxX = double.MinValue;
            foreach (string partName in partNames)
            {
                if (string.IsNullOrWhiteSpace(partName)) continue;
                if (!mesh.Parts.TryGetValue(partName, out BasePart part) || part.NodeLabels == null) continue;
                foreach (int nodeId in part.NodeLabels)
                {
                    if (!mesh.Nodes.TryGetValue(nodeId, out FeNode node)) continue;
                    minX = Math.Min(minX, node.X);
                    maxX = Math.Max(maxX, node.X);
                }
            }
            if (minX == double.MaxValue) minX = 0;
            if (maxX == double.MinValue) maxX = 0;
        }
        private string GetSweepMeshPythonScriptFileName()
        {
            string scriptFileName = _controller.Settings.General.SweepMeshPythonScript;
            if (!string.IsNullOrWhiteSpace(scriptFileName))
            {
                if (File.Exists(scriptFileName)) return Path.GetFullPath(scriptFileName);
                else throw new CaeException("The configured sweep mesh Python script does not exist:" +
                                            Environment.NewLine + scriptFileName);
            }
            //
            scriptFileName = FindFileInParentDirectories(Application.StartupPath, Path.Combine("Python", "sweep_mesh_cpu_gui.py"));
            if (scriptFileName == null && _controller.OpenedFileName != null)
            {
                string openedFileDirectory = Path.GetDirectoryName(_controller.OpenedFileName);
                scriptFileName = FindFileInParentDirectories(openedFileDirectory,
                                                             Path.Combine("Python", "sweep_mesh_cpu_gui.py"));
            }
            //
            if (scriptFileName == null)
                throw new CaeException("The sweep_mesh_cpu_gui.py script could not be found." + Environment.NewLine +
                                       "Set it in Settings -> General -> Python -> Sweep mesh script.");
            //
            return scriptFileName;
        }
        private string GetCylindricalExtrusionPythonScriptFileName()
        {
            string scriptFileName = _controller.Settings.General.CylindricalExtrusionPythonScript;
            if (!string.IsNullOrWhiteSpace(scriptFileName))
            {
                if (File.Exists(scriptFileName)) return Path.GetFullPath(scriptFileName);
                else throw new CaeException("The configured cylindrical extrusion Python script does not exist:" +
                                            Environment.NewLine + scriptFileName);
            }
            //
            scriptFileName = FindFileInParentDirectories(Application.StartupPath, Path.Combine("Python", "cylindrical_extrusion.py"));
            if (scriptFileName == null && _controller.OpenedFileName != null)
            {
                string openedFileDirectory = Path.GetDirectoryName(_controller.OpenedFileName);
                scriptFileName = FindFileInParentDirectories(openedFileDirectory,
                                                             Path.Combine("Python", "cylindrical_extrusion.py"));
            }
            //
            if (scriptFileName == null)
                throw new CaeException("The cylindrical_extrusion.py script could not be found." + Environment.NewLine +
                                       "Set it in Settings -> General -> Python -> Cylindrical extrusion script.");
            //
            return scriptFileName;
        }
        private string FindFileInParentDirectories(string startDirectory, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(startDirectory)) return null;
            //
            DirectoryInfo directory = new DirectoryInfo(startDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            return null;
        }
        private SweepMeshPythonResult RunSweepMeshPythonTool(string scriptFileName, double[] stanchionTop)
        {
            string reportDirectory = Path.Combine(Path.GetTempPath(), "PrePoMax");
            string reportFileName = Path.Combine(reportDirectory, "sweep_mesh_" + Guid.NewGuid().ToString("N") + ".report");
            Directory.CreateDirectory(reportDirectory);
            //
            Exception lastStartException = null;
            bool autoDetect = string.IsNullOrWhiteSpace(_controller.Settings.General.PythonExecutable);
            foreach (var executable in GetPythonExecutableCandidates())
            {
                try
                {
                    WriteDataToOutput("Trying Python executable: " + executable);
                    SweepMeshPythonResult result = RunSweepMeshPythonTool(executable, scriptFileName, reportFileName,
                                                                         stanchionTop);
                    if (autoDetect && !result.ReportFound && result.Status == "failed") continue;
                    return result;
                }
                catch (Win32Exception ex)
                {
                    lastStartException = ex;
                    if (!autoDetect) break;
                }
            }
            //
            if (!string.IsNullOrWhiteSpace(_controller.Settings.General.PythonExecutable))
                throw new CaeException("The configured Python executable could not be started:" + Environment.NewLine +
                                       _controller.Settings.General.PythonExecutable);
            //
            throw new CaeException("No Python executable could be started. Configure Settings -> General -> Python -> " +
                                   "Python executable or make py/python available in PATH." +
                                   (lastStartException == null ? "" : Environment.NewLine + lastStartException.Message));
        }
        private CylindricalExtrusionPythonResult RunCylindricalExtrusionPythonTool(string scriptFileName,
                                                                                   string exportInpFileName,
                                                                                   string outputInpFileName,
                                                                                   string deltaInpFileName,
                                                                                   string payloadFileName,
                                                                                   string cylinderJsonFileName)
        {
            string reportDirectory = Path.Combine(Path.GetTempPath(), "PrePoMax");
            string reportFileName = Path.Combine(reportDirectory, "cylindrical_extrusion_" + Guid.NewGuid().ToString("N") + ".report");
            Directory.CreateDirectory(reportDirectory);
            //
            Exception lastStartException = null;
            bool autoDetect = string.IsNullOrWhiteSpace(_controller.Settings.General.PythonExecutable);
            foreach (var executable in GetPythonExecutableCandidates())
            {
                try
                {
                    WriteDataToOutput("Trying Python executable: " + executable);
                    CylindricalExtrusionPythonResult result = RunCylindricalExtrusionPythonTool(
                        executable, scriptFileName, reportFileName, exportInpFileName, outputInpFileName,
                        deltaInpFileName, payloadFileName, cylinderJsonFileName);
                    if (autoDetect && !result.ReportFound && result.Status == "failed") continue;
                    return result;
                }
                catch (Win32Exception ex)
                {
                    lastStartException = ex;
                    if (!autoDetect) break;
                }
            }
            //
            if (!string.IsNullOrWhiteSpace(_controller.Settings.General.PythonExecutable))
                throw new CaeException("The configured Python executable could not be started:" + Environment.NewLine +
                                       _controller.Settings.General.PythonExecutable);
            //
            throw new CaeException("No Python executable could be started. Configure Settings -> General -> Python -> " +
                                   "Python executable or make py/python available in PATH." +
                                   (lastStartException == null ? "" : Environment.NewLine + lastStartException.Message));
        }
        private IEnumerable<string> GetPythonExecutableCandidates()
        {
            string pythonExecutable = _controller.Settings.General.PythonExecutable;
            if (!string.IsNullOrWhiteSpace(pythonExecutable)) yield return pythonExecutable;
            else
            {
                yield return "py";
                yield return "python";
            }
        }
        private SweepMeshPythonResult RunSweepMeshPythonTool(string executable,
                                                             string scriptFileName,
                                                             string reportFileName,
                                                             double[] stanchionTop)
        {
            if (File.Exists(reportFileName)) File.Delete(reportFileName);
            //
            int exitCode = 0;
            StringBuilder errorBuilder = new StringBuilder();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = executable;
            startInfo.Arguments = QuoteCommandLineArgument(scriptFileName) + " --report-file " +
                                  QuoteCommandLineArgument(reportFileName);
            if (stanchionTop != null && stanchionTop.Length >= 3)
            {
                startInfo.Arguments += " --stanchion-top " +
                                       FormatPythonNumberArgument(stanchionTop[0]) + " " +
                                       FormatPythonNumberArgument(stanchionTop[1]) + " " +
                                       FormatPythonNumberArgument(stanchionTop[2]);
            }
            startInfo.WorkingDirectory = Path.GetDirectoryName(scriptFileName);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = false;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            //
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null) WriteDataToOutput(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        WriteDataToOutput(e.Data);
                    }
                };
                //
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            //
            SweepMeshPythonResult result = ReadSweepMeshPythonResult(reportFileName);
            if (result == null)
            {
                result = new SweepMeshPythonResult();
                result.Status = exitCode == 0 ? "cancelled" : "failed";
                result.Error = errorBuilder.ToString().Trim();
                result.ReportFound = false;
                if (string.IsNullOrWhiteSpace(result.Error) && exitCode != 0)
                    result.Error = "The Python process exited with code " + exitCode + ".";
                else if (string.IsNullOrWhiteSpace(result.Error) && exitCode == 0)
                    result.Error = "The Python sweep mesh tool closed without generating a completion report.";
            }
            else if (result.Status != "completed" && string.IsNullOrWhiteSpace(result.Error) &&
                     errorBuilder.Length > 0)
            {
                result.Error = errorBuilder.ToString().Trim();
            }
            //
            if (File.Exists(reportFileName)) File.Delete(reportFileName);
            return result;
        }
        private CylindricalExtrusionPythonResult RunCylindricalExtrusionPythonTool(string executable,
                                                                                   string scriptFileName,
                                                                                   string reportFileName,
                                                                                   string exportInpFileName,
                                                                                   string outputInpFileName,
                                                                                   string deltaInpFileName,
                                                                                   string payloadFileName,
                                                                                   string cylinderJsonFileName)
        {
            if (File.Exists(reportFileName)) File.Delete(reportFileName);
            //
            int exitCode = 0;
            StringBuilder errorBuilder = new StringBuilder();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = executable;
            startInfo.Arguments = QuoteCommandLineArgument(scriptFileName) +
                                  " " + QuoteCommandLineArgument(exportInpFileName) +
                                  " " + QuoteCommandLineArgument(outputInpFileName) +
                                  " --report-file " + QuoteCommandLineArgument(reportFileName) +
                                  " --payload-file " + QuoteCommandLineArgument(payloadFileName) +
                                  " --delta-inp-file " + QuoteCommandLineArgument(deltaInpFileName);
            if (!string.IsNullOrWhiteSpace(cylinderJsonFileName))
                startInfo.Arguments += " --json " + QuoteCommandLineArgument(cylinderJsonFileName);
            startInfo.WorkingDirectory = Path.GetDirectoryName(scriptFileName);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = false;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            //
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null) WriteDataToOutput(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        WriteDataToOutput(e.Data);
                    }
                };
                //
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            //
            CylindricalExtrusionPythonResult result = ReadCylindricalExtrusionPythonResult(reportFileName);
            if (result == null)
            {
                result = new CylindricalExtrusionPythonResult();
                result.Status = exitCode == 0 ? "cancelled" : "failed";
                result.Error = errorBuilder.ToString().Trim();
                result.ReportFound = false;
                if (string.IsNullOrWhiteSpace(result.Error) && exitCode != 0)
                    result.Error = "The Python process exited with code " + exitCode + ".";
                else if (string.IsNullOrWhiteSpace(result.Error) && exitCode == 0)
                    result.Error = "The Python cylindrical extrusion tool closed without generating a completion report.";
            }
            else if (result.Status != "completed" && string.IsNullOrWhiteSpace(result.Error) &&
                     errorBuilder.Length > 0)
            {
                result.Error = errorBuilder.ToString().Trim();
            }
            //
            if (File.Exists(reportFileName)) File.Delete(reportFileName);
            return result;
        }
        private SweepMeshPythonResult ReadSweepMeshPythonResult(string reportFileName)
        {
            Dictionary<string, string> values = ReadPythonReportValues(reportFileName);
            if (values == null) return null;
            //
            SweepMeshPythonResult result = new SweepMeshPythonResult();
            result.ReportFound = true;
            string status;
            values.TryGetValue("status", out status);
            result.Status = string.IsNullOrWhiteSpace(status) ? "cancelled" : status;
            //
            string outputPath;
            values.TryGetValue("output_path_b64", out outputPath);
            result.OutputPath = DecodeBase64ReportValue(outputPath);
            //
            string error;
            values.TryGetValue("error_b64", out error);
            result.Error = DecodeBase64ReportValue(error);
            //
            return result;
        }
        private CylindricalExtrusionPythonResult ReadCylindricalExtrusionPythonResult(string reportFileName)
        {
            Dictionary<string, string> values = ReadPythonReportValues(reportFileName);
            if (values == null) return null;
            //
            CylindricalExtrusionPythonResult result = new CylindricalExtrusionPythonResult();
            result.ReportFound = true;
            string status;
            values.TryGetValue("status", out status);
            result.Status = string.IsNullOrWhiteSpace(status) ? "cancelled" : status;
            //
            string outputPath;
            values.TryGetValue("output_path_b64", out outputPath);
            result.OutputPath = DecodeBase64ReportValue(outputPath);
            //
            string deltaInpPath;
            values.TryGetValue("delta_inp_path_b64", out deltaInpPath);
            result.DeltaInpPath = DecodeBase64ReportValue(deltaInpPath);
            //
            string payloadPath;
            values.TryGetValue("payload_path_b64", out payloadPath);
            result.PayloadPath = DecodeBase64ReportValue(payloadPath);
            //
            string error;
            values.TryGetValue("error_b64", out error);
            result.Error = DecodeBase64ReportValue(error);
            //
            return result;
        }
        private Dictionary<string, string> ReadPythonReportValues(string reportFileName)
        {
            if (!File.Exists(reportFileName)) return null;
            //
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(reportFileName))
            {
                int separator = line.IndexOf('=');
                if (separator > 0)
                {
                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();
                    values[key] = value;
                }
            }
            return values;
        }
        private void ImportCylindricalExtrusionPayload(CylindricalExtrusionPayload payload, bool importGeneratedPhase)
        {
            if (payload == null) throw new CaeException("The cylindrical extrusion surface payload could not be read.");
            //
            List<CylindricalExtrusionElementSetPayload> elementSetsPayload;
            List<CylindricalExtrusionSurfacePayload> surfacesPayload;
            if (importGeneratedPhase)
            {
                elementSetsPayload = payload.GeneratedElementSets;
                surfacesPayload = payload.GeneratedSurfaces;
            }
            else
            {
                elementSetsPayload = payload.ExistingElementSets;
                surfacesPayload = payload.ExistingSurfaces;
            }
            //
            Dictionary<string, string> elementSetNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> reservedNames = new HashSet<string>(_controller.GetAllMeshEntityNames(), StringComparer.OrdinalIgnoreCase);
            //
            if (elementSetsPayload != null)
            {
                foreach (var elementSetPayload in elementSetsPayload)
                {
                    if (elementSetPayload == null || string.IsNullOrWhiteSpace(elementSetPayload.Name)) continue;
                    string uniqueName = GetUniqueMeshEntityName(elementSetPayload.Name, reservedNames);
                    FeElementSet elementSet = new FeElementSet(uniqueName, (elementSetPayload.Elements ?? new List<int>()).ToArray());
                    elementSet.Internal = true;
                    _controller.AddElementSet(elementSet);
                    elementSetNameMap[elementSetPayload.Name] = uniqueName;
                }
            }
            //
            if (surfacesPayload != null)
            {
                List<FeSurface> surfaces = new List<FeSurface>();
                foreach (var surfacePayload in surfacesPayload)
                {
                    if (surfacePayload == null || string.IsNullOrWhiteSpace(surfacePayload.Name)) continue;
                    string uniqueName = GetUniqueMeshEntityName(surfacePayload.Name, reservedNames);
                    FeSurface surface = new FeSurface(uniqueName);
                    surface.CreatedFrom = FeSurfaceCreatedFrom.Faces;
                    //
                    if (surfacePayload.Faces != null)
                    {
                        foreach (var facePayload in surfacePayload.Faces)
                        {
                            if (facePayload == null || string.IsNullOrWhiteSpace(facePayload.ElementSetName)) continue;
                            string elementSetName;
                            if (!elementSetNameMap.TryGetValue(facePayload.ElementSetName, out elementSetName))
                                elementSetName = facePayload.ElementSetName;
                            FeFaceName faceName;
                            if (!Enum.TryParse(facePayload.FaceName, true, out faceName) || faceName == FeFaceName.Empty)
                                throw new CaeException("Unsupported face name in cylindrical extrusion payload: " + facePayload.FaceName);
                            surface.AddElementFace(faceName, elementSetName);
                        }
                    }
                    surfaces.Add(surface);
                }
                //
                for (int i = 0; i < surfaces.Count; i++)
                {
                    _controller.AddImportedSurface(surfaces[i], i == surfaces.Count - 1);
                }
            }
        }
        private CylindricalExtrusionPayload ReadCylindricalExtrusionPayload(string payloadFileName)
        {
            using (FileStream stream = new FileStream(payloadFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(CylindricalExtrusionPayload));
                return serializer.ReadObject(stream) as CylindricalExtrusionPayload;
            }
        }
        private string GetUniqueMeshEntityName(string preferredName, HashSet<string> reservedNames)
        {
            string uniqueName = preferredName;
            if (reservedNames.Contains(uniqueName))
                uniqueName = reservedNames.GetNextNumberedKey(preferredName);
            reservedNames.Add(uniqueName);
            return uniqueName;
        }
        private string GetUniqueModelCylinderName(string preferredName, HashSet<string> reservedNames)
        {
            string uniqueName = preferredName;
            if (reservedNames.Contains(uniqueName))
                uniqueName = reservedNames.GetNextNumberedKey(preferredName);
            reservedNames.Add(uniqueName);
            return uniqueName;
        }
        private void InitializeSRSuntourMenu()
        {
            _tsmiImportCylinderDefinitionsJson = new ToolStripMenuItem("Import Cylinder Definitions (JSON)");
            _tsmiImportCylinderDefinitionsJson.Click += tsmiImportCylinderDefinitionsJson_Click;
            //
            _tsmiSRSuntourImportMesh = new ToolStripMenuItem("1. Import Existing Mesh");
            _tsmiSRSuntourImportMesh.Click += tsmiSRSuntourImportMesh_Click;
            //
            _tsmiSRSuntourAssignCylinders = new ToolStripMenuItem("2. Auto Assign / Mirror Cylinders");
            _tsmiSRSuntourAssignCylinders.Click += tsmiSRSuntourAssignCylinders_Click;
            //
            _tsmiSRSuntourCreateContacts = new ToolStripMenuItem("6. Create Stanchion Contacts");
            _tsmiSRSuntourCreateContacts.Click += tsmiSRSuntourCreateContacts_Click;
            //
            _tsmiImportCylinderDefinitionsJson.Text = "2. Import Cylinder Definitions (JSON)";
            tsmiCylindricalExtrusionPython.Text = "3. Extrude Cylinders";
            tsmiAutoCreateCylindricalTies.Text = "4. Auto Create Cylindrical Ties";
            tsmiSweepMeshPython.Text = "5. Sweep Stanchion Mesh (Python)";
            //
            _tsmiSRSuntour = new ToolStripMenuItem("SRSuntour");
            _tsmiSRSuntour.DropDownItems.AddRange(new ToolStripItem[]
            {
                _tsmiSRSuntourImportMesh,
                _tsmiImportCylinderDefinitionsJson,
                _tsmiSRSuntourAssignCylinders,
                new ToolStripSeparator(),
                tsmiCylindricalExtrusionPython,
                tsmiAutoCreateCylindricalTies,
                tsmiSweepMeshPython,
                _tsmiSRSuntourCreateContacts
            });
            //
            int insertIndex = menuStripMain.Items.IndexOf(tsmiFile);
            menuStripMain.Items.Insert(insertIndex >= 0 ? insertIndex + 1 : 1, _tsmiSRSuntour);
        }
        private void InitializeCompatibilityExportMenu()
        {
            ToolStripMenuItem tsmiExportPmx240 = new ToolStripMenuItem("Export Project for PrePoMax 2.4.0");
            tsmiExportPmx240.Name = "tsmiExportPmx240";
            tsmiExportPmx240.Click += tsmiExportPmx240_Click;
            int saveAsIndex = tsmiFile.DropDownItems.IndexOf(tsmiSaveAs);
            if (saveAsIndex >= 0) tsmiFile.DropDownItems.Insert(saveAsIndex + 1, tsmiExportPmx240);
            else tsmiFile.DropDownItems.Add(tsmiExportPmx240);
        }
        private void InitializeDisplayedFrdExportMenu()
        {
            ToolStripMenuItem tsmiExportDisplayedFrd = new ToolStripMenuItem("Displayed results *.frd");
            tsmiExportDisplayedFrd.Name = "tsmiExportDisplayedFrd";
            tsmiExportDisplayedFrd.Click += tsmiExportDisplayedFrd_Click;
            int insertIndex = tsmiExport.DropDownItems.IndexOf(tsmiExportToDeformedInp);
            if (insertIndex >= 0) tsmiExport.DropDownItems.Insert(insertIndex, tsmiExportDisplayedFrd);
            else tsmiExport.DropDownItems.Add(tsmiExportDisplayedFrd);
        }
        private void InitializeContactValidationMenu()
        {
            _tsmiCheckContactPair = new ToolStripMenuItem("Check Contact");
            _tsmiCheckContactPair.Name = "tsmiCheckContactPair";
            _tsmiCheckContactPair.Click += tsmiCheckContactPair_Click;
            //
            int insertIndex = contactPairToolStripMenuItem.DropDownItems.IndexOf(tsmiDividerContactPair1);
            if (insertIndex >= 0) contactPairToolStripMenuItem.DropDownItems.Insert(insertIndex, _tsmiCheckContactPair);
            else contactPairToolStripMenuItem.DropDownItems.Add(_tsmiCheckContactPair);
        }
        private void SetSRSuntourMenuEnabled(bool enabled)
        {
            if (_tsmiSRSuntour == null) return;
            _tsmiSRSuntour.Enabled = true;
            foreach (ToolStripItem item in _tsmiSRSuntour.DropDownItems) item.Enabled = enabled;
            if (_tsmiSRSuntourImportMesh != null) _tsmiSRSuntourImportMesh.Enabled = enabled;
            if (_tsmiImportCylinderDefinitionsJson != null) _tsmiImportCylinderDefinitionsJson.Enabled = enabled;
            if (_tsmiSRSuntourAssignCylinders != null) _tsmiSRSuntourAssignCylinders.Enabled = enabled;
            if (_tsmiSRSuntourCreateContacts != null) _tsmiSRSuntourCreateContacts.Enabled = enabled;
        }
        private void ImportCylinderDefinitionsFromJson()
        {
            if (!_controller.ModelInitialized)
                throw new CaeException("There is no model to import into. First create a new model.");
            //
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Json files|*.json|All files|*.*";
                openFileDialog.Title = "Import cylinder definitions";
                openFileDialog.FileName = "cylinder_data.json";
                //
                if (openFileDialog.ShowDialog(this) != DialogResult.OK) return;
                //
                CylinderDefinitionsPayload payload = ReadCylinderDefinitionsPayload(openFileDialog.FileName);
                if (payload == null || payload.Cylinders == null || payload.Cylinders.Count == 0)
                    throw new CaeException("No cylinder definitions were found in the selected JSON file.");
                //
                HashSet<string> reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string[] existingCylinderNames = _controller.GetModelCylinderNames();
                if (existingCylinderNames != null) reservedNames.UnionWith(existingCylinderNames);
                int importedCount = 0;
                foreach (var cylinderPayload in payload.Cylinders)
                {
                    FeCylinder cylinder = CreateModelCylinder(cylinderPayload, reservedNames);
                    _controller.AddModelCylinderCommand(cylinder);
                    importedCount++;
                }
                int renamed;
                int mirrored;
                AssignSRSuntourCylinderNamesAndMirrors(out renamed, out mirrored);
                //
                WriteDataToOutput("Imported " + importedCount.ToString() + " cylinder definition(s) from " +
                                  openFileDialog.FileName);
                WriteDataToOutput("SRSuntour cylinder assignment renamed " + renamed.ToString() +
                                  " cylinder(s) and created " + mirrored.ToString() + " mirrored cylinder(s).");
            }
        }
        private void AssignSRSuntourCylinderNamesAndMirrors(out int renamed, out int mirrored)
        {
            renamed = 0;
            mirrored = 0;
            if (!_controller.ModelInitialized)
                throw new CaeException("There is no model. First create or open a model.");
            //
            FeCylinder[] cylinders = _controller.GetAllModelCylinders();
            if (cylinders == null || cylinders.Length == 0)
                throw new CaeException("No model cylinders are available.");
            //
            double xTolerance = GetSRSuntourXDetectionTolerance(cylinders);
            HashSet<string> reservedNames = new HashSet<string>(_controller.GetModelCylinderNames() ?? new string[0],
                                                                StringComparer.OrdinalIgnoreCase);
            //
            FeCylinder steertube = cylinders.OrderBy(cylinder => Math.Abs(cylinder.Center[0])).FirstOrDefault();
            if (steertube != null && Math.Abs(steertube.Center[0]) <= xTolerance)
                renamed += RenameSRSuntourCylinder(steertube, SteertubeInsertCylinderName, reservedNames);
            //
            List<FeCylinder> offsetCylinders = cylinders.Where(cylinder => Math.Abs(cylinder.Center[0]) > xTolerance)
                                                        .OrderByDescending(cylinder => cylinder.Center[1])
                                                        .ToList();
            if (offsetCylinders.Count >= 1)
                renamed += RenameSRSuntourCylinder(offsetCylinders[0], StanchionInsertCylinderName, reservedNames);
            if (offsetCylinders.Count >= 2)
                renamed += RenameSRSuntourCylinder(offsetCylinders[1], TopBushingCylinderName, reservedNames);
            if (offsetCylinders.Count >= 3)
                renamed += RenameSRSuntourCylinder(offsetCylinders[2], BottomBushingCylinderName, reservedNames);
            //
            cylinders = _controller.GetAllModelCylinders();
            if (HasMeshNodesOnPositiveX(xTolerance))
            {
                reservedNames = new HashSet<string>(_controller.GetModelCylinderNames() ?? new string[0],
                                                    StringComparer.OrdinalIgnoreCase);
                foreach (FeCylinder cylinder in cylinders.Where(cylinder => Math.Abs(cylinder.Center[0]) > xTolerance)
                                                         .OrderBy(cylinder => cylinder.Name))
                {
                    if (HasMirroredCylinder(cylinder, cylinders, xTolerance)) continue;
                    FeCylinder mirroredCylinder = cylinder.DeepClone();
                    mirroredCylinder.Center = new double[] { -cylinder.Center[0], cylinder.Center[1], cylinder.Center[2] };
                    mirroredCylinder.Direction = new double[] { -cylinder.Direction[0], cylinder.Direction[1], cylinder.Direction[2] };
                    mirroredCylinder.Name = GetUniqueModelCylinderName(cylinder.Name + "_Mirror", reservedNames);
                    _controller.AddModelCylinderCommand(mirroredCylinder);
                    mirrored++;
                }
            }
        }
        private int RenameSRSuntourCylinder(FeCylinder cylinder, string targetName, HashSet<string> reservedNames)
        {
            if (cylinder == null || NormalizeEntityLookupName(cylinder.Name) == NormalizeEntityLookupName(targetName))
                return 0;
            //
            reservedNames.Remove(cylinder.Name);
            string uniqueName = GetUniqueModelCylinderName(targetName, reservedNames);
            FeCylinder renamedCylinder = cylinder.DeepClone();
            renamedCylinder.Name = uniqueName;
            _controller.ReplaceModelCylinderCommand(cylinder.Name, renamedCylinder);
            return 1;
        }
        private double GetSRSuntourXDetectionTolerance(FeCylinder[] cylinders)
        {
            double maxRadius = 0;
            foreach (FeCylinder cylinder in cylinders)
            {
                if (cylinder == null) continue;
                maxRadius = Math.Max(maxRadius, Math.Abs(cylinder.Radius));
                maxRadius = Math.Max(maxRadius, Math.Abs(cylinder.TargetRadius));
            }
            return Math.Max(1E-3, maxRadius * 1E-3);
        }
        private bool HasMeshNodesOnPositiveX(double xTolerance)
        {
            FeMesh mesh = _controller.DisplayedMesh;
            if (mesh == null || mesh.Nodes == null || mesh.Nodes.Count == 0) return false;
            double tolerance = Math.Max(xTolerance, GetMeshXSpan(mesh) * 1E-3);
            return mesh.Nodes.Values.Any(node => node.X > tolerance);
        }
        private double GetMeshXSpan(FeMesh mesh)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            foreach (FeNode node in mesh.Nodes.Values)
            {
                min = Math.Min(min, node.X);
                max = Math.Max(max, node.X);
            }
            if (min == double.MaxValue || max == double.MinValue) return 0;
            return max - min;
        }
        private bool HasMirroredCylinder(FeCylinder cylinder, FeCylinder[] cylinders, double xTolerance)
        {
            double coordinateTolerance = Math.Max(xTolerance, cylinder.Tolerance);
            foreach (FeCylinder candidate in cylinders)
            {
                if (ReferenceEquals(candidate, cylinder)) continue;
                if (Math.Abs(candidate.Center[0] + cylinder.Center[0]) > coordinateTolerance) continue;
                if (Math.Abs(candidate.Center[1] - cylinder.Center[1]) > coordinateTolerance) continue;
                if (Math.Abs(candidate.Center[2] - cylinder.Center[2]) > coordinateTolerance) continue;
                if (Math.Abs(candidate.Radius - cylinder.Radius) > Math.Max(candidate.Tolerance, cylinder.Tolerance)) continue;
                return true;
            }
            return false;
        }
        private int CreateSRSuntourContactDefinitions(out int skippedExisting, out int missingSurfaces)
        {
            skippedExisting = 0;
            missingSurfaces = 0;
            if (_controller.Model == null || _controller.Model.Mesh == null)
                throw new CaeException("There is no model mesh.");
            //
            string surfaceInteractionName = EnsureSRSuntourSurfaceInteraction();
            string[] allSurfaceNames = _controller.GetAllSurfaceNames() ?? new string[0];
            Dictionary<string, string> surfaceNameMap = allSurfaceNames.ToDictionary(name => name,
                                                                                     name => name,
                                                                                     StringComparer.OrdinalIgnoreCase);
            HashSet<string> existingPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ContactPair contactPair in _controller.GetAllContactPairs() ?? new ContactPair[0])
            {
                if (contactPair.MasterRegionType == RegionTypeEnum.SurfaceName &&
                    contactPair.SlaveRegionType == RegionTypeEnum.SurfaceName)
                    existingPairs.Add(GetSurfacePairKey(contactPair.MasterRegionName, contactPair.SlaveRegionName));
            }
            //
            FeCylinder[] cylinders = _controller.GetAllModelCylinders() ?? new FeCylinder[0];
            int created = 0;
            for (int i = 0; i < cylinders.Length; i++)
            {
                FeCylinder cylinder = cylinders[i];
                if (cylinder == null || string.IsNullOrWhiteSpace(cylinder.Name)) continue;
                //
                string stanchionSurfaceName = FindSRSuntourStanchionSurfaceName(cylinder.Name, surfaceNameMap);
                string outerSurfaceName = FindSRSuntourOuterSurfaceName(i + 1, surfaceNameMap.Keys);
                if (string.IsNullOrWhiteSpace(stanchionSurfaceName) || string.IsNullOrWhiteSpace(outerSurfaceName))
                {
                    missingSurfaces++;
                    continue;
                }
                //
                string pairKey = GetSurfacePairKey(outerSurfaceName, stanchionSurfaceName);
                if (existingPairs.Contains(pairKey))
                {
                    skippedExisting++;
                    continue;
                }
                //
                string contactPairName = GetUniqueContactPairName(outerSurfaceName + CaeMesh.Globals.MasterSlaveSeparator +
                                                                  stanchionSurfaceName);
                ContactPair contactPair = new ContactPair(contactPairName,
                                                          surfaceInteractionName,
                                                          outerSurfaceName,
                                                          RegionTypeEnum.SurfaceName,
                                                          stanchionSurfaceName,
                                                          RegionTypeEnum.SurfaceName);
                _controller.AddContactPairCommand(contactPair, true);
                existingPairs.Add(pairKey);
                created++;
            }
            return created;
        }
        private string EnsureSRSuntourSurfaceInteraction()
        {
            string[] existingNames = _controller.GetSurfaceInteractionNames() ?? new string[0];
            string existingName = existingNames.FirstOrDefault(name =>
                string.Equals(name, SRSuntourSurfaceInteractionName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(existingName)) return existingName;
            //
            SurfaceInteraction surfaceInteraction = new SurfaceInteraction(SRSuntourSurfaceInteractionName);
            SurfaceBehavior surfaceBehavior = new SurfaceBehavior();
            surfaceBehavior.PressureOverclosureType = PressureOverclosureEnum.Linear;
            surfaceBehavior.K = ConvertSRSuntourContactStiffness();
            surfaceBehavior.Sinf = StringPressureConverter.ConvertToCurrentUnits("1 MPa");
            surfaceBehavior.C0 = double.NaN;
            surfaceInteraction.AddProperty(surfaceBehavior);
            _controller.AddSurfaceInteractionCommand(surfaceInteraction);
            return surfaceInteraction.Name;
        }
        private double ConvertSRSuntourContactStiffness()
        {
            try
            {
                return StringForcePerVolumeConverter.ConvertToCurrentUnits("420000 N/mm³");
            }
            catch
            {
                return 420000;
            }
        }
        private string FindSRSuntourStanchionSurfaceName(string cylinderName, Dictionary<string, string> surfaceNameMap)
        {
            if (surfaceNameMap.TryGetValue(cylinderName, out string surfaceName)) return surfaceName;
            string normalizedCylinderName = NormalizeEntityLookupName(cylinderName);
            return surfaceNameMap.Keys.FirstOrDefault(name =>
                NormalizeEntityLookupName(name) == normalizedCylinderName);
        }
        private string FindSRSuntourOuterSurfaceName(int cylinderIndex, IEnumerable<string> surfaceNames)
        {
            string prefix = "CYL_" + cylinderIndex.ToString("00") + "_";
            return surfaceNames.FirstOrDefault(name =>
                name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                (name.EndsWith("_outer", StringComparison.OrdinalIgnoreCase) ||
                 name.EndsWith("_outter", StringComparison.OrdinalIgnoreCase)));
        }
        private string GetUniqueContactPairName(string preferredName)
        {
            HashSet<string> reservedNames = new HashSet<string>(_controller.GetContactPairNames() ?? new string[0],
                                                                StringComparer.OrdinalIgnoreCase);
            if (!reservedNames.Contains(preferredName)) return preferredName;
            return reservedNames.GetNextNumberedKey(preferredName);
        }
        private string GetSurfacePairKey(string surfaceName1, string surfaceName2)
        {
            if (string.Compare(surfaceName1, surfaceName2, StringComparison.OrdinalIgnoreCase) <= 0)
                return surfaceName1 + "|" + surfaceName2;
            else return surfaceName2 + "|" + surfaceName1;
        }
        private string PrepareCylinderDefinitionsJsonFile(string fileName)
        {
            FeCylinder[] cylinders = _controller.GetAllModelCylinders();
            if (cylinders == null || cylinders.Length == 0) return null;
            //
            CylinderDefinitionsPayload payload = new CylinderDefinitionsPayload();
            payload.Cylinders = cylinders.Select(cylinder => new CylinderDefinitionPayload()
            {
                Name = cylinder.Name,
                Center = cylinder.Center?.ToArray(),
                Radius = cylinder.Radius,
                Direction = cylinder.Direction?.ToArray(),
                Height = cylinder.Height,
                ExtraRadius = cylinder.ExtraRadius,
                Chamfer = cylinder.Chamfer?.ToArray(),
                TargetRadius = cylinder.TargetRadius,
                NumberOfLayers = cylinder.NumberOfLayers,
                Tolerance = cylinder.Tolerance
            }).ToList();
            //
            WriteCylinderDefinitionsPayload(fileName, payload);
            return fileName;
        }
        private FeCylinder CreateModelCylinder(CylinderDefinitionPayload cylinderPayload, HashSet<string> reservedNames)
        {
            string preferredName = cylinderPayload?.Name;
            preferredName = NamedClass.GetErrorFreeName(preferredName, "Cylinder", null);
            preferredName = GetUniqueModelCylinderName(preferredName, reservedNames);
            //
            FeCylinder cylinder = new FeCylinder(preferredName);
            if (cylinderPayload != null)
            {
                cylinder.Center = cylinderPayload.Center;
                cylinder.Radius = cylinderPayload.Radius;
                cylinder.Direction = cylinderPayload.Direction;
                cylinder.Height = cylinderPayload.Height;
                cylinder.ExtraRadius = cylinderPayload.ExtraRadius ?? 0;
                cylinder.Chamfer = cylinderPayload.Chamfer;
                cylinder.TargetRadius = cylinderPayload.TargetRadius ?? cylinder.Radius;
                cylinder.NumberOfLayers = cylinderPayload.NumberOfLayers ?? 1;
                cylinder.Tolerance = cylinderPayload.Tolerance ?? 0.025;
            }
            return cylinder;
        }
        private CylinderDefinitionsPayload ReadCylinderDefinitionsPayload(string fileName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(CylinderDefinitionsPayload));
                return serializer.ReadObject(stream) as CylinderDefinitionsPayload;
            }
        }
        private void WriteCylinderDefinitionsPayload(string fileName, CylinderDefinitionsPayload payload)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(CylinderDefinitionsPayload));
                serializer.WriteObject(stream, payload);
            }
        }
        private void DeleteIfExists(string fileName)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName)) File.Delete(fileName);
            }
            catch
            {
            }
        }
        private string DecodeBase64ReportValue(string encodedValue)
        {
            if (string.IsNullOrWhiteSpace(encodedValue)) return null;
            //
            try
            {
                byte[] bytes = Convert.FromBase64String(encodedValue);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return encodedValue;
            }
        }
        private double[] TryGetStanchionInsertTopCenter()
        {
            FeCylinder cylinder = FindModelCylinderByName(StanchionInsertCylinderName);
            if (cylinder == null) return null;
            //
            string error;
            if (!cylinder.IsProperlyDefined(out error))
                throw new CaeException("The model cylinder '" + cylinder.Name + "' is not properly defined." +
                                       Environment.NewLine + error);
            //
            return cylinder.GetHigherYEndCenter();
        }
        private string[] CreateStanchionSurfacesFromModelCylinders(string[] importedPartNames)
        {
            if (importedPartNames == null || importedPartNames.Length == 0) return new string[0];
            FeMesh mesh = _controller.DisplayedMesh;
            if (mesh == null || mesh.Nodes == null || mesh.Elements == null || mesh.Parts == null) return new string[0];
            //
            HashSet<int> importedElementIds = new HashSet<int>();
            HashSet<int> importedNodeIds = new HashSet<int>();
            foreach (string partName in importedPartNames)
            {
                if (string.IsNullOrWhiteSpace(partName)) continue;
                if (!mesh.Parts.TryGetValue(partName, out BasePart part)) continue;
                importedElementIds.UnionWith(part.Labels);
                importedNodeIds.UnionWith(part.NodeLabels);
            }
            if (importedElementIds.Count == 0 || importedNodeIds.Count == 0) return new string[0];
            //
            Dictionary<string, BoundaryFaceData> boundaryFaces = GetBoundaryFaces(mesh, importedElementIds);
            if (boundaryFaces.Count == 0) return new string[0];
            //
            HashSet<string> reservedNames = new HashSet<string>(_controller.GetAllMeshEntityNames() ?? new string[0],
                                                                StringComparer.OrdinalIgnoreCase);
            List<string> createdSurfaceNames = new List<string>();
            FeCylinder[] cylinders = _controller.GetAllModelCylinders();
            if (cylinders == null || cylinders.Length == 0) return createdSurfaceNames.ToArray();
            //
            foreach (FeCylinder cylinder in cylinders)
            {
                if (cylinder == null || string.IsNullOrWhiteSpace(cylinder.Name)) continue;
                if (!IsSRSuntourStanchionContactCylinder(cylinder.Name)) continue;
                string error;
                if (!cylinder.IsProperlyDefined(out error))
                {
                    WriteDataToOutput("Skipping cylinder '" + cylinder.Name + "': " + error);
                    continue;
                }
                //
                if (TryCreateStanchionSurface(mesh, importedNodeIds, boundaryFaces, cylinder, reservedNames,
                                              out string createdSurfaceName, out string message))
                {
                    createdSurfaceNames.Add(createdSurfaceName);
                    WriteDataToOutput(message);
                }
                else if (!string.IsNullOrWhiteSpace(message))
                    WriteDataToOutput(message);
            }
            //
            return createdSurfaceNames.ToArray();
        }
        private Dictionary<string, BoundaryFaceData> GetBoundaryFaces(FeMesh mesh, HashSet<int> elementIds)
        {
            Dictionary<string, BoundaryFaceData> boundaryFaces = new Dictionary<string, BoundaryFaceData>();
            if (mesh == null || elementIds == null || elementIds.Count == 0) return boundaryFaces;
            //
            foreach (int elementId in elementIds)
            {
                if (!mesh.Elements.TryGetValue(elementId, out FeElement element) || !(element is FeElement3D)) continue;
                int[][] faceCells = element.GetAllVtkCells();
                for (int i = 0; i < faceCells.Length; i++)
                {
                    FeFaceName faceName = FeElement.FaceNameFromVtkCellId(i);
                    int[] faceNodeIds = element.GetNodeIdsFromFaceName(faceName);
                    string key = GetSortedNodeKey(faceNodeIds);
                    if (boundaryFaces.TryGetValue(key, out BoundaryFaceData boundaryFace))
                        boundaryFace.IsBoundary = false;
                    else
                    {
                        boundaryFaces.Add(key, new BoundaryFaceData()
                        {
                            ElementId = elementId,
                            FaceName = faceName,
                            NodeIds = faceNodeIds,
                            IsBoundary = true
                        });
                    }
                }
            }
            //
            return boundaryFaces.Where(entry => entry.Value.IsBoundary)
                                .ToDictionary(entry => entry.Key, entry => entry.Value);
        }
        private bool TryCreateStanchionSurface(FeMesh mesh,
                                               HashSet<int> importedNodeIds,
                                               Dictionary<string, BoundaryFaceData> boundaryFaces,
                                               FeCylinder cylinder,
                                               HashSet<string> reservedNames,
                                               out string createdSurfaceName,
                                               out string message)
        {
            createdSurfaceName = null;
            message = null;
            double[] axisDirection = cylinder.GetNormalizedDirection();
            double[] axisCenter = cylinder.Center;
            double axialTolerance = Math.Max(cylinder.Tolerance, 1E-6);
            double halfHeight = 0.5 * cylinder.Height + 3.0;
            List<double> radiusCandidates = GetStanchionSurfaceRadiusCandidates(mesh, importedNodeIds, boundaryFaces,
                                                                                 cylinder, axisCenter, axisDirection,
                                                                                 halfHeight, axialTolerance);
            if (radiusCandidates.Count == 0)
            {
                message = "No stanchion radius could be determined for cylinder '" + cylinder.Name + "'.";
                return false;
            }
            //
            double selectedRadius = 0;
            Dictionary<FeFaceName, HashSet<int>> faceNameElementIds = new Dictionary<FeFaceName, HashSet<int>>();
            foreach (double radiusCandidate in radiusCandidates)
            {
                double radialTolerance = GetStanchionSurfaceRadiusTolerance(cylinder, radiusCandidate);
                Dictionary<FeFaceName, HashSet<int>> candidateFaceNameElementIds =
                    new Dictionary<FeFaceName, HashSet<int>>();
                foreach (BoundaryFaceData boundaryFace in boundaryFaces.Values)
                {
                    if (!IsFaceCenterWithinCylinderBand(mesh, boundaryFace, axisCenter, axisDirection, halfHeight,
                                                        axialTolerance))
                        continue;
                    if (!IsFaceAtAverageCylinderRadius(mesh, boundaryFace, axisCenter, axisDirection, radiusCandidate,
                                                       radialTolerance))
                        continue;
                    if (!IsFaceOnCylinderSide(mesh, boundaryFace, axisCenter, axisDirection)) continue;
                    //
                    if (!candidateFaceNameElementIds.TryGetValue(boundaryFace.FaceName, out HashSet<int> elementIds))
                    {
                        elementIds = new HashSet<int>();
                        candidateFaceNameElementIds.Add(boundaryFace.FaceName, elementIds);
                    }
                    elementIds.Add(boundaryFace.ElementId);
                }
                if (candidateFaceNameElementIds.Count > 0)
                {
                    faceNameElementIds = candidateFaceNameElementIds;
                    selectedRadius = radiusCandidate;
                    break;
                }
            }
            if (faceNameElementIds.Count == 0)
            {
                message = "No outer stanchion faces were found for cylinder '" + cylinder.Name + "'.";
                return false;
            }
            //
            createdSurfaceName = GetUniqueMeshEntityName(cylinder.Name, reservedNames);
            FeSurface surface = new FeSurface(createdSurfaceName);
            surface.CreatedFrom = FeSurfaceCreatedFrom.Faces;
            //
            foreach (var entry in faceNameElementIds.OrderBy(entry => entry.Key))
            {
                string elementSetName = GetUniqueMeshEntityName(createdSurfaceName + "_" + entry.Key, reservedNames);
                FeElementSet elementSet = new FeElementSet(elementSetName, entry.Value.OrderBy(id => id).ToArray());
                elementSet.Internal = true;
                _controller.AddElementSet(elementSet);
                surface.AddElementFace(entry.Key, elementSetName);
            }
            //
            _controller.AddImportedSurface(surface);
            message = string.Format("Created stanchion surface '{0}' from cylinder '{1}' at radius {2:G6}.",
                                    createdSurfaceName, cylinder.Name, selectedRadius);
            return true;
        }
        private List<double> GetStanchionSurfaceRadiusCandidates(FeMesh mesh,
                                                                 HashSet<int> importedNodeIds,
                                                                 Dictionary<string, BoundaryFaceData> boundaryFaces,
                                                                 FeCylinder cylinder,
                                                                 double[] axisCenter,
                                                                 double[] axisDirection,
                                                                 double halfHeight,
                                                                 double axialTolerance)
        {
            List<double> radiusCandidates = new List<double>();
            double maxBoundaryRadius = double.MinValue;
            foreach (BoundaryFaceData boundaryFace in boundaryFaces.Values)
            {
                if (!IsFaceCenterWithinCylinderBand(mesh, boundaryFace, axisCenter, axisDirection, halfHeight,
                                                    axialTolerance))
                    continue;
                if (!IsFaceOnCylinderSide(mesh, boundaryFace, axisCenter, axisDirection)) continue;
                //
                double averageRadius = GetFaceAverageRadius(mesh, boundaryFace, axisCenter, axisDirection);
                if (IsStanchionRadiusNearCylinder(cylinder, averageRadius) && averageRadius > maxBoundaryRadius)
                    maxBoundaryRadius = averageRadius;
            }
            //
            double maxNodeRadius = double.MinValue;
            foreach (int nodeId in importedNodeIds)
            {
                if (!mesh.Nodes.TryGetValue(nodeId, out FeNode node)) continue;
                GetPointAxialAndRadialCoordinates(node.Coor, axisCenter, axisDirection, out double axialCoordinate,
                                                  out double radialDistance);
                if (Math.Abs(axialCoordinate) <= halfHeight + axialTolerance &&
                    IsStanchionRadiusNearCylinder(cylinder, radialDistance) &&
                    radialDistance > maxNodeRadius)
                    maxNodeRadius = radialDistance;
            }
            AddUniquePositiveRadius(radiusCandidates, maxNodeRadius, cylinder);
            AddUniquePositiveRadius(radiusCandidates, maxBoundaryRadius, cylinder);
            return radiusCandidates;
        }
        private void AddUniquePositiveRadius(List<double> radiusCandidates, double radius, FeCylinder cylinder)
        {
            if (radius <= 0 || double.IsNaN(radius) || double.IsInfinity(radius)) return;
            double tolerance = GetCylinderRadiusTolerance(cylinder, radius);
            foreach (double existingRadius in radiusCandidates)
            {
                if (Math.Abs(existingRadius - radius) <= tolerance) return;
            }
            radiusCandidates.Add(radius);
        }
        private double GetCylinderRadiusTolerance(FeCylinder cylinder, double radius)
        {
            return Math.Max(Math.Max(cylinder.Tolerance, Math.Abs(radius) * 1E-4), 1E-6);
        }
        private double GetStanchionSurfaceRadiusTolerance(FeCylinder cylinder, double radius)
        {
            return Math.Max(Math.Max(cylinder.Tolerance * 4, Math.Abs(radius) * 1E-2), 0.05);
        }
        private double GetFaceAverageRadius(FeMesh mesh,
                                            BoundaryFaceData boundaryFace,
                                            double[] axisCenter,
                                            double[] axisDirection)
        {
            double radiusSum = 0;
            int count = 0;
            foreach (int nodeId in boundaryFace.NodeIds)
            {
                if (!mesh.Nodes.TryGetValue(nodeId, out FeNode node)) continue;
                GetPointAxialAndRadialCoordinates(node.Coor, axisCenter, axisDirection, out double axialCoordinate,
                                                  out double radialDistance);
                radiusSum += radialDistance;
                count++;
            }
            if (count == 0) return double.MinValue;
            return radiusSum / count;
        }
        private bool IsSRSuntourStanchionContactCylinder(string cylinderName)
        {
            string normalizedName = NormalizeEntityLookupName(cylinderName);
            return normalizedName.StartsWith(NormalizeEntityLookupName(StanchionInsertCylinderName),
                                             StringComparison.OrdinalIgnoreCase) ||
                   normalizedName.StartsWith(NormalizeEntityLookupName(TopBushingCylinderName),
                                             StringComparison.OrdinalIgnoreCase) ||
                   normalizedName.StartsWith(NormalizeEntityLookupName(BottomBushingCylinderName),
                                             StringComparison.OrdinalIgnoreCase);
        }
        private bool IsStanchionRadiusNearCylinder(FeCylinder cylinder, double radius)
        {
            if (radius <= 0 || double.IsNaN(radius) || double.IsInfinity(radius)) return false;
            double referenceRadius = Math.Max(Math.Abs(cylinder.Radius + cylinder.ExtraRadius), Math.Abs(cylinder.Radius));
            if (referenceRadius <= 0) return true;
            double allowedDistance = Math.Max(Math.Max(referenceRadius * 0.35, cylinder.Tolerance * 20), 1.0);
            return Math.Abs(radius - referenceRadius) <= allowedDistance;
        }
        private bool IsFaceCenterWithinCylinderBand(FeMesh mesh,
                                                    BoundaryFaceData boundaryFace,
                                                    double[] axisCenter,
                                                    double[] axisDirection,
                                                    double halfHeight,
                                                    double axialTolerance)
        {
            if (!TryGetBoundaryFaceCenter(mesh, boundaryFace, out double[] faceCenter)) return false;
            GetPointAxialAndRadialCoordinates(faceCenter, axisCenter, axisDirection, out double axialCoordinate,
                                              out double radialDistance);
            return Math.Abs(axialCoordinate) <= halfHeight + axialTolerance;
        }
        private bool IsFaceWithinCylinderBand(FeMesh mesh,
                                              BoundaryFaceData boundaryFace,
                                              double[] axisCenter,
                                              double[] axisDirection,
                                              double halfHeight,
                                              double axialTolerance)
        {
            foreach (int nodeId in boundaryFace.NodeIds)
            {
                if (!mesh.Nodes.TryGetValue(nodeId, out FeNode node)) return false;
                GetPointAxialAndRadialCoordinates(node.Coor, axisCenter, axisDirection, out double axialCoordinate,
                                                  out double radialDistance);
                if (Math.Abs(axialCoordinate) > halfHeight + axialTolerance) return false;
            }
            return true;
        }
        private bool IsFaceAtAverageCylinderRadius(FeMesh mesh,
                                                   BoundaryFaceData boundaryFace,
                                                   double[] axisCenter,
                                                   double[] axisDirection,
                                                   double targetRadius,
                                                   double radialTolerance)
        {
            double averageRadius = GetFaceAverageRadius(mesh, boundaryFace, axisCenter, axisDirection);
            if (averageRadius <= 0 || double.IsNaN(averageRadius) || double.IsInfinity(averageRadius)) return false;
            return Math.Abs(averageRadius - targetRadius) <= radialTolerance;
        }
        private bool IsFaceOnOuterCylinderRadius(FeMesh mesh,
                                                 BoundaryFaceData boundaryFace,
                                                 double[] axisCenter,
                                                 double[] axisDirection,
                                                 double targetRadius,
                                                 double radialTolerance)
        {
            foreach (int nodeId in boundaryFace.NodeIds)
            {
                if (!mesh.Nodes.TryGetValue(nodeId, out FeNode node)) return false;
                GetPointAxialAndRadialCoordinates(node.Coor, axisCenter, axisDirection, out double axialCoordinate,
                                                  out double radialDistance);
                if (Math.Abs(radialDistance - targetRadius) > radialTolerance) return false;
            }
            return true;
        }
        private bool TryGetBoundaryFaceCenter(FeMesh mesh, BoundaryFaceData boundaryFace, out double[] faceCenter)
        {
            faceCenter = null;
            double[] sum = new double[] { 0, 0, 0 };
            int count = 0;
            foreach (int nodeId in boundaryFace.NodeIds)
            {
                if (!mesh.Nodes.TryGetValue(nodeId, out FeNode node)) continue;
                sum[0] += node.Coor[0];
                sum[1] += node.Coor[1];
                sum[2] += node.Coor[2];
                count++;
            }
            if (count == 0) return false;
            faceCenter = new double[] { sum[0] / count, sum[1] / count, sum[2] / count };
            return true;
        }
        private bool IsFaceOnCylinderSide(FeMesh mesh,
                                          BoundaryFaceData boundaryFace,
                                          double[] axisCenter,
                                          double[] axisDirection)
        {
            mesh.GetElementFaceCenterAndNormal(boundaryFace.ElementId, boundaryFace.FaceName, out double[] faceCenter,
                                               out double[] faceNormal, out bool shellElement);
            if (faceCenter == null || faceNormal == null) return false;
            //
            double[] radialVector = GetRadialVector(faceCenter, axisCenter, axisDirection);
            double radialLength = GetVectorLength(radialVector);
            if (radialLength <= 1E-9) return false;
            //
            double[] radialDirection = ScaleVector(radialVector, 1.0 / radialLength);
            double normalAxisAlignment = Math.Abs(DotProduct(faceNormal, axisDirection));
            double normalRadialAlignment = Math.Abs(DotProduct(faceNormal, radialDirection));
            return normalAxisAlignment < 0.35 && normalRadialAlignment > 0.25;
        }
        private void GetPointAxialAndRadialCoordinates(double[] point,
                                                       double[] axisCenter,
                                                       double[] axisDirection,
                                                       out double axialCoordinate,
                                                       out double radialDistance)
        {
            double[] relative = SubtractVectors(point, axisCenter);
            axialCoordinate = DotProduct(relative, axisDirection);
            double[] radialVector = SubtractVectors(relative, ScaleVector(axisDirection, axialCoordinate));
            radialDistance = GetVectorLength(radialVector);
        }
        private double[] GetRadialVector(double[] point, double[] axisCenter, double[] axisDirection)
        {
            double[] relative = SubtractVectors(point, axisCenter);
            double axialCoordinate = DotProduct(relative, axisDirection);
            return SubtractVectors(relative, ScaleVector(axisDirection, axialCoordinate));
        }
        private double[] SubtractVectors(double[] first, double[] second)
        {
            return new double[] { first[0] - second[0], first[1] - second[1], first[2] - second[2] };
        }
        private double[] ScaleVector(double[] vector, double factor)
        {
            return new double[] { vector[0] * factor, vector[1] * factor, vector[2] * factor };
        }
        private double DotProduct(double[] first, double[] second)
        {
            return first[0] * second[0] + first[1] * second[1] + first[2] * second[2];
        }
        private double GetVectorLength(double[] vector)
        {
            return Math.Sqrt(DotProduct(vector, vector));
        }
        private string GetSortedNodeKey(int[] nodeIds)
        {
            int[] sortedNodeIds = nodeIds.ToArray();
            Array.Sort(sortedNodeIds);
            return string.Join("_", sortedNodeIds);
        }
        private FeCylinder FindModelCylinderByName(string cylinderName)
        {
            if (string.IsNullOrWhiteSpace(cylinderName)) return null;
            FeCylinder[] cylinders = _controller.GetAllModelCylinders();
            if (cylinders == null || cylinders.Length == 0) return null;
            //
            string normalizedTargetName = NormalizeEntityLookupName(cylinderName);
            return cylinders.FirstOrDefault(cylinder =>
                cylinder != null && NormalizeEntityLookupName(cylinder.Name) == normalizedTargetName);
        }
        private string NormalizeEntityLookupName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            char[] normalized = name.Where(ch => !char.IsWhiteSpace(ch) && ch != '_' && ch != '-')
                                    .Select(ch => char.ToUpperInvariant(ch))
                                    .ToArray();
            return new string(normalized);
        }
        private string FormatPythonNumberArgument(double value)
        {
            return value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
        }
        private string QuoteCommandLineArgument(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
        private async void SaveCADPartsAsStep(string[] partNames)
        {
            try
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Step files | *.stp";
                    if (_controller.OpenedFileName != null)
                        saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_controller.OpenedFileName) + ".stp";
                    //
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // The filter adds the extension to the file name
                        SetStateWorking(Globals.ExportingText);
                        //
                        await Task.Run(() => _controller.ExportCADGeometryPartsAsStepCommand(partNames, saveFileDialog.FileName));
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        private async void SaveCADPartsAsBrep(string[] partNames)
        {
            try
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Brep files | *.brep";
                    if (_controller.OpenedFileName != null)
                        saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_controller.OpenedFileName) + ".brep";
                    //
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // The filter adds the extension to the file name
                        SetStateWorking(Globals.ExportingText);
                        //
                        await Task.Run(() => _controller.ExportCADGeometryPartsAsBrepCommand(partNames, saveFileDialog.FileName));
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        private async void SavePartsAsStl(string[] partNames)
        {
            try
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Stereolithography files | *.stl";
                    if (_controller.OpenedFileName != null)
                        saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_controller.OpenedFileName) + ".stl";
                    //
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // The filter adds the extension to the file name
                        SetStateWorking(Globals.ExportingText);
                        //
                        await Task.Run(() => _controller.ExportToStlCommand(partNames, saveFileDialog.FileName));
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        private async void SavePartsAsGmshMesh()
        {
            try
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Gmsh mesh files | *.msh";
                    if (_controller.OpenedFileName != null)
                        saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_controller.OpenedFileName) + ".msh";
                    //
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // The filter adds the extension to the file name
                        SetStateWorking(Globals.ExportingText);
                        //
                        await Task.Run(() => _controller.ExportPartsAsGmshMeshCommand(saveFileDialog.FileName));
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        private async void SavePartsAsMmgMesh(string[] partNames)
        {
            try
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Mmg files | *.mesh";
                    if (_controller.OpenedFileName != null)
                        saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_controller.OpenedFileName) + ".mesh";
                    //
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // The filter adds the extension to the file name
                        SetStateWorking(Globals.ExportingText);
                        //
                        await Task.Run(() => _controller.ExportPartsAsMmgMeshCommand(partNames, saveFileDialog.FileName));
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        private async void SaveDeformedPartsAsInp(string[] partNames)
        {
            try
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Calculix files | *.inp";
                    if (_controller.OpenedFileName != null)
                        saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_controller.OpenedFileName) + ".inp";
                    //
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // The filter adds the extension to the file name
                        SetStateWorking(Globals.ExportingText);
                        //
                        await Task.Run(() => _controller.ExportDeformedPartsToCalculixCommand(partNames, saveFileDialog.FileName));
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        //
        private void tsmiCloseCurrentResult_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.AllResults.Count <= 1) tsmiCloseAllResults_Click(null, null);
                else
                {
                    _controller.RemoveCurrentResult();
                    SetResultNames();
                    if (tscbResultNames.SelectedItem != null) SetCurrentResults(tscbResultNames.SelectedItem.ToString());
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiCloseAllResults_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.ClearResults(); // calls this.ClearResults();
                //
                if (_controller.CurrentView == ViewGeometryModelResults.Results) Clear3D();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        //Recent
        public void UpdateRecentFilesThreadSafe()
        {
            InvokeIfRequired(UpdateRecentFiles);
        }
        public void UpdateRecentFiles()
        {
            try
            {
                string[] fileNames = _controller.Settings.General.GetRecentFiles();
                if (fileNames != null)
                {
                    tsmiOpenRecent.DropDownItems.Clear();
                    //
                    ToolStripMenuItem menuItem;
                    foreach (var fileName in fileNames)
                    {
                        menuItem = new ToolStripMenuItem(fileName.Replace("&", "&&"));
                        menuItem.Name = fileName;
                        menuItem.Click += tsmiRecentFile_Click;
                        tsmiOpenRecent.DropDownItems.Add(menuItem);
                    }
                    if (fileNames.Length > 0)
                    {
                        ToolStripSeparator separator = new ToolStripSeparator();
                        tsmiOpenRecent.DropDownItems.Add(separator);
                    }
                    menuItem = new ToolStripMenuItem("Clear Recent Files");
                    menuItem.Click += tsmiClearRecentFiles_Click;
                    tsmiOpenRecent.DropDownItems.Add(menuItem);
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiRecentFile_Click(object sender, EventArgs e)
        {
            try
            {
                string fileName = ((ToolStripMenuItem)sender).Name;
                if (!File.Exists(fileName))
                {
                    string text = "The file " + fileName + " does not exist. Remove it from the recent list?";
                    if (MessageBoxes.ShowWarningQuestionYesNo(text) == DialogResult.Yes)
                    {
                        _controller.RemoveFileNameFromRecentFiles(fileName);
                        UpdateRecentFilesThreadSafe();
                    }
                }
                else TryOpen(fileName);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiClearRecentFiles_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.ClearRecentFiles();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }

        #endregion  ################################################################################################################

        #region Edit menu ##########################################################################################################
        private async void tsmiUndo_Click(object sender, EventArgs e)
        {
            try
            {
                SetFormLocation(_frmRegenerate);
                if (_frmRegenerate.ShowDialog(this) == DialogResult.OK)
                {
                    SetStateWorking(Globals.UndoingText);
                    _modelTree.ScreenUpdating = false;
                    //
                    await Task.Run(() => _controller.UndoHistory(_frmRegenerate.RegenerateType));
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.UndoingText);
                _modelTree.ScreenUpdating = true;
                _modelTree.RegenerateTree(_controller.Model, _controller.Jobs, _controller.CurrentResult);
                //
                SetMenuAndToolStripVisibility();
                //
                SetZoomToFit(true);
            }
        }
        private void tsmiRedo_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.RedoHistory();
                //
                SetMenuAndToolStripVisibility();
                //
                SetZoomToFit(true);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditHistory_Click(object sender, EventArgs e)
        {
            try
            {
                CloseAllForms();
                //
                _frmEditCommands.PrepareForm();
                //
                SetFormLocation(_frmEditCommands);
                if (_frmEditCommands.ShowDialog(this) ==  DialogResult.OK)
                {
                    _controller.SetCommands(_frmEditCommands.Commands);
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void tsmiRegenerateHistory_Click(object sender, EventArgs e)
        {
            SetFormLocation(_frmRegenerate);
            if (_frmRegenerate.ShowDialog(this) == DialogResult.OK)
            {
                RegenerateHistory(false, false, _frmRegenerate.RegenerateType);
            }
        }
        private void tsmiRegenerateHistoryUsingOtherFiles_Click(object sender, EventArgs e)
        {
            SetFormLocation(_frmRegenerate);
            if (_frmRegenerate.ShowDialog(this) == DialogResult.OK)
            {
                RegenerateHistory(true, false, _frmRegenerate.RegenerateType);
            }
        }
        private void tsmiRegenerateHistoryWithRemeshing_Click(object sender, EventArgs e)
        {
            SetFormLocation(_frmRegenerate);
            if (_frmRegenerate.ShowDialog(this) == DialogResult.OK)
            {
                RegenerateHistory(false, true, _frmRegenerate.RegenerateType);
            }
        }
        //
        public async void RegenerateHistory(bool showFileDialog, bool showMeshDialog, RegenerateTypeEnum regenerateType)
        {
            try
            {
                CloseAllForms();
                Clear3D();
                Application.DoEvents();
                SetStateWorking(Globals.RegeneratingText);
                _modelTree.ScreenUpdating = false;
                await Task.Run(() => _controller.RegenerateHistoryCommands(showFileDialog, showMeshDialog, regenerateType));
                //
                UpdateResultMenus();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.RegeneratingText);
                _modelTree.ScreenUpdating = true;
                RegenerateTree();
                //
                SetMenuAndToolStripVisibility();
                // During regeneration the tree is empty
                if (_controller.CurrentResult != null) SelectFirstComponentOfFirstFieldOutput(); 
                //
                SetZoomToFit(true);
            }
        }
        public void EnableDisableUndoRedo(string undo, string redo)
        {
            InvokeIfRequired(EnableDisable, undo, redo);
        }
        private void EnableDisable(string undo, string redo)
        {
            if (undo == null)
            {
                tsmiUndo.Text = "Undo";
                tsmiUndo.Enabled = false;
            }
            else
            {
                tsmiUndo.Text = "Undo - " + undo;
                tsmiUndo.Enabled = true;
            }

            if (redo == null)
            {
                tsmiRedo.Text = "Redo";
                tsmiRedo.Enabled = false;
            }
            else
            {
                tsmiRedo.Text = "Redo - " + redo;
                tsmiRedo.Enabled = true;
            }
        }

        #endregion  ################################################################################################################

        #region View menu ##########################################################################################################
        // Standard views
        private void tsmiFrontView_Click(object sender, EventArgs e)
        {
            SetFrontBackView(true, true);
        }
        private void tsmiBackView_Click(object sender, EventArgs e)
        {
            SetFrontBackView(true, false);
        }
        private void tsmiTopView_Click(object sender, EventArgs e)
        {
            SetTopBottomView(true, true);
        }
        private void tsmiBottomView_Click(object sender, EventArgs e)
        {
            SetTopBottomView(true, false);
        }
        private void tsmiLeftView_Click(object sender, EventArgs e)
        {
            SetLeftRightView(true, true);
        }
        private void tsmiRightView_Click(object sender, EventArgs e)
        {
            SetLeftRightView(true, false);
        }
        private void SetTopBottomView(bool animate, bool top)
        {
            if (_vtkJs == null) _vtk.SetTopBottomView(animate, top);
            else
            {
                _vtkJsCameraCommandPending = true;
                _vtkJs.SetCameraView(top ? "top" : "bottom");
                _vtkJs.WriteCompatibilityTrace("camera", "set-top-bottom");
            }
        }
        private void SetLeftRightView(bool animate, bool left)
        {
            if (_vtkJs == null) _vtk.SetLeftRightView(animate, left);
            else
            {
                _vtkJsCameraCommandPending = true;
                _vtkJs.SetCameraView(left ? "left" : "right");
                _vtkJs.WriteCompatibilityTrace("camera", "set-left-right");
            }
        }
        // Normal and vertical views
        private void tsmiNormalView_Click(object sender, EventArgs e)
        {
            SetNormalView(true);
        }
        private void tsmiVerticalView_Click(object sender, EventArgs e)
        {
            SetVerticalView(true);
        }
        private void SetNormalView(bool animate)
        {
            if (_vtkJs == null)
            {
                _vtk.SetNormalView(animate);
                return;
            }
            if (!HasRetainedVtkJsCamera())
            {
                _vtkJs.WriteCompatibilityTrace("camera", "set-normal-fit-deferred");
                return;
            }
            double[] normal = GetViewPlaneNormal();
            int axis = Math.Abs(normal[0]) >= Math.Abs(normal[1]) ? 0 : 1;
            if (Math.Abs(normal[2]) > Math.Abs(normal[axis])) axis = 2;
            double[] position = (double[])_vtkJsCameraFocalPoint.Clone();
            position[axis] += Math.Abs(normal[axis]) == 1 ? -Math.Sign(normal[axis]) : Math.Sign(normal[axis]);
            _vtkJsCameraCommandPending = true;
            _vtkJs.SetCustomCamera(position, _vtkJsCameraFocalPoint, _vtkJsCameraViewUp,
                                   _vtkJsCameraParallelScale, "fit");
            _vtkJs.WriteCompatibilityTrace("camera", "set-normal-fit");
        }
        private void SetVerticalView(bool animate)
        {
            if (_vtkJs == null)
            {
                _vtk.SetVerticalView(animate, true);
                return;
            }
            if (!HasRetainedVtkJsCamera())
            {
                _vtkJs.WriteCompatibilityTrace("camera", "set-vertical-fit-deferred");
                return;
            }
            double[] closestUp = GetClosestUpView();
            _vtkJsCameraCommandPending = true;
            _vtkJs.SetCustomCamera(_vtkJsCameraPosition, _vtkJsCameraFocalPoint, closestUp,
                                   _vtkJsCameraParallelScale, "fit");
            _vtkJs.WriteCompatibilityTrace("camera", "set-vertical-fit");
        }
        // Isometric view and zoom to fit
        private void tsmiIsometricView_Click(object sender, EventArgs e)
        {
            if (_vtkJs == null) _vtk.SetIsometricView(true, true);
            else
            {
                _vtkJsCameraCommandPending = true;
                _vtkJs.SetCameraView("isometric");
                _vtkJs.WriteCompatibilityTrace("camera", "set-isometric");
            }
        }
        private void tsmiZoomToFit_Click(object sender, EventArgs e)
        {
            SetZoomToFit(true);
        }
        private void tsmiBoxZoom_Click(object sender, EventArgs e)
        {
            StartBoxZoom();
        }
        private void tsmiCopyCameraView_Click(object sender, EventArgs e)
        {
            try
            {
                GetViewParameters(out double[] position, out double[] focalPoint, out double[] viewUp,
                                  out double parallelScale);
                Clipboard.SetText(GetCameraViewClipboardText(position, focalPoint, viewUp, parallelScale));
                WriteDataToOutput("Camera view copied to clipboard.");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiPasteCameraView_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Clipboard.ContainsText()) throw new CaeException("The clipboard does not contain text.");
                ParseCameraViewClipboardText(Clipboard.GetText(), out double[] position, out double[] focalPoint,
                                             out double[] viewUp, out double parallelScale);
                SetViewParameters(true, position, focalPoint, viewUp, parallelScale);
                WriteDataToOutput("Camera view pasted from clipboard.");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private string GetCameraViewClipboardText(double[] position, double[] focalPoint, double[] viewUp,
                                                  double parallelScale)
        {
            return string.Format(CultureInfo.InvariantCulture,
                                 "PrePoMaxCameraView:v1;P={0:G17},{1:G17},{2:G17};F={3:G17},{4:G17},{5:G17};U={6:G17},{7:G17},{8:G17};S={9:G17}",
                                 position[0], position[1], position[2],
                                 focalPoint[0], focalPoint[1], focalPoint[2],
                                 viewUp[0], viewUp[1], viewUp[2], parallelScale);
        }
        private void ParseCameraViewClipboardText(string text, out double[] position, out double[] focalPoint,
                                                  out double[] viewUp, out double parallelScale)
        {
            position = null;
            focalPoint = null;
            viewUp = null;
            parallelScale = 1;
            //
            if (string.IsNullOrWhiteSpace(text)) throw new CaeException("The camera view text is empty.");
            string[] parts = text.Trim().Split(';');
            if (parts.Length != 5 || parts[0] != "PrePoMaxCameraView:v1")
                throw new CaeException("The clipboard text is not a valid PrePoMax camera view.");
            //
            foreach (string part in parts.Skip(1))
            {
                string[] keyValue = part.Split(new char[] { '=' }, 2);
                if (keyValue.Length != 2) throw new CaeException("The camera view text is invalid.");
                //
                if (keyValue[0] == "P") position = ParseCameraViewVector(keyValue[1]);
                else if (keyValue[0] == "F") focalPoint = ParseCameraViewVector(keyValue[1]);
                else if (keyValue[0] == "U") viewUp = ParseCameraViewVector(keyValue[1]);
                else if (keyValue[0] == "S") parallelScale = ParseCameraViewDouble(keyValue[1]);
                else throw new CaeException("The camera view text contains an unknown parameter.");
            }
            //
            if (position == null || focalPoint == null || viewUp == null || parallelScale <= 0)
                throw new CaeException("The camera view text is missing required parameters.");
            if (GetCameraViewVectorLengthSquared(viewUp) <= 0)
                throw new CaeException("The camera view up vector is invalid.");
            if (GetCameraViewDistanceSquared(position, focalPoint) <= 0)
                throw new CaeException("The camera position and focal point must be different.");
        }
        private double[] ParseCameraViewVector(string text)
        {
            string[] values = text.Split(',');
            if (values.Length != 3) throw new CaeException("The camera view vector is invalid.");
            //
            return new double[]
            {
                ParseCameraViewDouble(values[0]),
                ParseCameraViewDouble(values[1]),
                ParseCameraViewDouble(values[2])
            };
        }
        private double GetCameraViewVectorLengthSquared(double[] vector)
        {
            return vector[0] * vector[0] + vector[1] * vector[1] + vector[2] * vector[2];
        }
        private double GetCameraViewDistanceSquared(double[] a, double[] b)
        {
            double dx = a[0] - b[0];
            double dy = a[1] - b[1];
            double dz = a[2] - b[2];
            return dx * dx + dy * dy + dz * dz;
        }
        private double ParseCameraViewDouble(string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                double.IsNaN(value) || double.IsInfinity(value))
                throw new CaeException("The camera view contains an invalid number.");
            return value;
        }
        // User views
        private void tsmiEditUserViews_Click(object sender, EventArgs e)
        {
            try
            {
                _frmUserView.UserViewParameters = _controller.GetUserViewParameters();
                ShowForm(_frmUserView, tsmiUserViews.Text, null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        public void UpdateUserViewsThreadSafe(string[] viewNames)
        {
            InvokeIfRequired(UpdateUserViews, viewNames);
        }
        public void UpdateUserViews(string[] viewNames)
        {
            try
            {
                if (viewNames != null)
                {
                    tsmiUserViews.DropDownItems.Clear();
                    tsmiUserViews.DropDownItems.Add(tsmiEditUserViews);
                    // Separator
                    if (viewNames.Length > 0) tsmiUserViews.DropDownItems.Add(new ToolStripSeparator());
                    //
                    ToolStripMenuItem menuItem;
                    foreach (var viewName in viewNames)
                    {
                        menuItem = new ToolStripMenuItem(viewName);
                        menuItem.Name = viewName;
                        menuItem.Click += tsmiUserView_Click;
                        tsmiUserViews.DropDownItems.Add(menuItem);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiUserView_Click(object sender, EventArgs e)
        {
            try
            {
                string viewName = ((ToolStripMenuItem)sender).Name;
                _controller.SetUserView(viewName);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // Model appearance
        private void tsmiShowWireframeEdges_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentEdgesVisibility = vtkControl.vtkEdgesVisibility.Wireframe;
            }
            catch { }
        }
        private void tsmiShowElementEdges_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentEdgesVisibility = vtkControl.vtkEdgesVisibility.ElementEdges;
            }
            catch { }
        }
        private void tsmiShowModelEdges_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentEdgesVisibility = vtkControl.vtkEdgesVisibility.ModelEdges;
            }
            catch { }
        }
        private void tsmiShowNoEdges_Click(object sender, EventArgs e)
        {
            try
            {
                _controller.CurrentEdgesVisibility = vtkControl.vtkEdgesVisibility.NoEdges;
            }
            catch { }
        }
        // Section view
        private void tsmiSectionView_Click(object sender, EventArgs e)
        {
            try
            {
                SinglePointDataEditor.ParentForm = _frmSectionView;
                SinglePointDataEditor.Controller = _controller;
                //
                ShowForm(_frmSectionView, tsmiSectionView.Text, null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void TurnSectionViewOnOff()
        {
            try
            {
                if (!_frmSectionView.Visible) _controller.TurnSectionViewOnOff();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExplodePartsText);
            }
        }
        // Exploded view
        private void tsmiExplodedView_Click(object sender, EventArgs e)
        {
            try
            {
                ExplodedViewParameters parameters = _controller.GetCurrentExplodedViewParameters();
                _frmExplodedView.SetExplodedViewParameters(parameters);
                //
                ShowForm(_frmExplodedView, _frmExplodedView.Text, null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void TurnExplodedViewOnOff(bool animate)
        {
            try
            {
                SetStateWorking(Globals.ExplodePartsText);
                _controller.TurnExplodedViewOnOff(animate);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExplodePartsText);
            }
        }
        private void tsmiFollowerView_Click(object sender, EventArgs e)
        {
            try
            {
                SinglePointDataEditor.ParentForm = _frmFollowerView;
                SinglePointDataEditor.Controller = _controller;
                //
                FollowerViewParameters parameters = _controller.GetCurrentFollowerViewParameters();
                _frmFollowerView.SetFollowerViewParameters(parameters);
                //
                ShowForm(_frmFollowerView, _frmFollowerView.Text, null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // Hide/Show
        private void tsmiHideAllParts_Click(object sender, EventArgs e)
        {
            try
            {
                string[] partNames;
                if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
                {
                    partNames = _controller.GetGeometryPartNames();
                    _controller.HideGeometryPartsCommand(partNames);
                }
                else if (_controller.CurrentView == ViewGeometryModelResults.Model)
                {
                    partNames = _controller.GetModelPartNames();
                    _controller.HideModelPartsCommand(partNames);
                }
                else if (_controller.CurrentView == ViewGeometryModelResults.Results)
                {
                    partNames = _controller.GetResultPartNames();
                    _controller.HideResultPartsCommand(partNames);
                }
            }
            catch { }
        }
        private void tsmiShowAllParts_Click(object sender, EventArgs e)
        {
            try
            {
                string[] partNames;
                if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
                {
                    partNames = _controller.GetGeometryPartNames();
                    _controller.ShowGeometryPartsCommand(partNames);
                }
                else if (_controller.CurrentView == ViewGeometryModelResults.Model)
                {
                    partNames = _controller.GetModelPartNames();
                    _controller.ShowModelPartsCommand(partNames);
                }
                else if (_controller.CurrentView == ViewGeometryModelResults.Results)
                {
                    partNames = _controller.GetResultPartNames();
                    _controller.ShowResultPartsCommand(partNames);
                }
            }
            catch { }
        }
        private void tsmiInvertVisibleParts_Click(object sender, EventArgs e)
        {
            try
            {
                BasePart[] parts;
                List<string> partNamesToHide = new List<string>();
                List<string> partNamesToShow = new List<string>();

                if (_controller.CurrentView == ViewGeometryModelResults.Geometry)
                {
                    parts = _controller.GetGeometryParts();
                    foreach (var part in parts)
                    {
                        if (part.Visible) partNamesToHide.Add(part.Name);
                        else partNamesToShow.Add(part.Name);
                    }
                    if (partNamesToHide.Count > 0) _controller.HideGeometryPartsCommand(partNamesToHide.ToArray());
                    if (partNamesToShow.Count > 0) _controller.ShowGeometryPartsCommand(partNamesToShow.ToArray());
                }
                else if (_controller.CurrentView == ViewGeometryModelResults.Model)
                {
                    parts = _controller.GetModelParts();
                    foreach (var part in parts)
                    {
                        if (part.Visible) partNamesToHide.Add(part.Name);
                        else partNamesToShow.Add(part.Name);
                    }
                    if (partNamesToHide.Count > 0) _controller.HideModelPartsCommand(partNamesToHide.ToArray());
                    if (partNamesToShow.Count > 0) _controller.ShowModelPartsCommand(partNamesToShow.ToArray());
                }
                else if (_controller.CurrentView == ViewGeometryModelResults.Results)
                {
                    parts = _controller.GetResultParts();
                    if (parts !=null)
                    {
                        foreach (var part in parts)
                        {
                            if (part.Visible) partNamesToHide.Add(part.Name);
                            else partNamesToShow.Add(part.Name);
                        }
                    }
                    if (partNamesToHide.Count > 0) _controller.HideResultPartsCommand(partNamesToHide.ToArray());
                    if (partNamesToShow.Count > 0) _controller.ShowResultPartsCommand(partNamesToShow.ToArray());
                }
            }
            catch { }
        }
        // Annotate
        private void DropDown_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                e.Cancel = true;
        }
        private void tsmiAnnotateFaceOrientations_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        private void tsmiAnnotateParts_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        private void tsmiAnnotateMaterials_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        private void tsmiAnnotateSections_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        private void tsmiAnnotateSectionThicknesses_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        private void tsmiAnnotateAllSymbols_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem tsmi)
            {
                // Turn off
                if (tsmi.Checked)
                {
                    ClearAnnotationStatus();  // first check and then clear
                    _controller.AnnotateWithColor = AnnotateWithColorEnum.None;
                }
                // Turn on
                else
                {
                    ClearAnnotationStatus();
                    //
                    tsmi.Checked = true;
                    tsmiAnnotateReferencePoints.Checked = true;
                    tsmiAnnotateConstraints.Checked = true;
                    tsmiAnnotateContactPairs.Checked = true;
                    tsmiAnnotateInitialConditions.Checked = true;
                    tsmiAnnotateBCs.Checked = true;
                    tsmiAnnotateLoads.Checked = true;
                    tsmiAnnotateDefinedFields.Checked = true;
                    //
                    _controller.AnnotateWithColor = AnnotateWithColorEnum.ReferencePoints |
                                                    AnnotateWithColorEnum.Constraints |
                                                    AnnotateWithColorEnum.ContactPairs |
                                                    AnnotateWithColorEnum.InitialConditions |
                                                    AnnotateWithColorEnum.BoundaryConditions |
                                                    AnnotateWithColorEnum.Loads |
                                                    AnnotateWithColorEnum.DefinedFields;
                }
            }
        }
        private void tsmiAnnotateReferencePoints_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        private void tsmiAnnotateConstraints_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        private void tsmiAnnotateContactPairs_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        private void tsmiAnnotateInitialConditions_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        private void tsmiAnnotateBCs_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        private void tsmiAnnotateLoads_Click(object sender, EventArgs e)
        {
            _controller.AnnotateWithColor = ChangeAnnotationStatus(sender);
        }
        //
        private AnnotateWithColorEnum ChangeAnnotationStatus(object sender)
        {
            if (sender is ToolStripMenuItem tsmi)
            {
                // Turn off
                if (tsmi.Checked) tsmi.Checked = false;
                // Turn on
                else
                {
                    // Only one possibility - Face orientations or parts
                    if (tsmi == tsmiAnnotateFaceOrientations || tsmi == tsmiAnnotateParts ||
                        tsmi == tsmiAnnotateMaterials || tsmi == tsmiAnnotateSections ||  
                        tsmi == tsmiAnnotateSectionThicknesses)
                    {
                        ClearAnnotationStatus();
                        tsmi.Checked = true;
                    }
                    // Symbols
                    else
                    {
                        tsmiAnnotateFaceOrientations.Checked = false;
                        tsmiAnnotateParts.Checked = false;
                        tsmiAnnotateMaterials.Checked = false;
                        tsmiAnnotateSections.Checked = false;
                        tsmiAnnotateSectionThicknesses.Checked = false;
                        //
                        tsmi.Checked = true;
                    }
                }
                //
                if (tsmiAnnotateFaceOrientations.Checked) return AnnotateWithColorEnum.FaceOrientation;
                else if (tsmiAnnotateParts.Checked) return AnnotateWithColorEnum.Parts;
                else if (tsmiAnnotateMaterials.Checked) return AnnotateWithColorEnum.Materials;
                else if (tsmiAnnotateSections.Checked) return AnnotateWithColorEnum.Sections;
                else if (tsmiAnnotateSectionThicknesses.Checked) return AnnotateWithColorEnum.SectionThicknesses;
                else
                {
                    AnnotateWithColorEnum status = AnnotateWithColorEnum.None;
                    if (tsmiAnnotateReferencePoints.Checked) status |= AnnotateWithColorEnum.ReferencePoints;
                    if (tsmiAnnotateConstraints.Checked) status |= AnnotateWithColorEnum.Constraints;
                    if (tsmiAnnotateContactPairs.Checked) status |= AnnotateWithColorEnum.ContactPairs;
                    if (tsmiAnnotateInitialConditions.Checked) status |= AnnotateWithColorEnum.InitialConditions;
                    if (tsmiAnnotateBCs.Checked) status |= AnnotateWithColorEnum.BoundaryConditions;
                    if (tsmiAnnotateLoads.Checked) status |= AnnotateWithColorEnum.Loads;
                    if (tsmiAnnotateDefinedFields.Checked) status |= AnnotateWithColorEnum.DefinedFields;
                    //
                    tsmiAnnotateAllSymbols.Checked = status.HasFlag(AnnotateWithColorEnum.ReferencePoints |
                                                                    AnnotateWithColorEnum.Constraints |
                                                                    AnnotateWithColorEnum.ContactPairs |
                                                                    AnnotateWithColorEnum.InitialConditions |
                                                                    AnnotateWithColorEnum.BoundaryConditions |
                                                                    AnnotateWithColorEnum.Loads |
                                                                    AnnotateWithColorEnum.DefinedFields);
                    //
                    return status;
                }
            }
            return AnnotateWithColorEnum.None;
        }
        //
        private void tsmiResultsUndeformed_Click(object sender, EventArgs e)
        {
            SetUndeformedModelType(ViewResultsTypeEnum.Undeformed, UndeformedModelTypeEnum.None);
        }
        private void tsmiResultsDeformed_Click(object sender, EventArgs e)
        {
            SetUndeformedModelType(ViewResultsTypeEnum.Deformed, UndeformedModelTypeEnum.None);
        }
        private void tsmiResultsColorContours_Click(object sender, EventArgs e)
        {
            SetUndeformedModelType(ViewResultsTypeEnum.ColorContours, UndeformedModelTypeEnum.None);
        }
        private void tsmiResultsDeformedColorWireframe_Click(object sender, EventArgs e)
        {
            SetUndeformedModelType(ViewResultsTypeEnum.ColorContours, UndeformedModelTypeEnum.WireframeBody);
        }

        private void tsmiResultsDeformedColorSolid_Click(object sender, EventArgs e)
        {
            SetUndeformedModelType(ViewResultsTypeEnum.ColorContours, UndeformedModelTypeEnum.SolidBody);
        }
        private void SetUndeformedModelType(ViewResultsTypeEnum viewResultsType, UndeformedModelTypeEnum undeformedModelType)
        {
            _controller.SetUndeformedModelType(undeformedModelType);
            //
            if (GetCurrentView() == ViewGeometryModelResults.Results)
            {
                if (_frmAnimation.Visible) _frmAnimation.Hide();
                _controller.ViewResultsType = viewResultsType;
            }
            //
            UpdateResultsTypeIconStates();
        }
        private void UpdateResultsTypeIconStates()
        {
            tsbResultsUndeformed.Checked = false;
            tsbResultsDeformed.Checked = false;
            tsbResultsColorContours.Checked = false;
            tsbResultsUndeformedWireframe.Checked = false;
            tsbResultsUndeformedSolid.Checked = false;
            //
            if (GetCurrentView() == ViewGeometryModelResults.Results)
            {
                if (_controller.ViewResultsType == ViewResultsTypeEnum.Undeformed) tsbResultsUndeformed.Checked = true;
                else if (_controller.ViewResultsType == ViewResultsTypeEnum.Deformed) tsbResultsDeformed.Checked = true;
                else if (_controller.ViewResultsType == ViewResultsTypeEnum.ColorContours)
                {
                    if (_controller.GetUndeformedModelType() == UndeformedModelTypeEnum.None)
                        tsbResultsColorContours.Checked = true;
                    else if (_controller.GetUndeformedModelType() == UndeformedModelTypeEnum.WireframeBody)
                        tsbResultsUndeformedWireframe.Checked = true;
                    else if (_controller.GetUndeformedModelType() == UndeformedModelTypeEnum.SolidBody)
                        tsbResultsUndeformedSolid.Checked = true;
                }
            }
        }
        #endregion

        #region Geometry ###########################################################################################################
        internal void tsmiEditGeometryPart_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Parts", _controller.GetGeometryParts(), EditGeometryPart);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditColorForGeometryParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryParts(), SetColorForGeometryParts);
                Clear3DSelection();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // Transform
        private void tsmiScaleGeometryParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryPartsWithoutSubParts(), ScaleGeometryParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // End Transform
        private void tsmiCopyGeometryPartsToResults_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryParts(), CopyGeometryPartsToResults);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideGeometryParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryParts(), HideGeometryParts);
                Clear3DSelection();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowGeometryParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryParts(), ShowGeometryParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowOnlyGeometryParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryParts(), ShowOnlyGeometryParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSetColorForGeometryParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryParts(), SetColorForGeometryParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiResetColorForGeometryParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryParts(), ResetColorForGeometryParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSetTransparencyForGeometryParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryParts(), SetTransparencyForGeometryParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteGeometryParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryPartsWithoutSubParts(), DeleteGeometryParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void tsmiCreateAndImportCompoundPart_Click(object sender, EventArgs e)
        {
            try
            {
                Clear3DSelection();
                SelectMultipleEntities("Parts", _controller.GetCADGeometryParts(), CreateAndImportCompoundPart, 2);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiRegenerateCompoundPart_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetCompoundParts(), RegenerateCompoundParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSwapGeometryPartGeometries_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryParts(), SwapPartGeometries, 2, 2);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // Analyze geometry
        private void tsmiGeometryAnalyze_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryParts(), AnalyzeGeometry);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }

        }
        //                                                                                                                          
        private void EditGeometryPart(string partName)
        {
            _frmPartProperties.View = ViewGeometryModelResults.Geometry;
            ShowForm(_frmPartProperties, "Edit Part", partName);
        }
        // Transform
        private void ScaleGeometryParts(string[] partNames)
        {
            SinglePointDataEditor.ParentForm = _frmScale;
            SinglePointDataEditor.Controller = _controller;
            // Set all part names for scaling
            _frmScale.PartNames = partNames;
            //
            ShowForm(_frmScale, "Scale parts: " + partNames.ToShortString(), null);
        }
        public async Task ScaleGeometryPartsAsync(string[] partNames, double[] scaleCenter, double[] scaleFactors, bool copy)
        {
            bool stateSet = false;
            try
            {
                if (SetStateWorking(Globals.TransformingText))
                {
                    stateSet = true;
                    //
                    if (partNames != null && partNames.Length > 0)
                    {
                        await Task.Run(() => _controller.ScaleGeometryPartsCommand(partNames, scaleCenter, scaleFactors, copy));
                    }
                }
                else MessageBoxes.ShowWarning("Another task is already running.");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                if (stateSet) SetStateReady(Globals.TransformingText);
            }
        }
        // End Transform
        private void CopyGeometryPartsToResults(string[] partNames)
        {
            CloseAllForms();
            _controller.CopyGeometryPartsToResults(partNames);
        }
        private void HideGeometryParts(string[] partNames)
        {
            _controller.HideGeometryPartsCommand(partNames);            
        }
        private void ShowGeometryParts(string[] partNames)
        {
            _controller.ShowGeometryPartsCommand(partNames);
        }
        private void ShowOnlyGeometryParts(string[] partNames)
        {
            // If sub part is selected add the whole compound part
            HashSet<string> partsToShow = new HashSet<string>(partNames);
            foreach (var entry in _controller.Model.Geometry.Parts)
            {
                if (entry.Value is CompoundGeometryPart cgp)
                {
                    if (partNames.Contains(cgp.Name))
                    {
                        partsToShow.Add(cgp.Name);
                        partsToShow.UnionWith(cgp.SubPartNames);
                    }
                }
            }
            //
            HashSet<string> allNames = new HashSet<string>(_controller.Model.Geometry.Parts.Keys);
            allNames.ExceptWith(partsToShow);
            _controller.HideGeometryPartsCommand(allNames.ToArray());
            _controller.ShowGeometryPartsCommand(partsToShow.ToArray());
        }
        private void SetColorForGeometryParts(string[] partNames)
        {
            if (_controller.Model.Geometry == null) return;
            //
            using (FrmGetColor frmGetColor = new FrmGetColor())
            {
                Color color = _controller.Model.Geometry.Parts[partNames[0]].GetProperties().Color;
                SetFormLocation(frmGetColor);
                frmGetColor.PrepareForm("Set Part Color: " + partNames.ToShortString(), color);
                if (frmGetColor.ShowDialog(this) == DialogResult.OK)
                {
                    _controller.SetColorForGeometryPartsCommand(partNames, frmGetColor.Color);
                }
                SaveFormLocation(frmGetColor);
            }
        }
        private void ResetColorForGeometryParts(string[] partNames)
        {
            _controller.ResetColorForGeometryPartsCommand(partNames);
        }
        private void SetTransparencyForGeometryParts(string[] partNames)
        {
            if (_controller.Model.Geometry == null) return;
            //
            using (FrmGetValue frmGetValue = new FrmGetValue(_controller))
            {
                frmGetValue.NumOfDigits = 0;
                frmGetValue.MinValue = 25;
                frmGetValue.MaxValue = 255;
                SetFormLocation(frmGetValue);
                OrderedDictionary<string, double> presetValues =
                    new OrderedDictionary<string, double>("Preset Transparency values", StringComparer.OrdinalIgnoreCase);
                presetValues.Add("Semi-transparent", 128);
                presetValues.Add("Opaque", 255);
                string desc = "Enter the transparency between 0 and 255.\n" + "(0 - transparent; 255 - opaque)";
                frmGetValue.PrepareForm("Set Transparency: " + partNames.ToShortString(), "Transparency", desc, 128, presetValues);
                if (frmGetValue.ShowDialog(this) == DialogResult.OK)
                {
                    _controller.SetTransparencyForGeometryPartsCommand(partNames, (byte)frmGetValue.Value);
                }
                SaveFormLocation(frmGetValue);
            }
        }
        private void DeleteGeometryParts(string[] partNames)
        {
            GeometryPart[] parts = _controller.GetGeometryPartsWithoutSubParts();
            HashSet<string> deleteAblePartNames = new HashSet<string>();
            foreach (GeometryPart part in parts) deleteAblePartNames.Add(part.Name);
            deleteAblePartNames.IntersectWith(partNames);
            if (deleteAblePartNames.Count > 0)
            {
                partNames = deleteAblePartNames.ToArray();
                if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected parts?") == DialogResult.OK)
                {
                    _controller.RemoveGeometryPartsCommand(partNames.ToArray());
                }
            }
            else MessageBoxes.ShowError("Selected parts belong to a compound part and cannot be deleted:" + Environment.NewLine +
                                        partNames.ToRows());
        }
        //
        private async void CreateAndImportCompoundPart(string[] partNames)
        {
            try
            {
                SetStateWorking(Globals.CreatingCompoundText, true);
                //
                GeometryPart part;
                HashSet<PartType> stlPartTypes = new HashSet<PartType>();
                HashSet<PartType> cadPartTypes = new HashSet<PartType>();
                //
                string[] allPartNames = _controller.GetMeshablePartNames(partNames);
                foreach (var partName in allPartNames)
                {
                    part = (GeometryPart)_controller.Model.Geometry.Parts[partName];
                    if (part.IsCADPart) cadPartTypes.Add(part.PartType);
                    else stlPartTypes.Add(part.PartType);
                }
                if (stlPartTypes.Count + cadPartTypes.Count != 1)
                    throw new CaeException("Compound part can be made from only CAD or only stl based geometry parts " + 
                                           "of the same type.");
                await Task.Run(() => _controller.CreateAndImportCompoundPartCommand(partNames));
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.CreatingCompoundText);
            }
        }
        private async void RegenerateCompoundParts(string[] compoundPartNames)
        {
            try
            {
                SetStateWorking(Globals.RegeneratingCompoundText, true);
                //
                string missingPartName = null;
                string errorCompoundPartName = null;
                CompoundGeometryPart part;
                foreach (var compoundPartName in compoundPartNames)
                {
                    part = (CompoundGeometryPart)_controller.Model.Geometry.Parts[compoundPartName];
                    if (part.CreatedFromPartNames != null && part.CreatedFromPartNames.Length > 1)
                    {
                        foreach (var createdFromPartName in part.CreatedFromPartNames)
                        {
                            if (!_controller.Model.Geometry.Parts.ContainsKey(createdFromPartName))
                            {
                                missingPartName = createdFromPartName;
                                errorCompoundPartName = compoundPartName;
                                break;
                            }
                        }
                        //
                        if (missingPartName != null) break;
                    }
                }
                if (missingPartName != null)
                    throw new CaeException("The part '" + missingPartName + "' that was used to create a compound part '" +
                                           errorCompoundPartName + "' is missing.");
                //
                await Task.Run(() => _controller.RegenerateCompoundPartsCommand(compoundPartNames));
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.RegeneratingCompoundText);
            }
        }
        private void SwapPartGeometries(string[] partNames)
        {
            GeometryPart[] parts = _controller.GetGeometryPartsWithoutSubParts();
            GeometryPart part1 = _controller.GetGeometryPart(partNames[0]);
            GeometryPart part2 = _controller.GetGeometryPart(partNames[1]);
            if (parts.Contains(part1) && parts.Contains(part2))
            {
                if (part1 is CompoundGeometryPart || part2 is CompoundGeometryPart)
                    MessageBoxes.ShowError("Compound parts cannot be swapped.");
                else
                    _controller.SwapPartGeometriesCommand(partNames[0], partNames[1]);
            }
            else MessageBoxes.ShowError("Compound subparts cannot be swapped.");
        }
        // Analyze geometry
        private void AnalyzeGeometry(string[] partNames)
        {
            if (!_frmAnalyzeGeometry.Visible)
            {
                CloseAllForms();
                SetFormLocation((Form)_frmAnalyzeGeometry);
                _frmAnalyzeGeometry.PartNamesToAnalyze = partNames;
                _frmAnalyzeGeometry.Show();
            }
        }

        #endregion  ################################################################################################################

        #region Geometry CAD part menu   ###########################################################################################
        private void tsmiConvertShellToSolidCAD_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetCADGeometryShellParts(), ConvertShellToSolid);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void TsmiConvertSolidToShellCAD_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetCADGeometrySolidParts(), ConvertSolidToShell);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private async void tsmiFlipFaceNormalCAD_Click(object sender, EventArgs e)
        {
            try
            {
                // Must be outside the await part otherwise causes screen flickering
                AnnotateWithColorEnum _prevShowWithColor = _controller.AnnotateWithColor;
                _controller.AnnotateWithColor = AnnotateWithColorEnum.FaceOrientation;
                //
                await Task.Run(() =>
                {
                    _frmSelectGeometry.HideFormOnOK = false;
                    _frmSelectGeometry.SelectionFilter = SelectGeometryEnum.Surface;
                    _frmSelectGeometry.OnOKCallback = FlipFaces;
                    //
                    InvokeIfRequired(() => { ShowForm(_frmSelectGeometry, "Select faces to flip", null); });
                    while (_frmSelectGeometry.Visible) Thread.Sleep(100);
                });
                //
                _controller.AnnotateWithColor = _prevShowWithColor;
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.FlippingNormalsText);
            }
        }
        private async void tsmiSplitAFaceUsingTwoPoints_Click(object sender, EventArgs e)
        {
            try
            {
                await Task.Run(() =>
                {
                    GeometrySelection surfaceSelection;
                    GeometrySelection verticesSelection;
                    while (true)
                    {
                        // Get a surface to split
                        _frmSelectGeometry.MaxNumberOfGeometryIds = 1;
                        _frmSelectGeometry.SelectionFilter = SelectGeometryEnum.Surface;
                        //
                        InvokeIfRequired(() => { ShowForm(_frmSelectGeometry, "Select a face to split", null); });
                        while (_frmSelectGeometry.Visible) Thread.Sleep(100);
                        //
                        if (_frmSelectGeometry.DialogResult == DialogResult.OK)
                        {
                            surfaceSelection = _frmSelectGeometry.GeometrySelection.DeepClone();
                            // Get two vertices to split the surface
                            _frmSelectGeometry.MaxNumberOfGeometryIds = 2;
                            _frmSelectGeometry.SelectionFilter = SelectGeometryEnum.Vertex;
                            //
                            InvokeIfRequired(() => { ShowForm(_frmSelectGeometry, "Select splitting vertices", null); });
                            while (_frmSelectGeometry.Visible) Thread.Sleep(100);
                            //
                            if (_frmSelectGeometry.DialogResult == DialogResult.OK)
                            {
                                verticesSelection = _frmSelectGeometry.GeometrySelection.DeepClone();
                                SplitAFaceUsingTwoPoints(surfaceSelection, verticesSelection);
                            }
                            else break;
                        }
                        else break;
                    }
                });
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.SplittingFacesText);
            }
        }
        private async void tsmiDefeature_Click(object sender, EventArgs e)
        {
            try
            {
                await Task.Run(() =>
                {
                    _frmSelectGeometry.HideFormOnOK = false;
                    _frmSelectGeometry.SelectionFilter = SelectGeometryEnum.Surface;
                    _frmSelectGeometry.OnOKCallback = Defeature;
                    //
                    InvokeIfRequired(() => { ShowForm(_frmSelectGeometry, "Select faces to defeature", null); });
                    while (_frmSelectGeometry.Visible) Thread.Sleep(100);
                });
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.DefeaturingText);
            }
        }
        //
        private void ConvertShellToSolid(string[] partNames)
        {
            _controller.ConvertShellToSolidCommand(partNames);
        }
        private void ConvertSolidToShell(string[] partNames)
        {
            _controller.ConvertSolidToShellCommand(partNames);
        }
        private void FlipFaces(GeometrySelection geometrySelection)
        {
            try
            {
                SetStateWorking(Globals.FlippingNormalsText);
                _controller.FlipFaceOrientationsCommand(geometrySelection);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                SetStateReady(Globals.FlippingNormalsText);
            }
        }
        private void SplitAFaceUsingTwoPoints(GeometrySelection surfaceSelection, GeometrySelection verticesSelection)
        {
            try
            {
                SetStateWorking(Globals.SplittingFacesText);
                _controller.SplitAFaceUsingTwoPointsCommand(surfaceSelection, verticesSelection);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                SetStateReady(Globals.SplittingFacesText);
            }
        }
        private void Defeature(GeometrySelection geometrySelection)
        {
            try
            {
                SetStateWorking(Globals.DefeaturingText, true);
                _controller.DefeatureCommand(geometrySelection);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                SetStateReady(Globals.DefeaturingText);
            }
        }


        #endregion #################################################################################################################

        #region Geometry Stl part menu   ###########################################################################################
        private void tsmiFindStlEdgesByAngleForGeometryParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetNonCADGeometryParts(), FindEdgesByAngleForGeometryParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiFlipStlPartFaceNormals_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetNonCADGeometryParts(), FlipStlPartSurfacesNormal);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSmoothStlPart_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Parts", _controller.GetNonCADGeometryParts(), _controller.SmoothGeometryPart);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private async void tsmiDeleteStlPartFaces_Click(object sender, EventArgs e)
        {
            try
            {
                await Task.Run(() =>
                {
                    _frmSelectGeometry.HideFormOnOK = false;
                    _frmSelectGeometry.SelectionFilter = SelectGeometryEnum.Surface;
                    _frmSelectGeometry.OnOKCallback = DeleteStlPartFaces;
                    //
                    InvokeIfRequired(() => { ShowForm(_frmSelectGeometry, "Select faces to delete", null); });
                    while (_frmSelectGeometry.Visible) Thread.Sleep(100);
                });
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.DeletingFacesText);
            }
        }
        private void tsmiCropStlPartWithCylinder_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Parts", _controller.GetGeometryParts(), _controller.CropGeometryPartWithCylinder);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiCropStlPartWithCube_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Parts", _controller.GetGeometryParts(), _controller.CropGeometryPartWithCube);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void FlipStlPartSurfacesNormal(string[] partNames)
        {
            _controller.FlipStlPartSurfacesNormalCommand(partNames);
        }
        private void FindEdgesByAngleForGeometryParts(string[] partNames)
        {
            using (FrmGetValue frmGetValue = new FrmGetValue(_controller))
            {
                SetUpFrmGetValueForEdgeAngle(frmGetValue, partNames);
                //
                if (frmGetValue.ShowDialog(this) == DialogResult.OK)
                {
                    _controller.FindEdgesByAngleForGeometryPartsCommand(partNames, frmGetValue.Value);
                }
                SaveFormLocation(frmGetValue);
            }
        }
        private void SetUpFrmGetValueForEdgeAngle(FrmGetValue frmGetValue, string[] partNames)
        {
            frmGetValue.NumOfDigits = 2;
            frmGetValue.MinValue = 0;
            frmGetValue.MaxValue = 90;
            SetFormLocation(frmGetValue);
            OrderedDictionary<string, double> presetValues =
                new OrderedDictionary<string, double>("Preset Transparency Values", StringComparer.OrdinalIgnoreCase);
            presetValues.Add("Default", CaeMesh.Globals.EdgeAngle);
            string desc = "Enter the face angle for model edges detection.";
            frmGetValue.PrepareForm("Find model edges: " + partNames.ToShortString(), "Angle", desc,
                                    CaeMesh.Globals.EdgeAngle, presetValues, new StringAngleDegConverter());
        }
        private void DeleteStlPartFaces(GeometrySelection geometrySelection)
        {
            SetStateWorking(Globals.DeletingFacesText);
            _controller.DeleteStlPartFacesCommand(geometrySelection);
            SetStateReady(Globals.DeletingFacesText);
        }
        
        #endregion #################################################################################################################

        #region Mesh ###############################################################################################################
        // Mesh setup item
        private void tsmiCreateMeshSetupItem_Click(object sender, EventArgs e)
        {
            try
            {
                CreateMeshSetupItem();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditMeshSetupItem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Mesh Setup Items", _controller.GetMeshSetupItems(), EditMeshSetupItem);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateMeshSetupItem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Mesh Setup Items", _controller.GetMeshSetupItems(), DuplicateMeshSetupItems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteMeshSetupItem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Mesh Setup Items", _controller.GetMeshSetupItems(), DeleteMeshSetupItems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // Preview
        private void tsmiPreviewEdgeMesh_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryPartsWithoutSubParts(), PreviewEdgeMesh);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // Create mesh
        internal void tsmiCreateMesh_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryPartsWithoutSubParts(), CreatePartMeshes);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }            
        }
        // Mesh setup item
        private void CreateMeshSetupItem()
        {
            if (_controller.Model.Geometry == null) return;
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, true, _frmMeshSetupItem);
            ShowForm(_frmMeshSetupItem, "Create Mesh Setup Item", null);
        }
        private void EditMeshSetupItem(string meshSetupItemName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, true, _frmMeshSetupItem);
            ShowForm(_frmMeshSetupItem, "Edit Mesh Setup Item", meshSetupItemName);
        }
        private void DuplicateMeshSetupItems(string[] meshSetupItemNames)
        {
            _controller.DuplicateMeshSetupItemsCommand(meshSetupItemNames);
        }
        private void DeleteMeshSetupItems(string[] meshSetupItemNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected mesh setup items?" + Environment.NewLine
                                                 + meshSetupItemNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveMeshSetupItemsCommand(meshSetupItemNames);
            }
        }
        public MeshSetupItem EditMeshingParametersFromHistory(MeshSetupItem meshSetupItem)
        {
            InvokeIfRequired(() =>
            {
                ItemSetDataEditor.SetForms(_frmSelectItemSet, true, null);
                //
                CloseAllForms();
                SetFormLocation(_frmMeshSetupItem);
                _frmMeshSetupItem.Text = "Redefine " + meshSetupItem.Name;
                //
                _frmMeshSetupItem.PrepareForm(null, null);
                _frmMeshSetupItem.SetMeshSetupItem(meshSetupItem.DeepClone());
                _frmMeshSetupItem.ShowDialog(this);
                //
                if (_frmMeshSetupItem.DialogResult == DialogResult.OK) meshSetupItem = _frmMeshSetupItem.MeshSetupItem;
            });
            //
            return meshSetupItem;
        }
        // Preview
        private async void PreviewEdgeMesh(string[] partNames)
        {
            await PreviewEdgeMeshAsync(partNames, null);
        }
        private async Task PreviewEdgeMeshAsync(string[] partNames, MeshSetupItem meshSetupItem)
        {
            bool stateSet = false;
            try
            {
                if (SetStateWorking(Globals.PreviewText))
                {
                    stateSet = true;
                    //
                    await Task.Run(() =>
                    {
                        foreach (var partName in partNames)
                        {
                            _controller.PreviewEdgeMesh(partName, meshSetupItem);
                        }
                    });
                }
                else MessageBoxes.ShowWarning("Another task is already running.");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                if (stateSet) SetStateReady(Globals.PreviewText);
            }
        }
        // Create mesh
        private async void CreatePartMeshes(string[] partNames)
        {
            try
            {
                List<string> errors = new List<string>();
                SetStateWorking(Globals.MeshingText, true);
                MouseEventArgs e = new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0);
                Keys modifierKeys = Keys.Control;
                _modelTree.ClearTreeSelection(ViewType.Model);                
                //
                CloseAllForms();
                //
                Stopwatch watch = new Stopwatch();
                watch.Start();
                //
                GeometryPart part;
                foreach (var partName in partNames)
                {
                    try
                    {
                        part = _controller.GetGeometryPart(partName);
                        //
                        await Task.Run(() => _controller.CreateMeshCommand(partName));
                        // Check for the cancel button click
                        if (IsStateWorking())
                        {
                            _modelTree.SelectBasePart(e, modifierKeys, part, true);
                        }
                        else
                        {
                            errors.Add("Mesh generation canceled.");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is CaeException) errors.Add(partName + ": " + ex.Message);
                        errors.Add("Mesh generation failed for part " + partName +
                                   ". Check the geometry and/or adjust the meshing parameters.");
                    }
                }
                watch.Stop();
                if (partNames.Length > 1)
                {
                    WriteDataToOutput("");
                    WriteDataToOutput("Elapsed time [s]: " + watch.Elapsed.TotalSeconds.ToString());
                }
                //
                _controller.UpdateExplodedView(true);
                //
                if (errors.Count > 0)
                {
                    WriteDataToOutput("");
                    foreach (var error in errors) WriteDataToOutput(error);
                    MessageBoxes.ShowError("Errors occurred during meshing. Please check the output window.");
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.MeshingText);
            }
        }
        // Advisor
        internal void CreateDefaultMesh(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryPartsWithoutSubParts(), CreateDefaultMeshes);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        internal void CreateUserDefinedMesh(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetGeometryPartsWithoutSubParts(), CreateUserDefinedMeshes);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void CreateDefaultMeshes(string[] partNames)
        {
            CreatePartMeshes(partNames);
        }
        private async void CreateUserDefinedMeshes(string[] partNames)
        {
            await Task.Run(() =>
            {
                InvokeIfRequired(() => { CreateMeshSetupItem(); });
                //
                bool firstTime = true;
                do
                {
                    Thread.Sleep(250);
                    if (firstTime)
                    {
                        Selection selection = new Selection();
                        selection.Add(new SelectionNodeIds(vtkSelectOperation.None, false,
                                                           _controller.Model.Geometry.GetPartIdsFomPartNames(partNames)));
                        selection.SelectItem = vtkSelectItem.Part;
                        selection.CurrentView = (int)ViewGeometryModelResults.Geometry;
                        _controller.Selection = selection;
                        _controller.SetSelectByToOff();
                        //
                        InvokeIfRequired(() => {
                            //_controller.HighlightSelection();
                            SelectionChanged(_controller.Model.Geometry.GetPartIdsFomPartNames(partNames));
                        });
                        //
                        firstTime = false;
                    }
                }
                while (_frmMeshSetupItem.Visible);

            });
            if (_frmMeshSetupItem.DialogResult == DialogResult.OK) CreatePartMeshes(partNames);
        }

        #endregion  ################################################################################################################

        #region Model edit  ########################################################################################################
        private void tsmiEditModel_Click(object sender, EventArgs e)
        {
            try
            {
                EditModel();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditCalculiXKeywords_Click(object sender, EventArgs e)
        {
            try
            {
                EditCalculiXKeywords();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // Tools
        private void tsmiFindEdgesByAngleForModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetNonCADModelParts(), FindEdgesByAngleForModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiCreateBoundaryLayer_Click(object sender, EventArgs e)
        {
            try
            {
                CreateBoundaryLayer();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiRemeshElements_Click(object sender, EventArgs e)
        {
            try
            {
                RemeshElements();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiThickenShellMesh_Click(object sender, EventArgs e)
        {
            try
            {
                ThickenShellMesh();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSplitPartMeshUsingSurface_Click(object sender, EventArgs e)
        {
            try
            {
                // Data editor
                ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmSplitPartMeshUsingSurface);
                ShowForm(_frmSplitPartMeshUsingSurface, "Split Part Mesh Using Surface", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private async void tsmiUpdateNodalCoordinatesFromFile_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_controller.ModelInitialized || _controller.Model.Mesh == null) return;
                //
                string fileName = GetFileNameToImport("Abaqus/Calculix inp files|*.inp");
                //
                SetStateWorking(Globals.ImportingText);
                await Task.Run(() => _controller.UpdateNodalCoordinatesFromFileCommand(fileName));
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ImportingText);
            }
        }
        //                                                                                                                          
        private void EditModel()
        {
            if (_controller.Model.UnitSystem.UnitSystemType != UnitSystemType.Undefined)
                ShowForm(_frmModelProperties, "Edit Model", null);

        }
        private void EditCalculiXKeywords()
        {
            // This is also called from the model tree - needs try, catch
            try
            {
                if (CheckValidity())
                {
                    using (_frmCalculixKeywordEditor = new FrmCalculixKeywordEditor())
                    {
                        _frmCalculixKeywordEditor.Keywords = _controller.GetCalculixModelKeywords();
                        _frmCalculixKeywordEditor.UserKeywords = _controller.GetCalculixUserKeywords();
                        //
                        if (_frmCalculixKeywordEditor.Keywords != null)
                        {
                            _frmCalculixKeywordEditor.PrepareForm(); // must be here to check for errors
                            if (_frmCalculixKeywordEditor.ShowDialog(this) == DialogResult.OK)
                            {
                                _controller.SetCalculixUserKeywordsCommand(_frmCalculixKeywordEditor.UserKeywords);
                                _controller.CheckAndUpdateModelValidity(); // update keyword states
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // Tools
        private void FindEdgesByAngleForModelParts(string[] partNames)
        {
            using (FrmGetValue frmGetValue = new FrmGetValue(_controller))
            {
                SetUpFrmGetValueForEdgeAngle(frmGetValue, partNames);
                //
                if (frmGetValue.ShowDialog(this) == DialogResult.OK)
                {
                    _controller.FindEdgesByAngleForModelPartsCommand(partNames, frmGetValue.Value);
                }
                SaveFormLocation(frmGetValue);
            }
        }
        private void CreateBoundaryLayer()
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, true, _frmBoundaryLayer);
            ShowForm(_frmBoundaryLayer, "Create Boundary Layer", null);
        }
        private void RemeshElements()
        {
            if (_controller.Model == null || _controller.Model.Mesh == null) return;
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmRemeshingParameters);
            ShowForm(_frmRemeshingParameters, "Remeshing Parameters", null);
        }
        private void ThickenShellMesh()
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, true, _frmThickenShellMesh);
            ShowForm(_frmThickenShellMesh, "Thicken Shell Mesh", null);
        }
        private async void SplitPartMeshUsingSurface(SplitPartMeshData splitPartMeshData)
        {
            try
            {
                if (!_controller.ModelInitialized || _controller.Model.Mesh == null) return;
                //
                SetStateWorking(Globals.MeshingText, true);
                await Task.Run(() => _controller.SplitPartMeshUsingSurfaceCommand(splitPartMeshData));
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.MeshingText);
            }
        }
        public void ShowSplitMeshResults()
        {
            if (_controller.CurrentResult != null && _controller.CurrentResult.Mesh != null)
            {
                SetResultNames();
                // Reset the previous step and increment
                SetAllStepAndIncrementIds();
                // Set last increment
                SetDefaultStepAndIncrementIds();
                // Show the selection in the results tree
                SelectFirstComponentOfFirstFieldOutput();
            }
            // Set the representation which also calls Draw
            _controller.ViewResultsType = ViewResultsTypeEnum.ColorContours;  // Draw
            //
            SetMenuAndToolStripVisibility();
        }
        private void UpdateNodalCoordinatesFromFile()
        {

        }

        #endregion  ################################################################################################################

        #region Node menu  #########################################################################################################
        private void tsmiRenumberAllNodes_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                //
                using (FrmGetValue frmGetValue = new FrmGetValue(_controller))
                {
                    frmGetValue.NumOfDigits = 0;
                    frmGetValue.MinValue = 1;
                    SetFormLocation(frmGetValue);
                    string desc = "Enter the starting node id for the node renumbering.";
                    frmGetValue.PrepareForm("Renumber Nodes", "Start node id", desc, 1, null);
                    if (frmGetValue.ShowDialog(this) == DialogResult.OK)
                    {
                        _controller.RenumberNodesCommand((int)frmGetValue.Value);
                    }
                    SaveFormLocation(frmGetValue);
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiMergeCoincidentNodes_Click(object sender, EventArgs e)
        {
            try
            {
                MergeCoincidentNodes();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //                                                                                                                          
        private void MergeCoincidentNodes()
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmMergeCoincidentNodes);
            ShowForm(_frmMergeCoincidentNodes, "Merge Coincident Nodes", null);
        }
        #endregion  ################################################################################################################

        #region Element menu  ######################################################################################################
        private void tsmiRenumberAllElements_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                //
                using (FrmGetValue frmGetValue = new FrmGetValue(_controller))
                {
                    frmGetValue.NumOfDigits = 0;
                    frmGetValue.MinValue = 1;
                    SetFormLocation(frmGetValue);
                    string desc = "Enter the starting element id for the element renumbering.";
                    frmGetValue.PrepareForm("Renumber Elements", "Start element id", desc, 1, null);
                    if (frmGetValue.ShowDialog(this) == DialogResult.OK)
                    {
                        _controller.RenumberElementsCommand((int)frmGetValue.Value);
                    }
                    SaveFormLocation(frmGetValue);
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiElementQuality_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), ElementQuality);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void ElementQuality(string[] partNames)
        {
            // Set all part names for quality analysis
            _frmElementQuality.PartNames = partNames;
            //
            ShowForm(_frmElementQuality, "Element Quality", null);
        }
        #endregion  ################################################################################################################

        #region Model part menu  ###################################################################################################
        private void tsmiEditModelPart_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Parts", _controller.GetModelParts(), EditModelPart);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // Transform
        private void tsmiTranslateModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), TranslateModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiRotateModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), RotateModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiMirrorModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), MirrorModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiScaleModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), ScaleModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void tsmiMergeModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), MergeModelParts, 2);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), HideModelParts);
                Clear3DSelection();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), ShowModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowOnlyModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), ShowOnlyModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSetColorForModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), SetColorForModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiResetColorForModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), ResetColorForModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSetTransparencyForModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), SetTransparencyForModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteModelParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetModelParts(), DeleteModelParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //                                                                                                                          
        private void EditModelPart(string partName)
        {
            _frmPartProperties.View = ViewGeometryModelResults.Model; 
            ShowForm(_frmPartProperties, "Edit Part", partName);
        }
        // Transform
        private void TranslateModelParts(string[] partNames)
        {
            SinglePointDataEditor.ParentForm = _frmTranslate;
            SinglePointDataEditor.Controller = _controller;
            // Set all part names for translation
            _frmTranslate.PartNames = partNames;    
            //
            ShowForm(_frmTranslate, "Translate Parts: " + partNames.ToShortString(), null);
        }
        private void RotateModelParts(string[] partNames)
        {
            SinglePointDataEditor.ParentForm = _frmRotate;
            SinglePointDataEditor.Controller = _controller;
            // Set all part names for rotation
            _frmRotate.PartNames = partNames;
            //
            ShowForm(_frmRotate, "Rotate Parts: " + partNames.ToShortString(), null);
        }
        private void MirrorModelParts(string[] partNames)
        {
            SinglePointDataEditor.ParentForm = _frmMirror;
            SinglePointDataEditor.Controller = _controller;
            // Set all part names for mirror
            _frmMirror.PartNames = partNames;    
            //
            ShowForm(_frmMirror, "Mirror Parts: " + partNames.ToShortString(), null);
        }
        private void ScaleModelParts(string[] partNames)
        {
            SinglePointDataEditor.ParentForm = _frmScale;
            SinglePointDataEditor.Controller = _controller;
            // Set all part names for scaling
            _frmScale.PartNames = partNames;
            //
            ShowForm(_frmScale, "Scale Parts: " + partNames.ToShortString(), null);
        }
        //
        private void MergeModelParts(string[] partNames)
        {
            if (_controller.AreModelPartsMergeable(partNames))
            {
                if (MessageBoxes.ShowWarningQuestionOKCancel("OK to merge selected parts?") == DialogResult.OK)
                {
                    _controller.MergeModelPartsCommand(partNames);
                }
            }
            else MessageBoxes.ShowError("Selected parts are of a different type and thus cannot be merged.");
        }
        private void HideModelParts(string[] partNames)
        {
            _controller.HideModelPartsCommand(partNames);
        }
        private void ShowModelParts(string[] partNames)
        {
            _controller.ShowModelPartsCommand(partNames);
        }
        private void ShowOnlyModelParts(string[] partNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.Model.Mesh.Parts.Keys);
            allNames.ExceptWith(partNames);
            _controller.HideModelPartsCommand(allNames.ToArray());
            _controller.ShowModelPartsCommand(partNames);
        }
        private void SetColorForModelParts(string[] partNames)
        {
            if (_controller.Model.Mesh == null) return;
            //
            using (FrmGetColor frmGetColor = new FrmGetColor())
            {
                Color color = _controller.Model.Mesh.Parts[partNames[0]].GetProperties().Color;
                SetFormLocation(frmGetColor);
                frmGetColor.PrepareForm("Set Part Color: " + partNames.ToShortString(), color);
                if (frmGetColor.ShowDialog(this) == DialogResult.OK)
                {
                    _controller.SetColorForModelPartsCommand(partNames, frmGetColor.Color);
                }
                SaveFormLocation(frmGetColor);
            }
        }
        private void ResetColorForModelParts(string[] partNames)
        {
            _controller.ResetColorForModelPartsCommand(partNames);
        }
        private void SetTransparencyForModelParts(string[] partNames)
        {
            if (_controller.Model.Mesh == null) return;
            //
            using (FrmGetValue frmGetValue = new FrmGetValue(_controller))
            {
                frmGetValue.NumOfDigits = 0;
                frmGetValue.MinValue = 25;
                frmGetValue.MaxValue = 255;
                SetFormLocation(frmGetValue);
                OrderedDictionary<string, double> presetValues
                    = new OrderedDictionary<string, double>("Preset Transparency Values", StringComparer.OrdinalIgnoreCase);
                presetValues.Add("Semi-transparent", 128);
                presetValues.Add("Opaque", 255);
                string desc = "Enter the transparency between 0 and 255.\n" + "(0 - transparent; 255 - opaque)";
                frmGetValue.PrepareForm("Set Transparency: " + partNames.ToShortString(), "Transparency", desc, 128, presetValues);
                if (frmGetValue.ShowDialog(this) == DialogResult.OK)
                {
                    _controller.SetTransparencyForModelPartsCommand(partNames, (byte)frmGetValue.Value);
                }
                SaveFormLocation(frmGetValue);
            }
        }
        private void DeleteModelParts(string[] partNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected parts?" + Environment.NewLine
                                                 + partNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveModelPartsCommand(partNames);
            }
        }
        
        #endregion  ################################################################################################################

        #region Node set menu  #####################################################################################################
        private void tsmiCreateNodeSet_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                // Data editor
                ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmNodeSet);
                ShowForm(_frmNodeSet, "Create Node Set", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditNodeSet_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Node Sets", _controller.GetUserNodeSets(), EditNodeSet);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateNodeSet_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Node Sets", _controller.GetUserNodeSets(), DuplicateNodeSets);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteNodeSet_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Node Sets", _controller.GetUserNodeSets(), DeleteNodeSets);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditNodeSet(string nodeSetName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmNodeSet);
            ShowForm(_frmNodeSet, "Edit Node Set", nodeSetName);
        }
        private void DuplicateNodeSets(string[] nodeSetNames)
        {
            _controller.DuplicateNodeSetsCommand(nodeSetNames);
        }
        private void DeleteNodeSets(string[] nodeSetNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected node sets?" + Environment.NewLine
                                                 + nodeSetNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveNodeSetsCommand(nodeSetNames);
            }
        }

        #endregion  ################################################################################################################

        #region Element set menu  ##################################################################################################
        private void tsmiCreateElementSet_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                // Data editor
                ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmElementSet);
                ShowForm(_frmElementSet, "Create Element Set", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditElementSet_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Element Sets", _controller.GetUserElementSets(), EditElementSet);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateElementSet_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Element Sets", _controller.GetUserElementSets(), DuplicateElementSets);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiConvertElementSetsToMeshParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Element Sets", _controller.GetUserElementSets(), ConvertElementSetsToMeshParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteElementSet_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Element Sets", _controller.GetUserElementSets(), DeleteElementSets);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditElementSet(string elementSetName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmElementSet);
            ShowForm(_frmElementSet, "Edit Element Set", elementSetName);
        }
        private void DuplicateElementSets(string[] elementSetNames)
        {
            _controller.DuplicateElementSetsCommand(elementSetNames);
        }
        private void ConvertElementSetsToMeshParts(string[] elementSetNames)
        {
            _controller.ConvertElementSetsToMeshPartsCommand(elementSetNames);
        }
        private void DeleteElementsInElementSets(string[] elementSetNames)
        {
            FeElementSet elementSet;
            HashSet<int> elementIds = new HashSet<int>();
            HashSet<int> partIds;
            foreach (string elementSetName in elementSetNames)
            {
                elementSet = _controller.GetElementSet(elementSetName);
                if (elementSet != null)
                {
                    if (elementSet.CreatedFromParts)
                    {
                        partIds = new HashSet<int>(elementSet.Labels);
                        foreach (var entry in _controller.Model.Mesh.Parts)
                        {
                            if (partIds.Contains(entry.Value.PartId)) elementIds.UnionWith(entry.Value.Labels);
                        }
                    }
                    else
                    {
                        foreach (int elementId in elementSet.Labels) elementIds.Add(elementId);
                    }
                }
            }
            //
            string text = "OK to delete " + elementIds.Count + " elements in selected element sets?"
                          + Environment.NewLine + elementSetNames.ToRows();
            if (MessageBoxes.ShowWarningQuestionOKCancel(text) == DialogResult.OK)
            {
                _controller.DeleteElementsInElementSetsCommand(elementSetNames);
            }
        }
        private void DeleteElementSets(string[] elementSetNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected element sets?" + Environment.NewLine
                                                 + elementSetNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveElementSetsCommand(elementSetNames);
            }
        }

        #endregion  ################################################################################################################

        #region Surface menu  ######################################################################################################
        private void tsmiCreateSurface_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                // Data editor
                ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmSurface);
                ShowForm(_frmSurface, "Create Surface", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditSurface_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Surfaces", _controller.GetUserSurfaces(), EditSurface);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateSurface_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Surfaces", _controller.GetUserSurfaces(), DuplicateSurfaces);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteSurface_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Surfaces", _controller.GetUserSurfaces(), DeleteSurfaces);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditSurface(string surfaceName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmSurface);
            ShowForm(_frmSurface, "Edit Surface", surfaceName);
        }
        private void DuplicateSurfaces(string[] surfaceNames)
        {
            _controller.DuplicateSurfacesCommand(surfaceNames);
        }
        private void DeleteSurfaces(string[] surfaceNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected surfaces?" + Environment.NewLine
                                                 + surfaceNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveSurfacesCommand(surfaceNames);
            }
        }

        #endregion  ################################################################################################################

        #region Model Reference point  #############################################################################################
        private void tsmiCreateModelReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                //
                ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmReferencePoint);
                ShowForm(_frmReferencePoint, "Create Model Reference Point", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditModelReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Model Reference Points", _controller.GetAllModelReferencePoints(), EditModelReferencePoint);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateModelReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Model Reference Points", _controller.GetAllModelReferencePoints(),
                                       DuplicateModelReferencePoints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideModelReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Model Reference Points", _controller.GetAllModelReferencePoints(),
                                       HideModelReferencePoints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowModelReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Model Reference Points", _controller.GetAllModelReferencePoints(),
                                       ShowModelReferencePoints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowOnlyModelReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Model Reference Points", _controller.GetAllModelReferencePoints(),
                                       ShowOnlyModelReferencePoints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteModelReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Model Reference Points", _controller.GetAllModelReferencePoints(),
                                       DeleteModelReferencePoints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditModelReferencePoint(string referencePointName)
        {
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmReferencePoint);
            ShowForm(_frmReferencePoint, "Edit Reference Point", referencePointName);
        }
        private void DuplicateModelReferencePoints(string[] referencePointNames)
        {
            _controller.DuplicateModelReferencePointsCommand(referencePointNames);
        }
        private void HideModelReferencePoints(string[] referencePointNames)
        {
            _controller.HideModelReferencePointsCommand(referencePointNames);
        }
        private void ShowModelReferencePoints(string[] referencePointNames)
        {
            _controller.ShowModelReferencePointsCommand(referencePointNames);
        }
        private void ShowOnlyModelReferencePoints(string[] referencePointNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.GetModelReferencePointNames());
            allNames.ExceptWith(referencePointNames);
            _controller.HideModelReferencePointsCommand(allNames.ToArray());
            _controller.ShowModelReferencePointsCommand(referencePointNames);
        }
        private void DeleteModelReferencePoints(string[] referencePointNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected model reference points?" + Environment.NewLine
                                                 + referencePointNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveModelReferencePointsCommand(referencePointNames);
            }
        }

        #endregion  ################################################################################################################

        #region Model Cylinder  ####################################################################################################
        private void tsmiCreateModelCylinder_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model == null) return;
                ShowForm(_frmCylinder, "Create Model Cylinder", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void EditModelCylinder(string cylinderName)
        {
            ShowForm(_frmCylinder, "Edit Cylinder", cylinderName);
        }
        private void DuplicateModelCylinders(string[] cylinderNames)
        {
            _controller.DuplicateModelCylindersCommand(cylinderNames);
        }
        private void DeleteModelCylinders(string[] cylinderNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected model cylinders?" + Environment.NewLine
                                                 + cylinderNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveModelCylindersCommand(cylinderNames);
            }
        }

        #endregion  ################################################################################################################

        #region Model Coordinate system  ###########################################################################################
        private void tsmiCreateModelCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmCoordinateSystem);
                ShowForm(_frmCoordinateSystem, "Create Model Coordinate System", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditModelCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Model Coordinate Systems", _controller.GetAllModelCoordinateSystems(),
                                EditModelCoordinateSystem);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateModelCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Model Coordinate Systems", _controller.GetAllModelCoordinateSystems(),
                                       DuplicateModelCoordinateSystems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideModelCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Model Coordinate Systems", _controller.GetAllModelCoordinateSystems(),
                                       HideModelCoordinateSystems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowModelCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Model Coordinate Systems", _controller.GetAllModelCoordinateSystems(),
                                       ShowModelCoordinateSystems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowOnlyModelCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Model Coordinate Systems", _controller.GetAllModelCoordinateSystems(),
                                       ShowOnlyModelCoordinateSystems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteModelCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Model Coordinate Systems", _controller.GetAllModelCoordinateSystems(),
                                       DeleteModelCoordinateSystems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditModelCoordinateSystem(string coordinateSystemName)
        {
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmCoordinateSystem);
            ShowForm(_frmCoordinateSystem, "Edit Coordinate System", coordinateSystemName);
        }
        private void DuplicateModelCoordinateSystems(string[] coordinateSystemNames)
        {
            _controller.DuplicateModelCoordinateSystemsCommand(coordinateSystemNames);
        }
        private void HideModelCoordinateSystems(string[] coordinateSystemNames)
        {
            _controller.HideModelCoordinateSystemsCommand(coordinateSystemNames);
        }
        private void ShowModelCoordinateSystems(string[] coordinateSystemNames)
        {
            _controller.ShowModelCoordinateSystemsCommand(coordinateSystemNames);
        }
        private void ShowOnlyModelCoordinateSystems(string[] coordinateSystemNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.Model.Mesh.CoordinateSystems.Keys);
            allNames.ExceptWith(coordinateSystemNames);
            _controller.HideModelCoordinateSystemsCommand(allNames.ToArray());
            _controller.ShowModelCoordinateSystemsCommand(coordinateSystemNames);
        }
        private void DeleteModelCoordinateSystems(string[] coordinateSystemNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected model coordinate systems?" + Environment.NewLine
                                                 + coordinateSystemNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveModelCoordinateSystemsCommand(coordinateSystemNames);
            }
        }

        #endregion  ################################################################################################################

        #region Material menu  #####################################################################################################
        internal void tsmiCreateMaterial_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                ShowForm(_frmMaterial, "Create Material", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        internal void CreateSimpleMaterial(object sender, EventArgs e)
        {
            _frmMaterial.UseSimpleEditor = true;
            tsmiCreateMaterial_Click(sender, e);
        }
        private void tsmiEditMaterial_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Materials", _controller.GetAllMaterials(), EditMaterial);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateMaterial_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Materials", _controller.GetAllMaterials(), DuplicateMaterials);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private  void tsmiImportMaterial_Click(object sender, EventArgs e)
        {
            ImportFile(true);
        }
        private void tsmiExportMaterial_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Materials", _controller.GetAllMaterials(), ExportMaterials);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteMaterial_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Materials", _controller.GetAllMaterials(), DeleteMaterials);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
           
        }
        //
        private void EditMaterial(string materialName)
        {
            ShowForm(_frmMaterial, "Edit Material", materialName);
        }
        private void DuplicateMaterials(string[] materialNames)
        {
            _controller.DuplicateMaterialsCommand(materialNames);
        }
        private async void ExportMaterials(string[] materialNames)
        {
            try
            {
                _controller.CurrentView = ViewGeometryModelResults.Model;
                //
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Calculix files | *.inp";
                    //
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // The filter adds the extension to the file name
                        SetStateWorking(Globals.ExportingText);
                        //
                        await Task.Run(() => _controller.ExportMaterials(materialNames, saveFileDialog.FileName));
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.ExportingText);
            }
        }
        private void DeleteMaterials(string[] materialNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected materials?" + Environment.NewLine
                                                 + materialNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveMaterialsCommand(materialNames);
            }
        }
        //
        internal void tsmiMaterialLibrary_Click(object sender, EventArgs e)
        {
            try
            {
                ShowMaterialLibrary();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void ShowMaterialLibrary()
        {
            if (_controller.Model.Mesh != null)
            {
                using (FrmMaterialLibrary fml = new FrmMaterialLibrary(_controller))
                {
                    CloseAllForms();
                    SetFormLocation(fml);
                    fml.ShowDialog(this);
                }
            }
        }

        #endregion  ################################################################################################################

        #region Section menu  ######################################################################################################
        internal void tsmiCreateSection_Click(object sender, EventArgs e)
        {
            try
            {
                CreateSection();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditSection_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Sections", _controller.GetAllSections(), EditSection);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateSection_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Sections", _controller.GetAllSections(), DuplicateSections);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDelete_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Sections", _controller.GetAllSections(), DeleteSections);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void CreateSection()
        {
            if (_controller.Model.Mesh == null) return;
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmSection);
            ShowForm(_frmSection, "Create Section", null);
        }
        private void EditSection(string sectionName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmSection);
            ShowForm(_frmSection, "Edit Section", sectionName);
        }
        private void DuplicateSections(string[] sectionNames)
        {
            _controller.DuplicateSectionsCommand(sectionNames);
        }
        private void DeleteSections(string[] sectionNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected sections?" + Environment.NewLine
                                                 + sectionNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveSectionsCommand(sectionNames);
            }
        }

        #endregion  ################################################################################################################

        #region Interaction menu  ##################################################################################################

        #region Constraint menu  ###################################################################################################
        private void tsmiCreateConstraint_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                // Data editor
                ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmConstraint);
                ShowForm(_frmConstraint, "Create Constraint", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditConstraint_Click(object sender, EventArgs e)
        {
             try
             {
                 SelectOneEntity("Constraints", _controller.GetAllConstraints(), EditConstraint);
             }
             catch (Exception ex)
             {
                 ExceptionTools.Show(this, ex);
             }
        }
        private void tsmiDuplicateConstraint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Constraints", _controller.GetAllConstraints(), DuplicateConstraints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSwapMasterSlaveConstraint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Constraints", _controller.GetAllConstraints(), SwapMasterSlaveConstraints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiMergeByMasterSlaveConstraint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Constraints", _controller.GetAllConstraints(), MergeByMasterSlaveConstraints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideConstraint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Constraints", _controller.GetAllConstraints(), HideConstraints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowConstraint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Constraints", _controller.GetAllConstraints(), ShowConstraints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteConstraint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Constraints", _controller.GetAllConstraints(), DeleteConstraints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditConstraint(string constraintName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmConstraint);
            ShowForm(_frmConstraint, "Edit Constraint", constraintName);
        }
        private void DuplicateConstraints(string[] constraintNames)
        {
            _controller.DuplicateConstraintsCommand(constraintNames);
        }
        private void SwapMasterSlaveConstraints(string[] constraintNames)
        {
            _controller.SwapMasterSlaveConstraintsCommand(constraintNames);
        }
        private void MergeByMasterSlaveConstraints(string[] constraintNames)
        {
            _controller.MergeByMasterSlaveConstraintsCommand(constraintNames);
        }
        private void HideConstraints(string[] constraintNames)
        {
            _controller.HideConstraintsCommand(constraintNames);
        }
        private void ShowConstraints(string[] constraintNames)
        {
            _controller.ShowConstraintsCommand(constraintNames);
        }
        private void ShowOnlyConstraints(string[] constraintNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.Model.Constraints.Keys);
            allNames.ExceptWith(constraintNames);
            _controller.HideConstraintsCommand(allNames.ToArray());
            _controller.ShowConstraintsCommand(constraintNames);
        }
        private void DeleteConstraints(string[] constraintNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected constraints?" + Environment.NewLine
                                                 + constraintNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveConstraintsCommand(constraintNames);
            }
        }

        #endregion  ################################################################################################################
        
        #region Surface interaction menu  ##########################################################################################
        private void tsmiCreateSurfaceInteraction_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                ShowForm(_frmSurfaceInteraction, "Create surface interaction", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditSurfaceInteraction_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Surface interactions", _controller.GetAllSurfaceInteractions(), EditSurfaceInteraction);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateSurfaceInteraction_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Surface interactions", _controller.GetAllSurfaceInteractions(),
                                       DuplicateSurfaceInteractions);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteSurfaceInteraction_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Surface interactions", _controller.GetAllSurfaceInteractions(), DeleteSurfaceInteractions);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }

        }
        //
        private void EditSurfaceInteraction(string surfaceInteractionName)
        {
            ShowForm(_frmSurfaceInteraction, "Edit surface interaction", surfaceInteractionName);
        }
        private void DuplicateSurfaceInteractions(string[] surfaceInteractionNames)
        {
            _controller.DuplicateSurfaceInteractionsCommand(surfaceInteractionNames);
        }
        private void DeleteSurfaceInteractions(string[] surfaceInteractionNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected surface interactions?" + Environment.NewLine
                                                 + surfaceInteractionNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveSurfaceInteractionsCommand(surfaceInteractionNames);
            }
        }



        #endregion  ################################################################################################################

        #region Contact pair menu  #################################################################################################
        private void tsmiCreateContactPair_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                // Data editor
                ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmContactPair);
                ShowForm(_frmContactPair, "Create Contact Pair", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditContactPair_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Contact pairs", _controller.GetAllContactPairs(), EditContactPair);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateContactPair_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Contact pairs", _controller.GetAllContactPairs(), DuplicateContactPairs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiCheckContactPair_Click(object sender, EventArgs e)
        {
            try
            {
                CheckContactPairs(null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSwapMasterSlaveContactPair_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Contact pairs", _controller.GetAllContactPairs(), SwapMasterSlaveContactPairs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiMergeByMasterSlaveContactPair_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Contact pairs", _controller.GetAllContactPairs(), MergeByMasterSlaveContactPairs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideContactPair_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Contact pairs", _controller.GetAllContactPairs(), HideContactPairs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowContactPair_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Contact pairs", _controller.GetAllContactPairs(), ShowContactPairs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteContactPair_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Contact pairs", _controller.GetAllContactPairs(), DeleteContactPairs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditContactPair(string contactPairName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmContactPair);
            ShowForm(_frmContactPair, "Edit Contact Pair", contactPairName);
        }
        private void DuplicateContactPairs(string[] contactPairNames)
        {
            _controller.DuplicateContactPairsCommand(contactPairNames);
        }
        private void CheckContactPairs(string[] contactPairNames)
        {
            if (_controller.Model == null || _controller.Model.Mesh == null) return;
            //
            ContactPairValidationResult[] results = _controller.CheckContactPairs(contactPairNames);
            Dictionary<string, bool> passedByName = new Dictionary<string, bool>();
            foreach (ContactPairValidationResult result in results) passedByName[result.ContactPairName] = result.Passed;
            _modelTree.SetContactPairCheckStatus(passedByName);
        }
        private void SwapMasterSlaveContactPairs(string[] contactPairNames)
        {
            _controller.SwapMasterSlaveContactPairsCommand(contactPairNames);
        }
        private void MergeByMasterSlaveContactPairs(string[] contactPairNames)
        {
            _controller.MergeByMasterSlaveContactPairsCommand(contactPairNames);
        }
        private void HideContactPairs(string[] contactPairNames)
        {
            _controller.HideContactPairsCommand(contactPairNames);
        }
        private void ShowContactPairs(string[] contactPairNames)
        {
            _controller.ShowContactPairsCommand(contactPairNames);
        }
        private void ShowOnlyContactPairs(string[] contactPairNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.Model.ContactPairs.Keys);
            allNames.ExceptWith(contactPairNames);
            _controller.HideContactPairsCommand(allNames.ToArray());
            _controller.ShowContactPairsCommand(contactPairNames);
        }
        private void DeleteContactPairs(string[] contactPairNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected contact pairs?" + Environment.NewLine
                                                 + contactPairNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveContactPairsCommand(contactPairNames);
            }
        }
        #endregion  ################################################################################################################

        private void tsmiSearchContactPairs_Click(object sender, EventArgs e)
        {
            try
            {
                SearchContactPairs();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void SearchContactPairs()
        {
            if (_controller.Model == null || _controller.Model.Mesh == null) return;
            //
            if (!_frmSearchContactPairs.Visible)
            {
                CloseAllForms();
                SetFormLocation(_frmSearchContactPairs);
                _frmSearchContactPairs.PrepareForm();
                _frmSearchContactPairs.Show(this);
            }
        }
        private void tsmiAutoCreateCylindricalTies_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model == null || _controller.Model.Mesh == null) return;
                //
                int skippedExisting;
                int missingInnerSurfaces;
                int created = _controller.AutoCreateCylindricalSurfaceTies(out skippedExisting, out missingInnerSurfaces);
                //
                StringBuilder sb = new StringBuilder();
                sb.Append("Created ");
                sb.Append(created);
                sb.Append(" cylindrical tie constraint");
                if (created != 1) sb.Append("s");
                sb.Append(".");
                if (skippedExisting > 0)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(skippedExisting);
                    sb.Append(" matching tie pair");
                    if (skippedExisting != 1) sb.Append("s were");
                    else sb.Append(" was");
                    sb.Append(" already present.");
                }
                if (missingInnerSurfaces > 0)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(missingInnerSurfaces);
                    sb.Append(" base surface");
                    if (missingInnerSurfaces != 1) sb.Append("s had");
                    else sb.Append(" had");
                    sb.Append(" no matching inner surface.");
                }
                MessageBoxes.ShowInfo(sb.ToString(), "Cylindrical Ties");
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }

        #endregion  ################################################################################################################

        #region Distribution menu  #################################################################################################
        private void tsmiCreateDistribution_Click(object sender, EventArgs e)
        {
            try
            {
                CreateDistribution();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }

        private void tsmiEditDistribution_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Distributions", _controller.GetAllDistributions(), EditDistribution);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }

        private void tsmiDuplicateDistribution_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Distributions", _controller.GetAllDistributions(), DuplicateDistributions);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }

        private void tsmiDeleteDistribution_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Distributions", _controller.GetAllDistributions(), DeleteDistributions);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void CreateDistribution()
        {
            if (_controller.Model.Mesh == null) return;
            ShowForm(_frmDistribution, "Create Distribution", null);
        }
        private void EditDistribution(string amplitudeName)
        {
            ShowForm(_frmDistribution, "Edit Distribution", amplitudeName);
        }
        private void DuplicateDistributions(string[] distributionNames)
        {
            _controller.DuplicateDistributionsCommand(distributionNames);
        }
        private void DeleteDistributions(string[] distributionNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected distribution?" + Environment.NewLine
                                                 + distributionNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveDistributionsCommand(distributionNames);
            }
        }
        #endregion  ################################################################################################################

        #region Amplitude menu  ####################################################################################################
        private void tsmiCreateAmplitude_Click(object sender, EventArgs e)
        {
            try
            {
                CreateAmplitude();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditAmplitude_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Amplitudes", _controller.GetAllAmplitudes(), EditAmplitude);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateAmplitude_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Amplitudes", _controller.GetAllAmplitudes(), DuplicateAmplitudes);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteAmplitude_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Amplitudes", _controller.GetAllAmplitudes(), DeleteAmplitudes);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void CreateAmplitude()
        {
            if (_controller.Model.Mesh == null) return;
            ShowForm(_frmAmplitude, "Create Amplitude", null);
        }
        private void EditAmplitude(string amplitudeName)
        {
            ShowForm(_frmAmplitude, "Edit Amplitude", amplitudeName);
        }
        private void DuplicateAmplitudes(string[] amplitudeNames)
        {
            _controller.DuplicateAmplitudesCommand(amplitudeNames);
        }
        private void DeleteAmplitudes(string[] amplitudeNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected amplitudes?" + Environment.NewLine
                                                 + amplitudeNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveAmplitudesCommand(amplitudeNames);
            }
        }

        #endregion  ################################################################################################################

        #region Initial condition menu  ############################################################################################
        private void tsmiCreateInitialCondition_Click(object sender, EventArgs e)
        {
            try
            {
                CreateInitialCondition();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditInitialCondition_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Initial Conditions", _controller.GetAllInitialConditions(), EditInitialCondition);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateInitialCondition_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Initial Conditions", _controller.GetAllInitialConditions(), DuplicateInitialConditions);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiPreviewInitialCondition_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Initial Conditions", _controller.GetAllInitialConditions(), PreviewInitialCondition);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideInitialCondition_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Initial Conditions", _controller.GetAllInitialConditions(), HideInitialConditions);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowInitialCondition_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Initial Conditions", _controller.GetAllInitialConditions(), ShowInitialConditions);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteInitialCondition_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Initial Conditions", _controller.GetAllInitialConditions(), DeleteInitialConditions);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void CreateInitialCondition()
        {
            if (_controller.Model.Mesh == null) return;
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, true, _frmInitialCondition);
            //
            SinglePointDataEditor.ParentForm = _frmInitialCondition;
            SinglePointDataEditor.Controller = _controller;
            //
            ShowForm(_frmInitialCondition, "Create Initial Condition", null);
        }
        private void EditInitialCondition(string initialConditionName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, true, _frmInitialCondition);
            //
            SinglePointDataEditor.ParentForm = _frmInitialCondition;
            SinglePointDataEditor.Controller = _controller;
            //
            ShowForm(_frmInitialCondition, "Edit Initial Condition", initialConditionName);
        }
        private void DuplicateInitialConditions(string[] initialConditionNames)
        {
            _controller.DuplicateInitialConditionsCommand(initialConditionNames);
        }
        private void PreviewInitialConditions(string[] initialConditionNames)
        {
            foreach (var name in initialConditionNames) PreviewInitialCondition(name);
        }
        private void PreviewInitialCondition(string initialConditionName)
        {
            _controller.PreviewInitialCondition(initialConditionName);
            //
            if (_controller.CurrentResult != null && _controller.CurrentResult.Mesh != null)
            {
                SetResultNames();
                // Reset the previous step and increment
                SetAllStepAndIncrementIds();
                // Set last increment
                SetDefaultStepAndIncrementIds();
                // Show the selection in the results tree
                SelectFirstComponentOfFirstFieldOutput();
            }
            // Set the representation which also calls Draw
            _controller.ViewResultsType = ViewResultsTypeEnum.ColorContours;  // Draw
            //
            SetMenuAndToolStripVisibility();
        }
        private void HideInitialConditions(string[] initialConditionNames)
        {
            _controller.HideInitialConditionsCommand(initialConditionNames);
        }
        private void ShowInitialConditions(string[] initialConditionNames)
        {
            _controller.ShowInitialConditionsCommand(initialConditionNames);
        }
        private void ShowOnlyInitialConditions(string[] initialConditionNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.Model.InitialConditions.Keys);
            allNames.ExceptWith(initialConditionNames);
            _controller.HideInitialConditionsCommand(allNames.ToArray());
            _controller.ShowInitialConditionsCommand(initialConditionNames);
        }
        private void DeleteInitialConditions(string[] initialConditionNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected initial conditions?" + Environment.NewLine
                                                 + initialConditionNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveInitialConditionsCommand(initialConditionNames);
            }
        }

        #endregion  ################################################################################################################

        #region Step menu  #########################################################################################################
        internal void tsmiCreateStep_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                //
                int selectedIndex = -1;
                if (e is EventArgs<int> ea) selectedIndex = ea.Value;
                _frmStep.SetPreselectListViewItem(selectedIndex);
                //
                ShowForm(_frmStep, "Create Step", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditStep_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), EditStep);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditStepControls_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), EditStepControls);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateStep_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Steps", _controller.GetAllSteps(), DuplicateSteps);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteStep_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Steps", _controller.GetAllSteps(), DeleteSteps);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }

        private void EditStep(string stepName)
        {
            ShowForm(_frmStep, "Edit Step", stepName);
        }
        private void EditStepControls(string stepName)
        {
            ShowForm(_frmStepControls, "Edit Step Controls", stepName, null);
        }
        private void DuplicateSteps(string[] stepNames)
        {
            _controller.DuplicateStepsCommand(stepNames);
        }
        private void DeleteSteps(string[] stepNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected steps?" + Environment.NewLine
                                                 + stepNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveStepsCommand(stepNames);
            }
        }

        #endregion  ################################################################################################################

        #region History output menu  ###############################################################################################
        private void tsmiCreateHistoryOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), CreateHistoryOutput);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditHistoryOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndEditHistoryOutput);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateHistoryOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndDuplicateHistoryOutputs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiPropagateHistoryOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndPropagateHistoryOutput);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteHistoryOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndDeleteHistoryOutputs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void SelectAndEditHistoryOutput(string stepName)
        {
            SelectOneEntityInStep("History outputs", _controller.GetAllHistoryOutputs(stepName), stepName, EditHistoryOutput);
        }
        private void SelectAndDuplicateHistoryOutputs(string stepName)
        {
            SelectMultipleEntitiesInStep("History outputs", _controller.GetAllHistoryOutputs(stepName),
                                         stepName, DuplicateHistoryOutputs);
        }
        private void SelectAndPropagateHistoryOutput(string stepName)
        {
            SelectOneEntityInStep("History outputs", _controller.GetAllHistoryOutputs(stepName), stepName, PropagateHistoryOutput);
        }
        private void SelectAndDeleteHistoryOutputs(string stepName)
        {
            SelectMultipleEntitiesInStep("History outputs", _controller.GetAllHistoryOutputs(stepName),
                                         stepName, DeleteHistoryOutputs);
        }
        //
        private void CreateHistoryOutput(string stepName)
        {
            if (_controller.Model.Mesh == null) return;
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmHistoryOutput);
            ShowForm(_frmHistoryOutput, "Create History Output", stepName, null);
        }
        private void EditHistoryOutput(string stepName, string historyOutputName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmHistoryOutput);
            ShowForm(_frmHistoryOutput, "Edit History Output", stepName, historyOutputName);
        }
        private void DuplicateHistoryOutputs(string stepName, string[] historyOutputNames)
        {
            _controller.DuplicateHistoryOutputsCommand(stepName, historyOutputNames);
        }
        private void PropagateHistoryOutput(string stepName, string historyOutputName)
        {
            bool exists = false;
            string[] nextStepNames = _controller.Model.StepCollection.GetNextStepNames(stepName);
            //
            foreach (var nextStepName in nextStepNames)
            {
                if (_controller.Model.StepCollection.GetStep(nextStepName).HistoryOutputs.ContainsKey(historyOutputName))
                {
                    exists = true;
                    break;
                }
            }
            //
            bool propagate = true;
            if (exists)
            {
                if (MessageBoxes.ShowWarningQuestionOKCancel("OK to overwrite the existing history output " + historyOutputName
                                                     + "?") == DialogResult.Cancel) propagate = false;
            }
            if (propagate) _controller.PropagateHistoryOutputCommand(stepName, historyOutputName);
        }
        private void DeleteHistoryOutputs(string stepName, string[] historyOutputNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected history outputs from step " + stepName + "?"
                                                 + Environment.NewLine + historyOutputNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveHistoryOutputsCommand(stepName, historyOutputNames);
            }
        }
        #endregion  ################################################################################################################

        #region Field output menu  #################################################################################################
        private void tsmiCreateFieldOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), CreateFieldOutput);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditFieldOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndEditFieldOutput);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateFieldOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndDuplicateFieldOutputs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiPropagateFieldOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndPropagateFieldOutput);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteFieldOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndDeleteFieldOutputs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void SelectAndEditFieldOutput(string stepName)
        {
            SelectOneEntityInStep("Field outputs", _controller.GetAllFieldOutputs(stepName), stepName, EditFieldOutput);
        }
        private void SelectAndDuplicateFieldOutputs(string stepName)
        {
            SelectMultipleEntitiesInStep("Field outputs", _controller.GetAllFieldOutputs(stepName),
                                         stepName, DuplicateFieldOutputs);
        }
        private void SelectAndPropagateFieldOutput(string stepName)
        {
            SelectOneEntityInStep("Field outputs", _controller.GetAllFieldOutputs(stepName), stepName, PropagateFieldOutput);
        }
        private void SelectAndDeleteFieldOutputs(string stepName)
        {
            SelectMultipleEntitiesInStep("Field outputs", _controller.GetAllFieldOutputs(stepName), stepName, DeleteFieldOutputs);
        }
        //
        private void CreateFieldOutput(string stepName)
        {
            if (_controller.Model.Mesh == null) return;
            //
            ShowForm(_frmFieldOutput, "Create Field Output", stepName, null);
        }
        private void EditFieldOutput(string stepName, string fieldOutputName)
        {
            ShowForm(_frmFieldOutput, "Edit Field Output", stepName, fieldOutputName);
        }
        private void DuplicateFieldOutputs(string stepName, string[] fieldOutputNames)
        {
            _controller.DuplicateFieldOutputsCommand(stepName, fieldOutputNames);
        }
        private void PropagateFieldOutput(string stepName, string fieldOutputName)
        {
            bool exists = false;
            string[] nextStepNames = _controller.Model.StepCollection.GetNextStepNames(stepName);
            //
            foreach (var nextStepName in nextStepNames)
            {
                if (_controller.Model.StepCollection.GetStep(nextStepName).FieldOutputs.ContainsKey(fieldOutputName))
                {
                    exists = true;
                    break;
                }
            }
            //
            bool propagate = true;
            if (exists)
            {
                if (MessageBoxes.ShowWarningQuestionOKCancel("OK to overwrite the existing filed output " + fieldOutputName
                                                     + "?") == DialogResult.Cancel) propagate = false;
            }
            if (propagate) _controller.PropagateFieldOutputCommand(stepName, fieldOutputName);
        }
        private void DeleteFieldOutputs(string stepName, string[] fieldOutputNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected field outputs from step " + stepName + "?"
                                                 + Environment.NewLine + fieldOutputNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveFieldOutputsCommand(stepName, fieldOutputNames);
            }
        }

        #endregion  ################################################################################################################

        #region Boundary condition menu  ###########################################################################################
        internal void tsmiCreateBC_Click(object sender, EventArgs e)
        {
            try
            {
                int selectedIndex = -1;
                if (e is EventArgs<int> ea) selectedIndex = ea.Value;
                _frmBoundaryCondition.SetPreselectListViewItem(selectedIndex);
                //
                SelectOneEntity("Steps", _controller.GetAllSteps(), CreateBoundaryCondition);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditBC_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndEditBoundaryCondition);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateBC_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndDuplicateBoundaryCondition);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiPropagateBC_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndPropagateBoundaryCondition);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiPreviewBC_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndPreviewBoundaryCondition);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }

        private void tsmiHideBC_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndHideBoundaryConditions);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowBC_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndShowBoundaryConditions);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteBC_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndDeleteBoundaryCondition);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void SelectAndEditBoundaryCondition(string stepName)
        {
            SelectOneEntityInStep("Boundary conditions", _controller.GetStepBoundaryConditions(stepName), stepName,
                                  EditBoundaryCondition);
        }
        private void SelectAndDuplicateBoundaryCondition(string stepName)
        {
            SelectMultipleEntitiesInStep("Boundary conditions", _controller.GetStepBoundaryConditions(stepName),
                                         stepName, DuplicateBoundaryConditions);
        }
        private void SelectAndPropagateBoundaryCondition(string stepName)
        {
            SelectOneEntityInStep("Boundary conditions", _controller.GetStepBoundaryConditions(stepName), stepName,
                                  PropagateBoundaryCondition);
        }
        private void SelectAndPreviewBoundaryCondition(string stepName)
        {
            SelectOneEntityInStep("Boundary conditions", _controller.GetStepBoundaryConditions(stepName),
                                  stepName, PreviewBoundaryCondition);
        }
        private void SelectAndHideBoundaryConditions(string stepName)
        {
            SelectMultipleEntitiesInStep("Boundary conditions", _controller.GetStepBoundaryConditions(stepName),
                                         stepName, HideBoundaryConditions);
        }
        private void SelectAndShowBoundaryConditions(string stepName)
        {
            SelectMultipleEntitiesInStep("Boundary conditions", _controller.GetStepBoundaryConditions(stepName),
                                         stepName, ShowBoundaryConditions);
        }
        private void SelectAndDeleteBoundaryCondition(string stepName)
        {
            SelectMultipleEntitiesInStep("Boundary conditions", _controller.GetStepBoundaryConditions(stepName),
                                         stepName, DeleteBoundaryConditions);
        }
        //
        private void CreateBoundaryCondition(string stepName)
        {
            if (_controller.Model.Mesh == null) return;
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, true, _frmBoundaryCondition);
            ShowForm(_frmBoundaryCondition, "Create Boundary Condition", stepName, null);
        }
        private void EditBoundaryCondition(string stepName, string boundaryConditionName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, true, _frmBoundaryCondition);
            ShowForm(_frmBoundaryCondition, "Edit Boundary Condition", stepName, boundaryConditionName);
        }
        private void DuplicateBoundaryConditions(string stepName, string[] boundaryConditionNames)
        {
            _controller.DuplicateBoundaryConditionsCommand(stepName, boundaryConditionNames);
        }
        private void PropagateBoundaryCondition(string stepName, string boundaryConditionName)
        {
            bool exists = false;
            string[] nextStepNames = _controller.Model.StepCollection.GetNextStepNames(stepName);
            //
            foreach (var nextStepName in nextStepNames)
            {
                if (_controller.Model.StepCollection.GetStep(nextStepName).BoundaryConditions.ContainsKey(boundaryConditionName))
                {
                    exists = true;
                    break;
                }
            }
            //
            bool propagate = true;
            if (exists)
            {
                if (MessageBoxes.ShowWarningQuestionOKCancel("OK to overwrite the existing boundary condition " + boundaryConditionName
                                                     + "?") == DialogResult.Cancel) propagate = false;
            }
            if (propagate) _controller.PropagateBoundaryConditionCommand(stepName, boundaryConditionName);
        }
        private void PreviewBoundaryCondition(string stepName, string boundaryConditionName)
        {
            _controller.PreviewBoundaryCondition(stepName, boundaryConditionName);
            //
            if (_controller.CurrentResult != null && _controller.CurrentResult.Mesh != null)
            {
                SetResultNames();
                // Reset the previous step and increment
                SetAllStepAndIncrementIds();
                // Set last increment
                SetDefaultStepAndIncrementIds();
                // Show the selection in the results tree
                SelectFirstComponentOfFirstFieldOutput();
            }
            // Set the representation which also calls Draw
            _controller.ViewResultsType = ViewResultsTypeEnum.ColorContours;  // Draw
            //
            SetMenuAndToolStripVisibility();
        }
        private void HideBoundaryConditions(string stepName, string[] boundaryConditionNames)
        {
            _controller.HideBoundaryConditionCommand(stepName, boundaryConditionNames);
        }
        private void ShowBoundaryConditions(string stepName, string[] boundaryConditionNames)
        {
            _controller.ShowBoundaryConditionCommand(stepName, boundaryConditionNames);
        }
        private void ShowOnlyBoundaryConditions(string stepName, string[] boundaryConditionNames)
        {
            HashSet<string> allNames =
                new HashSet<string>(_controller.Model.StepCollection.GetStep(stepName).BoundaryConditions.Keys);
            allNames.ExceptWith(boundaryConditionNames);
            _controller.HideBoundaryConditionCommand(stepName, allNames.ToArray());
            _controller.ShowBoundaryConditionCommand(stepName, boundaryConditionNames);
        }
        private void DeleteBoundaryConditions(string stepName, string[] boundaryConditionNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected boundary conditions from step " + stepName + "?"
                                                 + Environment.NewLine + boundaryConditionNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveBoundaryConditionsCommand(stepName, boundaryConditionNames);
            }
        }

        #endregion  ################################################################################################################

        #region Load menu  #########################################################################################################
        internal void tsmiCreateLoad_Click(object sender, EventArgs e)
        {
            try
            {
                int selectedIndex = -1;
                if (e is EventArgs<int> ea) selectedIndex = ea.Value;
                _frmLoad.SetPreselectListViewItem(selectedIndex);
                //
                SelectOneEntity("Steps", _controller.GetAllSteps(), CreateLoad);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditLoad_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndEditLoad);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateLoad_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndDuplicateLoads);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiPropagateLoad_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndPropagateLoad);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiPreviewLoad_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndPreviewLoad);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideLoad_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndHideLoads);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowLoad_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndShowLoads);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteLoad_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndDeleteLoads);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void SelectAndEditLoad(string stepName)
        {
            SelectOneEntityInStep("Loads", _controller.GetStepLoads(stepName), stepName, EditLoad);
        }
        private void SelectAndDuplicateLoads(string stepName)
        {
            SelectMultipleEntitiesInStep("Loads", _controller.GetStepLoads(stepName), stepName, DuplicateLoads);
        }
        private void SelectAndPropagateLoad(string stepName)
        {
            SelectOneEntityInStep("Loads", _controller.GetStepLoads(stepName), stepName, PropagateLoad);
        }
        private void SelectAndPreviewLoad(string stepName)
        {
            SelectOneEntityInStep("Loads", _controller.GetStepLoads(stepName), stepName, PreviewLoad);
        }
        private void SelectAndHideLoads(string stepName)
        {
            SelectMultipleEntitiesInStep("Loads", _controller.GetStepLoads(stepName), stepName, HideLoads);
        }
        private void SelectAndShowLoads(string stepName)
        {
            SelectMultipleEntitiesInStep("Loads", _controller.GetStepLoads(stepName), stepName, ShowLoads);
        }
        private void SelectAndDeleteLoads(string stepName)
        {
            SelectMultipleEntitiesInStep("Loads", _controller.GetStepLoads(stepName), stepName, DeleteLoads);
        }
        //
        private void CreateLoad(string stepName)
        {
            if (_controller.Model.Mesh == null) return;
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmLoad);
            //
            SinglePointDataEditor.ParentForm = _frmLoad;
            SinglePointDataEditor.Controller = _controller;
            //
            ShowForm(_frmLoad, "Create Load", stepName, null);
        }
        private void EditLoad(string stepName, string loadName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmLoad);
            //
            SinglePointDataEditor.ParentForm = _frmLoad;
            SinglePointDataEditor.Controller = _controller;
            //
            ShowForm(_frmLoad, "Edit Load", stepName, loadName);
        }
        private void DuplicateLoads(string stepName, string[] loadNames)
        {
            _controller.DuplicateLoadsCommand(stepName, loadNames);
        }
        private void PropagateLoad(string stepName, string loadName)
        {
            bool exists = false;
            string[] nextStepNames = _controller.Model.StepCollection.GetNextStepNames(stepName);
            //
            foreach (var nextStepName in nextStepNames)
            {
                if (_controller.Model.StepCollection.GetStep(nextStepName).Loads.ContainsKey(loadName))
                {
                    exists = true;
                    break;
                }
            }
            //
            bool propagate = true;
            if (exists)
            {
                if (MessageBoxes.ShowWarningQuestionOKCancel("OK to overwrite the existing load " + loadName
                                                     + "?") == DialogResult.Cancel) propagate = false;
            }
            if (propagate) _controller.PropagateLoadCommand(stepName, loadName);
        }
        private void PreviewLoad(string stepName, string loadName)
        {
            _controller.PreviewLoad(stepName, loadName);
            //
            if (_controller.CurrentResult != null && _controller.CurrentResult.Mesh != null)
            {
                SetResultNames();
                // Reset the previous step and increment
                SetAllStepAndIncrementIds();
                // Set last increment
                SetDefaultStepAndIncrementIds();
                // Show the selection in the results tree
                SelectFirstComponentOfFirstFieldOutput();
            }
            // Set the representation which also calls Draw
            _controller.ViewResultsType = ViewResultsTypeEnum.ColorContours;  // Draw
            //
            SetMenuAndToolStripVisibility();
        }
        private void HideLoads(string stepName, string[] loadNames)
        {
            _controller.HideLoadsCommand(stepName, loadNames);
        }
        private void ShowLoads(string stepName, string[] loadNames)
        {
            _controller.ShowLoadsCommand(stepName, loadNames);
        }
        private void ShowOnlyLoads(string stepName, string[] loadNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.Model.StepCollection.GetStep(stepName).Loads.Keys);
            allNames.ExceptWith(loadNames);
            _controller.HideLoadsCommand(stepName, allNames.ToArray());
            _controller.ShowLoadsCommand(stepName, loadNames);
        }
        private void DeleteLoads(string stepName, string[] loadNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected loads from step " + stepName + "?"
                                                 + Environment.NewLine + loadNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveLoadsCommand(stepName, loadNames);
            }
        }

        #endregion  ################################################################################################################

        #region Defined field menu #################################################################################################
        private void tsmiCreateDefinedField_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), CreateDefinedField);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditDefinedField_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndEditDefinedField);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateDefinedField_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndDuplicateDefinedFields);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiPropagateDefinedField_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndPropagateDefinedField);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiPreviewDefinedField_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndPreviewDefinedField);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideDefinedField_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndHideDefinedFields);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowDefinedField_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndShowDefinedFields);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteDefinedField_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Steps", _controller.GetAllSteps(), SelectAndDeleteDefinedFields);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void SelectAndEditDefinedField(string stepName)
        {
            SelectOneEntityInStep("Defined fields", _controller.GetStepDefinedFields(stepName), stepName, EditDefinedField);
        }
        private void SelectAndDuplicateDefinedFields(string stepName)
        {
            SelectMultipleEntitiesInStep("Defined fields", _controller.GetStepDefinedFields(stepName),
                                         stepName, DuplicateDefinedFields);
        }
        private void SelectAndPropagateDefinedField(string stepName)
        {
            SelectOneEntityInStep("Defined fields", _controller.GetStepDefinedFields(stepName), stepName, PropagateDefinedField);
        }
        private void SelectAndPreviewDefinedField(string stepName)
        {
            SelectOneEntityInStep("Defined fields", _controller.GetStepDefinedFields(stepName), stepName, PreviewDefinedField);
        }
        private void SelectAndHideDefinedFields(string stepName)
        {
            SelectMultipleEntitiesInStep("Defined fields", _controller.GetStepDefinedFields(stepName),
                                         stepName, HideDefinedFields);
        }
        private void SelectAndShowDefinedFields(string stepName)
        {
            SelectMultipleEntitiesInStep("Defined fields", _controller.GetStepDefinedFields(stepName),
                                         stepName, ShowDefinedFields);
        }
        private void SelectAndDeleteDefinedFields(string stepName)
        {
            SelectMultipleEntitiesInStep("Defined fields", _controller.GetStepDefinedFields(stepName),
                                         stepName, DeleteDefinedFields);
        }
        //
        private void CreateDefinedField(string stepName)
        {
            if (_controller.Model.Mesh == null) return;
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmDefinedField);
            ShowForm(_frmDefinedField, "Create Defined Field", stepName, null);
        }
        private void EditDefinedField(string stepName, string definedFieldName)
        {
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmDefinedField);
            ShowForm(_frmDefinedField, "Edit Defined Field", stepName, definedFieldName);
        }
        private void DuplicateDefinedFields(string stepName, string[] definedFieldNames)
        {
            _controller.DuplicateDefinedFieldsForStepCommand(stepName, definedFieldNames);
        }
        private void PropagateDefinedField(string stepName, string definedFieldName)
        {
            bool exists = false;
            string[] nextStepNames = _controller.Model.StepCollection.GetNextStepNames(stepName);
            //
            foreach (var nextStepName in nextStepNames)
            {
                if (_controller.Model.StepCollection.GetStep(nextStepName).DefinedFields.ContainsKey(definedFieldName))
                {
                    exists = true;
                    break;
                }
            }
            //
            bool propagate = true;
            if (exists)
            {
                if (MessageBoxes.ShowWarningQuestionOKCancel("OK to overwrite the existing defined field " + definedFieldName
                                                     + "?") == DialogResult.Cancel) propagate = false;
            }
            if (propagate) _controller.PropagateDefinedFieldCommand(stepName, definedFieldName);
        }
        private void PreviewDefinedField(string stepName, string definedFieldName)
        {
            _controller.PreviewDefinedField(stepName, definedFieldName);
            //
            if (_controller.CurrentResult != null && _controller.CurrentResult.Mesh != null)
            {
                SetResultNames();
                // Reset the previous step and increment
                SetAllStepAndIncrementIds();
                // Set last increment
                SetDefaultStepAndIncrementIds();
                // Show the selection in the results tree
                SelectFirstComponentOfFirstFieldOutput();
            }
            // Set the representation which also calls Draw
            _controller.ViewResultsType = ViewResultsTypeEnum.ColorContours;  // Draw
            //
            SetMenuAndToolStripVisibility();
        }
        private void HideDefinedFields(string stepName, string[] definedFieldNames)
        {
            _controller.HideDefinedFieldsCommand(stepName, definedFieldNames);
        }
        private void ShowDefinedFields(string stepName, string[] definedFieldNames)
        {
            _controller.ShowDefinedFieldsCommand(stepName, definedFieldNames);
        }
        private void ShowOnlyDefinedFields(string stepName, string[] definedFieldNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.Model.StepCollection.GetStep(stepName).DefinedFields.Keys);
            allNames.ExceptWith(definedFieldNames);
            _controller.HideDefinedFieldsCommand(stepName, allNames.ToArray());
            _controller.ShowDefinedFieldsCommand(stepName, definedFieldNames);
        }
        private void DeleteDefinedFields(string stepName, string[] definedFieldNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected defined fields from step " + stepName + "?"
                                                 + Environment.NewLine + definedFieldNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveDefinedFieldsForStepCommand(stepName, definedFieldNames);
            }
        }
        
        #endregion  ################################################################################################################

        #region Tools ##############################################################################################################
        private void tsmiSettings_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_frmSettings.Visible)
                {
                    CloseAllForms();
                    SetFormLocation(_frmSettings);
                    _frmSettings.PrepareForm(_controller);
                    _frmSettings.Show();
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
           
        }
        private void tsmiEditParameters_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model != null)
                {
                    using (FrmParametersEditor fpe = new FrmParametersEditor(_controller))
                    {
                        fpe.Icon = Icon;
                        fpe.Owner = this;
                        CloseAllForms();
                        SetFormLocation(fpe);
                        fpe.ShowDialog(this);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiExportParameters_Click(object sender, EventArgs e)
        {
            try
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "PrePoMax parameters|*.pmp";
                    saveFileDialog.FileName = "Parameters";
                    //
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _controller.ExportParametersCommand(saveFileDialog.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiImportParameters_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "PrePoMax parameters|*.pmp";
                    openFileDialog.FileName = "";
                    //
                    if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _controller.ImportParametersCommand(openFileDialog.FileName);
                        //
                        //tsmiEditParameters_Click(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiQuery_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_frmQuery.Visible)
                {
                    ClearSelection();
                    //
                    CloseAllForms();
                    SetFormLocation(_frmQuery);
                    _frmQuery.PrepareForm(_controller);
                    _frmQuery.Show();
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiFind_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_frmFind.Visible)
                {
                    ClearSelection();
                    //
                    CloseAllForms();
                    SetFormLocation(_frmFind);
                    _frmFind.PrepareForm(_controller);
                    _frmFind.Show();
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void ShowColorBarSettings()
        {
            if (_controller.AnnotateWithColor == AnnotateWithColorEnum.FaceOrientation ||
                _controller.AnnotateWithColor == AnnotateWithColorEnum.Parts ||
                _controller.AnnotateWithColor == AnnotateWithColorEnum.Materials ||
                _controller.AnnotateWithColor == AnnotateWithColorEnum.Sections ||
                _controller.AnnotateWithColor == AnnotateWithColorEnum.SectionThicknesses)
            {
                _frmSettings.SetSettingsToShow(Globals.ColorSettingsName);
            }
            else
            {
                _frmSettings.SetSettingsToShow(Globals.PreSettingsName);
            }
            tsmiSettings_Click(null, null);
        }
        private void ShowAnnotationSettings()
        {
            _frmSettings.SetSettingsToShow(Globals.AnnotationSettingsName);
            tsmiSettings_Click(null, null);
        }
        private void ShowLegendSettings()
        {
            _frmSettings.SetSettingsToShow(Globals.LegendSettingsName);
            tsmiSettings_Click(null, null);
        }
        private void ShowStatusBlockSettings()
        {
            _frmSettings.SetSettingsToShow(Globals.StatusBlockSettingsName);
            tsmiSettings_Click(null, null);
        }
        // Annotations
        private void StartEditArrowAnnotation(string name, Rectangle rectangle)
        {
            if (AnnotationContainer.IsAnnotationNameReserved(name)) return;
            //
            AnnotationBase annotation = _controller.Annotations.GetCurrentAnnotation(name);
            string text = annotation.GetAnnotationText();
            rectangle.Offset(-4, 4);
            //
            Point vtkLocation = this.PointToClient(_vtk.PointToScreen(_vtk.Location));
            Point location = new Point(vtkLocation.X + rectangle.X,
                                       vtkLocation.Y + (_vtk.Height - rectangle.Y - rectangle.Height));
            Rectangle vtkArea = new Rectangle(vtkLocation, _vtk.Size);
            //
            aeAnnotationTextEditor.Location = location;
            aeAnnotationTextEditor.Size = rectangle.Size;
            aeAnnotationTextEditor.MinSize = rectangle.Size;
            aeAnnotationTextEditor.ParentArea = vtkArea;
            aeAnnotationTextEditor.Text = text;
            aeAnnotationTextEditor.BringToFront();
            aeAnnotationTextEditor.Visible = true;
            aeAnnotationTextEditor.Tag = annotation;
            //
            _vtk.SelectBy = vtkSelectBy.Widget;
            //_vtk.DisableInteractor = true;
        }
        private void EndEditArrowAnnotation()
        {
            if (aeAnnotationTextEditor.Visible)
            {
                AnnotationBase annotation = (AnnotationBase)aeAnnotationTextEditor.Tag;
                string nonOverriddenText = annotation.GetNotOverriddenAnnotationText();
                //
                nonOverriddenText = nonOverriddenText.Replace("\r\n", "\n");
                string newText = aeAnnotationTextEditor.Text.Replace("\r\n", "\n");
                //
                if (newText.Length > 0 && newText != nonOverriddenText)
                    annotation.OverriddenText = aeAnnotationTextEditor.Text;
                else
                    annotation.OverriddenText = null;
                //
                aeAnnotationTextEditor.Visible = false;
                //
                //_vtk.DisableInteractor = false;
                _vtk.SelectBy = vtkSelectBy.Default;
                //
                _controller.Annotations.DrawAnnotations();  // redraw in both cases
            }
        }
        public override void LeftMouseDownOnForm(Control sender)
        {
            if (aeAnnotationTextEditor.Visible && !aeAnnotationTextEditor.IsOrContainsControl(sender))
                EndEditArrowAnnotation();
        }
        public void AnnotationPicked(MouseEventArgs e, Keys modifierKeys, string annotationName, Rectangle annotationRectangle)
        {
            if (e.Button == MouseButtons.Left && e.Clicks == 2)
            {
                StartEditArrowAnnotation(annotationName, annotationRectangle);
            }
            else if (e.Button == MouseButtons.Right)
            {
                tsmiDeleteAnnotation.Tag = new object[] { annotationName, annotationRectangle };
                //
                bool reserved = AnnotationContainer.IsAnnotationNameReserved(annotationName);
                tsmiEditAnnotation.Enabled = !reserved;
                tsmiResetAnnotation.Enabled = !reserved;
                tsmiDeleteAnnotation.Enabled = !reserved;
                cmsAnnotation.Show(_vtk, new Point(e.X, _vtk.Height-  e.Y));
            }
        }
        private void tsmiEditAnnotation_Click(object sender, EventArgs e)
        {
            try
            {
                object[] tag = (object[])tsmiDeleteAnnotation.Tag;
                if (tag[0] is string annotationName && tag[1] is Rectangle annotationRectangle)
                {
                    StartEditArrowAnnotation(annotationName, annotationRectangle);
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiResetAnnotation_Click(object sender, EventArgs e)
        {
            try
            {
                object[] tag = (object[])tsmiDeleteAnnotation.Tag;
                if (tag[0] is string annotationName)
                {
                    if (AnnotationContainer.IsAnnotationNameReserved(annotationName)) return;
                    //
                    _controller.Annotations.ResetAnnotation(annotationName);
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiAnnotationSettings_Click(object sender, EventArgs e)
        {
            try
            {
                ShowAnnotationSettings();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteAnnotation_Click(object sender, EventArgs e)
        {
            try
            {
                object[] tag = (object[])tsmiDeleteAnnotation.Tag;
                if (tag[0] is string annotationName)
                {
                    if (AnnotationContainer.IsAnnotationNameReserved(annotationName)) return;
                    //
                    //if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected annotation?") == DialogResult.OK)
                    {
                        _controller.Annotations.RemoveCurrentArrowAnnotation(annotationName);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        // Settings
        private void UpdateSettings(SettingsContainer settings)
        {
            _controller.Settings = settings;
        }

        // Unit system
        private void tsslUnitSystem_Click(object sender, EventArgs e)
        {
            try
            {
                if (Debugger.IsAttached)
                {
                    if (GetCurrentView() == ViewGeometryModelResults.Geometry ||
                        GetCurrentView() == ViewGeometryModelResults.Model)
                    {
                        IfNeededSelectAndSetNewModelProperties();
                    }
                    else SelectResultsUnitSystem();
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void IfNeededSelectAndSetNewModelProperties()
        {
            try
            {
                UnitSystemType unitSystemType = _controller.Model.UnitSystem.UnitSystemType;
                ModelSpaceEnum modelSpace = _controller.Model.Properties.ModelSpace;
                // If needed
                if (modelSpace == ModelSpaceEnum.Undefined || unitSystemType == UnitSystemType.Undefined)
                {
                    // Select and set
                    if (SelectNewModelProperties(false)) SetNewModelProperties();
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private bool SelectNewModelProperties(bool cancelPossible)
        {
            DialogResult dialogResult = DialogResult.Cancel;
            //
            InvokeIfRequired(() =>
            {
                try
                {                    
                    // Disable the form during regenerate - check that the state is ready
                    if (tsslState.Text != Globals.RegeneratingText)
                    {
                        CloseAllForms();
                        SetFormLocation(_frmNewModel);
                        //
                        if (_frmNewModel.PrepareForm("", "New Model"))
                        {
                            _frmNewModel.SetCancelPossible(cancelPossible);
                            dialogResult = _frmNewModel.ShowDialog(this);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExceptionTools.Show(this, ex);
                }
            });
            //
            return dialogResult == DialogResult.OK;
        }
        private void SetNewModelProperties()
        {
            InvokeIfRequired(() =>
            {
                try
                {
                    if (_frmNewModel.ModelSpace.IsTwoD())
                    {
                        if ((_controller.Model.Geometry != null && !_controller.Model.Geometry.BoundingBox.Is2D())
                            || (_controller.Model.Mesh != null && !_controller.Model.Mesh.BoundingBox.Is2D()))
                            throw new CaeException("Use of the 2D model space is not possible. The geometry or the mesh " +
                                                   "do not contain 2D geometry in x-y plane.");
                    }
                    _controller.SetNewModelPropertiesCommand(_frmNewModel.ModelSpace, _frmNewModel.ModelWorkflow,
                                                             _frmNewModel.UnitSystem.UnitSystemType);
                }
                catch (Exception ex)
                {
                    ExceptionTools.Show(this, ex);
                }
            });
        }
        public void SelectResultsUnitSystem(string parameters = null)
        {
            InvokeIfRequired(() =>
            {
                try
                {
                    // Disable unit system selection during regenerate - check that the state is ready
                    if (tsslState.Text != Globals.RegeneratingText)
                    {
                        UnitSystemType unitSystemType = _controller.CurrentResult.UnitSystem.UnitSystemType;
                        //
                        if (unitSystemType == UnitSystemType.Undefined)
                        {
                            UnitSystemType unitSystemTypeFromParameters;
                            if (Enum.TryParse(parameters, out unitSystemTypeFromParameters))
                            {
                                _controller.SetResultsUnitSystem(unitSystemTypeFromParameters);
                            }
                            else
                            {
                                CloseAllForms();
                                SetFormLocation(_frmNewModel);
                                //
                                if (_frmNewModel.PrepareForm("", "Results"))
                                {
                                    if (_frmNewModel.ShowDialog(this) == DialogResult.OK)
                                    {
                                        _controller.SetResultsUnitSystem(_frmNewModel.UnitSystem.UnitSystemType);
                                    }
                                    else throw new NotSupportedException();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExceptionTools.Show(this, ex);
                }
            });
        }
        public void UpdateUnitSystem(UnitSystem unitSystem)
        {
            tsslUnitSystem.Text = "Unit system: " + unitSystem.UnitSystemType.GetDescription();
            //
            SetScaleWidgetUnit(unitSystem);
        }
        #endregion  ################################################################################################################

        #region Analysis menu  #####################################################################################################
        private void tsmiCreateAnalysis_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Model.Mesh == null) return;
                ShowForm(_frmAnalysis, "Create Analysis", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditAnalysis_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Analyses", _controller.GetAllJobs(), EditAnalysis);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateAnalysis_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Analyses", _controller.GetAllJobs(), DuplicateAnalyses);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        internal void tsmiRunAnalysis_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Analyses", _controller.GetAllJobs(), RunAnalysis);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiCheckModel_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Analyses", _controller.GetAllJobs(), CheckModel);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiMonitorAnalysis_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Analyses", _controller.GetAllJobs(), MonitorAnalysis);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        internal void tsmiResultsAnalysis_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Analyses", _controller.GetAllJobs(), ResultsAnalysis);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiKillAnalysis_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Analyses", _controller.GetAllJobs(), KillAnalysis);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteAnalysis_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Analyses", _controller.GetAllJobs(), DeleteAnalyses);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditAnalysis(string jobName)
        {
            ShowForm(_frmAnalysis, "Edit Analysis", jobName);
        }
        private void DuplicateAnalyses(string[] jobNames)
        {
            _controller.DuplicateJobsCommand(jobNames);
        }
        private void RunAnalysis(string jobName)
        {
            RunAnalysis(jobName, false);
        }
        private void CheckModel(string jobName)
        {
            RunAnalysis(jobName, true);
        }
        private void RunAnalysis(string jobName, bool onlyCheckModel)
        {
            // Check validity
            if (CheckValidity())
            {
                string workDirectory = _controller.Settings.GetWorkDirectory();
                //
                if (workDirectory == null || !Directory.Exists(workDirectory))
                    throw new Exception("The work directory of the analysis does not exist.");
                //
                AnalysisJob job = _controller.GetJob(jobName);
                if (job.JobStatus != JobStatus.Running)
                {
                    string inputFileName = _controller.GetCalculiXInpFileName(jobName);
                    if (File.Exists(inputFileName))
                    {
                        if (MessageBoxes.ShowWarningQuestionOKCancel("Overwrite existing analysis files?") != DialogResult.OK) return;
                    }
                    //
                    if (_controller.PrepareAndRunJobCommand(jobName, onlyCheckModel)) MonitorAnalysis(jobName);
                }
                else MessageBoxes.ShowError("The analysis is already running or is in queue.");
            }
        }
        private void EditFatigueSetup(string setupPage)
        {
            try
            {
                ApplyCurrentFatigueProjectContext(false);
                using (FrmFatigueSetup frm = new FrmFatigueSetup(_controller, _fatigueSetup, setupPage))
                {
                    if (frm.ShowDialog(this) == DialogResult.OK)
                    {
                        _controller.ModelChanged = true;
                        RefreshFatigueTree();
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        public void SetFatigueSetup(FatigueSetup setup)
        {
            _fatigueSetup = setup ?? FatigueSetup.CreateDefault();
            RefreshFatigueTree();
        }
        private void RefreshFatigueTree()
        {
            if (_modelTree == null || _fatigueSetup == null) return;
            List<string> loads = new List<string>();
            if (_fatigueSetup.Loads != null)
            {
                if (_fatigueSetup.Loads.GeneratedLoads != null && _fatigueSetup.Loads.GeneratedLoads.Count > 0)
                {
                    foreach (FatigueGeneratedLoadSetup load in _fatigueSetup.Loads.GeneratedLoads)
                    {
                        string name = string.IsNullOrWhiteSpace(load.Name) ? "Load" : load.Name;
                        loads.Add(name + " - Step " + Math.Max(1, load.ResultStep).ToString(CultureInfo.InvariantCulture));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(_fatigueSetup.Loads.LoadsFileName) &&
                         _fatigueSetup.Loads.Components != null)
                {
                    foreach (FatigueLoadComponentSetup component in _fatigueSetup.Loads.Components)
                    {
                        if (!string.IsNullOrWhiteSpace(component.Name)) loads.Add(component.Name);
                    }
                }
            }
            string backend = _fatigueSetup.SnCurve != null ? _fatigueSetup.SnCurve.CalculationBackend : "MATLAB";
            _modelTree.UpdateFatigueSetup(loads.ToArray(), backend);
        }
        private void RunFatigueAnalysis()
        {
            bool stateSet = false;
            try
            {
                stateSet = SetStateWorking(FatigueCalculationStateText);
                if (!stateSet) return;
                ApplyCurrentFatigueProjectContext(true);
                string[] setupErrors = _fatigueSetup.Validate();
                if (setupErrors.Length > 0)
                {
                    MessageBoxes.ShowError("Fatigue setup is incomplete:" + Environment.NewLine +
                                           string.Join(Environment.NewLine, setupErrors));
                    FinishFatigueProgress();
                    EditFatigueSetup("Model");
                    return;
                }
                //
                string matlabExecutable = GetMatlabExecutable();
                string packageRunnerFileName = GetFatiguePackageRunnerFileName();
                string pythonExecutable = GetPythonExecutable();
                string pythonRunnerFileName = GetPythonFatigueRunnerFileName();
                FatiguePackageInfo package = CreateFatigueExportPackage(matlabExecutable, packageRunnerFileName,
                                                                        pythonExecutable, pythonRunnerFileName);
                //
                RunFatiguePackage(package, matlabExecutable, packageRunnerFileName, pythonExecutable, pythonRunnerFileName);
            }
            catch (Exception ex)
            {
                if (stateSet) FinishFatigueProgress();
                ExceptionTools.Show(this, ex);
            }
        }
        private sealed class FatiguePackageInfo
        {
            public string PackageDirectory { get; set; }
            public string HandoffFileName { get; set; }
            public string PackageManifestFileName { get; set; }
            public string RunScriptFileName { get; set; }
        }
        private FatiguePackageInfo CreateFatigueExportPackage(string matlabExecutable, string packageRunnerFileName,
                                                              string pythonExecutable, string pythonRunnerFileName)
        {
            string workDirectory = GetFatigueWorkDirectory();
            string baseName = GetFatigueBaseFileName();
            string packageDirectory = Path.Combine(workDirectory, "Fatigue",
                                                   baseName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss",
                                                                                         CultureInfo.InvariantCulture));
            string modelDirectory = Path.Combine(packageDirectory, "model");
            string resultDirectory = Path.Combine(packageDirectory, "results");
            string loadsDirectory = Path.Combine(packageDirectory, "loads");
            Directory.CreateDirectory(modelDirectory);
            Directory.CreateDirectory(resultDirectory);
            Directory.CreateDirectory(loadsDirectory);
            //
            string modelFileName = Path.Combine(modelDirectory, baseName + ".inp");
            _controller.ExportToCalculix(modelFileName);
            //
            string fullResultFileName = Path.Combine(resultDirectory, baseName + "_displayed_all_steps.frd");
            ExportFatigueDisplayedResult(fullResultFileName, true, GetCurrentFatigueResultStep());
            //
            Dictionary<int, string> resultStepFiles = ExportFatigueResultStepFiles(resultDirectory, baseName);
            string primaryResultFileName;
            if (!resultStepFiles.TryGetValue(GetCurrentFatigueResultStep(), out primaryResultFileName))
                primaryResultFileName = fullResultFileName;
            //
            string loadsFileName = CreateFatiguePackageLoadsFile(loadsDirectory);
            //
            string originalInpFileName = _fatigueSetup.Model.InpFileName;
            string originalFrdFileName = _fatigueSetup.Model.FrdFileName;
            string originalLoadsFileName = _fatigueSetup.Loads.LoadsFileName;
            string[] originalUnitLoadFrdFileNames = _fatigueSetup.Model.UnitLoadCases.Select(ulc => ulc.FrdFileName).ToArray();
            string handoffFileName = Path.Combine(packageDirectory, "prepomax_fatigue_handoff.json");
            try
            {
                _fatigueSetup.Model.InpFileName = MakePackageRelative(packageDirectory, modelFileName);
                _fatigueSetup.Model.FrdFileName = MakePackageRelative(packageDirectory, primaryResultFileName);
                _fatigueSetup.Loads.LoadsFileName = MakePackageRelative(packageDirectory, loadsFileName);
                foreach (var ulc in _fatigueSetup.Model.UnitLoadCases)
                {
                    if (string.IsNullOrWhiteSpace(ulc.FrdFileName))
                    {
                        string stepFileName;
                        ulc.FrdFileName = resultStepFiles.TryGetValue(ulc.ResultStep, out stepFileName)
                                          ? MakePackageRelative(packageDirectory, stepFileName)
                                          : MakePackageRelative(packageDirectory, primaryResultFileName);
                    }
                    else
                    {
                        string copiedUlcFile = Path.Combine(resultDirectory, Path.GetFileName(ulc.FrdFileName));
                        CopyFileIfDifferent(ulc.FrdFileName, copiedUlcFile);
                        ulc.FrdFileName = MakePackageRelative(packageDirectory, copiedUlcFile);
                    }
                }
                _fatigueSetup.WriteHandoff(handoffFileName);
                ValidateFatigueHandoffJson(handoffFileName);
            }
            finally
            {
                _fatigueSetup.Model.InpFileName = originalInpFileName;
                _fatigueSetup.Model.FrdFileName = originalFrdFileName;
                _fatigueSetup.Loads.LoadsFileName = originalLoadsFileName;
                for (int i = 0; i < _fatigueSetup.Model.UnitLoadCases.Count && i < originalUnitLoadFrdFileNames.Length; i++)
                    _fatigueSetup.Model.UnitLoadCases[i].FrdFileName = originalUnitLoadFrdFileNames[i];
            }
            //
            string packageManifestFileName = Path.Combine(packageDirectory, "package_manifest.json");
            WriteFatiguePackageManifest(packageManifestFileName, packageDirectory, modelFileName,
                                        fullResultFileName, resultStepFiles, loadsFileName, handoffFileName);
            WriteFatiguePackageReadme(Path.Combine(packageDirectory, "README.txt"), packageDirectory);
            string runScriptFileName = Path.Combine(packageDirectory, "run_prepomax_fatigue_package.bat");
            WriteFatigueRunScript(runScriptFileName, matlabExecutable, packageRunnerFileName,
                                  pythonExecutable, pythonRunnerFileName, handoffFileName);
            //
            FatiguePackageInfo package = new FatiguePackageInfo();
            package.PackageDirectory = packageDirectory;
            package.HandoffFileName = handoffFileName;
            package.PackageManifestFileName = packageManifestFileName;
            package.RunScriptFileName = runScriptFileName;
            return package;
        }
        private static void ValidateFatigueHandoffJson(string fileName)
        {
            try
            {
                byte[] json = File.ReadAllBytes(fileName);
                using (XmlDictionaryReader reader =
                       JsonReaderWriterFactory.CreateJsonReader(json, XmlDictionaryReaderQuotas.Max))
                {
                    while (reader.Read()) { }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("The generated fatigue handoff JSON is invalid: " + ex.Message, ex);
            }
        }
        private string CreateFatiguePackageLoadsFile(string loadsDirectory)
        {
            if (!string.IsNullOrWhiteSpace(_fatigueSetup.Loads.LoadsFileName) &&
                File.Exists(_fatigueSetup.Loads.LoadsFileName))
            {
                string loadsFileName = Path.Combine(loadsDirectory, Path.GetFileName(_fatigueSetup.Loads.LoadsFileName));
                CopyFileIfDifferent(_fatigueSetup.Loads.LoadsFileName, loadsFileName);
                return loadsFileName;
            }
            //
            if (_fatigueSetup.Loads.GeneratedLoads == null || _fatigueSetup.Loads.GeneratedLoads.Count == 0)
                throw new Exception("Fatigue loads are not defined.");
            //
            string generatedLoadsFileName = Path.Combine(loadsDirectory, "generated_loads.csv");
            WriteGeneratedFatigueLoads(generatedLoadsFileName);
            return generatedLoadsFileName;
        }
        private void WriteGeneratedFatigueLoads(string fileName)
        {
            List<FatigueGeneratedLoadSetup> generatedLoads = _fatigueSetup.Loads.GeneratedLoads;
            double duration = 0;
            double samplesPerSecond = 0;
            foreach (FatigueGeneratedLoadSetup load in generatedLoads)
            {
                duration = Math.Max(duration, load.Cycles / Math.Max(1e-12, load.Frequency));
                samplesPerSecond = Math.Max(samplesPerSecond, load.SamplesPerSecond);
            }
            int pointCount = Math.Max(2, (int)Math.Ceiling(duration * samplesPerSecond));
            pointCount = Math.Min(pointCount, 1000000);
            //
            List<string> lines = new List<string>();
            lines.Add("n," + generatedLoads[0].Repetitions.ToString(CultureInfo.InvariantCulture));
            lines.Add("Time," + string.Join(",", generatedLoads.Select(load => load.Name)));
            lines.Add("s," + string.Join(",", generatedLoads.Select(load => load.Unit)));
            for (int i = 0; i < pointCount; i++)
            {
                double t = pointCount == 1 ? 0 : duration * i / (pointCount - 1);
                List<string> row = new List<string>();
                row.Add(t.ToString("R", CultureInfo.InvariantCulture));
                foreach (FatigueGeneratedLoadSetup load in generatedLoads)
                    row.Add(EvaluateGeneratedFatigueLoad(load, t).ToString("R", CultureInfo.InvariantCulture));
                lines.Add(string.Join(",", row));
            }
            File.WriteAllLines(fileName, lines.ToArray());
        }
        private static double EvaluateGeneratedFatigueLoad(FatigueGeneratedLoadSetup load, double time)
        {
            double phaseCycles = load.Phase / 360.0;
            double phaseRadians = load.Phase * Math.PI / 180.0;
            double baseValue;
            if (load.Waveform == "Triangular")
            {
                double x = load.Frequency * time + phaseCycles;
                baseValue = 2 * Math.Abs(2 * (x - Math.Floor(x + 0.5))) - 1;
            }
            else if (load.Waveform == "Square")
            {
                baseValue = Math.Sin(2 * Math.PI * load.Frequency * time + phaseRadians) >= 0 ? 1 : -1;
            }
            else
            {
                baseValue = Math.Sin(2 * Math.PI * load.Frequency * time + phaseRadians);
            }
            return load.Mean + load.Amplitude * baseValue;
        }
        private void ApplyCurrentFatigueProjectContext(bool requireMatch)
        {
            if (!_controller.ResultsInitialized ||
                _controller.CurrentResult == null ||
                _controller.CurrentResult.Mesh == null)
            {
                if (requireMatch) throw new Exception("Open results from the PMX project before running fatigue.");
                return;
            }
            if (!_controller.ModelInitialized || _controller.Model == null || _controller.Model.Mesh == null)
            {
                if (requireMatch) throw new Exception("The PMX project must contain the FE model before running fatigue.");
                return;
            }
            string[] visiblePartNames = _controller.CurrentResult.Mesh.GetVisibleAndActivePartNames();
            if (visiblePartNames == null || visiblePartNames.Length == 0)
            {
                if (requireMatch) throw new Exception("At least one result part must be visible for fatigue.");
                return;
            }
            //
            string frdFileName = _controller.CurrentResult.FileName;
            string resultName = !string.IsNullOrWhiteSpace(frdFileName)
                                ? Path.GetFileNameWithoutExtension(frdFileName)
                                : _controller.AllResults.GetCurrentResultName();
            if (string.IsNullOrWhiteSpace(resultName)) resultName = GetFatigueBaseFileName();
            _fatigueSetup.ApplyProjectContext(frdFileName, _controller.GetAllJobs(),
                                              job => ResolveFatigueInputFileName(job, frdFileName),
                                              visiblePartNames, resultName);
        }
        private string GetFatigueBaseFileName()
        {
            string name = _fatigueSetup != null && !string.IsNullOrWhiteSpace(_fatigueSetup.Model.AnalysisName)
                          ? _fatigueSetup.Model.AnalysisName
                          : null;
            if (string.IsNullOrWhiteSpace(name) && _controller.CurrentResult != null &&
                !string.IsNullOrWhiteSpace(_controller.CurrentResult.FileName))
                name = Path.GetFileNameWithoutExtension(_controller.CurrentResult.FileName);
            if (string.IsNullOrWhiteSpace(name)) name = _controller.AllResults.GetCurrentResultName();
            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(_controller.OpenedFileName))
                name = Path.GetFileNameWithoutExtension(_controller.OpenedFileName);
            if (string.IsNullOrWhiteSpace(name)) name = "fatigue_model";
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name;
        }
        private void ExportFatigueDisplayedResult(string fileName)
        {
            ExportFatigueDisplayedResult(fileName, true, GetCurrentFatigueResultStep());
        }
        private void ExportFatigueDisplayedResult(string fileName, bool allStepsAndIncrements, int stepId)
        {
            HashSet<string> existingFieldNames = new HashSet<string>(_controller.GetResultFieldOutputNames(),
                                                                      StringComparer.OrdinalIgnoreCase);
            string[] supportedFieldNames = new string[]
            {
                FOFieldNames.Stress,
                FOFieldNames.Disp,
                FOFieldNames.ToStrain,
                FOFieldNames.Forc,
                FOFieldNames.Contact
            };
            string[] fieldNames = supportedFieldNames.Where(name => existingFieldNames.Contains(name)).ToArray();
            if (fieldNames.Length == 0) throw new Exception("None of the supported result fields exist in the current results.");
            //
            FieldData currentField = _controller.CurrentFieldData;
            int stepIncrementId = currentField != null && currentField.StepId == stepId
                                  ? currentField.StepIncrementId
                                  : _controller.GetResultStepIncrementIds(stepId).LastOrDefault();
            if (stepIncrementId <= 0) stepIncrementId = _controller.GetResultStepIncrementIds(stepId).FirstOrDefault();
            _controller.ExportDisplayedResultsToFrd(fileName, fieldNames, allStepsAndIncrements, stepId, stepIncrementId);
        }
        private int GetCurrentFatigueResultStep()
        {
            FieldData currentField = _controller.CurrentFieldData;
            if (currentField != null && currentField.StepId > 0) return currentField.StepId;
            int[] stepIds = _controller.GetResultStepIDs();
            return stepIds != null && stepIds.Length > 0 ? stepIds[0] : 1;
        }
        private Dictionary<int, string> ExportFatigueResultStepFiles(string resultDirectory, string baseName)
        {
            HashSet<int> requestedSteps = new HashSet<int>();
            if (_fatigueSetup.Loads != null && _fatigueSetup.Loads.LoadCases != null)
            {
                foreach (var loadCase in _fatigueSetup.Loads.LoadCases)
                    if (loadCase.ResultStep > 0) requestedSteps.Add(loadCase.ResultStep);
            }
            if (requestedSteps.Count == 0) requestedSteps.Add(GetCurrentFatigueResultStep());
            //
            int[] availableSteps = _controller.GetResultStepIDs();
            Dictionary<int, string> resultStepFiles = new Dictionary<int, string>();
            foreach (int stepId in requestedSteps.OrderBy(id => id))
            {
                if (availableSteps != null && availableSteps.Length > 0 && !availableSteps.Contains(stepId)) continue;
                string fileName = Path.Combine(resultDirectory,
                                               baseName + "_displayed_step_" +
                                               stepId.ToString(CultureInfo.InvariantCulture) + ".frd");
                ExportFatigueDisplayedResult(fileName, false, stepId);
                resultStepFiles[stepId] = fileName;
            }
            return resultStepFiles;
        }
        private static string MakePackageRelative(string packageDirectory, string fileName)
        {
            Uri packageUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(packageDirectory)));
            Uri fileUri = new Uri(Path.GetFullPath(fileName));
            string relative = Uri.UnescapeDataString(packageUri.MakeRelativeUri(fileUri).ToString());
            return relative.Replace('/', Path.DirectorySeparatorChar);
        }
        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) return path;
            return path + Path.DirectorySeparatorChar;
        }
        private void WriteFatiguePackageManifest(string fileName, string packageDirectory, string modelFileName,
                                                 string fullResultFileName, Dictionary<int, string> resultStepFiles,
                                                 string loadsFileName, string handoffFileName)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            WriteJsonValue(sb, "schema", "prepomax-fatigue-package-v1", 1, true);
            WriteJsonValue(sb, "createdUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), 1, true);
            WriteJsonValue(sb, "analysisName", _fatigueSetup.Model.AnalysisName, 1, true);
            WriteJsonValue(sb, "handoff", MakePackageRelative(packageDirectory, handoffFileName), 1, true);
            WriteJsonValue(sb, "modelInp", MakePackageRelative(packageDirectory, modelFileName), 1, true);
            WriteJsonValue(sb, "displayedFrdAllSteps", MakePackageRelative(packageDirectory, fullResultFileName), 1, true);
            WriteJsonValue(sb, "loadsFile", MakePackageRelative(packageDirectory, loadsFileName), 1, true);
            IndentJson(sb, 1); sb.AppendLine("\"resultStepFrdFiles\": [");
            int index = 0;
            foreach (var entry in resultStepFiles.OrderBy(item => item.Key))
            {
                IndentJson(sb, 2); sb.AppendLine("{");
                WriteJsonValue(sb, "step", entry.Key, 3, true);
                WriteJsonValue(sb, "file", MakePackageRelative(packageDirectory, entry.Value), 3, false);
                IndentJson(sb, 2); sb.Append(index == resultStepFiles.Count - 1 ? "}" : "},"); sb.AppendLine();
                index++;
            }
            IndentJson(sb, 1); sb.AppendLine("],");
            IndentJson(sb, 1); sb.AppendLine("\"loadCaseStepMap\": [");
            for (int i = 0; i < _fatigueSetup.Loads.LoadCases.Count; i++)
            {
                FatigueLoadCaseSetup loadCase = _fatigueSetup.Loads.LoadCases[i];
                IndentJson(sb, 2); sb.AppendLine("{");
                WriteJsonValue(sb, "name", loadCase.Name, 3, true);
                WriteJsonValue(sb, "resultStep", loadCase.ResultStep, 3, false);
                IndentJson(sb, 2); sb.Append(i == _fatigueSetup.Loads.LoadCases.Count - 1 ? "}" : "},"); sb.AppendLine();
            }
            IndentJson(sb, 1); sb.AppendLine("]");
            sb.AppendLine("}");
            File.WriteAllText(fileName, sb.ToString());
        }
        private void WriteFatiguePackageReadme(string fileName, string packageDirectory)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("PrePoMax FATLAB fatigue package");
            sb.AppendLine();
            sb.AppendLine("This folder contains a reproducible fatigue-analysis handoff exported from PrePoMax.");
            sb.AppendLine("Files:");
            sb.AppendLine("- prepomax_fatigue_handoff.json: calculation parameters and FATLAB-facing paths.");
            sb.AppendLine("- package_manifest.json: package inventory and result-step mapping.");
            sb.AppendLine("- model/: exported CalculiX INP mesh/model.");
            sb.AppendLine("- results/: displayed FRD result export, plus one FRD per referenced result step.");
            sb.AppendLine("- loads/: FATLAB load-time series file.");
            sb.AppendLine();
            sb.AppendLine("The per-step FRD files avoid the interactive FATLAB step-selection prompt in read_frd_result_file.m.");
            sb.AppendLine("Run run_prepomax_fatigue_package.bat to recalculate the package with the selected backend.");
            sb.AppendLine("The Python CUDA backend writes prepomax_fatigue_results.frd for direct PrePoMax visualization.");
            File.WriteAllText(fileName, sb.ToString());
        }
        private void WriteFatigueRunScript(string fileName, string matlabExecutable, string packageRunnerFileName,
                                           string pythonExecutable, string pythonRunnerFileName,
                                           string handoffFileName)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal");
            sb.AppendLine("cd /d \"%~dp0\"");
            sb.AppendLine("set PREPOMAX_FATIGUE_HANDOFF=%~dp0" + Path.GetFileName(handoffFileName));
            if (IsPythonFatigueBackend())
            {
                if (!string.IsNullOrWhiteSpace(pythonExecutable) && !string.IsNullOrWhiteSpace(pythonRunnerFileName))
                    sb.AppendLine("\"" + pythonExecutable + "\" \"" + pythonRunnerFileName + "\" \"%PREPOMAX_FATIGUE_HANDOFF%\"");
                else
                {
                    sb.AppendLine("echo Python or prepomax_fatigue_cuda_runner.py was not found when this package was created.");
                    sb.AppendLine("echo Set PYTHON_EXE to python.exe and make sure Python/prepomax_fatigue_cuda_runner.py is available.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(matlabExecutable) && !string.IsNullOrWhiteSpace(packageRunnerFileName))
            {
                string command = "addpath('" + ToMatlabString(Path.GetDirectoryName(packageRunnerFileName)) + "'); " +
                                 "run_prepomax_fatigue_package('" + ToMatlabString(handoffFileName) + "');";
                sb.AppendLine("\"" + matlabExecutable + "\" -batch \"" + command + "\"");
            }
            else
            {
                sb.AppendLine("echo MATLAB or run_prepomax_fatigue_package.m was not found when this package was created.");
                sb.AppendLine("echo Set MATLAB_EXE to matlab.exe and make sure FATLAB-nonlinear is available, then run:");
                sb.AppendLine("echo matlab -batch \"addpath('path_to_FATLAB-nonlinear'); run_prepomax_fatigue_package('%PREPOMAX_FATIGUE_HANDOFF%')\"");
            }
            File.WriteAllText(fileName, sb.ToString());
        }
        private void RunFatiguePackage(FatiguePackageInfo package, string matlabExecutable, string packageRunnerFileName,
                                       string pythonExecutable, string pythonRunnerFileName)
        {
            if (IsPythonFatigueBackend()) RunPythonFatiguePackage(package, pythonExecutable, pythonRunnerFileName);
            else RunMatlabFatiguePackage(package, matlabExecutable, packageRunnerFileName);
        }
        private void RunMatlabFatiguePackage(FatiguePackageInfo package, string matlabExecutable, string packageRunnerFileName)
        {
            WriteDataToOutput("Fatigue package exported: " + package.PackageDirectory);
            if (string.IsNullOrWhiteSpace(packageRunnerFileName) || !File.Exists(packageRunnerFileName))
            {
                MessageBoxes.ShowError("The PrePoMax FATLAB package runner was not found. Expected run_prepomax_fatigue_package.m in the FATLAB-nonlinear folder." +
                                       Environment.NewLine + "The export package was created at:" + Environment.NewLine +
                                       package.PackageDirectory);
                FinishFatigueProgress();
                return;
            }
            if (string.IsNullOrWhiteSpace(matlabExecutable))
            {
                MessageBoxes.ShowError("MATLAB executable was not found. Set the MATLAB_EXE environment variable or add matlab.exe to PATH." +
                                       Environment.NewLine + "The export package was created at:" + Environment.NewLine +
                                       package.PackageDirectory);
                FinishFatigueProgress();
                return;
            }
            //
            string command = "addpath('" + ToMatlabString(Path.GetDirectoryName(packageRunnerFileName)) + "'); " +
                             "run_prepomax_fatigue_package('" + ToMatlabString(package.HandoffFileName) + "');";
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = matlabExecutable;
            psi.Arguments = "-batch \"" + command.Replace("\"", "\\\"") + "\"";
            psi.WorkingDirectory = package.PackageDirectory;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            //
            Task.Run(() =>
            {
                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo = psi;
                        process.OutputDataReceived += (sender, args) => WriteFatigueProcessOutput(args.Data);
                        process.ErrorDataReceived += (sender, args) => WriteFatigueProcessOutput(args.Data);
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        WriteFatigueProcessOutput("MATLAB fatigue calculation started. Package folder: " + package.PackageDirectory);
                        process.WaitForExit();
                        WriteFatigueProcessOutput("MATLAB fatigue calculation finished with exit code " +
                                                  process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".");
                        FinishFatigueProgress();
                    }
                }
                catch (Exception ex)
                {
                    WriteFatigueProcessOutput("MATLAB fatigue calculation failed to start: " + ex.Message);
                    FinishFatigueProgress();
                }
            });
        }
        private void RunPythonFatiguePackage(FatiguePackageInfo package, string pythonExecutable, string pythonRunnerFileName)
        {
            WriteDataToOutput("Fatigue package exported: " + package.PackageDirectory);
            if (string.IsNullOrWhiteSpace(pythonRunnerFileName) || !File.Exists(pythonRunnerFileName))
            {
                MessageBoxes.ShowError("The PrePoMax Python fatigue runner was not found. Expected prepomax_fatigue_cuda_runner.py in the Python folder." +
                                       Environment.NewLine + "The export package was created at:" + Environment.NewLine +
                                       package.PackageDirectory);
                FinishFatigueProgress();
                return;
            }
            if (string.IsNullOrWhiteSpace(pythonExecutable))
            {
                MessageBoxes.ShowError("Python executable was not found. Set the PYTHON_EXE environment variable or add python.exe to PATH." +
                                       Environment.NewLine + "The export package was created at:" + Environment.NewLine +
                                       package.PackageDirectory);
                FinishFatigueProgress();
                return;
            }
            //
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = pythonExecutable;
            psi.Arguments = "\"" + pythonRunnerFileName + "\" \"" + package.HandoffFileName + "\"";
            psi.WorkingDirectory = package.PackageDirectory;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            //
            Task.Run(() =>
            {
                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo = psi;
                        process.OutputDataReceived += (sender, args) => WriteFatigueProcessOutput(args.Data);
                        process.ErrorDataReceived += (sender, args) => WriteFatigueProcessOutput(args.Data);
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        WriteFatigueProcessOutput("Python fatigue calculation started. Package folder: " + package.PackageDirectory);
                        process.WaitForExit();
                        WriteFatigueProcessOutput("Python fatigue calculation finished with exit code " +
                                                  process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".");
                        FinishFatigueProgress();
                        if (process.ExitCode == 0)
                        {
                            string resultFileName = Path.Combine(package.PackageDirectory, "prepomax_fatigue_results.frd");
                            BeginInvoke(new Action<string>(OpenFatigueResultFileIfExists), resultFileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteFatigueProcessOutput("Python fatigue calculation failed to start: " + ex.Message);
                    FinishFatigueProgress();
                }
            });
        }
        private async void OpenFatigueResultFileIfExists(string resultFileName)
        {
            if (!File.Exists(resultFileName))
            {
                WriteDataToOutput("Fatigue FRD result file was not created: " + resultFileName);
                return;
            }
            WriteDataToOutput("Opening fatigue FRD result file: " + resultFileName);
            await OpenAsync(resultFileName, _controller.Open, false, null, Globals.FromFileOpenMenu);
        }
        private void WriteFatigueProcessOutput(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            if (InvokeRequired) BeginInvoke(new Action<string>(WriteFatigueProcessOutput), line);
            else
            {
                if (TryApplyFatigueProgressLine(line)) return;
                WriteDataToOutput(line);
            }
        }
        private bool TryApplyFatigueProgressLine(string line)
        {
            const string prefix = "FATIGUE_PROGRESS:";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string payload = line.Substring(prefix.Length);
            int separator = payload.IndexOf(':');
            string percentText = separator >= 0 ? payload.Substring(0, separator) : payload;
            string message = separator >= 0 ? payload.Substring(separator + 1) : FatigueCalculationStateText;
            double percent;
            if (!double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out percent)) return true;
            SetFatigueProgress(percent, message);
            return true;
        }
        private bool IsPythonFatigueBackend()
        {
            return _fatigueSetup != null &&
                   _fatigueSetup.SnCurve != null &&
                   string.Equals(_fatigueSetup.SnCurve.CalculationBackend, "Python CUDA",
                                 StringComparison.OrdinalIgnoreCase);
        }
        private static string ToMatlabString(string value)
        {
            return (value ?? "").Replace("'", "''");
        }
        private static void WriteJsonValue(StringBuilder sb, string name, string value, int indent, bool comma)
        {
            IndentJson(sb, indent);
            sb.Append('"').Append(JsonEscape(name)).Append("\": \"").Append(JsonEscape(value ?? "")).Append('"');
            if (comma) sb.Append(',');
            sb.AppendLine();
        }
        private static void WriteJsonValue(StringBuilder sb, string name, int value, int indent, bool comma)
        {
            IndentJson(sb, indent);
            sb.Append('"').Append(JsonEscape(name)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma) sb.Append(',');
            sb.AppendLine();
        }
        private static void IndentJson(StringBuilder sb, int indent)
        {
            sb.Append(' ', indent * 2);
        }
        private static string JsonEscape(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
        private string ResolveFatigueInputFileName(AnalysisJob job, string frdFileName)
        {
            if (job != null)
            {
                if (!string.IsNullOrWhiteSpace(job.InputFileName)) return job.InputFileName;
                if (!string.IsNullOrWhiteSpace(job.WorkDirectory))
                    return Path.Combine(job.WorkDirectory, job.Name + ".inp");
            }
            //
            if (!string.IsNullOrWhiteSpace(frdFileName))
            {
                string resultDirectory = Path.GetDirectoryName(frdFileName);
                string resultName = Path.GetFileNameWithoutExtension(frdFileName);
                if (!string.IsNullOrWhiteSpace(resultDirectory) && !string.IsNullOrWhiteSpace(resultName))
                    return Path.Combine(resultDirectory, resultName + ".inp");
            }
            //
            string workDirectory = _controller.Settings.GetWorkDirectory();
            if (job != null && !string.IsNullOrWhiteSpace(workDirectory))
                return Path.Combine(workDirectory, job.Name + ".inp");
            //
            return null;
        }
        private string GetFatigueWorkDirectory()
        {
            string workDirectory = _controller.Settings.GetWorkDirectory();
            if (!string.IsNullOrWhiteSpace(workDirectory) && Directory.Exists(workDirectory)) return workDirectory;
            //
            if (!string.IsNullOrWhiteSpace(_fatigueSetup.Model.FrdFileName))
            {
                string resultDirectory = Path.GetDirectoryName(_fatigueSetup.Model.FrdFileName);
                if (!string.IsNullOrWhiteSpace(resultDirectory) && Directory.Exists(resultDirectory)) return resultDirectory;
            }
            //
            if (!string.IsNullOrWhiteSpace(_controller.OpenedFileName))
            {
                string projectDirectory = Path.GetDirectoryName(_controller.OpenedFileName);
                if (!string.IsNullOrWhiteSpace(projectDirectory) && Directory.Exists(projectDirectory)) return projectDirectory;
            }
            //
            throw new Exception("A fatigue working directory could not be resolved. Define a work directory or open a saved project/result file.");
        }
        private void CopyFileIfDifferent(string source, string destination)
        {
            if (string.IsNullOrWhiteSpace(source)) throw new Exception("Source file is not defined.");
            if (!File.Exists(source)) throw new Exception("The file does not exist: " + source);
            //
            string sourceFull = Path.GetFullPath(source);
            string destinationFull = Path.GetFullPath(destination);
            if (!string.Equals(sourceFull, destinationFull, StringComparison.OrdinalIgnoreCase))
                File.Copy(sourceFull, destinationFull, true);
        }
        private string GetFatiguePackageRunnerFileName()
        {
            string[] candidates = new string[]
            {
                Path.Combine(Application.StartupPath, "run_prepomax_fatigue_package.m"),
                Path.Combine(Application.StartupPath, "FATLAB-nonlinear", "run_prepomax_fatigue_package.m"),
                Path.Combine(Environment.CurrentDirectory, "FATLAB-nonlinear", "run_prepomax_fatigue_package.m"),
                Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "..", "..", "..",
                                             "FATLAB-nonlinear", "run_prepomax_fatigue_package.m"))
            };
            //
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
        private string GetPythonFatigueRunnerFileName()
        {
            string[] candidates = new string[]
            {
                Path.Combine(Application.StartupPath, "prepomax_fatigue_cuda_runner.py"),
                Path.Combine(Application.StartupPath, "Python", "prepomax_fatigue_cuda_runner.py"),
                Path.Combine(Environment.CurrentDirectory, "Python", "prepomax_fatigue_cuda_runner.py"),
                Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "..", "..", "..",
                                             "Python", "prepomax_fatigue_cuda_runner.py"))
            };
            //
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
        private string GetMatlabExecutable()
        {
            string matlabExe = Environment.GetEnvironmentVariable("MATLAB_EXE");
            if (!string.IsNullOrWhiteSpace(matlabExe) && File.Exists(matlabExe)) return matlabExe;
            //
            string pathCandidate = FindExecutableOnPath("matlab.exe");
            if (pathCandidate != null) return pathCandidate;
            //
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                string matlabRoot = Path.Combine(programFiles, "MATLAB");
                if (Directory.Exists(matlabRoot))
                {
                    foreach (string versionDirectory in Directory.GetDirectories(matlabRoot)
                                                                 .OrderByDescending(dir => dir))
                    {
                        string candidate = Path.Combine(versionDirectory, "bin", "matlab.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            //
            return null;
        }
        private string GetPythonExecutable()
        {
            string pythonExe = Environment.GetEnvironmentVariable("PYTHON_EXE");
            if (!string.IsNullOrWhiteSpace(pythonExe) && File.Exists(pythonExe)) return pythonExe;
            //
            string pathCandidate = FindExecutableOnPath("python.exe");
            if (pathCandidate != null) return pathCandidate;
            pathCandidate = FindExecutableOnPath("py.exe");
            if (pathCandidate != null) return pathCandidate;
            //
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                string programsPython = Path.Combine(localAppData, "Programs", "Python");
                if (Directory.Exists(programsPython))
                {
                    foreach (string versionDirectory in Directory.GetDirectories(programsPython)
                                                                 .OrderByDescending(dir => dir))
                    {
                        string candidate = Path.Combine(versionDirectory, "python.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            return null;
        }
        private static string FindExecutableOnPath(string executableName)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path)) return null;
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory)) continue;
                try
                {
                    string candidate = Path.Combine(directory.Trim(), executableName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                }
            }
            return null;
        }
        private void MonitorAnalysis(string jobName)
        {
            try
            {
                CloseAllForms();
                SetFormLocation(_frmMonitor);
                _frmMonitor.PrepareForm(jobName);
                _frmMonitor.Show(this);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private async void ResultsAnalysis(string jobName)
        {
            await Task.Run(() => _controller.OpenResultsCommand(jobName));
            //
            RunHistoryPostprocessing();
        }
        public void RunHistoryPostprocessing()
        {
            try
            {
                CloseAllForms();
                Application.DoEvents();
                SetStateWorking(Globals.RegeneratingText);
                _modelTree.ScreenUpdating = false;
                _controller.RunHistoryPostprocessing();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(Globals.RegeneratingText);
                _modelTree.ScreenUpdating = true;
                RegenerateTree();
                //
                SetMenuAndToolStripVisibility();
                // During regeneration the tree is empty
                if (_controller.CurrentResult != null) SelectFirstComponentOfFirstFieldOutput();
                //
                SetZoomToFit(true);
            }
        }
        public async void OpenAnalysisResults(string jobName, bool asynchronous = true)
        {
            AnalysisJob job = _controller.GetJob(jobName);
            //
            bool openResults = true;
            string parameters = Globals.FromMonitorForm;
            //
            if (!_controller.BatchRegenerationMode)
            {
                if (job.JobStatus == JobStatus.Running)
                {
                    string question = $"The analysis {job.Name} is still running. Continue?";
                    if (MessageBoxes.ShowWarningQuestionOKCancel(question) == DialogResult.OK)
                    {
                        openResults = true;
                        parameters += Globals.NameSeparator + Globals.OpenRunningJobResults;
                    }
                }
                else if (!job.IsUpToDate)
                {
                    string question = $"The result file was modified after the analysis {job.Name} completed. Continue?";
                    openResults = MessageBoxes.ShowWarningQuestionOKCancel(question) == DialogResult.OK;
                }
            }
            //
            if (openResults)
            {
                string resultsFile = job.ResultsFileName;
                //
                if (asynchronous)
                    await OpenAsync(Path.Combine(job.WorkDirectory, resultsFile),
                                    _controller.Open, false, CloseMonitorWindow,
                                    parameters);
                else
                    OpenAsync(Path.Combine(job.WorkDirectory, resultsFile),
                              _controller.Open, false, CloseMonitorWindow,
                              parameters).Wait();
            }
        }
        private void CloseMonitorWindow()
        {
            // This hides the monitor window
            if (_controller.CurrentResult != null && _controller.CurrentResult.Mesh != null)
                _frmMonitor.DialogResult = DialogResult.OK;
        }
        private void KillAnalysis(string jobName)
        {
            if (_controller.GetJob(jobName).JobStatus == JobStatus.Running)
            {
                if (MessageBoxes.ShowWarningQuestionOKCancel("OK to kill selected analysis?") == DialogResult.OK)
                {
                    _controller.KillJob(jobName);
                }
            }
            else
            {
                MessageBoxes.ShowError("The analysis is not running.");
            }
        }
        private void DeleteAnalyses(string[] jobNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected analyses?" + Environment.NewLine
                                                 + jobNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveJobsCommand(jobNames);
            }
        }
        public void UpdateAnalysisProgress()
        {
            _frmMonitor.UpdateProgress();
        }
        //
        public AnalysisJob GetDefaultJob()
        {
            try
            {
                AnalysisJob analysisJob = null;
                //
                if (this.InvokeRequired)
                {
                    this.Invoke((MethodInvoker)delegate () { GetDefaultJob(out analysisJob); });
                }
                else
                {
                    GetDefaultJob(out analysisJob);
                }
                //
                return analysisJob;
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
                return null;
            }
        }
        private void GetDefaultJob(out AnalysisJob defaultJob)
        {
            _frmAnalysis.PrepareForm(null, null);
            defaultJob = _frmAnalysis.Job;
        }
        public string GetDefaultJobName()
        {
            string name = null;
            //
            if (_controller.OpenedFileName != null)
            {
                name = NamedClass.GetErrorFreeName(Path.GetFileNameWithoutExtension(_controller.OpenedFileName),
                                                   "Analysis", null);
            }
            return name;
        }

        #endregion  ################################################################################################################

        #region Results  ###########################################################################################################
        public void ViewResultHistoryOutputData(HistoryResultData historyData)
        {
            try
            {
                if (!_frmViewResultHistoryOutput.Visible)
                {
                    CloseAllForms();
                    SetFormLocation(_frmViewResultHistoryOutput);
                    //
                    string[] columnNames;
                    object[][] rowBasedData;
                    _controller.GetHistoryOutputData(historyData, out columnNames, out rowBasedData, false);
                    //
                    _frmViewResultHistoryOutput.SetData(columnNames, rowBasedData);
                    _frmViewResultHistoryOutput.Show();
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiTransformation_Click(object sender, EventArgs e)
        {
            try
            {
                SinglePointDataEditor.ParentForm = _frmTransformation;
                SinglePointDataEditor.Controller = _controller;
                //
                ShowForm(_frmTransformation, "Create Transformation", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private async void tsmiAppendResults_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Calculix result files|*.frd";
                    openFileDialog.Multiselect = false;
                    //
                    openFileDialog.FileName = "";
                    if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        foreach (var fileName in openFileDialog.FileNames)
                        {
                            // Do not use: if (CheckBeforeOpen(fileName))
                            if (File.Exists(fileName)) await OpenAsync(fileName, _controller.AppendResult);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
                _controller.ModelChanged = false;   // hide message box
                tsmiNew_Click(null, null);
            }
        }
        private async void tsmiConvertOdbToFrd_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Abaqus result files|*.odb";
                    openFileDialog.Multiselect = false;
                    //
                    openFileDialog.FileName = "";
                    if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        await Task.Run(() => _controller.ConvertOdbToFrd(openFileDialog.FileName));
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private async void tsmiFindLocalExtrema_Click(object sender, EventArgs e)
        {
            const string state = "Finding local extrema...";
            try
            {
                if (!_controller.ResultsInitialized) throw new CaeException("There are no active results.");
                if (_controller.CurrentFieldData == null) throw new CaeException("There is no active result field.");
                //
                LocalExtremaSearchParameters parameters = GetLocalExtremaSearchParameters();
                if (parameters == null) return;
                //
                SetStateWorking(state);
                await Task.Run(() => _controller.FindLocalExtrema(parameters));
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
            finally
            {
                SetStateReady(state);
            }
        }
        private LocalExtremaSearchParameters GetLocalExtremaSearchParameters()
        {
            LocalExtremaSearchParameters parameters = null;
            InvokeIfRequired(() =>
            {
                FieldData fieldData = _controller.CurrentFieldData;
                string unit = _controller.GetCurrentResultsUnitAbbreviation();
                using (FrmLocalExtremaSearch form = new FrmLocalExtremaSearch(fieldData.Name, fieldData.Component, unit))
                {
                    if (form.ShowDialog(this) == DialogResult.OK) parameters = form.Parameters;
                }
            });
            return parameters;
        }

        #endregion  ################################################################################################################

        #region Result part menu  ##################################################################################################
        private void tsmiEditResultParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Parts", _controller.GetResultParts(), EditResultPart);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiMergeResultPart_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetResultParts(), MergeResultParts, 2);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideResultParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetResultParts(), HideResultParts);
                Clear3DSelection();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowResultParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetResultParts(), ShowResultParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowOnlyResultParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetResultParts(), ShowOnlyResultParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSetColorForResultParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetResultParts(), SetColorForResultParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiResetColorForResultParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetResultParts(), ResetColorForResultParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiSetTransparencyForResultParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetResultParts(), SetTransparencyForResultParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiColorContoursOff_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetResultParts<ResultPart>(), ColorContoursOffResultPart);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiColorContoursOn_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetResultParts<ResultPart>(), ColorContoursOnResultPart);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteResultParts_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Parts", _controller.GetResultParts(), DeleteResultParts);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditResultPart(string partName)
        {
            _frmPartProperties.View = ViewGeometryModelResults.Results;
            ShowForm(_frmPartProperties, "Edit Part", partName);
        }
        private void MergeResultParts(string[] partNames)
        {
            if (_controller.AreResultPartsMergeable(partNames))
            {
                if (MessageBoxes.ShowWarningQuestionOKCancel("OK to merge selected parts?") == DialogResult.OK)
                {
                    _controller.MergeResultPartsCommand(partNames);
                }
            }
            else MessageBoxes.ShowError("Selected parts are of a different type and thus cannot be merged.");
        }
        private void HideResultParts(string[] partNames)
        {
            _controller.HideResultPartsCommand(partNames);
        }
        private void ShowResultParts(string[] partNames)
        {
            _controller.ShowResultPartsCommand(partNames);
        }
        private void ShowOnlyResultParts(string[] partNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.CurrentResult.Mesh.Parts.Keys);
            allNames.ExceptWith(partNames);
            _controller.ShowResultPartsCommand(partNames);
            _controller.HideResultPartsCommand(allNames.ToArray());
        }
        private void SetColorForResultParts(string[] partNames)
        {
            if (_controller.AllResults.CurrentResult.Mesh == null) return;
            //
            using (FrmGetColor frmGetColor = new FrmGetColor())
            {
                Color color = _controller.AllResults.CurrentResult.Mesh.Parts[partNames[0]].GetProperties().Color;
                SetFormLocation(frmGetColor);
                frmGetColor.PrepareForm("Set Part Color: " + partNames.ToShortString(), color);
                if (frmGetColor.ShowDialog(this) == DialogResult.OK)
                {
                    _controller.SetColorForResultPartsCommand(partNames, frmGetColor.Color);
                }
                SaveFormLocation(frmGetColor);
            }
        }
        private void ResetColorForResultParts(string[] partNames)
        {
            _controller.ResetColorForResultPartsCommand(partNames);
        }
        private void SetTransparencyForResultParts(string[] partNames)
        {
            if (_controller.CurrentResult == null || _controller.CurrentResult.Mesh == null) return;
            //
            using (FrmGetValue frmGetValue = new FrmGetValue(_controller))
            {
                frmGetValue.NumOfDigits = 0;
                frmGetValue.MinValue = 25;
                frmGetValue.MaxValue = 255;
                SetFormLocation(frmGetValue);
                OrderedDictionary<string, double> presetValues =
                    new OrderedDictionary<string, double>("Preset Transparency Values", StringComparer.OrdinalIgnoreCase);
                presetValues.Add("Semi-transparent", 128);
                presetValues.Add("Opaque", 255);
                string desc = "Enter the transparency between 0 and 255.\n" + "(0 - transparent; 255 - opaque)";
                frmGetValue.PrepareForm("Set Transparency: " + partNames.ToShortString(), "Transparency", desc, 128, presetValues);
                if (frmGetValue.ShowDialog(this) == DialogResult.OK)
                {
                    _controller.SetTransparencyForResultPartsCommand(partNames, (byte)frmGetValue.Value);
                }
                SaveFormLocation(frmGetValue);
            }
        }
        private void ColorContoursOffResultPart(string[] partNames)
        {
            _controller.SetColorContoursForResultPartsCommand(partNames, false);
        }
        private void ColorContoursOnResultPart(string[] partNames)
        {
            _controller.SetColorContoursForResultPartsCommand(partNames, true);
        }
        private void DeleteResultParts(string[] partNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected parts?" + Environment.NewLine + 
                                                 partNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveResultPartsCommand(partNames);
            }
        }

        #endregion  ################################################################################################################

        #region Result Reference point  ############################################################################################
        private void tsmiCreateResultReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.AllResults.CurrentResult.Mesh == null) return;
                //
                ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmReferencePoint);
                ShowForm(_frmReferencePoint, "Create Result Reference Point", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditResultReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Result Reference Points", _controller.GetAllResultReferencePoints(), EditResultReferencePoint);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateResultReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Result Reference Points", _controller.GetAllResultReferencePoints(),
                                       DuplicateResultReferencePoints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideResultReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Result Reference Points", _controller.GetAllResultReferencePoints(),
                                       HideResultReferencePoints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowResultReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Result Reference Points", _controller.GetAllResultReferencePoints(),
                                       ShowResultReferencePoints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowOnlyResultReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Result Reference Points", _controller.GetAllResultReferencePoints(),
                                       ShowOnlyResultReferencePoints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteResultReferencePoint_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Result Reference Points", _controller.GetAllResultReferencePoints(),
                                       DeleteResultReferencePoints);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditResultReferencePoint(string referencePointName)
        {
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmReferencePoint);
            ShowForm(_frmReferencePoint, "Edit Reference Point", referencePointName);
        }
        private void DuplicateResultReferencePoints(string[] referencePointNames)
        {
            _controller.DuplicateResultReferencePointsCommand(referencePointNames);
        }
        private void HideResultReferencePoints(string[] referencePointNames)
        {
            _controller.HideResultReferencePointsCommand(referencePointNames);
        }
        private void ShowResultReferencePoints(string[] referencePointNames)
        {
            _controller.ShowResultReferencePointsCommand(referencePointNames);
        }
        private void ShowOnlyResultReferencePoints(string[] referencePointNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.GetResultReferencePointNames());
            allNames.ExceptWith(referencePointNames);
            _controller.HideResultReferencePointsCommand(allNames.ToArray());
            _controller.ShowResultReferencePointsCommand(referencePointNames);
        }
        private void DeleteResultReferencePoints(string[] referencePointNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected result reference points?" + Environment.NewLine
                                                 + referencePointNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveResultReferencePointsCommand(referencePointNames);
            }
        }

        #endregion  ################################################################################################################

        #region Result Coordinate system  ##########################################################################################
        private void tsmiCreateResultCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmCoordinateSystem);
                ShowForm(_frmCoordinateSystem, "Create Result Coordinate System", null);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditResultCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Result Coordinate Systems", _controller.GetAllResultCoordinateSystems(),
                                EditResultCoordinateSystem);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDuplicateResultCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Result Coordinate Systems", _controller.GetAllResultCoordinateSystems(),
                                       DuplicateResultCoordinateSystems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiHideResultCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Result Coordinate Systems", _controller.GetAllResultCoordinateSystems(),
                                       HideResultCoordinateSystems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowResultCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Result Coordinate Systems", _controller.GetAllResultCoordinateSystems(),
                                       ShowResultCoordinateSystems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiShowOnlyResultCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Result Coordinate Systems", _controller.GetAllResultCoordinateSystems(),
                                       ShowOnlyResultCoordinateSystems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteResultCoordinateSystem_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Result Coordinate Systems", _controller.GetAllResultCoordinateSystems(),
                                       DeleteResultCoordinateSystems);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void EditResultCoordinateSystem(string coordinateSystemName)
        {
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmCoordinateSystem);
            ShowForm(_frmCoordinateSystem, "Edit Coordinate System", coordinateSystemName);
        }
        private void DuplicateResultCoordinateSystems(string[] coordinateSystemNames)
        {
            _controller.DuplicateResultCoordinateSystemsCommand(coordinateSystemNames);
        }
        private void HideResultCoordinateSystems(string[] coordinateSystemNames)
        {
            _controller.HideResultCoordinateSystemsCommand(coordinateSystemNames);
        }
        private void ShowResultCoordinateSystems(string[] coordinateSystemNames)
        {
            _controller.ShowResultCoordinateSystemsCommand(coordinateSystemNames);
        }
        private void ShowOnlyResultCoordinateSystems(string[] coordinateSystemNames)
        {
            HashSet<string> allNames = new HashSet<string>(_controller.AllResults.CurrentResult.Mesh.CoordinateSystems.Keys);
            allNames.ExceptWith(coordinateSystemNames);
            _controller.HideResultCoordinateSystemsCommand(allNames.ToArray());
            _controller.ShowResultCoordinateSystemsCommand(coordinateSystemNames);
        }
        private void DeleteResultCoordinateSystems(string[] coordinateSystemNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected result coordinate systems?" + Environment.NewLine
                                                 + coordinateSystemNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveResultCoordinateSystemsCommand(coordinateSystemNames);
            }
        }

        #endregion  ################################################################################################################

        #region Result field output  ###############################################################################################
        private void tsmiCreateResultFieldOutput_Click(object sender, EventArgs e)
        {
            try
            {
                CreateResultFieldOutput();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditResultFieldOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("Field Outputs", _controller.GetResultFieldOutputs(), EditResultFieldOutput);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteResultFieldOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("Field Outputs", _controller.GetVisibleResultFieldOutputsAsNamedItems(),
                                       DeleteResultFieldOutputs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void CreateResultFieldOutput()
        {
            if (_controller.CurrentResult == null || _controller.CurrentResult.Mesh == null) return;
            if (_controller.CurrentResult.GetAllFiledNameComponentNames().Count == 0) return;
            //
            ShowForm(_frmResultFieldOutput, "Create Field Output", null);
        }
        private void EditResultFieldOutput(string resultFieldOutputName)
        {
            ShowForm(_frmResultFieldOutput, "Edit Field Output", resultFieldOutputName);
        }
        public void DeleteResultFieldOutputs(string[] fieldOutputNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected field outputs?" + Environment.NewLine
                                                 + fieldOutputNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveResultFieldOutputsCommand(fieldOutputNames);
            }
        }
        public void DeleteResultFieldOutputComponents(string fieldOutputName, string[] componentNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected components from field output " + fieldOutputName + "?"
                                                 + Environment.NewLine + componentNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveResultFieldOutputComponentsCommand(fieldOutputName, componentNames);
            }
        }

        #endregion  ################################################################################################################

        #region Result history output  #############################################################################################
        private void tsmiCreateResultHistoryOutput_Click(object sender, EventArgs e)
        {
            try
            {
                CreateResultHistoryOutput();
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiEditResultHistoryOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectOneEntity("History Outputs", _controller.GetResultHistoryOutputs(), EditResultHistoryOutput);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiExportResultHistoryOutput_Click(object sender, EventArgs e)
        {
            try
            {
                ExportHistoryResultSetFileProperties properties = new ExportHistoryResultSetFileProperties();
                properties.HistoryOutputNames = _controller.GetHistoryResultSetNames();
                if (properties.HistoryOutputNames == null || properties.HistoryOutputNames.Length == 0)
                    MessageBoxes.ShowWarning("There are no history output items to export!");
                else
                {
                    _frmExportFileWithProperties.ExportFileProperties = properties;
                    ShowForm(_frmExportFileWithProperties, _frmExportFileWithProperties.Text, null);
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tsmiDeleteResultHistoryOutput_Click(object sender, EventArgs e)
        {
            try
            {
                SelectMultipleEntities("History Outputs", _controller.GetResultHistoryOutputsAsNamedItems(),
                                       DeleteResultHistoryOutputs);
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        private void CreateResultHistoryOutput()
        {
            if (_controller.CurrentResult == null || _controller.CurrentResult.Mesh == null) return;
            if (_controller.CurrentResult.GetAllFiledNameComponentNames().Count == 0) return;
            // Data editor
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmResultHistoryOutput);
            ShowForm(_frmResultHistoryOutput, "Create History Output", null);
        }
        private void EditResultHistoryOutput(string resultHistoryOutputName)
        {
            ItemSetDataEditor.SetForms(_frmSelectItemSet, false, _frmResultHistoryOutput);
            ShowForm(_frmResultHistoryOutput, "Edit History Output", resultHistoryOutputName);
        }
        public void DeleteResultHistoryOutputs(string[] resultHistoryOutputNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected history outputs?" + Environment.NewLine
                                                 + resultHistoryOutputNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveResultHistoryOutputsCommand(resultHistoryOutputNames);
            }
        }
        public void DeleteResultHistoryFields(string historyResultSetName, string[] historyResultFieldNames)
        {
            if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected fields from history output " + historyResultSetName + "?"
                                                 + Environment.NewLine + historyResultFieldNames.ToRows()) == DialogResult.OK)
            {
                _controller.RemoveResultHistoryFieldsCommand(historyResultSetName, historyResultFieldNames);
            }
        }
        public void DeleteResultHistoryComponents(NamedClass[] items)
        {
            Dictionary<string, List<string>> parentItemNames;
            Dictionary<string, Dictionary<string, List<string>>> parentParentItemNames =
                new Dictionary<string, Dictionary<string, List<string>>>();
            //
            foreach (var item in items)
            {
                if (item is HistoryResultData hrd)
                {
                    if (parentParentItemNames.TryGetValue(hrd.SetName, out parentItemNames))
                    {
                        parentItemNames[hrd.FieldName].Add(hrd.ComponentName);
                    }
                    else
                    {
                        parentParentItemNames.Add(hrd.SetName, new Dictionary<string, List<string>>()
                                                  { { hrd.FieldName, new List<string>() { hrd.ComponentName} } });
                    }
                }
            }
            //
            string[] itemNames;
            foreach (var parentParentEntry in parentParentItemNames)
            {
                foreach (var parentEntry in parentParentEntry.Value)
                {
                    itemNames = parentEntry.Value.ToArray();
                    if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete selected components from history field " +
                                                         parentEntry.Key + "?" + Environment.NewLine +
                                                         itemNames.ToRows()) == DialogResult.OK)
                    {
                        _controller.RemoveResultHistoryFieldsCommand(parentParentEntry.Key,
                                                                     parentEntry.Key,
                                                                     itemNames);
                    }
                }
            }
        }
        #endregion  ################################################################################################################

        #region Help menu  #########################################################################################################
        private void tsmiHomePage_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Globals.HomePage);
        }
        private void tsmiAbout_Click(object sender, EventArgs e)
        {
            using (FrmSplash frmSplash = new FrmSplash())
            {
                frmSplash.ShowHelp = true;
                frmSplash.ShowDialog(this);
            }
        }
        #endregion  ################################################################################################################

        #region Selection methods  #################################################################################################
        private void SelectOneEntity(string title, NamedClass[] entities, Action<string> OperateOnEntity)
        {
            if (entities == null || entities.Length == 0) return;
            // Only one entity exists
            if (entities.Length == 1)
            {
                OperateOnEntity(entities[0].Name);
            }
            // Multiple entities exists
            else
            {
                string[] preSelectedEntityNames = _modelTree.IntersectSelectionWithList(entities);
                //
                SetFormLocation(_frmSelectEntity);
                _frmSelectEntity.PrepareForm(title, false, entities, preSelectedEntityNames, null);
                _frmSelectEntity.OneEntitySelected = OperateOnEntity;
                _frmSelectEntity.Show();
            }
        }
        private void SelectOneEntityInStep(string title, NamedClass[] entities, string stepName,
                                           Action<string, string> OperateOnEntityInStep)
        {
            if (entities == null || entities.Length == 0) return;
            // Only one entity exists
            if (entities.Length == 1)
            {
                OperateOnEntityInStep(stepName, entities[0].Name);
            }
            // Multiple entities exists
            else
            {
                string[] preSelectedEntityNames = _modelTree.IntersectSelectionWithList(entities);
                //
                SetFormLocation(_frmSelectEntity);
                _frmSelectEntity.PrepareForm(title, false, entities, preSelectedEntityNames, stepName);
                _frmSelectEntity.OneEntitySelectedInStep = OperateOnEntityInStep;
                _frmSelectEntity.Show();
            }
        }
        private void SelectMultipleEntities(string title, NamedClass[] entities, Action<string[]> OperateOnMultipleEntities,
                                            int minNumberOfEntities = 1, int maxNumberOfEntities = int.MaxValue)
        {
            if (entities == null || entities.Length == 0 || entities.Length < minNumberOfEntities) return;
            // Only one entity exists
            if (entities.Length == 1)
            {
                if (minNumberOfEntities == 1) OperateOnMultipleEntities(entities.GetNames());
            }
            // Multiple entities exists
            else
            {
                string[] preSelectedEntityNames = _modelTree.IntersectSelectionWithList(entities);
                //
                SetFormLocation(_frmSelectEntity);
                _frmSelectEntity.PrepareForm(title, true, entities, preSelectedEntityNames, null);
                _frmSelectEntity.MultipleEntitiesSelected = OperateOnMultipleEntities;
                _frmSelectEntity.MinNumberOfEntities = minNumberOfEntities;
                _frmSelectEntity.MaxNumberOfEntities = maxNumberOfEntities;
                _frmSelectEntity.Show();
            }
        }
        private void SelectMultipleEntitiesInStep(string title, NamedClass[] entities, string stepName,
                                                  Action<string, string[]> OperateOnMultipleEntitiesInStep)
        {
            if (entities == null || entities.Length == 0) return;
            // Only one entity exists
            if (entities.Length == 1)
            {
                OperateOnMultipleEntitiesInStep(stepName, entities.GetNames());
            }
            // Multiple entities exists
            else
            {
                string[] preSelectedEntityNames = _modelTree.IntersectSelectionWithList(entities);
                //
                _frmSelectEntity.Location = new Point(Left + _formLocation.X, Top + _formLocation.Y);
                _frmSelectEntity.PrepareForm(title, true, entities, preSelectedEntityNames, stepName);
                _frmSelectEntity.MultipleEntitiesSelectedInStep = OperateOnMultipleEntitiesInStep;
                _frmSelectEntity.Show();
            }
        }

        #endregion  ################################################################################################################

        #region Mouse selection methods  ###########################################################################################
        private void VtkJs_CameraChanged(object sender, VtkJsCameraChangedEventArgs e)
        {
            _vtkJsCameraPosition = (double[])e.Position.Clone();
            _vtkJsCameraFocalPoint = (double[])e.FocalPoint.Clone();
            _vtkJsCameraViewUp = (double[])e.ViewUp.Clone();
            _vtkJsCameraParallelScale = e.ParallelScale;
            _vtkJsCameraRevision++;
            _vtkJsCameraCommandPending = false;
            if (_vtkJs != null) _vtkJs.WriteAcceptanceTrace("camera state retained");
        }

        private void VtkJs_BoxZoomEnded(object sender, VtkJsBoxZoomEndedEventArgs e)
        {
            tsbBoxZoom.Checked = false;
            bool scaleReduced = e.Applied && _vtkJsBoxZoomStartScale > 0 && HasRetainedVtkJsCamera() &&
                                _vtkJsCameraParallelScale < _vtkJsBoxZoomStartScale;
            if (_vtkJs != null) _vtkJs.WriteAcceptanceTrace("box zoom completed applied=" + e.Applied +
                                                            " scaleReduced=" + scaleReduced);
        }

        private bool HasRetainedVtkJsCamera()
        {
            return _vtkJsCameraPosition != null && _vtkJsCameraFocalPoint != null &&
                   _vtkJsCameraViewUp != null && _vtkJsCameraParallelScale > 0;
        }

        private void RetainVtkJsCamera(double[] position, double[] focalPoint, double[] viewUp,
                                       double parallelScale)
        {
            _vtkJsCameraPosition = (double[])position.Clone();
            _vtkJsCameraFocalPoint = (double[])focalPoint.Clone();
            _vtkJsCameraViewUp = (double[])viewUp.Clone();
            _vtkJsCameraParallelScale = parallelScale;
        }

        private void VtkJs_AnnotationEdited(object sender, VtkJsAnnotationEditedEventArgs e)
        {
            try
            {
                if (AnnotationContainer.IsAnnotationNameReserved(e.Name)) return;
                AnnotationBase annotation = _controller.Annotations.GetCurrentAnnotation(e.Name);
                if (annotation == null) return;
                string baseline = annotation.GetNotOverriddenAnnotationText().Replace("\r\n", "\n");
                string edited = e.Text.Replace("\r\n", "\n");
                annotation.OverriddenText = edited == baseline ? null : e.Text;
                _controller.Annotations.DrawAnnotations();
                if (_vtkJs != null) _vtkJs.WriteAcceptanceTrace("annotation edit applied name='" + e.Name + "'");
            }
            catch (Exception exception)
            {
                if (_vtkJs != null) _vtkJs.WriteAcceptanceTrace("annotation edit failed: " + exception.Message);
            }
        }

        private void VtkJs_AnnotationActionRequested(object sender, VtkJsAnnotationActionEventArgs e)
        {
            try
            {
                if (e.Action == "settings")
                {
                    ShowAnnotationSettings();
                }
                else
                {
                    if (AnnotationContainer.IsAnnotationNameReserved(e.Name)) return;
                    AnnotationBase annotation = _controller.Annotations.GetCurrentAnnotation(e.Name);
                    if (annotation == null) return;
                    if (e.Action == "reset") _controller.Annotations.ResetAnnotation(e.Name);
                    else if (e.Action == "delete") _controller.Annotations.RemoveCurrentArrowAnnotation(e.Name);
                    else return;
                }
                if (_vtkJs != null)
                    _vtkJs.WriteAcceptanceTrace("annotation action applied name='" + e.Name + "' action=" + e.Action);
            }
            catch (Exception exception)
            {
                if (_vtkJs != null) _vtkJs.WriteAcceptanceTrace("annotation action failed: " + exception.Message);
            }
        }

        private void VtkJs_ValidatedPick(object sender, VtkJsValidatedPickEventArgs e)
        {
            vtkSelectOperation operation;
            Keys modifierKeys;
            if (e.Operation == "add") { operation = vtkSelectOperation.Add; modifierKeys = Keys.Shift; }
            else if (e.Operation == "subtract") { operation = vtkSelectOperation.Subtract; modifierKeys = Keys.Control; }
            else if (e.Operation == "intersect")
            {
                operation = vtkSelectOperation.Intersect;
                modifierKeys = Keys.Shift | Keys.Control;
            }
            else { operation = vtkSelectOperation.None; modifierKeys = Keys.None; }

            try
            {
                if (_controller.SelectItem == vtkSelectItem.None)
                {
                    SelectBaseParts(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0),
                                    modifierKeys, new[] { e.ActorName });
                    _vtkJs.SetSelection("part", new int[0], new[] { e.ActorName });
                    _vtkJs.WriteAcceptanceTrace("selection applied item=TreePart actor='" + e.ActorName + "'");
                    return;
                }

                int[] ids = _controller.ApplyValidatedRenderPick(e.SourceNodeId, e.SourceElementId,
                                                                  e.TriangleSourceNodeIds, e.WorldPosition,
                                                                  e.CameraDirection, e.SelectionPrecision, operation);
                if (ids != null)
                {
                    SelectionChanged(ids);
                    _vtkJs.WriteAcceptanceTrace("selection applied item=" + _controller.SelectItem +
                                                " ids=" + string.Join(",", ids));
                    if (!string.IsNullOrWhiteSpace(
                        Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_PICK_TRIGGER")))
                        _vtkJs.TraceAcceptanceSelectionOverlay();
                }
            }
            catch (Exception exception)
            {
                WriteDataToOutput("VTK.js selection was ignored: " + exception.Message);
            }
        }

        private void VtkJs_ValidatedArea(object sender, VtkJsValidatedAreaEventArgs e)
        {
            vtkSelectOperation operation;
            Keys modifierKeys;
            if (e.Operation == "add") { operation = vtkSelectOperation.Add; modifierKeys = Keys.Shift; }
            else if (e.Operation == "subtract") { operation = vtkSelectOperation.Subtract; modifierKeys = Keys.Control; }
            else if (e.Operation == "intersect")
            {
                operation = vtkSelectOperation.Intersect;
                modifierKeys = Keys.Shift | Keys.Control;
            }
            else { operation = vtkSelectOperation.None; modifierKeys = Keys.None; }

            try
            {
                if (_controller.SelectItem == vtkSelectItem.None)
                {
                    string[] actorNames = e.LocatorValidated ? e.ActorNames :
                        e.Triangles.Select(triangle => triangle.ActorName).Distinct().ToArray();
                    SelectBaseParts(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0), modifierKeys, actorNames);
                    _vtkJs.SetSelection("part", new int[0], actorNames);
                    _vtkJs.WriteAcceptanceTrace("area selection applied item=TreePart actors=" + actorNames.Length);
                    return;
                }

                int[] ids = e.LocatorValidated
                    ? _controller.ApplyValidatedRenderFrustum(e.SourceNodeIds, e.SourceElementIds,
                                                               e.CompletelyInside, operation)
                    : _controller.ApplyValidatedRenderArea(
                        e.Triangles.Select(triangle => triangle.SourceElementId).ToArray(),
                        e.Triangles.Select(triangle => triangle.TriangleSourceNodeIds).ToArray(),
                        e.CameraDirection, e.CompletelyInside, operation);
                if (ids != null)
                {
                    SelectionChanged(ids);
                    _vtkJs.WriteAcceptanceTrace("area selection applied item=" + _controller.SelectItem +
                                                " ids=" + ids.Length + " triangles=" + e.Triangles.Length +
                                                " locator=" + e.LocatorValidated + " inside=" + e.CompletelyInside);
                    if (!string.IsNullOrWhiteSpace(
                        Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_PICK_TRIGGER")))
                        _vtkJs.TraceAcceptanceSelectionOverlay();
                }
            }
            catch (Exception exception)
            {
                WriteDataToOutput("VTK.js area selection was ignored: " + exception.Message);
            }
        }

        public void SelectPointOrArea(double[] pickedPoint, double[] selectionDirection,
                                      double[][] planeParameters, bool completelyInside,
                                      vtkSelectOperation selectOperation, string[] pickedPartNames)
        {
            //pickedPoint = null;
            //selectionDirection = null;
            ////planeParameters.DumpToFile(@"C:\temp\planeParameters");
            //completelyInside = true;
            //selectOperation = vtkSelectOperation.None;
            ////pickedPartNames.DumpToFile(@"C:\temp\partNames");
            ////
            //planeParameters = CaeGlobals.ExtensionMethods.LoadFromFile<double[][]>(@"C:\temp\planeParameters");
            //pickedPartNames = CaeGlobals.ExtensionMethods.LoadFromFile<string[]>(@"C:\temp\partNames");
            //
            PushMenuStates();
            //
            SetStateWorking(Globals.SelectionText);
            //
            _controller.SelectPointOrArea(pickedPoint, selectionDirection,
                                          planeParameters, completelyInside,
                                          selectOperation, pickedPartNames);
            //
            int[] ids = _controller.GetSelectionIds();
            // Must be here since it calls Clear which calls SelectionChanged
            if (_frmSectionView != null && _frmSectionView.Visible) _frmSectionView.PickedIds(ids);
            if (_frmFollowerView != null && _frmFollowerView.Visible) _frmFollowerView.PickedIds(ids);
            if (_frmTranslate != null && _frmTranslate.Visible) _frmTranslate.PickedIds(ids);
            if (_frmRotate != null && _frmRotate.Visible) _frmRotate.PickedIds(ids);
            if (_frmMirror != null && _frmMirror.Visible) _frmMirror.PickedIds(ids);
            if (_frmScale != null && _frmScale.Visible) _frmScale.PickedIds(ids);
            if (_frmQuery != null && _frmQuery.Visible) _frmQuery.PickedIds(ids);
            if (_frmTransformation != null && _frmTransformation.Visible) _frmTransformation.PickedIds(ids);
            //
            SelectionChanged(ids);
            //
            SetStateReady(Globals.SelectionText);
            //
            PopMenuStates();
        }
        public void SelectionChanged(int[] ids = null)
        {
            if (ids == null) ids = _controller.GetSelectionIds();
            if (_vtkJs != null)
            {
                string item;
                string[] actorNames = new string[0];
                int[][] faceNodeIds = new int[0][];
                int[][] edgeNodeIds = new int[0][];
                int[] rendererIds = ids;
                if (_controller.SelectItem == vtkSelectItem.Node) item = "node";
                else if (_controller.SelectItem == vtkSelectItem.Element) item = "element";
                else if (_controller.SelectItem == vtkSelectItem.Surface)
                {
                    item = "surface";
                    faceNodeIds = _controller.GetSurfaceCellsByFaceIds(ids, out _);
                }
                else if (_controller.SelectItem == vtkSelectItem.GeometrySurface)
                {
                    item = "surface";
                    int[] faceIds = _controller.DisplayedMesh == null
                        ? new int[0]
                        : _controller.DisplayedMesh.GetIdsFromGeometryIds(ids, vtkSelectItem.Surface, true);
                    faceNodeIds = _controller.GetSurfaceCellsByFaceIds(faceIds, out _);
                }
                else if (_controller.SelectItem == vtkSelectItem.Geometry)
                {
                    item = "geometry";
                    int[] vertexGeometryIds = ids.Where(id => FeMesh.GetTypeIdFromGeometryId(id) ==
                                                               (int)GeometryType.Vertex).ToArray();
                    int[] edgeGeometryIds = ids.Where(id => FeMesh.GetTypeIdFromGeometryId(id) ==
                                                             (int)GeometryType.Edge).ToArray();
                    int[] surfaceGeometryIds = ids.Where(id =>
                    {
                        int typeId = FeMesh.GetTypeIdFromGeometryId(id);
                        return typeId == (int)GeometryType.SolidSurface ||
                               typeId == (int)GeometryType.ShellFrontSurface ||
                               typeId == (int)GeometryType.ShellBackSurface ||
                               typeId == (int)GeometryType.ShellEdgeSurface;
                    }).ToArray();
                    if (_controller.DisplayedMesh != null)
                    {
                        rendererIds = _controller.DisplayedMesh.GetIdsFromGeometryIds(vertexGeometryIds,
                                                                                       vtkSelectItem.Node, true);
                        edgeNodeIds = _controller.DisplayedMesh.GetEdgeCellsFromGeometryEdgeIds(edgeGeometryIds);
                        int[] faceIds = _controller.DisplayedMesh.GetIdsFromGeometryIds(surfaceGeometryIds,
                                                                                        vtkSelectItem.Surface, true);
                        faceNodeIds = _controller.GetSurfaceCellsByFaceIds(faceIds, out _);
                    }
                }
                else if (_controller.SelectItem == vtkSelectItem.GeometryVertex)
                {
                    item = "node";
                    rendererIds = _controller.DisplayedMesh == null
                        ? new int[0]
                        : _controller.DisplayedMesh.GetIdsFromGeometryIds(ids, vtkSelectItem.Node, true);
                }
                else if (_controller.SelectItem == vtkSelectItem.GeometryEdge)
                {
                    item = "geometry-edge";
                    edgeNodeIds = _controller.DisplayedMesh == null
                        ? new int[0][]
                        : _controller.DisplayedMesh.GetEdgeCellsFromGeometryEdgeIds(ids);
                }
                else if (_controller.SelectItem == vtkSelectItem.Part)
                {
                    item = "part";
                    if (_controller.DisplayedMesh != null)
                        actorNames = _controller.DisplayedMesh.GetPartNamesFromPartIds(ids);
                }
                else if (_controller.SelectItem == vtkSelectItem.None) item = "none";
                else item = "unsupported";
                _vtkJs.SetSelection(item, rendererIds, actorNames, faceNodeIds, edgeNodeIds);
            }
            //
            if (_frmMeshSetupItem != null && _frmMeshSetupItem.Visible) _frmMeshSetupItem.SelectionChanged(ids);
            if (_frmSelectGeometry != null && _frmSelectGeometry.Visible) _frmSelectGeometry.SelectionChanged(ids);
            //
            if (_frmBoundaryLayer != null && _frmBoundaryLayer.Visible) _frmBoundaryLayer.SelectionChanged(ids);
            if (_frmRemeshingParameters != null && _frmRemeshingParameters.Visible) _frmRemeshingParameters.SelectionChanged(ids);
            if (_frmThickenShellMesh != null && _frmThickenShellMesh.Visible) _frmThickenShellMesh.SelectionChanged(ids);
            if (_frmSplitPartMeshUsingSurface != null && _frmSplitPartMeshUsingSurface.Visible)
                _frmSplitPartMeshUsingSurface.SelectionChanged(ids);
            if (_frmMergeCoincidentNodes != null && _frmMergeCoincidentNodes.Visible)
                _frmMergeCoincidentNodes.SelectionChanged(ids);
            if (_frmNodeSet != null && _frmNodeSet.Visible) _frmNodeSet.SelectionChanged(ids);
            if (_frmElementSet != null && _frmElementSet.Visible) _frmElementSet.SelectionChanged(ids);
            if (_frmSurface != null && _frmSurface.Visible) _frmSurface.SelectionChanged(ids);
            if (_frmReferencePoint != null && _frmReferencePoint.Visible) _frmReferencePoint.SelectionChanged(ids);
            if (_frmCoordinateSystem != null && _frmCoordinateSystem.Visible) _frmCoordinateSystem.SelectionChanged(ids);
            if (_frmSection != null && _frmSection.Visible) _frmSection.SelectionChanged(ids);
            if (_frmConstraint != null && _frmConstraint.Visible) _frmConstraint.SelectionChanged(ids);
            if (_frmContactPair != null && _frmContactPair.Visible) _frmContactPair.SelectionChanged(ids);
            if (_frmInitialCondition != null && _frmInitialCondition.Visible) _frmInitialCondition.SelectionChanged(ids);
            //
            if (_frmHistoryOutput != null && _frmHistoryOutput.Visible) _frmHistoryOutput.SelectionChanged(ids);
            if (_frmBoundaryCondition != null && _frmBoundaryCondition.Visible) _frmBoundaryCondition.SelectionChanged(ids);
            if (_frmLoad != null && _frmLoad.Visible) _frmLoad.SelectionChanged(ids);
            if (_frmDefinedField != null && _frmDefinedField.Visible) _frmDefinedField.SelectionChanged(ids);
            //
            if (_frmResultHistoryOutput != null && _frmResultHistoryOutput.Visible) _frmResultHistoryOutput.SelectionChanged(ids);
        }
        public void SetQuerySelection(bool querySelection)
        {
            InvokeIfRequired(() => _vtk.QuerySelectionInProgress = querySelection);
        }
        public bool GetQuerySelection()
        {
            return InvokeIfRequired(() => _vtk.QuerySelectionInProgress);
        }
        public void SetSelectBy(vtkSelectBy selectBy)
        {
            InvokeIfRequired(() => _vtk.SelectBy = selectBy);
        }
        public void SetSelectItem(vtkSelectItem selectItem)
        {
            InvokeIfRequired(() => _vtk.SelectItem = selectItem);
        }
        public void GetGeometryPickProperties(double[] point, out int elementId, out int[] edgeNodeIds,
                                              out int[] cellFaceNodeIds, string[] selectionPartNames = null)
        {
            elementId = -1;
            edgeNodeIds = null;
            cellFaceNodeIds = null;
            try
            {
                if (selectionPartNames != null) _vtk.SetSelectableActorsFilter(selectionPartNames);
                _vtk.GetGeometryPickProperties(point, out elementId, out edgeNodeIds, out cellFaceNodeIds);
            }
            catch { }
            finally
            {
                if (selectionPartNames != null) _vtk.SetSelectableActorsFilter(null);
            }
        }
        public double GetSelectionPrecision()
        {
            return _vtk.GetSelectionPrecision();
        }
        public void GetPointAndCellIdsInsideFrustum(double[][] planeParameters, string[] selectionPartNames,
                                                    out int[] pointIds, out int[] cellIds)
        {
            cellIds = null;
            pointIds = null;
            try
            {
                if (selectionPartNames != null) _vtk.SetSelectableActorsFilter(selectionPartNames);
                _vtk.GetPointAndCellIdsInsideFrustum(planeParameters, out pointIds, out cellIds);
            }
            catch { }
            finally
            {
                if (selectionPartNames != null) _vtk.SetSelectableActorsFilter(null);
            }
        }

        #endregion  ################################################################################################################
        //
        #region Child form methods  ################################################################################################
        private void AddFormToAllForms(Form form)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Icon = Icon;
            form.Owner = this;
            form.Move += itemForm_Move;
            form.VisibleChanged += itemForm_VisibleChanged;
            _allForms.Add(form);
        }
        private void ShowForm(IFormBase form, string text, string itemToEditName)
        {
            ShowForm(form, text, null, itemToEditName);
        }
        private void ShowForm(IFormBase form, string text, string stepName, string itemToEditName)
        {
            if (!form.Visible)
            {
                CloseAllForms();
                SetFormLocation((Form)form);
                form.Text = text;
                if (itemToEditName != null) form.Text += ": " + itemToEditName;
                if (form.PrepareForm(stepName, itemToEditName))
                {
                    ((Form)form).Show(this);
                }
                else
                {
                    if (stepName != null)
                    {
                        string itemName = text.Replace("Create ", "").Replace("Edit ", "").ToLower();
                        MessageBoxes.ShowWarning("Creating/editing of a " + itemName + " in the step: "
                                                 + stepName + " is not supported.");
                    }
                }
            }
        }
        private void SetFormLocation(Form form)
        {
            Rectangle screenBounds;
            bool intersects = false;
            Rectangle formSize = form.ClientRectangle;
            Point location = new Point(Left + _formLocation.X, Top + _formLocation.Y);
            Rectangle locationRect = new Rectangle(location, new Size(1, 1));
            //
            foreach (var screen in Screen.AllScreens)
            {
                screenBounds = screen.Bounds;
                if (screenBounds.IntersectsWith(locationRect))
                {
                    intersects = true;
                    // Size
                    if (formSize.Width > screenBounds.Width) formSize.Width = screenBounds.Width;
                    if (formSize.Height > screenBounds.Height) formSize.Height = screenBounds.Height;
                    // Location X
                    if (location.X < screenBounds.Left) location.X = screenBounds.Left;
                    else if (location.X + formSize.Width > screenBounds.Left + screenBounds.Width)
                        location.X = screenBounds.Left + screenBounds.Width - formSize.Width;
                    // Location Y
                    if (location.Y < screenBounds.Top) location.Y = screenBounds.Top;
                    else if (location.Y + formSize.Height > screenBounds.Top + screenBounds.Height)
                        location.Y = screenBounds.Top + screenBounds.Height - formSize.Height;
                }
            }
            //
            if (!intersects) location = Location;
            //
            if (form.Location != location) form.Location = location;
        }
        private void SaveFormLocation(Form form)
        {
            _formLocation.X = form.Location.X - Left;
            _formLocation.Y = form.Location.Y - Top;
        }
        public void CloseAllForms()
        {
            InvokeIfRequired(() =>
            {
                if (_allForms != null)
                {
                    foreach (var form in _allForms)
                    {
                        if (form.Visible) form.Hide();
                    }
                }
           });
        }

        #endregion  ################################################################################################################

        // Toolbars                                                                                                                 
        #region File toolbar #######################################################################################################
        private void tsbNew_Click(object sender, EventArgs e)
        {
            tsmiNew_Click(null, null);
        }
        
        private void tsbOpen_Click(object sender, EventArgs e)
        {
            tsmiOpen_Click(null, null);
        }
        private void tsbImport_Click(object sender, EventArgs e)
        {
            tsmiImportFile_Click(null, null);
        }
        private void tsbSave_Click(object sender, EventArgs e)
        {
            tsmiSave_Click(null, null);
        }
        #endregion  ################################################################################################################

        #region View toolbar  ######################################################################################################
        private void tsbFrontView_Click(object sender, EventArgs e)
        {
            tsmiFrontView_Click(null, null);
        }
        private void tsbBackView_Click(object sender, EventArgs e)
        {
            tsmiBackView_Click(null, null);
        }
        private void tsbTopView_Click(object sender, EventArgs e)
        {
            tsmiTopView_Click(null, null);
        }
        private void tsbBottomView_Click(object sender, EventArgs e)
        {
            tsmiBottomView_Click(null, null);
        }
        private void tsbLeftView_Click(object sender, EventArgs e)
        {
            tsmiLeftView_Click(null, null);
        }
        private void tsbRightView_Click(object sender, EventArgs e)
        {
            tsmiRightView_Click(null, null);
        }
        //
        private void tsbNormalView_Click(object sender, EventArgs e)
        {
            tsmiNormalView_Click(null, null);
        }
        private void tsbVerticalView_Click(object sender, EventArgs e)
        {
            tsmiVerticalView_Click(null, null);
        }
        //
        private void tsbIsometric_Click(object sender, EventArgs e)
        {
            tsmiIsometricView_Click(null, null);
        }
        //
        private void tsbZoomToFit_Click(object sender, EventArgs e)
        {
            tsmiZoomToFit_Click(null, null);
        }
        private void tsbBoxZoom_Click(object sender, EventArgs e)
        {
            tsmiBoxZoom_Click(null, null);
        }
        //
        private void tsbShowWireframeEdges_Click(object sender, EventArgs e)
        {
            tsmiShowWireframeEdges_Click(null, null);
        }
        private void tsbShowElementEdges_Click(object sender, EventArgs e)
        {
            tsmiShowElementEdges_Click(null, null);
        }
        private void tsbShowModelEdges_Click(object sender, EventArgs e)
        {
            tsmiShowModelEdges_Click(null, null);
        }
        private void tsbShowNoEdges_Click(object sender, EventArgs e)
        {
            tsmiShowNoEdges_Click(null, null);
        }
        //
        private void tsbSectionView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) TurnSectionViewOnOff();
            else if (e.Button == MouseButtons.Right) tsmiSectionView_Click(null, null);
        }
        private void tsbExplodedView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) TurnExplodedViewOnOff(true);
            else if (e.Button == MouseButtons.Right) tsmiExplodedView_Click(null, null);
        }
        private void tsbFollowerView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) tsmiFollowerView_Click(null, null);
        }
        //
        private void tsbHideAllParts_Click(object sender, EventArgs e)
        {
            tsmiHideAllParts_Click(sender, e);
        }
        private void tsbShowAllParts_Click(object sender, EventArgs e)
        {
            tsmiShowAllParts_Click(sender, e);
        }
        private void tsbInvertVisibleParts_Click(object sender, EventArgs e)
        {
            tsmiInvertVisibleParts_Click(sender, e);
        }
        //
        private void tsbQuery_Click(object sender, EventArgs e)
        {
            tsmiQuery_Click(sender, e);
        }
        private void tsbRemoveAnnotations_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.Annotations.GetCurrentAnnotationNames().Length > 0)
                {
                    if (MessageBoxes.ShowWarningQuestionOKCancel("OK to delete all current view annotations?") == DialogResult.OK)
                    {
                        _controller.Annotations.RemoveCurrentArrowAnnotations();
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        #endregion  ################################################################################################################

        #region Symbol toolbar  ####################################################################################################
        private void tscbSymbols_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressSymbolChanged)
            {
                // Only set the name here
                _controller.SetSymbolsName(tscbSymbols.SelectedItem.ToString());
            }
            else
            {
                // If BC or load from one step-1 is selected its selection requires the step-1 to be selected.
                // Changing the step symbols is not possible -> Clear selection
                _controller.ClearAllSelection();
                //
                _controller.DrawSymbols(tscbSymbols.SelectedItem.ToString(), false);
                // Save the selected index
                _selectedSymbolIndex[_controller.CurrentView] = tscbSymbols.SelectedIndex;
                //
                this.ActiveControl = null;
            }
        }
        public void SelectLastSymbolName()
        {
            InvokeIfRequired(() =>
            {
                _suppressSymbolChanged = true;
                tscbSymbols.SelectedIndex = tscbSymbols.Items.Count - 1;
                _suppressSymbolChanged = false;
            });
        }
        public void UpdateSymbolsList()
        {
            InvokeIfRequired(() =>
            {
                _suppressSymbolChanged = true;
                // Clear symbols dropdown
                tscbSymbols.Items.Clear();
                tscbSymbols.Items.Add(Globals.SymbolNone);
                tscbSymbols.SelectedIndex = 0;
                //
                if (_controller.CurrentView == ViewGeometryModelResults.Geometry) { }
                else if (_controller.CurrentView == ViewGeometryModelResults.Model)
                {
                    tscbSymbols.Items.Add(Globals.SymbolModel);
                    tscbSymbols.Items.AddRange(_controller.GetStepNames());
                }
                else if (_controller.CurrentView == ViewGeometryModelResults.Results)
                    tscbSymbols.Items.Add(Globals.SymbolResult);
                // Retrieve the selected index
                int index = int.MaxValue;
                if (_selectedSymbolIndex != null && !_selectedSymbolIndex.TryGetValue(_controller.CurrentView, out index))
                {
                    index = tscbSymbols.Items.Count - 1;
                }
                // Check the index
                index = Math.Min(index, tscbSymbols.Items.Count - 1);
                // Set the index
                tscbSymbols.SelectedIndex = index;
                //
                _suppressSymbolChanged = false;
            });
        }
        public void SelectOneNameInSymbolsNames(string name)
        {
            InvokeIfRequired(() =>
            {
                int index = -1;
                for (int i = 0; i < tscbSymbols.Items.Count; i++)
                {
                    if (tscbSymbols.Items[i].ToString() == name)
                    {
                        index = i;
                        break;
                    }
                }
                if (index != -1 && index != tscbSymbols.SelectedIndex)
                {
                    _suppressSymbolChanged = true;
                    tscbSymbols.SelectedIndex = index;
                    _selectedSymbolIndex[_controller.CurrentView] = index;
                    _suppressSymbolChanged = false;
                    //
                    _controller.DrawSymbols(tscbSymbols.SelectedItem.ToString(), false); // do not highlight!
                }
            });
        }

        #endregion  ################################################################################################################

        #region Deformation toolbar  ###############################################################################################
        private void tscbResultNames_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                string currentResultName = _controller.AllResults.GetCurrentResultName();
                string newResultName = tscbResultNames.SelectedItem.ToString();
                if (newResultName != currentResultName)
                {
                    _controller.SetCurrentResultsCommand(newResultName);
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }      
        private void tscbDeformationVariable_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                _controller.Redraw();
                this.ActiveControl = null;
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tscbDeformationType_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                // Enable scale factor text box if needed
                UpdateScaleFactorTextBoxState();
                //
                if (ScaleFactorChanged()) _controller.ClearSelectionBuffer();
                //
                _controller.Redraw();
                this.ActiveControl = null;
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tstbDeformationFactor_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (ScaleFactorChanged()) _controller.ClearSelectionBuffer();
                    //
                    _controller.Redraw();
                    this.ActiveControl = null;
                    // No beep
                    e.SuppressKeyPress = true;
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tstbDeformationFactor_EnabledChanged(object sender, EventArgs e)
        {
            if (tstbDeformationFactor.Enabled) UpdateScaleFactorTextBoxState();
        }
        // Complex
        private void tscbComplex_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                // Enable angle text box if needed
                UpdateAngleTextBoxState();
                //
                _controller.Redraw();
                this.ActiveControl = null;
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        private void tstbAngle_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Enter)
                {
                    tstbAngle.Text = Tools.GetPhase360(tstbAngle.Value).ToString();
                    //
                    _controller.Redraw();
                    this.ActiveControl = null;
                    // No beep
                    e.SuppressKeyPress = true;
                }
            }
            catch (Exception ex)
            {
                ExceptionTools.Show(this, ex);
            }
        }
        //
        public void SetResultNames()
        {
            InvokeIfRequired(() =>
            {
                tscbResultNames.Items.Clear();
                string[] allResultNames = _controller.AllResults.GetResultNames();
                if (allResultNames != null && allResultNames.Length > 0)
                {
                    foreach (var name in allResultNames) tscbResultNames.Items.Add(name);
                    // Drop down width
                    int maxWidth = GetMaxStringWidth(allResultNames, tscbResultNames.Font);
                    tscbResultNames.DropDownWidth = maxWidth;
                    //
                    string currentResultName = _controller.AllResults.GetCurrentResultName();
                    if (currentResultName != null) tscbResultNames.SelectedItem = currentResultName;
                    //
                    ResizeResultNamesComboBox();
                }
            });
        }
        public void SetCurrentResults(string resultsName)
        {
            InvokeIfRequired(() =>
            {
                ResizeResultNamesComboBox(); // must be here
                //
                string currentResultName = _controller.AllResults.GetCurrentResultName();
                if (resultsName != currentResultName)
                {
                    // Clear
                    ClearActiveTreeSelection();
                    Clear3D();
                    // Set results
                    _controller.AllResults.SetCurrentResults(resultsName);
                    // Regenerate tree
                    RegenerateTree();
                    // Get first component of the first field for the last increment in the last step
                    if (_controller.ResultsInitialized) _controller.CurrentFieldData =
                            _controller.AllResults.CurrentResult.GetFirstComponentOfTheFirstFieldAtDefaultIncrement();
                    //
                    if (_controller.CurrentResult != null && _controller.CurrentResult.Mesh != null)
                    {
                        // Reset the previous step and increment
                        SetAllStepAndIncrementIds();
                        // Set last increment
                        SetDefaultStepAndIncrementIds();
                        // Show the selection in the results tree
                        SelectFirstComponentOfFirstFieldOutput();
                        //
                        _controller.ViewResultsType = ViewResultsTypeEnum.ColorContours;  // Draw
                        //
                        SetMenuAndToolStripVisibility();
                        //tsmiZoomToFit_Click(null, null);    // different results have different views
                        SetCurrentEdgesVisibilities(_controller.CurrentEdgesVisibility);
                    }
                    //
                    UpdateComplexControlStates();
                    // Running this by from a command must change the results name
                    if (resultsName != tscbResultNames.SelectedItem.ToString())
                    {
                        tscbResultNames.SelectedIndexChanged -= tscbResultNames_SelectedIndexChanged;
                        tscbResultNames.SelectedItem = resultsName;
                        tscbResultNames.SelectedIndexChanged += tscbResultNames_SelectedIndexChanged;
                    }
                }
                //
                this.ActiveControl = null;
            });
        }
        private void ResizeResultNamesComboBox()
        {
            string[] allResultNames = new string[] { tscbResultNames.SelectedItem.ToString() };
            int currentWidth = GetMaxStringWidth(allResultNames, tscbResultNames.Font);
            // Control width
            currentWidth += 20; // to account for the drop down arrow
            if (currentWidth < 125) currentWidth = 125;
            else if (currentWidth > 400) currentWidth = 400;
            tscbResultNames.Size = new Size(currentWidth, tscbResultNames.Height);
            //
            Application.DoEvents();
        }
        private int GetMaxStringWidth(IEnumerable<string> items, Font font)
        {
            int maxWidth = 0;
            using (Graphics graphics = CreateGraphics())
            {
                foreach (string item in items)
                {
                    SizeF area = graphics.MeasureString(item, font);
                    maxWidth = Math.Max((int)area.Width, maxWidth);
                }
            }
            return maxWidth;
        }
        private void InitializeDeformationComboBoxes()
        {
            tscbDeformationVariable.Items.Clear();
            string[] variableNames = FeResults.GetPossibleDeformationFieldOutputNames();
            tscbDeformationVariable.Items.AddRange(variableNames);
            tscbDeformationVariable.SelectedIndex = 0;  // Displacements
            //
            tscbDeformationType.Items.Clear();
            Type type = typeof(DeformationScaleFactorTypeEnum);
            string[] typeNames = Enum.GetNames(type);
            for (int i = 0; i < typeNames.Length; i++)
            {
                typeNames[i] = ((DeformationScaleFactorTypeEnum)Enum.Parse(type, typeNames[i])).GetDisplayedName();
            }
            tscbDeformationType.Items.AddRange(typeNames);
            tscbDeformationType.SelectedIndex = 2;      // Automatic

            //if (controller.Results != null)
            //    vps.PopulateDropDownList(controller.Results.GetExistingDeformationFieldOutputNames());
            //else
            //    vps.PopulateDropDownList(CaeResults.FeResults.GetPossibleDeformationFieldOutputNames());
        }
        private void UpdateScaleFactorTextBoxState()
        {
            tslDeformationFactor.Enabled = GetDeformationType() == DeformationScaleFactorTypeEnum.UserDefined;
            tstbDeformationFactor.Enabled = tslDeformationFactor.Enabled;
        }
        public string GetDeformationVariable()
        {
            if (InvokeRequired) return (string)Invoke(new Func<string>(GetDeformationVariable));
            //
            return tscbDeformationVariable.SelectedItem.ToString();
        }
        public DeformationScaleFactorTypeEnum GetDeformationType()
        {
            // Invoke
            if (InvokeRequired)
                return (DeformationScaleFactorTypeEnum)Invoke(new Func<DeformationScaleFactorTypeEnum>(GetDeformationType));
            //
            string displayName = tscbDeformationType.SelectedItem.ToString();
            DeformationScaleFactorTypeEnum[] scaleFactorTypes =
                (DeformationScaleFactorTypeEnum[])Enum.GetValues(typeof(DeformationScaleFactorTypeEnum));
            //
            for (int i = 0; i < scaleFactorTypes.Length; i++)
            {
                if (displayName == scaleFactorTypes[i].GetDisplayedName()) return scaleFactorTypes[i];
            }
            //
            throw new NotSupportedException();
        }
        public float GetDeformationFactor()
        {
            return (float)tstbDeformationFactor.Value;
        }
        private bool ScaleFactorChanged()
        {
            float scaleFactor = _controller.GetScale();
            if (scaleFactor != _prevScaleFactor)
            {
                _prevScaleFactor = scaleFactor;
                return true;
            }
            return false;
        }
        // Complex
        private void InitializeComplexComboBoxes()
        {
            tscbComplex.Items.Clear();
            //
            Type type = typeof(ComplexResultTypeEnum);
            string[] typeNames = Enum.GetNames(type);
            for (int i = 0; i < typeNames.Length; i++)
            {
                typeNames[i] = ((ComplexResultTypeEnum)Enum.Parse(type, typeNames[i])).GetDisplayedName();
            }
            tscbComplex.Items.AddRange(typeNames);
            tscbComplex.SelectedIndex = 0;  // Real
            //
            UpdateComplexControlStates();
        }
        private void UpdateComplexControlStates()
        {
            bool visible;
            if (_controller.ContainsComplexResults)
            {
                visible = true;
                bool enabled = true;
                //
                tslComplex.Enabled = enabled;
                tscbComplex.Enabled = enabled;
                if (enabled) UpdateAngleTextBoxState();
                else
                {
                    tslAngle.Enabled = false;
                    tstbAngle.Enabled = false;
                }
            }
            else
            {
                visible = false;
            }
            //
            SetComplexControlsVisibility(visible);
        }
        private void SetComplexControlsVisibility(bool visible)
        {
            tslComplex.Visible = visible;
            tscbComplex.Visible = visible;
            tslAngle.Visible = visible;
            tstbAngle.Visible = visible;
        }
        private void UpdateAngleTextBoxState()
        {
            bool enabled = GetComplexResultType() == ComplexResultTypeEnum.RealAtAngle;
            tslAngle.Enabled = enabled;
            tstbAngle.Enabled = enabled;
        }
        public ComplexResultTypeEnum GetComplexResultType()
        {
            // Invoke
            if (InvokeRequired)
                return (ComplexResultTypeEnum)Invoke(new Func<ComplexResultTypeEnum>(GetComplexResultType));
            //
            string displayName = tscbComplex.SelectedItem.ToString();
            ComplexResultTypeEnum[] complexResultTypes = (ComplexResultTypeEnum[])Enum.GetValues(typeof(ComplexResultTypeEnum));
            //
            for (int i = 0; i < complexResultTypes.Length; i++)
            {
                if (displayName == complexResultTypes[i].GetDisplayedName()) return complexResultTypes[i];
            }
            //
            throw new NotSupportedException();
        }
        public double GetComplexAngleDeg()
        {
            return tstbAngle.Value;
        }

        #endregion  ################################################################################################################

        #region Results field toolbar  #############################################################################################

        private void tsbResultsUndeformed_Click(object sender, EventArgs e)
        {
            tsmiResultsUndeformed_Click(null, null);
        }
        private void tsbResultsDeformed_Click(object sender, EventArgs e)
        {
            tsmiResultsDeformed_Click(null, null);
        }
        private void tsbResultsColorContours_Click(object sender, EventArgs e)
        {
            tsmiResultsColorContours_Click(null, null);
        }
        private void tsbResultsUndeformedWireframe_Click(object sender, EventArgs e)
        {
            tsmiResultsDeformedColorWireframe_Click(null, null);
        }
        private void tsbResultsUndeformedSolid_Click(object sender, EventArgs e)
        {
            tsmiResultsDeformedColorSolid_Click(null, null);
        }
        private void tsbTransformation_Click(object sender, EventArgs e)
        {
            tsmiTransformation_Click(null, null);
        }
        //
        private void tscbStepAndIncrement_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                FieldData current = _controller.CurrentFieldData;
                SetFieldData(current.Name, current.Component, GetCurrentFieldOutputStepId(), GetCurrentFieldOutputStepIncrementId());
            }
            catch
            { }
        }
        private void tsbPreviousStepIncrement_Click(object sender, EventArgs e)
        {
            try
            {
                if (tscbStepAndIncrement.Enabled && tscbStepAndIncrement.SelectedIndex > 0) 
                    tscbStepAndIncrement.SelectedIndex--;
            }
            catch
            { }
        }
        private void tsbNextStepIncrement_Click(object sender, EventArgs e)
        {
            try
            {
                if (tscbStepAndIncrement.Enabled && tscbStepAndIncrement.SelectedIndex < tscbStepAndIncrement.Items.Count - 1) 
                    tscbStepAndIncrement.SelectedIndex++;
            }
            catch
            { }
        }
        private void tsbFirstStepIncrement_Click(object sender, EventArgs e)
        {
            try
            {
                if (tscbStepAndIncrement.Enabled) tscbStepAndIncrement.SelectedIndex = 0;
            }
            catch
            { }
        }
        private void tsbLastStepIncrement_Click(object sender, EventArgs e)
        {
            try
            {
                if (tscbStepAndIncrement.Enabled) tscbStepAndIncrement.SelectedIndex = tscbStepAndIncrement.Items.Count - 1;
            }
            catch
            { }
        }
        private void tsbAnimate_Click(object sender, EventArgs e)
        {
            try
            {
                if (_controller.ViewResultsType != ViewResultsTypeEnum.Undeformed &&
                    _controller.GetResultStepIDs().Length > 0 && !_frmAnimation.Visible)
                {
                    CloseAllForms();
                    SetMenuAndToolStripVisibilityByAnimation(false);
                    SetFormLocation(_frmAnimation);
                    _frmAnimation.PrepareForm(this, _controller);
                    if (_frmAnimation.DialogResult == DialogResult.Abort) SetMenuAndToolStripVisibilityByAnimation(true);
                    else _frmAnimation.Show();
                }
            }
            catch
            { }
        }

        #endregion  ################################################################################################################

        #region Status strip  ######################################################################################################
        public void SetState(string text, bool working)
        {
            InvokeIfRequired(() =>
            {
                tsslState.Text = text;
                // Progress
                if (working) tspbProgress.Style = ProgressBarStyle.Marquee;
                else tspbProgress.Style = ProgressBarStyle.Blocks;
                tspbProgress.Visible = working;
                // Rendering - vtk
                if (text == Globals.ExplodePartsText) _vtk.RenderingOn = true;
                else _vtk.RenderingOn = !working;
                _vtk.Enabled = !working;
                //_vtk.Visible = !working;
                // Hack
                //if (!working) _vtk.Left = 1;
                //else _vtk.Left = 100000;
                //
                bool menusActive = !working;
                SetMenuAndToolStripVisibilityBySetState(menusActive);
            });
        }
        private void SetStateProgress(string text, int progressPercent)
        {
            InvokeIfRequired(() =>
            {
                int value = Math.Max(tspbProgress.Minimum, Math.Min(tspbProgress.Maximum, progressPercent));
                tsslState.Text = text + " " + value.ToString(CultureInfo.InvariantCulture) + "%";
                tspbProgress.Style = ProgressBarStyle.Blocks;
                tspbProgress.Visible = true;
                tspbProgress.Value = value;
            });
        }
        private void SetFatigueProgress(double progressPercent, string message)
        {
            int value = (int)Math.Round(Math.Max(0, Math.Min(100, progressPercent)));
            string text = string.IsNullOrWhiteSpace(message) ? FatigueCalculationStateText : message;
            SetStateProgress(text, value);
        }
        private void FinishFatigueProgress()
        {
            InvokeIfRequired(() =>
            {
                SetStateReady(FatigueCalculationStateText);
                if (tsslState.Text != Globals.ReadyText)
                {
                    SetState(Globals.ReadyText, false);
                    tsslCancel.Visible = false;
                }
            });
        }
        public void SetStateReady(string currentText)
        {
            if (tsslState.Text == currentText) // check that the same command is being canceled
            {
                SetState(Globals.ReadyText, false);
                tsslCancel.Visible = false;
            }
        }
        public bool SetStateWorking(string text, bool showCancelButton = false)
        {
            if (tsslState.Text == Globals.ReadyText) // check that the state is ready
            {
                SetState(text, true);
                tsslCancel.Visible = showCancelButton;
                return true;
            }
            return false;
        }
        private bool IsStateWorking()
        {
            return tsslState.Text != Globals.ReadyText;
        }
        public bool IsStateOpening()
        {
            return tsslState.Text == Globals.OpeningText;
        }
        public bool IsStateRegeneratingOrUndoing()
        {
            return tsslState.Text == Globals.RegeneratingText || tsslState.Text == Globals.UndoingText;
        }
        //
        private void tsslCancel_MouseDown(object sender, MouseEventArgs e)
        {
            tsslCancel.BorderStyle = Border3DStyle.Sunken;
        }
        private void tsslCancel_MouseUp(object sender, MouseEventArgs e)
        {
            tsslCancel.BorderStyle = Border3DStyle.Raised;
        }
        private void tsslCancel_MouseLeave(object sender, EventArgs e)
        {
            tsslCancel.BorderStyle = Border3DStyle.Raised;
        }
        private void tsslCancel_Click(object sender, EventArgs e)
        {
            _controller.StopExecutableJob();
        }
        #endregion  ################################################################################################################

        // Methods                                                                                                                  
        #region Methods
        public void SetTitle(string title)
        {
            InvokeIfRequired(() => this.Text = title);
        }
        private bool CheckValidity()
        {
            string[] invalidItems = _controller.CheckAndUpdateModelValidity();
            if (invalidItems.Length > 0)
            {
                string text = "The model contains active invalid items:" + Environment.NewLine;
                foreach (var item in invalidItems) text += Environment.NewLine + item;
                text += Environment.NewLine + Environment.NewLine + "Continue?";
                return MessageBoxes.ShowWarningQuestionOKCancel(text) == DialogResult.OK;
            }
            return true;
        }

        public double[] GetBoundingBox()
        {
            // xMin, xMax, yMin, yMax, zMin, zMax
            return _vtk.GetBoundingBox();
        }
        public double[] GetBondingBoxSize()
        {
            return _vtk.GetBoundingBoxSize();
        }
        public string GetFileNameToOpen()
        {
            return GetFileNameToOpen(GetFileOpenFilter());
        }
        public string GetFileNameToOpen(string filter)
        {
            string fileName = null;
            InvokeIfRequired(() =>
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = filter;
                    openFileDialog.FileName = "";
                    if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        fileName = openFileDialog.FileName;
                    }
                }
            });
            return fileName;
        }
        public string GetFileNameToImport(bool onlyMaterials)
        {
            return GetFileNameToImport(GetFileImportFilter(onlyMaterials));
        }
        public string GetFileNameToImport(string filter)
        {
            string fileName = null;
            InvokeIfRequired(() =>
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = filter;
                    openFileDialog.FileName = "";
                    if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        fileName = openFileDialog.FileName;
                    }
                }
            });
            return fileName;
        }
        public string GetFileNameToSaveAs()
        {
            string fileName = null;
            InvokeIfRequired(() =>
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "PrePoMax files | *.pmx";
                    //
                    fileName = Path.GetFileName(_controller.OpenedFileName);
                    saveFileDialog.FileName = fileName;
                    //
                    saveFileDialog.OverwritePrompt = true;
                    //
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK) fileName = saveFileDialog.FileName;
                    else fileName = null;
                }
            });
            return fileName;
        }
        public bool AskToProtectPmxFile(string fileName)
        {
            bool protect = false;
            InvokeIfRequired(() =>
            {
                string text = "Protect " + fileName + " with a hash key?" + Environment.NewLine + Environment.NewLine +
                              "The key will be required to open the .pmx file. Store it in a password wallet.";
                protect = MessageBoxes.ShowQuestionYesNo("Protect .pmx file", text) == DialogResult.Yes;
            });
            return protect;
        }
        public string GetPmxProtectionKeyForSave(string fileName)
        {
            string hashKey = null;
            InvokeIfRequired(() =>
            {
                hashKey = FrmPmxProtectionKey.GetHashKey(this,
                                                         "Protect .pmx file",
                                                         "Enter the hash key required to open " + fileName + ".",
                                                         true);
            });
            return hashKey;
        }
        public string GetPmxProtectionKeyForOpen(string fileName)
        {
            string hashKey = null;
            InvokeIfRequired(() =>
            {
                hashKey = FrmPmxProtectionKey.GetHashKey(this,
                                                         "Open protected .pmx file",
                                                         fileName + " is protected. Enter its hash key.",
                                                         false);
            });
            return hashKey;
        }
        public string GetFileNameToExportPmx240()
        {
            string fileName = null;
            InvokeIfRequired(() =>
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "PrePoMax 2.4.0 compatible files | *.pmx";
                    string openedFileName = _controller.OpenedFileName;
                    if (!string.IsNullOrWhiteSpace(openedFileName))
                        fileName = Path.GetFileNameWithoutExtension(openedFileName) + "_PrePoMax_2_4_0.pmx";
                    else fileName = "PrePoMax_2_4_0.pmx";
                    saveFileDialog.FileName = fileName;
                    saveFileDialog.OverwritePrompt = true;
                    //
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK) fileName = saveFileDialog.FileName;
                    else fileName = null;
                }
            });
            return fileName;
        }
        public string[] GetFileNamesToImport(string filter)
        {
            string[] fileNames = null;
            InvokeIfRequired(() =>
            {
                // create new dialog to enable multiFilter
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Multiselect = true;
                    openFileDialog.Filter = filter;
                    openFileDialog.FileName = "";
                    if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        fileNames = openFileDialog.FileNames;
                    }
                }
            });
            return fileNames;
        }
        private string GetFileOpenFilter()
        {
            string filter;
            // Debugger attached
            if (Debugger.IsAttached)
            {
                filter = "All files|*.pmx;*.pmh;*.frd;*.dat;*.odb;*.foam" +
                         "|PrePoMax files|*.pmx" +
                         "|PrePoMax history|*.pmh" +
                         "|Calculix result files|*.frd" +
                         "|Calculix dat files|*.dat" +       // added .dat file
                         "|Abaqus result files|*.odb" +
                         "|OpenFoam files|*.foam";

            }
            // No debugger
            else
            {
                filter = "All files|*.pmx;*.pmh;*.frd;*.odb;*.foam" +
                         "|PrePoMax files|*.pmx" +
                         "|PrePoMax history|*.pmh" +
                         "|Calculix result files|*.frd" +
                         "|Abaqus result files|*.odb" +
                         "|OpenFoam files|*.foam";
            }
            return filter;
        }
        private string GetFileImportFilter(bool onlyMaterials)
        {
            if (onlyMaterials) return "Abaqus/Calculix inp files|*.inp";
            // Debugger attached
            string filter;
            if (Debugger.IsAttached)
            {
                filter = "All supported files|*.stp;*.step;*.igs;*.iges;*.brep;*.stl;*.unv;*.vol;*.inp;*.mesh;*.obj" + 
                         "|Step files|*.stp;*.step" +
                         "|Iges files|*.igs;*.iges" +
                         "|Brep files|*.brep" +
                         "|Stereolithography files|*.stl" +
                         "|Universal files|*.unv" +
                         "|Netgen files|*.vol" +
                         "|Abaqus/Calculix inp files|*.inp" +
                         "|Mmg mesh files|*.mesh" +
                         "|Wavefront obj files|*.obj";              // obj mesh reader added

            }
            // No debugger
            else
            {
                filter = "All supported files|*.stp;*.step;*.igs;*.iges;*.brep;*.stl;*.unv;*.vol;*.inp;*.mesh" +
                         "|Step files|*.stp;*.step" +
                         "|Iges files|*.igs;*.iges" +
                         "|Brep files|*.brep" +
                         "|Stereolithography files|*.stl" +
                         "|Universal files|*.unv" +
                         "|Netgen files|*.vol" +
                         "|Abaqus/Calculix inp files|*.inp" +
                         "|Mmg mesh files|*.mesh";
            }
            return filter;
        }
        private HashSet<string> GetFileImportExtensions()
        {
            string[] tmp = GetFileImportFilter(false).Split(new string[] { "*", "\"", ";", "|" },
                                                            StringSplitOptions.RemoveEmptyEntries);
            HashSet<string> extensions = new HashSet<string>();
            foreach (var entry in tmp)
            {
                if (entry.StartsWith(".")) extensions.Add(entry);
            }
            return extensions;
        }

        #region Clear  #############################################################################################################
        public void ClearControls()
        {
            InvokeIfRequired(() =>
            {
                _vtk.Clear();
                if (_vtkJs != null) _vtkJs.ClearCapturedScene();
                _vtk.RemoveAllArrowWidgets();
                _modelTree.Clear();
                outputLines = new string[0];
                tbOutput.Text = "";
                ClearResults();
                ClearAnnotationStatus();
                _selectedSymbolIndex = new Dictionary<ViewGeometryModelResults, int>();
            });
        }
        public void ClearResults()
        {
            InvokeIfRequired(() =>
            {
                tscbResultNames.Items.Clear();
                tscbResultNames.Size = new Size(125, tscbResultNames.Height);
                tscbStepAndIncrement.Items.Clear();
                _modelTree.ClearResults();
                //
                SetMenuAndToolStripVisibility();
            });
        }
        public void Clear3D()
        {
            InvokeIfRequired(() =>
            {
                _vtk.Clear();
                if (_vtkJs != null) _vtkJs.ClearCapturedScene();
            });
        }
        public void ClearButKeepParts()
        {
            InvokeIfRequired(_vtk.ClearButKeepParts);
        }
        public void ClearSelection()
        {
            _controller.ClearSelectionHistoryAndCallSelectionChanged();
        }
        public void Clear3DSelection()
        {
            InvokeIfRequired(() => _vtk.ClearSelection());
        }
        public void ClearActiveTreeSelection()
        {
            InvokeIfRequired(_modelTree.ClearActiveTreeSelection);
        }
        private void ClearAnnotationStatus()
        {
            tsmiAnnotateFaceOrientations.Checked = false;
            tsmiAnnotateParts.Checked = false;
            tsmiAnnotateMaterials.Checked = false;
            tsmiAnnotateSections.Checked = false;
            tsmiAnnotateSectionThicknesses.Checked = false;
            tsmiAnnotateReferencePoints.Checked = false;
            tsmiAnnotateConstraints.Checked = false;
            tsmiAnnotateContactPairs.Checked = false;
            tsmiAnnotateInitialConditions.Checked = false;
            tsmiAnnotateBCs.Checked = false;
            tsmiAnnotateLoads.Checked = false;
            tsmiAnnotateDefinedFields.Checked = false;
            tsmiAnnotateAllSymbols.Checked = false;
            //
            HideColorBar();
        }

        #endregion  ################################################################################################################

        #region vtkControl  ########################################################################################################
        // vtkControl
        public void GetViewParameters(out double[] position, out double[] focalPoint, out double[] viewUp, out double parallelScale)
        {
            if (_vtkJs != null && HasRetainedVtkJsCamera())
            {
                position = (double[])_vtkJsCameraPosition.Clone();
                focalPoint = (double[])_vtkJsCameraFocalPoint.Clone();
                viewUp = (double[])_vtkJsCameraViewUp.Clone();
                parallelScale = _vtkJsCameraParallelScale;
                _vtkJs.WriteCompatibilityTrace("camera", "query-parameters");
                return;
            }
            double[] pos = null;
            double[] foc = null;
            double[] up = null;
            double ps = 1;
            //
            if (this.InvokeRequired) this.Invoke((MethodInvoker)delegate
            {
                _vtk.GetViewParameters(out pos, out foc, out up, out ps);
            });
            else _vtk.GetViewParameters(out pos, out foc, out up, out ps);
            // Assign to out parameters after Invoke completes
            position = pos;
            focalPoint = foc;
            viewUp = up;
            parallelScale = ps;
        }
        public void SetViewParameters(bool animate, double[] position, double[] focalPoint, double[] viewUp, double parallelScale)
        {
            InvokeIfRequired(() =>
            {
                if (_vtkJs == null) _vtk.SetViewParameters(animate, position, focalPoint, viewUp, parallelScale);
                else
                {
                    RetainVtkJsCamera(position, focalPoint, viewUp, parallelScale);
                    _vtkJsCameraCommandPending = true;
                    _vtkJs.SetCustomCamera(position, focalPoint, viewUp, parallelScale);
                    _vtkJs.WriteCompatibilityTrace("camera", "set-custom");
                }
            });
        }
        public void SetFrontBackView(bool animate, bool front)
        {
            InvokeIfRequired(() =>
            {
                if (_vtkJs == null) _vtk.SetFrontBackView(animate, front);
                else
                {
                    _vtkJsCameraCommandPending = true;
                    _vtkJs.SetCameraView(front ? "front" : "back");
                    _vtkJs.WriteCompatibilityTrace("camera", "set-front-back");
                }
            });
        }
        public void SetZoomToFit(bool animate)
        {
            InvokeIfRequired(() =>
            {
                if (_vtkJs == null) _vtk.SetZoomToFit(animate);
                else
                {
                    _vtkJsCameraCommandPending = true;
                    _vtkJs.SetCameraView("reset");
                    _vtkJs.WriteCompatibilityTrace("camera", "zoom-to-fit");
                }
            });
        }
        public void StartBoxZoom()
        {
            InvokeIfRequired(() =>
            {
                tsbBoxZoom.Checked = true;
                if (_vtkJs == null) _vtk.StartBoxZoom();
                else
                {
                    _vtkJsBoxZoomStartScale = HasRetainedVtkJsCamera() ? _vtkJsCameraParallelScale : 0;
                    _vtkJs.StartBoxZoom();
                    _vtkJs.WriteCompatibilityTrace("camera", "start-box-zoom");
                }
            });
        }
        public void BoxZoomEnded()
        {
            tsbBoxZoom.Checked = false;
        }
        public double[] GetViewPlaneNormal()
        {
            if (_vtkJs != null && HasRetainedVtkJsCamera())
            {
                double x = _vtkJsCameraPosition[0] - _vtkJsCameraFocalPoint[0];
                double y = _vtkJsCameraPosition[1] - _vtkJsCameraFocalPoint[1];
                double z = _vtkJsCameraPosition[2] - _vtkJsCameraFocalPoint[2];
                double length = Math.Sqrt(x * x + y * y + z * z);
                _vtkJs.WriteCompatibilityTrace("camera", "query-view-plane-normal");
                return new double[] { x / length, y / length, z / length };
            }
            return InvokeIfRequired(() => _vtk.GetViewPlaneNormal());
        }
        public void AdjustCameraDistanceAndClipping()
        {
            InvokeIfRequired(() =>
            {
                if (_vtkJs == null) _vtk.AdjustCameraDistanceAndClippingRedraw();
                else AdjustVtkJsCameraDistanceAndClipping("adjust-distance");
            });
        }
        public void UpdateScalarsAndRedraw()
        {
            InvokeIfRequired(_vtk.UpdateScalarsAndRedraw);
        }
        public void UpdateScalarsAndCameraAndRedraw()
        {
            InvokeIfRequired(() =>
            {
                if (_vtkJs == null) _vtk.UpdateScalarsAndCameraAndRedraw();
                else
                {
                    _vtk.UpdateScalarsAndRedraw();
                    AdjustVtkJsCameraDistanceAndClipping("update-scalars-adjust");
                }
            });
        }
        private void AdjustVtkJsCameraDistanceAndClipping(string operation)
        {
            if (!_vtkJsCameraCommandPending && HasRetainedVtkJsCamera())
            {
                _vtkJsCameraCommandPending = true;
                _vtkJs.SetCustomCamera(_vtkJsCameraPosition, _vtkJsCameraFocalPoint, _vtkJsCameraViewUp,
                                       _vtkJsCameraParallelScale, "adjust");
                _vtkJs.WriteCompatibilityTrace("camera", operation);
            }
            else _vtkJs.WriteCompatibilityTrace("camera", operation + "-deferred");
        }
        //
        public vtkMaxActorData[] GetVtkMaxActorData()
        {
            return InvokeIfRequired(() => _vtk.GetVtkMaxActorData());
        }
        public vtkMaxLookupTable GetLookupTable()
        {
            return InvokeIfRequired(() => _vtk.GetLookupTable());
        }
        public double[] GetClosestUpView()
        {
            if (_vtkJs != null && HasRetainedVtkJsCamera())
            {
                double[] up = _vtkJsCameraViewUp;
                int axis = Math.Abs(up[0]) >= Math.Abs(up[1]) ? 0 : 1;
                if (Math.Abs(up[2]) > Math.Abs(up[axis])) axis = 2;
                double[] closest = new double[3];
                closest[axis] = Math.Sign(up[axis]);
                _vtkJs.WriteCompatibilityTrace("camera", "query-closest-up");
                return closest;
            }
            return InvokeIfRequired(() => _vtk.GetClosestUpView());
        }

        // Section view
        public void CreateSectionView(double[] point, double[] normal, bool lightenColors, Color sectionColor)
        {
            if (_vtkJs == null) InvokeIfRequired(_vtk.CreateSectionView, point, normal, lightenColors, sectionColor);
            else
            {
                _vtkJs.WriteCompatibilityTrace("geometry", "section-view-create");
                InvokeIfRequired(() => _vtkJs.SetSectionView(point, normal, lightenColors, sectionColor));
            }
            InvokeIfRequired(() => { tsbSectionView.Checked = true; });
        }
        public void UpdateSectionView(double[] point, double[] normal, bool lightenColors, Color sectionColor)
        {
            if (_vtkJs == null) InvokeIfRequired(_vtk.UpdateSectionView, point, normal, lightenColors, sectionColor);
            else
            {
                _vtkJs.WriteCompatibilityTrace("geometry", "section-view-update");
                InvokeIfRequired(() => _vtkJs.SetSectionView(point, normal, lightenColors, sectionColor));
            }
            InvokeIfRequired(() => { tsbSectionView.Checked = true; });
        }
        public void RemoveSectionView()
        {
            if (_vtkJs == null) InvokeIfRequired(_vtk.RemoveSectionView);
            else
            {
                _vtkJs.WriteCompatibilityTrace("geometry", "section-view-remove");
                InvokeIfRequired(_vtkJs.RemoveSectionView);
            }
            InvokeIfRequired(() => { tsbSectionView.Checked = false; });
        }
        // Exploded view
        public void PreviewExplodedView(Dictionary<string, double[]> partOffsets, bool animate, int timeMs,
                                        Action completed = null)
        {
            if (_vtkJs == null)
            {
                InvokeIfRequired(_vtk.PreviewExplodedView, partOffsets, animate, timeMs);
                completed?.Invoke();
            }
            else
            {
                _vtkJs.WriteCompatibilityTrace("geometry", "exploded-preview");
                InvokeIfRequired(() => _vtkJs.PreviewExplodedView(partOffsets, animate, timeMs, completed));
            }
        }
        public void RemovePreviewedExplodedView(string[] partNames)
        {
            if (_vtkJs == null) InvokeIfRequired(_vtk.RemovePreviewedExplodedView, partNames);
            else
            {
                _vtkJs.WriteCompatibilityTrace("geometry", "exploded-preview-remove");
                InvokeIfRequired(_vtkJs.RemoveExplodedViewPreview, partNames);
            }
        }
        public void CompleteAfterNextVtkJsSceneApplied(Action completed)
        {
            if (_vtkJs == null) completed?.Invoke();
            else InvokeIfRequired(() => _vtkJs.CompleteAfterNextSceneApplied(completed));
        }
        public void SetExplodedViewStatus(bool status)
        {
            InvokeIfRequired(() => { tsbExplodedView.Checked = status; });
        }
        // Follower view
        public void ApplyFollowerView(string[] partName, int[] nodeIds)
        {
            if (_vtkJs == null) InvokeIfRequired(() => _vtk.ApplyFollowerView(partName, nodeIds));
            else
            {
                _vtkJs.WriteCompatibilityTrace("camera", "follower-apply");
                InvokeIfRequired(_vtkJs.SetFollowerView, partName, nodeIds);
            }
            InvokeIfRequired(() => { tsbFollowerView.Checked = true; });
        }
        public void UpdateFollowerView()
        {
            if (_vtkJs == null) InvokeIfRequired(() => _vtk.UpdateFollowerView());
            else _vtkJs.WriteCompatibilityTrace("camera", "follower-update");
        }
        public void RemoveFollowerView()
        {
            if (_vtkJs == null) InvokeIfRequired(_vtk.RemoveFollowerView);
            else
            {
                _vtkJs.WriteCompatibilityTrace("camera", "follower-remove");
                InvokeIfRequired(_vtkJs.ClearFollowerView);
            }
            InvokeIfRequired(() => { tsbFollowerView.Checked = false; });
        }
        // Transformations
        public void AddSymmetry(int symmetryPlane, double[] symmetryPoint)
        {
            InvokeIfRequired(_vtk.AddSymmetry, symmetryPlane, symmetryPoint);
        }
        public void AddLinearPattern(double[] displacement, int numOfItems)
        {
            InvokeIfRequired(_vtk.AddLinearPattern, displacement, numOfItems);
        }
        public void AddCircularPattern(double[] axisPoint, double[] axisNormal, double angle, int numOfItems)
        {
            InvokeIfRequired(_vtk.AddCircularPattern, axisPoint, axisNormal, angle, numOfItems);
        }
        public void ApplyTransformations()
        {
            InvokeIfRequired(_vtk.ApplyTransforms);
        }
        public void SetTransformationsStatus(bool status)
        {
            InvokeIfRequired(() => { tsbTransformation.Checked = status; });
        }
        public void HideTransformedActors()
        {
            InvokeIfRequired(_vtk.HideTransformedActors);
        }
        public void ShowTransformedActors()
        {
            InvokeIfRequired(_vtk.ShowTransformedActors);
        }
        //
        public vtkMaxActor Add3DNodes(vtkMaxActorData nodeData)
        {
            return InvokeIfRequired(() => _vtk.AddPoints(nodeData));
        }
        public vtkMaxActor Add3DCells(vtkMaxActorData cellData)
        {
            return InvokeIfRequired(() =>
            {
                if (_vtkJs != null) _vtkJs.CaptureActor(cellData);
                return _vtk.AddCells(cellData);
            });
        }
        public void Add3DCells(vtkMaxActorData[] cellData)
        {
            InvokeIfRequired(() =>
            {
                if (_vtkJs != null)
                {
                    foreach (vtkMaxActorData actorData in cellData) _vtkJs.CaptureActor(actorData);
                }
                _vtk.AddCells(cellData);
            });
        }
        public void AddActor(vtkMaxActor actor)
        {
            InvokeIfRequired(_vtk.AddActor, actor);
        }
        public vtkMaxActor AddOpenFoamMesh(string fileName, double timeValue, string actorName, Color color)
        {
            return InvokeIfRequired(() => _vtk.AddOpenFoamMesh(fileName, timeValue, actorName, color));
        }
        public void AddScalarFieldOn3DCells(vtkMaxActorData actorData, bool update)
        {
            InvokeIfRequired(() =>
            {
                if (_vtkJs != null) _vtkJs.CaptureActor(actorData);
                _vtk.AddScalarFieldOnCells(actorData, update);
            });
        }
        public bool AddAnimatedScalarFieldOn3DCells(vtkMaxActorData actorData)
        {
            bool added = _vtk.AddAnimatedScalarFieldOnCells(actorData);
            if (added && _vtkJs != null) _vtkJs.RegisterAnimation(actorData);
            return added;
        }
        public void UpdateActorSurfaceScalarField(string actorName, float[] values, NodesExchangeData extremeNodes,
                                                  float[] frustumCellLocatorValues, bool update)
        {
            InvokeIfRequired(() =>
            {
                _vtk.UpdateActorScalarField(actorName, values, extremeNodes, frustumCellLocatorValues, update);
                if (_vtkJs != null)
                {
                    _vtkJs.UpdateActorScalars(actorName, values);
                    _vtkJs.UpdateActorExtrema(actorName, values == null ? null : extremeNodes);
                }
            });
        }
        public void UpdateActorColorContoursVisibility(string[] actorNames, bool colorContour)
        {
            InvokeIfRequired(() =>
            {
                _vtk.UpdateActorsColorContoursVisibility(actorNames, colorContour);
                if (_vtkJs != null) _vtkJs.SetActorColorContours(actorNames, colorContour);
            });
        }
        public void AddSphereActor(vtkMaxActorData actorData, double symbolSize)
        {
            InvokeIfRequired(_vtk.AddSphereActor, actorData, symbolSize);
        }
        public void AddCoordinateAxis(vtkMaxActorData data, double symbolSize)
        {
            InvokeIfRequired(_vtk.AddCoordinateAxis, data, symbolSize);
        }
        public void AddCaptionActor(string name, string caption, Color color, double[] position,
                                    double[] offsetVector, double fontScaleFactor, vtkRendererLayer layer)
        {
            InvokeIfRequired(_vtk.AddCaptionActor, name, caption, color, position, offsetVector, fontScaleFactor, layer);
        }
        public void AddOrientedDisplacementConstraintActor(vtkMaxActorData actorData, double symbolSize)
        {
            InvokeIfRequired(_vtk.AddOrientedDisplacementConstraintActor, actorData, symbolSize);
        }
        public void AddOrientedConeActors(vtkMaxActorData data, double symbolSize)
        {
            InvokeIfRequired(_vtk.AddOrientedConeActors, data, symbolSize);
        }
        public void AddOrientedRotationalConstraintActor(vtkMaxActorData actorData, double symbolSize)
        {
            InvokeIfRequired(_vtk.AddOrientedRotationalConstraintActor, actorData, symbolSize);
        }
        public void AddOrientedArrowsActor(vtkMaxActorData actorData, double symbolSize, bool invert = false, 
                                           double relativeSize = 1)
        {
            InvokeIfRequired(_vtk.AddOrientedArrowsActor, actorData, symbolSize, invert, relativeSize);
        }
        public void AddOrientedDoubleArrowsActor(vtkMaxActorData actorData, double symbolSize)
        {
            InvokeIfRequired(_vtk.AddOrientedDoubleArrowsActor, actorData, symbolSize);
        }
        public void AddOrientedSpringActor(vtkMaxActorData actorData, double symbolSize, bool invert = false)
        {
            InvokeIfRequired(_vtk.AddOrientedSpringActor, actorData, symbolSize, invert);
        }
        public void AddOrientedThermosActor(vtkMaxActorData actorData, double symbolSize, bool invert = false)
        {
            InvokeIfRequired(_vtk.AddOrientedThermoActor, actorData, symbolSize, invert);
        }
        public void AddOrientedFluxActor(vtkMaxActorData actorData, double symbolSize, bool center, bool invert)
        {
            InvokeIfRequired(_vtk.AddOrientedFluxActor, actorData, symbolSize, center, invert);
        }
        //
       
        //
        public bool ContainsActor(string actorName)
        {
            return _vtk.ContainsActor(actorName);
        }
        public void HighlightActor(string actorName)
        {
            InvokeIfRequired(_vtk.HighlightActor, actorName);
        }
        public void UpdateActor(string oldName, string newName, Color newColor)
        {
            InvokeIfRequired(() =>
            {
                _vtk.UpdateActor(oldName, newName, newColor);
                if (_vtkJs != null) _vtkJs.UpdateActor(oldName, newName, newColor);
            });
        }
        public void HideActors(string[] actorNames, bool updateColorContours)
        {
            InvokeIfRequired(() =>
            {
                _vtk.HideActors(actorNames, updateColorContours, _vtkJs == null);
                if (_vtkJs != null)
                {
                    _vtkJs.SetActorVisibility(actorNames, false);
                    _vtkJs.WriteCompatibilityTrace("camera", "actor-visibility-hide");
                }
            });
        }
        public void ShowActors(string[] actorNames, bool updateColorContours)
        {
            InvokeIfRequired(() =>
            {
                _vtk.ShowActors(actorNames, updateColorContours, _vtkJs == null);
                if (_vtkJs != null)
                {
                    _vtkJs.SetActorVisibility(actorNames, true);
                    _vtkJs.WriteCompatibilityTrace("camera", "actor-visibility-show");
                }
            });
        }
        // Settings                                             
        public void SetCoorSysVisibility(bool visibility)
        {
            InvokeIfRequired(_vtk.SetCoorSysVisibility, visibility);
        }
        // Scale bar
        public void SetScaleWidgetVisibility(bool visibility)
        {
            InvokeIfRequired(_vtk.SetScaleWidgetVisibility, visibility);
        }
        private void SetScaleWidgetUnit(UnitSystem unitSystem)
        {
            string unit = "";
            if (unitSystem.UnitSystemType != UnitSystemType.Undefined) unit = unitSystem.LengthUnitAbbreviation;
            //
            InvokeIfRequired(_vtk.SetScaleWidgetUnit, unit);
        }
        // Scalar bar
        public void InitializeResultWidgetPositions()
        {
            InvokeIfRequired(_vtk.InitializeResultWidgetPositions);
        }
        public void SetScalarBarColorSpectrum(vtkControl.vtkMaxColorSpectrum colorSpectrum)
        {
            InvokeIfRequired(_vtk.SetScalarBarColorSpectrum, colorSpectrum);
        }
        public void SetScalarBarNumberFormat(string numberFormat)
        {
            InvokeIfRequired(_vtk.SetScalarBarNumberFormat, numberFormat);
        }
        public void SetScalarBarText(string fieldName, string componentName, string unitAbbreviation, string complexComponent,
                                     string minMaxType)
        {
            if (_vtkJs != null)
            {
                string vtkJsComponent = string.IsNullOrWhiteSpace(complexComponent)
                    ? componentName
                    : componentName + " " + complexComponent;
                _vtkJs.SetFieldMetadata(fieldName, vtkJsComponent, unitAbbreviation);
            }
            InvokeIfRequired(_vtk.SetScalarBarText, fieldName, componentName, unitAbbreviation, complexComponent, minMaxType);
        }
        public void DrawLegendBackground(bool drawBackground)
        {
            InvokeIfRequired(_vtk.DrawScalarBarBackground, drawBackground);
        }
        public void DrawLegendBorder(bool drawBorder)
        {
            InvokeIfRequired(_vtk.DrawScalarBarBorder, drawBorder);
        }
        // Color bar
        public void InitializeColorBarWidgetPosition()
        {
            InvokeIfRequired(_vtk.InitializeColorBarWidgetPosition);
        }
        public void SetColorBarColorsAndLabels(Color[] colors, string[] labels)
        {
            InvokeIfRequired(_vtk.SetColorBarColorsAndLabels, colors, labels);
        }
        public void AddColorBarColorsAndLabels(Color[] colors, string[] labels)
        {
            InvokeIfRequired(_vtk.AddColorBarColorsAndLabels, colors, labels);
        }
        public void DrawColorBarBackground(bool drawBackground)
        {
            InvokeIfRequired(_vtk.DrawColorBarBackground, drawBackground);
        }
        public void DrawColorBarBorder(bool drawBorder)
        {
            InvokeIfRequired(_vtk.DrawColorBarBorder, drawBorder);
        }
        public void HideColorBar()
        {
            InvokeIfRequired(_vtk.HideColorBar);
        }
        // Status bar
        public void SetStatusBlockVisibility(bool draw)
        {
            InvokeIfRequired(_vtk.SetStatusBlockVisibility, draw);
        }
        public void DrawStatusBlockBackground(bool drawBackground)
        {
            InvokeIfRequired(_vtk.DrawStatusBlockBackground, drawBackground);
        }
        public void DrawStatusBlockBorder(bool drawBorder)
        {
            InvokeIfRequired(_vtk.DrawStatusBlockBorder, drawBorder);
        }
        public void SetStatusBlock(string name, DateTime dateTime, float analysisTime, string unit, string deformationVariable,
                                   float scaleFactor, vtkControl.vtkMaxFieldDataType fieldType, int stepNumber, int incrementNumber)
        {
            InvokeIfRequired(_vtk.SetStatusBlock, name, dateTime, analysisTime, unit, deformationVariable, scaleFactor,
                             fieldType, stepNumber, incrementNumber);
        }
        // General
        public void SetBackground(bool gradient, Color topColor, Color bottomColor, bool redraw)
        {
            InvokeIfRequired(_vtk.SetBackground, gradient, topColor, bottomColor, redraw);
        }
        public void SetLighting(double ambient, double diffuse, bool redraw)
        {
            InvokeIfRequired(_vtk.SetLighting, ambient, diffuse, redraw);
        }
        public void SetSmoothing(bool pointSmoothing, bool lineSmoothing, bool redraw)
        {
            InvokeIfRequired(_vtk.SetSmoothing, pointSmoothing, lineSmoothing, redraw);
        }
        // Highlight
        public void SetHighlightColor(Color primaryHighlightColor, Color secondaryHighlightColor)
        {
            InvokeIfRequired(_vtk.SetHighlightColor, primaryHighlightColor, secondaryHighlightColor);
        }
        public void SetMouseHighlightColor(Color mouseHighlightColor)
        {
            InvokeIfRequired(_vtk.SetMouseHighlightColor, mouseHighlightColor);
        }
        // Symbols
        public void SetDrawSymbolEdges(bool drawSilhouettes)
        {
            InvokeIfRequired(_vtk.SetDrawSymbolEdges, drawSilhouettes);
        }
        //
        public void CropPartWithCylinder(string partName, double r, string fileName)
        {
            InvokeIfRequired(_vtk.CropPartWithCylinder, partName, r, fileName);
        }
        public void CropPartWithCube(string partName, double a, string fileName)
        {
            InvokeIfRequired(_vtk.CropPartWithCube, partName, a, fileName);
        }
        public void SmoothPart(string partName, double a, string fileName)
        {
            InvokeIfRequired(_vtk.SmoothPart, partName, a, fileName);
        }
        // User pick
        public void ActivateUserPick()
        {
            _vtk.UserPick = true;
        }
        public void DeactivateUserPick()
        {
            _vtk.UserPick = false;
        }
        #endregion  ################################################################################################################

        #region Results  ###########################################################################################################
        // Results
        public void SetFieldData(string name, string component)
        {
            InvokeIfRequired(() =>
            {
                SetFieldData(name, component, GetCurrentFieldOutputStepId(), GetCurrentFieldOutputStepIncrementId());
            });
        }
        public void SetFieldData(string name, string component, int stepId, int stepIncrementId)
        {
            FieldData fieldData = new FieldData(name, component, stepId, stepIncrementId);
            FieldData currentData = _controller.CurrentFieldData;
            // In case the currentData is null exit
            if (currentData == null) return;
            //
            if (!fieldData.Equals(currentData)) // update results only if field data changed
            {
                // Stop and update animation data only if field data changed
                if (_frmAnimation.Visible) _frmAnimation.Hide();
                //
                if (fieldData.Name == currentData.Name && fieldData.Component == currentData.Component)
                {
                    // Step id or increment id changed                                              

                    // Find the chosen data; also contains info about type of step ...
                    fieldData = _controller.CurrentResult.GetFieldData(fieldData.Name,
                                                                       fieldData.Component,
                                                                       fieldData.StepId,
                                                                       fieldData.StepIncrementId,
                                                                       true);
                    // Update controller field data
                    _controller.CurrentFieldData = fieldData;
                    // Draw deformation or field data
                    if (_controller.ViewResultsType != ViewResultsTypeEnum.Undeformed) _controller.DrawResults(false);
                }
                else
                {
                    // Field of field component changed                                                 

                    // Update controller field data; this is used for the SetStepAndIncrementIds to detect missing ids
                    _controller.CurrentFieldData = fieldData;
                    // Find the existing chosen data; also contains info about type of step ...
                    fieldData = _controller.CurrentResult.GetFieldData(fieldData.Name,
                                                                       fieldData.Component,
                                                                       fieldData.StepId,
                                                                       fieldData.StepIncrementId,
                                                                       true);
                    // Update controller field data
                    _controller.CurrentFieldData = fieldData;
                    // Draw field data
                    if (_controller.ViewResultsType == ViewResultsTypeEnum.ColorContours) _controller.UpdatePartsScalarFields();
                }
                //
                UpdateComplexControlStates();
                // Move focus from step and step increment dropdown menus
                this.ActiveControl = null;
            }
        }
        public void SetAllStepAndIncrementIds(bool reset = false)
        {
            InvokeIfRequired(() =>
            {
                // Save current step and increment id
                string currentStepIncrement = (string)tscbStepAndIncrement.SelectedItem;
                string[] prevStepIncrementIds = null;
                if (!reset && currentStepIncrement != null)
                {
                    prevStepIncrementIds = currentStepIncrement.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
                }
                // Set all increments
                tscbStepAndIncrement.SelectedIndexChanged -= tscbStepAndIncrement_SelectionChanged;  // detach event
                tscbStepAndIncrement.Items.Clear();
                Dictionary<int, int[]> allIds = _controller.CurrentResult.GetAllExistingIncrementIds();
                int lastStepId = 1;
                int lastIncrementId = 0;
                foreach (var entry in allIds)
                {
                    foreach (int incrementId in entry.Value)
                    {
                        tscbStepAndIncrement.Items.Add(entry.Key.ToString() + ", " + incrementId);
                        lastIncrementId = incrementId;
                    }
                    lastStepId = entry.Key;
                }
                tscbStepAndIncrement.SelectedIndexChanged += tscbStepAndIncrement_SelectionChanged;  // reattach event
                // Reselect previous step and increment
                if (prevStepIncrementIds != null)
                {
                    int stepId = Math.Min(int.Parse(prevStepIncrementIds[0]), lastStepId);
                    int incrementId = Math.Min(int.Parse(prevStepIncrementIds[1]), lastIncrementId);
                    SetStepAndIncrementIds(stepId, incrementId);
                }
                else SetDefaultStepAndIncrementIds();
            });
        }
        public void SetStepAndIncrementIds(int stepId, int incrementId)
        {
            InvokeIfRequired(() =>
            {
                string stepIncrement = stepId + ", " + incrementId;
                // Set the combo box
                if (tscbStepAndIncrement.Items.Contains(stepIncrement))
                {
                    tscbStepAndIncrement.SelectedIndexChanged -= tscbStepAndIncrement_SelectionChanged;    // detach event
                    tscbStepAndIncrement.SelectedItem = stepIncrement;
                    // Set the step and increment if the combo box set was successful
                    FieldData data = _controller.CurrentFieldData;
                    data.StepId = stepId;
                    data.StepIncrementId = incrementId;
                    _controller.CurrentFieldData = data;   // to correctly update the increment time
                    tscbStepAndIncrement.SelectedIndexChanged += tscbStepAndIncrement_SelectionChanged;  // reattach event
                }
                else SetDefaultStepAndIncrementIds();
            });
        }
        public void SetDefaultStepAndIncrementIds()
        {
            string[] tmp;
            FieldData fieldData = _controller.CurrentFieldData;
            if (fieldData != null)
            {
                if (fieldData.StepId == -1 && fieldData.StepIncrementId == -1) return;
                else SetStepAndIncrementIds(fieldData.StepId, fieldData.StepIncrementId);
            }
            //
            return;
            //
            if (_controller.CurrentFieldData.StepType == CaeResults.StepTypeEnum.Frequency)
            {
                string firstStepIncrement = (string)tscbStepAndIncrement.Items[tscbStepAndIncrement.Items.Count - 1];
            }
            else
            {
                string lastStepIncrement = (string)tscbStepAndIncrement.Items[tscbStepAndIncrement.Items.Count - 1];
                tmp = lastStepIncrement.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
                
            }
        }
        public int GetCurrentFieldOutputStepId()
        {
            string selectedStepIncrement = (string)tscbStepAndIncrement.SelectedItem;
            string[] tmp = selectedStepIncrement.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
            return int.Parse(tmp[0]);
        }
        public int GetCurrentFieldOutputStepIncrementId()
        {
            string selectedStepIncrement = (string)tscbStepAndIncrement.SelectedItem;
            string[] tmp = selectedStepIncrement.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
            return int.Parse(tmp[1]);
        }
        public float GetAnimationScaleFactor(int frameNumber)
        {
            return _vtk.GetAnimationScaleFactor(frameNumber);
        }
        public void SetAnimationAcceleration(bool animationAcceleration)
        {
            InvokeIfRequired(_vtk.SetAnimationAcceleration, animationAcceleration);
        }
        public void SetAnimationFrameData(float[] time, int[] stepId, int[] stepIncrementId, float[] scale,
                                          double[] allFramesScalarRange, vtkMaxAnimationType animationType)
        {
            InvokeIfRequired(_vtk.SetAnimationFrameData, time, stepId, stepIncrementId, scale, allFramesScalarRange,
                             animationType);
            if (_vtkJs != null)
                InvokeIfRequired(_vtkJs.SetAnimationFrameData, time, stepId, stepIncrementId, scale, animationType);
        }
        public void SetAnimationFrame(int frameNum, bool scalarRangeFromAllFrames)
        {
            InvokeIfRequired(() =>
            {
                var totalTimer = Stopwatch.StartNew();
                _vtk.SetAnimationFrame(frameNum, scalarRangeFromAllFrames, _vtkJs == null, _vtkJs == null);
                double legacyMs = totalTimer.Elapsed.TotalMilliseconds;
                if (_vtkJs != null)
                {
                    _vtkJs.WriteCompatibilityTrace("camera", "animation-frame-adjust-" + frameNum);
                    var mirrorTimer = Stopwatch.StartNew();
                    _vtkJs.SetAnimationFrame(frameNum);
                    mirrorTimer.Stop();
                    totalTimer.Stop();
                    _vtkJs.WriteAcceptanceTrace("animation activation frame=" + frameNum +
                        " legacyMs=" + legacyMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                        " mirrorMs=" + mirrorTimer.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                        " totalMs=" + totalTimer.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            });
        }
        public void SaveAnimationAsAVI(string fileName, int[] firstLastFrame, int step, int fps, bool scalarRangeFromAllFrames,
                                       bool swing, bool encoderOptions)
        {
            InvokeIfRequired(_vtk.SaveAnimationAsAVI, fileName, firstLastFrame, step, fps, scalarRangeFromAllFrames,
                             swing, encoderOptions);
        }
        public void SaveAnimationAsImages(string fileName, int[] firstLastFrame, int step, bool scalarRangeFromAllFrames,
                                          bool swing)
        {
            InvokeIfRequired(_vtk.SaveAnimationAsImages, fileName, firstLastFrame, step, scalarRangeFromAllFrames, swing);
        }
        // Widgets
        public void AddArrowWidget(string name, string text, string numberFormat, double[] anchorPoint,
                                   bool drawBackground, bool drawBorder, bool visible)
        {
            InvokeIfRequired(() =>
            {
                if (_vtkJs == null)
                    _vtk.AddArrowWidget(name, text, numberFormat, anchorPoint, drawBackground, drawBorder, visible);
                else
                {
                    _vtkJs.SetAnnotation(name, text, numberFormat, anchorPoint,
                                         drawBackground, drawBorder, visible);
                    _vtkJs.WriteCompatibilityTrace("annotations", "add");
                }
            });
        }
        public void RemoveAllArrowWidgets()
        {
            InvokeIfRequired(() =>
            {
                if (_vtkJs == null) _vtk.RemoveAllArrowWidgets();
                else
                {
                    _vtkJs.ClearAnnotations();
                    _vtkJs.WriteCompatibilityTrace("annotations", "remove-all");
                }
            });
        }
        public void RemoveArrowWidgets(string[] widgetNames)
        {
            InvokeIfRequired(() =>
            {
                if (_vtkJs == null) _vtk.RemoveArrowWidgets(widgetNames);
                else
                {
                    _vtkJs.RemoveAnnotations(widgetNames);
                    _vtkJs.WriteCompatibilityTrace("annotations", "remove-named");
                }
            });
        }

        #endregion  ################################################################################################################

        #region Tree  ##############################################################################################################
        // Tree
        public void RegenerateTreeCallback()
        {
            RegenerateTree();
        }
        public void RegenerateTree(bool remeshing = false)
        {
            InvokeIfRequired(_modelTree.RegenerateTree, _controller.Model, _controller.Jobs, _controller.CurrentResult, remeshing);
            InvokeIfRequired(UpdateBaramMeshGeometryTree);
            InvokeIfRequired(UpdateSymbolsList);
            InvokeIfRequired(SelectLastSymbolName);
        }
        public void AddTreeNode(ViewGeometryModelResults view, NamedClass item, string parentName)
        {
            ViewType viewType = GetViewType(view);
            //
            InvokeIfRequired(_modelTree.AddTreeNode, viewType, item, parentName);
            if (item is Step) UpdateSymbolsList();
        }
        public void UpdateTreeNode(ViewGeometryModelResults view, string oldItemName, NamedClass item, string parentName,
                                   bool updateSelection = true)
        {
            ViewType viewType = GetViewType(view);
            //
            InvokeIfRequired(_modelTree.UpdateTreeNode, viewType, oldItemName, item, parentName, updateSelection);
            if (item is Step) UpdateSymbolsList();
        }
        public void SwapTreeNode(ViewGeometryModelResults view, string firstItemName, NamedClass firstItem,
                                string secondItemName, NamedClass secondItem, string parentName)
        {
            ViewType viewType = GetViewType(view);
            //
            InvokeIfRequired(_modelTree.SwapTreeNodes, viewType, firstItemName, firstItem, secondItemName,
                             secondItem, parentName);
            //if (item is Step) UpdateOneStepInSymbolsForStepList(oldItemName, item.Name);
        }
        public void RemoveTreeNode<T>(ViewGeometryModelResults view, string nodeName, string parentName) where T : NamedClass
        {
            ViewType viewType = GetViewType(view);
            //
            InvokeIfRequired(_modelTree.RemoveTreeNode<T>, viewType, nodeName, parentName);
            if (typeof(T) == typeof(Step)) UpdateSymbolsList();
        }
        public bool[][] GetTreeExpandCollapseState()
        {
            if (InvokeRequired)
            {
                return (bool[][])Invoke((Func<bool[][]>)delegate
                    { return _modelTree.GetAllTreesExpandCollapseState(out string[][] afterNodeNames); });
            }
            else
            {
                return _modelTree.GetAllTreesExpandCollapseState(out string[][] afterNodeNames);
            }
        }
        public void SetTreeExpandCollapseState(bool[][] states)
        {
            InvokeIfRequired(_modelTree.SetAllTreeExpandCollapseState, states);
        }
        public void UpdateHighlight()
        {
            if (_allForms == null) return;
            //
            List<IFormHighlight> highlightForms = new List<IFormHighlight>();
            foreach (var aForm in _allForms)
            {
                // Do not count the Query form
                if (aForm.Visible && (aForm is IFormHighlight ihf)) highlightForms.Add(ihf);
            }
            if (highlightForms.Count == 0) UpdateHighlightFromTree();
            else if (highlightForms.Count == 1) highlightForms[0].Highlight();
            else throw new NotSupportedException();
        }
        public void UpdateHighlightFromTree()
        {
            InvokeIfRequired(_modelTree.UpdateHighlight);
        }
        public void SelectBaseParts(string[] partNames)
        {
            MouseEventArgs e = new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0);
            InvokeIfRequired(SelectBaseParts, e, Keys.None, partNames);
        }
        public void SelectBaseParts(MouseEventArgs e, Keys modifierKeys, string[] partNames)
        {
            if ((partNames != null && partNames.Length == 0) || 
                (partNames != null && partNames.Length > 0 && partNames[0] == null))
            {
                if (modifierKeys != Keys.Shift && modifierKeys != Keys.Control) _controller.ClearAllSelection();
            }
            else
            {
                //
                if (e.Clicks == 2) _modelTree.EditSelectedPart();
                else
                {
                    int count = 0;
                    BasePart[] parts;
                    int numOfSelectedTreeNodes = 0;
                    //
                    if (GetCurrentView() == ViewGeometryModelResults.Geometry)
                        parts = _controller.GetGeometryPartsForSelection(partNames);
                    else if (GetCurrentView() == ViewGeometryModelResults.Model)
                        parts = _controller.GetModelParts(partNames);
                    else if (GetCurrentView() == ViewGeometryModelResults.Results)
                        parts = _controller.GetResultParts(partNames);
                    else throw new NotSupportedException();
                    //
                    foreach (var part in parts)
                    {
                        numOfSelectedTreeNodes = _modelTree.SelectBasePart(e, modifierKeys, part, false);
                        count++;
                        //
                        if (count == 1 && modifierKeys == Keys.None) modifierKeys |= Keys.Shift;
                    }
                    _modelTree.UpdateHighlight();
                    //
                    if (numOfSelectedTreeNodes > 0 && e.Button == MouseButtons.Right)
                    {
                        _modelTree.ShowContextMenu(_vtk, e.X, _vtk.Height - e.Y);
                    }
                }
            }
        }
        public void SelectFirstComponentOfFirstFieldOutput()
        {
            InvokeIfRequired(_modelTree.SelectFirstComponentOfFirstFieldOutput);
        }
        //
        public void SetNumberOfModelUserKeywords(int numOfUserKeywords)
        {
            InvokeIfRequired(_modelTree.SetNumberOfUserKeywords, numOfUserKeywords);
        }
        
        #endregion  ################################################################################################################

        // Output
        public void WriteDataToOutput(string data)
        {
            if (data == null) return;
            // 20 chars is an empty line with date
            if (data.Length == 0 && (outputLines.Length > 0 && outputLines.Last().Length == 20)) return;
            //
            InvokeIfRequired(() =>
            {
                data = data.Replace("\r\n", "\n");
                data = data.Replace('\r', '\n');
                string[] lines = data.Split('\n');
                //
                foreach (var line in lines)
                {
                    WriteLineToOutputWithDate(line);
                    //
                    Console.WriteLine(line);
                }
                //
                timerOutput.Start();
            });
        }
        private void WriteLineToOutputWithDate(string data)
        {
            if (outputLines != null)
            {
                int numColDate = 20;
                int numCol = (int)((tbOutput.Width - 60) / tbOutput.TextCharWidth);
                int numRows = 100;      // number of displayed lines
                //
                List<string> lines = new List<string>(outputLines);
                List<string> wrappedLines = new List<string>();
                //
                while (numColDate + data.Length > numCol)
                {
                    wrappedLines.Add(data.Substring(0, numCol - numColDate) + "...");
                    data = data.Substring(numCol - numColDate);
                }
                wrappedLines.Add(data);
                //
                foreach (var wrappedLine in wrappedLines)
                {
                    lines.Add(DateTime.Now.ToString("MM/dd/yy HH:mm:ss").PadRight(numColDate) + wrappedLine);
                }
                //
                int firstLine = Math.Max(0, lines.Count - numRows);
                int numLines = Math.Min(lines.Count, numRows);
                //
                outputLines = new string[numLines];
                Array.Copy(lines.ToArray(), firstLine, outputLines, 0, numLines);
            }
        }

        #region Invoke  ############################################################################################################
        // Invoke
        public T InvokeIfRequired<T>(Func<T> func)
        {
            if (this.InvokeRequired)
            {
                return (T)this.Invoke(func);
            }
            else
            {
                return func();
            }
        }
        public void InvokeIfRequired(Action action)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate () { action(); });
            }
            else
            {
                action();
            }
        }
        public void InvokeIfRequired<T>(Action<T> action, T parameter)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate() { action(parameter); });
            }
            else
            {
                action(parameter);
            }
        }
        public void InvokeIfRequired<T1,T2>(Action<T1, T2> action, T1 parameter1, T2 parameter2)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate() { action(parameter1, parameter2); });
            }
            else
            {
                action(parameter1, parameter2);
            }
        }
        public void InvokeIfRequired<T1, T2, T3>(Action<T1, T2, T3> action, T1 parameter1, T2 parameter2, T3 parameter3)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate() { action(parameter1, parameter2, parameter3); });
            }
            else
            {
                action(parameter1, parameter2, parameter3);
            }
        }
        public void InvokeIfRequired<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action, T1 parameter1, T2 parameter2, T3 parameter3, 
                                                     T4 parameter4)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate() { action(parameter1, parameter2, parameter3, parameter4); });
            }
            else
            {
                action(parameter1, parameter2, parameter3, parameter4);
            }
        }
        public void InvokeIfRequired<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action, T1 parameter1, T2 parameter2,
                                     T3 parameter3, T4 parameter4, T5 parameter5)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate() { action(parameter1, parameter2, parameter3, parameter4, parameter5); });
            }
            else
            {
                action(parameter1, parameter2, parameter3, parameter4, parameter5);
            }
        }
        public void InvokeIfRequired<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action, T1 parameter1, T2 parameter2, 
                                     T3 parameter3, T4 parameter4, T5 parameter5, T6 parameter6)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate () { action(parameter1, parameter2, parameter3, parameter4, 
                                                         parameter5, parameter6); });
            }
            else
            {
                action(parameter1, parameter2, parameter3, parameter4, parameter5, parameter6);
            }
        }
        public void InvokeIfRequired<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action, T1 parameter1, 
                                     T2 parameter2, T3 parameter3, T4 parameter4, T5 parameter5, T6 parameter6, T7 parameter7)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate () { action(parameter1, parameter2, parameter3, parameter4, parameter5,
                                                         parameter6, parameter7); });
            }
            else
            {
                action(parameter1, parameter2, parameter3, parameter4, parameter5, parameter6, parameter7);
            }
        }
        public void InvokeIfRequired<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 parameter1,
                                     T2 parameter2, T3 parameter3, T4 parameter4, T5 parameter5, T6 parameter6, T7 parameter7,
                                     T8 parameter8)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate () {
                    action(parameter1, parameter2, parameter3, parameter4, parameter5,
                    parameter6, parameter7, parameter8);
                });
            }
            else
            {
                action(parameter1, parameter2, parameter3, parameter4, parameter5, parameter6, parameter7, parameter8);
            }
        }
        public void InvokeIfRequired<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action,
                                     T1 parameter1, T2 parameter2, T3 parameter3, T4 parameter4, T5 parameter5, T6 parameter6,
                                     T7 parameter7, T8 parameter8, T9 parameter9)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate () {
                    action(parameter1, parameter2, parameter3, parameter4, parameter5,
                    parameter6, parameter7, parameter8, parameter9);
                });
            }
            else
            {
                action(parameter1, parameter2, parameter3, parameter4, parameter5, parameter6, parameter7, parameter8, parameter9);
            }
        }



































        #endregion  ################################################################################################################

        #endregion

        private void tsmiTest_Click(object sender, EventArgs e)
        {
            try
            {
                //_vtk.SwitchLights();
                //_controller.TestCreateSurface();
                //AnimateModel58();
                //_vtk.Export(GetFileNameToSaveAs());
                //TestSpring();
                //TestSuperposition();
                //TestNormals();
                //TestWearResults();
                //TestMmg();
                //TestGmshReadMesh();
                //TestDefeature();
                //AnimateRotation();
                //TestParameterization();
                //TestImportedStLoad();
                Test3MF();
            }
            catch
            {

            }
        }
        private void Test3MF()
        {
            string fileName = @"C:\Temp\Test.3mf";
            vtkMaxActorData[] vtkMaxActorDatas = _vtk.GetVtkMaxActorData();
            vtkMaxLookupTable lookupTable = _vtk.GetLookupTable();
            double[] closestUpView = GetClosestUpView();
            ThreeMFFileWriter.Write(fileName, vtkMaxActorDatas, lookupTable, 1, closestUpView);
        }
        private void TestImportedStLoad()
        {
            int n = 32;
            int count = 0;
            double delta = 2.0 / (n - 1);
            double[][] coor = new double[n * n][];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    coor[count] = new double[] { -1 + i * delta, -1 + j * delta, 0 };
                    count++;
                }
            }
            //
            double v1 = -1;
            double v2 = -2;
            double v3 = -3;
            double v4 = -4;
            double x;
            double y;

            double minX = 13.15;
            double maxX = minX + 300;
            double minY = 20.87;
            double maxY = minY + 300;
            double z = 129.5;

            count = 0;
            double[] values = new double[n * n];
            string[] lines = new string[n * n];
            for (int i = 0; i < values.Length; i++)
            {
                x = coor[count][0];
                y = coor[count][1];
                values[count] = v1 * 0.25 * (1 - x) * (1 - y) +
                                v2 * 0.25 * (1 + x) * (1 - y) +
                                v3 * 0.25 * (1 + x) * (1 + y) +
                                v4 * 0.25 * (1 - x) * (1 + y);
                //
                coor[count][0] = minX + (maxX - minX) * (x + 1) / 2;
                coor[count][1] = minY + (maxY - minY) * (y + 1) / 2;
                coor[count][2] = z;
                //
                lines[count] = string.Format("{0} {1} {2} {3} {4} {5}{6}", coor[count][0], coor[count][1], coor[count][2],
                                                                           0, 0, values[count], Environment.NewLine);
                //
                count++;

            }

            File.WriteAllLines(@"c:\Temp\load.txt", lines);
        }
        private void TestMmg()
        {
            string partName1 = "Solid_part-1";
            string partName2 = "Solid_part-2";
            string fileName = @"C:\Temp\mmg\tmp.sol";
            //
            //_controller.ExportSignedDistance(fileName, partName2, partName1);
            //
            if (_controller.CurrentResult != null && _controller.CurrentResult.Mesh != null)
            {
                SetResultNames();
                // Reset the previous step and increment
                SetAllStepAndIncrementIds();
                // Set last increment
                SetDefaultStepAndIncrementIds();
                // Show the selection in the results tree
                SelectFirstComponentOfFirstFieldOutput();
            }
            // Set the representation which also calls Draw
            _controller.ViewResultsType = ViewResultsTypeEnum.ColorContours;  // Draw
            //
            SetMenuAndToolStripVisibility();
        }
        private void TestWearResults()
        {
            string fileName = @"C:\Temp\Ignatijev\Model8\Analysis-1.frd";
            _controller.ReadFrdFileAsWear(fileName);
        }
        private void TestNormals()
        {
            string partName = "Shell_part-1";
            _controller.SuppressExplodedView(new string[] { partName });
            //
            MeshPart part = _controller.GetModelPart(partName);
            GeometryPart geometryPart = _controller.GetGeometryPart(partName);
            //
            Dictionary<int, FeNode[]> faceIdNodes = new Dictionary<int, FeNode[]>();
            //
            HashSet<int> nodeIds;
            List<FeNode> nodes;
            for (int i = 0; i < part.Visualization.FaceCount; i++)
            {
                nodes = new List<FeNode>();
                nodeIds = part.Visualization.GetNodeIdsForSurfaceId(i);
                foreach (var nodeId in nodeIds)
                {
                    nodes.Add(_controller.Model.Mesh.Nodes[nodeId]);
                }
                //
                faceIdNodes.Add(FeMesh.GmshTopologyId(i, part.PartId), nodes.ToArray());
            }
            //
            string workDirectory = _controller.Settings.GetWorkDirectory();
            //
            if (workDirectory == null || !Directory.Exists(workDirectory))
            {
                MessageBoxes.ShowWorkDirectoryError();
                return;
            }
            string brepFileName = Path.Combine(workDirectory, Globals.BrepFileName);
            //
            if (File.Exists(brepFileName)) File.Delete(brepFileName);
            //
            File.WriteAllText(brepFileName, geometryPart.CADFileData);
            GmshData gmshData = new GmshData();
            gmshData.GeometryFileName = brepFileName;
            gmshData.FaceIdNodes = faceIdNodes;
            _controller.Model.Geometry.GetPartTopologyForGmsh(geometryPart.Name, ref gmshData);
            _controller.ResumeExplodedViews(false);
            //
            GmshAPI gmshAPI = new GmshAPI(gmshData, WriteDataToOutput);
            string error = gmshAPI.GetOccNormals();
            Dictionary<int, List<Vec3D>> normals = gmshAPI.GmshData.NodeIdNormals;
        }
        private void TestGmshReadMesh()
        {
            string workDirectory = _controller.Settings.GetWorkDirectory();
            //

            if (workDirectory == null || !Directory.Exists(workDirectory))
            {
                MessageBoxes.ShowWorkDirectoryError();
                return;
            }
            string meshFileName = Path.Combine(workDirectory, Globals.GmshMeshFileName);
            //
            if (File.Exists(meshFileName)) File.Delete(meshFileName);
            //
            GmshMshFileWriter.Write(meshFileName, _controller.Model.Mesh);
            //
            GmshData gmshData = new GmshData();
            gmshData.MeshFileName = meshFileName;
            //
            GmshAPI gmshAPI = new GmshAPI(gmshData, null);
            string error = gmshAPI.GetElementQualities();
        }
        private void TestDefeature()
        {
            _controller.Defeature(null);
        }
        private void TestParameterization()
        {
            _controller.ViewParameterization();
            _vtk.Invalidate();
        }
        private void TestSuperposition()
        {
            _controller.CurrentResult.TestSuperposition();
        }
        private void TestEquation()
        {
            EquationContainer equationContainer1;
            EquationContainer equationContainer2;
            for (int i = 0; i < 10000; i++)
            {
                equationContainer1 = new EquationContainer(typeof(StringLengthConverter), 1.4);
                equationContainer2 = new EquationContainer(typeof(StringLengthConverter), 1);
                EquationContainer.SetAndCheck(ref equationContainer2, equationContainer1, null, false);
            }
        }
        private void TestSpring()
        {
            int[] directions;
            PointSpring pointSpring;
            PointSpringData pointSpringData;
            List<CaeModel.Constraint> constraints = new List<CaeModel.Constraint>();
            for (int i = 0; i < 10000; i++)
            {
                pointSpring = new PointSpring("spring", 101, 1, 2, 3, false, false);
                directions = pointSpring.GetSpringDirections();
            }
            directions = null;
            //
            for (int i = 0; i < 10000; i++)
            {
                pointSpringData = new PointSpringData("spring", 101, 1, 2, 3);
                directions = pointSpringData.GetSpringDirections();
            }
        }
        private void AnimateRotation()
        {
            tbOutput.Clear();
            Thread.Sleep(1000);
            int nFrames = 360;
            double delta = 360.0 / nFrames;
            double angle;
            double alpha;
            string[] partNames = new string[] { "Solid_part-1" };
            //
            //AnimateTransparency("Solid_part-1", 230, 20, 10);
            string fileName = @"c:\Temp\screen";
            _controller.SetTransparencyForGeometryParts(partNames, 255);
            _vtk.RenderToPNG(fileName + 0.ToString().PadLeft(3, '0') + ".png");
            //
            for (int i = 1; i <= nFrames; i++)
            {
                //_vtk.Rotate(-delta, 0, 0);
                _vtk.Rotate(0, 0, delta);
                angle = i * delta;

                if (i == nFrames / 4)
                    _controller.SetTransparencyForGeometryParts(partNames, (byte)100);
                else if (i == nFrames / 2)
                    _controller.SetTransparencyForGeometryParts(partNames, (byte)24);
                else if (i == 3 * nFrames / 4)
                    _controller.SetTransparencyForGeometryParts(partNames, (byte)100);



                //if (angle > 80 && angle < 180)
                //{
                //    alpha =  (angle - 80) / (180 - 80);    // 0...1
                //    alpha = (1 - alpha) * 250;
                //    _controller.SetTransparencyForGeometryParts(partNames, (byte)alpha);
                //}
                //if (angle > 270)
                //{
                //    alpha = (angle - 270) / (90);    // 0...1
                //    alpha = (alpha) * 250;
                //    _controller.SetTransparencyForGeometryParts(partNames, (byte)alpha);
                //}

                //Thread.Sleep(10);
                //Application.DoEvents();
                _vtk.RenderToPNG(fileName + i.ToString().PadLeft(3, '0') + ".png");
            }


            //
            //_vtk.AnimateZoomToFactor(3000, 100);
            //
            //if (true)
            //{
            //    int alphaStart = 230;
            //    int alphaEnd = 50;
            //    int alphaStep = 5;
            //    AnimateTransparency("Shell_part-7", alphaStart, alphaEnd, alphaStep);
            //    AnimateTransparency("Shell_part-6", alphaStart, alphaEnd, alphaStep);
            //    AnimateTransparency("Shell_part-11", alphaStart, alphaEnd, alphaStep);
            //    AnimateTransparency("Shell_part-1", alphaStart, alphaEnd, alphaStep);
            //    AnimateTransparency("Shell_part-10", alphaStart, alphaEnd, alphaStep);
            //    AnimateTransparency("Shell_part-2", alphaStart, alphaEnd, alphaStep);
            //    AnimateTransparency("Shell_part-14", alphaStart, alphaEnd, alphaStep);
            //}
            //Thread.Sleep(1000);
            //_vtk.AnimateZoomToFactor(6000, 1500);
            ////
            //Thread.Sleep(1000);
            //_controller.TurnExplodedViewOnOff(true, 1500);
            ////
            //Thread.Sleep(1000);
            //_controller.TurnExplodedViewOnOff(true, 1500);
            ////
            //Thread.Sleep(1000);
            //_vtk.AnimateZoomToFactor(3000, 1500);
        }
        private void AnimateModel58()
        {
            tbOutput.Clear();
            Thread.Sleep(3000);
            //
            //_vtk.AnimateZoomToFactor(3000, 100);
            //
            if (true)
            {
                int alphaStart = 230;
                int alphaEnd = 50;
                int alphaStep = 5;
                AnimateTransparency("Shell_part-7", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-6", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-11", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-1", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-10", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-2", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-14", alphaStart, alphaEnd, alphaStep);
            }
            //Thread.Sleep(1000);
            //_vtk.AnimateZoomToFactor(6000, 1500);
            ////
            //Thread.Sleep(1000);
            //_controller.TurnExplodedViewOnOff(true, 1500);
            ////
            //Thread.Sleep(1000);
            //_controller.TurnExplodedViewOnOff(true, 1500);
            ////
            //Thread.Sleep(1000);
            //_vtk.AnimateZoomToFactor(3000, 1500);
        }
        private void AnimateSierra()
        {
            tbOutput.Clear();
            Thread.Sleep(3000);
            //
            _vtk.AnimateZoomToFactor(800, 100);
            //
            if (true)
            {
                int alphaStart = 230;
                int alphaEnd = 50;
                int alphaStep = 5;
                AnimateTransparency("Shell_part-16", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-4", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-1", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-17", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-12", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-9", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-7", alphaStart, alphaEnd, alphaStep);
                AnimateTransparency("Shell_part-13", alphaStart, alphaEnd, alphaStep);
            }
            //Thread.Sleep(1000);
            //_vtk.AnimateZoomToFactor(2000, 1500);
            //
            //Thread.Sleep(1000);
            //_controller.TurnExplodedViewOnOff(true, 1500);
            //
            //Thread.Sleep(1000);
            //_controller.TurnExplodedViewOnOff(true, 1500);
            //
            //Thread.Sleep(1000);
            //_vtk.AnimateZoomToFactor(800, 1500);
        }
        private void AnimateTransparency(string partName, int alphaFrom, int alphaTo, int alphaStep)
        {
            string[] partNames = new string[] { partName };

            if (alphaFrom > alphaTo)
            {
                for (int i = alphaFrom; i >= alphaTo; i -= alphaStep)
                {
                    //_controller.SetTransparencyForResultParts(partNames, (byte)i);
                    _controller.SetTransparencyForGeometryParts(partNames, (byte)i);
                    Application.DoEvents();
                }
            }
            
        }
        private void timerTest_Tick(object sender, EventArgs e)
        {
            //TestAnimation();
            //TestSelection1();
            TestSelection2();
        }
        private void TestAnimation()
        {
            try
            {
                timerTest.Interval = 10;
                //timerTest.Stop();

                string[] names = new string[] { CaeResults.FOFieldNames.Stress, CaeResults.FOFieldNames.Disp };
                string[] components = new string[] { "SZZ", "D2" };

                CaeResults.FieldData currentData = _controller.CurrentFieldData;
                int i = 0;
                if (currentData.Component == components[i]) i++;

                int len = names.Length;

                SetFieldData(names[i % len], components[i % len], 1, 1);
            }
            catch
            { }
        }
        private void TestSelection1()
        {
            try
            {
                timerTest.Interval = 100;
                //
                string[] allPartNames = _controller.GetGeometryPartNames();
                HashSet<string> selectedPartNames = new HashSet<string>();
                //
                Random rand = new Random();
                for (int i = 0; i < 100; i++)
                {
                    selectedPartNames.Add(allPartNames[(int)(rand.NextDouble() * allPartNames.Length - 1)]);
                }
                Clear3DSelection();
                _controller.HighlightGeometryParts(selectedPartNames.ToArray());
                //
                Application.DoEvents();
            }
            catch
            { }
        }
        private void TestSelection2()
        {
            try
            {
                timerTest.Interval = 50;
                //
                Random rand = new Random();
                int x1 = _vtk.Width / 4 + rand.Next(_vtk.Width / 2);
                int y1 = _vtk.Height / 4 + rand.Next(_vtk.Height / 2);

                //x1 = 523;
                //y1 = 421;

                Clear3DSelection();
                _vtk.Pick(x1, y1, false, 0, 0);
                //
                Application.DoEvents();
            }
            catch
            { }
        }
        internal void tsmiAdvisor_Click(object sender, EventArgs e)
        {
            if (!_controller.ModelInitialized) return;
            // Change the wizard check state
            tsmiAdvisor.Checked = !tsmiAdvisor.Checked;
            // Add wizard panel
            if (tsmiAdvisor.Checked == true)
            {
                Control parent = panelControl.Parent;
                if (parent == splitContainer2.Panel1)
                {
                    // First remove the vtk control and panel border
                    RemoveRendererControls(parent);
                    parent.Controls.Remove(panelControl);
                    // Split container
                    SplitContainer splitContainer = new SplitContainer();
                    splitContainer.FixedPanel = FixedPanel.Panel2;
                    splitContainer.Dock = DockStyle.Fill;
                    parent.Controls.Add(splitContainer);
                    // Set the Panel 2 size - min 100 max 300
                    splitContainer.SplitterDistance = Math.Max(100, Math.Max(parent.Width - 300, (int)(parent.Width * 0.8)));
                    // Panel 1 - LEFT
                    AddRendererControls(splitContainer.Panel1);
                    splitContainer.Panel1.Controls.Add(panelControl);
                    panelControl.SendToBack();
                    // Update vtk control size
                    UpdateVtkControlSize();
                    // Panel 2 - RIGHT
                    _advisorControl = AdvisorCreator.CreateControl(this);
                    //
                    splitContainer.Panel2.Controls.Add(_advisorControl);
                    _advisorControl.Dock = DockStyle.Fill;
                    _advisorControl.UpdateDesign();
                    _advisorControl.AutoScroll = true;
                }
            }
            // Remove wizard panel
            else
            {
                Control parent = panelControl.Parent;
                if (parent is SplitterPanel && parent != splitContainer2.Panel1)
                {
                    // First remove the vtk control and panel border
                    RemoveRendererControls(parent);
                    parent.Controls.Remove(panelControl);
                    // Remove added split container
                    splitContainer2.Panel1.Controls.Clear();
                    _advisorControl = null;
                    // Add controls back
                    splitContainer2.Panel1.Controls.Add(panelControl);
                    AddRendererControls(splitContainer2.Panel1);
                    panelControl.SendToBack();
                    // Update vtk control size
                    UpdateVtkControlSize();
                }
            }
        }

        
    }
}
