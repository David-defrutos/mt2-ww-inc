using System;
using System.Collections;
using System.Linq;
namespace WwInc;

// Native hand selection, scoped to the effect's managers. No combat trackers or card IDs.
internal static class HandSelection
{
    private static bool IsPreview(ICoreGameManagers core)
        =>core.GetSaveManager().PreviewMode||core.GetCombatManager().IsRunningCardPlayPreview;

    internal static IEnumerator ChooseHand(ICoreGameManagers core,CardState? source,
        Func<CardState,bool> eligible,Action<CardState> picked)
        =>ChooseHand(core,source,eligible,new Func<CardState,IEnumerator>(card=>Complete(card,picked)));

    internal static IEnumerator ChooseHand(ICoreGameManagers core,CardState? source,
        Func<CardState,bool> eligible,Func<CardState,IEnumerator> apply,bool autoSelectSingleEligible=false)
    {
        if(IsPreview(core))yield break;
        var manager=core.GetCardManager();
        var choices=manager.GetHand().Where(card=>card!=source&&eligible(card)).ToArray();
        if(choices.Length==0)yield break;
        if(autoSelectSingleEligible&&choices.Length==1)
        {
            yield return ApplyChoice(core,choices[0],source,eligible,apply,manager);
            yield break;
        }
        // Opening the native UI completes the playing effect. Confirmation owns its coroutine.
        yield return manager.SelectCardFromHand(new HandSelectionUI.Params {
            descriptionKey="david.wwinc.ChooseCard",
            filterCallback=card=>card==source||!eligible(card),
            cardChosenCallback=(card,owner)=>ApplyChoice(core,card,source,eligible,apply,owner),
            disableIneligibleCards=true,selectionHandState=CardUI.CardUIState.HandDiscardCard },
            automaticallySelectLastCard:false);
        manager.AddSelectCardFromHandOnCompletedCallback(()=>HandSelectionPresentation.Restore(core));
    }

    private static IEnumerator ApplyChoice(ICoreGameManagers core,CardState card,CardState? source,
        Func<CardState,bool> eligible,Func<CardState,IEnumerator> apply,CardManager manager)
    {
        if(IsPreview(core)||!ReferenceEquals(manager,core.GetCardManager())||
            card==source||!manager.GetHand().Contains(card)||!eligible(card))yield break;
        yield return apply(card);
        yield return core.GetCombatManager().RunTriggerQueue();
    }

    private static IEnumerator Complete(CardState card,Action<CardState> picked)
    { picked(card);yield break; }
}
// 2026-10-25-2009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/HandSelection.cs||Extraer selector nativo con managers explícitos; bloquear preview y revalidar contexto al confirmar
// 2026-10-26-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/HandSelection.cs||Autoelección optativa de único objetivo válido revalidando contexto
// 2026-10-27-1431||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Restaurar presentación de cartas al completar o cancelar selector propio
// 2026-10-27-1528||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Resolver eventos generados por confirmación de selección antes de devolver control al preview
