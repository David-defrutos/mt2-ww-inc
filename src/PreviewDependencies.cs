using System;
using System.Collections.Generic;
using System.Linq;
namespace WwInc;
internal static class PreviewDependencies
{
    internal static bool RequiresExpansion(RoomManager rooms)
    {
        if(!BattlePresentationContext.TryGet(out var core)||!ReferenceEquals(rooms,core.GetRoomManager()))return false;
        if(SpellRules.Pending.Any(rule=>rule.Kind=="floors")||
            core.GetRelicManager().GetRelicEffects(new List<RelicEffectAcademic>()).Count>0)return true;
        var units=new List<CharacterState>();
        for(int i=0;i<rooms.GetNumRooms();i++)rooms.GetRoom(i)?.AddCharactersToList(units,Team.Type.Monsters);
        return units.Any(unit=>!unit.IsDestroyed&&unit.IsAlive&&
            unit.GetSourceCharacterData().name.StartsWith("david.wwinc-Character-",StringComparison.Ordinal));
    }
    internal static bool CanWrite(CharacterState unit,ICoreGameManagers core)
    {
        if(unit.IsDestroyed)return false;
        if(core.GetSaveManager().PreviewMode)return unit.PreviewMode||unit.TemporaryPreviewsEnabled;
        return !core.GetCombatManager().IsRunningCardPlayPreview&&!unit.TemporaryPreviewsEnabled;
    }
}
// 2026-10-27-1534||codex-ww-inc||salidas/build/ww-inc/src/PreviewDependencies.cs||Ampliar preview solo con dependencias W&W reales y bloquear mutaciones durante preparación/restauración de copias
