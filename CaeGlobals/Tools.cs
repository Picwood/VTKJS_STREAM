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
using System.Reflection;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.ComponentModel;
using DynamicTypeDescriptor;
using System.Linq.Expressions;
using System.Diagnostics;
using System.IO.Compression;
using UnitsNet;

namespace CaeGlobals
{
    [Serializable]
    public static partial class Tools
    {
        private static readonly float _twoPiThirds = 2f * (float)Math.PI / 3f;
        private static readonly float _fourPiThirds = 4f * (float)Math.PI / 3f;
        //

        // Read clone from File
        public static T LoadDumpFromFile<T>(string fileName)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                FileStream fs = new FileStream(fileName, FileMode.Open);
                byte[] bytes = new byte[fs.Length];
                fs.Read(bytes, 0, (int)fs.Length);
                stream.Write(bytes, 0, (int)fs.Length);
                fs.Close();
                stream.Position = 0;
                BinaryFormatter formatter = SerializationTools.CreateBinaryFormatter();
                return (T)formatter.Deserialize(stream);
            }
        }
        public static T LoadDumpFromFile<T>(BinaryReader br)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                long length = br.ReadInt64();
                byte[] bytes = br.ReadBytes((int)length);
                stream.Write(bytes, 0, (int)length);
                stream.Position = 0;
                BinaryFormatter formatter = SerializationTools.CreateBinaryFormatter();
                return (T)formatter.Deserialize(stream);
            }
        }
        public static async Task<T> LoadDumpFromFileAsync<T>(string fileName)
        {
            T a = default;
            await Task.Run(() => a = LoadDumpFromFile<T>(fileName));
            return a;
        }
        // Write




        // Read




        //
        public static string GetLocalPath(string path)
        {
            string startUpPath = System.Windows.Forms.Application.StartupPath;
            if (path.StartsWith(startUpPath))
            {
                return "#" + path.Substring(startUpPath.Length);
            }
            return path;
        }
        public static string GetGlobalPath(string path)
        {
            if (path != null && path.StartsWith("#"))
            {
                string startUpPath = System.Windows.Forms.Application.StartupPath;
                return Path.Combine(startUpPath, path.Substring(1).TrimStart('\\'));
            }
            return path;
        }
        public static string GetNonExistentRandomFileName(string path, string extension = "")
        {
            string hash;
            bool repeat;
            string[] allFiles = Directory.GetFiles(path);
            //
            do
            {
                hash = GetRandomString(8);
                //
                repeat = false;
                foreach (var fileName in allFiles)
                {
                    if (fileName.StartsWith(hash))
                    {
                        repeat = true;
                        break;
                    }
                }
            }
            while (repeat);
            //
            return Path.Combine(path, Path.ChangeExtension(hash, extension));
        }
        // Compression


        // Windows version
        public static string GetWindowsName()
        {
            var reg = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string productName = (string)reg.GetValue("ProductName");
            return productName;
        }
        // Check if it's Windows 8.1
        public static bool IsWindows10orNewer()
        {
            try
            {
                string[] tmp = GetWindowsName().Split(new string[] {" "}, StringSplitOptions.RemoveEmptyEntries);
                double version;

                if (tmp.Length > 2 && double.TryParse(tmp[1], out version) && version >= 10) return true;
                else return false;
            }
            catch
            {
                return false;
            }
        }



        public static List<string> GetLockingProcesses(string filePath)
        {
            List<string> result = new List<string>();
            //
            if (!File.Exists(filePath)) return result;
            //
            if (CanOpenFile(filePath))
            {
                return result; // No process is locking the file
            }
            //
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == 30960)
                        result = result;

                    if (process.ProcessName.ToLower().StartsWith("ccx"))
                    {

                        List<string> openFiles = GetOpenFiles(process.Id).ToList();

                        foreach (var file in openFiles)
                        {
                            if (string.Equals(file, filePath, StringComparison.OrdinalIgnoreCase))
                            {
                                result.Add($"{process.ProcessName} (PID: {process.Id})");
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore access denied errors
                }
            }
            //
            return result;
        }
        private static bool CanOpenFile(string filePath)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return true;
                }
            }
            catch
            {
                return false; // File is locked
            }
        }
        private static IEnumerable<string> GetOpenFiles(int processId)
        {
            var files = new List<string>();
            //
            try
            {
                string query = $"SELECT ProcessId, Name FROM Win32_Process WHERE ProcessId = {processId}";
                using (var searcher = new System.Management.ManagementObjectSearcher("root\\CIMV2", query))
                {
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        files.Add(obj["Name"].ToString());
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            //
            return files;
        }

        // Read number of lines        
        [DebuggerStepThrough]
        public static bool IsNullOrEmptyOrWhiteSpace(this string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }
        [DebuggerStepThrough]
        public static T NotNull<T>(T value, string argName) where T : class
        {
            if (argName.IsNullOrEmptyOrWhiteSpace()) { argName = "Invalid"; }
            //
            if (value == null) throw new Exception(argName);
            return value;
        }
        [DebuggerStepThrough]
        public static long CountLines(this Stream stream, Encoding encoding = default)
        {
            const char CR = '\r';
            const char LF = '\n';
            const char NULL = (char)0;
            //
            NotNull(stream, nameof(stream));
            //
            var lineCount = 0L;
            var byteBuffer = new byte[1024 * 1024];
            var detectedEOL = NULL;
            var currentChar = NULL;
            int bytesRead;
            //
            if (encoding is null || Equals(encoding, Encoding.ASCII) || Equals(encoding, Encoding.UTF8))
            {
                while ((bytesRead = stream.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
                {
                    for (var i = 0; i < bytesRead; i++)
                    {
                        currentChar = (char)byteBuffer[i];
                        //
                        if (detectedEOL != NULL)
                        {
                            if (currentChar == detectedEOL)
                            {
                                lineCount++;
                            }
                        }
                        else if (currentChar == LF || currentChar == CR)
                        {
                            detectedEOL = currentChar;
                            lineCount++;
                        }
                    }
                }
            }
            else
            {
                var charBuffer = new char[byteBuffer.Length];
                //
                while ((bytesRead = stream.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
                {
                    var charCount = encoding.GetChars(byteBuffer, 0, bytesRead, charBuffer, 0);
                    //
                    for (var i = 0; i < charCount; i++)
                    {
                        currentChar = charBuffer[i];
                        //
                        if (detectedEOL != NULL)
                        {
                            if (currentChar == detectedEOL)
                            {
                                lineCount++;
                            }
                        }
                        else if (currentChar == LF || currentChar == CR)
                        {
                            detectedEOL = currentChar;
                            lineCount++;
                        }
                    }
                }
            }
            //
            if (currentChar != LF && currentChar != CR && currentChar != NULL)
            {
                lineCount++;
            }
            //
            return lineCount;
        }
        // String
        // Sort


        // Solve Qubic

        public static void SolveQubicEquationDepressedCubicF(float a, float b, float c, float d,
                                                             ref float x1, ref float x2, ref float x3)
        {
            // https://en.wikipedia.org/wiki/Cubic_function
            // https://www.easycalculation.com/algebra/cubic-equation.php - check source
            float p;
            float q;
            float tmp1;
            float tmp2;
            float alpha;
            //
            p = (3f * a * c - b * b) / (3f * a * a);
            if (p > 0)
            {
                // One root real, two are complex
                x1 = x2 = x3 = 0;
            }
            else if (p == 0)
            {
                // All roots real, at least two are equal.
                float term1 = b * _oneThird;
                float r = -(27f * d) + b * (9f * c - 2f * (b * b));
                float r13 = (r < 0) ? (float)(-Math.Pow(-r, _oneThird)) : (float)Math.Pow(r, _oneThird);
                x1 = -term1 + 2f * r13;
                x2 = -(r13 + term1);
                x3 = x2;
            }
            else
            {
                q = (2f * b * b * b - 9f * a * b * c + 27f * a * a * d) / (27f * a * a * a);
                //
                tmp1 = (3f * q) / (2f * p) * (float)Math.Sqrt(-3f / p);
                if (tmp1 > 1f) tmp1 = 1f;
                else if (tmp1 < -1f) tmp1 = -1f;
                alpha = _oneThird * (float)Math.Acos(tmp1);
                //
                tmp1 = 2f * (float)Math.Sqrt(-p / 3f);
                tmp2 = b / (3f * a);
                x1 = tmp1 * (float)Math.Cos(alpha) - tmp2;
                x2 = tmp1 * (float)Math.Cos(alpha - _twoPiThirds) - tmp2;
                x3 = tmp1 * (float)Math.Cos(alpha - _fourPiThirds) - tmp2;
            }
        }
        public static float Sin(float x) //x in radians
        {
            float sinn;
            if (x < -3.14159265f)
                x += 6.28318531f;
            else
            if (x > 3.14159265f)
                x -= 6.28318531f;

            if (x < 0)
            {
                sinn = 1.27323954f * x + 0.405284735f * x * x;

                if (sinn < 0)
                    sinn = 0.225f * (sinn * -sinn - sinn) + sinn;
                else
                    sinn = 0.225f * (sinn * sinn - sinn) + sinn;
                return sinn;
            }
            else
            {
                sinn = 1.27323954f * x - 0.405284735f * x * x;

                if (sinn < 0)
                    sinn = 0.225f * (sinn * -sinn - sinn) + sinn;
                else
                    sinn = 0.225f * (sinn * sinn - sinn) + sinn;
                return sinn;

            }
        }
        public static float Cos(float x) //x in radians
        {
            return Sin(x + 1.5707963f);
        }
        // Complex





        public static float GetComplexRealAtAngle(float real, float imaginary, float angleDeg)
        {
            float magnitude = GetComplexMagnitude(real, imaginary);
            float phaseDeg = GetComplexPhaseDeg(real, imaginary);
            return magnitude * (float)Math.Cos((phaseDeg + angleDeg) * _degToRad);
        }
        public static double GetComplexRealAtAngle(double real, double imaginary, double angleDeg)
        {
            double magnitude = GetComplexMagnitude(real, imaginary);
            double phaseDeg = GetComplexPhaseDeg(real, imaginary);
            return magnitude * Math.Cos((phaseDeg + angleDeg) * _degToRad);
        }

        //
        public static int[] GetSortedKey(int id1, int id2)
        {
            if (id1 < id2) return new int[] { id1, id2 };
            else return new int[] { id2, id1 };
        }
        public static (int, int) GetSortedValueKey(int id1, int id2)
        {
            if (id1 < id2) return (id1, id2);
            else return (id2, id1);
        }
        public static int GetHashCode(int[] array)
        {
            int hash = 23;
            for (int i = 0; i < array.Length; i++)
            {
                hash = hash * 31 + array[i].GetHashCode();
            }
            return hash;
        }
        public static int GetHashCode(int[][] array)
        {
            int hash = 23;
            for (int i = 0; i < array.Length; i++)
            {
                hash = hash * 31 + GetHashCode(array[i]);
            }
            return hash;
        }
        // Statistics


    }
}
