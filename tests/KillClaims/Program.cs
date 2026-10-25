using System.Collections;
using System.Reflection;
using System.Text.Json;
using WwInc;
var errors=new List<string>();int checks=0;
void Check(bool value,string text){checks++;if(!value)errors.Add(text);}
void Drain(IEnumerator sequence){while(sequence.MoveNext())if(sequence.Current is IEnumerator inner)Drain(inner);}
void Setup(params CharacterState[] units)
{
    AcademicRuntime.Allies=units.ToList();AcademicRuntime.Core=new Core();AcademicRuntime.Simulating=false;
    MineMineMine.Claimed.Clear();MineMineMine.Trigger=CharacterTriggerData.Trigger.Claim;
}
IEnumerator NativeDamage(CharacterState victim,CharacterState.ApplyDamageParams parameters,bool lethal=true)
{
    victim.SetNativeLastAttacker(parameters.attacker);victim.Dead=lethal;
    if(lethal&&parameters.attacker!=null)
        yield return AcademicRuntime.Core.GetCombatManager().QueueAndRunTrigger(parameters.attacker,CharacterTriggerData.Trigger.OnKill,victim);
    yield return MineMineMine.BeforeDeath(Empty(),victim);
    if(lethal)
    {
        object?[] args={victim,false,parameters.attacker};
        typeof(MineMineMineCreditHook).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
        victim.RecordedKiller=(CharacterState?)args[2];
    }
}
IEnumerator Empty(){yield break;}
CharacterState Victim(int floor=0)=>new("enemy",floor,0,Team.Type.Heroes);
void Hit(CharacterState victim,CharacterState? attacker=null,Damage.Type kind=Damage.Type.Default,CardState? spell=null,bool lethal=true)
{
    var parameters=new CharacterState.ApplyDamageParams {attacker=attacker,damageSourceCard=spell??new CardState(),damageType=kind};
    Drain(MineMineMine.ObserveDamage(NativeDamage(victim,parameters,lethal),victim,parameters));
}
var front=new CharacterState("ordinary front",0);var first=new CharacterState("first holder",0,1);var second=new CharacterState("rear holder",0,1);
Setup(front,first,second);var enemy=Victim();Hit(enemy);
Check(first.Kills==1&&second.Kills==0&&front.Kills==0,"An ordinary spell grants one Slay to the first holder, skipping an ordinary front unit");
Check(ReferenceEquals(enemy.RecordedKiller,first)&&ReferenceEquals(enemy.LastAttacker,first),"Native death accounting and last attacker both identify the winner");
Check(MineMineMine.Claimed.Contains(enemy),"A claimed death suppresses Extra Credit's hand-Savagery fallback");
Check(MineMineMine.Current(enemy)==null,"Damage context is disposed after resolution");
var creator=new CharacterState("spell creator on another floor",2);
first=new("front holder",0,1);second=new("rear holder",0,1);Setup(first,second,creator);enemy=Victim();Hit(enemy,creator);
Check(first.Kills==1&&second.Kills==0&&creator.Kills==0,"Attributed spells transfer credit rather than duplicating Slay on their creator");
Check(ReferenceEquals(enemy.RecordedKiller,first),"Attributed spell's native death statistics transfer to the claimant");
var otherFloor=new CharacterState("other floor",1,1);Setup(otherFloor);enemy=Victim();Hit(enemy);
Check(otherFloor.Kills==0&&!MineMineMine.Claimed.Contains(enemy)&&enemy.RecordedKiller==null,"A holder on another floor cannot claim the kill");
creator=new("native creator",2);Setup(creator);enemy=Victim();Hit(enemy,creator);
Check(creator.Kills==1&&ReferenceEquals(enemy.RecordedKiller,creator),"Without a holder the original native credit is preserved");
first=new("duplicate effect holder",0,2);Setup(first);enemy=Victim();Hit(enemy);
Check(first.Kills==1,"Multiple copies of the claim effect grant only one Slay for the event");
first=new("holder",0,1);creator=new("attacking unit",0);Setup(first,creator);enemy=Victim();Hit(enemy,creator,Damage.Type.DirectAttack);
Check(first.Kills==0&&creator.Kills==1&&!MineMineMine.Claimed.Contains(enemy),"A direct attack keeps ordinary Slay attribution");
first=new("holder",0,1);Setup(first);enemy=Victim();Hit(enemy,lethal:false);
Check(first.Kills==0&&!MineMineMine.Claimed.Contains(enemy),"Nonlethal spells do not claim a kill");
first=new("holder",0,1);creator=new("ability owner",0);Setup(first,creator);enemy=Victim();Hit(enemy,creator,spell:new CardState {Ability=true});
Check(first.Kills==0&&creator.Kills==1,"Unit abilities remain outside spell-kill claiming");
first=new("holder",0,1);Setup(first);enemy=Victim();Hit(enemy,kind:Damage.Type.SpellOverflow);
Check(first.Kills==1&&ReferenceEquals(enemy.RecordedKiller,first),"Spell overflow uses the same floor claimant");
first=new("dead front holder",0,1){Dead=true};second=new("living rear holder",0,1);Setup(first,second);enemy=Victim();Hit(enemy);
Check(first.Kills==0&&second.Kills==1,"A dead front holder cannot prevent the next living holder from claiming");
first=new("floor zero",0,1);second=new("floor one",1,1);Setup(first,second);Hit(Victim(1));Hit(Victim(0));
Check(first.Kills==1&&second.Kills==1,"Separate spell deaths keep independent floor attribution");
var claimedBefore=MineMineMine.Claimed.Count;AcademicRuntime.Simulating=true;Hit(Victim(0));AcademicRuntime.Simulating=false;
Check(MineMineMine.Claimed.Count==claimedBefore,"Preview cannot mutate real claimed-death receipts");
Console.WriteLine(JsonSerializer.Serialize(new {success=errors.Count==0,checks,errors},new JsonSerializerOptions{WriteIndented=true}));
return errors.Count==0?0:1;

public enum CardType {Spell,Blight}
public static class Damage {public enum Type {Default,DirectAttack,SpellOverflow}}
public static class Team {public enum Type {Monsters,Heroes}}
public sealed class CardState
{
    public bool Ability;
    public CardType GetCardType()=>CardType.Spell;
    public bool IsAnyAbility()=>Ability;
}
public sealed class CharacterTriggerData {public enum Trigger {OnKill,Claim}}
public sealed class CharacterTriggerState
{
    public CharacterTriggerData.Trigger Kind;
    public CharacterTriggerData.Trigger GetTrigger()=>Kind;
}
public sealed class CharacterState
{
    public class FireTriggersData { }
    public struct ApplyDamageParams {public CharacterState? attacker;public CardState? damageSourceCard;public Damage.Type damageType;}
    private sealed class StateInformation {public CharacterState? lastAttackerCharacter;}
    private StateInformation PrimaryStateInformation {get;}=new();
    public CharacterState(string name,int floor,int holders=0,Team.Type team=Team.Type.Monsters){Name=name;Floor=floor;HolderCopies=holders;TeamValue=team;}
    public string Name;public int Floor,HolderCopies,Kills;public bool Dead,IsDestroyed;
    public bool IsDead=>Dead;
    public bool IsAlive=>!Dead&&!IsDestroyed;
    public CharacterState? RecordedKiller;
    public CharacterState? LastAttacker=>PrimaryStateInformation.lastAttackerCharacter;
    public Team.Type TeamValue;
    public Team.Type GetTeamType()=>TeamValue;
    public int GetCurrentRoomIndex()=>Floor;
    public bool HasEffectTrigger(CharacterTriggerData.Trigger trigger)=>trigger==CharacterTriggerData.Trigger.Claim&&HolderCopies>0;
    public void SetNativeLastAttacker(CharacterState? unit)=>PrimaryStateInformation.lastAttackerCharacter=unit;
    public void ApplyDamage(){}public void CheckDeathCardTriggers(){}public void FireTriggers(){}
}
public sealed class CardEffectState{}
public sealed class CardEffectParams
{
    public CharacterState? selfTarget;
    public CharacterTriggerState? sourceCharacterTriggerState;
}
public interface ICoreGameManagers {CombatManager GetCombatManager();}
public interface ISystemManagers{}
public sealed class Core:ICoreGameManagers
{
    readonly CombatManager combat=new();
    public CombatManager GetCombatManager()=>combat;
}
public sealed class CombatManager
{
    readonly Queue<(CharacterState unit,CharacterTriggerData.Trigger trigger,CharacterState? victim,CharacterState.FireTriggersData? data)> queue=new();
    bool running;
    public void QueueTrigger(){}
    public IEnumerator QueueAndRunTrigger(CharacterState unit,CharacterTriggerData.Trigger trigger,CharacterState? dyingCharacter=null,CharacterState.FireTriggersData? fireTriggersData=null)
    {
        var allow=(bool)typeof(MineMineMineSlayHook).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{trigger,dyingCharacter})!;
        if(allow)queue.Enqueue((unit,trigger,dyingCharacter,fireTriggersData));
        if(running)yield break;
        running=true;
        try
        {
            while(queue.Count>0)
            {
                var next=queue.Dequeue();
                if(next.trigger==CharacterTriggerData.Trigger.OnKill)next.unit.Kills++;
                else if(next.data is MineMineMine.Data data)
                    yield return MineMineMine.Execute(Fire(next.unit),next.unit,data);
            }
        }
        finally {running=false;}
    }
    IEnumerator Fire(CharacterState unit)
    {
        for(int i=0;i<unit.HolderCopies;i++)
            yield return new CardEffectMineMineMineClaim().ApplyEffect(new CardEffectState(),
                new CardEffectParams {selfTarget=unit,sourceCharacterTriggerState=new CharacterTriggerState {Kind=CharacterTriggerData.Trigger.Claim}},AcademicRuntime.Core,null!);
    }
}
namespace WwInc
{
    public abstract class AcademicEffectBase
    {
        public virtual bool CanApplyInPreviewMode=>false;
        public virtual bool CanPlayAfterBossDead=>false;
        public abstract bool TestEffect(CardEffectState state,CardEffectParams parameters,ICoreGameManagers core);
        public abstract IEnumerator ApplyEffect(CardEffectState state,CardEffectParams parameters,ICoreGameManagers core,ISystemManagers systems);
    }
    internal static class AcademicRuntime
    {
        internal static ICoreGameManagers Core=new Core();
        internal static bool Active=true,Simulating;
        internal static List<CharacterState> Allies=new();
        internal static List<CharacterState> Units(int floor)=>Allies.Where(u=>u.Floor==floor&&u.IsAlive).ToList();
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch:Attribute
    {
        public HarmonyPatch(Type type,string name){}
        public HarmonyPatch(Type type,string name,Type[] signature){}
    }
    public static class AccessTools
    {
        public static PropertyInfo Property(Type type,string name)=>type.GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!;
        public static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!;
    }
}
// 2026-10-24-1933||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/KillClaims/Program.cs||Banco de cola reentrante y daño con código real MineMineMine; casos frontal, origen propio, pisos, preview y Slay único
