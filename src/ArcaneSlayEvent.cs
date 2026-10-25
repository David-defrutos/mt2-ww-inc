using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;

namespace MonsterTrain2.SharedTriggers;

// Capture attribution when enqueuing; execute it in the corresponding trigger scope.
public static class ArcaneSlayEvent
{
    public static CharacterTriggerData.Trigger Trigger;
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
    public static IEnumerator Dispatch(ICoreGameManagers core,CardState source,int floor,int kills)
    {
        enqueuing.Push(source);
        try
        {
            yield return core.GetCombatManager().ApplyCharacterEffectsForRoom(
                ArcaneSlayEvent.Trigger,floor,triggerCount:kills);
        }
        finally { enqueuing.Pop(); }
    }
    internal static IEnumerator Run(IEnumerator original,CharacterState unit,Data data)
    {
        executing.Push(new Context {Unit=unit,Source=data.Source});
        try { yield return original; }
        finally { executing.Pop(); }
    }
    public static CardState? Source(CardEffectParams p)
        =>executing.Count>0&&executing.Peek().Unit==p.selfTarget&&
            p.sourceCharacterTriggerState?.GetTrigger()==ArcaneSlayEvent.Trigger
            ?executing.Peek().Source:null;
}

[HarmonyPatch(typeof(CombatManager),nameof(CombatManager.QueueTrigger),new Type[] {
    typeof(CharacterState),typeof(CharacterTriggerData.Trigger),typeof(CharacterState),
    typeof(bool),typeof(bool),typeof(CharacterState.FireTriggersData),typeof(int),typeof(CharacterTriggerState) })]
internal static class ArcaneSlayQueueHook
{
    static void Prefix(CharacterTriggerData.Trigger trigger,ref CharacterState.FireTriggersData fireTriggersData)
    {
        if(trigger==ArcaneSlayEvent.Trigger&&fireTriggersData==null&&ArcaneSlayEvent.EnqueuingSource is CardState source)
            fireTriggersData=new ArcaneSlayEvent.Data {Source=source};
    }
}
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.FireTriggers))]
internal static class ArcaneSlayTriggerHook
{
    static void Postfix(CharacterState __instance,CharacterTriggerData.Trigger trigger,CharacterState.FireTriggersData fireTriggersData,ref IEnumerator __result)
    {
        if(trigger==ArcaneSlayEvent.Trigger&&fireTriggersData is ArcaneSlayEvent.Data data)
            __result=ArcaneSlayEvent.Run(__result,__instance,data);
    }
}
// 2026-10-25-1849||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ArcaneSlayEvent.cs||Separar núcleo Arcane Slay, payload de autoría y colas nativas sin AcademicRuntime
