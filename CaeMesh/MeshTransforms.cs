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
using CaeGlobals;

namespace CaeMesh
{
    public static class MeshTransforms
    {
        public static void TranslatePoint(double[] pointCoor, double[] translateVector)
        {
            double[][] translated = new double[][] { pointCoor };
            TranslatePoints(translated, translateVector);
        }
        public static void TranslatePoints(double[][] pointCoor, double[] translateVector)
        {
            for (int i = 0; i < pointCoor.Length; i++)
            {
                pointCoor[i][0] += translateVector[0];
                pointCoor[i][1] += translateVector[1];
                pointCoor[i][2] += translateVector[2];
            }
        }
        public static void RotatePoint(double[] point, double[] rotateCenter, double[] rotateAxis, double rotateAngle)
        {
            double[][] rotated = new double[][] { point };
            RotatePoints(rotated, rotateCenter, rotateAxis, rotateAngle);
        }
        public static void RotatePoints(double[][] points, double[] rotateCenter, double[] rotateAxis, double rotateAngle)
        {
            // Matrix
            double[] x;
            double[][] m = RotationMatrix(rotateAxis, rotateAngle);
            for (int i = 0; i < points.Length; i++)
            {
                // Translate to origin
                points[i][0] -= rotateCenter[0];
                points[i][1] -= rotateCenter[1];
                points[i][2] -= rotateCenter[2];
                // Copy
                 x = points[i].ToArray();
                // Rotate
                points[i][0] = m[0][0] * x[0] + m[0][1] * x[1] + m[0][2] * x[2];
                points[i][1] = m[1][0] * x[0] + m[1][1] * x[1] + m[1][2] * x[2];
                points[i][2] = m[2][0] * x[0] + m[2][1] * x[1] + m[2][2] * x[2];
                // Translate to rotation center
                points[i][0] += rotateCenter[0];
                points[i][1] += rotateCenter[1];
                points[i][2] += rotateCenter[2];
            }
        }
        internal static double[][] RotationMatrix(double[] rotateAxis, double rotateAngle)
        {
            //https://stackoverflow.com/questions/6721544/circular-rotation-around-an-arbitrary-axis
            //https://en.wikipedia.org/wiki/Rotation_matrix#Rotation_matrix_from_axis_and_angle
            double[][] m = new double[3][];
            m[0] = new double[3];
            m[1] = new double[3];
            m[2] = new double[3];
            //
            Vec3D u = new Vec3D(rotateAxis);
            u.Normalize();
            //
            double cosPhi = Math.Cos(rotateAngle);
            double sinPhi = Math.Sin(rotateAngle);
            //
            m[0][0] = cosPhi + (Math.Pow(u.X, 2) * (1 - cosPhi));
            m[0][1] = u.X * u.Y * (1 - cosPhi) - u.Z * sinPhi;
            m[0][2] = u.X * u.Z * (1 - cosPhi) + u.Y * sinPhi;
            //
            m[1][0] = u.Y * u.X * (1 - cosPhi) + u.Z * sinPhi;
            m[1][1] = cosPhi + (Math.Pow(u.Y, 2) * (1 - cosPhi));
            m[1][2] = u.Y * u.Z * (1 - cosPhi) - u.X * sinPhi;
            //
            m[2][0] = u.Z * u.X * (1 - cosPhi) - u.Y * sinPhi;
            m[2][1] = u.Z * u.Y * (1 - cosPhi) + u.X * sinPhi;
            m[2][2] = cosPhi + (Math.Pow(u.Z, 2) * (1 - cosPhi));
            //
            return m;
        }
        public static void MirrorPoint(double[] point, double[] mirrorPoint, double[] mirrorDirection)
        {
            double[][] mirror = new double[][] { point };
            MirrorPoints(mirror, mirrorPoint, mirrorDirection);
            Array.Copy(mirror[0], point, 3);
        }
        public static void MirrorPoints(double[][] points, double[] mirrorPoint, double[] mirrorDirection)
        {
            double d;
            Vec3D x;
            Vec3D p = new Vec3D(mirrorPoint);
            Vec3D n = new Vec3D(mirrorDirection);
            n.Normalize();
            //
            for (int i = 0; i < points.Length; i++)
            {
                x = new Vec3D(points[i]);
                d = Vec3D.DotProduct(x - p, n);
                points[i] = new Vec3D(x - 2 * d * n).Coor;
            }
        }
        public static void ScalePoint(double[] point, double[] scaleCenter, double[] scaleFactors)
        {
            double[][] scaled = new double[][] { point };
            ScalePoints(scaled, scaleCenter, scaleFactors);
        }
        public static void ScalePoints(double[][] points, double[] scaleCenter, double[] scaleFactors)
        {
            for (int i = 0; i < points.Length; i++)
            {
                points[i][0] = scaleCenter[0] + (points[i][0] - scaleCenter[0]) * scaleFactors[0];
                points[i][1] = scaleCenter[1] + (points[i][1] - scaleCenter[1]) * scaleFactors[1];
                points[i][2] = scaleCenter[2] + (points[i][2] - scaleCenter[2]) * scaleFactors[2];
            }
        }
    }
}
