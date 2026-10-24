using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace WwInc;

public abstract class AcademicEffectBase : CardEffectBase
{
    public override bool CanApplyInPreviewMode=>false;
    public override PropDescriptions CreateEditorInspectorDescriptions()=>PropDescriptions.DisplayNone;
}

public sealed class CardEffectAcademicTransfer : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>p.targets.Count>0;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m; var target=p.targets[0]; string operation=s.GetParamStr();
        if(operation=="weakness")
        {
            var enemies=new List<CharacterState>(); m.GetHeroManager().AddCharactersToList(enemies);
            int total=enemies.Sum(u=>u.GetStatusEffectStacks("spell weakness"));
            foreach(var u in enemies) u.RemoveStatusEffect("spell weakness",u.GetStatusEffectStacks("spell weakness"),allowModification:false);
            target.AddStatusEffect("spell weakness",total,allowModification:false);
        }
        else if(operation=="debuffs")
        {
            foreach(var source in AcademicRuntime.Units(target.GetCurrentRoomIndex()).Where(u=>u!=target))
                TransferDebuffs(source,target);
        }
        else throw new InvalidOperationException("Unknown academic transfer: "+operation);
        yield break;
    }
    internal static void TransferDebuffs(CharacterState source,CharacterState target)
    {
        var stacks=new List<CharacterState.StatusEffectStack>(); source.GetStatusEffects(ref stacks);
        foreach(var stack in stacks)
        {
            var status=stack.State.GetSourceStatusEffectData();
            if(stack.State.GetDisplayCategory()!=StatusEffectData.DisplayCategory.Negative) continue;
            int amount=stack.Count; string id=stack.State.GetStatusId();
            source.RemoveStatusEffect(id,amount,allowModification:false);
            target.AddStatusEffect(id,amount,allowModification:false);
        }
    }
}
public sealed class CardEffectAcademicHealthExchange : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
    { AcademicRuntime.Core=m; return AcademicRuntime.Units(p.selectedRoom).Count>=2; }
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m; var units=AcademicRuntime.Units(p.selectedRoom); if(units.Count<2) yield break;
        var a=units[0]; var b=units[units.Count-1]; int hpA=a.GetHP(),hpB=b.GetHP();
        a.SetHealth(Math.Min(hpB,a.GetMaxHP()),a.GetMaxHP()); b.SetHealth(Math.Min(hpA,b.GetMaxHP()),b.GetMaxHP());
        if(hpB>a.GetMaxHP()) a.AddStatusEffect("armor",hpB-a.GetMaxHP());
        if(hpA>b.GetMaxHP()) b.AddStatusEffect("armor",hpA-b.GetMaxHP());
    }
}
public sealed class CardEffectAcademicDeskSwap : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
    { AcademicRuntime.Core=m; return AcademicRuntime.Units(p.selectedRoom).Count>=2; }
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m; var units=AcademicRuntime.Units(p.selectedRoom); if(units.Count<2) yield break;
        var rear=units[units.Count-1];
        var room=p.GetSelectedRoom(m.GetRoomManager())!;
        room.RearrangeCharacter(Team.Type.Monsters,0,units.Count-1);
        room.RearrangeCharacter(Team.Type.Monsters,units.Count-2,0);
        room.UpdateSpawnPointPositions(Team.Type.Monsters);
        yield return m.GetCombatManager().RunUnitTurn(rear,m.GetMonsterManager(),m.GetHeroManager(),
            room.GetRoomIndex(),allowTheft:false,null,ignoreAttackPrevention:true);
    }
}
public sealed class CardEffectAcademicSabbatical : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>p.targets.Any(u=>!u.HasStatusEffect("untouchable"));
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m;
        foreach(var unit in p.targets)
        {
            if(unit.HasStatusEffect("untouchable")) continue;
            unit.AddStatusEffect("untouchable",1);
            AcademicRuntime.NextTurnActions.Add(()=>Wake(unit,p.playedCard,s.GetParamInt()));
        }
        yield break;
    }
    static IEnumerator Wake(CharacterState unit,CardState? card,int extra)
    {
        if(unit.IsDestroyed || unit.IsDead) yield break;
        unit.RemoveStatusEffect("untouchable",1,allowModification:false);
        yield return unit.ApplyHeal(Math.Max(0,unit.GetMaxHP()-unit.GetHP())/2+extra,responsibleCard:card);
    }
}
public sealed class CardEffectAcademicArmorHonors : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m;
        foreach(var unit in p.targets)
        {
            int attack=unit.GetAttackDamage(); 
            unit.AddStatusEffect("armor",attack); AcademicRuntime.ZeroAttack.Add(unit); unit.AddStatusEffect("dazed",1);
            AcademicRuntime.EndTurnActions.Add(()=> { if(!unit.IsDestroyed) AcademicRuntime.ZeroAttack.Remove(unit); });
        }
        yield break;
    }
}
public sealed class CardEffectAcademicConversion : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m; var unit=p.characterThatActivatedAbility??p.selfTarget;
        if(unit!=null) AcademicRuntime.HealingModes[unit]=s.GetParamInt();
        yield break;
    }
}
public sealed class CardEffectAcademicConsumeCredits : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>m.GetCardManager().GetHand().Any(c=>c!=p.playedCard&&c.GetCardType()==CardType.Spell);
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;var unit=p.characterThatActivatedAbility??p.selfTarget;
        if(unit==null)yield break;
        yield return AcademicRuntime.ChooseHand(p.playedCard,c=>c.GetCardType()==CardType.Spell,
            new Func<CardState,IEnumerator>(chosen=>ResolveCredits(chosen,unit,m)));
    }
    private static IEnumerator ResolveCredits(CardState chosen,CharacterState unit,ICoreGameManagers m)
    {
        AcademicRuntime.Core=m;
        int conduit=Math.Max(0,chosen.GetCostWithoutAnyModifications())*5;
        yield return m.GetCardManager().ConsumeCardWithoutPlaying(new CardManager.DiscardCardParams { discardCard=chosen,overrideDiscardEffect=HandUI.DiscardEffect.Exhausted });
        unit.AddStatusEffect("conduit",conduit);
    }
}
public sealed class CardEffectAcademicConduitTransfer : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>m.GetCardManager().GetHand().Any(c=>c!=p.playedCard&&c.GetCardType()==CardType.Spell);
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;
        yield return AcademicRuntime.ChooseHand(p.playedCard,c=>c.GetCardType()==CardType.Spell,chosen=> {
            AcademicRuntime.Core=m;
            int total=0;
            foreach(var unit in AcademicRuntime.AllAllies()) { int amount=unit.GetStatusEffectStacks("conduit"); total+=amount; unit.RemoveStatusEffect("conduit",amount,allowModification:false); }
            AcademicRuntime.ModifyCard(chosen,total,0,false);SpellRules.Explosive(chosen);
        });
    }
}
public sealed class CardEffectAcademicEquipmentReturn : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m;
        foreach(var unit in p.targets)
        {
            var equipment=unit.GetEquipment().FirstOrDefault(); if(equipment==null) continue;
            bool preview=m.GetSaveManager().PreviewMode;
            if(!preview)equipment.SetRemoveFromStandByPileOverride(CardPile.HandPile);
            if(preview)AcademicRuntime.EquipmentPreviewDepth++;
            try { yield return unit.RemoveEquipment(m,equipment); }
            finally { if(preview)AcademicRuntime.EquipmentPreviewDepth--; }
            if(!preview)AcademicRuntime.SetFixedCost(equipment,0,true);
            unit.AddStatusEffect("damage shield",1);
        }
    }
}
public sealed class CardEffectAcademicOneTurnUpgrade : AcademicEffectBase
{
    public override bool CanApplyInPreviewMode=>true;
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m;
        foreach(var unit in p.targets)
        {
            var upgrade=new CardUpgradeState(); upgrade.Setup(s.GetParamCardUpgradeData());
            yield return unit.ApplyCardUpgrade(upgrade);
            AcademicRuntime.NextTurnActions.Add(()=>Remove(unit,upgrade));
        }
    }
    static IEnumerator Remove(CharacterState unit,CardUpgradeState upgrade)
    { if(!unit.IsDestroyed) yield return unit.RemoveCardUpgrade(upgrade); }
}
// 2026-10-10-0003||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Correcciones de resoluciÃ³n, traits y acumulaciÃ³n de reglas
// 2026-10-10-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Consumir sin resolver usando la pila real de Consume
// 2026-10-10-0014||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Evitar sustituir gestores reales durante la previsualización
// 2026-10-10-0016||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Coste cero temporal exacto al recuperar equipo
// 2026-10-10-2358||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Previsualizar transferencias, salud, Stasis, armadura, conversión y escudo
// 2026-10-11-0003||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Simular intercambio de posiciones usando SpawnPointGroup temporal nativo
// 2026-10-11-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Previsualizar retirada de bonos y Damage Shield sin mover la carta de equipo real


// 2026-10-11-0941||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Validar selectores sin ejecutarlos en preview; reducir recarga restante mediante estado cooldown

// 2026-10-12-0036||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Desk Swap deja Armor intacto y ejecuta ataque inmediato nativo del trasero seguido de Dazed 1

// 2026-10-12-0925||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Retirar Dazed de Desk Swap conservando intercambio y ataque inmediato

// 2026-10-12-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Créditos y transferencia Conduit resuelven sobre elección confirmada en callback nativo

// 2026-10-12-1712||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Eliminar efecto C# de recarga reemplazado por efecto nativo
// 2026-10-12-2322||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Sabbatical: sumar 10 a la mitad de salud faltante al salir de Stasis
// 2026-10-13-2353||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Retirar selector propio de Freeze y antiguo vínculo a una sola carta; usar efecto oficial
