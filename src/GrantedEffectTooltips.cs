using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ShinyShoe;
namespace WwInc;
// Missing bridge only: render the configured grants with native localized formatters.
internal static class GrantedEffectTooltips
{
    internal static IEnumerable<TooltipContent> Append(IEnumerable<TooltipContent> original,CardEffectData data)
    {
        var type=data.GetEffectStateType();
        if(type==null||type.Assembly!=typeof(Plugin).Assembly)
        {
            foreach(var item in original)yield return item;
            yield break;
        }
        var entries=new List<TooltipContent>();
        foreach(var item in original)
        {
            var content=item;
            if(TooltipIdentity.Prepare(ref content))entries.Add(content);
        }
        foreach(var content in entries)yield return content;
        if(data.GetHideTooltip()||StatusEffectManager.Instance==null)yield break;
        var seen=new HashSet<string>(entries.Select(t=>t.tooltipId));
        if(!data.GetStatusEffectTooltipsSuppressed())
            foreach(var status in data.GetParamStatusEffects())
            {
                if(StatusEffectManager.Instance.GetStatusEffectDataById(status.statusId)==null||
                    !CardTooltipHelper.TryGetStatusEffectTooltipContent(status.statusId,StatusEffectManager.Instance,out var tooltip))continue;
                TooltipUI.GetStatusEffectTooltipContents(status.statusId,StatusEffectManager.Instance,out tooltip.title,out tooltip.body);
                if(TooltipIdentity.Prepare(ref tooltip)&&seen.Add(tooltip.tooltipId))yield return tooltip;
            }
        var upgrade=data.GetParamCardUpgradeData();
        if(upgrade==null)yield break;
        foreach(var item in CardTooltipHelper.GenerateTooltipContentForInternalUpgrade(upgrade))
        {
            var tooltip=item;
            if(TooltipIdentity.Prepare(ref tooltip)&&seen.Add(tooltip.tooltipId))yield return tooltip;
        }
        foreach(var traitData in upgrade.GetTraitDataUpgrades())
        {
            if(traitData.GetTooltipSuppressed())continue;
            var trait=(CardTraitState)Activator.CreateInstance(ShinyShoe.TypeNameCache.GetType(traitData.GetTraitStateName()));
            trait.Setup(traitData,CardState.None);
            var title=trait.GetCardTooltipTitle();
            if(string.IsNullOrEmpty(title))title=CardTraitData.GetTraitCardTextLocalizationKey(trait.GetType().Name).Localize();
            var body=trait.GetCardTooltipText();
            if(string.IsNullOrEmpty(body))body=CardTraitData.GetTraitTooltipTextLocalizationKey(trait.GetType().Name)
                .Localize(new CardEffectLocalizationContext(traitData));
            var content=new TooltipContent(CardTextHelper.RemoveBold(title),CardTextHelper.RemoveBold(body),
                StatusEffectManager.Instance.GetTooltipDesignType(trait),trait.GetCardTooltipId());
            if(TooltipIdentity.Prepare(ref content)&&seen.Add(content.tooltipId))yield return content;
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
// 2026-10-26-2313||codex-ww-inc||salidas/build/ww-inc/src/GrantedEffectTooltips.cs||Normalizar tooltips originales solo de efectos W&W y conservar ruta ajena intacta
// 2026-10-26-2313||codex-ww-inc||salidas/build/ww-inc/src/GrantedEffectTooltips.cs||Validar tooltip de estado concedido antes de enviarlo al contenedor
// 2026-10-26-2313||codex-ww-inc||salidas/build/ww-inc/src/GrantedEffectTooltips.cs||Validar tooltips de mejoras internas sin suprimir su contenido válido
// 2026-10-26-2313||codex-ww-inc||salidas/build/ww-inc/src/GrantedEffectTooltips.cs||Validar identidad de tooltip de trait concedido
// 2026-10-27-1434||codex-ww-inc||salidas/build/ww-inc/src/GrantedEffectTooltips.cs||Completar título y cuerpo de estados con formateador nativo antes de validar tooltip; evitar datos inexistentes
// 2026-10-27-1436||codex-ww-inc||salidas/build/ww-inc/src/GrantedEffectTooltips.cs||Usar localización nativa de tooltips de traits concedidos
// 2026-10-27-1436||codex-ww-inc||salidas/build/ww-inc/src/GrantedEffectTooltips.cs||Replicar fallback nativo InitCardTrait para descripción localizada de Purge y otros rasgos concedidos
