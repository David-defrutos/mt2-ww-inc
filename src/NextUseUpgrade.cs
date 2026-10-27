using System.Collections;
using System.Linq;

namespace WwInc;

// CardManager invokes OnCardPlayed after resolving the spell, before discard/copies.
// The marker travels with native upgrades, including copied cards; no external ledger.
public sealed class CardTraitAcademicNextUseUpgrade : CardTraitState
{
    internal static void ReplaceCoveredBy(CardState card,CardUpgradeData lasting)
    {
        var supplied=lasting.GetTraitDataUpgrades().Select(t=>t.GetTraitStateName()).ToArray();
        var modifiers=card.GetTemporaryCardStateModifiers();
        foreach(var upgrade in modifiers.GetCardUpgrades().ToArray())
        {
            var traits=upgrade.GetTraitDataUpgrades();
            bool Marker(CardTraitData trait)=>ShinyShoe.TypeNameCache.GetType(trait.GetTraitStateName())==typeof(CardTraitAcademicNextUseUpgrade);
            if(traits.Any(Marker)&&traits.Where(t=>!Marker(t)).All(t=>supplied.Contains(t.GetTraitStateName())))
                card.RemoveUpgrade(upgrade,modifiers);
        }
    }
    public override PropDescriptions CreateEditorInspectorDescriptions()=>PropDescriptions.DisplayNone;
    public override string GetCardText()=>string.Empty;
    public override IEnumerator OnCardPlayed(CardState card,ICoreGameManagers managers)
    {
        if(managers.GetSaveManager().PreviewMode||AcademicRuntime.Simulating)yield break;
        var modifiers=card.GetTemporaryCardStateModifiers();
        var ownData=GetCardTraitData();
        foreach(var upgrade in modifiers.GetCardUpgrades().Where(u=>u.GetTraitDataUpgrades().Contains(ownData)).ToArray())
            card.RemoveUpgrade(upgrade,modifiers);
        card.UpdateCardBodyText();managers.GetCardManager().RefreshCardInHand(card);
    }
}
// 2026-10-15-2142||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/NextUseUpgrade.cs||Marcador de mejora: retirar solo su upgrade nativo tras el siguiente lanzamiento, sin limpiar por turno/descarte
// 2026-10-26-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/NextUseUpgrade.cs||Retirar solo mejora marcada cubierta por concesión duradera; preservar otras fuentes
