using System;
using System.Linq;
using HarmonyLib;
using ShinyShoe;

namespace WwInc;

public sealed class StatusEffectAcademicDissertation : StatusEffectState { }
internal static class DissertationMarker
{
    internal static string StatusId=null!;
    internal static void Refresh(CharacterState unit)
    {
        if(unit.IsDestroyed)return;
        int remaining=SpellRules.BoundAttacks.TryGetValue(unit,out var bindings)&&bindings.Count>0
            ?bindings.Max(binding=>binding.attacks):0;
        int current=unit.GetStatusEffectStacks(StatusId);
        if(remaining>current)unit.AddStatusEffect(StatusId,remaining-current,allowModification:false);
        else if(current>remaining)unit.RemoveStatusEffect(StatusId,current-remaining,allowModification:false);
    }
    internal static string Details(CharacterState unit)
    {
        if(!SpellRules.BoundAttacks.TryGetValue(unit,out var bindings))return "";
        return string.Join("<br><br>",bindings.Select(binding=>
        {
            var card=binding.card;
            var data=AcademicRuntime.Core.GetAllGameData().FindCardData(card.GetCardDataID());
            string description=data==null?card.GetTitle():CardTooltipHelper.GetTooltipContentForGeneratedCard(
                data,card,AcademicRuntime.Core.GetSaveManager());
            return string.Format("david.wwinc.ReservedAttacks".Localize(),binding.attacks)+" — "+
                DissertationTargeting.SideLabel(card)+"<br>"+description;
        }));
    }
}

[HarmonyPatch(typeof(TooltipUI),nameof(TooltipUI.GetStatusEffectCharacterContent))]
internal static class DissertationMarkerTooltipHook
{
    static void Postfix(StatusEffectState statusEffectState,CharacterState characterState,ref TooltipContent __result)
    {
        if(statusEffectState.GetStatusId()!=DissertationMarker.StatusId)return;
        string details=DissertationMarker.Details(characterState);
        if(!string.IsNullOrWhiteSpace(details))__result.body+="<br><br>"+details;
    }
}
// 2026-10-13-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/DissertationMarker.cs||Marca visual de vínculos: ataques restantes y tooltip con nombres de hechizos y contadores individuales
// 2026-10-13-0033||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/DissertationMarker.cs||Usar ID real de Dissertation en concesión, contador y tooltip
// 2026-10-24-2100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/DissertationMarker.cs||Mostrar bando elegido junto al hechizo y ataques restantes
// 2026-10-26-2151||codex-ww-inc||salidas/build/ww-inc/src/DissertationMarker.cs||Tooltip Dissertation incluye carta completa con formateador nativo y CardState real, contador y destino
