using System;
using HarmonyLib;
namespace WwInc;

// Diagnosis only: never invent or substitute a saved clan ID.
internal static class RunLoadDiagnostic
{
    internal static bool DataReady;
    private static string last="";
    internal static string RawClassId(SaveManager save,string method)
    {
        var data=AccessTools.Property(typeof(SaveManager),"ActiveSaveData")?.GetValue(save);
        return data==null?"<no active save>":AccessTools.Method(data.GetType(),method)?.Invoke(data,null)?.ToString()??"<unavailable>";
    }
    internal static void Check(ClassData main,ClassData sub,SaveManager save)
    {
        if(main!=null||save==null)return;
        string mainId=RawClassId(save,"GetMainClassID"),subId=RawClassId(save,"GetSubClassID");
        string message=$"W&W LOAD DIAGNOSTIC: unresolved main clan; mainId={mainId}; subId={subId}; subResolved={sub!=null}; wwDataReady={DataReady}; wwClassId={AcademicTooltips.ClassId}; version={typeof(Plugin).Assembly.GetName().Version}. Saved IDs were not changed.";
        if(message==last)return;
        last=message;Plugin.Log(message);
    }
}
[HarmonyPatch(typeof(CardPoolHelper),nameof(CardPoolHelper.GetCardsToLoadForClass))]
internal static class MissingSavedClanDiagnostic
{
    static void Prefix(ClassData mainClass,ClassData subClass,SaveManager saveManager)
        =>RunLoadDiagnostic.Check(mainClass,subClass,saveManager);
}
// 2026-10-27-1535||codex-ww-inc||salidas/build/ww-inc/src/RunLoadDiagnostic.cs||Diagnosticar IDs originales de clase no resuelta y momento de registro sin alterar guardado ni ocultar assertion
// 2026-10-27-1545||codex-ww-inc||salidas/build/ww-inc/src/RunLoadDiagnostic.cs||Leer versión real del ensamblado en diagnóstico sin mantener otro literal
