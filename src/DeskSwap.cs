using System.Collections;
using System.Linq;
namespace WwInc;

// One atomic swap: native shift/relic/sentry events see the final order, never an intermediate one.
public sealed class CardEffectAcademicDeskSwap : AcademicEffectBase,ICardEffectChangesUnitPosition
{
    public override bool CanApplyInPreviewMode=>true;
    internal static bool Movable(CharacterState unit)=>!unit.IsDestroyed&&unit.IsAlive&&
        !unit.IsOuterTrainBoss()&&!unit.HasStatusEffect("immobile");
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers core)
    {
        var units=UnitSpellPassives.Units(core,p.selectedRoom);
        return units.Count>=2&&Movable(units[0])&&Movable(units[units.Count-1]);
    }
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers core,ISystemManagers systems)
    {
        var units=UnitSpellPassives.Units(core,p.selectedRoom);
        if(units.Count<2||!Movable(units[0])||!Movable(units[units.Count-1]))yield break;
        var front=units[0];var rear=units[units.Count-1];
        var room=core.GetRoomManager().GetRoom(p.selectedRoom);
        if(room==null)yield break;
        room.RearrangeCharacter(Team.Type.Monsters,0,units.Count-1);
        room.RearrangeCharacter(Team.Type.Monsters,units.Count-2,0);
        room.UpdateSpawnPointPositions(Team.Type.Monsters);
        foreach(var moved in new[]{front,rear})
        {
            if(!Present(moved,p.selectedRoom))continue;
            yield return core.GetRelicManager().CharacterFloorRearranged(moved,p.playedCard);
            if(!Present(moved,p.selectedRoom))continue;
            yield return core.GetHeroManager().ShiftCharacterTrigger(moved);
            if(Present(moved,p.selectedRoom))
                yield return core.GetCombatManager().SentryCharacterTrigger(moved,p.selectedRoom);
        }
        yield return core.GetRoomManager().HandleRoomUnitOrderPossiblyChanged();
        var save=core.GetSaveManager();
        if(!save.PreviewMode)
        {
            save.IncrementTrackedValueForUnlocks(MetagameSaveData.TrackedValue.UnitsMoved);
            save.IncrementTrackedValueForUnlocks(MetagameSaveData.TrackedValue.SoulUnitsMoved);
            save.CheckForNewUnlocks();
        }
        // Movement triggers may kill/move the intended attacker. Never substitute a different unit.
        if(!Present(rear,p.selectedRoom)||UnitSpellPassives.Units(core,p.selectedRoom).FirstOrDefault()!=rear)yield break;
        var previous=p.targets;
        p.targets=new System.Collections.Generic.List<CharacterState>{rear};
        try {yield return new CardEffectAttackWithUnit().ApplyEffect(s,p,core,systems);}
        finally {p.targets=previous;}
    }
    internal static bool Present(CharacterState unit,int floor)
        =>!unit.IsDestroyed&&unit.IsAlive&&unit.GetCurrentRoomIndex()==floor;
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/DeskSwap.cs||Intercambiar extremos atómicamente y delegar eventos de movimiento y ataque a APIs nativas; validar atacante tras eventos
