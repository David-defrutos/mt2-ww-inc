// Documento generado el 2026-10-11-1637
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
namespace WwInc;

internal static class ReservedSpellMarkers
{
    internal static CardUpgradeState? Marker(CardState? card)
    {
        var all=AllGameManagers.Instance;
        if(card==null||all==null||!all.AreMainSceneOnlyManagersInitialized()||!SpellRules.ReservedCards.Contains(card))return null;
        var data=all.GetCoreManagers().GetAllGameData().GetAllCardUpgradeData()
            .FirstOrDefault(u=>u!=null&&u.name.EndsWith("WW_ReservedMarker",System.StringComparison.Ordinal));
        if(data==null)return null;
        var marker=new CardUpgradeState();marker.Setup(data);return marker;
    }
}
// Mirror stand-by cards in the cemetery viewer only. Keep native consumed-card
// lists unchanged so display does not activate Consume rewards or recovery.
[HarmonyPatch(typeof(DeckScreen),"CollectCardsForMode")]
internal static class ReservedCemeteryViewHook
{
    static void Postfix(DeckScreen.Mode ___mode,ref List<CardState> __result)
    {
        var all=AllGameManagers.Instance;
        if(___mode!=DeckScreen.Mode.ExhaustedPile||all==null||!all.AreMainSceneOnlyManagersInitialized())return;
        __result=__result.Concat(SpellRules.ReservedCards).Distinct().ToList();
    }
}
// Like Vesper's Tamable marker, append to a copied UI upgrade list only.
[HarmonyPatch(typeof(CardUpgradeDisplayContainer),nameof(CardUpgradeDisplayContainer.DisplayUpgrades))]
internal static class ReservedCardIconHook
{
    static void Prefix(CardUpgradeDisplayContainer __instance,ref List<CardUpgradeState> temporaryUpgrades,bool ____isRegionRunDisplay)
    {
        if(____isRegionRunDisplay)return;
        var marker=ReservedSpellMarkers.Marker(__instance.GetComponentInParent<CardUI>()?.GetCardState());
        if(marker==null)return;
        temporaryUpgrades=new List<CardUpgradeState>(temporaryUpgrades??new List<CardUpgradeState>());temporaryUpgrades.Add(marker);
    }
}
[HarmonyPatch(typeof(CardTooltipContainer),"AddTooltipsForCardState")]
internal static class ReservedCardTooltipHook
{
    static void Postfix(CardTooltipContainer __instance,CardState cardState,SaveManager saveManager)
    {
        var marker=ReservedSpellMarkers.Marker(cardState);
        if(marker!=null)__instance.AddTooltipsCardUpgrade(cardState,marker,saveManager);
    }
}

// 2026-10-11-1637||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ReservedSpellMarkers.cs||Mostrar apartadas en cementerio y marcador visual basado en Vesper, sin añadir mejoras reales
