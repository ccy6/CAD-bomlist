using System.Text;
using BomCadPlugin.Core.Models;

namespace BomCadPlugin.Core.Services;

public sealed class ExportService
{
    public void ExportCsv(string path, BomStatResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("序号,体系,构件名称,图块名,单位,平面数量,计算系数,总数量,备注");

        for (var i = 0; i < result.Items.Count; i++)
        {
            var item = result.Items[i];
            sb.AppendLine(string.Join(",", new[]
            {
                (i + 1).ToString(),
                Escape(item.SystemName),
                Escape(item.ComponentName),
                Escape(item.BlockName),
                Escape(item.Unit),
                item.PlaneCount.ToString(),
                item.CalculationFactor.ToString("0.####"),
                item.TotalQty.ToString("0.####"),
                Escape(BuildNote(item))
            }));
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string BuildNote(BomStatItem item)
    {
        if (string.IsNullOrWhiteSpace(item.CalculationError))
        {
            return item.Note;
        }

        if (string.IsNullOrWhiteSpace(item.Note))
        {
            return item.CalculationError;
        }

        return $"{item.Note}；{item.CalculationError}";
    }
}
