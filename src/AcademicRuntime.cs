using MonsterTrain2.SharedTriggers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace WwInc;

internal static class AcademicRuntime
{
    internal static ICoreGameManagers Core = null!;
    internal static int Turn;
    internal static int ConversionDepth;
    internal static int HostileHealingDepth;
    internal static int EquipmentPreviewDepth;
    internal static CardState? LastPlayedCommonSpell;
    internal static readonly Dictionary<CharacterState, int> HealingModes = new();
    internal static readonly Dictionary<CharacterState, int> DamageCredits = new();
    internal static readonly List<Action> EndTurnActions = new();
    internal static readonly List<Func<IEnumerator>> NextTurnActions = new();
    internal static readonly Stack<SpellResolution> Frames = new();
    internal static bool DamageSpellPlayed;
    internal static CardState? FirstDamageSpell;
    internal static readonly HashSet<CharacterState> ZeroAttack = new();
    internal static readonly Dictionary<CharacterState,List<CardState>> BoundForAttack = new();
    internal static readonly Dictionary<CardState,int> FixedCosts = new();
    internal static bool Simulating => Core != null && Core.GetSaveManager().PreviewMode;
    internal static bool Active => Core != null && (!Simulating || PreviewScope.Active);

    internal sealed class SpellResolution
    {
        internal CardState Card = null!;
        internal int Floor;
        internal bool FirstDamage;
        internal bool WasFrozen;
        internal bool Redirected;
        internal bool Indirect;
        internal readonly HashSet<string> Rules = new();
        internal readonly List<Action> Cleanup = new();
        internal CharacterState? Target;
        internal CharacterState? EnemyBehind;
        internal int RepresentativeDamage;
        internal readonly Dictionary<int,int> Damage = new();
        internal readonly Dictionary<int,int> Healing = new();
        internal readonly Dictionary<int,int> Kills = new();
    }
    internal static string Id(CardState? card) => card?.GetAssetName() ?? "";
    internal static bool Is(CharacterState unit, string id) => unit.GetSourceCharacterData().name.EndsWith(id + "_Character", StringComparison.Ordinal);
    internal static List<CharacterState> Units(int floor, Team.Type team = Team.Type.Monsters)
    {
        var result = new List<CharacterState>();
        var room = Core.GetRoomManager().GetRoom(floor);
        if (room != null) room.AddCharactersToList(result, team);
        return result.Where(c => !c.IsDead && !c.IsDestroyed).ToList();
    }
    internal static IEnumerable<CharacterState> AllAllies()
    {
        var all = new List<CharacterState>(); Core.GetMonsterManager().AddCharactersToList(all);
        return all.Where(u => !u.IsDead && !u.IsDestroyed);
    }
    internal static void Reset(CardManager manager)
    {
        Core = ((AllGameManagers)AccessTools.Field(typeof(CardManager), "allGameManagers").GetValue(manager)).GetCoreManagers();
        SocialRules.AuraBonuses.Clear(); ZeroAttack.Clear(); FixedCosts.Clear();BoundForAttack.Clear(); Turn=0; ConversionDepth=0; HostileHealingDepth=0; EquipmentPreviewDepth=0; Frames.Clear(); LastPlayedCommonSpell=null; HealingModes.Clear();SpellRules.Reset();
        MineMineMine.Claimed.Clear();
        DamageCredits.Clear(); EndTurnActions.Clear(); NextTurnActions.Clear();
        DamageSpellPlayed=false; FirstDamageSpell=null;
        CardEffectAcademicPurgePermit.BeginBattle(Core);
    }
    internal static IEnumerator BeginTurn()
    {
        if (Core == null || Core.GetSaveManager().PreviewMode) yield break;
        Turn++; DamageSpellPlayed=false; FirstDamageSpell=null; HealingModes.Clear();
        var pending=NextTurnActions.ToArray(); NextTurnActions.Clear();
        foreach (var action in pending) yield return action();
        yield return SpellRules.BeginTurn();
        yield return SocialRules.BeginTurn();
        SocialRules.RefreshAuras();
        yield return CateringRules.BeginTurn();
    }
    internal static void EndTurn()
    {
        if (!Active) return;
        if (!DamageSpellPlayed && SpellRules.HasRelic("WW_A04")) Core.GetCardManager().AdjustDrawCountModifier(1);
        TurnCleanup.Run(EndTurnActions); HealingModes.Clear(); SpellRules.Pending.RemoveAll(r=>r.Kind=="power");
    }
    internal static void RecordDamage(CharacterState target, int lost, bool died, CardState? card, CharacterState? attacker, int floor)
    {
        if (!Active || ConversionDepth != 0) return;
        if (!Simulating && lost>0 && attacker!=null && Is(attacker,"WW_U18"))
        {
            var credit=AcademicMath.Tuition(DamageCredits.TryGetValue(attacker,out var previous)?previous:0,lost);
            int discounts=credit.discounts; DamageCredits[attacker]=credit.remainder;
            if(discounts>0)
                foreach(var cardInHand in Core.GetCardManager().GetHand().Where(c=>c.HasTrait(typeof(CardTraitFreeze))).ToArray())
                    ModifyCard(cardInHand,0,discounts,false);
        }
        if (Frames.Count==0 || card==null || Frames.Peek().Card!=card) return;
        var frame=Frames.Peek();
        if(target.GetTeamType()!=Team.Type.Heroes) return;
        frame.Damage[floor]=frame.Damage.TryGetValue(floor,out int old)?old+lost:lost;
        if(died) frame.Kills[floor]=frame.Kills.TryGetValue(floor,out old)?old+1:1;
    }
    internal static void RecordHeal(CharacterState target,int restored,int excess,CardState? source)
    {
        if(!Active) return;
        
        if(ConversionDepth!=0)return;
        if(Frames.Count==0 || source==null || Frames.Peek().Card!=source || target.GetTeamType()!=Team.Type.Monsters) return;
        int floor=target.GetCurrentRoomIndex(); var frame=Frames.Peek();
        frame.Healing[floor]=frame.Healing.TryGetValue(floor,out int hp)?hp+restored:restored;
    }
    internal static IEnumerator AfterSpell(SpellResolution frame)
    {
        yield return SpellRules.After(frame);
        bool first=frame.FirstDamage;
        if(first)
        {
            int amount=frame.RepresentativeDamage;
            foreach(var unit in AllAllies().Where(u=>Is(u,"WW_U17")))
            {
                unit.SetAttackDamage(unit.GetAttackDamageWithoutStatusEffectBuffs()+amount);
                EndTurnActions.Add(()=> { if(!unit.IsDestroyed) unit.SetAttackDamage(Math.Max(0,unit.GetAttackDamageWithoutStatusEffectBuffs()-amount)); });
            }
        }
        foreach(var pair in frame.Kills)
            if(pair.Value>0)yield return ArcaneSlayEvent.Dispatch(Core,frame.Card,pair.Key,pair.Value);
        ConversionDepth++;
        try
        {
            if(!frame.Indirect)
            {
                foreach(var pair in frame.Damage)
                    if(AcademicMath.FirstQualifyingSpell(SpellRules.OnceTurn,"U02Damage",pair.Key,pair.Value))
                        foreach(var unit in Units(pair.Key).Where(u=>Is(u,"WW_U02")).ToArray())
                            yield return HealShared(pair.Key,pair.Value,frame.Card);
            }
        }
        finally { ConversionDepth--; }
    }
    internal static IEnumerator HealShared(int floor,int amount,CardState? source)
    {
        var allies=Units(floor).ToArray();
        int healing=AcademicMath.SharedHealing(amount,allies.Length);
        if(healing<=0)yield break;
        foreach(var ally in allies)
            if(!ally.IsDestroyed&&ally.IsAlive)
                yield return ally.ApplyHeal(healing,responsibleCard:source);
    }
    internal static CardUpgradeState ModifyCard(CardState card,int power,int discount,bool thisTurn)
    {
        if(FixedCosts.TryGetValue(card,out int fixedCost)&&discount!=0)FixedCosts[card]=Math.Max(0,fixedCost-discount);
        var upgrade=PowerUpgrade(power,card.GetMagicPowerMultiplierFromTraits());
        upgrade.SetCostReduction(discount);
        var mods=card.GetTemporaryCardStateModifiers(); mods.AddUpgrade(upgrade);
        card.UpdateCardBodyText();
        if(thisTurn) EndTurnActions.Add(()=> { mods.RemoveUpgrade(upgrade); if(FixedCosts.TryGetValue(card,out var cost))FixedCosts[card]=Math.Max(0,cost+discount); card.UpdateCardBodyText(); });
        return upgrade;
    }
    // Native Attuned recalibration needs the unscaled bonuses in source data.
    internal static CardUpgradeState PowerUpgrade(int power,int multiplier=1)
    {
        var upgrade=new CardUpgradeState();upgrade.Setup();
        if(power!=0)
        {
            var source=UnityEngine.ScriptableObject.CreateInstance<CardUpgradeData>();
            AccessTools.Field(typeof(CardUpgradeData),"bonusDamage").SetValue(source,power);
            AccessTools.Field(typeof(CardUpgradeData),"bonusHeal").SetValue(source,power);
            AccessTools.Field(typeof(CardUpgradeState),"sourceCardUpgradeData").SetValue(upgrade,source);
        }
        upgrade.SetAttackDamage(power*multiplier);upgrade.SetAdditionalHeal(power*multiplier);
        return upgrade;
    }
    internal static void SetFixedCost(CardState card,int cost,bool thisTurn)
    {
        bool previous=FixedCosts.TryGetValue(card,out int old);FixedCosts[card]=cost;
        if(thisTurn)EndTurnActions.Add(()=> {if(previous)FixedCosts[card]=old;else FixedCosts.Remove(card);card.UpdateCardBodyText();});
        card.UpdateCardBodyText();
    }
    internal static void Trait(CardState card,string type)
    {
        var trait=new CardTraitData(); trait.Setup(type); Core.GetCardManager().AddTemporaryTraitToCard(card,trait);
    }
    internal static IEnumerator ChooseHand(CardState? source,Func<CardState,bool> eligible,Action<CardState> picked)
        =>ChooseHand(source,eligible,new Func<CardState,IEnumerator>(c=>Complete(c,picked)));
    internal static IEnumerator ChooseHand(CardState? source,Func<CardState,bool> eligible,Func<CardState,IEnumerator> apply)
    {
        var manager=Core.GetCardManager();
        if(!manager.GetHand().Any(c=>c!=source&&eligible(c)))yield break;
        // Native UI input stays locked while the playing effect is unresolved.
        // Open and return; the confirmation callback resolves the selected effect.
        yield return manager.SelectCardFromHand(new HandSelectionUI.Params {
            descriptionKey="david.wwinc.ChooseCard",
            filterCallback=c=>c==source||!eligible(c),
            cardChosenCallback=(c,m)=>ApplyChoice(c,source,eligible,apply,m),disableIneligibleCards=true,
            selectionHandState=CardUI.CardUIState.HandDiscardCard },
            automaticallySelectLastCard:false);
    }
    private static IEnumerator ApplyChoice(CardState card,CardState? source,Func<CardState,bool> eligible,
        Func<CardState,IEnumerator> apply,CardManager manager)
    {
        if(card==source||!manager.GetHand().Contains(card)||!eligible(card))yield break;
        yield return apply(card);
    }
    private static IEnumerator Complete(CardState card,Action<CardState> picked) { picked(card);yield break; }

}
// 2026-10-10-0003||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Correcciones de resoluciÃ³n, traits y acumulaciÃ³n de reglas
// 2026-10-10-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Reglas de Power solo durante el turno y marca de redirección
// 2026-10-10-0014||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Contar daño y muertes de todas las reproducciones antes de repartir bonos
// 2026-10-10-0016||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Costes fijos aplicables también a hechizos X y compatibles con descuentos
// 2026-10-10-0018||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Estado por ataque para Sweep y Multistrike
// 2026-10-10-0020||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Contar umbrales de cincuenta y retirar descuentos temporales de costes fijos
// 2026-10-10-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Permitir varios hechizos vinculados al mismo ataque
// 2026-10-10-0058||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Generar una unidad por puesto al comienzo de cada turno del jugador
// 2026-10-10-2358||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Permitir cálculo aislado de daño y curación; reconstruir Purge al reanudar
// 2026-10-11-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Añadir ámbito de retirada temporal de equipo y limpiarlo por batalla

// 2026-10-11-0941||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Power modifica daño y curación del hechizo

// 2026-10-11-1039||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Escalar Power con Attuned y conservar base para recalibración; retirar cambios temporales en orden inverso

// 2026-10-11-1104||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Acumular daño de Proyecto Aplazado al terminar un turno en mano

// 2026-10-11-1743||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Selector de mano deja de anunciar Freeze para acciones distintas

// 2026-10-12-0923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Activar pasivas U02 U03 después del primer daño o curación positivo por turno y piso, sin bucles

// 2026-10-12-1427||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Esperar cierre nativo del selector después de confirmar; desactivar autoelección que omite filtros

// 2026-10-12-1438||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Sustituir historial por piso por último hechizo global reiniciado al iniciar batalla

// 2026-10-12-1543||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Registrar y copiar solo último hechizo Common global; posteriores rarezas no sustituyen el registro

// 2026-10-12-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Selector nativo sin espera bloqueante; resolver efecto completo dentro del callback de confirmación

// 2026-10-12-1712||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Eliminar escaneo manual de Reserve; lo dispara el trigger nativo

// 2026-10-12-1808||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Otorgar Armor inmediata al exceso de Emeritus y retirar acumuladores de daño obsoletos

// 2026-10-12-2053||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Cambiar conversión U03 de primer hechizo y trample a exceso propio y daño nativo a cada enemigo

// 2026-10-12-2056||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Repartir curación PhD con ApplyHeal nativo; permitir excedente a Hostile sin ciclos de daño/curación

// 2026-10-12-2200||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Despachar Arcane Slay por piso y baja mediante cola nativa a cualquier unidad con trigger
// 2026-10-12-2211||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Retirar efectos de exceso ligados a IDs U03/U13; usar trigger reutilizable Overheal
// 2026-10-13-2138||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Despachar Arcane Slay conservando hechizo causante y conteo de bajas por piso
// 2026-10-13-2353||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Tuition pasiva independiente: cada 30 de daño descuenta todas las cartas Frozen actualmente en mano
// 2026-10-24-1839||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Reiniciar origen de hechizos generados al comenzar/reconstruir batalla
// 2026-10-24-1923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Conservar autoría entre rings; reinicio y carga quedan a cargo de Conductor ISaveData
// 2026-10-24-1930||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Limpiar recibos de bajas reclamadas al configurar cada batalla
// 2026-10-25-1605||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Usar daño total independiente para U17 sin cambiar daño efectivo para PhD y otras conversiones
// 2026-10-25-1640||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Configurar estado nativo de selección para marcar y oscurecer cartas no elegibles
// 2026-10-25-1849||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicRuntime.cs||Conectar consumidores al núcleo independiente con managers explícitos y guardias W&W conservadas
