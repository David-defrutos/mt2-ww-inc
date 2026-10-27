using System.Collections;
using System.Linq;
using HarmonyLib;
namespace WwInc;
[HarmonyPatch(typeof(CombatManager),"DoUnitCombat")]
internal static class ArmorHonorsRound
{
    static void Postfix(RoomState room,ref IEnumerator __result)=>__result=After(__result,room);
    internal static IEnumerator After(IEnumerator original,RoomState room)
    {
        try { yield return original; }
        finally
        {
            foreach(var unit in AcademicRuntime.ZeroAttack.ToArray())
                if(unit.IsDestroyed||unit.GetCurrentRoomIndex()==room.GetRoomIndex())
                    AcademicRuntime.ZeroAttack.Remove(unit);
        }
    }
}
// 2026-10-26-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ArmorHonorsRound.cs||Expirar ataque cero después de ronda nativa, no Resolve
