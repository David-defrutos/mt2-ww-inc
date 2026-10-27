using System.Collections.Generic;
using System.Linq;
namespace WwInc;
internal static class PendingSpellRules
{
    internal static bool ConsumeFloorUse(List<SpellRules.Rule> pending,int floor,bool indirect,bool eligible)
    {
        if(indirect||!eligible)return false;
        var rule=pending.FirstOrDefault(rule=>rule.Kind=="floors"&&(rule.Floor<0||rule.Floor==floor));
        return rule!=null&&pending.Remove(rule);
    }
}
// 2026-10-27-1540||codex-ww-inc||salidas/build/ww-inc/src/PendingSpellRules.cs||Extraer consumo FIFO de usos Memo sin otro contador y reutilizar la lista real de reglas
