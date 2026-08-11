// PrePoMax - Copyright (C) 2016-2026 Matej BorovinÅ¡ek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej BorovinÅ¡ek
// Contributors:

using CaeGlobals;
using CaeModel;
using DynamicTypeDescriptor;
using System;
using System.ComponentModel;

namespace PrePoMax.Forms
{
    [Serializable]
    public class ViewFeCylinder
    {
        private FeCylinder _cylinder;


        // Properties
        [Category("Data")]
        [OrderedDisplayName(0, 10, "Name")]
        [Description("Name of the cylinder.")]
        [Id(1, 1)]
        public string Name { get { return _cylinder.Name; } set { _cylinder.Name = value; } }
        //
        [Category("Center")]
        [OrderedDisplayName(0, 10, "X")]
        [Description("X coordinate of the cylinder center.")]
        [TypeConverter(typeof(StringLengthConverter))]
        [Id(1, 2)]
        public double X { get { return _cylinder.Center[0]; } set { SetCenter(0, value); } }
        //
        [Category("Center")]
        [OrderedDisplayName(1, 10, "Y")]
        [Description("Y coordinate of the cylinder center.")]
        [TypeConverter(typeof(StringLengthConverter))]
        [Id(2, 2)]
        public double Y { get { return _cylinder.Center[1]; } set { SetCenter(1, value); } }
        //
        [Category("Center")]
        [OrderedDisplayName(2, 10, "Z")]
        [Description("Z coordinate of the cylinder center.")]
        [TypeConverter(typeof(StringLengthConverter))]
        [Id(3, 2)]
        public double Z { get { return _cylinder.Center[2]; } set { SetCenter(2, value); } }
        //
        [Category("Geometry")]
        [OrderedDisplayName(0, 10, "Radius")]
        [Description("Cylinder radius.")]
        [TypeConverter(typeof(StringLengthConverter))]
        [Id(1, 3)]
        public double Radius { get { return _cylinder.Radius; } set { _cylinder.Radius = value; } }
        //
        [Category("Geometry")]
        [OrderedDisplayName(1, 10, "Height")]
        [Description("Cylinder height measured along its axis.")]
        [TypeConverter(typeof(StringLengthConverter))]
        [Id(2, 3)]
        public double Height { get { return _cylinder.Height; } set { _cylinder.Height = value; } }
        //
        [Category("Direction")]
        [OrderedDisplayName(0, 10, "X")]
        [Description("X component of the cylinder axis direction.")]
        [TypeConverter(typeof(StringDoubleConverter))]
        [Id(1, 4)]
        public double Nx { get { return _cylinder.Direction[0]; } set { SetDirection(0, value); } }
        //
        [Category("Direction")]
        [OrderedDisplayName(1, 10, "Y")]
        [Description("Y component of the cylinder axis direction.")]
        [TypeConverter(typeof(StringDoubleConverter))]
        [Id(2, 4)]
        public double Ny { get { return _cylinder.Direction[1]; } set { SetDirection(1, value); } }
        //
        [Category("Direction")]
        [OrderedDisplayName(2, 10, "Z")]
        [Description("Z component of the cylinder axis direction.")]
        [TypeConverter(typeof(StringDoubleConverter))]
        [Id(3, 4)]
        public double Nz { get { return _cylinder.Direction[2]; } set { SetDirection(2, value); } }
        //
        [Category("Meshing")]
        [OrderedDisplayName(0, 10, "Extra radius")]
        [Description("Additional radius used by the cylindrical extrusion workflow.")]
        [TypeConverter(typeof(StringLengthConverter))]
        [Id(1, 5)]
        public double ExtraRadius { get { return _cylinder.ExtraRadius; } set { _cylinder.ExtraRadius = value; } }
        //
        [Category("Meshing")]
        [OrderedDisplayName(1, 10, "Chamfer start")]
        [Description("Chamfer value at the cylinder start.")]
        [TypeConverter(typeof(StringLengthConverter))]
        [Id(2, 5)]
        public double ChamferStart { get { return _cylinder.Chamfer[0]; } set { SetChamfer(0, value); } }
        //
        [Category("Meshing")]
        [OrderedDisplayName(2, 10, "Chamfer end")]
        [Description("Chamfer value at the cylinder end.")]
        [TypeConverter(typeof(StringLengthConverter))]
        [Id(3, 5)]
        public double ChamferEnd { get { return _cylinder.Chamfer[1]; } set { SetChamfer(1, value); } }
        //
        [Category("Meshing")]
        [OrderedDisplayName(3, 10, "Target radius")]
        [Description("Target radius used by the cylindrical extrusion workflow.")]
        [TypeConverter(typeof(StringLengthConverter))]
        [Id(4, 5)]
        public double TargetRadius { get { return _cylinder.TargetRadius; } set { _cylinder.TargetRadius = value; } }
        //
        [Category("Meshing")]
        [OrderedDisplayName(4, 10, "Layers")]
        [Description("Number of radial layers used by the cylindrical extrusion workflow.")]
        [TypeConverter(typeof(StringIntegerConverter))]
        [Id(5, 5)]
        public int NumberOfLayers { get { return _cylinder.NumberOfLayers; } set { _cylinder.NumberOfLayers = value; } }
        //
        [Category("Meshing")]
        [OrderedDisplayName(5, 10, "Tolerance")]
        [Description("Tolerance used by the cylindrical extrusion workflow.")]
        [TypeConverter(typeof(StringLengthConverter))]
        [Id(6, 5)]
        public double Tolerance { get { return _cylinder.Tolerance; } set { _cylinder.Tolerance = value; } }


        // Constructor
        public ViewFeCylinder(FeCylinder cylinder)
        {
            _cylinder = cylinder;
            DynamicCustomTypeDescriptor = ProviderInstaller.Install(this);
            DynamicCustomTypeDescriptor.CategorySortOrder = CustomSortOrder.AscendingById;
            DynamicCustomTypeDescriptor.PropertySortOrder = CustomSortOrder.AscendingById;
        }


        // Methods
        public DynamicCustomTypeDescriptor DynamicCustomTypeDescriptor { get; private set; }
        public FeCylinder GetBase()
        {
            return _cylinder;
        }
        private void SetCenter(int direction, double value)
        {
            double[] center = _cylinder.Center;
            center[direction] = value;
            _cylinder.Center = center;
        }
        private void SetDirection(int direction, double value)
        {
            double[] axis = _cylinder.Direction;
            axis[direction] = value;
            _cylinder.Direction = axis;
        }
        private void SetChamfer(int direction, double value)
        {
            double[] chamfer = _cylinder.Chamfer;
            chamfer[direction] = value;
            _cylinder.Chamfer = chamfer;
        }
    }
}
