using System.Collections;
using System.Linq;
namespace WwInc;
public sealed class CardEffectAcademicCollectiveAssessment : AcademicEffectBase,ICardEffectUiDialog
{
    public override bool CanPlayAfterBossDead=>false;
    public override bool CanApplyInPreviewMode=>true;
    static bool Eligible(CardState card,ICoreGameManagers managers)=>CollectiveAssessmentTargets.Eligible(card,managers.GetCombatManager());
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>m.GetCardManager().GetHand().Any(c=>c!=p.playedCard&&Eligible(c,m));
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;
        yield return HandSelection.ChooseHand(m,p.playedCard,c=>Eligible(c,m),selected=> {
            SpellRules.AreaTurn[selected]=AcademicRuntime.Turn;
            SpellRules.DamageFactors[selected]=0.5f;
        });
    }
}
// 2026-10-25-1852||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CollectiveAssessment.cs||Separar efecto activo por mecánica con selectores y operaciones nativas; sin despacho por cadena de regla
// 2026-10-25-2009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CollectiveAssessment.cs||Usar selector nativo independiente con managers del efecto; conservar filtros y resolución
// 2026-10-29-1945||codex-ww-inc||salidas/build/ww-inc/src/CollectiveAssessment.cs||Igualar selectores nativos: impedir interfaces nuevas después de morir el jefe y durante transición a recompensas
// 2026-11-07-1058||codex-ww-inc||salidas/build/ww-inc/src/CollectiveAssessment.cs||Rechazar modos de daño no convertibles tanto en TestEffect como al seleccionar
// 2026-11-07-1058||codex-ww-inc||salidas/build/ww-inc/src/CollectiveAssessment.cs||Usar elegibilidad de conversión compartida al validar la habilidad
// 2026-11-07-1058||codex-ww-inc||salidas/build/ww-inc/src/CollectiveAssessment.cs||Usar la misma elegibilidad al abrir y confirmar selección
