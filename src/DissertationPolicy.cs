using System.Collections.Generic;
namespace WwInc;
internal enum DissertationIntent { Unknown, Beneficial, Harmful }
internal enum DissertationSide { Choose, Allies, Enemies }
internal static class DissertationPolicy
{
    // Only effects that accept a chosen unit on either team need inference.
    internal static DissertationSide Resolve(IEnumerable<DissertationIntent> flexibleEffects)
    {
        bool allies=false,enemies=false,unknown=false;
        foreach(var intent in flexibleEffects)
        {
            allies|=intent==DissertationIntent.Beneficial;
            enemies|=intent==DissertationIntent.Harmful;
            unknown|=intent==DissertationIntent.Unknown;
        }
        if(unknown||(allies&&enemies))return DissertationSide.Choose;
        return allies?DissertationSide.Allies:DissertationSide.Enemies;
    }
}
// 2026-10-24-2100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/DissertationPolicy.cs||Política de destino para efectos positivos, negativos y ambiguos sin inferir por nombre
