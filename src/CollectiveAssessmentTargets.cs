using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
namespace WwInc;

// Change only the native query's mode/team. TargetHelper still owns filtering,
// last-target history and ordering; no card/effect data is mutated.
internal static class CollectiveAssessmentTargets
{
    internal static bool Supported(CardEffectState effect)
        =>effect.GetCardEffect() is CardEffectDamage&&
          effect.GetTargetTeamType().HasFlag(Team.Type.Heroes)&&
          effect.GetTargetMode() is TargetMode.DropTargetCharacter or TargetMode.FrontInRoom or
              TargetMode.BackInRoom or TargetMode.Room;
    internal static bool Eligible(CardState card,CombatManager combat)
    {
        if(card.GetCardType()!=CardType.Spell||card.IsAnyAbility()||!card.GetIsDamageCard())return false;
        var damage=card.GetEffectStates().Where(effect=>effect.GetCardEffect() is CardEffectDamage).ToArray();
        return damage.Length>0&&damage.All(effect=>Supported(effect)&&!effect.HasRandomTarget(combat));
    }
    internal static bool Applied(CardState card,CardEffectState effect)
        =>AcademicRuntime.Active&&SpellRules.AreaTurn.TryGetValue(card,out int turn)&&
          turn==AcademicRuntime.Turn&&Supported(effect);

    // Nested unrelated queries shadow the outer scope. Null entries are intentional.
    internal static readonly Stack<CardEffectState?> Queries=new();
    internal static void Begin(CardEffectState effect,CardState? card)
        =>Queries.Push(effect!=null&&card!=null&&Applied(card,effect)?effect:null);
    internal static void End()=>Queries.Pop();
    internal static void Convert(ref TargetHelper.CollectTargetsData data)
    {
        if(Queries.Count==0||Queries.Peek() is not { } effect||
            data.targetMode!=effect.GetTargetMode()||data.targetTeamType!=effect.GetTargetTeamType())return;
        data.targetMode=TargetMode.Room;
        data.targetTeamType=Team.Type.Heroes;
    }
}
[HarmonyPatch(typeof(TargetHelper),nameof(TargetHelper.CollectTargets),new[]{
    typeof(CardEffectState),typeof(CardEffectParams),typeof(ICoreGameManagers),typeof(bool)})]
internal static class CollectiveTargetScopeHook
{
    internal static void Prefix(CardEffectState effectState,CardEffectParams cardEffectParams,out bool __state)
    {
        __state=false;
        CollectiveAssessmentTargets.Begin(effectState,cardEffectParams.playedCard);
        __state=true;
    }
    internal static void Finalizer(bool __state){if(__state)CollectiveAssessmentTargets.End();}
}
[HarmonyPatch]
internal static class CollectiveNativeTargetsHook
{
    static System.Reflection.MethodBase TargetMethod()=>typeof(TargetHelper).GetMethod(nameof(TargetHelper.CollectTargets),
        new[]{typeof(TargetHelper.CollectTargetsData),typeof(ICoreGameManagers),typeof(List<CharacterState>).MakeByRefType()})!;
    internal static void Prefix(ref TargetHelper.CollectTargetsData data)=>CollectiveAssessmentTargets.Convert(ref data);
}
// 2026-11-07-1058||codex-ww-inc||salidas/build/ww-inc/src/CollectiveAssessmentTargets.cs||Compartir elegibilidad y convertir consulta nativa a Room sin reconstruir filtros ni mutar datos de cartas
// 2026-11-07-1059||codex-ww-inc||salidas/build/ww-inc/src/CollectiveAssessmentTargets.cs||Resolver firma exacta de CollectTargets nativo sin depender de reflexión extendida
// 2026-11-07-1100||codex-ww-inc||salidas/build/ww-inc/src/CollectiveAssessmentTargets.cs||Preservar validación nativa de consultas con efecto ausente sin introducir excepción en el scope
