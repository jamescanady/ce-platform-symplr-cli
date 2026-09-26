using System.Text.Json;

namespace SymplrCli.Output;

public enum OutputFormat { Table, Json }

public static class Formatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Print<T>(T data, OutputFormat format)
    {
        if (format == OutputFormat.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(data, JsonOptions));
            return;
        }

        if (data is IEnumerable<string[]> rows)
            PrintTable([], rows);
    }

    public static void PrintTable(string[] headers, IEnumerable<string[]> rows)
    {
        var allRows = rows.ToList();
        if (allRows.Count == 0 && headers.Length == 0) { Console.WriteLine("(no results)"); return; }

        int colCount = headers.Length > 0 ? headers.Length : (allRows.FirstOrDefault()?.Length ?? 0);
        var widths = new int[colCount];

        for (int i = 0; i < colCount; i++)
        {
            widths[i] = headers.Length > i ? headers[i].Length : 0;
            widths[i] = Math.Max(widths[i], allRows.Max(r => i < r.Length ? r[i].Length : 0));
        }

        if (headers.Length > 0) PrintRow(headers, widths);
        Console.WriteLine(string.Join("  ", widths.Select(w => new string('-', w))));
        foreach (var row in allRows) PrintRow(row, widths);
    }

    private static void PrintRow(string[] cells, int[] widths)
    {
        var parts = widths.Select((w, i) =>
        {
            var cell = i < cells.Length ? cells[i] : "";
            return cell.PadRight(w);
        });
        Console.WriteLine(string.Join("  ", parts).TrimEnd());
    }

    public static void PrintJson<T>(T data) =>
        Console.WriteLine(JsonSerializer.Serialize(data, JsonOptions));

    public static void Error(string message)
    {
        var old = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"error: {message}");
        Console.ForegroundColor = old;
    }
}
