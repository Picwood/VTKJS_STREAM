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
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
namespace CaeGlobals
{
 public static partial class Tools
 {
        private static readonly float _oneThird = 1f / 3f;
        private static readonly float _radToDeg = (float)(180f / Math.PI);
        private static readonly float _degToRad = (float)(Math.PI / 180f);
        public static double GetPhase360(double phase)
        {
            return (phase % 360 + 360) % 360;
        }
        public static float GetComplexRealAtAngleFromMagAndPha(float magnitude, float phaseDeg, float angleDeg)
        {
            return magnitude * (float)Math.Cos((phaseDeg + angleDeg) * _degToRad);
        }
        public static float Median(List<float> data)
        {
            if (data == null || data.Count == 0)
                throw new ArgumentException("Median: empty data.");
            //
            float[] sorted = data.ToArray();
            Array.Sort(sorted);
            //
            int n = sorted.Length;
            int mid = n / 2;
            //
            if (n % 2 == 0) return (float)(0.5 * (sorted[mid - 1] + sorted[mid]));
            else return sorted[mid];
        }
        public static float Average(List<float> data)
        {
            if (data == null || data.Count == 0)
                throw new ArgumentException("Average: empty data.");
            //
            float sum = 0;
            foreach (var item in data) sum += item;
            sum /= data.Count;
            //
            return sum;
        }
        public static string GetRandomString(int len)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[len];
            var random = new Random();
            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }
            return new String(stringChars);
        }
        public static void WriteBoolToFileStream(FileStream fileStream, bool value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (buffer.Length != 1) throw new NotSupportedException();
            fileStream.Write(buffer, 0, 1);
        }
        public static void WriteIntToFileStream(FileStream fileStream, int value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (buffer.Length != 4) throw new NotSupportedException();
            fileStream.Write(buffer, 0, 4);
        }
        public static void WriteStringToFileStream(FileStream fileStream, string value)
        {
            if (value == null) value = "@null#";
            byte[] byteData = Encoding.UTF8.GetBytes(value);
            int numOfBytes = Encoding.UTF8.GetBytes(value).Length;
            WriteIntToFileStream(fileStream, numOfBytes);
            fileStream.Write(byteData, 0, numOfBytes);
        }
        public static void WriteStringToFileStream(FileStream fileStream, string value, int numOfBytes)
        {
            if (value == null) value = "@null#";
            byte[] byteData = Encoding.UTF8.GetBytes(value);
            byte[] buffer = new byte[numOfBytes];
            byteData.CopyTo(buffer, 0); // buffer can be longer than byteData
            //
            fileStream.Write(buffer, 0, numOfBytes);
        }
        public static void ReadFileStreamExactly(FileStream fileStream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int count = fileStream.Read(buffer, offset, buffer.Length - offset);
                if (count == 0) throw new EndOfStreamException("Unexpected end of binary result data.");
                offset += count;
            }
        }
        public static bool ReadBoolFromFileStream(FileStream fileStream)
        {
            byte[] buffer = new byte[1];
            ReadFileStreamExactly(fileStream, buffer);
            return BitConverter.ToBoolean(buffer, 0);
        }
        public static int ReadIntFromFileStream(FileStream fileStream)
        {
            byte[] buffer = new byte[4];
            ReadFileStreamExactly(fileStream, buffer);
            return BitConverter.ToInt32(buffer, 0);
        }
        public static string ReadStringFromFileStream(FileStream fileStream)
        {
            int numOfBytes = ReadIntFromFileStream(fileStream);
            if (numOfBytes < 0 || numOfBytes > 1024 * 1024)
                throw new InvalidDataException("Invalid binary string length.");
            string value = ReadStringFromFileStream(fileStream, numOfBytes);
            if (value == "@null#") value = null;
            return value;
        }
        public static string ReadStringFromFileStream(FileStream fileStream, int numOfBytes)
        {
            byte[] buffer = new byte[numOfBytes];
            ReadFileStreamExactly(fileStream, buffer);
            string value = Encoding.UTF8.GetString(buffer);
            if (value == "@null#") value = null;
            return value;
        }
        public static byte[] Compress(Stream input, CompressionLevel compressionLevel)
        {
            using (var compressStream = new MemoryStream())
            using (var compressor = new DeflateStream(compressStream, compressionLevel))
            {
                input.CopyTo(compressor);
                compressor.Close();
                return compressStream.ToArray();
            }
        }
        public static Stream Decompress(Stream input)
        {
            return Decompress(input, long.MaxValue);
        }
        public static Stream Decompress(Stream input, long maximumOutputBytes)
        {
            if (maximumOutputBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
            var output = new MemoryStream();
            //
            using (var decompressor = new DeflateStream(input, CompressionMode.Decompress))
            {
                byte[] buffer = new byte[81920];
                int count;
                while ((count = decompressor.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (output.Length > maximumOutputBytes - count)
                        throw new InvalidDataException("Decompressed binary data exceeds its limit.");
                    output.Write(buffer, 0, count);
                }
            }
            //
            output.Position = 0;
            return output;
        }
        public static void Sort3_descending(ref float arr0, ref float arr1, ref float arr2)
        {
            float arr120;
            float arr121;

            // sort first two values
            if (arr0 > arr1)
            {
                arr120 = arr0;
                arr121 = arr1;
            } // if
            else
            {
                arr120 = arr1;
                arr121 = arr0;
            } // else

            // decide where to put arr12 and the third original value arr[ 3 ]
            if (arr121 > arr2)
            {
                arr0 = arr120;
                arr1 = arr121;
            } // if
            else if (arr2 > arr120)
            {
                arr0 = arr2;
                arr1 = arr120;
                arr2 = arr121;
            } // if
            else
            {
                arr0 = arr120;
                arr1 = arr2;
                arr2 = arr121;
            } // else
        }
        public static void Sort3_descending(ref double arr0, ref double arr1, ref double arr2)
        {
            double arr120;
            double arr121;

            // sort first two values
            if (arr0 > arr1)
            {
                arr120 = arr0;
                arr121 = arr1;
            } // if
            else
            {
                arr120 = arr1;
                arr121 = arr0;
            } // else

            // decide where to put arr12 and the third original value arr[ 3 ]
            if (arr121 > arr2)
            {
                arr0 = arr120;
                arr1 = arr121;
            } // if
            else if (arr2 > arr120)
            {
                arr0 = arr2;
                arr1 = arr120;
                arr2 = arr121;
            } // if
            else
            {
                arr0 = arr120;
                arr1 = arr2;
                arr2 = arr121;
            } // else
        }
        public static void SolveQubicEquationDepressedCubic(double a, double b, double c, double d,
                                                            ref double x1, ref double x2, ref double x3)
        {
            // https://en.wikipedia.org/wiki/Cubic_function
            // https://www.easycalculation.com/algebra/cubic-equation.php - check source
            double p;
            double q;
            double tmp1;
            double tmp2;
            double alpha;

            p = (3.0 * a * c - b * b) / (3.0 * a * a);
            if (p > 0)
            {
                // One root real, two are complex
                x1 = x2 = x3 = 0;
            }
            else if (p == 0)
            {
                // All roots real, at least two are equal.
                double term1 = b * _oneThird;
                double r = -(27f * d) + b * (9f * c - 2f * b * b);
                double r13 = (r < 0) ? -Math.Pow(-r, _oneThird) : Math.Pow(r, _oneThird);
                x1 = -term1 + 2f * r13;
                x2 = -(r13 + term1);
                x3 = x2;
            }
            else
            {
                q = (2.0 * b * b * b - 9.0 * a * b * c + 27.0 * a * a * d) / (27.0 * a * a * a);
                //
                tmp1 = (3.0 * q) / (2.0 * p) * Math.Sqrt(-3.0 / p);
                if (tmp1 > 1.0) tmp1 = 1.0;
                else if (tmp1 < -1.0) tmp1 = -1.0;
                alpha = 1.0 / 3.0 * Math.Acos(tmp1);
                //
                tmp1 = 2.0 * Math.Sqrt(-p / 3.0);
                tmp2 = b / (3.0 * a);
                x1 = tmp1 * Math.Cos(alpha) - tmp2;
                x2 = tmp1 * Math.Cos(alpha - 2.0 * Math.PI / 3.0) - tmp2;
                x3 = tmp1 * Math.Cos(alpha - 4.0 * Math.PI / 3.0) - tmp2;
            }
        }
        public static float GetComplexMagnitude(float real, float imaginary)
        {
            return (float)Math.Sqrt(real * real + imaginary * imaginary);
        }
        public static double GetComplexMagnitude(double real, double imaginary)
        {
            return Math.Sqrt(real * real + imaginary * imaginary);
        }
        public static float GetComplexPhaseDeg(float real, float imaginary)
        {
            float result;
            if (real == 0)
            {
                if (imaginary > 0) result = 90;
                else if ((imaginary < 0)) result = 270;
                else result = 0;
            }
            else
            {
                result = (float)Math.Atan(imaginary / real) * _radToDeg;
                if (real < 0) result += 180;
            }
            return result;
        }
        public static double GetComplexPhaseDeg(double real, double imaginary)
        {
            double result;
            if (real == 0)
            {
                if (imaginary > 0) result = 90;
                else if ((imaginary < 0)) result = 270;
                else result = 0;
            }
            else
            {
                result = Math.Atan(imaginary / real) * _radToDeg;
                if (real < 0) result += 180;
            }
            return result;
        }
        public static void GetPrincipalValuesFromMatrix(double s11, double s22, double s33,
                                                        double s12, double s23, double s31,
                                                        ref float x1f, ref float x2f, ref float x3f)
        {
            // https://en.wikipedia.org/wiki/Cubic_function
            // https://www.easycalculation.com/algebra/cubic-equation.php - check source
            double x1 = 0;
            double x2 = 0;
            double x3 = 0;
            //
            double I1 = s11 + s22 + s33;
            double I2 = s11 * s22 + s22 * s33 + s33 * s11 - s12 * s12 - s23 * s23 - s31 * s31;
            double I3 = s11 * s22 * s33 - s11 * s23 * s23 - s22 * s31 * s31 - s33 * s12 * s12 + 2.0 * s12 * s23 * s31;
            //
            SolveQubicEquationDepressedCubic(1, -I1, I2, -I3, ref x1, ref x2, ref x3);
            //
            x1f = (float)x1;
            x2f = (float)x2;
            x3f = (float)x3;
        }

 }
}
