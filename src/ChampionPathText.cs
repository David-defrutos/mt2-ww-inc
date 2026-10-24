using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ShinyShoe;

namespace WwInc;

internal static class ChampionPathText
{
    // These paths have code-driven effects, which the native statistics-only
    // upgrade text deliberately omits. Incant paths already have native text.
    static readonly string[] Prefixes={"WW_FratBro","WW_QueenBee"};
    internal static IEnumerable<CardUpgradeState> Selected(CardState? card,CharacterState? unit=null)
    {
        if(card==null)return Array.Empty<CardUpgradeState>();
        string id=AcademicRuntime.Id(card);
        if(!id.EndsWith("WW_C01",StringComparison.Ordinal)&&!id.EndsWith("WW_C02",StringComparison.Ordinal))
            return Array.Empty<CardUpgradeState>();
        var upgrades=new List<CardUpgradeState>(card.GetCardStateModifiers().GetCardUpgrades());
        upgrades.AddRange(card.GetTemporaryCardStateModifiers().GetCardUpgrades());
        if(unit!=null)upgrades.AddRange(unit.GetAppliedCardUpgrades());
        var selected=new List<CardUpgradeState>();
        foreach(string prefix in Prefixes)
        {
            int rank=AcademicMath.ChampionRank(upgrades.Select(u=>u.GetAssetName()),prefix);
            if(rank==0)continue;
            var upgrade=upgrades.FirstOrDefault(u=>u.GetAssetName().EndsWith(prefix+rank,StringComparison.Ordinal));
            if(upgrade!=null&&upgrade.GetSourceCardUpgradeData()!=null)selected.Add(upgrade);
        }
        return selected;
    }
    internal static string Description(CardUpgradeState upgrade,CardState card)
        =>upgrade.GetUpgradeDescriptionKey().Localize(new CardEffectLocalizationContext(upgrade.GetSourceCardUpgradeData()!,null,card));
}

[HarmonyPatch(typeof(CardState),nameof(CardState.GenerateCardBodyText))]
internal static class ChampionPathCardTextHook
{
    static void Postfix(CardState __instance,CharacterState associatedCharacterState,ref string generatedText)
    {
        foreach(var upgrade in ChampionPathText.Selected(__instance,associatedCharacterState))
        {
            string text=ChampionPathText.Description(upgrade,__instance);
            if(!string.IsNullOrWhiteSpace(text))generatedText+=(string.IsNullOrWhiteSpace(generatedText)?"":Environment.NewLine)+text;
        }
    }
}

[HarmonyPatch(typeof(CharacterTooltipHelper),nameof(CharacterTooltipHelper.GetAdditionalTooltipContents))]
internal static class ChampionPathTooltipHook
{
    static void Postfix(CharacterState characterState,ref IEnumerable<(TooltipContent content,bool allowDuplicates)> __result)
        =>__result=Append(__result,characterState);
    static IEnumerable<(TooltipContent content,bool allowDuplicates)> Append(
        IEnumerable<(TooltipContent content,bool allowDuplicates)> original,CharacterState unit)
    {
        var seen=new HashSet<string>();
        foreach(var item in original){seen.Add(item.content.tooltipId);yield return item;}
        var card=unit?.GetSpawnerCard();
        foreach(var upgrade in ChampionPathText.Selected(card,unit))
        {
            string id="david.wwinc.path."+upgrade.GetAssetName();
            if(!seen.Add(id))continue;
            yield return(new TooltipContent(upgrade.GetUpgradeTitle(),ChampionPathText.Description(upgrade,card!),
                TooltipDesigner.TooltipDesignType.Keyword,id),false);
        }
    }
}

// 2026-10-11-0019||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ChampionPathText.cs||Mostrar sendas C# localizadas en carta y tooltip, con rango máximo y multisenda

// 2026-10-11-0020||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ChampionPathText.cs||Evitar la extensión Concat ambigua y usar colecciones nativas tipadas
// 2026-10-12-2232||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ChampionPathText.cs||Excluir sendas Dean ahora nativas del texto C# adicional para evitar duplicar Arcane Slay
