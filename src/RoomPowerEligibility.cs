using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
namespace WwInc;

internal static class RoomPowerEligibility
{
    // CanScaleWithMagicPower only rejects non-spells and Pyre damage. It does
    // not check whether an effect reads Damage/Heal modifiers at all.
    internal static bool Affected(CardState? card)
        =>card!=null&&card.CanScaleWithMagicPower()&&!card.IsAnyAbility()&&
          card.GetEffectStates().Concat(card.GetTriggers().SelectMany(t=>t.GetCardEffectParams()))
            .Any(e=>e.GetCardEffect() is CardEffectDamage or CardEffectHeal or CardEffectNULL);

    internal static bool IsPairUpgrade(CardUpgradeState upgrade)
        =>upgrade.GetTraitDataUpgrades().Any(t=>t.GetTraitStateName()
            .EndsWith(nameof(CardTraitAcademicPairPower),StringComparison.Ordinal));
}

// Also cover inherited room upgrades on generated copies and existing saves:
// hide only this room's irrelevant icon, preserving every other upgrade.
[HarmonyPatch(typeof(CardUpgradeDisplayContainer),nameof(CardUpgradeDisplayContainer.DisplayUpgrades))]
internal static class PairPowerIconHook
{
    static void Prefix(CardUpgradeDisplayContainer __instance,ref List<CardUpgradeState> upgrades,
        ref List<CardUpgradeState> temporaryUpgrades)
    {
        var card=__instance.GetComponentInParent<CardUI>()?.GetCardState();
        if(card==null||RoomPowerEligibility.Affected(card))return;
        if(upgrades!=null)upgrades=upgrades.Where(u=>!RoomPowerEligibility.IsPairUpgrade(u)).ToList();
        if(temporaryUpgrades!=null)temporaryUpgrades=temporaryUpgrades.Where(u=>!RoomPowerEligibility.IsPairUpgrade(u)).ToList();
    }
}
[HarmonyPatch(typeof(CardTooltipContainer),nameof(CardTooltipContainer.AddTooltipsCardUpgrade))]
internal static class PairPowerTooltipHook
{
    static bool Prefix(CardState cardState,CardUpgradeState upgradeState)
        =>!RoomPowerEligibility.IsPairUpgrade(upgradeState)||RoomPowerEligibility.Affected(cardState);
}

// 2026-10-11-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/RoomPowerEligibility.cs||Filtrar mejora e icono de sala por efectos de daño o curación; proteger copias y tooltips
