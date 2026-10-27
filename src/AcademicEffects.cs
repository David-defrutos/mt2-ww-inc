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
        string armor=s.GetParamStatusEffectStackData()[0].statusId;
        a.SetHealth(Math.Min(hpB,a.GetMaxHP()),a.GetMaxHP()); b.SetHealth(Math.Min(hpA,b.GetMaxHP()),b.GetMaxHP());
        if(hpB>a.GetMaxHP()) a.AddStatusEffect(armor,hpB-a.GetMaxHP());
        if(hpA>b.GetMaxHP()) b.AddStatusEffect(armor,hpA-b.GetMaxHP());
    }
}
public sealed class CardEffectAcademicSabbatical : CardEffectHeal
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>p.targets.Any(u=>!u.HasStatusEffect("untouchable"));
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        AcademicRuntime.Core=m;
        foreach(var unit in p.targets)
        {
            if(unit.HasStatusEffect("untouchable")) continue;
            var status=s.GetParamStatusEffectStackData()[0];
            int extra=GetHealAmount(s);
            var source=p.playedCard;
            unit.AddStatusEffect(status.statusId,status.count);
            EffectLifetimes.AtNextPlayerTurn(()=>Wake(unit,source,extra,status.statusId,status.count));
        }
        yield break;
    }
    static IEnumerator Wake(CharacterState unit,CardState? card,int extra,string status,int count)
    {
        if(unit.IsDestroyed || unit.IsDead) yield break;
        unit.RemoveStatusEffect(status,count,allowModification:false);
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
            var states=s.GetParamStatusEffectStackData();
            unit.AddStatusEffect(states[0].statusId,attack); AcademicRuntime.ZeroAttack.Add(unit); unit.AddStatusEffect(states[1].statusId,states[1].count);
            // Expires at the end of the next native combat round.
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
public sealed class CardEffectAcademicConduitTransfer : AcademicEffectBase,ICardEffectUiDialog
{
    public override bool CanApplyInPreviewMode=>true;
    public override bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)
        =>m.GetCardManager().GetHand().Any(c=>c!=p.playedCard&&c.GetCardType()==CardType.Spell);
    public override IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
    {
        if(m.GetSaveManager().PreviewMode)yield break;
        AcademicRuntime.Core=m;
        yield return HandSelection.ChooseHand(m,p.playedCard,c=>c.GetCardType()==CardType.Spell,chosen=> {
            AcademicRuntime.Core=m;
            int total=0;string status=s.GetParamStatusEffectStackData()[0].statusId;
            foreach(var unit in AcademicRuntime.AllAllies()) { int amount=unit.GetStatusEffectStacks(status); total+=amount; unit.RemoveStatusEffect(status,amount,allowModification:false); }
            AcademicRuntime.ModifyCard(chosen,total,0,false);
            var grant=s.GetParamCardUpgradeData()??throw new InvalidOperationException("Missing Explosive grant");
            CardTraitAcademicNextUseUpgrade.ReplaceCoveredBy(chosen,grant);
            SpellRules.GrantConfiguredTraits(chosen,grant);
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
            EffectLifetimes.AtNextPlayerTurn(()=>Remove(unit,upgrade));
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
// 2026-10-25-1557||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Sabbatical hereda curación nativa para escalar el término fijo con mejoras de Magic Power
// 2026-10-25-1616||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Resolver Stasis, Armor/Dazed, Conduit y rasgos con las mismas referencias JSON que usa la UI
// 2026-10-25-1617||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Pasar configuración de concesión Conduit al callback de selección
// 2026-10-25-1640||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Declarar ICardEffectUiDialog en efectos que abren selección de cartas
// 2026-10-25-2009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Usar selector nativo independiente con managers del efecto; conservar filtros y resolución
// 2026-10-25-2137||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Desk Swap solo coordina intercambio; Health Exchange lee Armor de metadatos JSON
// 2026-10-26-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Intern consume automáticamente el único hechizo elegible
// 2026-10-26-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Conduit Transfer sustituye concesión de próximo uso por Explosive duradero
// 2026-10-26-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicEffects.cs||Armored Honors caduca por ronda incluida Relentless
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/src/AcademicEffects.cs||Sabbatical captura curación modificada al lanzar; no relee power tras expirar Education/Conduit
// 2026-10-27-1528||codex-ww-inc||salidas/build/ww-inc/src/AcademicEffects.cs||Pasar sistemas al consumidor nativo de estados tras consumir crédito
// 2026-10-27-1528||codex-ww-inc||salidas/build/ww-inc/src/AcademicEffects.cs||Resolver Conduit dinámico mediante API nativa
// 2026-10-27-1528||codex-ww-inc||salidas/build/ww-inc/src/AcademicEffects.cs||Evitar cola pendiente al conceder Conduit y no interpretar multiplicador cinco como 5% de probabilidad
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/src/AcademicEffects.cs||Retirar clase extraída manteniendo nombre binario y referencias JSON
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/AcademicEffects.cs||Registrar vencimiento explícito sin cambiar instancias de mejoras ni duración aprobada
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/AcademicEffects.cs||Extraer coordinación de intercambio a efecto dedicado con contrato nativo de movimiento
