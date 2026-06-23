using BomCadPlugin.Services;
using Autodesk.Windows;

namespace BomCadPlugin.Ribbon;

internal static class BomRibbon
{
    public static void Create()
    {
        var ribbon = ComponentManager.Ribbon;
        if (ribbon == null) return;

        const string tabId = "BOM清单统计";
        if (ribbon.Tabs.Any(t => t.Id == tabId)) return;

        var tab = new RibbonTab
        {
            Id = tabId,
            Title = "BOM清单统计"
        };
        ribbon.Tabs.Add(tab);

        var panelSource = new RibbonPanelSource { Title = "BOM清单统计" };
        var panel = new RibbonPanel { Source = panelSource };
        tab.Panels.Add(panel);

        AddButton(panelSource, "项目参数", "BOM_PARAMS");
        AddButton(panelSource, "添加构件", "BOM_ADD_RULE");
        AddButton(panelSource, "构件管理", "BOM_RULES");
        AddButton(panelSource, "平面统计", "BOM_STAT");
        AddButton(panelSource, "清除统计", "BOM_CLEAR");
        AddButton(panelSource, "导出BOM", "BOM_EXPORT");
        AddButton(panelSource, "关于", "BOM_ABOUT");
    }

    private static void AddButton(RibbonPanelSource panelSource, string text, string command)
    {
        panelSource.Items.Add(new RibbonButton
        {
            Text = text,
            ShowText = true,
            CommandParameter = command + " ",
            CommandHandler = new RibbonCommandHandler(command)
        });
    }

    private sealed class RibbonCommandHandler : System.Windows.Input.ICommand
    {
        private readonly string _command;

        public RibbonCommandHandler(string command)
        {
            _command = command;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            if (CadDocumentService.TryGetActiveDocumentOrWarn(out var document))
            {
                document.SendStringToExecute(_command + " ", true, false, true);
            }
        }
    }
}
