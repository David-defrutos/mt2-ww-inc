using System;
using HarmonyLib;
namespace WwInc;

// Preserve the original exception; identify the W&W card for the next report.
[HarmonyPatch(typeof(CardTooltipContainer),"UpdateTooltipsUI")]
internal static class TooltipFailureDiagnostic
{
    static void Finalizer(CardState cardState,Exception __exception)
    {
        if(__exception!=null&&cardState!=null&&cardState.GetLinkedClassID()==AcademicTooltips.ClassId)
            Plugin.Log("W&W tooltip failed: card="+cardState.GetAssetName()+"; exception="+__exception.GetType().Name);
    }
}
// 2026-10-26-2340||codex-ww-inc||salidas/build/ww-inc/src/TooltipFailureDiagnostic.cs||Registrar ID de carta W&W si falla tooltip sin silenciar ni reemplazar excepción original
