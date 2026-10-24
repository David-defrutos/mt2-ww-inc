using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
namespace WwInc;

internal static class EducationPreview
{
    internal static int TextDepth;
    internal static int Power(CardState? card)
    {
        var core=AcademicRuntime.Core;
        if(core==null||AcademicRuntime.Simulating||TextDepth>0||card==null||
            !RoomPowerEligibility.Affected(card)||!core.GetCombatManager().IsPlayerActionPhase()||
            !core.GetCardManager().GetHand().Contains(card)||
            AcademicRuntime.Frames.Any(frame=>frame.Card==card))return 0;
        int floor=core.GetRoomManager().GetSelectedRoom();
        return floor<0||SpellRules.OnceTurn.Contains("FirstSpell:"+floor)?0:SpellRules.EducationPowerOnFloor(floor);
    }
    internal sealed class Scope
    {
        readonly CardState card;
        readonly CardUpgradeState upgrade;
        bool finished;
        internal Scope(CardState card,int power)
        {
            this.card=card;
            upgrade=AcademicRuntime.PowerUpgrade(power,card.GetMagicPowerMultiplierFromTraits());
            card.GetTemporaryCardStateModifiers().AddUpgrade(upgrade);
            TextDepth++;
        }
        internal void Finish()
        {
            if(finished)return;
            finished=true;
            try {card.GetTemporaryCardStateModifiers().RemoveUpgrade(upgrade);}
            finally {TextDepth--;}
        }
    }
}
// Reuse native power scaling, formatting and highlighting. The prospective
// upgrade exists only during this synchronous text read, never during play.
[HarmonyPatch(typeof(CardState),nameof(CardState.GetCardText),new[]{typeof(CardStatistics),typeof(SaveManager),typeof(bool)})]
internal static class EducationTextHook
{
    static void Prefix(CardState __instance,ref bool includeCurrentTraitEffectText,out EducationPreview.Scope? __state)
    {
        __state=null;
        int power=EducationPreview.Power(__instance);
        if(power<=0)return;
        __state=new EducationPreview.Scope(__instance,power);
        includeCurrentTraitEffectText=true;
    }
    static void Postfix(EducationPreview.Scope? __state)=>__state?.Finish();
    static void Finalizer(EducationPreview.Scope? __state)=>__state?.Finish();
}
[HarmonyPatch(typeof(CardUI),"LateUpdate")]
internal static class EducationRefreshHook
{
    internal sealed class View {internal CardState? Card;internal int Power=-1;internal int Multiplier;internal string Language="";}
    static readonly ConditionalWeakTable<CardUI,View> views=new();
    static void Postfix(CardUI __instance,CardStatistics ___currentCardStats)
    {
        var card=__instance.GetCardState();
        if(card==null||___currentCardStats==null||!RoomPowerEligibility.Affected(card))return;
        int power=EducationPreview.Power(card),multiplier=card.GetMagicPowerMultiplierFromTraits();
        string language=LocalizationUtil.CurrentLanguage.ToString();
        var view=views.GetValue(__instance,_=>new View());
        if(view.Card==card&&view.Power==power&&view.Multiplier==multiplier&&view.Language==language)return;
        view.Card=card;view.Power=power;view.Multiplier=multiplier;view.Language=language;
        __instance.UpdateTextContent(card,___currentCardStats);
    }
}
// 2026-10-15-2226||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/EducationPreview.cs||Mostrar bono Education en texto del piso seleccionado con mejora nativa acotada y limpieza incluso ante excepción
