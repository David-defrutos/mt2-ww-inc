using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;

namespace MonsterTrain2.SharedTriggers;

// The native queue retains FireTriggersData, but does not expose paramInt to effects.
// Keep the amount scoped to the coroutine which actually executes the queued event.
public static class OverhealEvent
{
    public static CharacterTriggerData.Trigger Trigger;
    internal sealed class Data : CharacterState.FireTriggersData
    {
        internal CardState? Source;
    }
    sealed class Context
    {
        internal CharacterState Unit = null!;
        internal int Amount;
        internal CardState? Source;
    }
    static readonly Stack<Context> contexts = new();
    public static IEnumerator Dispatch(ICoreGameManagers core,CharacterState unit,int excess,CardState? source)
    {
        if(excess<=0||unit.IsDestroyed||!unit.IsAlive)yield break;
        yield return core.GetCombatManager().QueueAndRunTrigger(unit,Trigger,
            fireTriggersData:new Data {paramInt=excess,Source=source});
    }
    internal static IEnumerator Run(IEnumerator original,CharacterState unit,CharacterState.FireTriggersData data)
    {
        var context=new Context {Unit=unit,Amount=Math.Max(0,data.paramInt),Source=(data as Data)?.Source};
        contexts.Push(context);
        try { yield return original; }
        finally { contexts.Pop(); }
    }
    public static int Amount(CardEffectParams p)
        =>contexts.Count>0&&contexts.Peek().Unit==p.selfTarget&&p.sourceCharacterTriggerState?.GetTrigger()==Trigger
            ?contexts.Peek().Amount:0;
    public static CardState? Source=>contexts.Count>0?contexts.Peek().Source:null;
}

[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.FireTriggers))]
internal static class OverhealTriggerHook
{
    static void Postfix(CharacterState __instance,CharacterTriggerData.Trigger trigger,CharacterState.FireTriggersData fireTriggersData,ref IEnumerator __result)
    {
        if(trigger==OverhealEvent.Trigger&&fireTriggersData!=null)
            __result=OverhealEvent.Run(__result,__instance,fireTriggersData);
    }
}
// 2026-10-25-1849||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/OverhealEvent.cs||Separar núcleo Overheal sin dependencias de W&W; recibir managers desde el caller
// 2026-10-25-1853||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/OverhealEvent.cs||Limpiar importación de efectos consumidores retirada del núcleo independiente
