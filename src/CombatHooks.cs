using MonsterTrain2.SharedTriggers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace WwInc;

[HarmonyPatch(typeof(CardManager), nameof(CardManager.SetupCards))]
internal static class BattleStartHook
{
    static void Postfix(CardManager __instance)=>AcademicRuntime.Reset(__instance);
}
[HarmonyPatch(typeof(CardManager),nameof(CardManager.DrawHand))]
internal static class TurnStartHook
{
    static void Postfix(ref IEnumerator __result) { __result=Run(__result); }
    static IEnumerator Run(IEnumerator original)
    {
        yield return AcademicRuntime.BeginTurn();
        yield return original;
    }
}
[HarmonyPatch(typeof(CombatManager),"RunEndMonsterTurn")]
internal static class TurnEndHook
{
    static void Postfix(ref IEnumerator __result) { __result=Run(__result); }
    static IEnumerator Run(IEnumerator original) { yield return original; AcademicRuntime.EndTurn(); }
}
[HarmonyPatch]
internal static class SpellResolutionHook
{
    static MethodBase TargetMethod()=>typeof(CombatManager).GetMethods().Single(m=>m.Name==nameof(CombatManager.ApplyEffects)&&m.GetParameters().FirstOrDefault()?.ParameterType==typeof(List<CardEffectState>));
    static void Postfix(CardState playedCard,int selectedRoom,bool fromPlayedCard,CharacterState selfTarget,SpawnPoint dropLocation,AllGameManagers ___allGameManagers,ref IEnumerator __result)
    {
        if(playedCard==null || !fromPlayedCard || playedCard.IsUnitAbility() || playedCard.GetCardType()!=CardType.Spell) return;
        __result=Run(__result,playedCard,selectedRoom,dropLocation?.GetCharacterState()??selfTarget,___allGameManagers.GetCoreManagers());
    }
    static IEnumerator Run(IEnumerator original,CardState card,int floor,CharacterState target,ICoreGameManagers core)
    {
        if(core.GetSaveManager().PreviewMode&&!PreviewScope.Active){yield return original;yield break;}
        var previousCore=AcademicRuntime.Core;AcademicRuntime.Core=core;
        var frame=new AcademicRuntime.SpellResolution { Card=card,Floor=floor,FirstDamage=card.GetIsDamageCard()&&AcademicRuntime.FirstDamageSpell==null,WasFrozen=card.HasTrait(typeof(CardTraitFreeze)) };
        try
        {
            if(target!=null&&AcademicRuntime.Is(target,"WW_U01")&&LightningRodSafety.Eligible(card)&&
                RedirectTargets.InRoom(core.GetRoomManager().GetRoom(floor),Team.Type.Heroes).Count>0)
            {
                frame.Redirected=true;
                yield return LightningRodSafety.Feedback(target,card,core);
                target=RedirectTargets.InRoom(core.GetRoomManager().GetRoom(floor),Team.Type.Heroes).FirstOrDefault();
            }
            yield return SpellRules.Before(frame,target);
            if(!frame.Indirect&&card.GetRarity()==CollectableRarity.Common)AcademicRuntime.LastPlayedCommonSpell=card;
            if(card.GetIsDamageCard())
            {
                AcademicRuntime.DamageSpellPlayed=true;
                AcademicRuntime.FirstDamageSpell ??=card;
            }
            AcademicRuntime.Frames.Push(frame);
            try { yield return original; }
            finally { AcademicRuntime.Frames.Pop(); }
            yield return AcademicRuntime.AfterSpell(frame);
        }
        finally
        {
            try {foreach(var cleanup in frame.Cleanup)cleanup();}
            finally {AcademicRuntime.Core=previousCore;}
        }
    }
}
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.ApplyHeal))]
internal static class HealHook
{
    static void Postfix(CharacterState __instance,int amount,CardState responsibleCard,bool fromMaxHPChange,ref IEnumerator __result)
        =>__result=Run(__result,__instance,amount,responsibleCard,fromMaxHPChange);
    static IEnumerator Run(IEnumerator original,CharacterState unit,int amount,CardState source,bool fromMaxHPChange)
    {
        if(!AcademicRuntime.Active||!PreviewDependencies.CanWrite(unit,AcademicRuntime.Core)){yield return original;yield break;}
        int before=unit.GetHP();
        int possible=unit.GetHealToTarget(amount,source,fromMaxHPChange,limitByHp:false);
        int excess=fromMaxHPChange||amount>=9999?0:AcademicMath.ExcessHealing(possible,before,unit.GetMaxHP());
        yield return original;
        if(!unit.IsDestroyed&&unit.IsAlive)
        {
            AcademicRuntime.RecordHeal(unit,Math.Max(0,unit.GetHP()-before),excess,source);
            if(AcademicRuntime.Active&&unit.HasEffectTrigger(OverhealEvent.Trigger))yield return OverhealEvent.Dispatch(AcademicRuntime.Core,unit,excess,source);
        }
    }
}
// 2026-10-10-0003||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Correcciones de resoluciÃƒÂ³n, traits y acumulaciÃƒÂ³n de reglas
// 2026-10-10-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Aplicar el daño previo de Dazed antes de añadir la nueva acumulación
// 2026-10-10-0018||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Los bonos duran hasta terminar el combate, después de descartar la mano
// 2026-10-10-0020||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Coste de redirección basado en Dazed previo comprobado
// 2026-10-10-2358||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Retirar bonos temporales incluso si falla la resolución original
// 2026-10-11-0002||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Ampliar finally a la preparación del hechizo para no filtrar mejoras temporales
// 2026-10-11-0003||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Normalizar indentación de resolución protegida

// 2026-10-11-1104||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Retirar solo cargas del Proyecto tras resolución y repeticiones

// 2026-10-12-0111||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Redirección acepta efectos Heroes o Both; filtros de UI, selección, destino y resolución usan flags

// 2026-10-12-0112||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Exigir enemigo vivo para activar redirección Both y conservar destino aliado normal sin enemigo

// 2026-10-12-0927||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Eliminar retirada de cargas de Deferred Project al resolverse

// 2026-10-12-1438||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Registrar hechizo global antes de resolver daño, incluidas muertes durante su lanzamiento

// 2026-10-12-1543||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Registrar y copiar solo último hechizo Common global; posteriores rarezas no sustituyen el registro

// 2026-10-12-1808||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Medir exceso antes de triggers de curación, con modificadores nativos y sin cambios de HP máximo

// 2026-10-12-2053||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Reutilizar cálculo de exceso de ApplyHeal para activar pasiva Hostile Medicine
// 2026-10-12-2211||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Despachar Overheal al personaje curado con la cantidad sobrante y la fuente original
// 2026-10-21-2043||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Excluir Overheal también por cantidad original >=9999 antes de modificadores; conservar curación nativa
// 2026-10-25-1605||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Resolver con managers nativos del caller y proteger preparación, preview, reembolso y restauración de contexto
// 2026-10-25-1849||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CombatHooks.cs||Conectar consumidores al núcleo independiente con managers explícitos y guardias W&W conservadas
// 2026-10-27-1534||codex-ww-inc||salidas/build/ww-inc/src/CombatHooks.cs||No generar eventos Overheal en unidades sin consumidores del trigger
// 2026-10-27-1535||codex-ww-inc||salidas/build/ww-inc/src/CombatHooks.cs||No calcular ni despachar efectos propios de curación sobre estados no preparados
