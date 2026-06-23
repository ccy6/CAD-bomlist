using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;
using Xunit;

namespace BomCadPlugin.Core.Tests.Services;

public sealed class BomTableDataBuilderTests
{
    [Fact]
    public void Build_CreatesTitleHeadersAndRows()
    {
        var result = new BomStatResult
        {
            StatTime = new DateTime(2026, 6, 17, 9, 30, 0),
            Items =
            [
                new BomStatItem
                {
                    ComponentName = "Clamp",
                    BlockName = "LG-SF-65-Clamp",
                    Unit = "个",
                    PlaneCount = 12,
                    CalculationFactor = 5,
                    TotalQty = 60,
                    Note = "A"
                }
            ]
        };
        var project = new ProjectParams { ProjectName = "测试项目" };

        var table = BomTableDataBuilder.Build(result, project);

        Assert.Equal("BOM 平面统计表", table.Title);
        Assert.Equal("项目：测试项目    统计时间：2026-06-17 09:30", table.Subtitle);
        Assert.Equal(["序号", "构件名称", "图块名", "单位", "平面数量", "计算系数", "总数量", "备注"], table.Headers);
        Assert.Equal(["1", "Clamp", "LG-SF-65-Clamp", "个", "12", "5", "60", "A"], table.Rows.Single());
    }

    [Fact]
    public void Build_UsesFallbackProjectName()
    {
        var result = new BomStatResult();

        var table = BomTableDataBuilder.Build(result, new ProjectParams());

        Assert.StartsWith("项目：未命名项目", table.Subtitle);
    }
}
