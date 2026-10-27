using System;
using System.Linq;
using HarmonyLib;
namespace WwInc;

internal static class TrainingWandText
{
    internal static StatusEffectStackData Adjust(CardState card,StatusEffectStackData status,bool dualityAlreadyApplied)
    {
        if(status.statusId!="conduit"||!card.IsEquipmentCard()||
            !card.GetEffectStates().Any(effect=>effect.GetCardEffect() is CardEffectAcademicTrainingWand))return status;
        int multiplier=dualityAlreadyApplied&&card.HasDualityStatusEffect()&&
            StatusEffectDualityState.IsStatusEffectedByDuality(status.statusId)?2:1;
        var result=status.Copy();
        result.count=(int)Math.Min(int.MaxValue,Math.Max(0L,(long)status.count+
            ((long)TrainingWand.CurrentAmount()-TrainingWand.BaseAmount)*multiplier));
        return result;
    }
}
[HarmonyPatch(typeof(CardTextHelper),nameof(CardTextHelper.SetUnupgradedStatusEffectText))]
internal static class TrainingWandPlainTextHook
{
    static void Prefix(CardState cardState,ref StatusEffectStackData statusEffectStackData,bool dualityAlreadyApplied)
        =>statusEffectStackData=TrainingWandText.Adjust(cardState,statusEffectStackData,dualityAlreadyApplied);
}
[HarmonyPatch(typeof(CardTextHelper),nameof(CardTextHelper.SetStatusEffectsUpgradeText))]
internal static class TrainingWandUpgradedTextHook
{
    static void Prefix(CardState cardState,ref StatusEffectStackData status)
        =>status=TrainingWandText.Adjust(cardState,status,true);
}
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/src/TrainingWandText.cs||Ajustar copia de estado nativo al mostrar varita; conservar extras, Duality y plantillas compartidas
