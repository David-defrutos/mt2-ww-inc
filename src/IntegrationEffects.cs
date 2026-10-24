using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace WwInc;

public sealed class CardEffectAcademicWizardDraw : AcademicEffectBase,ICardTargetingCardEffect,ICardEffectDrawsCards
{
    readonly CardEffectDrawType nativeDraw=new();
    internal static CardUpgradeData NextUseUpgrade=null!;
    public override bool CanPlayAfterBossDead=>false;
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
    {
        var cm=m.GetCardManager();var filter=s.GetParamCardFilter();
        return !cm.GetDrawingDeckCardsDisabledThisTurn()&&cm.GetBelowHandSize()&&filter!=null&&
            cm.GetDrawPile().Concat(cm.GetDiscardPile()).Any(c=>c!=p.playedCard&&filter.FilterCard(c,m.GetRelicManager()));
    }
    public int GetNumCardTargets(CardState card,CardEffectState effect,CardStatistics statistics)
        =>nativeDraw.GetNumCardTargets(card,effect,statistics);
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        if(!TestEffect(s,p,m))yield break;
        AcademicRuntime.Core=m;
        // Shared by all paths: native Runespeak upgrade-then-draw sequence.
        var upgrade=new CardUpgradeState();upgrade.Setup(NextUseUpgrade);
        upgrade.SetAvoidClobberingExistingTraits(true);
        m.GetCardManager().AddTempCardUpgradeToNextDrawnCard(upgrade);
        yield return nativeDraw.ApplyEffect(s,p,m,systems);
    }
    internal static int Rank(CharacterState? unit,string prefix)=>unit==null?0:AcademicMath.ChampionRank(ChampionPathText.Selected(unit.GetSpawnerCard(),unit).Select(u=>u.GetAssetName()),prefix);
}

public sealed class CardEffectAcademicPurgePermit : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    static readonly RingPermitLedger Permit=new();
    internal static void BeginBattle(ICoreGameManagers core)=>Permit.BeginBattle(Ring(core),SaveManager.IsInUndoModeStatic);
    static string Ring(ICoreGameManagers core)=>core.GetSaveManager().GetRunId()+":"+core.GetSaveManager().GetRunState().GetLoopIndex()+":"+core.GetSaveManager().GetRunState().GetCurrentDistance();
    static CardState? Equipment(CardEffectState effect,CardEffectParams parameters)
        =>effect.GetParentEquipment()??(parameters.characterThatActivatedAbility??parameters.selfTarget)?
            .GetEquipment().LastOrDefault(card=>AcademicRuntime.Id(card).EndsWith("WW_E01",StringComparison.Ordinal));
    static bool Eligible(CardState card,CardEffectState effect,ICoreGameManagers managers,CardState equipment)
    {
        var filter=effect.GetParamCardFilter();
        return Permit.Available(Ring(managers),equipment)&&filter!=null&&filter.FilterCard(card,managers.GetRelicManager());
    }
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>Equipment(s,p) is { } equipment&&m.GetCardManager().GetHand().Any(c=>c!=p.playedCard&&Eligible(c,s,m,equipment));
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;
        var equipment=Equipment(s,p);
        if(equipment==null||!TestEffect(s,p,m))yield break;
        yield return AcademicRuntime.ChooseHand(p.playedCard,c=>Eligible(c,s,m,equipment),card=> {
            AcademicRuntime.Core=m;
            string ring=Ring(m);
            AcademicRuntime.Trait(card,"CardTraitSelfPurge");
            Permit.MarkUsed(ring,equipment);
        });
    }
}

[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.GetAttackDamage))]
internal static class ZeroAttackHook
{
    static void Postfix(CharacterState __instance,ref int __result)
    { if(AcademicRuntime.Active&&AcademicRuntime.ZeroAttack.Contains(__instance))__result=0; }
}

internal static class RedirectTargets
{
    // Selection gets a live room or managers from the native caller. Do not
    // resolve that room through a manager retained by an earlier preview.
    internal static List<CharacterState> InRoom(RoomState? room,Team.Type team)
    {
        var units=new List<CharacterState>();
        if(room!=null)room.AddCharactersToList(units,team);
        return units.Where(u=>!u.IsDead&&!u.IsDestroyed).ToList();
    }
}

[HarmonyPatch]
internal static class EligibleRedirectHook
{
    static MethodBase TargetMethod()=>typeof(TargetHelper).GetMethods().Single(m=>m.Name==nameof(TargetHelper.CollectTargets)&&m.GetParameters()[0].ParameterType==typeof(CardEffectState));
    static void Postfix(CardEffectState effectState,CardEffectParams cardEffectParams,ICoreGameManagers coreGameManagers)
    {
        var card=cardEffectParams.playedCard;if(card==null||card.IsUnitAbility()||card.GetCardType()!=CardType.Spell)return;
        if(coreGameManagers.GetSaveManager().PreviewMode&&!PreviewScope.Active)return;
        AcademicRuntime.Core=coreGameManagers;
        if(SpellRules.IsAreaEffect(card,effectState))
        {
            cardEffectParams.targets.Clear();
            cardEffectParams.targets.AddRange(RedirectTargets.InRoom(coreGameManagers.GetRoomManager().GetRoom(cardEffectParams.selectedRoom),Team.Type.Heroes));
            return;
        }
        if(!effectState.GetTargetTeamType().HasFlag(Team.Type.Heroes))return;
        if(effectState.GetTargetMode()==TargetMode.DropTargetCharacter)
        {
            if(RedirectTargets.InRoom(coreGameManagers.GetRoomManager().GetRoom(cardEffectParams.selectedRoom),Team.Type.Heroes).Count>0)
                cardEffectParams.targets.AddRange(RedirectTargets.InRoom(coreGameManagers.GetRoomManager().GetRoom(cardEffectParams.selectedRoom),Team.Type.Monsters).Where(u=>AcademicRuntime.Is(u,"WW_U01")&&!cardEffectParams.targets.Contains(u)));
        }
    }
}

[HarmonyPatch(typeof(TargetHelper),nameof(TargetHelper.CheckTargetsOverride))]
internal static class RedirectTargetHook
{
    static bool Prefix(CardEffectState effectState,List<CharacterState> targets,SpawnPoint dropLocation,ref CharacterState overrideTargetCharacter)
    {
        var card=effectState.GetParentCardState();if(card==null||card.IsUnitAbility())return true;
        if(SpellRules.IsAreaEffect(card,effectState))
        {return false;}
        // Repeat supplies its own target; do not restore the original redirected enemy.
        if(SpellRules.EffectFloor>=0)return true;
        if(AcademicRuntime.Frames.Count>0&&AcademicRuntime.Frames.Peek().Card==card&&AcademicRuntime.Frames.Peek().Redirected&&effectState.GetTargetTeamType().HasFlag(Team.Type.Heroes))
        {overrideTargetCharacter=AcademicRuntime.Frames.Peek().Target!;return true;}
        var unit=dropLocation?.GetCharacterState();
        if(unit!=null&&AcademicRuntime.Is(unit,"WW_U01")&&effectState.GetTargetTeamType().HasFlag(Team.Type.Heroes))
        {
            var enemy=RedirectTargets.InRoom(unit.GetCurrentRoom(),Team.Type.Heroes).FirstOrDefault();
            if(enemy!=null)overrideTargetCharacter=enemy;
        }
        return true;
    }
}

// 2026-10-10-0005||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Habilidad compartida del Wizard, permiso de Purge por ring y selecciÃ³n de objetivos

public sealed class CardEffectAcademicDiscardForEmber : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    internal static bool Eligible(CardState card)=>(card.GetCardType() is CardType.Spell or CardType.Blight or CardType.Junk)&&
        !card.HasTrait(typeof(CardTraitPersistent))&&!card.HasTrait(typeof(CardTraitInfusion));
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>m.GetCardManager().GetHand().Any(c=>c!=p.playedCard&&Eligible(c));
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;
        yield return AcademicRuntime.ChooseHand(p.playedCard,Eligible,
            new Func<CardState,IEnumerator>(card=>ResolveWithdrawal(card,p,m)));
    }
    private static IEnumerator ResolveWithdrawal(CardState card,CardEffectParams p,ICoreGameManagers m)
    {
        AcademicRuntime.Core=m;
        var manager=m.GetCardManager();
        if(!manager.GetHand().Contains(card)||!Eligible(card))yield break;
        int cost=(int)Math.Min(int.MaxValue,2L*Math.Max(0,SpellRules.Cost(card)));
        yield return manager.DiscardCard(new CardManager.DiscardCardParams {
            discardCard=card,triggeredByCard=true,triggeredCard=p.playedCard,wasPlayed=false });
        if(!manager.GetHand().Contains(card))m.GetPlayerManager().AddEnergy(cost);
    }
}
// 2026-10-10-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Descarte con Ember actual y redirección tras muerte del receptor

[HarmonyPatch(typeof(CombatManager),nameof(CombatManager.GetTargetCharacters))]
internal static class RedirectUiHook
{
    static void Postfix(List<CharacterState> outCharacters,CardState cardState,RoomState room)
    {
        if(cardState==null||room==null||cardState.IsAnyAbility()||cardState.GetCardType()!=CardType.Spell||!cardState.GetEffectStates().Any(e=>e.GetTargetTeamType().HasFlag(Team.Type.Heroes)))return;
        if(RedirectTargets.InRoom(room,Team.Type.Heroes).Count==0)return;
        foreach(var unit in RedirectTargets.InRoom(room,Team.Type.Monsters).Where(u=>AcademicRuntime.Is(u,"WW_U01")))if(!outCharacters.Contains(unit))outCharacters.Add(unit);
    }
}
// 2026-10-10-0011||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Habilitar la selección del receptor en la interfaz de hechizos enemigos
// 2026-10-10-0014||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||No conservar gestores de simulación en las reglas de combate

[HarmonyPatch(typeof(CardState),nameof(CardState.GetCost))]
internal static class FixedCostHook
{
    static bool Prefix(CardState __instance,ref int __result)
    {if(AcademicRuntime.Active&&AcademicRuntime.FixedCosts.TryGetValue(__instance,out int cost)){__result=cost;return false;}return true;}
}
[HarmonyPatch(typeof(CardState),nameof(CardState.IsConsumeRemainingEnergyCostType))]
internal static class FixedXCostHook
{
    static void Postfix(CardState __instance,ref bool __result)
    {if(AcademicRuntime.Active&&AcademicRuntime.FixedCosts.ContainsKey(__instance))__result=false;}
}
// 2026-10-10-0016||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Permitir coste fijo sin consumir todo el Ember de hechizos X

[HarmonyPatch(typeof(CombatManager),nameof(CombatManager.RunUnitTurn))]
internal static class AttackSequenceHook
{
    static void Prefix(CharacterState attackerState,ref Action<int> perAttackVfxCallback,out System.Collections.Generic.List<CardState> __state)
    {
        __state=new();
        if(!AcademicRuntime.Active)return;
        var seen=__state;
        var original=perAttackVfxCallback;
        perAttackVfxCallback=index=>
        {
            AcademicRuntime.BoundForAttack[attackerState]=new();
            if(SpellRules.BoundAttacks.TryGetValue(attackerState,out var bindings))
            {
                foreach(var bound in bindings.ToArray())
                {
                    AcademicRuntime.BoundForAttack[attackerState].Add(bound.card);seen.Add(bound.card);bindings.Remove(bound);
                    if(bound.attacks>1)bindings.Add((bound.card,bound.attacks-1));
                }
                if(bindings.Count==0)SpellRules.BoundAttacks.Remove(attackerState);
            }
            DissertationMarker.Refresh(attackerState);
            original?.Invoke(index);
        };
    }
    static void Postfix(CharacterState attackerState,System.Collections.Generic.List<CardState> __state,ref IEnumerator __result)=>__result=Run(__result,attackerState,__state);
    static IEnumerator Run(IEnumerator original,CharacterState unit,System.Collections.Generic.List<CardState> seen)
    {
        try{yield return original;}
        finally{AcademicRuntime.BoundForAttack.Remove(unit);}
        foreach(var card in seen.Distinct())yield return SpellRules.ReleaseBoundSpell(card);
    }
}
// 2026-10-10-0018||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Contabilizar cada ataque dentro de Sweep y Multistrike usando el callback nativo
// 2026-10-10-0021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Usar el piso real del efecto al reproducir hechizos de área
// 2026-10-10-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Descontar una carga de cada vínculo por ataque completo
// 2026-10-10-0109||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Permitir al receptor objetivos de hechizos de área enemigos, además de objetivos únicos
// 2026-10-10-0111||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||No activar robo o Purge cuando no hay una carta válida o la mano está llena
// 2026-10-10-2358||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Purge compatible con replay y redirección disponible en simulación aislada
// 2026-10-10-2359||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Compartir reconocimiento exacto de rangos y suma de sendas del Wizard
// 2026-10-11-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Bloquear habilidad de robo del Wizard si el motor impide robar del mazo

// 2026-10-11-0019||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Respetar el objetivo explícito de una repetición tras redirigir el hechizo

// 2026-10-11-0038||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Usar robo nativo de Tethys, ambos montones, interfaces de robo y bonos antes de robar

// 2026-10-11-0941||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Separar elegibilidad de simulación en robo/Purge/descarte y ampliar área a efectos enemigos del hechizo elegido

// 2026-10-11-1032||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Reconocer Explosive y Attuned desde mejoras permanentes/temporales de carta y unidad usando lector compartido de sendas

// 2026-10-11-1039||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Guardar Power base del robo para posteriores cambios de Attuned

// 2026-10-11-1503||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Resolver redirección con piso nativo actual en UI, validación y objetivo, evitando Core retenido

// The UI separately checks card-level team flags, even after the receiver was
// added to GetTargetCharacters. Mixed damage/heal spells already pass that gate.
internal static class RedirectTeamScope
{
    internal static readonly Stack<CardState> Cards=new();
}
[HarmonyPatch(typeof(CharacterTargetValidator),nameof(CharacterTargetValidator.IsValidTarget))]
internal static class RedirectTeamValidationHook
{
    static void Prefix(CharacterTargetValidator __instance,RoomState selectedRoom,
        SpawnPointUI selectedSpawnPointUI,out bool __state)
    {
        __state=false;
        var card=__instance.GetActiveCard();
        var unit=selectedSpawnPointUI?.GetParentSpawnPoint()?.GetCharacterState();
        if(card==null||selectedRoom==null||unit==null||unit.IsDead||unit.IsDestroyed||
            card.IsAnyAbility()||card.GetCardType()!=CardType.Spell||!AcademicRuntime.Is(unit,"WW_U01")||
            !card.GetEffectStates().Any(e=>e.GetTargetTeamType().HasFlag(Team.Type.Heroes))||
            RedirectTargets.InRoom(selectedRoom,Team.Type.Heroes).Count==0)return;
        RedirectTeamScope.Cards.Push(card);__state=true;
    }
    // Finalizer runs on success, early returns and exceptions. The team change
    // must never escape this one receiver's UI validation.
    static void Finalizer(bool __state)
    {if(__state)RedirectTeamScope.Cards.Pop();}
}
[HarmonyPatch(typeof(CardState),nameof(CardState.GetTargetedTeamType))]
internal static class RedirectCardTeamHook
{
    static void Postfix(CardState __instance,ref Team.Type __result)
    {
        if(RedirectTeamScope.Cards.Count>0&&RedirectTeamScope.Cards.Peek()==__instance)
            __result|=Team.Type.Monsters;
    }
}

// 2026-10-11-1510||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Permitir bando aliado solo en validación UI sobre becario, conservando filtros nativos y restauración final

// 2026-10-11-1742||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Shared Damages admite ambos bandos en área; conservar redirección exclusiva de enemigos

// 2026-10-12-0111||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Redirección acepta efectos Heroes o Both; filtros de UI, selección, destino y resolución usan flags

// 2026-10-12-0111||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||No duplicar becario en lista nativa de objetivos de hechizos Both

// 2026-10-12-0112||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Exigir enemigo vivo para activar redirección Both y conservar destino aliado normal sin enemigo

// 2026-10-12-1427||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Course Withdrawal descarta carta elegida tras confirmación y concede coste actual solo al salir de mano

public sealed class CardEffectAcademicCopyLastCommon : AcademicEffectBase
{
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>AcademicRuntime.LastPlayedCommonSpell is CardState last&&last.CanBeCopied()&&m.GetCardManager().GetBelowHandSize();
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;
        if(AcademicRuntime.LastPlayedCommonSpell is CardState last)SpellRules.CopyEphemeral(last);
        yield break;
    }
}

// 2026-10-12-1554||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Efecto de Extinguish copia último común; no selecciona unidad ni modifica preview

// 2026-10-12-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Purge y Course Withdrawal resuelven en confirmación; coste descarte Ember conservan orden nativo

// 2026-10-12-1808||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Eliminar bono pendiente de ataque por exceso de curación; conservar vínculos de hechizos

// 2026-10-12-1815||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Expulsion Permit: delegar elegibilidad en máscara nativa sin limitar a hechizos

// 2026-10-12-1815||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Proteger filtro nativo ausente en Expulsion Permit; eliminar advertencias nullable
// 2026-10-12-2232||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Dean siempre añade Explosive al robo; retirar bonos Explosive/Attuned y Power ligados a antiguas sendas
// 2026-10-13-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Actualizar número de marca al descontar cada ataque, cubriendo Sweep y Multistrike
// 2026-10-13-0106||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Liberar hechizos al terminar la secuencia; preservar último lanzamiento y Sweep/Multistrike
// 2026-10-15-2142||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Dean aplica mejora identificada de Explosive para próximo uso, conservando robo nativo y traits previos
// 2026-10-19-2239||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Course Withdrawal admite Spell o Blight y concede el doble del coste actual antes de descartar
// 2026-10-19-2259||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Withdrawal Stamp: primera concesión Purge por ring y posteriores Consume con filtro propio que admite Unpurgable
// 2026-10-19-2359||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Withdrawal Stamp vuelve a un solo Purge por ring; retirar fase Consume
// 2026-10-20-0017||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Identificar copia de Withdrawal Stamp desde equipo del activador y gastar solo su permiso
// 2026-10-21-2245||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/IntegrationEffects.cs||Course Withdrawal acepta Scourge mediante CardType.Junk nativo
