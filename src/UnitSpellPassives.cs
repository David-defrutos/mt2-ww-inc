using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace WwInc;

// Card-specific consumers. Native unit data identifies carriers; the caller owns spell timing.
internal static class UnitSpellPassives
{
    internal static CharacterData Representative=null!,FirstAid=null!,Education=null!;

    internal static List<CharacterState> Units(ICoreGameManagers core,int floor)
    {
        var units=new List<CharacterState>();
        core.GetRoomManager().GetRoom(floor)?.AddCharactersToList(units,Team.Type.Monsters);
        return units.Where(unit=>!unit.IsDead&&!unit.IsDestroyed).ToList();
    }

    internal static void ApplyRepresentative(IEnumerable<CharacterState> allies,int damage,List<Action> cleanup)
    {
        foreach(var unit in allies.Where(unit=>unit.GetSourceCharacterData()==Representative))
        {
            if(AcademicRuntime.Simulating&&!unit.PreviewMode&&!unit.TemporaryPreviewsEnabled)continue;
            unit.SetAttackDamage(unit.GetAttackDamageWithoutStatusEffectBuffs()+damage);
            cleanup.Add(()=> {
                if(!unit.IsDestroyed)unit.SetAttackDamage(Math.Max(0,unit.GetAttackDamageWithoutStatusEffectBuffs()-damage));
            });
        }
    }

    internal static int EducationPower(ICoreGameManagers core,int floor)
    {
        var units=Units(core,floor);
        return AcademicMath.EducationPower(units.FirstOrDefault()?.GetAttackDamage()??0,
            units.Count(unit=>unit.GetSourceCharacterData()==Education));
    }

    internal static IEnumerator HealShared(ICoreGameManagers core,int floor,int damage,CardState? source)
    {
        var allies=Units(core,floor).ToArray();
        int healing=AcademicMath.SharedHealing(damage,allies.Length);
        if(healing<=0)yield break;
        foreach(var ally in allies)
            if(!ally.IsDestroyed&&ally.IsAlive&&PreviewDependencies.CanWrite(ally,core))
                yield return ally.ApplyHeal(healing,responsibleCard:source);
    }
}
// 2026-10-25-2137||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/UnitSpellPassives.cs||Separar consumidores Representative, PhD y Education con referencias nativas y managers explícitos
// 2026-10-27-1535||codex-ww-inc||salidas/build/ww-inc/src/UnitSpellPassives.cs||No escalar ataque real de Representative sin copia de simulación preparada
// 2026-10-27-1535||codex-ww-inc||salidas/build/ww-inc/src/UnitSpellPassives.cs||Limitar curación compartida a destinos con estado válido durante preview
