using System;
using System.Collections;
using System.Linq;
using HarmonyLib;

namespace WwInc;

[HarmonyPatch(typeof(MonsterManager),nameof(MonsterManager.RemoveCharacter))]
internal static class DeathHook
{
    static void Prefix(CharacterState removeCharacter,bool death,ref bool vanishUnit)
    { if(AcademicRuntime.Active&&death&&removeCharacter.GetSourceCharacterData().name.Contains("WW_Token_"))vanishUnit=true; }
    static void Postfix(CharacterState removeCharacter,bool death,ref IEnumerator __result)
    { if(AcademicRuntime.Active&&death)__result=Run(__result,removeCharacter); }
    static IEnumerator Run(IEnumerator original,CharacterState unit)
    {
        int floor=unit.GetCurrentRoomIndex();bool chaperone=SocialRules.Chaperone!=null&&unit.GetHasSubtype(SocialRules.Chaperone);
        var archived=SpellRules.Archived.TryGetValue(unit,out var stored)?stored.ToArray():Array.Empty<CardState>();
        var bound=SpellRules.BoundAttacks.TryGetValue(unit,out var bindings)?bindings.Select(b=>b.card).ToArray():Array.Empty<CardState>();
        if(AcademicRuntime.BoundForAttack.TryGetValue(unit,out var active))bound=bound.Concat(active).Distinct().ToArray();
        yield return original;
        SpellRules.BoundAttacks.Remove(unit);
        foreach(var spell in bound)yield return SpellRules.ReleaseBoundSpell(spell);
        if(!unit.IsDestroyed)DissertationMarker.Refresh(unit);
        if(!AcademicRuntime.Simulating&&archived.Length>0){SpellRules.Archived.Remove(unit);foreach(var spell in archived)yield return SpellRules.Restore(spell,false,true,attunedThisTurn:true);}
        if(chaperone&&SpellRules.HasRelic("WW_A08")&&SpellRules.OnceTurn.Add("A08"))
            foreach(var ally in AcademicRuntime.Units(floor))
                if(ally.GetStatusEffectStacks("cooldown")>0)ally.RemoveStatusEffect("cooldown",1);
        SocialRules.RefreshAuras();
    }
}
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.GetUnitAbilityCooldown))]
internal static class PlatonicCooldownHook
{
    static void Postfix(CharacterState __instance,ref int __result)
    {
        var core=AcademicRuntime.Core;if(core==null||SocialRules.Wizard==null||__instance.IsDestroyed)return;
        var room=__instance.GetCurrentRoom();if(room==null||!room.GetRoomStateModifiersFromTrainRoomAttachments<RoomStateAcademicPlatonic>(Team.Type.Monsters).Any())return;
        var units=AcademicRuntime.Units(room.GetRoomIndex());
        for(int i=0;i+1<units.Count;i++)
            if(SocialRules.Mixed(units[i],units[i+1])&&(units[i]==__instance||units[i+1]==__instance)){__result=Math.Max(1,__result-1);return;}
    }
}
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.SetSpawnPoint))]
internal static class AuraPositionHook
{
    static void Postfix()=>SocialRules.RefreshAuras();
}
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.ApplyDamage))]
internal static class UnitAttackHook
{
    static void Prefix(ref int damage,CharacterState.ApplyDamageParams damageParams)
    {
        if(!AcademicRuntime.Active)return;
        if(damageParams.damageSourceCard!=null && damageParams.damageType!=Damage.Type.SpellOverflow && SpellRules.DamageFactors.TryGetValue(damageParams.damageSourceCard,out var factor)) damage=(int)Math.Floor(damage*factor);
    }
    static void Postfix(CharacterState __instance,int damage,CharacterState.ApplyDamageParams damageParams,ref IEnumerator __result)
        =>__result=Run(__result,__instance,damage,damageParams);
    static IEnumerator Run(IEnumerator original,CharacterState target,int damage,CharacterState.ApplyDamageParams parameters)
    {
        if(!AcademicRuntime.Active){yield return original;yield break;}
        var attacker=parameters.attacker; bool direct=parameters.damageType==Damage.Type.DirectAttack;
        int hp=target.GetHP(),floor=target.GetCurrentRoomIndex();bool dead=target.IsDead;
        var observed=RepresentativeDamage.Begin(target);
        try {yield return original;}
        finally {RepresentativeDamage.End(observed);}
        AcademicRuntime.RecordDamage(target,Math.Max(0,hp-(target.IsDestroyed?0:target.GetHP())),!dead&&(target.IsDead||target.IsDestroyed),parameters.damageSourceCard,attacker,floor);
        if(attacker!=null&&direct)
        {
            var spells=new System.Collections.Generic.List<CardState>();
            var completed=new System.Collections.Generic.List<CardState>();
            if(AcademicRuntime.BoundForAttack.TryGetValue(attacker,out var active))spells.AddRange(active);
            else if(SpellRules.BoundAttacks.TryGetValue(attacker,out var bindings))
            {
                foreach(var bound in bindings.ToArray())
                {
                    spells.Add(bound.card);bindings.Remove(bound);
                    if(bound.attacks>1)bindings.Add((bound.card,bound.attacks-1));
                    else completed.Add(bound.card);
                }
                if(bindings.Count==0)SpellRules.BoundAttacks.Remove(attacker);
                DissertationMarker.Refresh(attacker);
            }
            foreach(var spell in spells)yield return DissertationTargeting.Repeat(spell,floor,attacker,target);
            foreach(var spell in completed)yield return SpellRules.ReleaseBoundSpell(spell);
        }
        if(!AcademicRuntime.Simulating&&damage>0&&target.HasStatusEffect(StatusEffectAcademicScholarship.Id))
        {
            var cards=AcademicRuntime.Core.GetCardManager().GetDrawPile().ToArray();
            if(cards.Length>0)SpellRules.ApplyHitDiscount(cards.RandomElement(RngId.Battle));
        }
    }
}
[HarmonyPatch(typeof(CardManager),nameof(CardManager.DiscardCard))]
internal static class DiscardRuleHook
{
    static void Postfix(CardManager __instance,CardManager.DiscardCardParams discardCardParams,ref IEnumerator __result)
        =>__result=Run(__instance,discardCardParams,__result);
    static IEnumerator Run(CardManager manager,CardManager.DiscardCardParams parameters,IEnumerator original)
    {
        if(!AcademicRuntime.Active||AcademicRuntime.Simulating){yield return original;yield break;}
        var card=parameters.discardCard;
        bool chain=parameters.wasPlayed&&SpellRules.HasRule(card,"spellchain");
        bool returned=parameters.wasPlayed&&SpellRules.HasRule(card,"kill_return")&&SpellRules.PlayedKills.TryGetValue(card,out int kills)&&kills>0;
        bool frozenReturn=parameters.wasPlayed&&SpellRules.HasRule(card,"return_frozen");
        if(frozenReturn)SpellRules.RemoveRule(card,"return_frozen");
        bool deferred=parameters.wasPlayed&&SpellRules.HasRule(card,"defer");
        if(returned||deferred||frozenReturn)parameters.outSuppressTraitOnDiscard=typeof(CardTraitExhaustState);
        yield return original;
        if(chain&&card.CanBeCopied())
        {
            SpellRules.RemoveRule(card,"spellchain");
            var copy=manager.AddNewCardCopy(AcademicRuntime.Core.GetAllGameData().FindCardData(card.GetCardDataID()),card,CardPile.HandPile,false,new CardManager.AddCardUpgradingInfo {tempCardUpgrade=true,upgradingCardSource=card,ignoreTempUpgrades=false,copyModifiersFromCard=card});
            if(copy!=null){AcademicRuntime.SetFixedCost(copy,1,false);AcademicRuntime.Trait(copy,"CardTraitEphemeral");}
        }
        if(deferred) {SpellRules.RemoveRule(card,"defer");SpellRules.Apart(card,AcademicRuntime.Turn+1);yield break;}
        if(returned||frozenReturn)
        {
            yield return SpellRules.Restore(card,false,false,zero:false);
            if(frozenReturn)CardEffectAcademicReturnFrozen.ProtectUntilNextTurn(card,manager);
            yield break;
        }
    }
}
// 2026-10-10-0003||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Correcciones de resoluciÃƒÂ³n, traits y acumulaciÃƒÂ³n de reglas
// 2026-10-10-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||No disparar robos falsos al colocar hechizos al fondo del mazo
// 2026-10-10-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Notificar movimiento de pila usando el método interno verificado
// 2026-10-10-0011||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Conservar triggers de descarte y combinar Spellchain, retorno y aplazamiento
// 2026-10-10-0016||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Copias Spellchain con coste fijo 1 aunque el original sea X
// 2026-10-10-0018||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||La reserva y las tres reproducciones cuentan ataques completos, no cada impacto de Sweep
// 2026-10-10-0021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Al morir devuelve cada hechizo apartado por usos anteriores de la habilidad
// 2026-10-10-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Reproducir todos los hechizos vinculados y conservar sus contadores independientes
// 2026-10-10-2358||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Simular pasivas sin devolver cartas ni alterar costes o descartes reales

[HarmonyPatch(typeof(MonsterManager),nameof(MonsterManager.CreateMonsterState))]
internal static class ChampionSpawnAuraHook
{
    static void Postfix(ref IEnumerator __result)=>__result=Run(__result);
    static IEnumerator Run(IEnumerator original){yield return original;SocialRules.RefreshAuras();}
}
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.ApplyCardUpgrade))]
internal static class ChampionUpgradeAuraHook
{
    static void Postfix(ref IEnumerator __result)=>__result=Run(__result);
    static IEnumerator Run(IEnumerator original){yield return original;SocialRules.RefreshAuras();}
}
// 2026-10-10-2359||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Actualizar auras tras invocar unidades y aplicar mejoras, incluidas multisendas

[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.RemoveCardUpgrade))]
internal static class ChampionRemovedUpgradeAuraHook
{
    static void Postfix(ref IEnumerator __result)=>__result=Run(__result);
    static IEnumerator Run(IEnumerator original){yield return original;SocialRules.RefreshAuras();}
}
// 2026-10-11-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Recalcular auras tras retirar mejoras o devolver equipo

[HarmonyPatch(typeof(CardManager),nameof(CardManager.PlayAnyCard))]
internal static class FrozenCardDrawHook
{
    static void Prefix(CardState cardState,out bool __state)
        =>__state=AcademicRuntime.Active&&!AcademicRuntime.Simulating&&!cardState.IsAnyAbility()&&cardState.HasTrait(typeof(CardTraitFreeze));
    static void Postfix(CardManager __instance,bool __result,bool __state)
    {
        if(__result&&__state&&SpellRules.HasRelic("WW_A03")&&SpellRules.OnceTurn.Add("A03"))__instance.DrawCards(1);
    }
}

// 2026-10-11-0941||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Devolver Consume sin duplicados ni pérdida con mano llena; Freeze detecta cualquier carta jugada con éxito

// 2026-10-11-1554||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Eliminar mejora de coste +1 al retornar un hechizo con kill_return

// 2026-10-11-1618||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Eliminar traslado al fondo del mazo de hechizos descartados por Exam Week

// 2026-10-11-1816||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Extinguish de Postdoc crea copia sin mover ni modificar el hechizo original

// 2026-10-12-1438||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Muerte de Postdoc copia último hechizo global sin filtrar piso ni mover original

// 2026-10-12-1543||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Registrar y copiar solo último hechizo Common global; posteriores rarezas no sustituyen el registro

// 2026-10-12-1554||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Retirar copia de Postdoc del hook de eliminación para activar por Extinguish sin duplicación

// 2026-10-12-1758||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Reutilizar retorno tras descarte para Slay; marcar una sola devolución y aplicar Freeze/Unplayable

// 2026-10-12-1808||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Eliminar incremento de daño de Emeritus; exceso convertido en Armor

// 2026-10-12-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Strictly Platonic: reducir cooldown de miembros de dúos Wizard–Witch adyacentes
// 2026-10-12-2255||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Scholarship no expira al final de turno; limpiar descuento solo al jugar, antes de retornos o copias
// 2026-10-12-2322||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||No restaurar coste de Scholarship al jugar el hechizo
// 2026-10-13-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Sincronizar marca de ataques fuera de secuencia y limpiar vínculo cuando se retira la unidad
// 2026-10-13-0031||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Aplicar descuento al recibir golpe solo mientras la unidad tiene el estado visible Scholarship
// 2026-10-13-0051||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Scholarship elige hechizo aleatorio de pila de robo durante golpes enemigos, en vez de mano descartada
// 2026-10-13-0106||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Liberar vínculo tras muerte y ataques directos alternativos; evitar duplicados
// 2026-10-13-0108||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Scholarship selecciona cualquier carta de robo; retirar filtro Spell y conservar descuento nativo
// 2026-10-14-2156||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Eliminar conversión de Spell Weakness en Melee Weakness del Inspector
// 2026-10-24-2100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Reproducir Dissertation con aliado portador y enemigo atacado incluso tras baja del objetivo
// 2026-10-25-1605||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LifecycleHooks.cs||Observar daño nativo en contexto por víctima con limpieza de pila garantizada
