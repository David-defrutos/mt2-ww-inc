using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace WwInc;

internal static class ExcessCredit
{
    // Card identity, not template ID: multiple champions and copies keep their own origin.
    internal static readonly Dictionary<CardState,CardState> Origins=new();
    internal static bool IsCredit(CardState card)=>AcademicRuntime.Id(card).EndsWith("WW_Token_ExtraCredit",StringComparison.Ordinal);
    internal static CharacterState? Owner(CardState? spell)
    {
        if(spell==null||!Origins.TryGetValue(spell,out var card))return null;
        return AcademicRuntime.AllAllies().FirstOrDefault(u=>ReferenceEquals(u.GetSpawnerCard(),card));
    }
    internal static void Generate(CharacterState attacker,int excess,int percent,CardUpgradeData? grantedUpgrade,CardData template)
    {
        if(!AcademicRuntime.Active||AcademicRuntime.Simulating||attacker.IsDestroyed||!attacker.IsAlive)return;
        int power=AcademicMath.ScaleOverdamage(excess,percent);
        var origin=attacker.GetSpawnerCard();
        if(power<=0||origin==null)return;
        var manager=AcademicRuntime.Core.GetCardManager();
        // Reuse only a card already in hand; a card elsewhere does not block a new one.
        var spell=manager.GetHand().FirstOrDefault(IsCredit);
        if(spell==null)
            spell=manager.AddNewCard(template,manager.GetBelowHandSize()?CardPile.HandPile:CardPile.DiscardPile,
                fromRelic:false,permanent:true);
        if(spell==null)return;
        // Preserve its original creator when several carriers add damage to the same card.
        if(!Origins.ContainsKey(spell))Origins[spell]=origin;
        var modifiers=spell.GetCardStateModifiers();
        if(grantedUpgrade!=null&&!modifiers.HasUpgrade(grantedUpgrade.GetID()))
        {
            var upgrade=new CardUpgradeState();upgrade.Setup(grantedUpgrade);
            upgrade.SetAvoidClobberingExistingTraits(true);
            if(CardUpgradeHelper.TryApplyUpgradeToCard(spell,origin,upgrade,isTemporaryUpgrade:false,AcademicRuntime.Core))
                spell.ReapplyMagicPowerScalingFromTraitsToAllExistingUpgrades(AcademicRuntime.Core);
        }
        long current=modifiers.GetAdditionalDamage().Sum(value=>(long)value);
        modifiers.IncrementAdditionalDamage(AcademicMath.CreditDamageIncrement(current,power));
        spell.UpdateDamageText();spell.UpdateCardBodyText();
        manager.RefreshCardInHand(spell);
    }
    internal static void RewardHandOwner(CardState? spell,int kills,ICoreGameManagers core)
    {
        // Same exceptional hand-champion Savagery case as Pathogens; live owners use native credit.
        if(kills<=0||core.GetSaveManager().PreviewMode||spell==null||Owner(spell)!=null||
            !Origins.TryGetValue(spell,out var card)||!core.GetCardManager().GetHand().Contains(card)||
            core.GetSaveManager().GetActiveSavePyreCharacterData()?.name!="PyreHeartSavagery")return;
        card.GetCardStateModifiers().IncrementAdditionalDamage(kills);
        card.UpdateDamageText();card.UpdateCardBodyText();core.GetCardManager().RefreshCardInHand(card);
    }
}

public sealed class CardEffectAcademicAuthoredDamage : CardEffectDamage
{
    public override IEnumerator ApplyEffect(CardEffectState state,CardEffectParams parameters,ICoreGameManagers core,ISystemManagers systems)
    {
        var previous=parameters.selfTarget;
        var owner=ExcessCredit.Owner(parameters.playedCard);
        var victims=parameters.targets.Where(u=>!u.IsDestroyed&&u.IsAlive&&u.GetTeamType()==Team.Type.Heroes).Distinct().ToArray();
        // Native selfTarget -> attacker -> lastAttackerCharacter, Slay and Savagery.
        parameters.selfTarget=owner;
        try {
            var effects=base.ApplyEffect(state,parameters,core,systems);
            yield return parameters.playedCard is { } spell
                ?SpellReplay.WithSource(spell,core,parameters.selectedRoom,effects):effects;
        }
        finally { parameters.selfTarget=previous; }
        if(!core.GetSaveManager().PreviewMode&&!AcademicRuntime.Simulating)
            ExcessCredit.RewardHandOwner(parameters.playedCard,victims.Count(u=>(u.IsDestroyed||u.IsDead)&&!MineMineMine.Claimed.Contains(u)),core);
    }
}

[HarmonyPatch(typeof(CardManager),nameof(CardManager.AddNewCardCopy))]
internal static class ExcessCreditCopyHook
{
    static void Postfix(CardState sourceCardState,CardState __result)
    {
        if(!AcademicRuntime.Simulating&&__result!=null&&ExcessCredit.Origins.TryGetValue(sourceCardState,out var owner))
            ExcessCredit.Origins[__result]=owner;
    }
}
// 2026-10-24-1839||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ExcessCredit.cs||Conectar exceso de ataque con hechizo nativo; origen por carta, Slay/Savagery como Pathogens y copias; sin generación en preview
// 2026-10-24-1840||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ExcessCredit.cs||Exigir baja enemiga real antes de generar; impedir cartas por daño a aliados
// 2026-10-24-1843||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ExcessCredit.cs||Retirar detección y rango ligados a campeona; consumidor recibe portador, exceso y porcentaje del trigger
// 2026-10-24-1923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ExcessCredit.cs||Acumular en Crédito Extra en mano; crear solo si falta, permanente=true, daño permanente y autoría original
// 2026-10-24-1930||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ExcessCredit.cs||Evitar Savagery doble en creadora en mano si MineMineMine reclama la baja
// 2026-10-24-1946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ExcessCredit.cs||Permitir mejora nativa permanente del Crédito Extra creado o acumulado sin duplicar upgrades ni borrar traits previos
// 2026-10-25-1616||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ExcessCredit.cs||Generación de Crédito Extra recibe plantilla del efecto en vez de referencia global hardcoded
// 2026-10-27-2018||codex-ww-inc||salidas/build/ww-inc/src/ExcessCredit.cs||Conservar traits del Crédito Extra y autoría de su creadora en el mismo daño nativo
// 2026-10-27-2020||codex-ww-inc||salidas/build/ww-inc/src/ExcessCredit.cs||Mantener resolución nativa sin fuente cuando no existe carta; eliminar argumento nullable sin ocultar errores
