using System.Collections;
using System.Linq;
namespace WwInc;
public sealed class CardEffectAcademicCollectiveAssessment : AcademicEffectBase,ICardEffectUiDialog
{
    public override bool CanApplyInPreviewMode=>true;
    static bool Eligible(CardState card)=>card.GetCardType()==CardType.Spell&&card.GetIsDamageCard();
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>m.GetCardManager().GetHand().Any(c=>c!=p.playedCard&&Eligible(c));
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;
        yield return HandSelection.ChooseHand(m,p.playedCard,Eligible,selected=> {
            SpellRules.AreaTurn[selected]=AcademicRuntime.Turn;
            SpellRules.DamageFactors[selected]=0.5f;
        });
    }
}
// 2026-10-25-1852||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CollectiveAssessment.cs||Separar efecto activo por mecánica con selectores y operaciones nativas; sin despacho por cadena de regla
// 2026-10-25-2009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CollectiveAssessment.cs||Usar selector nativo independiente con managers del efecto; conservar filtros y resolución
