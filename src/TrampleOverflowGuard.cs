using System;
using System.Collections;
using HarmonyLib;

namespace WwInc;

// Native traits suppress their own damage type, but allow each other's output.
// Limit the compatibility workaround to cards owned by this plugin, including preview.
internal static class TrampleOverflowGuard
{
    internal static bool Block(CardTraitState.ApplyingDamageParameters damage, Damage.Type secondaryType)
        => damage.damageType == secondaryType &&
           AcademicRuntime.Id(damage.damageSourceCard).StartsWith("david.wwinc-Card-", StringComparison.Ordinal);

    internal static IEnumerator Empty() { yield break; }
}

[HarmonyPatch(typeof(CardTraitTrample), nameof(CardTraitTrample.OnDamageWasApplied))]
internal static class TrampleFromOverflowHook
{
    internal static bool Prefix(CardTraitState.ApplyingDamageParameters damageParams, ref IEnumerator __result)
    {
        if (!TrampleOverflowGuard.Block(damageParams, Damage.Type.SpellOverflow)) return true;
        __result = TrampleOverflowGuard.Empty();
        return false;
    }
}

[HarmonyPatch(typeof(CardTraitDamageOverflow), nameof(CardTraitDamageOverflow.OnDamageWasApplied))]
internal static class OverflowFromTrampleHook
{
    internal static bool Prefix(CardTraitState.ApplyingDamageParameters damageParams, ref IEnumerator __result)
    {
        if (!TrampleOverflowGuard.Block(damageParams, Damage.Type.Trample)) return true;
        __result = TrampleOverflowGuard.Empty();
        return false;
    }
}
// 2026-10-17-1448||script-corregir-trample-explosive.py||repos/MT2/repos-propios/mt2-ww-inc/src/TrampleOverflowGuard.cs||Cortar reactivación cruzada Trample/SpellOverflow solo en cartas david.wwinc, también en preview
