using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;

namespace BomCadPlugin.UI;

internal sealed class ProjectParamsForm : Form
{
    private readonly IReadOnlyList<ProductSystem> _productSystems;
    private readonly TextBox _projectName = new() { Width = 220 };
    private readonly ComboBox _selectedSystem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly NumericUpDown _floorHeight = NumberBox(0, 1000, 3);
    private readonly FlowLayoutPanel _templateHeights = new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        MaximumSize = new Size(280, 0)
    };
    private readonly NumericUpDown _wallThickness = NumberBox(0, 100000, 200);
    private readonly TableLayoutPanel _customParameters = new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 2
    };
    private readonly Dictionary<string, NumericUpDown> _customParameterBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBox _note = new() { Multiline = true, Height = 70, Width = 220 };

    public ProjectParams ProjectParams { get; private set; }

    public ProjectParamsForm(ProjectParams projectParams)
        : this(projectParams, [])
    {
    }

    public ProjectParamsForm(ProjectParams projectParams, IEnumerable<ProductSystem> productSystems)
    {
        _productSystems = productSystems.ToList();
        ProjectParams = CloneProjectParams(projectParams);

        Text = "项目参数设置";
        Width = 500;
        Height = 560;
        MinimumSize = new Size(500, 460);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _projectName.Text = ProjectParams.ProjectName;
        _selectedSystem.Items.AddRange(_productSystems.Select(system => system.Name).Cast<object>().ToArray());
        if (_selectedSystem.Items.Count > 0)
        {
            var selected = ResolveInitialSystemName();
            _selectedSystem.SelectedItem = _selectedSystem.Items.Contains(selected) ? selected : _selectedSystem.Items[0];
        }

        _selectedSystem.SelectedIndexChanged += (_, _) => RebuildCustomParameterInputs();
        _floorHeight.Value = SafeValue(ProjectParams.FloorHeightM > 0 ? ProjectParams.FloorHeightM : 3, _floorHeight);
        foreach (var height in ProjectParams.TemplateHeightsM.Count > 0 ? ProjectParams.TemplateHeightsM : [3m])
        {
            AddTemplateHeightBox(height);
        }

        _wallThickness.Value = SafeValue(ProjectParams.WallThicknessMm, _wallThickness);
        _note.Text = ProjectParams.Note;

        var layout = FormLayout();
        layout.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 96);
        layout.Controls.Add(new Label { Text = "项目名称", AutoSize = true }, 0, 0);
        layout.Controls.Add(_projectName, 1, 0);
        layout.Controls.Add(new Label { Text = "产品体系", AutoSize = true }, 0, 1);
        layout.Controls.Add(_selectedSystem, 1, 1);
        layout.Controls.Add(new Label { Text = "体系参数", AutoSize = true }, 0, 2);
        layout.Controls.Add(_customParameters, 1, 2);
        layout.Controls.Add(new Label { Text = "层高 m", AutoSize = true }, 0, 3);
        layout.Controls.Add(_floorHeight, 1, 3);
        layout.Controls.Add(new Label { Text = "模板高度 m", AutoSize = true }, 0, 4);
        layout.Controls.Add(_templateHeights, 1, 4);
        layout.Controls.Add(new Label { Text = "墙厚 mm", AutoSize = true }, 0, 5);
        layout.Controls.Add(_wallThickness, 1, 5);
        layout.Controls.Add(new Label { Text = "备注", AutoSize = true }, 0, 6);
        layout.Controls.Add(_note, 1, 6);

        Controls.Add(FormShell(layout, Save));
        RebuildCustomParameterInputs();
    }

    private void Save()
    {
        var templateHeights = GetTemplateHeights();
        if (_floorHeight.Value <= 0 || templateHeights.Count == 0 || templateHeights.Any(h => h <= 0) || _wallThickness.Value <= 0)
        {
            MessageBox.Show("层高、模板高度、墙厚必须大于 0。", "参数不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ProjectParams = new ProjectParams
        {
            ProjectName = _projectName.Text.Trim(),
            SelectedSystemName = _selectedSystem.SelectedItem?.ToString() ?? "",
            FloorHeightM = _floorHeight.Value,
            TemplateHeightsM = templateHeights,
            WallThicknessMm = _wallThickness.Value,
            CustomParameters = _customParameterBoxes.ToDictionary(pair => NormalizeParameterKey(pair.Key), pair => pair.Value.Value),
            Note = _note.Text.Trim(),
            UpdatedAt = DateTime.Now
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    internal static NumericUpDown NumberBox(decimal min, decimal max, decimal value)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            DecimalPlaces = 2,
            Increment = 1,
            Value = value,
            Width = 220
        };
    }

    private void AddTemplateHeightBox(decimal value)
    {
        var box = NumberBox(0, 1000, SafeValue(value, NumberBox(0, 1000, 3)));
        box.Width = 72;

        var plus = new Button { Text = "+", Width = 28, Height = 24, UseVisualStyleBackColor = true };
        plus.Click += (_, _) => AddTemplateHeightBox(2);

        var group = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 8, 4),
            Tag = box
        };
        group.Controls.Add(box);
        group.Controls.Add(plus);

        if (_templateHeights.Controls.Count > 0)
        {
            var remove = new Button { Text = "-", Width = 28, Height = 24, UseVisualStyleBackColor = true };
            remove.Click += (_, _) => _templateHeights.Controls.Remove(group);
            group.Controls.Add(remove);
        }

        _templateHeights.Controls.Add(group);
        _templateHeights.PerformLayout();
    }

    private List<decimal> GetTemplateHeights()
    {
        return _templateHeights.Controls
            .OfType<FlowLayoutPanel>()
            .Select(panel => panel.Tag as NumericUpDown)
            .Where(box => box is not null)
            .Select(box => box!.Value)
            .ToList();
    }

    private void RebuildCustomParameterInputs()
    {
        _customParameters.Controls.Clear();
        _customParameters.RowStyles.Clear();
        _customParameterBoxes.Clear();
        _customParameters.ColumnStyles.Clear();
        _customParameters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        _customParameters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        var systemName = _selectedSystem.SelectedItem?.ToString();
        var parameters = _productSystems
            .FirstOrDefault(system => string.Equals(system.Name, systemName, StringComparison.OrdinalIgnoreCase))
            ?.Parameters ?? [];

        if (parameters.Count == 0)
        {
            _customParameters.Controls.Add(new Label { Text = "暂无自定义参数", AutoSize = true }, 0, 0);
            return;
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            var box = NumberBox(-100000000, 100000000, SafeCustomValue(parameter));
            box.DecimalPlaces = 4;
            box.Width = 120;
            _customParameterBoxes[NormalizeParameterKey(parameter.Key)] = box;

            _customParameters.Controls.Add(new Label
            {
                Text = SystemParameterDisplayFormatter.FormatInputLabel(parameter),
                AutoSize = true,
                MaximumSize = new Size(260, 0)
            }, 0, i);
            _customParameters.Controls.Add(box, 1, i);
        }
    }

    private string? ResolveInitialSystemName()
    {
        if (!string.IsNullOrWhiteSpace(ProjectParams.SelectedSystemName))
        {
            return ProjectParams.SelectedSystemName;
        }

        return _productSystems.FirstOrDefault(system => system.Parameters.Count > 0)?.Name
            ?? _selectedSystem.Items[0]?.ToString();
    }

    private decimal SafeCustomValue(SystemParameterDefinition parameter)
    {
        return TryGetCustomParameterValue(parameter.Key, out var value)
            ? value
            : parameter.DefaultValue;
    }

    private bool TryGetCustomParameterValue(string key, out decimal value)
    {
        var normalizedKey = NormalizeParameterKey(key);
        if (ProjectParams.CustomParameters.TryGetValue(normalizedKey, out value))
        {
            return true;
        }

        foreach (var parameter in ProjectParams.CustomParameters)
        {
            if (string.Equals(parameter.Key, normalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                value = parameter.Value;
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static Dictionary<string, decimal> NormalizeCustomParameters(Dictionary<string, decimal> parameters)
    {
        var normalized = new Dictionary<string, decimal>();
        foreach (var parameter in parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Key))
            {
                normalized[NormalizeParameterKey(parameter.Key)] = parameter.Value;
            }
        }

        return normalized;
    }

    private static string NormalizeParameterKey(string key) => key.Trim().ToLowerInvariant();

    private static List<decimal> ResolveTemplateHeights(ProjectParams projectParams)
    {
        if (projectParams.TemplateHeightsM.Count > 0)
        {
            return projectParams.TemplateHeightsM;
        }

        if (projectParams.TemplateHeightMm > 0)
        {
            return [projectParams.TemplateHeightMm / 1000];
        }

        return [3];
    }

    private static ProjectParams CloneProjectParams(ProjectParams projectParams)
    {
        return new ProjectParams
        {
            ProjectName = projectParams.ProjectName,
            SelectedSystemName = projectParams.SelectedSystemName,
            FloorHeightM = projectParams.FloorHeightM > 0 ? projectParams.FloorHeightM : projectParams.FloorHeightMm / 1000,
            TemplateHeightsM = ResolveTemplateHeights(projectParams),
            FloorHeightMm = projectParams.FloorHeightMm,
            TemplateHeightMm = projectParams.TemplateHeightMm,
            WallThicknessMm = projectParams.WallThicknessMm,
            CustomParameters = NormalizeCustomParameters(projectParams.CustomParameters),
            Note = projectParams.Note,
            UpdatedAt = projectParams.UpdatedAt
        };
    }

    internal static TableLayoutPanel FormLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(16),
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    internal static Control FormShell(Control content, Action onSave)
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(content);
        shell.Controls.Add(scroll, 0, 0);
        shell.Controls.Add(ButtonPanel(onSave), 0, 1);
        return shell;
    }

    internal static FlowLayoutPanel ButtonPanel(Action onSave)
    {
        var save = new Button { Text = "保存", Width = 88, Height = 36, DialogResult = DialogResult.None, UseVisualStyleBackColor = true };
        var cancel = new Button { Text = "取消", Width = 88, Height = 36, DialogResult = DialogResult.Cancel, UseVisualStyleBackColor = true };
        save.Click += (_, _) => onSave();

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 16, 16, 12),
            WrapContents = false,
            BackColor = SystemColors.Control
        };
        panel.Controls.Add(cancel);
        panel.Controls.Add(save);
        return panel;
    }

    private static decimal SafeValue(decimal value, NumericUpDown box)
    {
        if (value < box.Minimum)
        {
            return box.Minimum;
        }

        if (value > box.Maximum)
        {
            return box.Maximum;
        }

        return value;
    }
}
