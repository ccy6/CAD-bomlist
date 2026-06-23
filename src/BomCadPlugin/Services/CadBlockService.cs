using Autodesk.AutoCAD.DatabaseServices;

namespace BomCadPlugin.Services;

internal static class CadBlockService
{
    public static string GetEffectiveBlockName(BlockReference blockReference, Transaction transaction)
    {
        if (blockReference.IsDynamicBlock)
        {
            var dynamicRecord = (BlockTableRecord)transaction.GetObject(blockReference.DynamicBlockTableRecord, OpenMode.ForRead);
            return dynamicRecord.Name;
        }

        var record = (BlockTableRecord)transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead);
        return record.Name;
    }
}
