using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
namespace WwInc;
internal static class RepresentativeDamage
{
    internal sealed class Hit
    {
        internal CharacterState Unit=null!;
        internal int ArmorBefore;
    }
    static readonly Stack<Hit> hits=new();
    internal static Hit Begin(CharacterState unit)
    {
        var hit=new Hit{Unit=unit,ArmorBefore=unit.GetStatusEffectStacks("armor")};hits.Push(hit);return hit;
    }
    internal static void End(Hit hit)
    {
        if(hits.Count==0||!ReferenceEquals(hits.Peek(),hit))throw new InvalidOperationException("Unbalanced damage observation");
        hits.Pop();
    }
    internal static int Total(int healthDamage,int armorBefore,int armorAfter)
        =>(int)Math.Min(int.MaxValue,Math.Max(0L,healthDamage)+Math.Max(0L,(long)armorBefore-armorAfter));
    internal static void Record(CharacterState victim,CardState? spell,int healthDamage)
    {
        if(!AcademicRuntime.Active||AcademicRuntime.ConversionDepth!=0||spell==null||spell.IsAnyAbility()||
            victim.GetTeamType()!=Team.Type.Heroes||hits.Count==0||!ReferenceEquals(hits.Peek().Unit,victim)||
            AcademicRuntime.Frames.Count==0||!ReferenceEquals(AcademicRuntime.Frames.Peek().Card,spell))return;
        var frame=AcademicRuntime.Frames.Peek();
        int amount=Total(healthDamage,hits.Peek().ArmorBefore,victim.GetStatusEffectStacks("armor"));
        frame.RepresentativeDamage=(int)Math.Min(int.MaxValue,(long)frame.RepresentativeDamage+amount);
    }
}
// Native notification runs after mitigation/Armor consumption and before HP clipping or Revenge.
[HarmonyPatch(typeof(CharacterState),"DoNotifyDamageWasApplied")]
internal static class RepresentativeDamageHook
{
    static void Prefix(CharacterState attacked,CardState damageSourceCard,int damage)
        =>RepresentativeDamage.Record(attacked,damageSourceCard,damage);
}
// 2026-10-25-1605||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/RepresentativeDamage.cs||Contador separado de daño postmitigación y armadura para representante, conservando exceso sin contar escudos
