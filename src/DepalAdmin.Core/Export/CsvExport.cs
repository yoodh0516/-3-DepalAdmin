using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace DepalAdmin.Core.Export;

/// <summary>
/// 표를 CSV로 저장 (엑셀에서 한글이 안 깨지게 UTF-8 BOM).
/// 사용 예:
///   CsvExport.Save("결과.csv", rows,
///       ("시각", r => r.Time),
///       ("등급", r => r.GradeText));
/// </summary>
public static class CsvExport
{
    /// <summary>저장한 파일 경로. 사용자가 취소하면 null.</summary>
    public static string? Save<T>(string fileName, IEnumerable<T> rows, params (string Header, Func<T, object?> Get)[] columns)
    {
        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = "CSV 파일 (*.csv)|*.csv",
            DefaultExt = ".csv",
        };
        if (dialog.ShowDialog() != true) return null;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(c => Escape(c.Header))));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", columns.Select(c => Escape(Format(c.Get(row))))));

        File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return dialog.FileName;
    }

    private static string Format(object? value) => value switch
    {
        null => "",
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        double x => x.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static string Escape(string text) =>
        text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
}
