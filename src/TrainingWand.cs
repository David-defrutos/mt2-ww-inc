using System;
using System.Linq;
using HarmonyLib;
using ShinyShoe;

namespace WwInc;

// Only bridge missing from native equipment: the displayed ring is not a CardStatistics value.
internal static class TrainingWand
{
    internal static CardUpgradeData Template=null!;
    internal static int Amount(int displayedRing,int baseStacks=10)
        =>(int)Math.Max(0L,(long)baseStacks-Math.Max(0L,(long)displayedRing-1));
    internal static int BaseAmount=>Template?.GetStatusEffectUpgrades().FirstOrDefault(s=>s.statusId=="conduit")?.count??10;
    internal static int CurrentAmount()=>Amount(AllGameManagers.Instance?.GetSaveManager()?.GetDisplayDistance()??1,
        BaseAmount);
    internal static void Prepare(CardUpgradeState upgrade)
    {
        if(Template!=null&&upgrade.GetSourceCardUpgradeData()==Template)
            upgrade.SetStatusCount("conduit",CurrentAmount());
    }
}

// Keep native attachment, validation, targeting, graft handling and equipment UI.
public sealed class CardEffectAcademicTrainingWand : CardEffectAttachEquipment
{
    public override string GetCardText(CardEffectState state,RelicManager? relicManager=null)
    {
        var parent=state.GetParentCardState()??CardState.None;
        var context=new CardEffectLocalizationContext(parent);
        return parent.IsEquipmentCard()?"david.wwinc.TrainingWandDecayText".Localize(context):
            string.Format("david.wwinc.TrainingWandText".Localize(context),TrainingWand.CurrentAmount());
    }
}

// Both paths create a fresh upgrade from data. Resolve the same ring amount on apply and removal.
// Never mutate CardUpgradeData, the equipment card, or other source upgrades during prediction.
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.ApplyCardUpgrade))]
internal static class TrainingWandApplyHook
{
    static void Prefix(CardUpgradeState cardUpgradeState)=>TrainingWand.Prepare(cardUpgradeState);
}
[HarmonyPatch(typeof(CharacterState),nameof(CharacterState.RemoveCardUpgrade))]
internal static class TrainingWandRemoveHook
{
    static void Prefix(CardUpgradeState cardUpgradeState)=>TrainingWand.Prepare(cardUpgradeState);
}
// 2026-10-26-2229||codex-ww-inc||salidas/build/ww-inc/src/TrainingWand.cs||Conectar ring visible con upgrade nativo de Conduit por instancia al equipar y retirar, sin trackers
// 2026-10-26-2230||codex-ww-inc||salidas/build/ww-inc/src/TrainingWand.cs||Leer potencia base del upgrade configurado en JSON
// 2026-10-26-2230||codex-ww-inc||salidas/build/ww-inc/src/TrainingWand.cs||Parametrizar potencia base en datos y calcular rings completados sin desbordamientos
// 2026-10-26-2234||codex-ww-inc||salidas/build/ww-inc/src/TrainingWand.cs||Usar CardState como contexto ILocalizationEffectPower del formateador nativo
// 2026-10-26-2236||codex-ww-inc||salidas/build/ww-inc/src/TrainingWand.cs||Usar sentinel nativo de carta cuando un tooltip se genera sin carta padre
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/src/TrainingWand.cs||Extraer base de varita para compartir cálculo de UI y aplicación
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/src/TrainingWand.cs||Usar potencia configurable compartida en fórmula de varita
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/src/TrainingWand.cs||Varita no repite Conduit en descripción cuando la carta ya lo muestra por línea nativa
