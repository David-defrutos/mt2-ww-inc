using System;
using System.Collections;
using System.Linq;
namespace WwInc;
// Card-specific adapter: native hand selection, native mask and shared targeting policy.
public sealed class CardEffectAcademicDissertation : AcademicEffectBase,ICardEffectUiDialog
{
    public override bool CanApplyInPreviewMode=>true;
    private static bool EligibleBoundSpell(CardState card,CardEffectState effect,ICoreGameManagers managers)
        =>effect.GetParamCardFilter() is { } filter&&filter.FilterCard(card,managers.GetRelicManager())&&
          DissertationTargeting.CanBind(card,managers.GetCombatManager());
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>m.GetCardManager().GetHand().Any(c=>c!=p.playedCard&&EligibleBoundSpell(c,s,m));
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;
        var target=p.targets.FirstOrDefault();if(target==null)yield break;
        yield return HandSelection.ChooseHand(m,p.playedCard,c=>EligibleBoundSpell(c,s,m),
            new Func<CardState,IEnumerator>(selected=>DissertationTargeting.Bind(selected,target,m,systems)));
    }
}
// 2026-10-25-1852||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/EnchantedDissertation.cs||Separar efecto activo por mecánica con selectores y operaciones nativas; sin despacho por cadena de regla
// 2026-10-25-2009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/EnchantedDissertation.cs||Usar selector nativo independiente con managers del efecto; conservar filtros y resolución
