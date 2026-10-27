using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace WwInc;

// These boundaries are distinct. Native upgrades/traits still own the actual modifiers.
// Next-use expiry remains on CardTraitAcademicNextUseUpgrade; resolution expiry on its frame.
internal static class EffectLifetimes
{
    internal static readonly List<Action> EndCombatTurn=new();
    internal static readonly List<Func<IEnumerator>> NextPlayerTurn=new();
    internal static void AtEndCombatTurn(Action remove)=>EndCombatTurn.Add(remove);
    internal static void AtNextPlayerTurn(Func<IEnumerator> remove)=>NextPlayerTurn.Add(remove);
    internal static void FinishCombatTurn()=>TurnCleanup.Run(EndCombatTurn);
    internal static IEnumerator StartPlayerTurn()
    {
        var pending=NextPlayerTurn.ToArray();NextPlayerTurn.Clear();
        foreach(var remove in pending)yield return remove();
    }
    internal static void Reset(){EndCombatTurn.Clear();NextPlayerTurn.Clear();}
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/EffectLifetimes.cs||Separar fin de turno de combate y comienzo del próximo turno; conservar próximo uso y resolución en sus propietarios
