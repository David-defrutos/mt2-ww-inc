using System.Collections;
using System.Collections.Generic;
using HarmonyLib;

namespace WwInc;

// Freeze is removed before native post-play relic tests. Only that timing bridge is custom.
public sealed class RelicEffectAcademicFrozenDraw : RelicEffectBase
{
    public override PropDescriptions CreateEditorInspectorDescriptions()=>PropDescriptions.DisplayNone;

    internal void OnFrozenCardPlayed(ICoreGameManagers core)
    {
        if(core.GetSaveManager().PreviewMode||core.GetCombatManager().IsRunningCardPlayPreview)return;
        var statistics=core.GetCardStatistics();
        if(!AreConditionsTrue(statistics))return;
        OnTrigger(statistics); // Native per-turn conditions own counting, reset and copying.
        core.GetCardManager().DrawCards(_srcRelicEffectData.GetParamInt());
        NotifyRelicTriggeredScreenCenter(core.GetRelicManager());
    }
}
// 2026-10-25-2140||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/FrozenEnrollmentScholarship.cs||Usar contador y condiciones nativos para primera carta Frozen; solo puente temporal propio

// Native after-play effects run after Freeze is removed. Preserve only that missing bit of history.
internal static class FrozenCardPlays
{
    internal static readonly Dictionary<CardState,int> Pending=new();
    internal static void Record(CardState card,bool accepted,bool wasFrozen)
    {
        if(accepted&&wasFrozen)Pending[card]=Pending.TryGetValue(card,out int count)?count+1:1;
    }
    internal static IEnumerator AfterNativeRelics(IEnumerator original,CardState card)
    {
        yield return original;
        if(!BattlePresentationContext.TryGet(out var core)||core.GetSaveManager().PreviewMode||
            core.GetCombatManager().IsRunningCardPlayPreview||!Pending.TryGetValue(card,out int count))yield break;
        if(count>1)Pending[card]=count-1;else Pending.Remove(card);
        foreach(var relic in core.GetRelicManager().GetRelicEffects(new List<RelicEffectAcademicFrozenDraw>()))
            relic.OnFrozenCardPlayed(core);
        yield return core.GetCombatManager().RunTriggerQueue();
    }
}
[HarmonyPatch(typeof(RelicManager),nameof(RelicManager.ApplyCardPlayedRelicEffects))]
internal static class FrozenAfterPlayHook
{
    static void Postfix(CardState cardState,ref IEnumerator __result)
        =>__result=FrozenCardPlays.AfterNativeRelics(__result,cardState);
}
// 2026-10-27-2204||codex-ww-inc||salidas/build/ww-inc/src/FrozenEnrollmentScholarship.cs||Resolver Scholarship al finalizar reliquias nativas post-jugada, conservando Freeze previo y cerrando eventos del robo
