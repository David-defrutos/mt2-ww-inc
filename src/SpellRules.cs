using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace WwInc;

internal static class SpellRules
{
    internal sealed class Rule
    {
        internal string Kind="";
        internal int Floor=-1;
        internal CharacterState? Unit;
    }
    internal static readonly HashSet<CardState> ReservedCards=new();
    internal static readonly Dictionary<DeckScreen,List<CardState>> PileSelections=new();
    internal static readonly List<Rule> Pending=new();
    internal static readonly Dictionary<CardState,HashSet<string>> CardRules=new();
    internal static readonly Dictionary<CardState,int> PlayedKills=new();
    internal static readonly Dictionary<CharacterState,List<(CardState card,int attacks)>> BoundAttacks=new();
    internal static readonly Dictionary<CardState,Team.Type> BoundSides=new();
    internal static readonly Dictionary<CharacterState,List<CardState>> Archived=new();
    internal static readonly List<(CardState card,int turn,bool zero)> Deferred=new();
    internal static readonly HashSet<string> OnceTurn=new();
    internal static int TuitionRefundCount;
    internal static readonly Dictionary<CardState,float> DamageFactors=new();
    internal static readonly Dictionary<CardState,int> AreaTurn=new();
    internal static int HealingAreaTurn=-1;
    internal static CardUpgradeData HitDiscountUpgrade=null!;
    internal static void ApplyHitDiscount(CardState card)
    {
        var core=AcademicRuntime.Core;
        if(!CardUpgradeHelper.TryApplyUpgradeToCard(card,null,HitDiscountUpgrade,true,false,core,out _))return;
        if(AcademicRuntime.FixedCosts.TryGetValue(card,out int cost))
        {
            int applied=Math.Min(1,Math.Max(0,cost));
            AcademicRuntime.FixedCosts[card]=cost-applied;
        }
        card.UpdateCardBodyText();core.GetCardManager().RefreshCardInHand(card);
    }

    internal static void Reset()
    {
        ReservedCards.Clear();PileSelections.Clear();Pending.Clear();CardRules.Clear();PlayedKills.Clear();BoundAttacks.Clear();BoundSides.Clear();Archived.Clear();Deferred.Clear();
        OnceTurn.Clear();TuitionRefundCount=0;DamageFactors.Clear();AreaTurn.Clear();HealingAreaTurn=-1;EffectFloor=-1;
    }
    internal static bool HasRelic(string id)=>AcademicRuntime.Core.GetRelicManager()
        .GetRelicEffects(new List<RelicEffectAcademic>()).Any(r=>r.SourceRelicData.name.EndsWith(id,StringComparison.Ordinal));
    internal static void AddRule(CardState card,string rule)
    { if(!CardRules.TryGetValue(card,out var rules)) CardRules[card]=rules=new(); rules.Add(rule); }
    internal static bool HasRule(CardState card,string rule)=>CardRules.TryGetValue(card,out var rules)&&rules.Contains(rule);
    internal static void RemoveRule(CardState card,string rule)
    { if(CardRules.TryGetValue(card,out var rules)){ rules.Remove(rule);if(rules.Count==0)CardRules.Remove(card); } }
    internal static void ResolutionPower(AcademicRuntime.SpellResolution frame,int power)
    {
        if(power<=0)return;
        var upgrade=AcademicRuntime.ModifyCard(frame.Card,power,0,false);
        frame.Cleanup.Add(()=> {frame.Card.GetTemporaryCardStateModifiers().RemoveUpgrade(upgrade);frame.Card.UpdateCardBodyText();});
    }
    // Match native healing effects plus Conductor's shared extension registry.
    internal static bool HealingEffect(CardEffectState effect)
        =>effect.GetCardEffect() is CardEffectHeal||
          Conductor.RoomModifiers.RoomStateHealSpellCostModifier.OtherHealingCardEffects.Contains(effect.GetCardEffect().GetType());
    internal static bool IsHealingSpell(CardState card)
        =>card.GetCardType()==CardType.Spell&&!card.IsUnitAbility()&&
          (card.GetEffectStates().Any(HealingEffect)||card.GetTriggers().Where(t=>t.GetTrigger()==CardTriggerType.OnCast)
            .Any(t=>t.GetCardEffectParams().Any(HealingEffect)));
    internal static bool TargetsEnemy(CharacterState? target)=>target!=null&&target.GetTeamType()==Team.Type.Heroes;
    internal static bool IsAreaEffect(CardState card,CardEffectState effect)
        =>AreaTurn.TryGetValue(card,out int turn)&&turn==AcademicRuntime.Turn&&
          effect.GetTargetMode()==TargetMode.DropTargetCharacter&&effect.GetTargetTeamType().HasFlag(Team.Type.Heroes);
    internal static bool MonoEnemy(CardState card)
    {
        if(AreaTurn.TryGetValue(card,out int turn)&&turn==AcademicRuntime.Turn)return false;
        var enemyEffects=card.GetEffectStates().Where(e=>e.GetTargetTeamType().HasFlag(Team.Type.Heroes)).ToArray();
        return enemyEffects.Length>0&&enemyEffects.All(e=>e.GetTargetMode()==TargetMode.DropTargetCharacter);
    }
    internal static List<CardState> RecoveryCandidates(CardManager manager,CardState? played)
        =>manager.GetDiscardPile().Concat(manager.GetDrawPile())
            .Where(c=>c!=played&&c.GetCardType()==CardType.Spell).Distinct().ToList();
    internal static List<CardState> ArchiveCandidates(CardManager manager,CardState? played)
        =>RecoveryCandidates(manager,played).Concat(manager.GetExhaustedPile())
            .Where(c=>c!=played&&c.GetCardType()==CardType.Spell).Distinct().ToList();
    internal static int Cost(CardState card)=>card.GetCost(AcademicRuntime.Core.GetCardStatistics(),AcademicRuntime.Core.GetMonsterManager(),AcademicRuntime.Core.GetRelicManager());

    internal static IEnumerator SelectPile(ISystemManagers systems,TargetMode pile,Func<CardState,bool> eligible,Action<CardState> picked,bool cancel=true,List<CardState>? candidates=null,string instructions="david.wwinc.ChooseSpell")
    {
        bool done=false; DeckScreen? selectedDeck=null;var screenManager=systems.GetScreenManager();
        try
        {
            screenManager.SetScreenActive(ScreenName.Deck,true,screen=> {
                if(!(screen is DeckScreen deck)) { done=true; return; }
                selectedDeck=deck;
                if(candidates!=null)PileSelections[deck]=candidates.Where(eligible).ToList();
                deck.Setup(new DeckScreen.Params { mode=DeckScreen.Mode.CardEffectSelection,targetMode=pile,
                    cardTypeFilter=CardType.Spell,showCancel=cancel,excludeFilteredOutCards=true,
                    filterFunc=c=>!eligible(c),instructionsKey=instructions,includeTemporaryCardsInBattle=true });
                deck.AddDeckScreenCancelCallback(()=>done=true);
                deck.AddDeckScreenCardStateChosenDelegate(c=> { if(c!=null&&eligible(c)) picked(c);done=true;screenManager.SetScreenActive(ScreenName.Deck,false); });
            });
            while(!done) yield return null;
        }
        finally { if(selectedDeck!=null)PileSelections.Remove(selectedDeck); }
    }
    internal static void Apart(CardState card,int turn,bool zero=false)
    {
        AcademicRuntime.Core.GetCardManager().MoveToStandByPile(card,false,false);
        ReservedCards.Add(card);Deferred.Add((card,turn,zero));
    }
    // Generate a battle-only copy; the source stays in its current pile and keeps its traits/cost.
    internal static void CopyEphemeral(CardState source,CardUpgradeData? copyUpgrade)
    {
        if(copyUpgrade==null)throw new InvalidOperationException("Missing configured copy traits");
        if(!source.CanBeCopied())return;
        var cm=AcademicRuntime.Core.GetCardManager();
        var copy=cm.AddNewCardCopy(AcademicRuntime.Core.GetAllGameData().FindCardData(source.GetCardDataID()),
            source,CardPile.HandPile,false,new CardManager.AddCardUpgradingInfo {
                tempCardUpgrade=true,upgradingCardSource=source,ignoreTempUpgrades=false,copyModifiersFromCard=source });
        if(copy==null)return;
        AcademicRuntime.SetFixedCost(copy,0,false);
        GrantConfiguredTraits(copy,copyUpgrade);
    }
    // Native standby return preserves pile signals and relic hooks.
    internal static IEnumerator ReleaseBoundSpell(CardState card)
    {
        if(AcademicRuntime.Simulating||!ReservedCards.Contains(card)||
            BoundAttacks.Values.Any(bindings=>bindings.Any(bound=>bound.card==card)))yield break;
        var cm=AcademicRuntime.Core.GetCardManager();
        if(cm.IsCardInStandByPile(card))
        {
            cm.ChangeStandbyCondition(card,new RemoveFromStandByCondition(()=>CardPile.DiscardPile));
            yield return (IEnumerator)AccessTools.Method(typeof(CardManager),"CheckStandByConditions")
                .Invoke(cm,new object?[] {null,card});
        }
        if(!cm.IsCardInStandByPile(card)){ReservedCards.Remove(card);BoundSides.Remove(card);}
    }
    internal static IEnumerator Restore(CardState card,bool ephemeral,bool attuned,bool zero=true,bool attunedThisTurn=false)
    {
        ReservedCards.Remove(card);
        var cm=AcademicRuntime.Core.GetCardManager();
        if(cm.GetExhaustedPile().Contains(card)) cm.RestoreExhaustedOrEatenCard(card);
        ((System.Collections.IDictionary)AccessTools.Field(typeof(CardManager),"pileStandBy").GetValue(cm)).Remove(card);
        if(zero)AcademicRuntime.SetFixedCost(card,0,true);
        if(ephemeral) AcademicRuntime.Trait(card,"CardTraitEphemeral");
        if(attuned) Attuned(card,attunedThisTurn);
        if(cm.GetHand().Contains(card))yield break;
        if(!cm.GetBelowHandSize())
        {cm.GetDiscardPile().Remove(card);if(!cm.GetDrawPile().Contains(card))cm.GetDrawPile().Add(card);}
        else cm.DrawSpecificCard(card,drawSource:HandUI.DrawSource.Consume);
        yield break;
    }
    internal static void Attuned(CardState card,bool thisTurn=false)
    {
        var trait=new CardTraitData();trait.Setup("CardTraitStrongerMagicPower");
        if(card.HasTrait(typeof(CardTraitStrongerMagicPower)))return;
        AcademicRuntime.Core.GetCardManager().AddTemporaryTraitToCard(card,trait);
        var added=card.GetTemporaryCardStateModifiers().GetTemporaryTraits().Last(t=>t is CardTraitStrongerMagicPower);
        card.ReapplyMagicPowerScalingFromTraitsToAllExistingUpgrades(AcademicRuntime.Core);
        card.UpdateCardBodyText();
        if(thisTurn)AcademicRuntime.EndTurnActions.Add(()=>
        {
            card.RemoveTemporaryTrait(added);
            card.ReapplyMagicPowerScalingFromTraitsToAllExistingUpgrades(AcademicRuntime.Core);
            card.UpdateCardBodyText();
        });
    }
    internal static void GrantConfiguredTraits(CardState card,CardUpgradeData? data)
    {
        if(data==null)throw new InvalidOperationException("Missing configured granted traits");
        var sourceTraits=data.GetTraitDataUpgrades();
        if(sourceTraits.Count>0&&sourceTraits.All(t=>card.HasTrait(ShinyShoe.TypeNameCache.GetType(t.GetTraitStateName()))))return;
        var upgrade=new CardUpgradeState();upgrade.Setup(data);upgrade.SetAvoidClobberingExistingTraits(true);
        CardUpgradeHelper.TryApplyUpgradeToCard(card,null,upgrade,isTemporaryUpgrade:true,AcademicRuntime.Core);
    }
    internal static void Explosive(CardState card)
    {
        if(card.HasTrait(typeof(CardTraitDamageOverflow)))return;
        var trait=new CardTraitData();trait.Setup("CardTraitDamageOverflow");
        AccessTools.Field(typeof(CardTraitData),"paramInt").SetValue(trait,2);
        AcademicRuntime.Core.GetCardManager().AddTemporaryTraitToCard(card,trait);
    }
    internal static IEnumerator BeginTurn()
    {
        OnceTurn.Clear();
        foreach(var pair in Deferred.Where(x=>x.turn<=AcademicRuntime.Turn).ToArray())
        { Deferred.Remove(pair); yield return Restore(pair.card,false,false,pair.zero); }
        AreaTurn.Clear();DamageFactors.Clear();
    }
    internal static void ApplyPairKeywords(AcademicRuntime.SpellResolution frame)
    {
        var card=frame.Card;
        var previous=card.GetTemporaryCardStateModifiers().GetTemporaryTraits().ToArray();
        try
        {
            Explosive(card);
            Attuned(card);
            card.UpdateCardBodyText();
        }
        finally
        {
            // Snapshot only this call's new instances, never pre-existing traits.
            var added=card.GetTemporaryCardStateModifiers().GetTemporaryTraits().Except(previous).ToArray();
            if(added.Length>0)frame.Cleanup.Add(()=> {
                foreach(var trait in added)card.RemoveTemporaryTrait(trait);
                if(added.Any(t=>t is CardTraitStrongerMagicPower))
                    card.ReapplyMagicPowerScalingFromTraitsToAllExistingUpgrades(AcademicRuntime.Core);
                card.UpdateCardBodyText();
            });
        }
    }
    internal static int EducationPowerOnFloor(int floor)
    {
        var units=AcademicRuntime.Units(floor);
        return AcademicMath.EducationPower(units.FirstOrDefault()?.GetAttackDamage()??0,
            units.Count(u=>AcademicRuntime.Is(u,"WW_U04")));
    }
    internal static IEnumerator Before(AcademicRuntime.SpellResolution frame,CharacterState? target)
    {
        var card=frame.Card; var units=AcademicRuntime.Units(frame.Floor);
        if(AcademicRuntime.Id(card).EndsWith("WW_S32",StringComparison.Ordinal)&&units.Zip(units.Skip(1),SocialRules.Mixed).Any(mixed=>mixed))
            ApplyPairKeywords(frame);
        bool firstSpellOnFloor=!frame.Indirect&&OnceTurn.Add("FirstSpell:"+frame.Floor);
        if(firstSpellOnFloor)
            ResolutionPower(frame,EducationPowerOnFloor(frame.Floor));
        if(card.GetIsDamageCard() && HasRelic("WW_A01") && OnceTurn.Add("A01"))
            ResolutionPower(frame,(units.FirstOrDefault()?.GetAttackDamage()??0)/2);
        foreach(var rule in Pending.ToArray())
        {
            if(rule.Floor>=0&&rule.Floor!=frame.Floor) continue;
            if(rule.Kind=="power"&&!card.GetIsDamageCard())continue;
            if(rule.Kind=="double"&&!card.GetIsDamageCard())continue;
            if(rule.Kind=="floors"&&(!TargetsEnemy(target)||!MonoEnemy(card)))continue;
            Pending.Remove(rule);
            if(rule.Kind=="power") ResolutionPower(frame,(units.FirstOrDefault()?.GetAttackDamage()??0)/2);
            else if(rule.Kind=="double") { float previous=DamageFactors.TryGetValue(card,out var f)?f:1; DamageFactors[card]=previous*2;frame.Cleanup.Add(()=> { if(previous==1)DamageFactors.Remove(card);else DamageFactors[card]=previous; });AddRule(card,"defer"); }
            else if(rule.Kind=="floors") frame.Rules.Add("floors");
        }
        if(!frame.Indirect&&IsHealingSpell(card)&&HasRelic("WW_A10")&&OnceTurn.Add("A10"))
            frame.Rules.Add("offensive_medicine");
        if(TargetsEnemy(target)&&MonoEnemy(card)&&HasRelic("WW_A06")&&OnceTurn.Add("A06")) frame.Rules.Add("behind");
        if(card.HasTrait(typeof(CardTraitExhaustState))&&HasRelic("WW_A02")&&OnceTurn.Add("A02")) AddRule(card,"spellchain");
        frame.Target=target;
        if(target!=null)
        {
            var enemies=AcademicRuntime.Units(frame.Floor,Team.Type.Heroes);int index=enemies.IndexOf(target);
            if(index>=0&&index+1<enemies.Count)frame.EnemyBehind=enemies[index+1];
        }
        yield break;
    }
    internal static IEnumerator After(AcademicRuntime.SpellResolution frame)
    {
        var core=AcademicRuntime.Core; var card=frame.Card;
        if(frame.Rules.Count>0)
        {
            if(frame.Rules.Contains("floors"))
                foreach(int floor in SocialRules.FloorsBottomUp())
                { if(floor==frame.Floor) continue; var enemy=AcademicRuntime.Units(floor,Team.Type.Heroes).FirstOrDefault();if(enemy!=null)yield return Repeat(card,floor,enemy,frame); }
            if(frame.Rules.Contains("behind")&&frame.Target!=null)
            {
                var enemies=AcademicRuntime.Units(frame.Floor,Team.Type.Heroes);
                var behind=frame.EnemyBehind;
                if(behind!=null&&!behind.IsDead&&!behind.IsDestroyed) yield return Repeat(card,frame.Floor,behind,frame);
            }
        }
        int kills=frame.Kills.Values.Sum();PlayedKills[card]=kills;
        if(!frame.Indirect&&kills>0&&!AcademicRuntime.Simulating)
            foreach(var relic in core.GetRelicManager().GetRelicEffects(new List<RelicEffectAcademic>()))
                relic.RecordKillingSpell(card,kills,core);
        if(frame.Rules.Contains("offensive_medicine")||HealingAreaTurn==AcademicRuntime.Turn)
        {
            AcademicRuntime.ConversionDepth++;
            try {
                foreach(var pair in frame.Healing)
                    foreach(var enemy in AcademicRuntime.Units(pair.Key,Team.Type.Heroes))
                        yield return core.GetCombatManager().ApplyDamageToTarget(pair.Value,enemy,new CombatManager.ApplyDamageToTargetParameters{playedCard=card});
            } finally {AcademicRuntime.ConversionDepth--;}
        }

    }
    internal static int EffectFloor=-1;
    internal static IEnumerator Repeat(CardState card,int floor,CharacterState target,AcademicRuntime.SpellResolution? aggregate=null)
    {
        var frame=aggregate??new AcademicRuntime.SpellResolution {Card=card,Floor=floor,Target=target,Indirect=true};
        int previousFloor=EffectFloor;EffectFloor=floor;AcademicRuntime.Frames.Push(frame);
        try { yield return AcademicRuntime.Core.GetCombatManager().ApplyEffects(card.GetEffectStates(),floor,card,selfTarget:target,overrideTargetCharacter:target); }
        finally { AcademicRuntime.Frames.Pop();EffectFloor=previousFloor; }
        if(aggregate==null)yield return AcademicRuntime.AfterSpell(frame);
    }
}

public sealed class RelicEffectAcademic : RelicEffectBase
{
    public override PropDescriptions CreateEditorInspectorDescriptions()=>PropDescriptions.DisplayNone;
    bool IsTuitionRefund=>SourceRelicData!=null&&SourceRelicData.name.EndsWith("WW_A05",StringComparison.Ordinal);
    internal void RecordKillingSpell(CardState card,int kills,ICoreGameManagers managers)
    {
        if(!IsTuitionRefund||managers.GetSaveManager().PreviewMode)return;
        var progress=AcademicMath.RefundProgress(SpellRules.TuitionRefundCount,kills);
        SpellRules.TuitionRefundCount=progress.count;
        if(!progress.refund)return;
        managers.GetPlayerManager().AddEnergy(Math.Max(0,card.GetLastPlayedCost()));
        NotifyRelicTriggeredScreenCenter(managers.GetRelicManager());
    }
    public override void AppendConditionCountDisplay(System.Text.StringBuilder builder,CardStatistics statistics)
    {
        if(!IsTuitionRefund){base.AppendConditionCountDisplay(builder,statistics);return;}
        builder.Append(string.Format("TextFormat_Divide".Localize(),SpellRules.TuitionRefundCount,5));
    }
}
public class CardEffectAcademicSpellRule : AcademicEffectBase
{
    // Only rules without player choices or card-pile changes may simulate.
    static bool CanPreview(string rule)=>rule is "floors" or "double" or "power" or "healing_area";
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
    {
        string rule=s.GetParamStr();
        var cm=m.GetCardManager();
        if(rule=="defer_hand")return cm.GetHand().Any(c=>c!=p.playedCard&&c.GetCardType()==CardType.Spell);
        if(rule=="recover_x")return SpellRules.RecoveryCandidates(cm,p.playedCard).Count>0;
        if(rule=="archive")return SpellRules.ArchiveCandidates(cm,p.playedCard).Count>0;
        if(rule=="kill_return")return cm.GetHand().Any(c=>c!=p.playedCard&&c.GetCardType()==CardType.Spell&&c.HasTrait(typeof(CardTraitExhaustState)));
        return true;
    }
    public override bool CanApplyInPreviewMode=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        string rule=s.GetParamStr();
        if(m.GetSaveManager().PreviewMode&&!CanPreview(rule))yield break;
        AcademicRuntime.Core=m;var unit=p.characterThatActivatedAbility??p.selfTarget;
        if(rule=="floors"||rule=="double"||rule=="power")
            SpellRules.Pending.Add(new SpellRules.Rule {Kind=rule,Floor=rule=="power"?unit!.GetCurrentRoomIndex():-1,Unit=unit});
        else if(rule=="healing_area") SpellRules.HealingAreaTurn=AcademicRuntime.Turn;
        else if(rule=="defer_hand")
        {
            for(int i=0;i<3;i++)
            {
                CardState? selected=null;
                yield return SpellRules.SelectPile(systems,TargetMode.Hand,c=>c!=p.playedCard,c=>selected=c);
                if(selected==null)break;SpellRules.Apart(selected,AcademicRuntime.Turn+1,true);
            }
        }
        else if(rule=="recover_x")
        {
            for(int i=0;i<(p.playedCard?.GetLastPlayedCost()??0);i++)
            {
                CardState? selected=null;
                var candidates=SpellRules.RecoveryCandidates(m.GetCardManager(),p.playedCard);
                if(candidates.Count==0)break;
                // Native Self + includeTemporaryCardsInBattle exposes hand, draw
                // and discard. The exact candidate filter excludes hand and all
                // other piles while allowing one combined selection screen.
                yield return SpellRules.SelectPile(systems,TargetMode.Self,c=>candidates.Contains(c),c=>selected=c);
                if(selected==null)break;
                yield return SpellRules.Restore(selected,false,true);AcademicRuntime.Trait(selected,"CardTraitExhaustState");
            }
        }
        else if(rule=="archive")
        {
            CardState? selected=null;var manager=m.GetCardManager();
            var candidates=SpellRules.ArchiveCandidates(manager,p.playedCard);
            if(candidates.Count==0||unit==null)yield break;
            yield return SpellRules.SelectPile(systems,TargetMode.Self,c=>candidates.Contains(c),c=>selected=c,candidates:candidates);
            if(selected!=null)
            {
                // Consumed spells also have stand-by bookkeeping. Native restore
                // removes both entries before the spell is reserved again.
                if(manager.GetExhaustedPile().Contains(selected))manager.RestoreExhaustedOrEatenCard(selected);
                manager.MoveToStandByPile(selected,false,false);SpellRules.ReservedCards.Add(selected);
                if(!SpellRules.Archived.TryGetValue(unit,out var list))SpellRules.Archived[unit]=list=new();
                list.Add(selected);
            }
        }
        else if(rule=="kill_return")
        {
            yield return AcademicRuntime.ChooseHand(p.playedCard,c=>c.GetCardType()==CardType.Spell&&c.HasTrait(typeof(CardTraitExhaustState)),selected=> {
                SpellRules.AddRule(selected,"kill_return");
            });
        }
        else throw new InvalidOperationException("Unknown academic spell rule: "+rule);
    }
}
// 2026-10-10-0003||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Correcciones de resoluciÃ³n, traits y acumulaciÃ³n de reglas
// 2026-10-10-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Duración de Attuned, coste al regresar y liberación real de cartas apartadas
// 2026-10-10-0014||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Agregación de reproducciones del hechizo y contexto correcto del piso
// 2026-10-10-0016||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Coste cero real al recuperar cartas, incluidos hechizos X
// 2026-10-10-0020||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Reproducir solo en los otros pisos de combate y usar el vecino original
// 2026-10-10-0021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Conservar todos los hechizos archivados y evitar perder cartas al recuperar con la mano llena
// 2026-10-10-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Acumular vínculos de varias copias de S02 sin perder hechizos
// 2026-10-10-0111||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Evitar gastar hechizos de selección cuando no hay cartas elegibles
// 2026-10-10-2358||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Evitar robo real durante previsualización y limpiar contexto del piso
// 2026-10-11-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||No insertar de nuevo hechizos recuperados que ya estén en la mano
// 2026-10-11-0010||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Comprobar directamente la mano real antes de volver a insertar la carta

// 2026-10-11-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Simular cinco reglas sin selección y bloquear selectores/pilas en TestEffect y ApplyEffect

// 2026-10-11-0941||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||No bloquear validación por preview; distinguir objetivo único de área y restringir selección a hechizos

// 2026-10-11-0945||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Elegibilidad de S05 exige hechizo Consume, igual que el selector

// 2026-10-11-1039||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Attuned recalibra mejoras al añadir y retirar solo su instancia; impedir duplicar Explosive

// 2026-10-11-1552||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Recuperar hasta X hechizos de descarte y robo en un selector conjunto; excluir mano y carta jugada

// 2026-10-11-1618||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Retirar regla draw_full y estado BottomDeckTurn sustituidos por robo nativo

// 2026-10-11-1622||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Retornos aplazados S06 y S10 dejan de añadir Ephemeral; conservar coste cero

[HarmonyPatch(typeof(DeckScreen),"CollectCardsForSelection")]
internal static class AcademicPileSelectionHook
{
    static void Postfix(DeckScreen __instance,ref List<CardState> __result)
    {
        if(SpellRules.PileSelections.TryGetValue(__instance,out var cards))__result=new List<CardState>(cards);
    }
}

// 2026-10-11-1631||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Archivera busca tres pilas y restaura consumidos antes de apartar; selector acotado por DeckScreen y limpieza finally

// 2026-10-11-1637||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Rastrear cartas realmente apartadas por archivo, vínculo o aplazamiento; retirar marca al retornar y limpiar batalla

// 2026-10-11-1742||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Repeticiones S01/A06 requieren objetivo enemigo real; área reconoce flags de ambos bandos

// 2026-10-11-1743||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Selector de hechizos apartados y recuperados usa mensaje genérico localizado

// 2026-10-11-1816||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Crear copia temporal con mejoras originales; aplicar coste 0 y Ephemeral exclusivamente a copia

// 2026-10-12-0053||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||No crear mejora temporal ni actualizar texto si el bono de Chair of Magical Strength es cero

// 2026-10-12-0915||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Aplicar bono de Educación al primer hechizo por piso y turno sin activación manual

// 2026-10-12-0918||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Registrar primer hechizo del piso incluso sin Profesor para no activar pasiva tarde al invocarlo

// 2026-10-12-0946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Aplicar Explosive y Attuned S32 antes del daño por pareja adyacente y restaurar traits de preview

// 2026-10-12-0947||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Corregir identificación S32 por CardState Id en lugar de helper de personajes

// 2026-10-12-1430||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||S32 keywords solo durante resolución real y preview; retirar solo nuevas instancias conservando traits previos

// 2026-10-12-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Reglas de selección de mano aplican vínculos área y retorno dentro de confirmación sin leer selección prematura

// 2026-10-12-2103||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Validar y seleccionar S02 con máscara OnlySpells y consulta nativa de equipo enemigo, sin filtro damage
// 2026-10-12-2255||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Aplicar mejora de coste nativa y retirarla por ID hasta jugar; preservar otros descuentos y copias
// 2026-10-12-2300||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Reliquia A10: reservar primera curación de turno y reutilizar daño nativo por salud restaurada
// 2026-10-12-2301||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Clasificar curaciones como Conductor: CardEffectHeal/subclases, registro compartido y triggers OnCast
// 2026-10-12-2322||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Scholarship: conservar mejora temporal toda la batalla y quitar retirada al jugar
// 2026-10-13-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Mostrar marca Dissertation al vincular el hechizo
// 2026-10-13-0031||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Sustituir flag invisible HitDiscounts por estado nativo Scholarship concedido desde JSON
// 2026-10-13-0106||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Devolver vínculo terminado al descarte con coroutine nativa; conservar original y excluir simulación
// 2026-10-13-2257||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Postdoc: copia mantiene coste 0 entre turnos; no registrar limpieza de fin de turno
// 2026-10-15-2226||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Compartir cálculo del bono Education entre resolución y texto de mano
// 2026-10-17-0910||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||A05 cuenta hechizos letales en ciclos de cinco y expone contador nativo 0/5; elimina carga única
// 2026-10-24-2100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Ampliar selección de Dissertation, guardar bando por vínculo y reutilizar selector de pilas con instrucciones
// 2026-10-25-1616||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Aplicar rasgos declarados con helper nativo preservando existentes en Postdoc y Conduit Transfer
// 2026-10-25-1617||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Resolver TypeNameCache nativo y validar referencias de mejoras antes de mutar copias

// Only the rules which open a card selector declare the native UI contract.
public sealed class CardEffectAcademicInteractiveRule : CardEffectAcademicSpellRule,ICardEffectUiDialog { }
// 2026-10-25-1640||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Separar contrato de reglas interactivas de reglas sin diálogos manteniendo resolución existente
// 2026-10-25-1832||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Pasar el CombatManager del caller al filtro nativo de aleatoriedad sin depender de contexto global
// 2026-10-25-1852||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SpellRules.cs||Retirar ramas activas movidas a efectos dedicados; conservar compatibilidad de cartas retiradas
