using System;
using System.Collections.Generic;
using System.Linq;
namespace WwInc;

// Owns only this passive's attribution and remainder. Discount application stays with the caller.
internal static class FrozenTuition
{
    internal static CharacterData Source=null!;
    internal static readonly Dictionary<CharacterState,int> Remainders=new();

    internal static void Reset()=>Remainders.Clear();

    internal static void RecordDamage(ICoreGameManagers core,CharacterState? attacker,int lost,
        Action<CardState,int> applyDiscount)
    {
        if(lost<=0||attacker==null||Source==null||attacker.GetSourceCharacterData()!=Source||
            core.GetSaveManager().PreviewMode||core.GetCombatManager().IsRunningCardPlayPreview)return;
        var credit=AcademicMath.Tuition(Remainders.TryGetValue(attacker,out var previous)?previous:0,lost);
        Remainders[attacker]=credit.remainder;
        if(credit.discounts==0)return;
        // Freeze is checked now, not when the ability was used. Includes every card type.
        foreach(var card in core.GetCardManager().GetHand().Where(card=>card.HasTrait(typeof(CardTraitFreeze))).ToArray())
            applyDiscount(card,credit.discounts);
    }
}
// 2026-10-25-2036||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/FrozenTuition.cs||Separar acumulación por unidad y selección de cartas Frozen con contexto explícito y personaje registrado
