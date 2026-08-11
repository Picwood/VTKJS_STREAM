// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using CaeGlobals;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.IO.Compression;
using DynamicTypeDescriptor;

namespace PrePoMax
{
    [Serializable]
    public enum RunPostProcessingEnum
    {
        [StandardValue("Yes", DisplayName = "Yes")]
        Yes,
        //
        [StandardValue("No", DisplayName = "No")]
        No,
        //
        [StandardValue("Ask", DisplayName = "Ask")]
        Ask,
    }
    [Serializable]
    public class GeneralSettings : ISettings
    {
        // Variables                                                                                                                
        private bool _openLastFile;
        private string _lastFileName;
        private bool _saveResultsInPmx;
        private CompressionLevel _compressionLevel;
        private UnitSystemType _unitSystemType;
        // CAD periodic surface split
        private int _numOfSplitFaces;
        // Mesh edge angle
        private double _edgeAngle;
        // Python integration
        private string _pythonExecutable;
        private string _sweepMeshPythonScript;
        private string _cylindricalExtrusionPythonScript;
        // Post-processing
        private RunPostProcessingEnum _runHistoryPostprocessing;
        //
        private LinkedList<string> _recentFiles;
        private List<string> _materialLibraryFiles;
        // Form size and position
        private FormWindowState _formWindowState;
        private Size _formSize;
        private double _formRelativeXLocation;
        private double _formRelativeYLocation;


        // Properties                                                                                                               
        public bool OpenLastFile { get { return _openLastFile; } set { _openLastFile = value; } }
        public string LastFileName { get { return _lastFileName; } set { _lastFileName = value; } }
        public bool SaveResultsInPmx { get { return _saveResultsInPmx; } set { _saveResultsInPmx = value; } }
        public CompressionLevel CompressionLevel { get { return _compressionLevel; } set { _compressionLevel = value; } }
        public UnitSystemType UnitSystemType { get { return _unitSystemType; } set { _unitSystemType = value; } }
        public int NumOfSplitFaces
        {
            get { return _numOfSplitFaces; }
            set
            {
                _numOfSplitFaces = value;
                if (_numOfSplitFaces < 1) _numOfSplitFaces = 1;
            }
        }
        public double EdgeAngle
        {
            get { return _edgeAngle; }
            set
            {
                _edgeAngle = value;
                if (_edgeAngle < 0) _edgeAngle = 0;
                else if (_edgeAngle > 90) _edgeAngle = 90;
            }
        }
        public string PythonExecutable
        {
            get { return Tools.GetGlobalPath(_pythonExecutable); }
            set
            {
                string path = Tools.GetGlobalPath(value);
                if (string.IsNullOrWhiteSpace(path)) _pythonExecutable = null;
                else _pythonExecutable = Tools.GetLocalPath(path);
            }
        }
        public string SweepMeshPythonScript
        {
            get { return Tools.GetGlobalPath(_sweepMeshPythonScript); }
            set
            {
                string path = Tools.GetGlobalPath(value);
                if (string.IsNullOrWhiteSpace(path)) _sweepMeshPythonScript = null;
                else _sweepMeshPythonScript = Tools.GetLocalPath(path);
            }
        }
        public string CylindricalExtrusionPythonScript
        {
            get { return Tools.GetGlobalPath(_cylindricalExtrusionPythonScript); }
            set
            {
                string path = Tools.GetGlobalPath(value);
                if (string.IsNullOrWhiteSpace(path)) _cylindricalExtrusionPythonScript = null;
                else _cylindricalExtrusionPythonScript = Tools.GetLocalPath(path);
            }
        }
        public RunPostProcessingEnum RunHistoryPostprocessing
        {
            get { return _runHistoryPostprocessing; }
            set { _runHistoryPostprocessing = value; }
        }


        // Constructors                                                                                                             
        public GeneralSettings()
        {
            Reset();
        }


        // Methods                                                                                                                  
        public void CheckValues()
        {
        }
        public void Reset()
        {
            _openLastFile = false;
            _lastFileName = null;
            _saveResultsInPmx = true;
            _compressionLevel = CompressionLevel.Fastest;
            _unitSystemType = UnitSystemType.MM_TON_S_C;
            _numOfSplitFaces = 2;
            _edgeAngle = 30;
            _pythonExecutable = null;
            _sweepMeshPythonScript = null;
            _cylindricalExtrusionPythonScript = null;
            _runHistoryPostprocessing = RunPostProcessingEnum.Ask;
            //
            ResetFormSize();
        }
        // Recent files
        public string[] GetRecentFiles()
        {
            if (_recentFiles != null) return _recentFiles.ToArray();
            else return null;
        }
        public void AddRecentFile(string fileNameWithPath)
        {
            if (_recentFiles == null) _recentFiles = new LinkedList<string>();
            //
            if (_recentFiles.Count == 0) _recentFiles.AddFirst(fileNameWithPath);
            else
            {
                if (!_recentFiles.Contains(fileNameWithPath))
                {
                    int _maxRecentFiles = 15;
                    while (_recentFiles.Count >= _maxRecentFiles) _recentFiles.RemoveLast();
                    _recentFiles.AddFirst(fileNameWithPath);
                }
                else
                {
                    _recentFiles.Remove(fileNameWithPath);
                    _recentFiles.AddFirst(fileNameWithPath);
                }
            }
        }
        public void RemoveRecentFile(string fileNameWithPath)
        {
            if (_recentFiles == null) _recentFiles = new LinkedList<string>();
            if (_recentFiles.Count > 0) _recentFiles.Remove(fileNameWithPath);
        }

        public void ClearRecentFiles()
        {
            _recentFiles.Clear();
        }
        // Material library files
        public string[] GetMaterialLibraryFiles()
        {
            if (_materialLibraryFiles != null) return _materialLibraryFiles.ToArray();
            else return new string[0];
        }
        public void AddMaterialLibraryFile(string fileNameWithPath)
        {
            if (_materialLibraryFiles == null) _materialLibraryFiles = new List<string>();
            if (!_materialLibraryFiles.Contains(fileNameWithPath)) _materialLibraryFiles.Add(fileNameWithPath);
        }
        public void RemoveMaterialLibraryFile(string fileNameWithPath)
        {
            if (_materialLibraryFiles != null && _materialLibraryFiles.Contains(fileNameWithPath))
                _materialLibraryFiles.Remove(fileNameWithPath);
        }
        public void ClearMaterialLibraryFiles()
        {
            _materialLibraryFiles = null;
        }
        // Form size
        private void ResetFormSize()
        {
            _formWindowState = FormWindowState.Normal;
            _formSize = new Size(1280, 720);
            _formRelativeXLocation = 0.5;
            _formRelativeYLocation = 0.5;
        }
        public void SaveFormSize(Form form)
        {
            Size size;
            Point location;
            FormWindowState windowState = form.WindowState;
            //
            if (windowState == FormWindowState.Maximized || windowState == FormWindowState.Minimized)
            {
                size = form.RestoreBounds.Size;
                location = form.RestoreBounds.Location;
            }
            else
            {
                size = form.Size;
                location = form.Location;
            }
            //
            Rectangle workingArea = Screen.FromRectangle(new Rectangle(location, size)).WorkingArea;
            Point center = new Point(location.X + size.Width / 2, location.Y + size.Height / 2);
            // Set values
            _formWindowState = windowState == FormWindowState.Minimized ? FormWindowState.Normal : windowState;
            _formSize = size;
            _formRelativeXLocation = GetRelativeLocation(center.X, workingArea.Left, workingArea.Width);
            _formRelativeYLocation = GetRelativeLocation(center.Y, workingArea.Top, workingArea.Height);
        }
        public void ApplyFormSize(Form form)
        {
            Rectangle workingArea = Screen.FromControl(form).WorkingArea;
            //
            if (_formSize.Width <= form.MinimumSize.Width || _formSize.Height <= form.MinimumSize.Height)
                ResetFormSize(); // also resets _formRelativeXLocation and _formRelativeYLocation
            //
            Size formSize = GetSizeInsideWorkingArea(_formSize, form.MinimumSize, workingArea.Size);
            double relativeX = Clamp(_formRelativeXLocation, 0, 1);
            double relativeY = Clamp(_formRelativeYLocation, 0, 1);
            Point center = new Point(workingArea.Left + (int)(workingArea.Width * relativeX),
                                     workingArea.Top + (int)(workingArea.Height * relativeY));
            //
            Rectangle bounds = new Rectangle(new Point(center.X - formSize.Width / 2, center.Y - formSize.Height / 2), formSize);
            // Limit it to the working area
            if (bounds.Right > workingArea.Right) bounds.X = workingArea.Right - bounds.Width;
            if (bounds.Left < workingArea.Left) bounds.X = workingArea.Left;
            if (bounds.Bottom > workingArea.Bottom) bounds.Y = workingArea.Bottom - bounds.Height;
            if (bounds.Top < workingArea.Top) bounds.Y = workingArea.Top;
            // Set form size
            form.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);    // also sets the restore bounds
            form.WindowState = _formWindowState == FormWindowState.Maximized
                ? FormWindowState.Maximized
                : FormWindowState.Normal;
        }
        private static Size GetSizeInsideWorkingArea(Size formSize, Size minimumSize, Size workingArea)
        {
            int width = Math.Min(formSize.Width, workingArea.Width);
            int height = Math.Min(formSize.Height, workingArea.Height);
            //
            width = Math.Max(width, Math.Min(minimumSize.Width, workingArea.Width));
            height = Math.Max(height, Math.Min(minimumSize.Height, workingArea.Height));
            //
            return new Size(width, height);
        }
        private static double GetRelativeLocation(int value, int start, int length)
        {
            if (length <= 0) return 0.5;
            return Clamp((double)(value - start) / length, 0, 1);
        }
        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0.5;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
