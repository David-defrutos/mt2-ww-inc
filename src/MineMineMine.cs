using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;

namespace WwInc;

// Capture only spell damage. Attribution is resolved after damage, without changing mitigation.
internal static class MineMineMine
{
    internal static CharacterTriggerData.Trigger Trigger;
    internal static readonly HashSet<CharacterState> Claimed=new();
    internal sealed class DamageContext
    {
        internal CharacterState Victim=null!;
        internal CardState Spell=null!;
        internal int Floor;
        internal CharacterState? Owner;
        internal bool Dispatched;
    }
    internal sealed class Data : CharacterState.FireTriggersData
    {
        internal CharacterState Victim=null!;
        internal CardState Spell=null!;
    }
    sealed class Execution
    {
        internal CharacterState Owner=null!;
        internal Data Data=null!;
        internal bool SlayGranted;
    }
    static readonly Stack<DamageContext> damageContexts=new();
    static readonly Stack<Execution> executions=new();
    static readonly Stack<CharacterState> grantingSlay=new();
    static readonly System.Reflection.PropertyInfo Primary=AccessTools.Property(typeof(CharacterState),"PrimaryStateInformation");
    static readonly System.Reflection.FieldInfo LastAttacker=AccessTools.Field(Primary.PropertyType,"lastAttackerCharacter");
    internal static DamageContext? Current(CharacterState victim)
        =>damageContexts.Count>0&&ReferenceEquals(damageContexts.Peek().Victim,victim)?damageContexts.Peek():null;
    internal static bool Holds(CharacterState unit)=>unit.HasEffectTrigger(Trigger);
    internal static CharacterState? Claim(DamageContext? context)
    {
        if(context==null||context.Victim.IsDestroyed||!context.Victim.IsDead)return null;
        if(context.Owner==null)
        {
            // Native room order is front-to-back, matching the game's FrontInRoom selector.
            var candidates=AcademicRuntime.Units(context.Floor);
            int index=MineMineMineRules.FirstHolder(candidates.ConvertAll(Holds));
            if(index>=0)context.Owner=candidates[index];
        }
        if(context.Owner!=null)
        {
            LastAttacker.SetValue(Primary.GetValue(context.Victim),context.Owner);
            if(!AcademicRuntime.Simulating)Claimed.Add(context.Victim);
        }
        return context.Owner;
    }
    internal static IEnumerator ObserveDamage(IEnumerator original,CharacterState victim,CharacterState.ApplyDamageParams parameters)
    {
        if(!AcademicRuntime.Active){yield return original;yield break;}
        if(!AcademicRuntime.Simulating)Claimed.Remove(victim);
        var spell=parameters.damageSourceCard;
        if(spell==null||spell.GetCardType()!=CardType.Spell||spell.IsAnyAbility()||
            parameters.damageType==Damage.Type.DirectAttack||victim.IsDestroyed||victim.IsDead||
            victim.GetTeamType()!=Team.Type.Heroes){yield return original;yield break;}
        damageContexts.Push(new DamageContext {Victim=victim,Spell=spell,Floor=victim.GetCurrentRoomIndex()});
        try {yield return original;}
        finally {damageContexts.Pop();}
    }
    internal static bool SuppressOriginalSlay(CharacterState victim)
        =>!(grantingSlay.Count>0&&ReferenceEquals(grantingSlay.Peek(),victim))&&Claim(Current(victim))!=null;
    internal static IEnumerator BeforeDeath(IEnumerator original,CharacterState victim)
    {
        var context=Current(victim);var owner=Claim(context);
        if(owner!=null&&context!=null&&!context.Dispatched)
        {
            context.Dispatched=true;
            yield return AcademicRuntime.Core.GetCombatManager().QueueAndRunTrigger(owner,Trigger,dyingCharacter:victim,
                fireTriggersData:new Data {Victim=victim,Spell=context.Spell});
        }
        yield return original;
    }
    internal static IEnumerator Execute(IEnumerator original,CharacterState owner,Data data)
    {
        executions.Push(new Execution {Owner=owner,Data=data});
        try {yield return original;}
        finally {executions.Pop();}
    }
    internal static IEnumerator GrantSlay(CardEffectParams parameters,ICoreGameManagers core)
    {
        if(executions.Count==0||parameters.selfTarget!=executions.Peek().Owner||
            parameters.sourceCharacterTriggerState?.GetTrigger()!=Trigger)yield break;
        var context=executions.Peek();
        if(context.SlayGranted)yield break;
        context.SlayGranted=true;
        grantingSlay.Push(context.Data.Victim);
        try {yield return core.GetCombatManager().QueueAndRunTrigger(context.Owner,CharacterTriggerData.Trigger.OnKill,context.Data.Victim);}
        finally {grantingSlay.Pop();}
    }
}

[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.ApplyDamage))]
[HarmonyAfter("Conductor")]
internal static class MineMineMineDamageHook
{
    static void Postfix(CharacterState __instance,CharacterState.ApplyDamageParams damageParams,ref IEnumerator __result)
        =>__result=MineMineMine.ObserveDamage(__result,__instance,damageParams);
}
[HarmonyPatch(typeof(CombatManager),nameof(CombatManager.QueueTrigger),new Type[] {
    typeof(CharacterState),typeof(CharacterTriggerData.Trigger),typeof(CharacterState),
    typeof(bool),typeof(bool),typeof(CharacterState.FireTriggersData),typeof(int),typeof(CharacterTriggerState) })]
internal static class MineMineMineSlayHook
{
    static bool Prefix(CharacterTriggerData.Trigger trigger,CharacterState dyingCharacter)
        =>trigger!=CharacterTriggerData.Trigger.OnKill||dyingCharacter==null||!MineMineMine.SuppressOriginalSlay(dyingCharacter);
}
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.CheckDeathCardTriggers))]
internal static class MineMineMineDeathHook
{
    static void Postfix(CharacterState __instance,ref IEnumerator __result)
        =>__result=MineMineMine.BeforeDeath(__result,__instance);
}
[HarmonyPatch(typeof(CharacterState),"CheckForDeath")]
internal static class MineMineMineCreditHook
{
    static void Prefix(CharacterState __instance,bool forSacrifice,ref CharacterState attacker)
    {
        if(!forSacrifice&&MineMineMine.Claim(MineMineMine.Current(__instance)) is { } owner)attacker=owner;
    }
}
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.FireTriggers))]
internal static class MineMineMineTriggerHook
{
    static void Postfix(CharacterState __instance,CharacterTriggerData.Trigger trigger,
        CharacterState.FireTriggersData fireTriggersData,ref IEnumerator __result)
    {
        if(trigger==MineMineMine.Trigger&&fireTriggersData is MineMineMine.Data data)
            __result=MineMineMine.Execute(__result,__instance,data);
    }
}
public sealed class CardEffectMineMineMineClaim : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool CanPlayAfterBossDead=>true;
    public override bool TestEffect(CardEffectState state,CardEffectParams parameters,ICoreGameManagers core)=>true;
    public override IEnumerator ApplyEffect(CardEffectState state,CardEffectParams parameters,ICoreGameManagers core,ISystemManagers systems)
        =>MineMineMine.GrantSlay(parameters,core);
}
// 2026-10-24-1930||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/MineMineMine.cs||Trigger reutilizable de atribución al primer portador frontal; contexto por daño, Slay nativo único y crédito de muerte sin modificar daño
// 2026-10-24-1932||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/MineMineMine.cs||Evitar nombre Damage que oculta enum nativo y conceder Slay una sola vez por evento incluso con efectos duplicados
// 2026-10-27-1428||codex-ww-inc||salidas/build/ww-inc/src/MineMineMine.cs||Ejecutar wrapper después de Conductor para que su detector vea la coroutine nativa ApplyDamage
