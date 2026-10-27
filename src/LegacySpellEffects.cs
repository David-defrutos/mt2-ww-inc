using System;
using System.Collections;
using System.Linq;
namespace WwInc;
// Retired card compatibility only. Active effects have their own implementations.
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
            yield return HandSelection.ChooseHand(m,p.playedCard,c=>c.GetCardType()==CardType.Spell&&c.HasTrait(typeof(CardTraitExhaustState)),selected=> {
                SpellRules.AddRule(selected,"kill_return");
            });
        }
        else throw new InvalidOperationException("Unknown academic spell rule: "+rule);
    }
}
// Only the rules which open a card selector declare the native UI contract.
public sealed class CardEffectAcademicInteractiveRule : CardEffectAcademicSpellRule,ICardEffectUiDialog { }
// 2026-10-25-2137||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LegacySpellEffects.cs||Aislar dispatcher de cartas retiradas; conservar tipos y reglas para compatibilidad
