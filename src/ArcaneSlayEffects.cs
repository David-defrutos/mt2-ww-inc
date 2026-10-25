using System.Collections;
using MonsterTrain2.SharedTriggers;
namespace WwInc;

public sealed class CardEffectAcademicUpgradeSlayingSpell : AcademicEffectBase
{
    public override bool CanPlayAfterBossDead=>true;
    // FireTriggers tests effects without sourceCharacterTriggerState. Attribution is checked at execution.
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode||AcademicRuntime.Simulating)yield break;
        var card=ArcaneSlayEvent.Source(p);
        if(card==null||card.GetCardType()!=CardType.Spell||card.IsAnyAbility())yield break;
        CardUpgradeHelper.TryApplyUpgradeToCard(card,null,s.GetParamCardUpgradeData(),
            false,p.isFromHiddenTrigger,m,out _);
        card.UpdateCardBodyText();m.GetCardManager().RefreshCardInHand(card);
    }
}
// 2026-10-25-1849||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ArcaneSlayEffects.cs||Mantener premio de mejora permanente como consumidor W&W separado del trigger reutilizable
