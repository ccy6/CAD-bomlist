using System.Text;
using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;
using Xunit;

namespace BomCadPlugin.Core.Tests.Services;

public sealed class ExportServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void ExportCsv_EscapesCsvFieldsAndWritesUtf8Bom()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "bom.csv");
        var result = new BomStatResult
        {
            Items =
            [
                new BomStatItem
                {
                    SystemName = "体系,一",
                    ComponentName = "构件\"A\"",
                    BlockName = "BLK_A",
                    Unit = "个",
                    PlaneCount = 2,
                    CalculationFactor = 3.5m,
                    TotalQty = 7,
                    Note = "第一行\n第二行"
                }
            ]
        };

        new ExportService().ExportCsv(path, result);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3).ToArray());

        var csv = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains("\"体系,一\"", csv);
        Assert.Contains("\"构件\"\"A\"\"\"", csv);
        Assert.Contains("\"第一行\n第二行\"", csv);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
