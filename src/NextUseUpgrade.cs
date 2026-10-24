using System.Collections;
using System.Linq;

namespace WwInc;

// CardManager invokes OnCardPlayed after resolving the spell, before discard/copies.
// The marker travels with native upgrades, including copied cards; no external ledger.
public sealed class CardTraitAcademicNextUseUpgrade : CardTraitState
{
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
