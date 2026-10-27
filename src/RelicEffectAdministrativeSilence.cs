using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShinyShoe;
namespace WwInc;

public sealed class RelicEffectAdministrativeSilence : RelicEffectAcademic
{
    internal override void UnitDied(bool chaperone,int floor)
    {
        if(!chaperone||!SpellRules.OnceTurn.Add("A08"))return;
        foreach(var ally in AcademicRuntime.Units(floor))
            if(ally.GetStatusEffectStacks("cooldown")>0)ally.RemoveStatusEffect("cooldown",1);
    }
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/RelicEffectAdministrativeSilence.cs||Separar comportamiento A08 en consumidor propio sin identificación por ID
