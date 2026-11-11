// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
namespace CaeGlobals
{
    public static partial class Tools
    {
        // File locked
        public static bool IsFileLocked(string fileName)
        {
            FileInfo fileInfo = new FileInfo(fileName);
            FileStream stream = null;

            try
            {
                stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                // The file is unavailable because it is still being written to
                // or being processed by another thread or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null) stream.Close();
            }
            // File is not locked
            return false;
        }
        public static bool WaitForFileToUnlock(string fileName, long miliseconds)
        {
            DateTime start = DateTime.Now;
            while (IsFileLocked(fileName))
            {
                if ((DateTime.Now - start).TotalMilliseconds >= miliseconds) return false;
                System.Threading.Thread.Sleep(100);
            }
            return true;
        }
        public static string[] ReadAllLines(string fileName, bool trimStart = false)
        {
            List<string> lines = new List<string>(100000);
            //
            if (!WaitForFileToUnlock(fileName, 5000)) return null;
            //
            using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8, true, 16*4096))
            {
                fileStream.Position = 0;
                while (!streamReader.EndOfStream)
                {
                    if (trimStart) lines.Add(streamReader.ReadLine().TrimStart());
                    else lines.Add(streamReader.ReadLine());
                }
                //
                streamReader.Close();
                fileStream.Close();
            }
            //
            return lines.ToArray();
        }
    }
}
