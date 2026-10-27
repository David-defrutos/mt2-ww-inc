using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShinyShoe;
namespace WwInc;

public sealed class RelicEffectStrengthChair : RelicEffectAcademic
{
    internal override void BeforePower(AcademicRuntime.SpellResolution frame,List<CharacterState> units)
    {
        if(frame.Card.GetIsDamageCard()&&SpellRules.OnceTurn.Add("A01"))
            SpellRules.ResolutionPower(frame,(units.FirstOrDefault()?.GetAttackDamage()??0)/2);
    }
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/RelicEffectStrengthChair.cs||Separar comportamiento A01 en consumidor propio sin identificación por ID
