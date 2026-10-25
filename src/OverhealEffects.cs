using System;
using System.Collections;
using System.Linq;
using MonsterTrain2.SharedTriggers;
namespace WwInc;
// W&W consumers own conversion rules, caps and recursion policy; shared events do not.
internal static class OverhealEffects
{
    internal static CardEffectState Copy(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
    {
        var copy=new CardEffectState();
        copy.Setup(s.GetSourceCardEffectData(),p.selfTarget!.GetTeamType(),m.GetSaveManager());
        return copy;
    }
    internal static int Scale(int value,int amount)=>(int)Math.Min(int.MaxValue,(long)Math.Max(0,value)*Math.Max(0,amount));
}
// Only dynamic parameter scaling is custom. Native effects apply all combat rules.
public sealed class CardEffectOverhealStatus : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        int amount=OverhealEvent.Amount(p);if(amount<=0)yield break;
        var copy=OverhealEffects.Copy(s,p,m);
        var stacks=s.GetParamStatusEffectStackData().Select(stack=> {
            var entry=stack.Copy();entry.count=OverhealEffects.Scale(entry.count,amount);
            if(entry.statusId=="armor")entry.count=AcademicMath.OverhealArmor(entry.count,p.selfTarget!.GetMaxHP());
            return entry;
        }).ToArray();
        copy.SetParamStatusEffects(stacks);
        yield return new CardEffectAddStatusEffect().ApplyEffect(copy,p,m,systems);
    }
}
public sealed class CardEffectOverhealDamage : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        int amount=OverhealEvent.Amount(p);
        if(amount<=0||AcademicRuntime.HostileHealingDepth!=0)yield break;
        var copy=OverhealEffects.Copy(s,p,m);copy.SetParamInt(OverhealEffects.Scale(s.GetParamInt(),amount));
        var originalSource=p.playedCard;p.playedCard=OverhealEvent.Source;
        AcademicRuntime.ConversionDepth++;AcademicRuntime.HostileHealingDepth++;
        try { yield return new CardEffectDamage().ApplyEffect(copy,p,m,systems); }
        finally { AcademicRuntime.HostileHealingDepth--;AcademicRuntime.ConversionDepth--;p.playedCard=originalSource; }
    }
}
// 2026-10-25-1849||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/OverhealEffects.cs||Mantener efectos de Armor y daño y sus políticas como consumidores propios del clan
