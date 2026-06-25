using BomCadPlugin.Core.Models;

namespace BomCadPlugin.Core.Services;

public static class SystemParameterDisplayFormatter
{
    public static string FormatInputLabel(SystemParameterDefinition parameter)
    {
        var name = string.IsNullOrWhiteSpace(parameter.Name) ? parameter.Key.Trim() : parameter.Name.Trim();
        var key = parameter.Key.Trim();
        var unit = parameter.Unit?.Trim() ?? "";
        var identity = string.IsNullOrWhiteSpace(unit) ? key : $"{key}, {unit}";
        var label = $"{name} ({identity})";
        var description = parameter.Description?.Trim();

        return string.IsNullOrWhiteSpace(description) ? label : $"{label} - {description}";
    }

    public static string FormatFormulaHelp(IEnumerable<SystemParameterDefinition> parameters)
    {
        const string referenceCodeHelp = "A=raw reference if not a parameter, A_raw=raw reference, A_count=plane count, A_qty=rounded quantity";
        var lines = new List<string>
        {
            "count=图块数量",
            "h=模板总高度mm，n=模板段数，h1/h2...=每段模板高度mm"
        };

        lines.Add(referenceCodeHelp);

        foreach (var parameter in parameters.Where(parameter => !string.IsNullOrWhiteSpace(parameter.Key)))
        {
            lines.Add(FormatFormulaParameter(parameter));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatFormulaParameter(SystemParameterDefinition parameter)
    {
        var key = parameter.Key.Trim();
        var name = string.IsNullOrWhiteSpace(parameter.Name) ? key : parameter.Name.Trim();
        var unit = parameter.Unit?.Trim();
        var description = parameter.Description?.Trim();
        var label = string.IsNullOrWhiteSpace(unit) ? $"{key}={name}" : $"{key}={name}({unit})";

        return string.IsNullOrWhiteSpace(description) ? label : $"{label}，{description}";
    }
}
