using System.Collections;
namespace WwInc;
public sealed class CardEffectAcademicInterdepartmentalMemo : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m;
        SpellRules.Pending.Add(new SpellRules.Rule {Kind="floors",Floor=-1,Unit=p.characterThatActivatedAbility??p.selfTarget});
        yield break;
    }
}
// 2026-10-25-1852||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/InterdepartmentalDamageMemo.cs||Separar efecto activo por mecánica con selectores y operaciones nativas; sin despacho por cadena de regla
// 2026-10-25-1853||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/InterdepartmentalDamageMemo.cs||Conservar explícitamente prevalidación del Memo al separar su efecto dedicado
