// PrePoMax - Copyright (C) 2016-2026 Matej BorovinÅ¡ek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej BorovinÅ¡ek
// Contributors:

using CaeGlobals;
using System;
using System.Linq;
using System.Runtime.Serialization;

namespace CaeModel
{
    [Serializable]
    public class FeCylinder : NamedClass, ISerializable
    {
        // Variables
        private double[] _center;               // ISerializable
        private double _radius;                 // ISerializable
        private double[] _direction;            // ISerializable
        private double _height;                 // ISerializable
        private double _extraRadius;            // ISerializable
        private double[] _chamfer;              // ISerializable
        private double _targetRadius;           // ISerializable
        private int _numberOfLayers;            // ISerializable
        private double _tolerance;              // ISerializable


        // Properties
        public double[] Center
        {
            get { return _center; }
            set { _center = PrepareVector(value, 3, new double[] { 0, 0, 0 }); }
        }
        public double Radius
        {
            get { return _radius; }
            set { _radius = value; }
        }
        public double[] Direction
        {
            get { return _direction; }
            set { _direction = PrepareVector(value, 3, new double[] { 0, 1, 0 }); }
        }
        public double Height
        {
            get { return _height; }
            set { _height = value; }
        }
        public double ExtraRadius
        {
            get { return _extraRadius; }
            set { _extraRadius = value; }
        }
        public double[] Chamfer
        {
            get { return _chamfer; }
            set { _chamfer = PrepareVector(value, 2, new double[] { 0, 0 }); }
        }
        public double TargetRadius
        {
            get { return _targetRadius; }
            set { _targetRadius = value; }
        }
        public int NumberOfLayers
        {
            get { return _numberOfLayers; }
            set
            {
                _numberOfLayers = value;
                if (_numberOfLayers < 1) _numberOfLayers = 1;
            }
        }
        public double Tolerance
        {
            get { return _tolerance; }
            set { _tolerance = value; }
        }


        // Constructors
        public FeCylinder(string name)
            : base(name)
        {
            Reset();
        }
        public FeCylinder(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Reset();
            //
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "_center":
                        _center = (double[])entry.Value;
                        break;
                    case "_radius":
                        _radius = (double)entry.Value;
                        break;
                    case "_direction":
                        _direction = (double[])entry.Value;
                        break;
                    case "_height":
                        _height = (double)entry.Value;
                        break;
                    case "_extraRadius":
                        _extraRadius = (double)entry.Value;
                        break;
                    case "_chamfer":
                        _chamfer = (double[])entry.Value;
                        break;
                    case "_targetRadius":
                        _targetRadius = (double)entry.Value;
                        break;
                    case "_numberOfLayers":
                        _numberOfLayers = (int)entry.Value;
                        break;
                    case "_tolerance":
                        _tolerance = (double)entry.Value;
                        break;
                    default:
                        break;
                }
            }
            //
            _center = PrepareVector(_center, 3, new double[] { 0, 0, 0 });
            _direction = PrepareVector(_direction, 3, new double[] { 0, 1, 0 });
            _chamfer = PrepareVector(_chamfer, 2, new double[] { 0, 0 });
            if (_numberOfLayers < 1) _numberOfLayers = 1;
            if (_targetRadius == 0 && _radius > 0) _targetRadius = _radius;
            if (_tolerance == 0) _tolerance = 0.025;
        }


        // Methods
        public void Reset()
        {
            _center = new double[] { 0, 0, 0 };
            _radius = 1;
            _direction = new double[] { 0, 1, 0 };
            _height = 1;
            _extraRadius = 0;
            _chamfer = new double[] { 0, 0 };
            _targetRadius = _radius;
            _numberOfLayers = 1;
            _tolerance = 0.025;
        }
        public double[] GetNormalizedDirection()
        {
            double[] direction = PrepareVector(_direction, 3, new double[] { 0, 1, 0 });
            double length = Math.Sqrt(direction[0] * direction[0] +
                                      direction[1] * direction[1] +
                                      direction[2] * direction[2]);
            if (length > 0)
            {
                direction[0] /= length;
                direction[1] /= length;
                direction[2] /= length;
            }
            return direction;
        }
        public double[] GetTopCenter()
        {
            return GetPointAt(0.5 * _height);
        }
        public double[] GetBottomCenter()
        {
            return GetPointAt(-0.5 * _height);
        }
        public double[] GetHigherYEndCenter()
        {
            double[] firstEnd = GetBottomCenter();
            double[] secondEnd = GetTopCenter();
            if (secondEnd[1] > firstEnd[1]) return secondEnd;
            else return firstEnd;
        }
        public double[] GetPointAt(double axisOffsetFromCenter)
        {
            double[] direction = GetNormalizedDirection();
            return new double[]
            {
                _center[0] + axisOffsetFromCenter * direction[0],
                _center[1] + axisOffsetFromCenter * direction[1],
                _center[2] + axisOffsetFromCenter * direction[2]
            };
        }
        public bool IsProperlyDefined(out string error)
        {
            error = null;
            if (_center == null || _center.Length != 3)
                error = "The cylinder center must contain three coordinates.";
            else if (_direction == null || _direction.Length != 3)
                error = "The cylinder direction must contain three coordinates.";
            else if (_chamfer == null || _chamfer.Length != 2)
                error = "The cylinder chamfer must contain the start and end values.";
            else if (_radius <= 0)
                error = "The cylinder radius must be larger than 0.";
            else if (_height <= 0)
                error = "The cylinder height must be larger than 0.";
            else if (_targetRadius <= 0)
                error = "The cylinder target radius must be larger than 0.";
            else if (_numberOfLayers < 1)
                error = "The cylinder number of layers must be at least 1.";
            else if (_tolerance <= 0)
                error = "The cylinder tolerance must be larger than 0.";
            else if (_extraRadius < 0)
                error = "The cylinder extra radius cannot be negative.";
            else if (_chamfer[0] < 0 || _chamfer[1] < 0)
                error = "The cylinder chamfer values cannot be negative.";
            else if (_chamfer[0] + _chamfer[1] > _height)
                error = "The sum of both chamfers cannot exceed the cylinder height.";
            else
            {
                double directionLength = Math.Sqrt(_direction[0] * _direction[0] +
                                                   _direction[1] * _direction[1] +
                                                   _direction[2] * _direction[2]);
                if (directionLength == 0)
                    error = "The cylinder direction must not be the zero vector.";
            }
            return error == null;
        }
        public override string ToString()
        {
            return Name;
        }
        private static double[] PrepareVector(double[] values, int length, double[] defaultValue)
        {
            if (values == null || values.Length != length) return defaultValue.ToArray();
            else return values.ToArray();
        }
        // ISerialization
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            //
            info.AddValue("_center", _center, typeof(double[]));
            info.AddValue("_radius", _radius, typeof(double));
            info.AddValue("_direction", _direction, typeof(double[]));
            info.AddValue("_height", _height, typeof(double));
            info.AddValue("_extraRadius", _extraRadius, typeof(double));
            info.AddValue("_chamfer", _chamfer, typeof(double[]));
            info.AddValue("_targetRadius", _targetRadius, typeof(double));
            info.AddValue("_numberOfLayers", _numberOfLayers, typeof(int));
            info.AddValue("_tolerance", _tolerance, typeof(double));
        }
    }
}
