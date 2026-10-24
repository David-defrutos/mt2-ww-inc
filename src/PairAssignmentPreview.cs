using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
namespace WwInc;

// Display prospective keywords without adding traits or changing card modifiers.
internal static class PairAssignmentPreview
{
    internal static int MissingKeywords(CardState? card)
    {
        if(card==null||AcademicRuntime.Core==null||SocialRules.Wizard==null||SocialRules.Witch==null||
            !AcademicRuntime.Id(card).EndsWith("WW_S32",StringComparison.Ordinal))return 0;
        var core=AcademicRuntime.Core;
        if(!core.GetCombatManager().IsPlayerActionPhase()||!core.GetCardManager().GetHand().Contains(card))return 0;
        int floor=core.GetRoomManager().GetSelectedRoom();
        if(floor<0)return 0;
        var units=AcademicRuntime.Units(floor);
        if(!units.Zip(units.Skip(1),SocialRules.Mixed).Any(mixed=>mixed))return 0;
        return (card.HasTrait(typeof(CardTraitDamageOverflow))?0:1)|
            (card.HasTrait(typeof(CardTraitStrongerMagicPower))?0:2);
    }
    internal static string Text(int mask)
    {
        string explosive=(mask&1)!=0?new CardTraitDamageOverflow().GetCardText():"";
        string attuned=(mask&2)!=0?new CardTraitStrongerMagicPower().GetCardText():"";
        return string.Join(" ",new[]{explosive,attuned}.Where(t=>!string.IsNullOrWhiteSpace(t)));
    }
}
[HarmonyPatch(typeof(CardState),nameof(CardState.GetCardText),new[]{typeof(CardStatistics),typeof(SaveManager),typeof(bool)})]
internal static class PairAssignmentTextHook
{
    static void Postfix(CardState __instance,ref string __result)
    {
        string text=PairAssignmentPreview.Text(PairAssignmentPreview.MissingKeywords(__instance));
        if(text.Length>0)__result=CardTextHelper.FormatTextAsUpgrade(text,isTempModifier:true,useUpgradeHighlightTextTags:true)+Environment.NewLine+__result;
    }
}
// Native room selection does not always redraw the hand's body text.
[HarmonyPatch(typeof(CardUI),"LateUpdate")]
internal static class PairAssignmentRefreshHook
{
    internal sealed class View {internal CardState? Card;internal int Mask=-1;internal string Language="";}
    static readonly ConditionalWeakTable<CardUI,View> views=new();
    static void Postfix(CardUI __instance,CardStatistics ___currentCardStats)
    {
        var card=__instance.GetCardState();
        if(card==null||___currentCardStats==null||!AcademicRuntime.Id(card).EndsWith("WW_S32",StringComparison.Ordinal))return;
        int mask=PairAssignmentPreview.MissingKeywords(card);
        string language=LocalizationUtil.CurrentLanguage.ToString();
        var view=views.GetValue(__instance,_=>new View());
        if(view.Card==card&&view.Mask==mask&&view.Language==language)return;
        view.Card=card;view.Mask=mask;view.Language=language;
        __instance.UpdateTextContent(card,___currentCardStats);
    }
}

// 2026-10-12-1303||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/PairAssignmentPreview.cs||Mostrar keywords condicionales S32 solo en texto de mano por piso, sin mutar traits y refrescar al cambiar condición

// 2026-10-12-1303||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/PairAssignmentPreview.cs||Usar los tres parámetros nativos de formato y fábrica explícita de estado visual
