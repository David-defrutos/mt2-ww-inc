using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace WwInc;

public sealed class CardEffectAcademicConsumeCredits : AcademicEffectBase,ICardEffectUiDialog
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>m.GetCardManager().GetHand().Any(c=>c!=p.playedCard&&c.GetCardType()==CardType.Spell);
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;var unit=p.characterThatActivatedAbility??p.selfTarget;
        if(unit==null)yield break;
        yield return HandSelection.ChooseHand(m,p.playedCard,c=>c.GetCardType()==CardType.Spell,
            new Func<CardState,IEnumerator>(chosen=>ResolveCredits(chosen,unit,m,s,systems)),autoSelectSingleEligible:true);
    }
    internal static IEnumerator ResolveCredits(CardState chosen,CharacterState unit,ICoreGameManagers m,CardEffectState s,ISystemManagers systems)
    {
        AcademicRuntime.Core=m;
        int conduit=(int)Math.Min(int.MaxValue,(long)Math.Max(0,chosen.GetCostWithoutAnyModifications())*Math.Max(0,s.GetParamInt()));
        yield return m.GetCardManager().ConsumeCardWithoutPlaying(new CardManager.DiscardCardParams { discardCard=chosen,overrideDiscardEffect=HandUI.DiscardEffect.Exhausted });
        if(unit.IsDestroyed||unit.IsDead)yield break;
        var copy=new CardEffectState();copy.Setup(s.GetSourceCardEffectData(),unit.GetTeamType(),m.GetSaveManager());
        var status=s.GetParamStatusEffectStackData()[0].Copy();status.count=conduit;
        copy.SetParamStatusEffects(new[]{status});copy.SetParamInt(0); // Native parameter is application chance, not the cost multiplier.
        yield return new CardEffectAddStatusEffect().ApplyEffect(copy,new CardEffectParams {
            playedCard=s.GetParentCardState(),selfTarget=unit,targets=new List<CharacterState>{unit} },m,systems);
    }
}
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/src/ConsumableCredits.cs||Extraer conversión de créditos a efecto dedicado para probar coste, fuente, estado nativo y cola
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/src/ConsumableCredits.cs||Exponer límite de resolución al arnés sin cambiar API visible de cartas
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/src/ConsumableCredits.cs||Saturar coste base por multiplicador sin desbordamiento de Conduit
