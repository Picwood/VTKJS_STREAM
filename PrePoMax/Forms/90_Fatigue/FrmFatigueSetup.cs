// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeJob;
using CaeGlobals;
using CaeMesh;
using PrePoMax.Fatigue;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace PrePoMax.Forms
{
    public class FrmFatigueSetup : Form
    {
        private readonly Controller _controller;
        private readonly FatigueSetup _setup;
        private readonly string _singleSetupPage;
        private readonly TabControl _tabs;
        private readonly TabPage _tpModel;
        private readonly TabPage _tpLoads;
        private readonly TabPage _tpSn;
        private readonly TextBox _tbAnalysis;
        private readonly TextBox _tbInp;
        private readonly TextBox _tbFrd;
        private readonly NumericUpDown _nudStep;
        private readonly TextBox _tbMaterials;
        private readonly NumericUpDown _nudEdge;
        private readonly CheckBox _cbKeepInterfaces;
        private readonly CheckBox _cbRequireMatch;
        private readonly Label _lbModelStatus;
        private readonly DataGridView _dgvUnitLoads;
        private readonly DataGridView _dgvModelParts;
        private readonly ComboBox _cbLoadsFormat;
        private readonly TextBox _tbLoadsFile;
        private readonly ComboBox _cbReduction;
        private readonly ComboBox _cbPlotType;
        private readonly NumericUpDown _nudDelM;
        private readonly NumericUpDown _nudDelCycles;
        private readonly CheckBox _cbShowDataMarkers;
        private readonly LoadPlotPanel _loadPlotPanel;
        private readonly DataGridView _dgvLoadCases;
        private readonly DataGridView _dgvComponents;
        private readonly TextBox _tbGeneratedLoadName;
        private readonly TextBox _tbGeneratedLoadUnit;
        private readonly ComboBox _cbWaveformType;
        private readonly NumericUpDown _nudMeanLoad;
        private readonly NumericUpDown _nudLoadAmplitude;
        private readonly NumericUpDown _nudLoadFrequency;
        private readonly NumericUpDown _nudPhaseShift;
        private readonly NumericUpDown _nudTotalCycles;
        private readonly NumericUpDown _nudSamplingRate;
        private readonly NumericUpDown _nudLoadRepetitions;
        private readonly ComboBox _cbGeneratedLoadResultStep;
        private readonly FlowLayoutPanel _flpGeneratedLoads;
        private int _editingGeneratedLoadIndex;
        private readonly ListBox _lbSnDatabase;
        private readonly TextBox _tbSnName;
        private readonly TextBox _tbSnComments;
        private readonly NumericUpDown _nudDs1;
        private readonly NumericUpDown _nudN1;
        private readonly NumericUpDown _nudN2;
        private readonly TextBox _tbDs2;
        private readonly NumericUpDown _nudM1;
        private readonly NumericUpDown _nudM2;
        private readonly NumericUpDown _nudGf;
        private readonly NumericUpDown _nudAllowableDamage;
        private readonly NumericUpDown _nudMinS;
        private readonly NumericUpDown _nudMaxS;
        private readonly ComboBox _cbMeanMode;
        private readonly NumericUpDown _nudM;
        private readonly NumericUpDown _nudRe;
        private readonly NumericUpDown _nudRm;
        private readonly CheckBox _cbObserveYieldLines;
        private readonly CheckBox _cbExtrapolateCompression;
        private readonly NumericUpDown _nudDal;
        private readonly NumericUpDown _nudPs;
        private readonly NumericUpDown _nudStd;
        private readonly ComboBox _cbCalculationBackend;
        private readonly ComboBox _cbFatigueStress;
        private readonly ComboBox _cbCycleCounter;
        private readonly ComboBox _cbMultiaxialCriterion;
        private readonly NumericUpDown _nudNormalSearchPlanes;
        private readonly NumericUpDown _nudInclinedSearchPlanes;
        private readonly NumericUpDown _nudMultiaxialK1;
        private readonly NumericUpDown _nudMultiaxialK2;
        private readonly SnCurvePlotPanel _snCurvePlotPanel;
        private readonly MeanStressPlotPanel _meanStressPlotPanel;

        public FrmFatigueSetup(Controller controller, FatigueSetup setup)
            : this(controller, setup, null)
        {
        }

        public FrmFatigueSetup(Controller controller, FatigueSetup setup, string setupPageName)
        {
            _controller = controller;
            _setup = setup;
            _singleSetupPage = NormalizeSetupPageName(setupPageName);
            //
            Text = _singleSetupPage == null ? "Fatigue setup" : "Setup " + _singleSetupPage.ToLowerInvariant();
            StartPosition = FormStartPosition.CenterParent;
            SetInitialWindowSize();
            //
            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tpModel = new TabPage("Model");
            _tpLoads = new TabPage("Loads");
            _tpSn = new TabPage("SN Curve");
            _tabs.TabPages.AddRange(new[] { _tpModel, _tpLoads, _tpSn });
            //
            _tbAnalysis = new TextBox();
            _tbInp = new TextBox();
            _tbFrd = new TextBox();
            _nudStep = CreateNumeric(1, 100000, 1, 0);
            _tbMaterials = new TextBox();
            _nudEdge = CreateNumeric(0, 180, 40, 1);
            _cbKeepInterfaces = new CheckBox { Text = "Keep internal faces at material interfaces" };
            _cbRequireMatch = new CheckBox { Text = "Require analysis name to match opened FRD result name" };
            _lbModelStatus = new Label { AutoSize = true };
            _dgvUnitLoads = new DataGridView();
            _dgvModelParts = new DataGridView();
            //
            _cbLoadsFormat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cbLoadsFormat.Items.AddRange(new object[] { "XLS", "SOT", "MAT" });
            _tbLoadsFile = new TextBox();
            _cbReduction = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cbReduction.Items.AddRange(new object[]
            {
                "Full time series", "All turning points", "Race track filter 5%", "Race track filter 10%",
                "Race track filter 20%", "Race track filter 30%", "Race track filter 40%", "Single cycle DEL"
            });
            _cbPlotType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cbPlotType.Items.AddRange(new object[] { "Load-time series", "Frequency content (FFT)" });
            _nudDelM = CreateNumeric(0.0001m, 1000, 4, 4);
            _nudDelCycles = CreateNumeric(1, 1000000000, 1, 0);
            _cbShowDataMarkers = new CheckBox { Text = "Show data markers" };
            _loadPlotPanel = new LoadPlotPanel { Dock = DockStyle.Fill };
            _dgvLoadCases = new DataGridView();
            _dgvComponents = new DataGridView();
            _tbGeneratedLoadName = new TextBox { Text = "Fx" };
            _tbGeneratedLoadUnit = new TextBox { Text = "kN" };
            _cbWaveformType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cbWaveformType.Items.AddRange(new object[] { "Sinusoidal", "Triangular", "Square" });
            _cbWaveformType.SelectedIndex = 0;
            _nudMeanLoad = CreateNumeric(-100000000, 100000000, 0, 6);
            _nudLoadAmplitude = CreateNumeric(0, 100000000, 1, 6);
            _nudLoadFrequency = CreateNumeric(0.000001m, 1000000, 1, 6);
            _nudPhaseShift = CreateNumeric(-360000, 360000, 0, 3);
            _nudTotalCycles = CreateNumeric(1, 1000000000, 10, 0);
            _nudSamplingRate = CreateNumeric(2, 1000000, 1000, 0);
            _nudLoadRepetitions = CreateNumeric(1, 1000000000, 1, 0);
            _cbGeneratedLoadResultStep = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _flpGeneratedLoads = new FlowLayoutPanel();
            _editingGeneratedLoadIndex = -1;
            //
            _lbSnDatabase = new ListBox();
            _lbSnDatabase.Items.Add("Default");
            _lbSnDatabase.SelectedIndex = 0;
            _tbSnName = new TextBox();
            _tbSnComments = new TextBox();
            _nudDs1 = CreateNumeric(0, 100000000, 100, 3);
            _nudN1 = CreateNumeric(1, 1000000000000m, 2000000, 0);
            _nudN2 = CreateNumeric(1, 1000000000000m, 10000000, 0);
            _tbDs2 = new TextBox { ReadOnly = true };
            _nudM1 = CreateNumeric(0, 100, 3, 3);
            _nudM2 = CreateNumeric(0, 100, 5, 3);
            _nudGf = CreateNumeric(0.0001m, 1000, 1.35m, 4);
            _nudAllowableDamage = CreateNumeric(0.000001m, 1000000, 1, 4);
            _nudMinS = CreateNumeric(0, 100000000, 0, 3);
            _nudMaxS = CreateNumeric(0, 100000000, 10000, 3);
            _cbMeanMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cbMeanMode.Items.AddRange(new object[]
            {
                "None", "Linear", "Bilinear", "Soderberg", "Modified Goodman", "Gerber parabola",
                "Smith-Watson-Topper", "60% Compression", "IIW low RS", "IIW medium RS", "FKM"
            });
            _nudM = CreateNumeric(-1000, 1000, 0.2m, 4);
            _nudRe = CreateNumeric(0, 1000000, 235, 3);
            _nudRm = CreateNumeric(0, 1000000, 370, 3);
            _cbObserveYieldLines = new CheckBox { Text = "Observe yield-lines" };
            _cbExtrapolateCompression = new CheckBox { Text = "Extrapolate in compression" };
            _nudDal = CreateNumeric(0, 1000000, 1, 4);
            _nudPs = CreateNumeric(0, 100, 97.7m, 3);
            _nudStd = CreateNumeric(0, 100, 0.2m, 4);
            _cbCalculationBackend = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cbCalculationBackend.Items.AddRange(new object[] { "MATLAB", "Python CUDA" });
            _cbFatigueStress = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cbFatigueStress.Items.AddRange(new object[] { "Pnmax", "Signed von Mises", "Tresca", "Principal stress" });
            _cbCycleCounter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cbCycleCounter.Items.AddRange(new object[] { "Reservoir", "Rainflow" });
            _cbMultiaxialCriterion = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cbMultiaxialCriterion.Items.AddRange(new object[] { "Normal stress", "Findley", "Dang Van" });
            _nudNormalSearchPlanes = CreateNumeric(1, 10000, 36, 0);
            _nudInclinedSearchPlanes = CreateNumeric(1, 10000, 1, 0);
            _nudMultiaxialK1 = CreateNumeric(0, 1000, 0.3m, 4);
            _nudMultiaxialK2 = CreateNumeric(0, 1000, 1, 4);
            _snCurvePlotPanel = new SnCurvePlotPanel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            _meanStressPlotPanel = new MeanStressPlotPanel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            //
            BuildModelTab();
            BuildLoadsTab();
            BuildSnTab();
            //
            if (_singleSetupPage == null) Controls.Add(_tabs);
            else Controls.Add(DetachSetupPageControl(_singleSetupPage));
            Controls.Add(CreateButtonsPanel());
            //
            LoadFromSetup();
        }

        private Panel CreateButtonsPanel()
        {
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(10, 8, 10, 8) };
            //
            if (_singleSetupPage == null || _singleSetupPage == "Model")
            {
                Button btnAuto = new Button { Text = "Detect", Width = 90, Dock = DockStyle.Left };
                btnAuto.Click += btnAuto_Click;
                footer.Controls.Add(btnAuto);
            }
            //
            FlowLayoutPanel rightButtons = new FlowLayoutPanel();
            rightButtons.Dock = DockStyle.Right;
            rightButtons.FlowDirection = FlowDirection.LeftToRight;
            rightButtons.WrapContents = false;
            rightButtons.AutoSize = true;
            rightButtons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            rightButtons.Padding = new Padding(0);
            rightButtons.Margin = new Padding(0);
            //
            Button btnOk = new Button { Text = "OK", Width = 90, Height = 28, DialogResult = DialogResult.None };
            Button btnCancel = new Button { Text = "Cancel", Width = 90, Height = 28, DialogResult = DialogResult.Cancel };
            btnOk.Click += btnOk_Click;
            rightButtons.Controls.Add(btnOk);
            rightButtons.Controls.Add(btnCancel);
            footer.Controls.Add(rightButtons);
            //
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            return footer;
        }

        private void SetInitialWindowSize()
        {
            if (_singleSetupPage == "Loads")
            {
                MinimumSize = new Size(1180, 760);
                Size = new Size(1400, 900);
            }
            else if (_singleSetupPage == "Model")
            {
                MinimumSize = new Size(900, 620);
                Size = new Size(1050, 720);
            }
            else if (_singleSetupPage == "SN Curve")
            {
                MinimumSize = new Size(1180, 760);
                Size = new Size(1420, 900);
            }
            else
            {
                MinimumSize = new Size(980, 700);
                Size = new Size(1120, 760);
            }
        }

        private static string NormalizeSetupPageName(string pageName)
        {
            if (string.IsNullOrWhiteSpace(pageName)) return null;
            if (string.Equals(pageName, "Loads", StringComparison.OrdinalIgnoreCase)) return "Loads";
            if (string.Equals(pageName, "SN Curve", StringComparison.OrdinalIgnoreCase)) return "SN Curve";
            return "Model";
        }

        private Control DetachSetupPageControl(string pageName)
        {
            TabPage page = GetSetupPage(pageName);
            Control content = page.Controls.Count > 0 ? page.Controls[0] : new Panel();
            page.Controls.Remove(content);
            content.Dock = DockStyle.Fill;
            return content;
        }

        private TabPage GetSetupPage(string pageName)
        {
            if (string.Equals(pageName, "Loads", StringComparison.OrdinalIgnoreCase)) return _tpLoads;
            if (string.Equals(pageName, "SN Curve", StringComparison.OrdinalIgnoreCase)) return _tpSn;
            return _tpModel;
        }

        public void SelectSetupPage(string pageName)
        {
            _tabs.SelectedTab = GetSetupPage(pageName);
        }

        private void BuildModelTab()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(10);
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            //
            TableLayoutPanel context = new TableLayoutPanel();
            context.Dock = DockStyle.Fill;
            context.Padding = new Padding(10);
            context.ColumnCount = 2;
            context.RowCount = 5;
            context.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            context.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < context.RowCount; i++) context.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            _tbAnalysis.ReadOnly = true;
            _tbInp.ReadOnly = true;
            _tbFrd.ReadOnly = true;
            AddContextRow(context, 0, "Result context", _tbAnalysis);
            AddContextRow(context, 1, "Generated INP", _tbInp);
            AddContextRow(context, 2, "Generated FRD", _tbFrd);
            AddContextRow(context, 3, "Result step", _nudStep);
            context.Controls.Add(_lbModelStatus, 1, 4);
            GroupBox contextGroup = new GroupBox { Text = "Current PMX model and results", Dock = DockStyle.Fill };
            contextGroup.Controls.Add(context);
            //
            _dgvModelParts.Dock = DockStyle.Fill;
            _dgvModelParts.AllowUserToAddRows = false;
            _dgvModelParts.AllowUserToDeleteRows = false;
            _dgvModelParts.ReadOnly = true;
            _dgvModelParts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _dgvModelParts.MultiSelect = true;
            _dgvModelParts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _dgvModelParts.Columns.Add("Name", "Part");
            _dgvModelParts.Columns.Add("Visible", "Visible");
            _dgvModelParts.Columns.Add("Active", "Active");
            _dgvModelParts.Columns.Add("Nodes", "Nodes");
            _dgvModelParts.Columns.Add("Elements", "Elements");
            GroupBox partsGroup = new GroupBox { Text = "Fatigue model parts", Dock = DockStyle.Fill };
            partsGroup.Controls.Add(_dgvModelParts);
            //
            TableLayoutPanel options = new TableLayoutPanel();
            options.Dock = DockStyle.Fill;
            options.Padding = new Padding(10);
            options.ColumnCount = 5;
            options.RowCount = 2;
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            options.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            options.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            options.Controls.Add(new Label { Text = "Materials", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _tbMaterials.Dock = DockStyle.Fill;
            options.Controls.Add(_tbMaterials, 1, 0);
            options.Controls.Add(new Label { Text = "Edge angle", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            _nudEdge.Dock = DockStyle.Left;
            options.Controls.Add(_nudEdge, 1, 1);
            Button btnShow = new Button { Text = "Show", Dock = DockStyle.Fill };
            Button btnHide = new Button { Text = "Hide", Dock = DockStyle.Fill };
            Button btnRefresh = new Button { Text = "Refresh", Dock = DockStyle.Fill };
            btnShow.Click += btnShowModelParts_Click;
            btnHide.Click += btnHideModelParts_Click;
            btnRefresh.Click += (sender, args) => RefreshModelPartsGrid();
            options.Controls.Add(btnShow, 2, 0);
            options.Controls.Add(btnHide, 3, 0);
            options.Controls.Add(btnRefresh, 4, 0);
            options.Controls.Add(_cbKeepInterfaces, 2, 1);
            options.SetColumnSpan(_cbKeepInterfaces, 3);
            //
            layout.Controls.Add(contextGroup, 0, 0);
            layout.Controls.Add(partsGroup, 0, 1);
            layout.Controls.Add(options, 0, 2);
            _tpModel.Controls.Add(layout);
        }

        private void AddContextRow(TableLayoutPanel layout, int row, string label, Control control)
        {
            layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(control, 1, row);
        }

        private void BuildLoadsTab()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(10);
            layout.RowCount = 2;
            layout.ColumnCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            //
            _loadPlotPanel.BorderStyle = BorderStyle.FixedSingle;
            layout.Controls.Add(_loadPlotPanel, 0, 0);
            //
            TableLayoutPanel bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.ColumnCount = 3;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
            bottom.RowCount = 1;
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            //
            _dgvLoadCases.Dock = DockStyle.Fill;
            _dgvLoadCases.AllowUserToAddRows = true;
            _dgvLoadCases.AllowUserToDeleteRows = true;
            _dgvLoadCases.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _dgvLoadCases.Columns.Add("Name", "Name");
            _dgvLoadCases.Columns.Add("Repetitions", "Repetitions");
            DataGridViewComboBoxColumn resultStepColumn = new DataGridViewComboBoxColumn();
            resultStepColumn.Name = "ResultStep";
            resultStepColumn.HeaderText = "Result step";
            resultStepColumn.Items.AddRange(GetAvailableResultStepItems());
            _dgvLoadCases.Columns.Add(resultStepColumn);
            GroupBox cases = new GroupBox { Text = "Load cases", Dock = DockStyle.Fill };
            cases.Controls.Add(_dgvLoadCases);
            //
            _dgvComponents.Dock = DockStyle.Fill;
            _dgvComponents.AllowUserToAddRows = true;
            _dgvComponents.AllowUserToDeleteRows = true;
            _dgvComponents.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _dgvComponents.Columns.Add("Name", "Name");
            _dgvComponents.Columns.Add("Unit", "Unit");
            _dgvComponents.Columns.Add("Min", "Min");
            _dgvComponents.Columns.Add("Max", "Max");
            _dgvComponents.Columns.Add("Mean", "Mean");
            _dgvComponents.Columns.Add("Cycles", "Cycles");
            _dgvComponents.Columns.Add("Del", "DEL");
            _dgvComponents.Columns.Add("Scale", "Scale");
            _dgvComponents.Columns.Add("Offset", "Offset");
            DataGridViewCheckBoxColumn plot = new DataGridViewCheckBoxColumn { Name = "Plot", HeaderText = "Plot" };
            _dgvComponents.Columns.Add(plot);
            foreach (string columnName in new[] { "Min", "Max", "Mean", "Cycles", "Del" })
                _dgvComponents.Columns[columnName].ReadOnly = true;
            GroupBox components = new GroupBox { Text = "Load components", Dock = DockStyle.Fill };
            components.Controls.Add(_dgvComponents);
            //
            TableLayoutPanel options = new TableLayoutPanel();
            options.Dock = DockStyle.Top;
            options.Height = 620;
            options.ColumnCount = 3;
            options.RowCount = 23;
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            for (int i = 0; i < options.RowCount; i++) options.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Button btnBrowse = CreateBrowseButton(_tbLoadsFile, "FATLAB loads (*.xls;*.xlsx;*.mat;*.sot;*.csv;*.txt)|*.xls;*.xlsx;*.mat;*.sot;*.csv;*.txt|All files (*.*)|*.*");
            Button btnUpdate = new Button { Text = "Update", Dock = DockStyle.Fill };
            Button btnSave = new Button { Text = "Save...", Dock = DockStyle.Fill };
            Button btnGenerate = new Button { Text = "Generate", Dock = DockStyle.Fill };
            btnUpdate.Click += btnUpdateLoads_Click;
            btnSave.Click += btnSaveLoads_Click;
            btnGenerate.Click += btnGenerateLoad_Click;
            AddOptionRow(options, 0, "Loads format", _cbLoadsFormat, null);
            AddOptionRow(options, 1, "Loads file", _tbLoadsFile, btnBrowse);
            AddOptionRow(options, 2, "Plot type", _cbPlotType, null);
            AddOptionRow(options, 3, "Cycle counter", new TextBox { Text = "Reservoir", ReadOnly = true }, null);
            AddOptionRow(options, 4, "DEL calculation: m", _nudDelM, null);
            options.Controls.Add(new Label { Text = "@ N=", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 5);
            options.Controls.Add(_nudDelCycles, 1, 5);
            options.Controls.Add(new Label { Text = "cycles", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 5);
            AddOptionRow(options, 6, "Load reduction", _cbReduction, null);
            options.Controls.Add(_cbShowDataMarkers, 0, 7);
            options.SetColumnSpan(_cbShowDataMarkers, 3);
            options.Controls.Add(btnUpdate, 1, 8);
            options.Controls.Add(btnSave, 2, 8);
            Label generateLabel = new Label { Text = "Define load", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font, FontStyle.Bold) };
            options.Controls.Add(generateLabel, 0, 9);
            options.SetColumnSpan(generateLabel, 3);
            AddOptionRow(options, 10, "Name", _tbGeneratedLoadName, null);
            AddOptionRow(options, 11, "Unit", _tbGeneratedLoadUnit, null);
            AddOptionRow(options, 12, "Waveform", _cbWaveformType, null);
            AddOptionRow(options, 13, "Mean", _nudMeanLoad, null);
            AddOptionRow(options, 14, "Amplitude", _nudLoadAmplitude, null);
            AddOptionRow(options, 15, "Frequency", _nudLoadFrequency, null);
            AddOptionRow(options, 16, "Phase", _nudPhaseShift, null);
            AddOptionRow(options, 17, "Cycles", _nudTotalCycles, null);
            AddOptionRow(options, 18, "Samples/s", _nudSamplingRate, null);
            AddOptionRow(options, 19, "Result step", _cbGeneratedLoadResultStep, null);
            options.Controls.Add(new Label { Text = "Repetitions", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 20);
            options.Controls.Add(_nudLoadRepetitions, 1, 20);
            options.Controls.Add(btnGenerate, 2, 20);
            Label cardsLabel = new Label { Text = "Created loads", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font, FontStyle.Bold) };
            options.Controls.Add(cardsLabel, 0, 21);
            options.SetColumnSpan(cardsLabel, 3);
            _flpGeneratedLoads.Dock = DockStyle.Fill;
            _flpGeneratedLoads.FlowDirection = FlowDirection.TopDown;
            _flpGeneratedLoads.WrapContents = false;
            _flpGeneratedLoads.AutoScroll = true;
            _flpGeneratedLoads.Padding = new Padding(0, 2, 0, 0);
            options.Controls.Add(_flpGeneratedLoads, 0, 22);
            options.SetColumnSpan(_flpGeneratedLoads, 3);
            options.RowStyles[22] = new RowStyle(SizeType.Absolute, 150);
            Panel optionsHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            optionsHost.Controls.Add(options);
            GroupBox optionsGroup = new GroupBox { Text = "Load setup", Dock = DockStyle.Fill };
            optionsGroup.Controls.Add(optionsHost);
            //
            bottom.Controls.Add(cases, 0, 0);
            bottom.Controls.Add(components, 1, 0);
            bottom.Controls.Add(optionsGroup, 2, 0);
            layout.Controls.Add(bottom, 0, 1);
            _tpLoads.Controls.Add(layout);
            //
            _cbPlotType.SelectedIndex = 0;
            _dgvLoadCases.SelectionChanged += (sender, args) => RefreshComponentMetrics();
            _dgvComponents.CellValueChanged += (sender, args) => UpdateLoadPlot();
            _dgvComponents.CurrentCellDirtyStateChanged += (sender, args) =>
            {
                if (_dgvComponents.IsCurrentCellDirty) _dgvComponents.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _cbShowDataMarkers.CheckedChanged += (sender, args) => UpdateLoadPlot();
            _nudDelM.ValueChanged += (sender, args) => RefreshComponentMetrics();
            _nudDelCycles.ValueChanged += (sender, args) => RefreshComponentMetrics();
            _cbWaveformType.SelectedIndexChanged += (sender, args) => GenerateLoadPreview(false);
            _nudMeanLoad.ValueChanged += (sender, args) => GenerateLoadPreview(false);
            _nudLoadAmplitude.ValueChanged += (sender, args) => GenerateLoadPreview(false);
            _nudLoadFrequency.ValueChanged += (sender, args) => GenerateLoadPreview(false);
            _nudPhaseShift.ValueChanged += (sender, args) => GenerateLoadPreview(false);
            _nudTotalCycles.ValueChanged += (sender, args) => GenerateLoadPreview(false);
            _nudSamplingRate.ValueChanged += (sender, args) => GenerateLoadPreview(false);
            _nudLoadRepetitions.ValueChanged += (sender, args) => GenerateLoadPreview(false);
            _cbGeneratedLoadResultStep.SelectedIndexChanged += (sender, args) => GenerateLoadPreview(false);
        }

        private void BuildSnTab()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(10);
            layout.RowCount = 2;
            layout.ColumnCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 320));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            //
            TableLayoutPanel top = new TableLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.ColumnCount = 4;
            top.RowCount = 1;
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            //
            GroupBox databaseGroup = new GroupBox { Text = "Database", Dock = DockStyle.Fill };
            TableLayoutPanel databaseLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), RowCount = 2, ColumnCount = 1 };
            databaseLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            databaseLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            _lbSnDatabase.Dock = DockStyle.Fill;
            FlowLayoutPanel dbButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            dbButtons.Controls.Add(new Button { Text = "New", Width = 74 });
            dbButtons.Controls.Add(new Button { Text = "Load", Width = 74 });
            dbButtons.Controls.Add(new Button { Text = "Save", Width = 74 });
            dbButtons.Controls.Add(new Button { Text = "Delete", Width = 74, Enabled = false });
            dbButtons.Controls.Add(new Button { Text = "Duplicate", Width = 86 });
            databaseLayout.Controls.Add(_lbSnDatabase, 0, 0);
            databaseLayout.Controls.Add(dbButtons, 0, 1);
            databaseGroup.Controls.Add(databaseLayout);
            //
            GroupBox curveGroup = new GroupBox { Text = "SN curve", Dock = DockStyle.Fill };
            TableLayoutPanel curveLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 6, RowCount = 8 };
            curveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            curveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            curveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
            curveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            curveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            curveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < curveLayout.RowCount; i++) curveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            AddSnRow(curveLayout, 0, 0, "SN curve name:", _tbSnName, "", 3);
            AddSnRow(curveLayout, 0, 1, "Primary slope, m1:", _nudM1, "", 1);
            AddSnRow(curveLayout, 0, 2, "Secondary slope, m2:", _nudM2, "", 1);
            AddSnRow(curveLayout, 0, 3, "Fatigue strength, dsR1:", _nudDs1, "MPa", 1);
            AddSnRow(curveLayout, 0, 4, "Associated cycles, NR1:", _nudN1, "cycles", 1);
            AddSnRow(curveLayout, 0, 5, "Knee point stress range, dsR2:", _tbDs2, "MPa", 1);
            AddSnRow(curveLayout, 0, 6, "Knee point cycles, NR2:", _nudN2, "cycles", 1);
            AddSnRow(curveLayout, 3, 1, "Min cut-off:", _nudMinS, "MPa", 1);
            AddSnRow(curveLayout, 3, 2, "Max cut-off:", _nudMaxS, "MPa", 1);
            AddSnRow(curveLayout, 3, 3, "Partial safety factor:", _nudGf, "", 1);
            AddSnRow(curveLayout, 3, 4, "Allowable damage:", _nudAllowableDamage, "", 1);
            AddSnRow(curveLayout, 3, 5, "Probability of survival:", _nudPs, "%", 1);
            AddSnRow(curveLayout, 3, 6, "Stdv. of log(N):", _nudStd, "", 1);
            AddSnRow(curveLayout, 0, 7, "Comments:", _tbSnComments, "", 5);
            curveGroup.Controls.Add(curveLayout);
            //
            GroupBox meanGroup = new GroupBox { Text = "Mean stress", Dock = DockStyle.Fill };
            TableLayoutPanel meanLayout = CreateSnOptionLayout();
            AddSnOptionRow(meanLayout, "Mean stress mode:", _cbMeanMode);
            AddSnOptionRow(meanLayout, "Mean stress sensitivity, M:", _nudM);
            AddSnOptionRow(meanLayout, "Yield strength, Re: [MPa]", _nudRe);
            AddSnOptionRow(meanLayout, "Tensile strength, Rm: [MPa]", _nudRm);
            meanLayout.Controls.Add(_cbObserveYieldLines, 0, meanLayout.RowCount);
            meanLayout.SetColumnSpan(_cbObserveYieldLines, 2);
            meanLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            meanLayout.RowCount++;
            meanLayout.Controls.Add(_cbExtrapolateCompression, 0, meanLayout.RowCount);
            meanLayout.SetColumnSpan(_cbExtrapolateCompression, 2);
            meanLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            meanLayout.RowCount++;
            meanGroup.Controls.Add(meanLayout);
            //
            GroupBox analysisGroup = new GroupBox { Text = "Analysis options", Dock = DockStyle.Fill };
            TableLayoutPanel analysisLayout = CreateSnOptionLayout();
            AddSnOptionRow(analysisLayout, "Calculation backend:", _cbCalculationBackend);
            AddSnOptionRow(analysisLayout, "Fatigue stress:", _cbFatigueStress);
            AddSnOptionRow(analysisLayout, "Cycle counter:", _cbCycleCounter);
            AddSnOptionRow(analysisLayout, "Multiaxial criterion:", _cbMultiaxialCriterion);
            AddSnOptionRow(analysisLayout, "Normal search planes:", _nudNormalSearchPlanes);
            AddSnOptionRow(analysisLayout, "Inclined search planes:", _nudInclinedSearchPlanes);
            AddSnOptionRow(analysisLayout, "Multiaxiality parameter k1:", _nudMultiaxialK1);
            AddSnOptionRow(analysisLayout, "Multiaxiality parameter k2:", _nudMultiaxialK2);
            analysisGroup.Controls.Add(analysisLayout);
            //
            top.Controls.Add(databaseGroup, 0, 0);
            top.Controls.Add(curveGroup, 1, 0);
            top.Controls.Add(meanGroup, 2, 0);
            top.Controls.Add(analysisGroup, 3, 0);
            //
            TableLayoutPanel plots = new TableLayoutPanel();
            plots.Dock = DockStyle.Fill;
            plots.ColumnCount = 2;
            plots.RowCount = 1;
            plots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            plots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            plots.Controls.Add(_snCurvePlotPanel, 0, 0);
            plots.Controls.Add(_meanStressPlotPanel, 1, 0);
            //
            layout.Controls.Add(top, 0, 0);
            layout.Controls.Add(plots, 0, 1);
            _tpSn.Controls.Add(layout);
            //
            WireSnPreviewEvents();
            _cbCalculationBackend.SelectedIndex = 0;
            _cbFatigueStress.SelectedIndex = 0;
            _cbCycleCounter.SelectedIndex = 0;
            _cbMultiaxialCriterion.SelectedIndex = 0;
        }

        private TableLayoutPanel CreateSnOptionLayout()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(12);
            layout.ColumnCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
            return layout;
        }

        private void AddSnOptionRow(TableLayoutPanel layout, string label, Control control)
        {
            int row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(control, 1, row);
        }

        private void AddSnRow(TableLayoutPanel layout, int column, int row, string label, Control control, string unit, int controlColumnSpan)
        {
            layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, column, row);
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(control, column + 1, row);
            if (controlColumnSpan > 1) layout.SetColumnSpan(control, controlColumnSpan);
            if (!string.IsNullOrWhiteSpace(unit))
                layout.Controls.Add(new Label { Text = unit, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, column + 2, row);
        }

        private void WireSnPreviewEvents()
        {
            foreach (Control control in new Control[]
            {
                _nudDs1, _nudN1, _nudN2, _nudM1, _nudM2, _nudGf, _nudMinS, _nudMaxS,
                _nudM, _nudRe, _nudRm, _nudPs, _nudStd
            })
            {
                NumericUpDown numeric = control as NumericUpDown;
                if (numeric != null) numeric.ValueChanged += (sender, args) => UpdateSnPreview();
            }
            _cbMeanMode.SelectedIndexChanged += (sender, args) => UpdateSnPreview();
            _cbObserveYieldLines.CheckedChanged += (sender, args) => UpdateSnPreview();
            _cbExtrapolateCompression.CheckedChanged += (sender, args) => UpdateSnPreview();
        }

        private void UpdateSnPreview()
        {
            double ds2 = CalculateSnKneeStress();
            _tbDs2.Text = ds2.ToString("0.###", CultureInfo.InvariantCulture);
            _snCurvePlotPanel.Ds1 = (double)_nudDs1.Value;
            _snCurvePlotPanel.N1 = (double)_nudN1.Value;
            _snCurvePlotPanel.N2 = (double)_nudN2.Value;
            _snCurvePlotPanel.M1 = (double)_nudM1.Value;
            _snCurvePlotPanel.M2 = (double)_nudM2.Value;
            _snCurvePlotPanel.Gf = (double)_nudGf.Value;
            _snCurvePlotPanel.MinStress = (double)_nudMinS.Value;
            _snCurvePlotPanel.MaxStress = (double)_nudMaxS.Value;
            _snCurvePlotPanel.Invalidate();
            //
            _meanStressPlotPanel.Re = (double)_nudRe.Value;
            _meanStressPlotPanel.Rm = (double)_nudRm.Value;
            _meanStressPlotPanel.M = (double)_nudM.Value;
            _meanStressPlotPanel.ReferenceAmplitude = Math.Max(0, (double)_nudDs1.Value / Math.Max(1e-12, (double)_nudGf.Value) / 2.0);
            _meanStressPlotPanel.MeanStressMode = Convert.ToString(_cbMeanMode.SelectedItem);
            _meanStressPlotPanel.ObserveYieldLines = _cbObserveYieldLines.Checked;
            _meanStressPlotPanel.ExtrapolateCompression = _cbExtrapolateCompression.Checked;
            _meanStressPlotPanel.Invalidate();
        }

        private double CalculateSnKneeStress()
        {
            double ds1 = (double)_nudDs1.Value;
            double n1 = Math.Max(1e-12, (double)_nudN1.Value);
            double n2 = Math.Max(1e-12, (double)_nudN2.Value);
            double m1 = Math.Max(1e-12, (double)_nudM1.Value);
            double gf = Math.Max(1e-12, (double)_nudGf.Value);
            return ds1 / gf * Math.Pow(n1 / n2, 1.0 / m1);
        }

        private TableLayoutPanel CreateMainLayout()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(10);
            layout.ColumnCount = 3;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            return layout;
        }

        private void AddRow(TableLayoutPanel layout, string label, Control control, Control sideButton)
        {
            int row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Label lb = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(lb, 0, row);
            layout.Controls.Add(control, 1, row);
            if (sideButton != null) layout.Controls.Add(sideButton, 2, row);
        }

        private void AddOptionRow(TableLayoutPanel layout, int row, string label, Control control, Control sideButton)
        {
            Label lb = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(lb, 0, row);
            layout.Controls.Add(control, 1, row);
            if (sideButton != null) layout.Controls.Add(sideButton, 2, row);
            else layout.SetColumnSpan(control, 2);
        }

        private Button CreateBrowseButton(TextBox textBox, string filter)
        {
            Button btn = new Button { Text = "...", Dock = DockStyle.Fill };
            btn.Click += (sender, args) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = filter;
                    if (!string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        string dir = Path.GetDirectoryName(textBox.Text);
                        if (Directory.Exists(dir)) ofd.InitialDirectory = dir;
                    }
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        textBox.Text = ofd.FileName;
                        if (textBox == _tbLoadsFile) TryReadLoadsFile(true);
                    }
                }
            };
            return btn;
        }

        private static NumericUpDown CreateNumeric(decimal min, decimal max, decimal value, int decimals)
        {
            NumericUpDown nud = new NumericUpDown();
            nud.Minimum = min;
            nud.Maximum = max;
            nud.Value = Math.Min(Math.Max(value, min), max);
            nud.DecimalPlaces = decimals;
            nud.Increment = decimals == 0 ? 1 : 0.1m;
            nud.ThousandsSeparator = true;
            return nud;
        }

        private static void AddComboColumn(DataGridView dgv, string name, string header, string[] values)
        {
            DataGridViewComboBoxColumn column = new DataGridViewComboBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.Items.AddRange(values);
            dgv.Columns.Add(column);
        }

        private object[] GetAvailableResultStepItems()
        {
            int[] stepIds = GetAvailableResultStepIds();
            object[] items = new object[stepIds.Length];
            for (int i = 0; i < stepIds.Length; i++) items[i] = stepIds[i].ToString(CultureInfo.InvariantCulture);
            return items;
        }

        private int[] GetAvailableResultStepIds()
        {
            try
            {
                if (_controller.ResultsInitialized)
                {
                    int[] stepIds = _controller.GetResultStepIDs();
                    if (stepIds != null && stepIds.Length > 0) return stepIds;
                }
            }
            catch
            {
            }
            int fallback = _setup.Model.ResultStep > 0 ? _setup.Model.ResultStep : 1;
            return new[] { fallback };
        }

        private int GetDefaultResultStep()
        {
            int[] stepIds = GetAvailableResultStepIds();
            if (_setup.Model.ResultStep > 0 && stepIds.Contains(_setup.Model.ResultStep)) return _setup.Model.ResultStep;
            return stepIds.Length > 0 ? stepIds[0] : 1;
        }

        private void RefreshResultStepControls()
        {
            object selected = _cbGeneratedLoadResultStep.SelectedItem;
            object[] items = GetAvailableResultStepItems();
            _cbGeneratedLoadResultStep.Items.Clear();
            _cbGeneratedLoadResultStep.Items.AddRange(items);
            string selectedText = selected != null ? selected.ToString() : GetDefaultResultStep().ToString(CultureInfo.InvariantCulture);
            _cbGeneratedLoadResultStep.SelectedItem = selectedText;
            if (_cbGeneratedLoadResultStep.SelectedIndex < 0 && _cbGeneratedLoadResultStep.Items.Count > 0)
                _cbGeneratedLoadResultStep.SelectedIndex = 0;
            //
            DataGridViewComboBoxColumn resultStepColumn = _dgvLoadCases.Columns["ResultStep"] as DataGridViewComboBoxColumn;
            if (resultStepColumn != null)
            {
                resultStepColumn.Items.Clear();
                resultStepColumn.Items.AddRange(items);
                string fallback = GetDefaultResultStep().ToString(CultureInfo.InvariantCulture);
                foreach (DataGridViewRow row in _dgvLoadCases.Rows)
                {
                    if (row.IsNewRow) continue;
                    object value = row.Cells["ResultStep"].Value;
                    if (value == null || !resultStepColumn.Items.Contains(value.ToString()))
                        row.Cells["ResultStep"].Value = fallback;
                }
            }
        }

        private void LoadFromSetup()
        {
            if (_setup.Loads.GeneratedLoads == null) _setup.Loads.GeneratedLoads = new List<FatigueGeneratedLoadSetup>();
            RefreshResultStepControls();
            _tbAnalysis.Text = _setup.Model.AnalysisName;
            _tbInp.Text = _setup.Model.InpFileName;
            _tbFrd.Text = _setup.Model.FrdFileName;
            _nudStep.Value = Clamp(_setup.Model.ResultStep, _nudStep);
            _tbMaterials.Text = _setup.Model.Materials;
            _nudEdge.Value = Clamp((decimal)_setup.Model.EdgeDetectionAngle, _nudEdge);
            _cbKeepInterfaces.Checked = _setup.Model.KeepMaterialInterfaces;
            _cbRequireMatch.Checked = _setup.Model.RequireMatchingAnalysisAndResultNames;
            RefreshModelPartsGrid();
            //
            _cbLoadsFormat.SelectedItem = _setup.Loads.Format;
            if (_cbLoadsFormat.SelectedIndex < 0) _cbLoadsFormat.SelectedIndex = 0;
            _tbLoadsFile.Text = _setup.Loads.LoadsFileName;
            _cbReduction.SelectedItem = _setup.Loads.Reduction;
            if (_cbReduction.SelectedIndex < 0) _cbReduction.SelectedIndex = 0;
            _cbPlotType.SelectedIndex = _cbPlotType.SelectedIndex < 0 ? 0 : _cbPlotType.SelectedIndex;
            //
            _dgvLoadCases.Rows.Clear();
            foreach (var lc in _setup.Loads.LoadCases)
                _dgvLoadCases.Rows.Add(lc.Name, lc.Repetitions,
                                       lc.ResultStep > 0
                                       ? lc.ResultStep.ToString(CultureInfo.InvariantCulture)
                                       : GetDefaultResultStep().ToString(CultureInfo.InvariantCulture));
            _dgvComponents.Rows.Clear();
            foreach (var comp in _setup.Loads.Components)
                _dgvComponents.Rows.Add(comp.Name, comp.Unit, "", "", "", "", "", comp.Scale, comp.Offset, comp.Plot);
            if (!string.IsNullOrWhiteSpace(_tbLoadsFile.Text) && File.Exists(_tbLoadsFile.Text)) TryReadLoadsFile(false);
            else if (_setup.Loads.GeneratedLoads.Count > 0) ApplyGeneratedLoadsPreview();
            else UpdateLoadPlot();
            RenderGeneratedLoadCards();
            //
            _tbSnName.Text = _setup.SnCurve.Name;
            _tbSnComments.Text = _setup.SnCurve.Comments;
            _nudDs1.Value = Clamp((decimal)_setup.SnCurve.Ds1, _nudDs1);
            _nudN1.Value = Clamp((decimal)_setup.SnCurve.N1, _nudN1);
            _nudN2.Value = Clamp((decimal)_setup.SnCurve.N2, _nudN2);
            _nudM1.Value = Clamp((decimal)_setup.SnCurve.M1, _nudM1);
            _nudM2.Value = Clamp((decimal)_setup.SnCurve.M2, _nudM2);
            _nudGf.Value = Clamp((decimal)_setup.SnCurve.Gf, _nudGf);
            _nudAllowableDamage.Value = Clamp((decimal)_setup.SnCurve.AllowableDamage, _nudAllowableDamage);
            _nudMinS.Value = Clamp((decimal)_setup.SnCurve.MinS, _nudMinS);
            _nudMaxS.Value = Clamp((decimal)_setup.SnCurve.MaxS, _nudMaxS);
            _cbMeanMode.SelectedItem = _setup.SnCurve.MeanMode;
            if (_cbMeanMode.SelectedIndex < 0) _cbMeanMode.SelectedIndex = 0;
            _nudM.Value = Clamp((decimal)_setup.SnCurve.M, _nudM);
            _nudRe.Value = Clamp((decimal)_setup.SnCurve.Re, _nudRe);
            _nudRm.Value = Clamp((decimal)_setup.SnCurve.Rm, _nudRm);
            _cbObserveYieldLines.Checked = _setup.SnCurve.ObserveYieldLines;
            _cbExtrapolateCompression.Checked = _setup.SnCurve.ExtrapolateInCompression;
            _nudDal.Value = Clamp((decimal)_setup.SnCurve.Dal, _nudDal);
            _nudPs.Value = Clamp((decimal)_setup.SnCurve.Ps, _nudPs);
            _nudStd.Value = Clamp((decimal)_setup.SnCurve.Std, _nudStd);
            _cbCalculationBackend.SelectedItem = _setup.SnCurve.CalculationBackend;
            if (_cbCalculationBackend.SelectedIndex < 0) _cbCalculationBackend.SelectedIndex = 0;
            _cbFatigueStress.SelectedItem = _setup.SnCurve.FatigueStress;
            if (_cbFatigueStress.SelectedIndex < 0) _cbFatigueStress.SelectedIndex = 0;
            _cbCycleCounter.SelectedItem = _setup.SnCurve.CycleCounter;
            if (_cbCycleCounter.SelectedIndex < 0) _cbCycleCounter.SelectedIndex = 0;
            _cbMultiaxialCriterion.SelectedItem = _setup.SnCurve.MultiaxialCriterion;
            if (_cbMultiaxialCriterion.SelectedIndex < 0) _cbMultiaxialCriterion.SelectedIndex = 0;
            _nudNormalSearchPlanes.Value = Clamp((decimal)_setup.SnCurve.NormalSearchPlanes, _nudNormalSearchPlanes);
            _nudInclinedSearchPlanes.Value = Clamp((decimal)_setup.SnCurve.InclinedSearchPlanes, _nudInclinedSearchPlanes);
            _nudMultiaxialK1.Value = Clamp((decimal)_setup.SnCurve.MultiaxialK1, _nudMultiaxialK1);
            _nudMultiaxialK2.Value = Clamp((decimal)_setup.SnCurve.MultiaxialK2, _nudMultiaxialK2);
            UpdateSnPreview();
            UpdateModelStatus();
        }

        private static decimal Clamp(decimal value, NumericUpDown nud)
        {
            if (value < nud.Minimum) return nud.Minimum;
            if (value > nud.Maximum) return nud.Maximum;
            return value;
        }

        private void SaveToSetup()
        {
            _setup.Model.AnalysisName = _tbAnalysis.Text.Trim();
            _setup.Model.InpFileName = _tbInp.Text.Trim();
            _setup.Model.FrdFileName = _tbFrd.Text.Trim();
            _setup.Model.ResultStep = (int)_nudStep.Value;
            _setup.Model.Materials = _tbMaterials.Text.Trim();
            _setup.Model.EdgeDetectionAngle = (double)_nudEdge.Value;
            _setup.Model.KeepMaterialInterfaces = _cbKeepInterfaces.Checked;
            _setup.Model.RequireMatchingAnalysisAndResultNames = false;
            SyncModelVisiblePartsToSetup();
            EnsureDefaultUnitLoadCases();
            //
            _setup.Loads.Format = Convert.ToString(_cbLoadsFormat.SelectedItem);
            _setup.Loads.LoadsFileName = _tbLoadsFile.Text.Trim();
            _setup.Loads.Reduction = Convert.ToString(_cbReduction.SelectedItem);
            if (_setup.Loads.GeneratedLoads == null) _setup.Loads.GeneratedLoads = new List<FatigueGeneratedLoadSetup>();
            _setup.Loads.LoadCases.Clear();
            foreach (DataGridViewRow row in _dgvLoadCases.Rows)
            {
                if (row.IsNewRow) continue;
                FatigueLoadCaseSetup lc = new FatigueLoadCaseSetup();
                lc.Name = GetCell(row, "Name", "Load case 1");
                lc.Repetitions = GetDouble(row, "Repetitions", 1);
                lc.ResultStep = (int)Math.Round(GetDouble(row, "ResultStep", GetDefaultResultStep()));
                _setup.Loads.LoadCases.Add(lc);
            }
            _setup.Loads.Components.Clear();
            foreach (DataGridViewRow row in _dgvComponents.Rows)
            {
                if (row.IsNewRow) continue;
                FatigueLoadComponentSetup comp = new FatigueLoadComponentSetup();
                comp.Name = GetCell(row, "Name", "Load 1");
                comp.Unit = GetCell(row, "Unit", "");
                comp.Scale = GetDouble(row, "Scale", 1);
                comp.Offset = GetDouble(row, "Offset", 0);
                comp.Plot = GetBool(row, "Plot", true);
                _setup.Loads.Components.Add(comp);
            }
            EnsureDefaultUnitLoadCases();
            if (string.IsNullOrWhiteSpace(_setup.Loads.LoadsFileName) &&
                _loadPlotPanel.Preview != null &&
                _loadPlotPanel.Preview.LoadCases.Count > 0 &&
                _loadPlotPanel.Preview.Components.Count > 0)
            {
                string generatedDirectory = Path.Combine(Path.GetTempPath(), "PrePoMax", "Fatigue");
                Directory.CreateDirectory(generatedDirectory);
                string generatedFileName = Path.Combine(generatedDirectory, "generated_loads.csv");
                WriteCurrentLoadsToCsv(generatedFileName);
                _setup.Loads.LoadsFileName = generatedFileName;
                _tbLoadsFile.Text = generatedFileName;
                _setup.Loads.Format = "XLS";
                _cbLoadsFormat.SelectedItem = "XLS";
            }
            //
            _setup.SnCurve.Name = _tbSnName.Text.Trim();
            _setup.SnCurve.Comments = _tbSnComments.Text.Trim();
            _setup.SnCurve.Ds1 = (double)_nudDs1.Value;
            _setup.SnCurve.N1 = (double)_nudN1.Value;
            _setup.SnCurve.N2 = (double)_nudN2.Value;
            _setup.SnCurve.M1 = (double)_nudM1.Value;
            _setup.SnCurve.M2 = (double)_nudM2.Value;
            _setup.SnCurve.Gf = (double)_nudGf.Value;
            _setup.SnCurve.AllowableDamage = (double)_nudAllowableDamage.Value;
            _setup.SnCurve.MinS = (double)_nudMinS.Value;
            _setup.SnCurve.MaxS = (double)_nudMaxS.Value;
            _setup.SnCurve.MeanMode = Convert.ToString(_cbMeanMode.SelectedItem);
            _setup.SnCurve.M = (double)_nudM.Value;
            _setup.SnCurve.Re = (double)_nudRe.Value;
            _setup.SnCurve.Rm = (double)_nudRm.Value;
            _setup.SnCurve.ObserveYieldLines = _cbObserveYieldLines.Checked;
            _setup.SnCurve.ExtrapolateInCompression = _cbExtrapolateCompression.Checked;
            _setup.SnCurve.Dal = (double)_nudDal.Value;
            _setup.SnCurve.Ps = (double)_nudPs.Value;
            _setup.SnCurve.Std = (double)_nudStd.Value;
            _setup.SnCurve.CalculationBackend = Convert.ToString(_cbCalculationBackend.SelectedItem);
            _setup.SnCurve.FatigueStress = Convert.ToString(_cbFatigueStress.SelectedItem);
            _setup.SnCurve.CycleCounter = Convert.ToString(_cbCycleCounter.SelectedItem);
            _setup.SnCurve.MultiaxialCriterion = Convert.ToString(_cbMultiaxialCriterion.SelectedItem);
            _setup.SnCurve.NormalSearchPlanes = (int)_nudNormalSearchPlanes.Value;
            _setup.SnCurve.InclinedSearchPlanes = (int)_nudInclinedSearchPlanes.Value;
            _setup.SnCurve.MultiaxialK1 = (double)_nudMultiaxialK1.Value;
            _setup.SnCurve.MultiaxialK2 = (double)_nudMultiaxialK2.Value;
        }

        private void RefreshModelPartsGrid()
        {
            _dgvModelParts.Rows.Clear();
            if (!_controller.ResultsInitialized ||
                _controller.CurrentResult == null ||
                _controller.CurrentResult.Mesh == null)
            {
                UpdateModelStatus();
                return;
            }
            //
            foreach (var entry in _controller.CurrentResult.Mesh.Parts)
            {
                BasePart part = entry.Value;
                _dgvModelParts.Rows.Add(part.Name,
                                        part.Visible ? "Yes" : "No",
                                        part.Active ? "Yes" : "No",
                                        part.NodeLabels != null ? part.NodeLabels.Length : 0,
                                        part.Labels != null ? part.Labels.Length : 0);
            }
            SyncModelVisiblePartsToSetup();
            UpdateModelStatus();
        }

        private void SyncModelVisiblePartsToSetup()
        {
            _setup.Model.VisiblePartNames.Clear();
            if (_controller.ResultsInitialized &&
                _controller.CurrentResult != null &&
                _controller.CurrentResult.Mesh != null)
            {
                string[] visiblePartNames = _controller.CurrentResult.Mesh.GetVisibleAndActivePartNames();
                if (visiblePartNames != null) _setup.Model.VisiblePartNames.AddRange(visiblePartNames);
            }
        }

        private void EnsureDefaultUnitLoadCases()
        {
            if (_setup.Model.UnitLoadCases.Count == 0) _setup.Model.UnitLoadCases.Add(new FatigueUnitLoadCaseSetup());
            if (_setup.Model.UnitLoadCases.Count == 1)
            {
                _setup.Model.UnitLoadCases[0].LoadName = _setup.Loads.Components.Count > 0
                                                         ? _setup.Loads.Components[0].Name
                                                         : "Load 1";
                _setup.Model.UnitLoadCases[0].FrdFileName = _setup.Model.FrdFileName;
                _setup.Model.UnitLoadCases[0].ResultStep = _setup.Model.ResultStep;
            }
        }

        private string[] GetSelectedModelPartNames()
        {
            List<string> names = new List<string>();
            foreach (DataGridViewRow row in _dgvModelParts.SelectedRows)
            {
                object value = row.Cells["Name"].Value;
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString())) names.Add(value.ToString());
            }
            return names.ToArray();
        }

        private static string GetCell(DataGridViewRow row, string name, string defaultValue)
        {
            object value = row.Cells[name].Value;
            string text = value != null ? value.ToString() : null;
            return string.IsNullOrWhiteSpace(text) ? defaultValue : text.Trim();
        }

        private static double GetDouble(DataGridViewRow row, string name, double defaultValue)
        {
            object value = row.Cells[name].Value;
            if (value == null) return defaultValue;
            double result;
            if (double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.CurrentCulture, out result)) return result;
            if (double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result)) return result;
            return defaultValue;
        }

        private static bool GetBool(DataGridViewRow row, string name, bool defaultValue)
        {
            object value = row.Cells[name].Value;
            if (value == null) return defaultValue;
            if (value is bool) return (bool)value;
            bool result;
            return bool.TryParse(value.ToString(), out result) ? result : defaultValue;
        }

        private void btnAuto_Click(object sender, EventArgs e)
        {
            SaveToSetup();
            string currentResultFileName = GetCurrentResultFileName();
            _setup.ApplyProjectContext(currentResultFileName, _controller.GetAllJobs(),
                                       job => ResolveInputFileName(job, currentResultFileName),
                                       GetCurrentVisibleResultPartNames(), GetCurrentResultName());
            LoadFromSetup();
        }

        private void btnShowModelParts_Click(object sender, EventArgs e)
        {
            string[] partNames = GetSelectedModelPartNames();
            if (partNames.Length == 0) return;
            _controller.ShowResultPartsCommand(partNames);
            RefreshModelPartsGrid();
        }

        private void btnHideModelParts_Click(object sender, EventArgs e)
        {
            string[] partNames = GetSelectedModelPartNames();
            if (partNames.Length == 0) return;
            _controller.HideResultPartsCommand(partNames);
            RefreshModelPartsGrid();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                if (_singleSetupPage == null || _singleSetupPage == "Loads")
                {
                    if (string.IsNullOrWhiteSpace(_tbLoadsFile.Text) &&
                        (_setup.Loads.GeneratedLoads == null || _setup.Loads.GeneratedLoads.Count == 0))
                    {
                        SaveGeneratedLoadFromEditor(false);
                        ApplyGeneratedLoadsPreview();
                    }
                    else if (_editingGeneratedLoadIndex >= 0)
                    {
                        SaveGeneratedLoadFromEditor(false);
                        ApplyGeneratedLoadsPreview();
                    }
                    else if (string.IsNullOrWhiteSpace(_tbLoadsFile.Text) &&
                             (_loadPlotPanel.Preview == null ||
                              _loadPlotPanel.Preview.LoadCases.Count == 0 ||
                              _loadPlotPanel.Preview.Components.Count == 0))
                    {
                        GenerateLoadPreview(true);
                    }
                }
                //
                SaveToSetup();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                DialogResult = DialogResult.None;
                MessageBoxes.ShowError("The fatigue setup could not be saved." + Environment.NewLine + ex.Message);
            }
        }

        private void btnUpdateLoads_Click(object sender, EventArgs e)
        {
            TryReadLoadsFile(true);
        }

        private void btnSaveLoads_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV loads (*.csv)|*.csv|All files (*.*)|*.*";
                sfd.FileName = string.IsNullOrWhiteSpace(_tbLoadsFile.Text)
                               ? "fatigue_loads.csv"
                               : Path.GetFileNameWithoutExtension(_tbLoadsFile.Text) + ".csv";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    SaveCurrentLoadsToCsv(sfd.FileName);
                    _tbLoadsFile.Text = sfd.FileName;
                    _cbLoadsFormat.SelectedItem = "XLS";
                }
            }
        }

        private void TryReadLoadsFile(bool showErrors)
        {
            if (string.IsNullOrWhiteSpace(_tbLoadsFile.Text) || !File.Exists(_tbLoadsFile.Text))
            {
                if (showErrors) MessageBoxes.ShowError("The loads file does not exist.");
                return;
            }
            //
            try
            {
                FatigueLoadsPreview preview = ReadLoadsPreview(_tbLoadsFile.Text);
                if (preview == null || preview.LoadCases.Count == 0 || preview.Components.Count == 0)
                    throw new Exception("No FATLAB load data was found.");
                ApplyLoadsPreview(preview);
                if (showErrors)
                {
                    if (_setup.Loads.GeneratedLoads == null) _setup.Loads.GeneratedLoads = new List<FatigueGeneratedLoadSetup>();
                    _setup.Loads.GeneratedLoads.Clear();
                    _editingGeneratedLoadIndex = -1;
                    RenderGeneratedLoadCards();
                }
            }
            catch (Exception ex)
            {
                if (showErrors) MessageBoxes.ShowError("The loads file could not be previewed in PrePoMax." +
                                                       Environment.NewLine + ex.Message +
                                                       Environment.NewLine + Environment.NewLine +
                                                       "The file path is still stored for FATLAB runtime processing.");
            }
        }

        private void btnGenerateLoad_Click(object sender, EventArgs e)
        {
            try
            {
                SaveGeneratedLoadFromEditor(true);
                ApplyGeneratedLoadsPreview();
                RenderGeneratedLoadCards();
            }
            catch (Exception ex)
            {
                MessageBoxes.ShowError("The load waveform could not be generated." +
                                       Environment.NewLine + ex.Message);
            }
        }

        private void GenerateLoadPreview(bool showErrors)
        {
            if (!Visible && !showErrors) return;
            try
            {
                FatigueLoadsPreview preview = GenerateWaveformPreview();
                ApplyLoadsPreview(preview);
                _tbLoadsFile.Text = "";
            }
            catch (Exception ex)
            {
                if (showErrors) MessageBoxes.ShowError("The load waveform could not be generated." +
                                                       Environment.NewLine + ex.Message);
            }
        }

        private void ApplyLoadsPreview(FatigueLoadsPreview preview)
        {
            _dgvLoadCases.Rows.Clear();
            foreach (FatigueLoadCasePreview loadCase in preview.LoadCases)
                _dgvLoadCases.Rows.Add(loadCase.Name, loadCase.Repetitions,
                                       loadCase.ResultStep > 0
                                       ? loadCase.ResultStep.ToString(CultureInfo.InvariantCulture)
                                       : GetDefaultResultStep().ToString(CultureInfo.InvariantCulture));
            //
            _dgvComponents.Rows.Clear();
            foreach (FatigueLoadComponentPreview component in preview.Components)
                _dgvComponents.Rows.Add(component.Name, component.Unit, component.Min, component.Max,
                                        component.Mean, component.Cycles, component.Del, 1, 0, true);
            //
            _loadPlotPanel.Preview = preview;
            _loadPlotPanel.SelectedLoadCase = 0;
            RefreshComponentMetrics();
        }

        private void SaveGeneratedLoadFromEditor(bool selectSavedLoad)
        {
            FatigueGeneratedLoadSetup load = CreateGeneratedLoadFromEditor();
            if (_setup.Loads.GeneratedLoads == null) _setup.Loads.GeneratedLoads = new List<FatigueGeneratedLoadSetup>();
            int index = _editingGeneratedLoadIndex >= 0 &&
                        _editingGeneratedLoadIndex < _setup.Loads.GeneratedLoads.Count
                        ? _editingGeneratedLoadIndex
                        : _setup.Loads.GeneratedLoads.FindIndex(l => string.Equals(l.Name, load.Name,
                                                                                   StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _setup.Loads.GeneratedLoads[index] = load;
            else
            {
                _setup.Loads.GeneratedLoads.Add(load);
                index = _setup.Loads.GeneratedLoads.Count - 1;
            }
            if (selectSavedLoad) _editingGeneratedLoadIndex = index;
            _tbLoadsFile.Text = "";
        }

        private FatigueGeneratedLoadSetup CreateGeneratedLoadFromEditor()
        {
            FatigueGeneratedLoadSetup load = new FatigueGeneratedLoadSetup();
            load.Name = string.IsNullOrWhiteSpace(_tbGeneratedLoadName.Text) ? "Fx" : _tbGeneratedLoadName.Text.Trim();
            load.Unit = _tbGeneratedLoadUnit.Text.Trim();
            load.Waveform = Convert.ToString(_cbWaveformType.SelectedItem);
            if (string.IsNullOrWhiteSpace(load.Waveform)) load.Waveform = "Sinusoidal";
            load.Mean = (double)_nudMeanLoad.Value;
            load.Amplitude = (double)_nudLoadAmplitude.Value;
            load.Frequency = (double)_nudLoadFrequency.Value;
            load.Phase = (double)_nudPhaseShift.Value;
            load.Cycles = (double)_nudTotalCycles.Value;
            load.SamplesPerSecond = (double)_nudSamplingRate.Value;
            load.Repetitions = (double)_nudLoadRepetitions.Value;
            load.ResultStep = GetSelectedEditorResultStep();
            return load;
        }

        private int GetSelectedEditorResultStep()
        {
            int result;
            object selected = _cbGeneratedLoadResultStep.SelectedItem;
            if (selected != null &&
                int.TryParse(selected.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                return result;
            return GetDefaultResultStep();
        }

        private void SetEditorFromGeneratedLoad(FatigueGeneratedLoadSetup load)
        {
            if (load == null) return;
            _tbGeneratedLoadName.Text = load.Name;
            _tbGeneratedLoadUnit.Text = load.Unit;
            _cbWaveformType.SelectedItem = load.Waveform;
            if (_cbWaveformType.SelectedIndex < 0) _cbWaveformType.SelectedIndex = 0;
            _nudMeanLoad.Value = Clamp((decimal)load.Mean, _nudMeanLoad);
            _nudLoadAmplitude.Value = Clamp((decimal)load.Amplitude, _nudLoadAmplitude);
            _nudLoadFrequency.Value = Clamp((decimal)load.Frequency, _nudLoadFrequency);
            _nudPhaseShift.Value = Clamp((decimal)load.Phase, _nudPhaseShift);
            _nudTotalCycles.Value = Clamp((decimal)load.Cycles, _nudTotalCycles);
            _nudSamplingRate.Value = Clamp((decimal)load.SamplesPerSecond, _nudSamplingRate);
            _nudLoadRepetitions.Value = Clamp((decimal)load.Repetitions, _nudLoadRepetitions);
            string resultStep = (load.ResultStep > 0 ? load.ResultStep : GetDefaultResultStep()).ToString(CultureInfo.InvariantCulture);
            _cbGeneratedLoadResultStep.SelectedItem = resultStep;
            if (_cbGeneratedLoadResultStep.SelectedIndex < 0 && _cbGeneratedLoadResultStep.Items.Count > 0)
                _cbGeneratedLoadResultStep.SelectedIndex = 0;
        }

        private void RenderGeneratedLoadCards()
        {
            _flpGeneratedLoads.Controls.Clear();
            if (_setup.Loads.GeneratedLoads == null || _setup.Loads.GeneratedLoads.Count == 0)
            {
                _flpGeneratedLoads.Controls.Add(new Label
                {
                    Text = "No loads created",
                    AutoSize = false,
                    Height = 24,
                    Width = Math.Max(300, _flpGeneratedLoads.ClientSize.Width - 24),
                    TextAlign = ContentAlignment.MiddleLeft
                });
                return;
            }
            for (int i = 0; i < _setup.Loads.GeneratedLoads.Count; i++)
                _flpGeneratedLoads.Controls.Add(CreateGeneratedLoadCard(i, _setup.Loads.GeneratedLoads[i]));
        }

        private Control CreateGeneratedLoadCard(int index, FatigueGeneratedLoadSetup load)
        {
            Panel card = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Width = 340,
                Height = 74,
                Margin = new Padding(0, 0, 0, 6),
                Tag = index
            };
            Label title = new Label
            {
                Text = load.Name + "  Step " + load.ResultStep.ToString(CultureInfo.InvariantCulture),
                Font = new Font(Font, FontStyle.Bold),
                Location = new Point(6, 4),
                Size = new Size(210, 20)
            };
            Label details = new Label
            {
                Text = load.Waveform + ", mean " + load.Mean.ToString("G5", CultureInfo.InvariantCulture) +
                       ", amp " + load.Amplitude.ToString("G5", CultureInfo.InvariantCulture) +
                       ", " + load.Frequency.ToString("G5", CultureInfo.InvariantCulture) + " Hz",
                Location = new Point(6, 26),
                Size = new Size(320, 18)
            };
            Label repetitions = new Label
            {
                Text = load.Repetitions.ToString("G5", CultureInfo.InvariantCulture) + " repetitions",
                Location = new Point(6, 47),
                Size = new Size(150, 18)
            };
            Button edit = new Button { Text = "Edit", Location = new Point(198, 46), Size = new Size(62, 23), Tag = index };
            Button delete = new Button { Text = "Delete", Location = new Point(266, 46), Size = new Size(62, 23), Tag = index };
            edit.Click += btnEditGeneratedLoad_Click;
            delete.Click += btnDeleteGeneratedLoad_Click;
            card.Controls.Add(title);
            card.Controls.Add(details);
            card.Controls.Add(repetitions);
            card.Controls.Add(edit);
            card.Controls.Add(delete);
            return card;
        }

        private void btnEditGeneratedLoad_Click(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;
            int index = (int)button.Tag;
            if (_setup.Loads.GeneratedLoads == null || index < 0 || index >= _setup.Loads.GeneratedLoads.Count) return;
            _editingGeneratedLoadIndex = index;
            SetEditorFromGeneratedLoad(_setup.Loads.GeneratedLoads[index]);
            GenerateLoadPreview(false);
        }

        private void btnDeleteGeneratedLoad_Click(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;
            int index = (int)button.Tag;
            if (_setup.Loads.GeneratedLoads == null || index < 0 || index >= _setup.Loads.GeneratedLoads.Count) return;
            _setup.Loads.GeneratedLoads.RemoveAt(index);
            _editingGeneratedLoadIndex = -1;
            if (_setup.Loads.GeneratedLoads.Count > 0) ApplyGeneratedLoadsPreview();
            else
            {
                _dgvLoadCases.Rows.Clear();
                _dgvComponents.Rows.Clear();
                _loadPlotPanel.Preview = null;
                _loadPlotPanel.Invalidate();
            }
            RenderGeneratedLoadCards();
        }

        private void ApplyGeneratedLoadsPreview()
        {
            if (_setup.Loads.GeneratedLoads == null || _setup.Loads.GeneratedLoads.Count == 0) return;
            ApplyLoadsPreview(BuildGeneratedLoadsPreview());
            _tbLoadsFile.Text = "";
        }

        private FatigueLoadsPreview BuildGeneratedLoadsPreview()
        {
            FatigueLoadsPreview preview = new FatigueLoadsPreview();
            int componentCount = _setup.Loads.GeneratedLoads.Count;
            List<double[]> times = new List<double[]>();
            List<double[]> values = new List<double[]>();
            for (int i = 0; i < componentCount; i++)
            {
                double[] time;
                double[] loadValues;
                GenerateWaveformValues(_setup.Loads.GeneratedLoads[i], out time, out loadValues);
                times.Add(time);
                values.Add(loadValues);
                preview.Components.Add(CreateComponentPreview(_setup.Loads.GeneratedLoads[i].Name,
                                                              _setup.Loads.GeneratedLoads[i].Unit,
                                                              loadValues));
            }
            for (int i = 0; i < componentCount; i++)
            {
                FatigueGeneratedLoadSetup generatedLoad = _setup.Loads.GeneratedLoads[i];
                FatigueLoadCasePreview loadCase = new FatigueLoadCasePreview();
                loadCase.Name = "LC" + (i + 1).ToString(CultureInfo.InvariantCulture);
                loadCase.Repetitions = generatedLoad.Repetitions;
                loadCase.ResultStep = generatedLoad.ResultStep > 0 ? generatedLoad.ResultStep : GetDefaultResultStep();
                loadCase.Time = times[i];
                loadCase.Values = new double[componentCount][];
                for (int c = 0; c < componentCount; c++)
                    loadCase.Values[c] = c == i ? values[i] : new double[times[i].Length];
                preview.LoadCases.Add(loadCase);
            }
            return preview;
        }

        private FatigueLoadsPreview ReadLoadsPreview(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (extension == ".xls" || extension == ".xlsx") return ReadExcelLoadsPreview(fileName);
            return ReadDelimitedLoadsPreview(fileName);
        }

        private FatigueLoadsPreview ReadDelimitedLoadsPreview(string fileName)
        {
            string[] lines = File.ReadAllLines(fileName);
            char delimiter = lines.Any(line => line.Contains(";")) ? ';' : ',';
            List<string[]> rows = lines.Select(line => SplitDelimitedLine(line, delimiter)).ToList();
            return ReadRowsLoadsPreview(fileName, rows);
        }

        private FatigueLoadsPreview ReadExcelLoadsPreview(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (extension == ".xlsx") return ReadRowsLoadsPreview(fileName, ReadXlsxRows(fileName));
            //
            string provider = extension == ".xls"
                              ? "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + fileName + ";Extended Properties='Excel 8.0;HDR=NO;IMEX=1'"
                              : "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + fileName + ";Extended Properties='Excel 12.0 Xml;HDR=NO;IMEX=1'";
            using (OleDbConnection connection = new OleDbConnection(provider))
            {
                connection.Open();
                DataTable sheets = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                if (sheets == null || sheets.Rows.Count == 0) return null;
                string sheet = sheets.Rows[0]["TABLE_NAME"].ToString();
                using (OleDbDataAdapter adapter = new OleDbDataAdapter("SELECT * FROM [" + sheet + "]", connection))
                {
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    return ReadTableLoadsPreview(fileName, table);
                }
            }
        }

        private FatigueLoadsPreview ReadTableLoadsPreview(string fileName, DataTable table)
        {
            List<string[]> rows = new List<string[]>();
            foreach (DataRow row in table.Rows)
            {
                string[] cells = new string[table.Columns.Count];
                for (int i = 0; i < table.Columns.Count; i++) cells[i] = Convert.ToString(row[i]);
                rows.Add(cells);
            }
            return ReadRowsLoadsPreview(fileName, rows);
        }

        private FatigueLoadsPreview ReadRowsLoadsPreview(string fileName, List<string[]> rows)
        {
            if (rows == null || rows.Count == 0) return null;
            foreach (string[] row in rows)
            {
                for (int i = 2; row != null && i < row.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(GetCell(row, i)))
                        throw new Exception("Imported load files must contain only two columns: time and amplitude.");
                }
            }
            //
            int dataStartRow = -1;
            for (int r = 0; r < rows.Count; r++)
            {
                double timeValue;
                double amplitudeValue;
                if (TryParseDouble(GetCell(rows[r], 0), out timeValue) &&
                    TryParseDouble(GetCell(rows[r], 1), out amplitudeValue))
                {
                    dataStartRow = r;
                    break;
                }
            }
            if (dataStartRow < 0) throw new Exception("No numeric time/amplitude rows were found.");
            //
            int headerRow = -1;
            for (int r = 0; r < dataStartRow; r++)
            {
                string first = GetCell(rows[r], 0);
                string second = GetCell(rows[r], 1);
                if (string.Equals(first, "Time", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(first, "t", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(second))
                    headerRow = r;
            }
            int unitRow = headerRow >= 0 && headerRow + 1 < dataStartRow ? headerRow + 1 : -1;
            //
            double repetitions = 1;
            for (int r = 0; r < dataStartRow; r++)
            {
                if (string.Equals(GetCell(rows[r], 0), "n", StringComparison.OrdinalIgnoreCase))
                {
                    repetitions = ParseDouble(GetCell(rows[r], 1), 1);
                    break;
                }
            }
            //
            FatigueLoadCasePreview loadCase = new FatigueLoadCasePreview();
            loadCase.Name = Path.GetFileNameWithoutExtension(fileName);
            loadCase.Repetitions = repetitions;
            loadCase.ResultStep = GetDefaultResultStep();
            //
            List<double> time = new List<double>();
            List<double> values = new List<double>();
            //
            for (int r = dataStartRow; r < rows.Count; r++)
            {
                double t;
                if (!TryParseDouble(GetCell(rows[r], 0), out t)) continue;
                double value;
                if (!TryParseDouble(GetCell(rows[r], 1), out value)) continue;
                time.Add(t);
                values.Add(value);
            }
            if (time.Count == 0) return null;
            //
            loadCase.Time = time.ToArray();
            loadCase.Values = new[] { values.ToArray() };
            //
            FatigueLoadsPreview preview = new FatigueLoadsPreview();
            preview.LoadCases.Add(loadCase);
            string name = headerRow >= 0 ? GetCell(rows[headerRow], 1) : "";
            string unit = unitRow >= 0 ? GetCell(rows[unitRow], 1) : "";
            if (string.IsNullOrWhiteSpace(name)) name = "Fx";
            preview.Components.Add(CreateComponentPreview(name, unit, loadCase.Values[0]));
            return preview;
        }

        private List<string[]> ReadXlsxRows(string fileName)
        {
            using (ZipArchive archive = ZipFile.OpenRead(fileName))
            {
                List<string> sharedStrings = ReadXlsxSharedStrings(archive);
                string sheetPath = GetFirstWorksheetPath(archive);
                ZipArchiveEntry sheetEntry = archive.GetEntry(sheetPath);
                if (sheetEntry == null) throw new Exception("The XLSX workbook does not contain a readable worksheet.");
                //
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XDocument document;
                using (Stream stream = sheetEntry.Open()) document = XDocument.Load(stream);
                List<string[]> rows = new List<string[]>();
                foreach (XElement rowElement in document.Descendants(ns + "row"))
                {
                    SortedDictionary<int, string> cells = new SortedDictionary<int, string>();
                    int maxColumn = -1;
                    foreach (XElement cellElement in rowElement.Elements(ns + "c"))
                    {
                        XAttribute refAttribute = cellElement.Attribute("r");
                        int column = refAttribute != null ? GetXlsxColumnIndex(refAttribute.Value) : maxColumn + 1;
                        cells[column] = ReadXlsxCellValue(cellElement, sharedStrings, ns);
                        maxColumn = Math.Max(maxColumn, column);
                    }
                    string[] row = new string[Math.Max(0, maxColumn + 1)];
                    foreach (var item in cells) row[item.Key] = item.Value;
                    rows.Add(row);
                }
                return rows;
            }
        }

        private List<string> ReadXlsxSharedStrings(ZipArchive archive)
        {
            List<string> values = new List<string>();
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return values;
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XDocument document;
            using (Stream stream = entry.Open()) document = XDocument.Load(stream);
            foreach (XElement item in document.Descendants(ns + "si"))
                values.Add(string.Concat(item.Descendants(ns + "t").Select(t => t.Value)));
            return values;
        }

        private string GetFirstWorksheetPath(ZipArchive archive)
        {
            ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");
            ZipArchiveEntry relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (workbookEntry != null && relsEntry != null)
            {
                XNamespace workbookNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                XDocument workbook;
                using (Stream stream = workbookEntry.Open()) workbook = XDocument.Load(stream);
                XElement sheet = workbook.Descendants(workbookNs + "sheet").FirstOrDefault();
                XAttribute relationshipId = sheet != null ? sheet.Attribute(relNs + "id") : null;
                if (relationshipId != null)
                {
                    XDocument rels;
                    using (Stream stream = relsEntry.Open()) rels = XDocument.Load(stream);
                    XElement rel = rels.Root.Elements()
                                           .FirstOrDefault(e => string.Equals((string)e.Attribute("Id"),
                                                                              relationshipId.Value,
                                                                              StringComparison.OrdinalIgnoreCase));
                    string target = rel != null ? (string)rel.Attribute("Target") : null;
                    if (!string.IsNullOrWhiteSpace(target))
                        return "xl/" + target.TrimStart('/');
                }
            }
            //
            ZipArchiveEntry fallback = archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
                                                                           e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (fallback == null) throw new Exception("The XLSX workbook does not contain a worksheet.");
            return fallback.FullName;
        }

        private string ReadXlsxCellValue(XElement cellElement, List<string> sharedStrings, XNamespace ns)
        {
            string type = (string)cellElement.Attribute("t");
            if (type == "inlineStr") return string.Concat(cellElement.Descendants(ns + "t").Select(t => t.Value));
            string value = Convert.ToString(cellElement.Element(ns + "v") != null ? cellElement.Element(ns + "v").Value : "");
            if (type == "s")
            {
                int index;
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
                    index >= 0 && index < sharedStrings.Count) return sharedStrings[index];
            }
            return value;
        }

        private static int GetXlsxColumnIndex(string cellReference)
        {
            int column = 0;
            foreach (char c in cellReference)
            {
                if (!char.IsLetter(c)) break;
                column = column * 26 + char.ToUpperInvariant(c) - 'A' + 1;
            }
            return Math.Max(0, column - 1);
        }

        private FatigueLoadsPreview GenerateWaveformPreview()
        {
            FatigueGeneratedLoadSetup generatedLoad = CreateGeneratedLoadFromEditor();
            double[] time;
            double[] values;
            GenerateWaveformValues(generatedLoad, out time, out values);
            //
            FatigueLoadCasePreview loadCase = new FatigueLoadCasePreview();
            loadCase.Name = "LC1";
            loadCase.Repetitions = generatedLoad.Repetitions;
            loadCase.ResultStep = generatedLoad.ResultStep;
            loadCase.Time = time;
            loadCase.Values = new[] { values };
            //
            FatigueLoadsPreview preview = new FatigueLoadsPreview();
            preview.LoadCases.Add(loadCase);
            preview.Components.Add(CreateComponentPreview(generatedLoad.Name, generatedLoad.Unit, values));
            return preview;
        }

        private void GenerateWaveformValues(FatigueGeneratedLoadSetup load, out double[] time, out double[] values)
        {
            if (load.Frequency <= 0) throw new Exception("Frequency must be positive.");
            if (load.SamplesPerSecond <= 0) throw new Exception("Sampling rate must be positive.");
            //
            double totalTime = load.Cycles / load.Frequency;
            int pointCount = Math.Max(2, (int)Math.Ceiling(totalTime * load.SamplesPerSecond));
            pointCount = Math.Min(pointCount, 1000000);
            time = new double[pointCount];
            values = new double[pointCount];
            double phaseCycles = load.Phase / 360.0;
            double phaseRadians = load.Phase * Math.PI / 180.0;
            //
            for (int i = 0; i < pointCount; i++)
            {
                double t = pointCount == 1 ? 0 : totalTime * i / (pointCount - 1);
                double baseValue;
                if (load.Waveform == "Triangular")
                {
                    double x = load.Frequency * t + phaseCycles;
                    baseValue = 2 * Math.Abs(2 * (x - Math.Floor(x + 0.5))) - 1;
                }
                else if (load.Waveform == "Square")
                {
                    baseValue = Math.Sin(2 * Math.PI * load.Frequency * t + phaseRadians) >= 0 ? 1 : -1;
                }
                else
                {
                    baseValue = Math.Sin(2 * Math.PI * load.Frequency * t + phaseRadians);
                }
                time[i] = t;
                values[i] = load.Mean + load.Amplitude * baseValue;
            }
        }

        private FatigueLoadComponentPreview CreateComponentPreview(string name, string unit, double[] values)
        {
            FatigueLoadComponentPreview component = new FatigueLoadComponentPreview();
            component.Name = name;
            component.Unit = unit;
            if (values != null && values.Length > 0)
            {
                component.Min = values.Min();
                component.Max = values.Max();
                component.Mean = values.Average();
                component.Cycles = EstimateCycles(values);
                component.Del = CalculateDel(values, (double)_nudDelM.Value, (double)_nudDelCycles.Value);
            }
            return component;
        }

        private void RefreshComponentMetrics()
        {
            FatigueLoadsPreview preview = _loadPlotPanel.Preview;
            if (preview == null || preview.Components.Count == 0) return;
            //
            int loadCaseIndex = _dgvLoadCases.CurrentRow != null && !_dgvLoadCases.CurrentRow.IsNewRow
                                ? _dgvLoadCases.CurrentRow.Index
                                : 0;
            FatigueLoadCasePreview loadCase = loadCaseIndex >= 0 && loadCaseIndex < preview.LoadCases.Count
                                              ? preview.LoadCases[loadCaseIndex]
                                              : preview.LoadCases[0];
            int count = Math.Min(preview.Components.Count, _dgvComponents.Rows.Count);
            for (int i = 0; i < count; i++)
            {
                double[] values = loadCase.Values != null && i < loadCase.Values.Length ? loadCase.Values[i] : null;
                FatigueLoadComponentPreview component = CreateComponentPreview(preview.Components[i].Name,
                                                                               preview.Components[i].Unit, values);
                _dgvComponents.Rows[i].Cells["Min"].Value = component.Min;
                _dgvComponents.Rows[i].Cells["Max"].Value = component.Max;
                _dgvComponents.Rows[i].Cells["Mean"].Value = component.Mean;
                _dgvComponents.Rows[i].Cells["Cycles"].Value = component.Cycles;
                _dgvComponents.Rows[i].Cells["Del"].Value = component.Del;
            }
            UpdateLoadPlot();
        }

        private double EstimateCycles(double[] values)
        {
            if (values == null || values.Length < 3) return 0;
            int extrema = 0;
            for (int i = 1; i < values.Length - 1; i++)
            {
                if ((values[i] >= values[i - 1] && values[i] > values[i + 1]) ||
                    (values[i] <= values[i - 1] && values[i] < values[i + 1])) extrema++;
            }
            return extrema / 2.0;
        }

        private double CalculateDel(double[] values, double m, double equivalentCycles)
        {
            if (values == null || values.Length == 0 || equivalentCycles <= 0) return 0;
            double range = values.Max() - values.Min();
            if (range <= 0) return 0;
            return Math.Pow(Math.Pow(range, m) / equivalentCycles, 1.0 / m);
        }

        private string[] SplitDelimitedLine(string line, char delimiter)
        {
            return line.Split(delimiter).Select(cell => cell.Trim().Trim('"')).ToArray();
        }

        private static string GetCell(string[] row, int index)
        {
            if (row == null || index < 0 || index >= row.Length || row[index] == null) return "";
            return row[index].Trim();
        }

        private static bool TryParseDouble(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static double ParseDouble(string text, double defaultValue)
        {
            double value;
            return TryParseDouble(text, out value) ? value : defaultValue;
        }

        private void UpdateLoadPlot()
        {
            _loadPlotPanel.ShowMarkers = _cbShowDataMarkers.Checked;
            _loadPlotPanel.SelectedLoadCase = _dgvLoadCases.CurrentRow != null && !_dgvLoadCases.CurrentRow.IsNewRow
                                              ? _dgvLoadCases.CurrentRow.Index
                                              : 0;
            List<string> plottedNames = new List<string>();
            foreach (DataGridViewRow row in _dgvComponents.Rows)
            {
                if (row.IsNewRow) continue;
                if (GetBool(row, "Plot", false)) plottedNames.Add(GetCell(row, "Name", ""));
            }
            _loadPlotPanel.PlottedComponentNames = plottedNames;
            _loadPlotPanel.Invalidate();
        }

        private void SaveCurrentLoadsToCsv(string fileName)
        {
            SaveToSetup();
            WriteCurrentLoadsToCsv(fileName);
        }

        private void WriteCurrentLoadsToCsv(string fileName)
        {
            List<string> lines = new List<string>();
            if (_setup.Loads.GeneratedLoads != null && _setup.Loads.GeneratedLoads.Count > 0)
            {
                double[] time;
                double[] ignoredValues;
                GenerateWaveformValues(_setup.Loads.GeneratedLoads[0], out time, out ignoredValues);
                lines.Add("," + _setup.Loads.GeneratedLoads[0].Repetitions.ToString(CultureInfo.InvariantCulture));
                lines.Add("Time," + string.Join(",", _setup.Loads.GeneratedLoads.Select(l => l.Name)));
                lines.Add("s," + string.Join(",", _setup.Loads.GeneratedLoads.Select(l => l.Unit)));
                for (int r = 0; r < time.Length; r++)
                {
                    List<string> cells = new List<string>();
                    cells.Add(time[r].ToString("R", CultureInfo.InvariantCulture));
                    foreach (FatigueGeneratedLoadSetup load in _setup.Loads.GeneratedLoads)
                        cells.Add(EvaluateGeneratedLoad(load, time[r]).ToString("R", CultureInfo.InvariantCulture));
                    lines.Add(string.Join(",", cells));
                }
                File.WriteAllLines(fileName, lines.ToArray());
                return;
            }
            //
            FatigueLoadsPreview preview = _loadPlotPanel.Preview;
            int loadCaseIndex = _loadPlotPanel.SelectedLoadCase;
            FatigueLoadCasePreview loadCase = preview != null &&
                                              loadCaseIndex >= 0 &&
                                              loadCaseIndex < preview.LoadCases.Count
                                              ? preview.LoadCases[loadCaseIndex]
                                              : null;
            if (preview != null && loadCase != null && loadCase.Time != null && loadCase.Values != null)
            {
                lines.Add("," + loadCase.Repetitions.ToString(CultureInfo.InvariantCulture));
                lines.Add("Time," + string.Join(",", preview.Components.Select(c => c.Name)));
                lines.Add("s," + string.Join(",", preview.Components.Select(c => c.Unit)));
                for (int r = 0; r < loadCase.Time.Length; r++)
                {
                    List<string> cells = new List<string>();
                    cells.Add(loadCase.Time[r].ToString("R", CultureInfo.InvariantCulture));
                    for (int c = 0; c < preview.Components.Count; c++)
                    {
                        double value = loadCase.Values.Length > c && loadCase.Values[c].Length > r
                                       ? loadCase.Values[c][r]
                                       : 0;
                        cells.Add(value.ToString("R", CultureInfo.InvariantCulture));
                    }
                    lines.Add(string.Join(",", cells));
                }
            }
            else
            {
                lines.Add("," + (_setup.Loads.LoadCases.Count > 0 ? _setup.Loads.LoadCases[0].Repetitions.ToString(CultureInfo.InvariantCulture) : "1"));
                lines.Add("Time," + string.Join(",", _setup.Loads.Components.Select(c => c.Name)));
                lines.Add("s," + string.Join(",", _setup.Loads.Components.Select(c => c.Unit)));
            }
            File.WriteAllLines(fileName, lines.ToArray());
        }

        private double EvaluateGeneratedLoad(FatigueGeneratedLoadSetup load, double time)
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

        private string GetCurrentResultFileName()
        {
            if (_controller.ResultsInitialized &&
                _controller.CurrentResult != null &&
                !string.IsNullOrWhiteSpace(_controller.CurrentResult.FileName))
                return _controller.CurrentResult.FileName;
            return null;
        }

        private string GetCurrentResultName()
        {
            if (!string.IsNullOrWhiteSpace(GetCurrentResultFileName()))
                return Path.GetFileNameWithoutExtension(GetCurrentResultFileName());
            if (_controller.ResultsInitialized && _controller.AllResults != null)
                return _controller.AllResults.GetCurrentResultName();
            return null;
        }

        private string[] GetCurrentVisibleResultPartNames()
        {
            if (_controller.ResultsInitialized &&
                _controller.CurrentResult != null &&
                _controller.CurrentResult.Mesh != null)
                return _controller.CurrentResult.Mesh.GetVisibleAndActivePartNames();
            return new string[0];
        }

        private string ResolveInputFileName(AnalysisJob job, string frdFileName)
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

        private void UpdateModelStatus()
        {
            if (!_controller.ResultsInitialized ||
                _controller.CurrentResult == null ||
                _controller.CurrentResult.Mesh == null)
            {
                _lbModelStatus.ForeColor = Color.DarkRed;
                _lbModelStatus.Text = "No active results are available. Open a PMX with results before running fatigue.";
                return;
            }
            //
            string[] visiblePartNames = GetCurrentVisibleResultPartNames();
            if (visiblePartNames.Length > 0)
            {
                _lbModelStatus.ForeColor = Color.DarkGreen;
                _lbModelStatus.Text = "Fatigue model uses " + visiblePartNames.Length + " visible result part(s).";
            }
            else
            {
                _lbModelStatus.ForeColor = Color.DarkRed;
                _lbModelStatus.Text = "No result parts are visible. Show at least one part for fatigue.";
            }
        }
    }

    internal class FatigueLoadsPreview
    {
        public List<FatigueLoadCasePreview> LoadCases { get; private set; }
        public List<FatigueLoadComponentPreview> Components { get; private set; }

        public FatigueLoadsPreview()
        {
            LoadCases = new List<FatigueLoadCasePreview>();
            Components = new List<FatigueLoadComponentPreview>();
        }
    }

    internal class FatigueLoadCasePreview
    {
        public string Name { get; set; }
        public double Repetitions { get; set; }
        public int ResultStep { get; set; }
        public double[] Time { get; set; }
        public double[][] Values { get; set; }

        public FatigueLoadCasePreview()
        {
            Name = "LC1";
            Repetitions = 1;
            ResultStep = 1;
            Time = new double[0];
            Values = new double[0][];
        }
    }

    internal class FatigueLoadComponentPreview
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Mean { get; set; }
        public double Cycles { get; set; }
        public double Del { get; set; }

        public FatigueLoadComponentPreview()
        {
            Name = "";
            Unit = "";
        }
    }

    internal class SnCurvePlotPanel : Panel
    {
        public double Ds1 { get; set; }
        public double N1 { get; set; }
        public double N2 { get; set; }
        public double M1 { get; set; }
        public double M2 { get; set; }
        public double Gf { get; set; }
        public double MinStress { get; set; }
        public double MaxStress { get; set; }

        public SnCurvePlotPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            Rectangle plot = new Rectangle(72, 48, Math.Max(40, Width - 120), Math.Max(40, Height - 104));
            using (Font titleFont = new Font(Font.FontFamily, Font.Size + 2, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.Black))
            {
                DrawText(g, "SN curve", titleFont, Color.Black, new Rectangle(0, 12, Width, 24),
                         ContentAlignment.MiddleCenter);
                DrawAxes(g, plot, "Fatigue life, Nf [cycles]", "Stress range, dS [MPa]");
                if (!HasValidInput())
                {
                    DrawText(g, "SN curve parameters must be positive.", Font, Color.Gray, plot,
                             ContentAlignment.MiddleCenter);
                    return;
                }
                //
                double minX = 1e3;
                double maxX = Math.Max(1e10, N2 * 100);
                double ds1Design = Ds1 / Math.Max(Gf, 1e-12);
                double ds2Design = StressAt(N2, ds1Design, N1, N2, M1, M2);
                double maxY = Math.Max(1000, Math.Max(Ds1, ds1Design) * 10);
                if (MaxStress > 0) maxY = Math.Max(maxY, MaxStress);
                double minY = Math.Max(1, MinStress > 0 ? MinStress : 1);
                if (minY >= maxY) minY = Math.Max(1, maxY / 1000);
                //
                DrawLogGrid(g, plot, minX, maxX, minY, maxY);
                DrawSnPolyline(g, plot, minX, maxX, minY, maxY, Ds1, DashStyle.Dash, Color.Red);
                DrawSnPolyline(g, plot, minX, maxX, minY, maxY, ds1Design, DashStyle.Solid, Color.Red);
                DrawPoint(g, plot, minX, maxX, minY, maxY, N1, ds1Design, ds1Design.ToString("0.###", CultureInfo.InvariantCulture));
                DrawPoint(g, plot, minX, maxX, minY, maxY, N2, ds2Design, ds2Design.ToString("0.###", CultureInfo.InvariantCulture));
                DrawSlopeLabels(g, plot, minX, maxX, minY, maxY);
            }
        }

        private bool HasValidInput()
        {
            return Ds1 > 0 && N1 > 0 && N2 > 0 && M1 > 0 && M2 > 0 && Gf > 0;
        }

        private void DrawSnPolyline(Graphics g, Rectangle plot, double minX, double maxX, double minY, double maxY,
                                    double referenceStress, DashStyle dashStyle, Color color)
        {
            List<PointF> points = new List<PointF>();
            for (int i = 0; i < 180; i++)
            {
                double ratio = i / 179.0;
                double cycles = Math.Pow(10, Math.Log10(minX) + ratio * (Math.Log10(maxX) - Math.Log10(minX)));
                double stress = StressAt(cycles, referenceStress, N1, N2, M1, M2);
                if (stress > 0) points.Add(ToLogPoint(plot, cycles, stress, minX, maxX, minY, maxY));
            }
            if (points.Count < 2) return;
            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.DashStyle = dashStyle;
                g.DrawLines(pen, points.ToArray());
            }
        }

        private static double StressAt(double cycles, double referenceStress, double n1, double n2, double m1, double m2)
        {
            double kneeStress = referenceStress * Math.Pow(n1 / n2, 1.0 / m1);
            return cycles <= n2
                   ? referenceStress * Math.Pow(n1 / cycles, 1.0 / m1)
                   : kneeStress * Math.Pow(n2 / cycles, 1.0 / m2);
        }

        private void DrawPoint(Graphics g, Rectangle plot, double minX, double maxX, double minY, double maxY,
                               double x, double y, string label)
        {
            PointF point = ToLogPoint(plot, x, y, minX, maxX, minY, maxY);
            using (Brush brush = new SolidBrush(Color.White))
            using (Pen pen = new Pen(Color.Red, 1.4f))
            {
                g.FillEllipse(brush, point.X - 4, point.Y - 4, 8, 8);
                g.DrawEllipse(pen, point.X - 4, point.Y - 4, 8, 8);
            }
            DrawText(g, label, Font, Color.Black, new Rectangle((int)point.X + 6, (int)point.Y - 10, 70, 20),
                     ContentAlignment.MiddleLeft);
        }

        private void DrawSlopeLabels(Graphics g, Rectangle plot, double minX, double maxX, double minY, double maxY)
        {
            double ds1Design = Ds1 / Math.Max(Gf, 1e-12);
            PointF p1 = ToLogPoint(plot, Math.Sqrt(N1 * Math.Min(N2, N1 * 10)), StressAt(Math.Sqrt(N1 * Math.Min(N2, N1 * 10)), ds1Design, N1, N2, M1, M2), minX, maxX, minY, maxY);
            PointF p2 = ToLogPoint(plot, Math.Sqrt(N2 * maxX), StressAt(Math.Sqrt(N2 * maxX), ds1Design, N1, N2, M1, M2), minX, maxX, minY, maxY);
            DrawText(g, "m1=" + M1.ToString("0.###", CultureInfo.InvariantCulture), Font, Color.Black,
                     new Rectangle((int)p1.X, (int)p1.Y + 8, 80, 18), ContentAlignment.MiddleLeft);
            DrawText(g, "m2=" + M2.ToString("0.###", CultureInfo.InvariantCulture), Font, Color.Black,
                     new Rectangle((int)p2.X, (int)p2.Y + 8, 80, 18), ContentAlignment.MiddleLeft);
        }

        private void DrawLogGrid(Graphics g, Rectangle plot, double minX, double maxX, double minY, double maxY)
        {
            using (Pen major = new Pen(Color.Gainsboro))
            using (Pen minor = new Pen(Color.LightGray))
            using (Pen axis = new Pen(Color.Black))
            {
                minor.DashStyle = DashStyle.Dot;
                for (int decade = (int)Math.Floor(Math.Log10(minX)); decade <= (int)Math.Ceiling(Math.Log10(maxX)); decade++)
                {
                    for (int tick = 1; tick < 10; tick++)
                    {
                        double x = tick * Math.Pow(10, decade);
                        if (x < minX || x > maxX) continue;
                        PointF p = ToLogPoint(plot, x, minY, minX, maxX, minY, maxY);
                        g.DrawLine(tick == 1 ? major : minor, p.X, plot.Top, p.X, plot.Bottom);
                        if (tick == 1)
                            DrawText(g, FormatPower(1, decade), Font, Color.Black,
                                     new Rectangle((int)p.X - 40, plot.Bottom + 5, 80, 20),
                                     ContentAlignment.MiddleCenter);
                    }
                }
                for (int decade = (int)Math.Floor(Math.Log10(minY)); decade <= (int)Math.Ceiling(Math.Log10(maxY)); decade++)
                {
                    for (int tick = 1; tick < 10; tick++)
                    {
                        double y = tick * Math.Pow(10, decade);
                        if (y < minY || y > maxY) continue;
                        PointF p = ToLogPoint(plot, minX, y, minX, maxX, minY, maxY);
                        g.DrawLine(tick == 1 ? major : minor, plot.Left, p.Y, plot.Right, p.Y);
                        if (tick == 1)
                            DrawText(g, Math.Pow(10, decade).ToString("0.###", CultureInfo.InvariantCulture), Font,
                                     Color.Black, new Rectangle(4, (int)p.Y - 10, plot.Left - 10, 20),
                                     ContentAlignment.MiddleRight);
                    }
                }
                g.DrawRectangle(axis, plot);
            }
        }

        private void DrawAxes(Graphics g, Rectangle plot, string xLabel, string yLabel)
        {
            using (Brush textBrush = new SolidBrush(Color.Black))
            {
                DrawText(g, xLabel, Font, Color.Black, new Rectangle(plot.Left, plot.Bottom + 32, plot.Width, 22),
                         ContentAlignment.MiddleCenter);
                DrawRotatedText(g, yLabel, Font, textBrush, 16, plot.Top + plot.Height / 2 + 58);
            }
        }

        private static PointF ToLogPoint(Rectangle plot, double x, double y, double minX, double maxX, double minY, double maxY)
        {
            double lx = (Math.Log10(Math.Max(x, minX)) - Math.Log10(minX)) / (Math.Log10(maxX) - Math.Log10(minX));
            double ly = (Math.Log10(Math.Max(y, minY)) - Math.Log10(minY)) / (Math.Log10(maxY) - Math.Log10(minY));
            return new PointF((float)(plot.Left + lx * plot.Width), (float)(plot.Bottom - ly * plot.Height));
        }

        private static string FormatPower(int number, int power)
        {
            return number.ToString(CultureInfo.InvariantCulture) + "e" + power.ToString(CultureInfo.InvariantCulture);
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, ContentAlignment alignment)
        {
            TextRenderer.DrawText(g, text, font, bounds, color,
                                  TextFormatFlags.VerticalCenter | GetTextFormatFlags(alignment));
        }

        private static TextFormatFlags GetTextFormatFlags(ContentAlignment alignment)
        {
            if (alignment == ContentAlignment.MiddleRight) return TextFormatFlags.Right;
            if (alignment == ContentAlignment.MiddleCenter) return TextFormatFlags.HorizontalCenter;
            return TextFormatFlags.Left;
        }

        private static void DrawRotatedText(Graphics g, string text, Font font, Brush brush, int x, int y)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(-90);
            g.DrawString(text, font, brush, new PointF(0, 0));
            g.Restore(state);
        }
    }

    internal class MeanStressPlotPanel : Panel
    {
        public double Re { get; set; }
        public double Rm { get; set; }
        public double M { get; set; }
        public double ReferenceAmplitude { get; set; }
        public string MeanStressMode { get; set; }
        public bool ObserveYieldLines { get; set; }
        public bool ExtrapolateCompression { get; set; }

        public MeanStressPlotPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            Rectangle plot = new Rectangle(78, 66, Math.Max(40, Width - 112), Math.Max(40, Height - 120));
            using (Font titleFont = new Font(Font.FontFamily, Font.Size + 2, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.Black))
            {
                DrawText(g, "Mean stress effect", titleFont, Color.Black, new Rectangle(0, 18, Width, 24),
                         ContentAlignment.MiddleCenter);
                DrawAxes(g, plot, "Mean stress sm [MPa]", "Stress amplitude sa [MPa]");
                if (Re <= 0 || Rm <= 0)
                {
                    DrawText(g, "Material strengths must be positive.", Font, Color.Gray, plot,
                             ContentAlignment.MiddleCenter);
                    return;
                }
                double xLimit = Math.Max(Math.Max(Re, Rm), Math.Max(ReferenceAmplitude * 3.0, 1.0)) * 1.08;
                List<PointF> meanCurve = BuildMeanStressCurve(plot, -xLimit, xLimit, out double maxAmplitude);
                double yLimit = Math.Max(Re, Math.Max(Rm, Math.Max(ReferenceAmplitude, maxAmplitude))) * 1.18;
                meanCurve = BuildMeanStressCurve(plot, -xLimit, xLimit, 0, yLimit);
                DrawGrid(g, plot, -xLimit, xLimit, 0, yLimit);
                PointF left = ToPoint(plot, -Re, 0, -xLimit, xLimit, 0, yLimit);
                PointF top = ToPoint(plot, 0, Re, -xLimit, xLimit, 0, yLimit);
                PointF right = ToPoint(plot, Rm, 0, -xLimit, xLimit, 0, yLimit);
                using (Pen pen = new Pen(Color.Black, 1f))
                using (Pen red = new Pen(Color.Red, 1.2f))
                using (Pen reference = new Pen(Color.Red, 1f))
                {
                    reference.DashStyle = DashStyle.Dash;
                    g.DrawLine(pen, left, top);
                    g.DrawLine(pen, top, right);
                    g.DrawLine(pen, ToPoint(plot, 0, 0, -xLimit, xLimit, 0, yLimit), top);
                    g.DrawLine(pen, ToPoint(plot, -Re, Re * 0.75, -xLimit, xLimit, 0, yLimit),
                               ToPoint(plot, 0, 0, -xLimit, xLimit, 0, yLimit));
                    g.DrawLine(pen, ToPoint(plot, 0, 0, -xLimit, xLimit, 0, yLimit),
                               ToPoint(plot, Rm * 0.75, Re * 0.75, -xLimit, xLimit, 0, yLimit));
                    PointF ampLeft = ToPoint(plot, -xLimit, ReferenceAmplitude, -xLimit, xLimit, 0, yLimit);
                    PointF ampRight = ToPoint(plot, xLimit, ReferenceAmplitude, -xLimit, xLimit, 0, yLimit);
                    g.DrawLine(reference, ampLeft, ampRight);
                    if (meanCurve.Count > 1) g.DrawLines(red, meanCurve.ToArray());
                    PointF ampPoint = ToPoint(plot, 0, ReferenceAmplitude, -xLimit, xLimit, 0, yLimit);
                    g.DrawEllipse(red, ampPoint.X - 4, ampPoint.Y - 4, 8, 8);
                }
                DrawText(g, "Re= " + Re.ToString("0.###", CultureInfo.InvariantCulture) + "MPa",
                         Font, Color.Black, new Rectangle(plot.Left + 20, plot.Top + 14, 120, 20), ContentAlignment.MiddleLeft);
                DrawText(g, "Rm= " + Rm.ToString("0.###", CultureInfo.InvariantCulture) + "MPa",
                         Font, Color.Black, new Rectangle(plot.Left + 20, plot.Top + 34, 120, 20), ContentAlignment.MiddleLeft);
                DrawText(g, "R=-1", Font, Color.Black, new Rectangle((int)top.X - 28, (int)top.Y - 28, 56, 20),
                         ContentAlignment.MiddleCenter);
                DrawText(g, "Re", Font, Color.Black, new Rectangle((int)top.X + 18, (int)top.Y - 8, 50, 20),
                         ContentAlignment.MiddleLeft);
                DrawText(g, "R=0", Font, Color.Black, new Rectangle((int)right.X - 52, (int)(plot.Top + plot.Height * 0.40), 48, 20),
                         ContentAlignment.MiddleRight);
                DrawText(g, "R=-inf", Font, Color.Black, new Rectangle(plot.Left + 72, (int)(plot.Top + plot.Height * 0.40), 70, 20),
                         ContentAlignment.MiddleLeft);
                DrawText(g, "-Re", Font, Color.Black, new Rectangle((int)left.X - 26, plot.Bottom - 25, 52, 20),
                         ContentAlignment.MiddleCenter);
                DrawText(g, "Rm", Font, Color.Black, new Rectangle((int)right.X - 26, plot.Bottom - 25, 52, 20),
                         ContentAlignment.MiddleCenter);
                DrawText(g, "Mode: " + (MeanStressMode ?? "None"), Font, Color.Black,
                         new Rectangle(plot.Right - 210, plot.Top + 12, 190, 20), ContentAlignment.MiddleRight);
            }
        }

        private List<PointF> BuildMeanStressCurve(Rectangle plot, double minX, double maxX, out double maxAmplitude)
        {
            maxAmplitude = ReferenceAmplitude;
            List<PointF> points = new List<PointF>();
            const int sampleCount = 180;
            for (int i = 0; i <= sampleCount; i++)
            {
                double sm = minX + (maxX - minX) * i / sampleCount;
                double sa = CalculateAllowableAmplitude(sm);
                if (double.IsNaN(sa) || double.IsInfinity(sa) || sa < 0) continue;
                maxAmplitude = Math.Max(maxAmplitude, sa);
                points.Add(new PointF((float)sm, (float)sa));
            }
            return points;
        }

        private List<PointF> BuildMeanStressCurve(Rectangle plot, double minX, double maxX, double minY, double maxY)
        {
            List<PointF> points = new List<PointF>();
            const int sampleCount = 180;
            for (int i = 0; i <= sampleCount; i++)
            {
                double sm = minX + (maxX - minX) * i / sampleCount;
                double sa = CalculateAllowableAmplitude(sm);
                if (double.IsNaN(sa) || double.IsInfinity(sa) || sa < 0) continue;
                if (sa > maxY) continue;
                points.Add(ToPoint(plot, sm, sa, minX, maxX, minY, maxY));
            }
            return points;
        }

        private double CalculateAllowableAmplitude(double sm)
        {
            double sa2 = Math.Max(0, ReferenceAmplitude);
            double m = M;
            double re = Math.Max(1e-12, Re);
            double rm = Math.Max(1e-12, Rm);
            double sa;
            switch ((MeanStressMode ?? "None").Trim().ToLowerInvariant())
            {
                case "linear":
                    sa = sa2 - m * sm;
                    break;
                case "bilinear":
                    {
                        double saR0 = sa2 / Math.Max(1e-12, 1.0 + m);
                        if (sm <= saR0) sa = sa2 - m * sm;
                        else sa = saR0 * (rm - sm) / Math.Max(1e-12, rm - saR0);
                        break;
                    }
                case "soderberg":
                    sa = sa2 * (1.0 - sm / re);
                    break;
                case "modified goodman":
                    sa = sa2 * (1.0 - sm / rm);
                    break;
                case "gerber parabola":
                    sa = sm < 0 ? sa2 : sa2 * (1.0 - sm * sm / (rm * rm));
                    break;
                case "smith-watson-topper":
                    sa = sm <= 0 ? sa2 : -0.5 * (sm - Math.Sqrt(sm * sm + 4.0 * sa2 * sa2));
                    break;
                case "60% compression":
                    if (sm < -sa2 / 0.6) sa = sa2 / 0.6;
                    else if (sm <= sa2) sa = 1.25 * sa2 - 0.25 * sm;
                    else sa = sa2;
                    break;
                case "iiw low rs":
                    if (sm < 0) sa = 1.6 * sa2;
                    else if (sm <= 3.0 * sa2) sa = ((-0.6 / Math.Max(1e-12, 3.0 * sa2)) * sm + 1.6) * sa2;
                    else sa = sa2;
                    break;
                case "iiw medium rs":
                    if (sm < 0) sa = 1.3 * sa2;
                    else if (sm <= 3.0 * sa2) sa = ((-0.3 / Math.Max(1e-12, 3.0 * sa2)) * sm + 1.3) * sa2;
                    else sa = sa2;
                    break;
                case "fkm":
                    {
                        double saR0 = sa2 / Math.Max(1e-12, 1.0 + m);
                        double saRni = sa2 / Math.Max(1e-12, 1.0 - m);
                        double smR05 = saR0 * (3.0 + m) / Math.Max(1e-12, 1.0 + m);
                        if (sm <= -saRni) sa = saRni;
                        else if (sm < saR0) sa = sa2 - m * sm;
                        else if (sm < smR05) sa = saR0 * (1.0 + m / 3.0) - m / 3.0 * sm;
                        else sa = smR05 / 3.0;
                        break;
                    }
                default:
                    sa = sa2;
                    break;
            }
            if (!ExtrapolateCompression && sm < 0 && GetMeanModeIndex() < 8) sa = sa2;
            if (ObserveYieldLines)
            {
                double yieldLine = sm <= 0 ? re + sm : re - sm;
                sa = Math.Min(yieldLine, sa);
            }
            return Math.Max(0, sa);
        }

        private int GetMeanModeIndex()
        {
            switch ((MeanStressMode ?? "None").Trim().ToLowerInvariant())
            {
                case "linear": return 2;
                case "bilinear": return 3;
                case "soderberg": return 4;
                case "modified goodman": return 5;
                case "gerber parabola": return 6;
                case "smith-watson-topper": return 7;
                case "60% compression": return 8;
                case "iiw low rs": return 9;
                case "iiw medium rs": return 10;
                case "fkm": return 11;
                default: return 1;
            }
        }

        private void DrawGrid(Graphics g, Rectangle plot, double minX, double maxX, double minY, double maxY)
        {
            using (Pen grid = new Pen(Color.Gainsboro))
            using (Pen axis = new Pen(Color.Black))
            {
                for (int i = 0; i <= 5; i++)
                {
                    double x = minX + (maxX - minX) * i / 5.0;
                    PointF p = ToPoint(plot, x, minY, minX, maxX, minY, maxY);
                    g.DrawLine(grid, p.X, plot.Top, p.X, plot.Bottom);
                    DrawText(g, x.ToString("0", CultureInfo.InvariantCulture), Font, Color.Black,
                             new Rectangle((int)p.X - 34, plot.Bottom + 5, 68, 20), ContentAlignment.MiddleCenter);
                }
                for (int i = 0; i <= 5; i++)
                {
                    double y = minY + (maxY - minY) * i / 5.0;
                    PointF p = ToPoint(plot, minX, y, minX, maxX, minY, maxY);
                    g.DrawLine(grid, plot.Left, p.Y, plot.Right, p.Y);
                    DrawText(g, y.ToString("0", CultureInfo.InvariantCulture), Font, Color.Black,
                             new Rectangle(4, (int)p.Y - 10, plot.Left - 10, 20), ContentAlignment.MiddleRight);
                }
                g.DrawRectangle(axis, plot);
            }
        }

        private void DrawAxes(Graphics g, Rectangle plot, string xLabel, string yLabel)
        {
            using (Brush textBrush = new SolidBrush(Color.Black))
            {
                DrawText(g, xLabel, Font, Color.Black, new Rectangle(plot.Left, plot.Bottom + 36, plot.Width, 22),
                         ContentAlignment.MiddleCenter);
                DrawRotatedText(g, yLabel, Font, textBrush, 18, plot.Top + plot.Height / 2 + 62);
            }
        }

        private static PointF ToPoint(Rectangle plot, double x, double y, double minX, double maxX, double minY, double maxY)
        {
            float px = (float)(plot.Left + (x - minX) / (maxX - minX) * plot.Width);
            float py = (float)(plot.Bottom - (y - minY) / (maxY - minY) * plot.Height);
            return new PointF(px, py);
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, ContentAlignment alignment)
        {
            TextRenderer.DrawText(g, text, font, bounds, color,
                                  TextFormatFlags.VerticalCenter | GetTextFormatFlags(alignment));
        }

        private static TextFormatFlags GetTextFormatFlags(ContentAlignment alignment)
        {
            if (alignment == ContentAlignment.MiddleRight) return TextFormatFlags.Right;
            if (alignment == ContentAlignment.MiddleCenter) return TextFormatFlags.HorizontalCenter;
            return TextFormatFlags.Left;
        }

        private static void DrawRotatedText(Graphics g, string text, Font font, Brush brush, int x, int y)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(-90);
            g.DrawString(text, font, brush, new PointF(0, 0));
            g.Restore(state);
        }
    }

    internal class LoadPlotPanel : Panel
    {
        private static readonly Color[] LineColors = new[]
        {
            Color.FromArgb(0, 114, 189),
            Color.FromArgb(217, 83, 25),
            Color.FromArgb(237, 177, 32),
            Color.FromArgb(126, 47, 142),
            Color.FromArgb(119, 172, 48),
            Color.FromArgb(77, 190, 238)
        };

        public FatigueLoadsPreview Preview { get; set; }
        public int SelectedLoadCase { get; set; }
        public List<string> PlottedComponentNames { get; set; }
        public bool ShowMarkers { get; set; }

        public LoadPlotPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            PlottedComponentNames = new List<string>();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(Color.White);
            Rectangle plot = new Rectangle(65, 35, Math.Max(20, Width - 115), Math.Max(20, Height - 90));
            using (Pen axisPen = new Pen(Color.Black))
            using (Brush textBrush = new SolidBrush(Color.Black))
            using (Font boldFont = new Font(Font, FontStyle.Bold))
            {
                g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
                g.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
                DrawText(g, "Time", Font, Color.Black, new Rectangle(plot.Left, plot.Bottom + 28, plot.Width, 20),
                         ContentAlignment.MiddleCenter);
                DrawRotatedText(g, "Load", Font, textBrush, plot.Left - 45, plot.Top + plot.Height / 2);
                //
                FatigueLoadCasePreview loadCase = GetSelectedLoadCase();
                if (Preview == null || loadCase == null || Preview.Components.Count == 0 ||
                    loadCase.Time == null || loadCase.Time.Length == 0)
                {
                    DrawText(g, "No load data preview", Font, Color.Gray, plot, ContentAlignment.MiddleCenter);
                    return;
                }
                //
                DrawText(g, "Load case " + (SelectedLoadCase + 1) + ": " + loadCase.Name, boldFont, Color.Black,
                         new Rectangle(plot.Left, 8, plot.Width, 24), ContentAlignment.MiddleCenter);
                //
                double minX = loadCase.Time.Min();
                double maxX = loadCase.Time.Max();
                double minY = double.MaxValue;
                double maxY = double.MinValue;
                List<int> plotted = GetPlottedComponentIndexes();
                foreach (int index in plotted)
                {
                    if (loadCase.Values == null || index >= loadCase.Values.Length || loadCase.Values[index].Length == 0) continue;
                    minY = Math.Min(minY, loadCase.Values[index].Min());
                    maxY = Math.Max(maxY, loadCase.Values[index].Max());
                }
                if (plotted.Count == 0 || minY == double.MaxValue)
                {
                    DrawText(g, "Select at least one component to plot", Font, Color.Gray, plot, ContentAlignment.MiddleCenter);
                    return;
                }
                if (Math.Abs(maxX - minX) < 1e-12) maxX = minX + 1;
                if (Math.Abs(maxY - minY) < 1e-12)
                {
                    minY -= 0.5;
                    maxY += 0.5;
                }
                DrawTicks(g, plot, minX, maxX, minY, maxY);
                //
                for (int p = 0; p < plotted.Count; p++)
                {
                    int index = plotted[p];
                    if (index >= loadCase.Values.Length) continue;
                    double[] values = loadCase.Values[index];
                    int count = Math.Min(loadCase.Time.Length, values.Length);
                    if (count < 2) continue;
                    PointF[] points = new PointF[count];
                    for (int i = 0; i < count; i++)
                        points[i] = ToPoint(plot, loadCase.Time[i], values[i], minX, maxX, minY, maxY);
                    Color color = LineColors[p % LineColors.Length];
                    using (Pen linePen = new Pen(color, 1.2f))
                    using (Brush markerBrush = new SolidBrush(color))
                    {
                        g.DrawLines(linePen, points);
                        if (ShowMarkers)
                        {
                            int step = Math.Max(1, count / 250);
                            for (int i = 0; i < count; i += step)
                                g.FillEllipse(markerBrush, points[i].X - 2, points[i].Y - 2, 4, 4);
                        }
                    }
                }
                DrawLegend(g, plot, plotted);
            }
        }

        private FatigueLoadCasePreview GetSelectedLoadCase()
        {
            if (Preview == null || Preview.LoadCases.Count == 0) return null;
            int index = Math.Min(Math.Max(SelectedLoadCase, 0), Preview.LoadCases.Count - 1);
            return Preview.LoadCases[index];
        }

        private List<int> GetPlottedComponentIndexes()
        {
            List<int> indexes = new List<int>();
            if (Preview == null) return indexes;
            for (int i = 0; i < Preview.Components.Count; i++)
            {
                string name = Preview.Components[i].Name;
                if (PlottedComponentNames == null || PlottedComponentNames.Count == 0 ||
                    PlottedComponentNames.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
                    indexes.Add(i);
            }
            return indexes;
        }

        private void DrawTicks(Graphics g, Rectangle plot, double minX, double maxX, double minY, double maxY)
        {
            using (Pen pen = new Pen(Color.Black))
            {
                for (int i = 0; i <= 5; i++)
                {
                    float x = plot.Left + plot.Width * i / 5f;
                    g.DrawLine(pen, x, plot.Bottom, x, plot.Bottom + 5);
                    string label = FormatNumber(minX + (maxX - minX) * i / 5.0);
                    DrawText(g, label, Font, Color.Black, new Rectangle((int)x - 35, plot.Bottom + 7, 70, 18),
                             ContentAlignment.MiddleCenter);
                }
                for (int i = 0; i <= 5; i++)
                {
                    float y = plot.Bottom - plot.Height * i / 5f;
                    g.DrawLine(pen, plot.Left - 5, y, plot.Left, y);
                    string label = FormatNumber(minY + (maxY - minY) * i / 5.0);
                    DrawText(g, label, Font, Color.Black, new Rectangle(0, (int)y - 9, plot.Left - 8, 18),
                             ContentAlignment.MiddleRight);
                }
            }
        }

        private void DrawLegend(Graphics g, Rectangle plot, List<int> plotted)
        {
            int width = 105;
            int height = Math.Max(22, plotted.Count * 20 + 6);
            Rectangle legend = new Rectangle(plot.Right - width - 10, plot.Top + 10, width, height);
            using (Brush back = new SolidBrush(Color.White))
            using (Pen border = new Pen(Color.Black))
            {
                g.FillRectangle(back, legend);
                g.DrawRectangle(border, legend);
            }
            for (int i = 0; i < plotted.Count; i++)
            {
                Color color = LineColors[i % LineColors.Length];
                int y = legend.Top + 12 + i * 20;
                using (Pen pen = new Pen(color, 1.5f))
                {
                    g.DrawLine(pen, legend.Left + 8, y, legend.Left + 42, y);
                    DrawText(g, Preview.Components[plotted[i]].Name, Font, Color.Black,
                             new Rectangle(legend.Left + 47, y - 9, legend.Width - 50, 18),
                             ContentAlignment.MiddleLeft);
                }
            }
        }

        private static PointF ToPoint(Rectangle plot, double x, double y, double minX, double maxX, double minY, double maxY)
        {
            float px = (float)(plot.Left + (x - minX) / (maxX - minX) * plot.Width);
            float py = (float)(plot.Bottom - (y - minY) / (maxY - minY) * plot.Height);
            return new PointF(px, py);
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, ContentAlignment alignment)
        {
            TextRenderer.DrawText(g, text, font, bounds, color,
                                  TextFormatFlags.VerticalCenter | GetTextFormatFlags(alignment));
        }

        private static TextFormatFlags GetTextFormatFlags(ContentAlignment alignment)
        {
            if (alignment == ContentAlignment.MiddleRight) return TextFormatFlags.Right;
            if (alignment == ContentAlignment.MiddleCenter) return TextFormatFlags.HorizontalCenter;
            return TextFormatFlags.Left;
        }

        private static void DrawRotatedText(Graphics g, string text, Font font, Brush brush, int x, int y)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(-90);
            g.DrawString(text, font, brush, new PointF(0, 0));
            g.Restore(state);
        }
    }
}
