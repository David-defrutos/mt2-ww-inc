using System;
using System.Collections;
using System.Linq;
namespace WwInc;

public sealed class CardEffectAcademicTome : AcademicEffectBase,ICardEffectUiDialog
{
    public override bool CanApplyInPreviewMode=>true;
    private static bool Eligible(CardState card,CardEffectState effect,ICoreGameManagers managers)
        =>effect.GetParamCardUpgradeData() is { } upgrade&&upgrade.GetFilters().All(filter=>filter.FilterCard(card,managers.GetRelicManager()));
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>m.GetCardManager().GetHand().Any(c=>c!=p.playedCard&&Eligible(c,s,m));
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;
        yield return HandSelection.ChooseHand(m,p.playedCard,c=>Eligible(c,s,m),selected=> {
            CardEffectAddTempCardUpgradeToCardsInHand.TryApplyUpgradeToCard(selected,false,s,p,m);
        });
    }
}

public sealed class CardEffectAcademicReturnFrozen : AcademicEffectBase
{
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>p.playedCard!=null;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode||p.playedCard==null)yield break;
        // Slay runs before natural discard; returning here would duplicate the card in its piles.
        SpellRules.AddRule(p.playedCard,"return_frozen");
        yield break;
    }
    internal static void ProtectUntilNextTurn(CardState card,CardManager manager)
    {
        if(!card.HasTrait(typeof(CardTraitFreeze)))AcademicRuntime.Trait(card,"CardTraitFreeze");
        if(card.HasTrait(typeof(CardTraitUnplayable)))return;
        AcademicRuntime.Trait(card,"CardTraitUnplayable");
        var added=card.GetTemporaryCardStateModifiers().GetTemporaryTraits().Last(t=>t is CardTraitUnplayable);
        EffectLifetimes.AtNextPlayerTurn(()=>Unlock(card,added,manager));
    }
    private static IEnumerator Unlock(CardState card,CardTraitState added,CardManager manager)
    {
        card.RemoveTemporaryTrait(added);
        card.UpdateCardBodyText();
        if(manager.GetHand().Contains(card))manager.RefreshCardInHand(card);
        yield break;
    }
}

// 2026-10-11-1102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Crear carga y reset de Proyecto Aplazado, Spike X y Tomo Consume

// 2026-10-11-1104||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Preview del spike respeta costes fijos de recuperaciones y no consume todo el Ember

// 2026-10-11-1623||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Modificar tome: descuento de -1 a 0; conservar Attuned y poder +10 de batalla

// 2026-10-12-0927||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Reserve añade Magic Power fijo 5 con Attuned y conserva todas las cargas al lanzar

// 2026-10-12-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Tome permite selección y aplica Attuned más Power al confirmar sin bloquear el lanzamiento

// 2026-10-12-1712||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Eliminar escaneo Reserve y delegar filtro/mejora del tomo en máscaras y helper nativos

// 2026-10-12-1713||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Proteger selector si falta la mejora declarativa del tomo

// 2026-10-12-1758||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Conexión Slay con retorno tras descarte; Freeze nativo y retirada exacta de Unplayable al turno siguiente
// 2026-10-12-2258||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Retirar C# del Spike y su reducción cooldown; sustituido por componentes nativos
// 2026-10-25-1640||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Declarar ICardEffectUiDialog en efectos que abren selección de cartas
// 2026-10-25-2009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ApprovedSpells.cs||Usar selector nativo independiente con managers del efecto; conservar filtros y resolución
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/ApprovedSpells.cs||Registrar vencimiento explícito sin cambiar instancias de mejoras ni duración aprobada
