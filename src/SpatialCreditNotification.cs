using System;
using HarmonyLib;
using ShinyShoe;
namespace WwInc;
internal static class SpatialCreditNotification
{
    internal static bool Matches(string? asset,int size)
        =>size<0&&asset!=null&&asset.StartsWith("david.wwinc-",StringComparison.Ordinal)&&
          asset.EndsWith("-WW_S23_Upgrade",StringComparison.Ordinal);
}
[HarmonyPatch(typeof(CardEffectAddCardUpgradeToUnits),"GetSizeNotificationText")]
internal static class SpatialCreditNotificationHook
{
    static void Postfix(CardUpgradeState upgradeState,ref string __result)
    {
        int size=upgradeState.GetAdditionalSize();
        if(SpatialCreditNotification.Matches(upgradeState.GetAssetName(),size))
            __result=string.Format("SizeNotificationText".Localize(),size);
    }
}
// 2026-10-25-1849||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpatialCreditNotification.cs||Corregir +−1 a −1 solo para la reducción de tamaño de Spatial Credit, conservando el aviso localizado
