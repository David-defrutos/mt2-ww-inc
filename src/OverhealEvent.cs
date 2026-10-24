using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace WwInc;

// The native queue retains FireTriggersData, but does not expose paramInt to effects.
// Keep the amount scoped to the coroutine which actually executes the queued event.
internal static class OverhealEvent
{
    internal static CharacterTriggerData.Trigger Trigger;
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
    internal static IEnumerator Dispatch(CharacterState unit,int excess,CardState? source)
    {
        if(!AcademicRuntime.Active||excess<=0||unit.IsDestroyed||!unit.IsAlive)yield break;
        yield return AcademicRuntime.Core.GetCombatManager().QueueAndRunTrigger(unit,Trigger,
            fireTriggersData:new Data {paramInt=excess,Source=source});
    }
    internal static IEnumerator Run(IEnumerator original,CharacterState unit,CharacterState.FireTriggersData data)
    {
        var context=new Context {Unit=unit,Amount=Math.Max(0,data.paramInt),Source=(data as Data)?.Source};
        contexts.Push(context);
        try { yield return original; }
        finally { contexts.Pop(); }
    }
    internal static int Amount(CardEffectParams p)
        =>contexts.Count>0&&contexts.Peek().Unit==p.selfTarget&&p.sourceCharacterTriggerState?.GetTrigger()==Trigger
            ?contexts.Peek().Amount:0;
    internal static CardState? Source=>contexts.Count>0?contexts.Peek().Source:null;
    internal static CardEffectState Copy(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
    {
        var copy=new CardEffectState();
        copy.Setup(s.GetSourceCardEffectData(),p.selfTarget!.GetTeamType(),m.GetSaveManager());
        return copy;
    }
    internal static int Scale(int value,int amount)=>(int)Math.Min(int.MaxValue,(long)Math.Max(0,value)*Math.Max(0,amount));
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

// Only dynamic parameter scaling is custom. Native effects apply all combat rules.
public sealed class CardEffectOverhealStatus : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        int amount=OverhealEvent.Amount(p);if(amount<=0)yield break;
        var copy=OverhealEvent.Copy(s,p,m);
        var stacks=s.GetParamStatusEffectStackData().Select(stack=> {
            var entry=stack.Copy();entry.count=OverhealEvent.Scale(entry.count,amount);
            if(entry.statusId=="armor")entry.count=AcademicMath.OverhealArmor(entry.count,p.selfTarget!.GetMaxHP());
            return entry;
        }).ToArray();
        copy.SetParamStatusEffects(stacks);
        yield return new CardEffectAddStatusEffect().ApplyEffect(copy,p,m,systems);
    }
}
public sealed class CardEffectOverhealDamage : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        int amount=OverhealEvent.Amount(p);
        if(amount<=0||AcademicRuntime.HostileHealingDepth!=0)yield break;
        var copy=OverhealEvent.Copy(s,p,m);copy.SetParamInt(OverhealEvent.Scale(s.GetParamInt(),amount));
        var originalSource=p.playedCard;p.playedCard=OverhealEvent.Source;
        AcademicRuntime.ConversionDepth++;AcademicRuntime.HostileHealingDepth++;
        try { yield return new CardEffectDamage().ApplyEffect(copy,p,m,systems); }
        finally { AcademicRuntime.HostileHealingDepth--;AcademicRuntime.ConversionDepth--;p.playedCard=originalSource; }
    }
}
// 2026-10-12-2211||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/OverhealEvent.cs||Crear trigger Overheal con cantidad por evento en cola y adaptadores a efectos nativos sin modificar datos compartidos
// 2026-10-12-2213||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/OverhealEvent.cs||Asegurar referencia del portador tras comprobar contexto Overheal
// 2026-10-21-2059||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/OverhealEvent.cs||Aplicar límite por activación solo a Armor; otros estados y daño intactos
