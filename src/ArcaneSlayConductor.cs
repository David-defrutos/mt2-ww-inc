using Conductor.Extensions;
using MonsterTrain2.SharedTriggers;
namespace WwInc;
internal static class ArcaneSlayConductor
{
    internal static void Register()=>ArcaneSlayEvent.Trigger.SetToTriggerOnCharacterHit(OnHit);
    internal static bool OnHit(TriggerOnCharacterHitParams hit,out QueueTriggerParams? queued)
    {
        queued=null;
        var spell=hit.DamageParams.damageSourceCard;
        if(!AcademicRuntime.Active||!hit.Character.HasEffectTrigger(ArcaneSlayEvent.Trigger)||AcademicRuntime.ConversionDepth!=0||spell==null||
           spell.GetCardType()!=CardType.Spell||spell.IsAnyAbility()||
           (hit.DamagedCharacter.GetTeamType()&(Team.Type.Heroes|Team.Type.Monsters))==Team.Type.None||!hit.DamagedCharacter.IsDead||
           hit.Character.GetTeamType()!=Team.Type.Monsters)return false;
        queued=new QueueTriggerParams {
            fireTriggersData=new ArcaneSlayEvent.Data {
                Source=spell,paramInt=hit.OriginalDamage,paramInt2=(int)hit.DamageParams.damageType,
                overrideTargetCharacter=hit.DamagedCharacter }
        };
        return true;
    }
}
// 2026-10-26-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ArcaneSlayConductor.cs||Migrar detección y cola Arcane Slay a registro de impactos Conductor
// 2026-10-27-1428||codex-ww-inc||salidas/build/ww-inc/src/ArcaneSlayConductor.cs||Admitir bajas aliadas y enemigas en Arcane Slay por decisión del usuario
// 2026-10-27-1534||codex-ww-inc||salidas/build/ww-inc/src/ArcaneSlayConductor.cs||No encolar Arcane Slay en personajes sin el trigger ni intervenir en clanes ajenos
