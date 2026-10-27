using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShinyShoe;
namespace WwInc;

public sealed class RelicEffectConsumeCopy : RelicEffectAcademic
{
    internal override void BeforeRules(AcademicRuntime.SpellResolution frame,CharacterState? target)
    {
        if(frame.Card.HasTrait(typeof(CardTraitExhaustState))&&SpellRules.OnceTurn.Add("A02"))
            SpellRules.AddRule(frame.Card,"spellchain");
    }
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/RelicEffectConsumeCopy.cs||Separar comportamiento A02 en consumidor propio sin identificación por ID
