using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;

namespace WwInc;

// Generic queued trigger: amount belongs to this event, never to the last global hit.
internal static class OverdamageEvent
{
    internal static CharacterTriggerData.Trigger Trigger;
    internal sealed class Data : CharacterState.FireTriggersData
    {
        internal CardState? Source;
    }
    sealed class Context
    {
        internal CharacterState Unit=null!;
        internal int Amount;
        internal CardState? Source;
    }
    static readonly Stack<Context> contexts=new();
    internal static IEnumerator Dispatch(CharacterState unit,int excess,CardState? source)
    {
        if(!AcademicRuntime.Active||excess<=0||unit.IsDestroyed||!unit.IsAlive||!unit.HasEffectTrigger(Trigger))yield break;
        yield return AcademicRuntime.Core.GetCombatManager().QueueAndRunTrigger(unit,Trigger,
            fireTriggersData:new Data {paramInt=excess,Source=source});
    }
    internal static IEnumerator Run(IEnumerator original,CharacterState unit,Data data)
    {
        contexts.Push(new Context {Unit=unit,Amount=Math.Max(0,data.paramInt),Source=data.Source});
        try { yield return original; }
        finally { contexts.Pop(); }
    }
    internal static int Amount(CardEffectParams parameters)
        =>contexts.Count>0&&contexts.Peek().Unit==parameters.selfTarget&&
            parameters.sourceCharacterTriggerState?.GetTrigger()==Trigger?contexts.Peek().Amount:0;
    internal static CardState? Source=>contexts.Count>0?contexts.Peek().Source:null;
}

[HarmonyPatch(typeof(CharacterState),"Attacking")]
internal static class OverdamageAttackHook
{
    static void Postfix(CharacterState __instance,CharacterState attackedCharacter,int damage,int damageSustained,
        Damage.Type damageType,CardState damageSourceCard,ref IEnumerator __result)
        =>__result=Run(__result,__instance,attackedCharacter,damage,damageSustained,damageType,damageSourceCard);
    static IEnumerator Run(IEnumerator original,CharacterState attacker,CharacterState victim,int damage,int sustained,
        Damage.Type kind,CardState? source)
    {
        bool lethalOpponent=victim!=null&&!victim.IsDestroyed&&victim.IsDead&&
            victim.GetTeamType()!=attacker.GetTeamType();
        int excess=kind==Damage.Type.DirectAttack&&lethalOpponent?AcademicMath.OverdamageAmount(damage,sustained):0;
        yield return original;
        if(excess>0)yield return OverdamageEvent.Dispatch(attacker,excess,source);
    }
}

[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.FireTriggers))]
internal static class OverdamageTriggerHook
{
    static void Postfix(CharacterState __instance,CharacterTriggerData.Trigger trigger,
        CharacterState.FireTriggersData fireTriggersData,ref IEnumerator __result)
    {
        if(trigger==OverdamageEvent.Trigger&&fireTriggersData is OverdamageEvent.Data data)
            __result=OverdamageEvent.Run(__result,__instance,data);
    }
}

// This consumer is separate from detection: other cards may attach different effects to Overdamage.
public sealed class CardEffectOverdamageExtraCredit : AcademicEffectBase
{
    public override bool TestEffect(CardEffectState state,CardEffectParams parameters,ICoreGameManagers core)=>true;
    public override IEnumerator ApplyEffect(CardEffectState state,CardEffectParams parameters,ICoreGameManagers core,ISystemManagers systems)
    {
        if(core.GetSaveManager().PreviewMode||parameters.selfTarget==null)yield break;
        var pool=state.GetParamCardPool();
        if(pool==null||pool.GetNumCards()!=1)throw new InvalidOperationException("Overdamage generation requires exactly one configured card");
        ExcessCredit.Generate(parameters.selfTarget,OverdamageEvent.Amount(parameters),state.GetParamInt(),state.GetParamCardUpgradeData(),pool.GetCardAtIndex(0));
    }
}
// 2026-10-24-1843||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/OverdamageEvent.cs||Crear trigger Overdamage genérico: cola nativa, payload de exceso, contexto seguro y consumidor independiente Extra Credit
// 2026-10-24-1946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/OverdamageEvent.cs||Pasar al consumidor de acumulación la mejora configurada en JSON
// 2026-10-25-1616||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/OverdamageEvent.cs||Leer tarjeta generada del mismo pool que el motor usa para explicar Extra Credit
// 2026-10-27-1534||codex-ww-inc||salidas/build/ww-inc/src/OverdamageEvent.cs||No despachar Overdamage para unidades sin ese trigger
