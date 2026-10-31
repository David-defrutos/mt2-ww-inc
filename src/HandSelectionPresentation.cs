using System.Linq;
namespace WwInc;

internal static class HandSelectionPresentation
{
    internal static void Restore(ICoreGameManagers owner)
    {
        if(!BattlePresentationContext.TryGet(out var core)||!ReferenceEquals(core,owner))return;
        var hand=core.GetHandUI();
        if(hand==null||hand.IsHandSelectionActive())return;
        var manager=core.GetCardManager();
        foreach(var card in manager.GetHand().ToArray())
        {
            // A refresh may focus a card and open another native selector.
            if(hand.IsHandSelectionActive())break;
            manager.RefreshCardInHand(card,cleanupTweens:false,resetHandState:true);
        }
    }
}

// 2026-10-27-1431||codex-ww-inc||salidas/build/ww-inc/src/HandSelectionPresentation.cs||Limpiar Not Eligible y sombreado mediante API nativa solo en selectores del mismo combate
// 2026-10-27-1528||codex-ww-inc||salidas/build/ww-inc/src/HandSelectionPresentation.cs||Sustituir manipulación manual de CardUI por RefreshHandCards nativo como ChooseDiscard
// 2026-10-29-2033||codex-ww-inc||salidas/build/ww-inc/src/HandSelectionPresentation.cs||Usar instantánea de mano al restaurar presentación tras selector
// 2026-10-29-2033||codex-ww-inc||salidas/build/ww-inc/src/HandSelectionPresentation.cs||Restaurar mano sin destruir tweens y detener limpieza si otro selector nativo se abre durante el refresco
