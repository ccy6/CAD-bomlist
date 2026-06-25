using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;
using Xunit;

namespace BomCadPlugin.Core.Tests.Services;

public sealed class SystemParameterDisplayFormatterTests
{
    [Fact]
    public void FormatInputLabel_WithDescription_AppendsRemark()
    {
        var parameter = new SystemParameterDefinition
        {
            Key = "L",
            Name = "墙长",
            Unit = "m",
            Description = "围护墙体总长度"
        };

        var label = SystemParameterDisplayFormatter.FormatInputLabel(parameter);

        Assert.Equal("墙长 (L, m) - 围护墙体总长度", label);
    }

    [Fact]
    public void FormatFormulaHelp_WithParameters_ListsMeaningAndRemark()
    {
        var parameters = new[]
        {
            new SystemParameterDefinition
            {
                Key = "L",
                Name = "墙长",
                Unit = "m",
                Description = "围护墙体总长度"
            }
        };

        var help = SystemParameterDisplayFormatter.FormatFormulaHelp(parameters);
        Assert.Contains("A=raw reference if not a parameter, A_raw=raw reference, A_count=plane count, A_qty=rounded quantity", help);

        Assert.Contains("count=图块数量", help);
        Assert.Contains("L=墙长(m)，围护墙体总长度", help);
    }
}
