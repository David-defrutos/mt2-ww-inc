using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
namespace WwInc;
// Missing bridge only: render the configured grants with native localized formatters.
internal static class GrantedEffectTooltips
{
    internal static IEnumerable<TooltipContent> Append(IEnumerable<TooltipContent> original,CardEffectData data)
    {
        var entries=original.ToList();
        foreach(var content in entries)yield return content;
        var type=data.GetEffectStateType();
        if(type==null||type.Assembly!=typeof(Plugin).Assembly||data.GetHideTooltip()||StatusEffectManager.Instance==null)yield break;
        var seen=new HashSet<string>(entries.Select(t=>t.tooltipId));
        if(!data.GetStatusEffectTooltipsSuppressed())
            foreach(var status in data.GetParamStatusEffects())
                if(CardTooltipHelper.TryGetStatusEffectTooltipContent(status.statusId,StatusEffectManager.Instance,out var tooltip)&&seen.Add(tooltip.tooltipId))yield return tooltip;
        var upgrade=data.GetParamCardUpgradeData();
        if(upgrade==null)yield break;
        foreach(var tooltip in CardTooltipHelper.GenerateTooltipContentForInternalUpgrade(upgrade))
            if(seen.Add(tooltip.tooltipId))yield return tooltip;
        foreach(var traitData in upgrade.GetTraitDataUpgrades())
        {
            if(traitData.GetTooltipSuppressed())continue;
            var trait=(CardTraitState)Activator.CreateInstance(ShinyShoe.TypeNameCache.GetType(traitData.GetTraitStateName()));
            trait.Setup(traitData,CardState.None);
            var content=new TooltipContent(trait.GetCardTooltipTitle(),trait.GetCardTooltipText(),
                StatusEffectManager.Instance.GetTooltipDesignType(trait),trait.GetCardTooltipId());
            if(seen.Add(content.tooltipId))yield return content;
        }
    }
}
[HarmonyPatch(typeof(CardTooltipHelper),nameof(CardTooltipHelper.GetCardEffectTooltipContents),
    new[]{typeof(CardEffectState),typeof(TooltipUI.TitleStyle),typeof(SaveManager)})]
internal static class GrantedEffectStateTooltipHook
{
    static void Postfix(CardEffectState cardEffectState,ref IEnumerable<TooltipContent> __result)
        =>__result=GrantedEffectTooltips.Append(__result,cardEffectState.GetSourceCardEffectData());
}
[HarmonyPatch(typeof(CardTooltipHelper),nameof(CardTooltipHelper.GetCardEffectTooltipContents),
    new[]{typeof(CardEffectData),typeof(SaveManager)})]
internal static class GrantedEffectDataTooltipHook
{
    static void Postfix(CardEffectData cardEffectData,ref IEnumerable<TooltipContent> __result)
        =>__result=GrantedEffectTooltips.Append(__result,cardEffectData);
}
// 2026-10-25-1616||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/GrantedEffectTooltips.cs||Puente acotado a efectos propios: tooltips nativos de estados, rasgos y triggers concedidos, sin repetir cuerpos
// 2026-10-25-1617||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/GrantedEffectTooltips.cs||Usar namespace real ShinyShoe.TypeNameCache al formatear traits nativos
