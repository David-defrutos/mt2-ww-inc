using System.Collections.Generic;
namespace WwInc;

internal static class PreviewTargets
{
    internal static void Expand(RoomManager rooms,List<CharacterState> targets)
    {
        if(!BattlePresentationContext.TryGet(out var core)||
            !ReferenceEquals(rooms,core.GetRoomManager()))return;
        var additional=new List<CharacterState>();
        for(int i=0;i<rooms.GetNumRooms();i++)
            rooms.GetRoom(i)?.AddCharactersToList(additional,Team.Type.Monsters|Team.Type.Heroes);
        foreach(var unit in additional)
            if(!unit.IsDestroyed&&unit.IsAlive&&unit.GetCharacterUI()!=null&&!targets.Contains(unit))
                targets.Add(unit);
    }
}
// 2026-10-27-1428||codex-ww-inc||salidas/build/ww-inc/src/PreviewTargets.cs||Aislar ampliación de preview en contexto vivo sin gestores retenidos ni unidades sin UI
