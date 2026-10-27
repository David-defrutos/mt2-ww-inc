using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShinyShoe;
namespace WwInc;

public sealed class RelicEffectComplaintsOffice : RelicEffectAcademic
{
    internal override int TurnOrder=>1;
    internal override IEnumerator BeginTurn()=>SocialRules.TransferComplaints();
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/RelicEffectComplaintsOffice.cs||Separar comportamiento A09 en consumidor propio sin identificación por ID
