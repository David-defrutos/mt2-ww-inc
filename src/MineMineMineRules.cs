using System.Collections.Generic;
namespace WwInc;
internal static class MineMineMineRules
{
    internal static int FirstHolder(IReadOnlyList<bool> holders)
    {
        for(int i=0;i<holders.Count;i++)if(holders[i])return i;
        return -1;
    }
}
// 2026-10-24-1930||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/MineMineMineRules.cs||Selector comprobable y determinista del primer portador en orden nativo frontal
