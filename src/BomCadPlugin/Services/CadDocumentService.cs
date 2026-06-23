using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using System.IO;

namespace BomCadPlugin.Services;

internal static class CadDocumentService
{
    public static Document? Document => CadApplication.DocumentManager.MdiActiveDocument;
    public static Editor Editor => GetEditorOrThrow();
    public static Database Database => GetDatabaseOrThrow();

    public static bool TryGetActiveDocument([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Document? document)
    {
        document = CadApplication.DocumentManager.MdiActiveDocument;
        return document is not null;
    }

    public static bool TryGetActiveDocumentOrWarn([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Document? document)
    {
        if (TryGetActiveDocument(out document))
        {
            return true;
        }

        MessageBox.Show("请先打开一个 DWG 文档，再使用 BOM清单统计。", "BOM清单统计", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    public static string? GetConfigPathOrWarn()
    {
        if (!TryGetActiveDocumentOrWarn(out var document))
        {
            return null;
        }

        var database = document.Database;
        var editor = document.Editor;
        var dwgPath = database.Filename;
        if (string.IsNullOrWhiteSpace(dwgPath))
        {
            editor.WriteMessage("\n请先保存当前 DWG 文件，再使用 BOM清单统计。");
            MessageBox.Show("请先保存当前 DWG 文件，再使用 BOM清单统计。", "BOM清单统计", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        return Path.ChangeExtension(dwgPath, ".bomconfig.json");
    }

    private static Editor GetEditorOrThrow()
    {
        if (!TryGetActiveDocument(out var document))
        {
            throw new InvalidOperationException("No active AutoCAD document is available.");
        }

        return document.Editor;
    }

    private static Database GetDatabaseOrThrow()
    {
        if (!TryGetActiveDocument(out var document))
        {
            throw new InvalidOperationException("No active AutoCAD document is available.");
        }

        return document.Database;
    }
}
