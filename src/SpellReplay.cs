using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
namespace WwInc;

// Only the missing context: native ApplyEffects and native targeting still execute the spell.
internal static class SpellReplay
{
    internal sealed class Context
    {
        internal CardState Card=null!;
        internal ICoreGameManagers Core=null!;
        internal int Floor;
    }
    internal static readonly Stack<Context> Casts=new();
    private static readonly Stack<(RoomManager rooms,int floor)> TargetFloors=new();
    internal static void BeginTargeting(ICoreGameManagers core,int floor)=>TargetFloors.Push((core.GetRoomManager(),floor));
    internal static void EndTargeting()=>TargetFloors.Pop();
    internal static int SelectedRoom(RoomManager rooms,int original)
        =>TargetFloors.Count>0&&ReferenceEquals(TargetFloors.Peek().rooms,rooms)?TargetFloors.Peek().floor:original;
    internal static CardState? Responsible(CardState? source,CardState? original)
        =>Casts.Count>0&&ReferenceEquals(source,Casts.Peek().Card)&&source!=null&&
            source.GetCardType()==CardType.Spell&&!source.IsAnyAbility()?source:original;
    internal static IEnumerator Cast(CardState card,int floor,CharacterState target,CharacterState? owner=null)
    {
        var core=AcademicRuntime.Core;
        yield return WithSource(card,core,floor,core.GetCombatManager().ApplyEffects(card.GetEffectStates(),floor,card,
            selfTarget:owner,overrideTargetCharacter:target));
    }
    // Author and source card are independent: keep native kill credit without borrowing unit traits.
    internal static IEnumerator WithSource(CardState card,ICoreGameManagers core,int floor,IEnumerator effects)
    {
        Casts.Push(new Context{Card=card,Floor=floor,Core=core});
        try {yield return effects;}
        finally {Casts.Pop();}
    }
    internal static CharacterState? FrontEnemy(ICoreGameManagers core,int floor,List<CharacterState>? skip=null)
    {
        var data=new TargetHelper.CollectTargetsData();data.Reset(TargetMode.Room);
        data.targetTeamType=Team.Type.Heroes;data.roomIndex=floor;data.inCombat=true;
        data.ignorePyre=true;data.isCardEffectDamage=true;data.skipCharacters=skip;
        var targets=new List<CharacterState>();
        BeginTargeting(core,floor);
        try {TargetHelper.CollectTargets(data,core,ref targets);}
        finally {EndTargeting();}
        return targets.FirstOrDefault(unit=>!unit.IsDestroyed&&unit.IsAlive);
    }
    internal static IEnumerator PreserveView(IEnumerator effects)
    {
        var core=AcademicRuntime.Core;var rooms=core.GetRoomManager();int selected=rooms.GetSelectedRoom();
        yield return effects;
        var originalRoom=rooms.GetRoom(selected);
        if(core.GetSaveManager().PreviewMode||!core.GetCombatManager().IsPlayerActionPhase()||
            originalRoom==null||!originalRoom.IsRoomEnabled()||rooms.GetSelectedRoom()==selected)yield break;
        yield return rooms.GetRoomUI().SetSelectedRoom(selected);
    }
}
[HarmonyPatch(typeof(CombatManager),nameof(CombatManager.GetResponsibleCard))]
internal static class ReplayResponsibleCardHook
{
    static void Postfix(CardState damageSourceCard,ref CardState __result)
        =>__result=SpellReplay.Responsible(damageSourceCard,__result)!;
}
[HarmonyPatch(typeof(RoomManager),nameof(RoomManager.GetSelectedRoom))]
internal static class ReplayTargetFloorHook
{
    static void Postfix(RoomManager __instance,ref int __result)=>__result=SpellReplay.SelectedRoom(__instance,__result);
}
[HarmonyPatch(typeof(TargetHelper),nameof(TargetHelper.CollectTargets),
    new[]{typeof(CardEffectState),typeof(CardEffectParams),typeof(ICoreGameManagers),typeof(bool)})]
internal static class ReplayTargetCollectionScope
{
    static void Prefix(CardEffectParams cardEffectParams,out bool __state)
    {
        __state=false;
        if(SpellReplay.Casts.Count==0||!ReferenceEquals(cardEffectParams.playedCard,SpellReplay.Casts.Peek().Card))return;
        var cast=SpellReplay.Casts.Peek();SpellReplay.BeginTargeting(cast.Core,cast.Floor);__state=true;
    }
    static void Finalizer(bool __state){if(__state)SpellReplay.EndTargeting();}
}
// 2026-10-27-1529||codex-ww-inc||salidas/build/ww-inc/src/SpellReplay.cs||Separar carta responsable, objetivo y piso lógico de repeticiones; delegar filtros, daño y cámara al motor
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/src/SpellReplay.cs||Recoger candidatos válidos antes de excluir objetivos procesados y elegir frente sin perder salto
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/src/SpellReplay.cs||Capturar sala original una vez y evitar lecturas repetidas o nulas al restaurar cámara
// 2026-10-27-1543||codex-ww-inc||salidas/build/ww-inc/src/SpellReplay.cs||Cerrar ámbito de targeting únicamente si llegó a abrirse correctamente
// 2026-10-27-2018||codex-ww-inc||salidas/build/ww-inc/src/SpellReplay.cs||Compartir ámbito seguro de fuente entre repeticiones y hechizos con autor nativo
