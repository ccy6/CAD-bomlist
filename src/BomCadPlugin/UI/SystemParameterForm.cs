using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;

namespace BomCadPlugin.UI;

internal sealed class SystemParameterForm : Form
{
    private readonly IReadOnlyList<string> _existingKeys;
    private readonly string? _originalKey;
    private readonly TextBox _key = new() { Width = 220 };
    private readonly TextBox _name = new() { Width = 220 };
    private readonly TextBox _unit = new() { Width = 220 };
    private readonly NumericUpDown _defaultValue = ProjectParamsForm.NumberBox(-100000000, 100000000, 0);
    private readonly TextBox _description = new() { Width = 280, Height = 80, Multiline = true };
    private readonly Label _hint = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick,
        MaximumSize = new Size(340, 0)
    };

    public SystemParameterForm(string systemName, IEnumerable<string> existingKeys)
        : this(systemName, existingKeys, null)
    {
    }

    public SystemParameterForm(string systemName, IEnumerable<string> existingKeys, SystemParameterDefinition? initialParameter)
    {
        _existingKeys = existingKeys.ToList();
        _originalKey = initialParameter?.Key;
        Text = initialParameter is null ? $"添加参数 - {systemName}" : $"编辑参数 - {systemName}";
        Width = 460;
        Height = 360;
        MinimumSize = new Size(430, 340);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _defaultValue.DecimalPlaces = 4;
        _defaultValue.Width = 220;
        if (initialParameter is not null)
        {
            _key.Text = initialParameter.Key;
            _name.Text = initialParameter.Name;
            _unit.Text = initialParameter.Unit;
            _defaultValue.Value = ClampDefaultValue(initialParameter.DefaultValue);
            _description.Text = initialParameter.Description;
        }

        var layout = ProjectParamsForm.FormLayout();
        layout.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 96);
        layout.Controls.Add(new Label { Text = "参数代号", AutoSize = true }, 0, 0);
        layout.Controls.Add(_key, 1, 0);
        layout.Controls.Add(new Label { Text = "参数名称", AutoSize = true }, 0, 1);
        layout.Controls.Add(_name, 1, 1);
        layout.Controls.Add(new Label { Text = "单位", AutoSize = true }, 0, 2);
        layout.Controls.Add(_unit, 1, 2);
        layout.Controls.Add(new Label { Text = "默认值", AutoSize = true }, 0, 3);
        layout.Controls.Add(_defaultValue, 1, 3);
        layout.Controls.Add(new Label { Text = "参数备注", AutoSize = true }, 0, 4);
        layout.Controls.Add(_description, 1, 4);
        layout.Controls.Add(_hint, 1, 5);

        Controls.Add(ProjectParamsForm.FormShell(layout, Save));
        AcceptButton = FindButton("保存");
        CancelButton = FindButton("取消");
        _key.Focus();
    }

    public SystemParameterDefinition Parameter { get; private set; } = new();

    private void Save()
    {
        var key = _key.Text.Trim().ToLowerInvariant();
        var name = _name.Text.Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name))
        {
            _hint.Text = "参数代号和参数名称不能为空。";
            return;
        }

        if (!string.Equals(_originalKey, key, StringComparison.OrdinalIgnoreCase) &&
            SystemParameterKeySuggestionService.IsKeyInUse(_existingKeys, key))
        {
            var suggestion = SystemParameterKeySuggestionService.SuggestAvailableKey(_existingKeys, key);
            _hint.Text = $"参数代号“{key}”已存在，建议改成“{suggestion}”。";
            _key.Text = suggestion;
            _key.SelectAll();
            _key.Focus();
            return;
        }

        Parameter = new SystemParameterDefinition
        {
            Key = key,
            Name = name,
            Unit = _unit.Text.Trim(),
            DefaultValue = _defaultValue.Value,
            Description = _description.Text.Trim()
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private Button? FindButton(string text)
    {
        return Controls
            .OfType<Control>()
            .SelectMany(FlattenControls)
            .OfType<Button>()
            .FirstOrDefault(button => button.Text == text);
    }

    private decimal ClampDefaultValue(decimal value)
    {
        if (value < _defaultValue.Minimum)
        {
            return _defaultValue.Minimum;
        }

        if (value > _defaultValue.Maximum)
        {
            return _defaultValue.Maximum;
        }

        return value;
    }

    private static IEnumerable<Control> FlattenControls(Control control)
    {
        yield return control;

        foreach (Control child in control.Controls)
        {
            foreach (var descendant in FlattenControls(child))
            {
                yield return descendant;
            }
        }
    }
}
