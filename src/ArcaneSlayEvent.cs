using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;

namespace WwInc;

// Capture attribution when enqueuing; execute it in the corresponding trigger scope.
internal static class ArcaneSlayEvent
{
    internal sealed class Data : CharacterState.FireTriggersData
    {
        internal CardState Source=null!;
    }
    sealed class Context
    {
        internal CharacterState Unit=null!;
        internal CardState Source=null!;
    }
    static readonly Stack<CardState> enqueuing=new();
    static readonly Stack<Context> executing=new();
    internal static CardState? EnqueuingSource=>enqueuing.Count>0?enqueuing.Peek():null;
    internal static IEnumerator Dispatch(CardState source,int floor,int kills)
    {
        enqueuing.Push(source);
        try
        {
            yield return AcademicRuntime.Core.GetCombatManager().ApplyCharacterEffectsForRoom(
                AcademicRuntime.ArcaneSlay,floor,triggerCount:kills);
        }
        finally { enqueuing.Pop(); }
    }
    internal static IEnumerator Run(IEnumerator original,CharacterState unit,Data data)
    {
        executing.Push(new Context {Unit=unit,Source=data.Source});
        try { yield return original; }
        finally { executing.Pop(); }
    }
    internal static CardState? Source(CardEffectParams p)
        =>executing.Count>0&&executing.Peek().Unit==p.selfTarget&&
            p.sourceCharacterTriggerState?.GetTrigger()==AcademicRuntime.ArcaneSlay
            ?executing.Peek().Source:null;
}

[HarmonyPatch(typeof(CombatManager),nameof(CombatManager.QueueTrigger),new Type[] {
    typeof(CharacterState),typeof(CharacterTriggerData.Trigger),typeof(CharacterState),
    typeof(bool),typeof(bool),typeof(CharacterState.FireTriggersData),typeof(int),typeof(CharacterTriggerState) })]
internal static class ArcaneSlayQueueHook
{
    static void Prefix(CharacterTriggerData.Trigger trigger,ref CharacterState.FireTriggersData fireTriggersData)
    {
        if(trigger==AcademicRuntime.ArcaneSlay&&fireTriggersData==null&&ArcaneSlayEvent.EnqueuingSource is CardState source)
            fireTriggersData=new ArcaneSlayEvent.Data {Source=source};
    }
}
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.FireTriggers))]
internal static class ArcaneSlayTriggerHook
{
    static void Postfix(CharacterState __instance,CharacterTriggerData.Trigger trigger,CharacterState.FireTriggersData fireTriggersData,ref IEnumerator __result)
    {
        if(trigger==AcademicRuntime.ArcaneSlay&&fireTriggersData is ArcaneSlayEvent.Data data)
            __result=ArcaneSlayEvent.Run(__result,__instance,data);
    }
}

public sealed class CardEffectAcademicUpgradeSlayingSpell : AcademicEffectBase
{
    public override bool CanPlayAfterBossDead=>true;
    // FireTriggers tests effects without sourceCharacterTriggerState. Attribution is checked at execution.
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode||AcademicRuntime.Simulating)yield break;
        var card=ArcaneSlayEvent.Source(p);
        if(card==null||card.GetCardType()!=CardType.Spell||card.IsAnyAbility())yield break;
        CardUpgradeHelper.TryApplyUpgradeToCard(card,null,s.GetParamCardUpgradeData(),
            false,p.isFromHiddenTrigger,m,out _);
        card.UpdateCardBodyText();m.GetCardManager().RefreshCardInHand(card);
    }
}
// 2026-10-13-2138||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ArcaneSlayEvent.cs||Contexto de hechizo responsable con cola nativa y mejora permanente mediante CardUpgradeHelper
// 2026-10-13-2144||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ArcaneSlayEvent.cs||Corregir TestEffect sin contexto de trigger; atribución via FireTriggersData en cola y scope de ejecución
