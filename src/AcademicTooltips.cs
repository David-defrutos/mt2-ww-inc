using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ShinyShoe;

namespace WwInc;

// Native character tooltips describe triggers, abilities and statuses, but do
// not read a spawning card's override description for code-driven passives.
internal static class AcademicTooltips
{
    static readonly HashSet<string> Cards = new(
        Enumerable.Range(1,32).Select(i=>"WW_S"+i.ToString("00"))
        .Concat(new[]{"WW_C01","WW_C02","WW_E01","WW_E02","WW_R01","WW_R02",
            "WW_U01","WW_U02","WW_U03","WW_U04","WW_U05","WW_U06","WW_U08",
            "WW_U10","WW_U11","WW_U13","WW_U16","WW_U17","WW_U18"}));

    internal static bool TryCreate(CardState? card,CharacterState? unit,out TooltipContent content)
    {
        content=default;
        if(card==null)return false;
        string asset=AcademicRuntime.Id(card);
        if(!Cards.Any(id=>asset.EndsWith(id,StringComparison.Ordinal)))return false;
        string key=card.GetOverrideDescriptionKey();
        if(string.IsNullOrWhiteSpace(key)||!key.HasTranslation())return false;
        // CardState is the localization power context, just as in the native
        // body generator: upgraded effect values remain dynamic in tooltips.
        string body=key.Localize(new CardEffectLocalizationContext(card,unit));
        if(string.IsNullOrWhiteSpace(body))return false;
        content=new TooltipContent(card.GetTitle(),body,TooltipDesigner.TooltipDesignType.Keyword,
            "david.wwinc.effect."+asset);
        return true;
    }

    internal static bool Reserved(CharacterState unit,out TooltipContent content)
    {
        content=default;
        var lines=new List<string>();
        if(SpellRules.Archived.TryGetValue(unit,out var archived))
            foreach(var spell in archived.ToArray())lines.Add(spell.GetTitle());
        if(lines.Count==0)return false;
        string body=lines.Count==0?"david.wwinc.ReservedSpellsEmpty".Localize():string.Join("<br>",lines);
        content=new TooltipContent("david.wwinc.ReservedSpellsTitle".Localize(),body,
            TooltipDesigner.TooltipDesignType.Keyword,"david.wwinc.reserved");
        return true;
    }

    internal static IEnumerable<(TooltipContent content,bool allowDuplicates)> Append(
        IEnumerable<(TooltipContent content,bool allowDuplicates)> original,CharacterState unit)
    {
        var seen=new HashSet<string>();
        foreach(var item in original){seen.Add(item.content.tooltipId);yield return item;}
        if(TryCreate(unit.GetSpawnerCard(),unit,out var content)&&seen.Add(content.tooltipId))
            yield return(content,false);
        if(Reserved(unit,out var reserved)&&seen.Add(reserved.tooltipId))
            yield return(reserved,false);
    }
}

[HarmonyPatch(typeof(CardTooltipContainer),"AddTooltipsForCardState")]
internal static class AcademicCardTooltipHook
{
    static void Postfix(CardTooltipContainer __instance,CardState cardState)
    {
        if(AcademicTooltips.TryCreate(cardState,null,out var content))
            __instance.InstantiateTooltip(content.tooltipId,content.designType,allowDuplicates:false)
                ?.Set(content.title,content.body);
    }
}

[HarmonyPatch(typeof(CharacterTooltipHelper),nameof(CharacterTooltipHelper.GetAdditionalTooltipContents))]
internal static class AcademicCharacterTooltipHook
{
    static void Postfix(CharacterState characterState,
        ref IEnumerable<(TooltipContent content,bool allowDuplicates)> __result)
        =>__result=AcademicTooltips.Append(__result,characterState);
}

// 2026-10-11-1445||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicTooltips.cs||Añadir tooltips localizados de efectos C# a cartas y unidades con contexto de valores y deduplicación

// 2026-10-11-1633||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicTooltips.cs||Tooltip por unidad muestra reservas reales de Archived y BoundAttacks, nombres localizados y ataques restantes

// 2026-10-12-0915||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicTooltips.cs||Quitar tooltip de reservas vacías de Archivera y cubrir nuevo hechizo S31

// 2026-10-12-0946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicTooltips.cs||Cubrir tooltip S32
// 2026-10-13-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicTooltips.cs||Mover detalles de Dissertation al tooltip de su marca nativa y evitar duplicado en reservas
