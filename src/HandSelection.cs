using System;
using System.Collections;
using System.Linq;
namespace WwInc;

// Native hand selection, scoped to the effect's managers. No combat trackers or card IDs.
internal static class HandSelection
{
    private static bool CannotResolve(ICoreGameManagers core)
    {
        var save=core.GetSaveManager();
        return save.GetGameSequence()!=SaveData.GameSequence.InBattle||save.PreviewMode||
            core.GetCombatManager().IsRunningCardPlayPreview;
    }

    internal static IEnumerator ChooseHand(ICoreGameManagers core,CardState? source,
        Func<CardState,bool> eligible,Action<CardState> picked)
        =>ChooseHand(core,source,eligible,new Func<CardState,IEnumerator>(card=>Complete(card,picked)));

    internal static IEnumerator ChooseHand(ICoreGameManagers core,CardState? source,
        Func<CardState,bool> eligible,Func<CardState,IEnumerator> apply,bool autoSelectSingleEligible=false)
    {
        if(CannotResolve(core))yield break;
        var manager=core.GetCardManager();
        var choices=manager.GetHand().Where(card=>card!=source&&eligible(card)).ToArray();
        if(choices.Length==0)yield break;
        if(autoSelectSingleEligible&&choices.Length==1)
        {
            yield return ApplyChoice(core,choices[0],source,eligible,apply,manager);
            yield break;
        }
        // Native UI may retain delegates while finishing animations. Seal the selection
        // before effects run, so stale filters and repeated callbacks cannot act again.
        bool completed=false;
        IEnumerator Confirm(CardState card,CardManager owner)
        {
            if(completed||CannotResolve(core)||!ReferenceEquals(manager,owner)||
                card==source||!manager.GetHand().Contains(card)||!eligible(card))yield break;
            completed=true;
            yield return ApplyChoice(core,card,source,eligible,apply,owner);
        }
        // Opening the native UI completes the playing effect. Confirmation owns its coroutine.
        yield return manager.SelectCardFromHand(new HandSelectionUI.Params {
            descriptionKey="david.wwinc.ChooseCard",
            filterCallback=card=>!completed&&!CannotResolve(core)&&(card==source||!eligible(card)),
            cardChosenCallback=Confirm,
            disableIneligibleCards=true,selectionHandState=CardUI.CardUIState.HandDiscardCard },
            automaticallySelectLastCard:false);
        manager.AddSelectCardFromHandOnCompletedCallback(()=> {
            completed=true;
            HandSelectionPresentation.Restore(core);
        });
    }

    private static IEnumerator ApplyChoice(ICoreGameManagers core,CardState card,CardState? source,
        Func<CardState,bool> eligible,Func<CardState,IEnumerator> apply,CardManager manager)
    {
        if(CannotResolve(core)||!ReferenceEquals(manager,core.GetCardManager())||
            card==source||!manager.GetHand().Contains(card)||!eligible(card))yield break;
        yield return apply(card);
        if(!CannotResolve(core))yield return core.GetCombatManager().RunTriggerQueue();
    }

    private static IEnumerator Complete(CardState card,Action<CardState> picked)
    { picked(card);yield break; }
}
// 2026-10-25-2009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/HandSelection.cs||Extraer selector nativo con managers explícitos; bloquear preview y revalidar contexto al confirmar
// 2026-10-26-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/HandSelection.cs||Autoelección optativa de único objetivo válido revalidando contexto
// 2026-10-27-1431||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Restaurar presentación de cartas al completar o cancelar selector propio
// 2026-10-27-1528||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Resolver eventos generados por confirmación de selección antes de devolver control al preview
// 2026-10-29-1359||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Cerrar lógicamente cada selección antes de resolverla y hacer inocuos callbacks retenidos
// 2026-10-29-1359||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Desactivar filtro Not Eligible tras confirmar sin interferir con otros selectores nativos
// 2026-10-29-1359||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Sellar también selecciones canceladas o cerradas por la interfaz nativa
// 2026-10-29-2038||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Rechazar aperturas y callbacks tardíos de selección en recompensas/mapa antes de acceder al CombatManager
// 2026-10-29-2038||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Aplicar guardia de fase nativa exacta tanto a autoelección como a confirmación diferida
// 2026-10-29-2038||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Evitar que filtros retenidos de combate sigan oscureciendo cartas durante recompensas
// 2026-10-29-2039||codex-ww-inc||salidas/build/ww-inc/src/HandSelection.cs||Revalidar fase tras coroutine seleccionada antes de ejecutar cola del combate que puede haber terminado
