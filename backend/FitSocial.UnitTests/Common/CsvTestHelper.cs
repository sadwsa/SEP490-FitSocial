using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FitSocial.UnitTests.Common;

public static class CsvTestHelper
{
    public static string Get(string[] row, Dictionary<string, int> index, string name)
    {
        if (!index.TryGetValue(name, out var i)) return string.Empty;
        if (i >= row.Length) return string.Empty;
        return row[i];
    }

    public static bool ParseBool(string value)
    {
        return string.Equals(value?.Trim(), "TRUE", StringComparison.OrdinalIgnoreCase);
    }

    public static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        using var sr = new StringReader(text);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            rows.Add(SplitCsvLine(line));
        }
        return rows;
    }

    public static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }
}
