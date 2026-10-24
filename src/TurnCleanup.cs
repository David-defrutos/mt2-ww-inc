using System;
using System.Collections.Generic;
using System.Linq;
namespace WwInc;
internal static class TurnCleanup
{
    internal static void Run(List<Action> actions)
    {
        var pending=actions.ToArray();actions.Clear();
        for(int i=pending.Length-1;i>=0;i--)pending[i]();
    }
}

// 2026-10-11-1039||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/TurnCleanup.cs||Restaurar estados temporales anidados desde el más reciente al original
