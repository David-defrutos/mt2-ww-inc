// Documento generado el 2026-10-11-1637
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
namespace WwInc;

internal static class ReservedSpellMarkers
{
    internal static CardUpgradeState? Marker(CardState? card)
    {
        if(card==null||!BattlePresentationContext.TryGet(out var core)||!SpellRules.ReservedCards.Contains(card))return null;
        var data=core.GetAllGameData().GetAllCardUpgradeData()
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
        if(___mode!=DeckScreen.Mode.ExhaustedPile||!BattlePresentationContext.TryGet(out _))return;
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
// 2026-10-27-2203||codex-ww-inc||salidas/build/ww-inc/src/ReservedSpellMarkers.cs||Ocultar marcadores y cartas de standby de batalla anterior cuando se consulta el mazo en el mapa
