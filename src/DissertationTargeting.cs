using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ShinyShoe;
namespace WwInc;
internal static class DissertationTargeting
{
    internal static CardData AllyChoice=null!,EnemyChoice=null!;
    internal sealed class Cast
    {
        internal CardState Card=null!;
        internal CharacterState Owner=null!,Enemy=null!;
        internal Team.Type Side;
    }
    internal static readonly Stack<Cast> Casts=new();
    internal static bool OpensDialog(CardState card)
        =>card.GetEffectStates().Any(e=>e.GetCardEffect() is ICardEffectUiDialog);
    internal static bool HasRandomTargets(CardState card,CombatManager combat)
        =>card.GetEffectStates().Any(e=>e.HasRandomTarget(combat));
    // Concrete native effects verified to resolve without prompting during combat.
    internal static bool SafeTargetless(CardState card)
        =>card.GetEffectStates().Count>0&&card.GetEffectStates().All(e=>e.GetCardEffect() is
            CardEffectRewardGold or CardEffectAdjustDragonsHoard or CardEffectDraw or CardEffectDrawType);
    internal static bool CanBind(CardState card,CombatManager combat)
        =>!OpensDialog(card)&&!HasRandomTargets(card,combat)&&
          (SafeTargetless(card)||card.GetEffectStates().Any(e=>
            (e.GetTargetTeamType()&(Team.Type.Monsters|Team.Type.Heroes))!=0||e.GetTargetMode()==TargetMode.Self));
    internal static DissertationSide PreferredSide(CardState card)
        =>DissertationPolicy.Resolve(card.GetEffectStates().Where(e=>e.GetTargetMode()==TargetMode.DropTargetCharacter&&
            e.GetTargetTeamType().HasFlag(Team.Type.Monsters)&&e.GetTargetTeamType().HasFlag(Team.Type.Heroes)).Select(Intent));
    internal static DissertationIntent Intent(CardEffectState effect)
    {
        var native=effect.GetCardEffect();
        if(native is CardEffectHealAndDamageRelative)return DissertationIntent.Unknown;
        if(native is CardEffectDamage)return DissertationIntent.Harmful;
        if(SpellRules.HealingEffect(effect)||native is CardEffectRewardGold)return DissertationIntent.Beneficial;
        if(native is CardEffectAddStatusEffect)
        {
            var entries=effect.GetParamStatusEffectStackData();
            if(entries.Length==0)return DissertationIntent.Unknown;
            DissertationIntent result=DissertationIntent.Unknown;
            foreach(var entry in entries)
            {
                var data=StatusEffectManager.Instance.GetStatusEffectDataById(entry.statusId);
                if(data==null||entry.count<=0)return DissertationIntent.Unknown;
                var current=data.GetDisplayCategory()==StatusEffectData.DisplayCategory.Positive?DissertationIntent.Beneficial:
                    data.GetDisplayCategory()==StatusEffectData.DisplayCategory.Negative?DissertationIntent.Harmful:DissertationIntent.Unknown;
                if(current==DissertationIntent.Unknown||(result!=DissertationIntent.Unknown&&current!=result))return DissertationIntent.Unknown;
                result=current;
            }
            return result;
        }
        if(native is CardEffectBuffDamage||native is CardEffectBuffMaxHealth)
            return effect.GetParamInt()>0?DissertationIntent.Beneficial:effect.GetParamInt()<0?DissertationIntent.Harmful:DissertationIntent.Unknown;
        if(native is CardEffectDebuffDamage||native is CardEffectDebuffMaxHealth)return DissertationIntent.Harmful;
        // Unknown custom effects and mixed native effects require an explicit choice.
        return DissertationIntent.Unknown;
    }
    internal static IEnumerator Bind(CardState selected,CharacterState owner,ICoreGameManagers core,ISystemManagers systems)
    {
        if(!CanBind(selected,core.GetCombatManager()))yield break;
        var preference=PreferredSide(selected);
        Team.Type side=preference==DissertationSide.Allies?Team.Type.Monsters:Team.Type.Heroes;
        if(preference==DissertationSide.Choose)
        {
            // Detached display cards never enter a pile or the permanent deck. Native
            // DeckScreen records/replays ChooseCardSelection (including index + data ID).
            var allies=new CardState(AllyChoice,core.GetSaveManager());
            var enemies=new CardState(EnemyChoice,core.GetSaveManager());
            CardState? choice=null;
            yield return SpellRules.SelectPile(systems,TargetMode.Self,c=>c==allies||c==enemies,c=>choice=c,
                cancel:false,candidates:new List<CardState>{allies,enemies},instructions:"david.wwinc.DissertationChooseSide");
            if(choice==null)yield break;
            side=choice==allies?Team.Type.Monsters:Team.Type.Heroes;
        }
        if(owner.IsDestroyed||owner.IsDead||!core.GetCardManager().GetHand().Contains(selected)||!CanBind(selected,core.GetCombatManager()))yield break;
        core.GetCardManager().MoveToStandByPile(selected,false,false);SpellRules.ReservedCards.Add(selected);
        SpellRules.BoundSides[selected]=side;
        if(!SpellRules.BoundAttacks.TryGetValue(owner,out var bindings))SpellRules.BoundAttacks[owner]=bindings=new();
        bindings.Add((selected,3));DissertationMarker.Refresh(owner);
    }
    internal static string SideLabel(CardState card)
    {
        if(SafeTargetless(card))return "david.wwinc.DissertationNoTarget".Localize();
        var flexible=card.GetEffectStates().Any(e=>e.GetTargetMode()==TargetMode.DropTargetCharacter&&
            e.GetTargetTeamType().HasFlag(Team.Type.Monsters)&&e.GetTargetTeamType().HasFlag(Team.Type.Heroes));
        var team=flexible&&SpellRules.BoundSides.TryGetValue(card,out var side)?side:card.GetTargetedTeamType();
        if(team.HasFlag(Team.Type.Monsters)&&team.HasFlag(Team.Type.Heroes))
            return "david.wwinc.DissertationAllies".Localize()+" / "+"david.wwinc.DissertationEnemies".Localize();
        return (team.HasFlag(Team.Type.Heroes)?"david.wwinc.DissertationEnemies":"david.wwinc.DissertationAllies").Localize();
    }
    internal static IEnumerator Repeat(CardState card,int floor,CharacterState owner,CharacterState enemy)
    {
        // Recheck in case another effect changed the reserved spell after binding.
        if(owner.IsDestroyed||owner.IsDead||OpensDialog(card)||HasRandomTargets(card,AcademicRuntime.Core.GetCombatManager()))yield break;
        var cast=new Cast{Card=card,Owner=owner,Enemy=enemy,Side=SpellRules.BoundSides.TryGetValue(card,out var side)?side:Team.Type.Heroes};
        var target=cast.Side==Team.Type.Monsters?owner:enemy;
        var frame=new AcademicRuntime.SpellResolution{Card=card,Floor=floor,Target=target,Indirect=true};
        int previous=SpellRules.EffectFloor;SpellRules.EffectFloor=floor;
        Casts.Push(cast);AcademicRuntime.Frames.Push(frame);
        try
        {
            yield return SpellReplay.Cast(card,floor,target,owner);
        }
        finally{AcademicRuntime.Frames.Pop();Casts.Pop();SpellRules.EffectFloor=previous;}
        yield return AcademicRuntime.AfterSpell(frame);
    }
}
[HarmonyPatch(typeof(TargetHelper),nameof(TargetHelper.CheckTargetsOverride))]
internal static class DissertationTargetOverride
{
    static bool Prefix(CardEffectState effectState,List<CharacterState> targets,ref CharacterState overrideTargetCharacter)
    {
        if(DissertationTargeting.Casts.Count==0||effectState.GetTargetMode()!=TargetMode.DropTargetCharacter)return true;
        var cast=DissertationTargeting.Casts.Peek();
        if(effectState.GetParentCardState()!=cast.Card)return true;
        var team=effectState.GetTargetTeamType();
        bool ally=team.HasFlag(Team.Type.Monsters),enemy=team.HasFlag(Team.Type.Heroes);
        overrideTargetCharacter=ally&&(!enemy||cast.Side==Team.Type.Monsters)?cast.Owner:cast.Enemy;
        if(overrideTargetCharacter.IsDestroyed||overrideTargetCharacter.IsDead){targets.Clear();return false;}
        return true; // Native subtype, team, boss and remaining targeting checks still apply.
    }
}
// 2026-10-24-2100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/DissertationTargeting.cs||Destinos de Dissertation con metadata nativa, selección nativa reproducible y override acotado de objetivo único
// 2026-10-24-2101||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/DissertationTargeting.cs||Usar API real GetParamStatusEffectStackData de CardEffectState
// 2026-10-24-2102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/DissertationTargeting.cs||Marca distingue bando flexible elegido de objetivos nativos exclusivos o múltiples
// 2026-10-24-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/DissertationTargeting.cs||Tratar HealAndDamageRelative como mixto aunque herede de CardEffectHeal
// 2026-10-25-1640||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/DissertationTargeting.cs||Excluir diálogos de Dissertation al seleccionar, vincular y repetir para impedir UI durante ataques
// 2026-10-25-1832||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/DissertationTargeting.cs||Excluir objetivos aleatorios mediante HasRandomTarget nativo al seleccionar, reservar y repetir
// 2026-10-26-2151||codex-ww-inc||salidas/build/ww-inc/src/DissertationTargeting.cs||Permitir recursos y robo nativos seguros sin objetivo; mantener exclusiones UI y aleatorios
// 2026-10-26-2151||codex-ww-inc||salidas/build/ww-inc/src/DissertationTargeting.cs||Etiqueta sin objetivo para hechizos globales vinculados
// 2026-10-27-1528||codex-ww-inc||salidas/build/ww-inc/src/DissertationTargeting.cs||Compartir reproducción segura y fuente explícita sin perder destinos positivos de Dissertation
