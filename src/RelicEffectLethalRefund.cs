using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShinyShoe;
namespace WwInc;

public sealed class RelicEffectLethalRefund : RelicEffectAcademic
{
    internal override bool Refund(CardState card,int kills,ICoreGameManagers core)
    {
        var progress=AcademicMath.RefundProgress(SpellRules.TuitionRefundCount,kills);
        SpellRules.TuitionRefundCount=progress.count;
        if(!progress.refund)return false;
        core.GetPlayerManager().AddEnergy(Math.Max(0,card.GetLastPlayedCost()));return true;
    }
    internal override bool Count(StringBuilder builder,CardStatistics statistics)
    {builder.Append(string.Format("TextFormat_Divide".Localize(),SpellRules.TuitionRefundCount,5));return true;}
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/RelicEffectLethalRefund.cs||Separar comportamiento A05 en consumidor propio sin identificación por ID
