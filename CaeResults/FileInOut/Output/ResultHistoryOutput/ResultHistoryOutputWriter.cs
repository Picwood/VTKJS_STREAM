// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CaeResults
{
    public static class ResultHistoryOutputWriter
    {
        public static void Write(string fileName, string[] historyOutputNames, string delimiter, FeResults results)
        {
            HistoryResultSet[] historyResultSets = new HistoryResultSet[historyOutputNames.Length];
            for (int i = 0; i < historyOutputNames.Length; i++)
            {
                historyResultSets[i] = results.GetHistoryResultSet(historyOutputNames[i]);
            }
            //
            StringBuilder sb = new StringBuilder();
            HistoryResultData historyData;
            string[] columnNames;
            object[][] rowBasedData;
            //
            foreach (HistoryResultSet historyResultSet in historyResultSets)
            {
                foreach (var fieldEntry in historyResultSet.Fields)
                {
                    foreach (var componentEntry in fieldEntry.Value.Components)
                    {
                        historyData = new HistoryResultData(historyResultSet.Name, fieldEntry.Key, componentEntry.Key);
                        results.GetHistoryOutputData(historyData, out columnNames, out rowBasedData, true);
                        // Title
                        sb.AppendLine("History output component" + delimiter +
                                      FormatCell(historyResultSet.Name + "." + fieldEntry.Key + "." + componentEntry.Key, delimiter));
                        // Column names
                        for (int i = 0; i < columnNames.Length; i++)
                        {
                            if (i == 0) sb.Append(FormatCell(columnNames[i], delimiter));
                            else
                            {
                                sb.Append(delimiter);
                                sb.Append(FormatCell(columnNames[i], delimiter));
                            }
                        }
                        sb.AppendLine();
                        // Data
                        for (int i = 0; i < rowBasedData.Length; i++)
                        {
                            for (int j = 0; j < rowBasedData[i].Length; j++)
                            {
                                if (j != 0) sb.Append(delimiter);
                                sb.Append(FormatCell(rowBasedData[i][j], delimiter));
                            }
                            sb.AppendLine();
                        }
                        sb.AppendLine("End component");
                    }
                }
            }
            //
            File.WriteAllText(fileName, sb.ToString());
        }
        private static string FormatCell(object value, string delimiter)
        {
            string text = value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value?.ToString() ?? "";
            if (text.Contains(delimiter) || text.Contains("\"") || text.Contains("\r") || text.Contains("\n"))
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            return text;
        }
    }
}
