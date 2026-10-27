using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using ShinyShoe;
namespace WwInc;

internal static class SpellTrample
{
    internal sealed class Hit
    {
        internal CharacterState Target=null!;
        internal CardState Card=null!;
        internal int Incoming,Remaining;
    }
    private static readonly Stack<Hit> hits=new();
    internal static bool Own(CardState? card)=>card!=null&&card.GetCardType()==CardType.Spell&&!card.IsAnyAbility()&&
        AcademicRuntime.Id(card).StartsWith("david.wwinc-Card-",StringComparison.Ordinal);
    internal static int Excess(int damage,int health,int lifeLink=0)
    {
        long extra=(long)Math.Max(0,damage)-Math.Max(0,health);
        return (int)Math.Min(int.MaxValue,extra==0?Math.Max(0,lifeLink):Math.Max(0,extra));
    }
    internal static void Observe(CharacterState target,CardState? card,int damage)
    {
        if(hits.Count==0||!ReferenceEquals(hits.Peek().Target,target)||!ReferenceEquals(hits.Peek().Card,card))return;
        var hit=hits.Peek();
        hit.Remaining=Excess(Math.Min(hit.Incoming,Math.Max(0,damage)),target.GetHP());
    }
    internal static IEnumerator Apply(CardTraitState.ApplyingDamageParameters damage,ICoreGameManagers core)
    {
        var victim=damage.targetCharacter;var card=damage.damageSourceCard;
        if(victim==null||victim.IsDestroyed||card==null||damage.damageType is Damage.Type.Trample or Damage.Type.SpellOverflow)yield break;
        int floor=victim.GetCurrentRoomIndex();
        int remaining=Excess(damage.damage,victim.GetHP(),damage.damagePreventedByLifeLink);
        var processed=new List<CharacterState>{victim};
        while(remaining>0)
        {
            var target=SpellReplay.FrontEnemy(core,floor,processed);
            if(target==null)yield break;
            processed.Add(target);
            var hit=new Hit{Target=target,Card=card,Incoming=remaining};hits.Push(hit);
            try {
                // Native overflow damage avoids a second Spell Weakness multiplier and recursive traits.
                yield return core.GetCombatManager().ApplyDamageToTarget(remaining,target,
                    new CombatManager.ApplyDamageToTargetParameters{playedCard=card,damageType=Damage.Type.SpellOverflow});
            } finally {hits.Pop();}
            remaining=hit.Remaining;
        }
    }
}
[HarmonyPatch(typeof(CharacterState),"DoNotifyDamageWasApplied")]
internal static class SpellTrampleDamageObservation
{
    static void Prefix(CharacterState attacked,CardState damageSourceCard,int damage)=>SpellTrample.Observe(attacked,damageSourceCard,damage);
}
[HarmonyPatch(typeof(CardTraitTrample),nameof(CardTraitTrample.GetCardTooltipText))]
internal static class SpellTrampleTooltip
{
    static void Postfix(CardTraitTrample __instance,ref string __result)
    {
        if(SpellTrample.Own(__instance.GetCard()))__result="david.wwinc.SpellTrample".Localize();
    }
}
// 2026-10-27-1531||codex-ww-inc||salidas/build/ww-inc/src/SpellTrample.cs||Resolver Trample de hechizo por exceso postmitigación hacia enemigos frontales, con fuente y guardia de recursión
// 2026-10-27-1545||codex-ww-inc||salidas/build/ww-inc/src/SpellTrample.cs||Limitar Trample propio a hechizos W&W, sin cambiar unidades, equipos ni habilidades
