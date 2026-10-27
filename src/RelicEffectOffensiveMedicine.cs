using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShinyShoe;
namespace WwInc;

public sealed class RelicEffectOffensiveMedicine : RelicEffectAcademic
{
    internal override void BeforeRules(AcademicRuntime.SpellResolution frame,CharacterState? target)
    {
        if(!frame.Indirect&&SpellRules.IsHealingSpell(frame.Card)&&SpellRules.OnceTurn.Add("A10"))
            frame.Rules.Add("offensive_medicine");
    }
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/RelicEffectOffensiveMedicine.cs||Separar comportamiento A10 en consumidor propio sin identificación por ID
