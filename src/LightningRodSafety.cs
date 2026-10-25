using System;
using System.Collections;
using System.Linq;
namespace WwInc;
internal static class LightningRodSafety
{
    internal static bool EnemyEffect(CardEffectState effect)
        =>effect.GetTargetTeamType().HasFlag(Team.Type.Heroes)&&effect.GetTargetMode() is
            TargetMode.DropTargetCharacter or TargetMode.Room or TargetMode.RandomInRoom or
            TargetMode.FrontInRoom or TargetMode.BackInRoom or TargetMode.FrontWithStatus or
            TargetMode.Weakest or TargetMode.Tower or TargetMode.FrontInAllRooms or
            TargetMode.FrontInRoomAndRoomAbove or TargetMode.WeakestAllRooms or
            TargetMode.StrongestAllRooms or TargetMode.RandomFromAnyRoom;
    internal static bool Eligible(CardState card)
        =>card.GetCardType()==CardType.Spell&&!card.IsAnyAbility()&&card.GetEffectStates().Any(EnemyEffect);
    internal static IEnumerator Feedback(CharacterState unit,CardState card,ICoreGameManagers core)
    {
        if(!Eligible(card))yield break;
        bool preview=core.GetSaveManager().PreviewMode||core.GetCombatManager().IsRunningCardPlayPreview;
        // An unprepared character would mutate its live state. Never do so in a preview.
        if(preview&&!unit.TemporaryPreviewsEnabled)yield break;
        int dazed=unit.GetStatusEffectStacks("dazed");
        if(dazed>0)yield return core.GetCombatManager().ApplyDamageToTarget(AcademicMath.RedirectionDamage(dazed),unit,
            new CombatManager.ApplyDamageToTargetParameters());
        if(!unit.IsDestroyed&&!unit.IsDead)unit.AddStatusEffect("dazed",1);
        // PlayerManager.AddEnergy writes real energy and dispatches UI signals even in preview.
        if(!preview)core.GetPlayerManager().AddEnergy(Math.Max(0,card.GetLastPlayedCost()));
    }
}
// 2026-10-25-1605||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LightningRodSafety.cs||Compartir elegibilidad real de enemigos y separar feedback temporal de reembolso de Ember real
// 2026-10-25-1606||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/LightningRodSafety.cs||Revalidar elegibilidad en feedback para no consumir redirección por llamada accidental
