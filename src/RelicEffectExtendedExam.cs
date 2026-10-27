using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShinyShoe;
namespace WwInc;

public sealed class RelicEffectExtendedExam : RelicEffectAcademic
{
    internal override void BeforeRules(AcademicRuntime.SpellResolution frame,CharacterState? target)
    {
        if(!frame.Indirect&&SpellRules.TargetsEnemy(target)&&SpellRules.MonoEnemy(frame.Card)&&SpellRules.OnceTurn.Add("A06"))
            frame.Rules.Add("behind");
    }
    internal override bool Count(StringBuilder builder,CardStatistics statistics)
    {builder.Append(string.Format("TextFormat_Divide".Localize(),SpellRules.OnceTurn.Contains("A06")?1:0,1));return true;}
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/RelicEffectExtendedExam.cs||Separar comportamiento A06 en consumidor propio sin identificación por ID
