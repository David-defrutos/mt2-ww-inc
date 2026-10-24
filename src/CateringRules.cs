using System;
using System.Collections;
using System.Linq;

namespace WwInc;

internal static class CateringRules
{
    internal static CharacterData Stand=null!,Serving=null!;
    internal static bool IsStand(CharacterState unit)=>unit.GetSourceCharacterData().name.EndsWith("WW_Token_IceCreamStand",StringComparison.Ordinal);
    internal static bool CanSpawn(ICoreGameManagers core,int floor,SpawnPoint? location,int size)
    {
        var room=core.GetRoomManager().GetRoom(floor);
        return room!=null&&!room.GetIsPyreRoom()&&room.IsRoomEnabled()
            &&!room.IsRoomSummonBlocked(core.GetRelicManager())
            &&!room.WillBeAtCapacity(Team.Type.Monsters,size)
            &&core.GetMonsterManager().GetCanSpawnMonster(floor,location!,size);
    }
    internal static IEnumerator BeginTurn()
    {
        if(!AcademicRuntime.Active||Stand==null||Serving==null)yield break;
        var core=AcademicRuntime.Core;
        SocialRules.RefreshAuras();core.GetRoomManager().UpdateEnchantments();
        foreach(int floor in SocialRules.FloorsBottomUp())
        {
            // Snapshot producers so spawning cannot create additional production this turn.
            var producers=SocialRules.VisualUnits(floor).Where(IsStand).ToArray();
            foreach(var producer in producers)
            {
                if(producer.IsDead||producer.IsDestroyed||producer.GetCurrentRoomIndex()!=floor)continue;
                if(!CanSpawn(core,floor,null,Serving.GetSize()))continue;
                long conduit=AcademicRuntime.Units(floor).Sum(u=>(long)Math.Max(0,u.GetStatusEffectStacks("conduit")));
                var stats=AcademicMath.ServingStats(conduit);
                CharacterState? created=null;
                yield return core.GetMonsterManager().CreateMonsterState(Serving,null!,floor,u=>created=u,
                    spawnMode:SpawnMode.BackSlot,isCardless:true);
                if(created==null)continue;
                var upgrade=new CardUpgradeState();upgrade.Setup();
                upgrade.SetAttackDamage(stats.attack);upgrade.SetAdditionalHP(stats.health-Serving.GetHealth());
                yield return created.ApplyCardUpgrade(upgrade);
            }
        }
    }
}

public sealed class CardEffectAcademicCatering : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>CateringRules.CanSpawn(m,p.selectedRoom,p.dropLocation,s.GetParamCharacterData()?.GetSize()??1);
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m;
        var stand=s.GetParamCharacterData();
        if(stand==null||!CateringRules.CanSpawn(m,p.selectedRoom,p.dropLocation,stand.GetSize()))yield break;
        yield return m.GetMonsterManager().CreateMonsterState(stand,null!,p.selectedRoom,_=>{},
            spawnMode:p.dropLocation==null?SpawnMode.BackSlot:SpawnMode.SelectedSlot,
            spawnLocation:p.dropLocation,isCardless:true,fromPlayedCard:p.playedCard);
    }
}
// 2026-10-10-0058||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CateringRules.cs||Puesto token y producción por Conduit con capacidad nativa y orden determinista
// 2026-10-10-0108||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CateringRules.cs||Comprobar pips explícitamente: GetCanSpawnMonster omite capacidad sin ubicación
// 2026-10-11-0003||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CateringRules.cs||Mostrar la invocación del puesto en la previsualización nativa sin adelantar producción
