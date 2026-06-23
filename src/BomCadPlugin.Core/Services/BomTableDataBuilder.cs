using BomCadPlugin.Core.Models;

namespace BomCadPlugin.Core.Services;

public static class BomTableDataBuilder
{
    private static readonly string[] HeaderTexts =
    [
        "序号",
        "构件名称",
        "图块名",
        "单位",
        "平面数量",
        "计算系数",
        "总数量",
        "备注"
    ];

    public static BomTableData Build(BomStatResult result, ProjectParams project)
    {
        var projectName = string.IsNullOrWhiteSpace(project.ProjectName) ? "未命名项目" : project.ProjectName.Trim();
        var rows = result.Items
            .Select((item, index) => (IReadOnlyList<string>)
            [
                (index + 1).ToString(),
                item.ComponentName,
                item.BlockName,
                item.Unit,
                item.PlaneCount.ToString(),
                FormatDecimal(item.CalculationFactor),
                FormatDecimal(item.TotalQty),
                BuildNote(item)
            ])
            .ToList();

        return new BomTableData
        {
            Title = "BOM 平面统计表",
            Subtitle = $"项目：{projectName}    统计时间：{result.StatTime:yyyy-MM-dd HH:mm}",
            Headers = HeaderTexts,
            Rows = rows
        };
    }

    private static string FormatDecimal(decimal value) => value.ToString("0.####");

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
