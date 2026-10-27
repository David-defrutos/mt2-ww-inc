using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShinyShoe;
namespace WwInc;

public sealed class RelicEffectQuietStudy : RelicEffectAcademic
{
    internal override void EndTurn(bool damagePlayed)
    {if(!damagePlayed)AcademicRuntime.Core.GetCardManager().AdjustDrawCountModifier(1);}
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/RelicEffectQuietStudy.cs||Separar comportamiento A04 en consumidor propio sin identificación por ID
