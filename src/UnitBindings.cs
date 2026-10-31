using System.Collections;
using System.Linq;
namespace WwInc;
internal static class UnitBindings
{
    internal static IEnumerator AfterRemoval(IEnumerator original,CharacterState unit)
    {
        var bound=SpellRules.BoundAttacks.TryGetValue(unit,out var bindings)
            ?bindings.Select(binding=>binding.card).ToArray():System.Array.Empty<CardState>();
        if(AcademicRuntime.BoundForAttack.TryGetValue(unit,out var active))bound=bound.Concat(active).Distinct().ToArray();
        yield return original;
        SpellRules.BoundAttacks.Remove(unit);
        AcademicRuntime.BoundForAttack.Remove(unit);
        foreach(var spell in bound)yield return SpellRules.ReleaseBoundSpell(spell);
        if(!unit.IsDestroyed)DissertationMarker.Refresh(unit);
    }
}
// 2026-10-27-2203||codex-ww-inc||salidas/build/ww-inc/src/UnitBindings.cs||Liberar vínculos al retirar una instancia de unidad, tanto por muerte como por retorno sin muerte
