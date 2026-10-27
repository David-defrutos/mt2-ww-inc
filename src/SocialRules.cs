using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WwInc;

internal static class SocialRules
{
    internal static CharacterData Chaperon=null!,Chaperona=null!;
    internal static SubtypeData Wizard=null!,Witch=null!,Chaperone=null!;
    internal static readonly Dictionary<CharacterState,int> AuraBonuses=new();
    internal static int Kind(CharacterState unit)=>(unit.GetHasSubtype(Wizard)?1:0)|(unit.GetHasSubtype(Witch)?2:0);
    internal static bool Mixed(CharacterState a,CharacterState b)=>AcademicMath.Mixed(Kind(a),Kind(b));
    internal static List<CharacterState> VisualUnits(int floor)=>AcademicRuntime.Units(floor).OrderBy(u=>u.GetSpawnPoint()?.GetPosition().x??0).ToList();
    internal static int PairPower(int floor,ICoreGameManagers core)
    {
        var units=UnitSpellPassives.Units(core,floor);int pairs=0;
        for(int i=0;i+1<units.Count;i++)if(Mixed(units[i],units[i+1]))pairs++;
        return pairs*15;
    }
    internal static IEnumerator BeginTurn()=>AcademicRelics.BeginTurn();
    internal static IEnumerator CreateSeparators()
    {
        if(Wizard==null)yield break;
        var core=AcademicRuntime.Core;
        foreach(int floor in FloorsBottomUp())
        {
            var room=core.GetRoomManager().GetRoom(floor);if(room==null||room.GetIsPyreRoom())continue;
            var units=VisualUnits(floor);
            for(int i=0;i+1<units.Count&&AcademicMath.CanInsertChaperone(units.Count);i++)
            {
                if(!Mixed(units[i],units[i+1]))continue;
                var raw=AcademicRuntime.Units(floor);int insertion=Math.Max(raw.IndexOf(units[i]),raw.IndexOf(units[i+1]));
                CharacterState? token=null;
                yield return core.GetMonsterManager().CreateMonsterState(units[i].GetHasSubtype(Wizard)?Chaperon:Chaperona,null!,floor,
                    created=>token=created,spawnMode:SpawnMode.BackSlot,isCardless:true);
                if(token==null)break;
                room.RearrangeCharacter(Team.Type.Monsters,raw.Count,insertion);room.UpdateSpawnPointPositions(Team.Type.Monsters);
                units=VisualUnits(floor);i++;
            }
        }
    }
    internal static IEnumerator TransferComplaints()
    {
        if(Wizard==null)yield break;
        foreach(int floor in FloorsBottomUp())
        {
            var units=VisualUnits(floor);
            var wizard=units.FirstOrDefault(u=>u.GetHasSubtype(Wizard));
            var witch=units.FirstOrDefault(u=>u!=wizard&&u.GetHasSubtype(Witch));
            var chaperone=units.FirstOrDefault(u=>u!=wizard&&u!=witch&&u.GetHasSubtype(Chaperone));
            if(wizard!=null&&witch!=null&&chaperone!=null)
            {
                CardEffectAcademicTransfer.TransferDebuffs(wizard,chaperone);
                CardEffectAcademicTransfer.TransferDebuffs(witch,chaperone);yield break;
            }
        }
    }
    internal static IEnumerable<int> FloorsBottomUp()=>Enumerable.Range(0,AcademicRuntime.Core.GetRoomManager().GetNumRooms())
        .Where(i=>AcademicRuntime.Core.GetRoomManager().GetRoom(i)!=null&&!AcademicRuntime.Core.GetRoomManager().GetRoom(i).GetIsPyreRoom())
        .OrderBy(i=>AcademicRuntime.Core.GetRoomManager().GetRoom(i).GetPosition().y);
    internal static void RefreshAuras()
    {
        if(!AcademicRuntime.Active||Wizard==null)return;
        var units=AcademicRuntime.AllAllies().ToArray();int witches=units.Count(u=>u.GetHasSubtype(Witch)),wizards=units.Count(u=>u.GetHasSubtype(Wizard));
        foreach(var unit in units)
        {
            if(!PreviewDependencies.CanWrite(unit,AcademicRuntime.Core))continue;
            bool wizard=AcademicRuntime.Is(unit,"WW_C01"),witch=AcademicRuntime.Is(unit,"WW_C02");
            if(!wizard&&!witch)continue;
            string prefix=wizard?"WW_FratBro":"WW_QueenBee";
            int rank=CardEffectAcademicWizardDraw.Rank(unit,prefix);
            int amount=AcademicMath.ChampionAura(rank,wizard?witches:wizards,wizard?5:1);int old=AuraBonuses.TryGetValue(unit,out int previous)?previous:0;
            old=Math.Min(old,unit.GetStatusEffectStacks(wizard?"conduit":"mageblade"));
            int delta=amount-old;if(delta>0)unit.AddStatusEffect(wizard?"conduit":"mageblade",delta,CharacterState.defaultAddStatusEffectParams,allowModification:false,skipDefaultStatusEffectNotifs:AcademicRuntime.Simulating||AcademicRuntime.Core.GetCombatManager().IsRunningCardPlayPreview);
            else if(delta<0)unit.RemoveStatusEffect(wizard?"conduit":"mageblade",-delta,allowModification:false);
            AuraBonuses[unit]=amount;
        }
    }
}

public sealed class RoomStateAcademicPower : RoomStateSpellUpgradeModifier
{
    protected override bool ApplyRoomUpgrade(CardState card,CardManager manager,RelicManager relics)
    {
        if(!RoomPowerEligibility.Affected(card))return false;
        var upgrade=GetParamCardUpgradeData();if(upgrade==null)return false;
        _cardsWeHaveModified.AddUpgradeToCard(card,upgrade,manager,relics);return true;
    }
}
public sealed class CardTraitAcademicPairPower : CardTraitState
{
    public override PropDescriptions CreateEditorInspectorDescriptions()=>PropDescriptions.DisplayNone;
    public override int GetMagicPowerAdditive(CardManager manager)
    {
        if(SocialRules.Wizard==null||!BattlePresentationContext.TryGet(out var core))return 0;
        int floor=SpellRules.EffectFloor>=0?SpellRules.EffectFloor:AcademicRuntime.Frames.Count>0?AcademicRuntime.Frames.Peek().Floor:core.GetRoomManager().GetSelectedRoom();
        return floor>=0?SocialRules.PairPower(floor,core):0;
    }
}
public sealed class RoomStateAcademicPlatonic : RoomStateModifierBase
{
}
// 2026-10-10-0003||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Correcciones de resolución, traits y acumulación de reglas
// 2026-10-10-0014||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Invocaciones y tríos recorren posiciones visuales de izquierda a derecha
// 2026-10-10-0016||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Restaurar auras vivas cuando sus estados hayan sido retirados
// 2026-10-10-0020||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Aplicar reglas verificadas a adyacencia y límite de siete unidades
// 2026-10-10-2359||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Aura dinámica con rango comprobado y protección contra desbordamiento

// 2026-10-11-0945||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Conservar reducción de talla y estadísticas de E02 al devolver y reinvocar al portador

// 2026-10-11-1032||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Frat Bro y Queen Bee leen también sendas de la carta del campeón

// 2026-10-11-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Awkward Silence solo añade mejora a hechizos con efecto que escala Magic Power

// 2026-10-12-0915||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Retirar por completo encogimiento y penalización -10/-10 del equipo reemplazado

// 2026-10-12-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Eliminar comprobación de tríos ya sin usuarios; reutilizar adyacencia Mixed existente

// 2026-10-12-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Conservar helper IsTrio usado por reliquia A09; la sala usa Mixed
// 2026-10-13-2124||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Office of Complaints exige tres tipos en mismo piso sin adyacencia; conserva recorrido determinista y una activación
// 2026-10-14-0021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Awkward Silence concede 15 Magic Power por pareja adyacente
// 2026-10-24-1835||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||Queen Bee Mageblade +1/+2/+3 por Wizard; Frat Bro conserva +5/+10/+15 Conduit
// 2026-10-26-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/SocialRules.cs||No notificar aura en predicción
// 2026-10-26-2045||codex-ww-inc||salidas/build/ww-inc/src/SocialRules.cs||No emitir avisos de aura con ninguna de las dos señales de preview
// 2026-10-27-0007||codex-ww-inc||salidas/build/ww-inc/src/SocialRules.cs||Awkward Silence: recibir gestores actuales explícitos al calcular parejas
// 2026-10-27-0007||codex-ww-inc||salidas/build/ww-inc/src/SocialRules.cs||Awkward Silence: no buscar RoomManager desde texto fuera de la escena principal
// 2026-10-27-1535||codex-ww-inc||salidas/build/ww-inc/src/SocialRules.cs||Bloquear escritura de auras sobre estado real mientras se prepara/restaura preview
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/SocialRules.cs||Separar creación de chaperones y transferencia de estados; mantener orden A07 antes de A09 mediante eventos de reliquia
// 2026-10-27-2021||codex-ww-inc||salidas/build/ww-inc/src/SocialRules.cs||Normalizar indentación de las dos operaciones sociales extraídas
