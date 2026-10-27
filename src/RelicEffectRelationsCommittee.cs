using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShinyShoe;
namespace WwInc;

public sealed class RelicEffectRelationsCommittee : RelicEffectAcademic
{
    internal override int TurnOrder=>0;
    internal override IEnumerator BeginTurn()=>SocialRules.CreateSeparators();
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/RelicEffectRelationsCommittee.cs||Separar comportamiento A07 en consumidor propio sin identificación por ID
