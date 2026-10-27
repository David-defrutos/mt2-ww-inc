namespace WwInc;

internal static class HandSelectionPresentation
{
    internal static void Restore(ICoreGameManagers owner)
    {
        if(!BattlePresentationContext.TryGet(out var core)||!ReferenceEquals(core,owner))return;
        var hand=core.GetHandUI();
        if(hand==null||hand.IsHandSelectionActive())return;
        core.GetCardManager().RefreshHandCards(resetHandState:true);
    }
}

// 2026-10-27-1431||codex-ww-inc||salidas/build/ww-inc/src/HandSelectionPresentation.cs||Limpiar Not Eligible y sombreado mediante API nativa solo en selectores del mismo combate
// 2026-10-27-1528||codex-ww-inc||salidas/build/ww-inc/src/HandSelectionPresentation.cs||Sustituir manipulación manual de CardUI por RefreshHandCards nativo como ChooseDiscard
