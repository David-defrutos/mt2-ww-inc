using System.Collections;
using System.Reflection;
using MonsterTrain2.SharedTriggers;
namespace HarmonyLib
{
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatchAttribute:Attribute
 {public HarmonyPatchAttribute(Type type,string method,Type[]? parameters=null){_=type;_=method;_=parameters;}}
}
public class CharacterTriggerData{public enum Trigger{None,Overheal,ArcaneSlay,Other}}
public enum CardType{Spell,Monster}
public static class Damage{public enum Type{Default,DirectAttack}}
public static class Team{public enum Type{None=0,Monsters=1,Heroes=2}}
public class CardState{public string Id="";public CardType Type=CardType.Spell;public bool Ability;public CardType GetCardType()=>Type;public bool IsAnyAbility()=>Ability;}
public class CharacterTriggerState(CharacterTriggerData.Trigger trigger){public CharacterTriggerData.Trigger GetTrigger()=>trigger;}
public class CardEffectParams{public CharacterState? selfTarget;public CharacterTriggerState? sourceCharacterTriggerState;}
public class CharacterState
{
 public bool HasTrigger=true;public bool HasEffectTrigger(CharacterTriggerData.Trigger trigger)=>HasTrigger;public bool IsDestroyed;public bool IsAlive=true;public bool IsDead=>!IsAlive;public Team.Type TeamType=Team.Type.Monsters;public Team.Type GetTeamType()=>TeamType;
 public struct ApplyDamageParams{public CardState? damageSourceCard;public Damage.Type damageType;}
 public class FireTriggersData{public int paramInt,paramInt2;public CharacterState? overrideTargetCharacter;}
 public void FireTriggers(){}
}
public interface ICoreGameManagers{CombatManager GetCombatManager();}
public class FakeCore:ICoreGameManagers{public CombatManager Combat=new();public CombatManager GetCombatManager()=>Combat;}
public class CombatManager
{
 public List<CharacterState> Listeners=new();
 public List<(CharacterState unit,CharacterTriggerData.Trigger trigger,CharacterState.FireTriggersData data)> Queued=new();
 public Func<CharacterState,CharacterTriggerData.Trigger,CharacterState.FireTriggersData,IEnumerator>? Body;
 public bool ThrowBeforeRoom;
 public void QueueTrigger(){}
 static IEnumerator Empty(){yield break;}
 public IEnumerator QueueAndRunTrigger(CharacterState unit,CharacterTriggerData.Trigger trigger,CharacterState.FireTriggersData fireTriggersData)
 {
  Queued.Add((unit,trigger,fireTriggersData));
  var original=Body?.Invoke(unit,trigger,fireTriggersData)??Empty();
  var hook=typeof(OverhealEvent).Assembly.GetType(trigger==OverhealEvent.Trigger?
      "MonsterTrain2.SharedTriggers.OverhealTriggerHook":"MonsterTrain2.SharedTriggers.ArcaneSlayTriggerHook")!;
  object?[] args=[unit,trigger,fireTriggersData,original];
  hook.GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
  yield return (IEnumerator)args[3]!;
 }
 public IEnumerator ApplyCharacterEffectsForRoom(CharacterTriggerData.Trigger trigger,int floor,int triggerCount)
 {
  _=floor;if(ThrowBeforeRoom)throw new InvalidOperationException("queue failed");
  foreach(var unit in Listeners)for(int i=0;i<triggerCount;i++)
  {
   var hook=typeof(ArcaneSlayEvent).Assembly.GetType("MonsterTrain2.SharedTriggers.ArcaneSlayQueueHook")!;
   object?[] args=[trigger,null];hook.GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
   yield return QueueAndRunTrigger(unit,trigger,(CharacterState.FireTriggersData)args[1]!);
  }
 }
}
// 2026-10-25-1850||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/TriggerEvents/NativeDoubles.cs||Dobles de API que ejecutan hooks reales de payload, cola y scopes sin referencias al clan
// 2026-10-26-2035||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/TriggerEvents/NativeDoubles.cs||Dobles de fuente y equipos para ejecutar registro Conductor
// 2026-10-26-2035||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/TriggerEvents/NativeDoubles.cs||Reproducir parámetros reales de impacto y baja
// 2026-10-26-2035||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/TriggerEvents/NativeDoubles.cs||Payload nativo con daño, tipo y objetivo

namespace WwInc
{
 internal static class AcademicRuntime{internal static bool Active=true;internal static int ConversionDepth;}
}
namespace Conductor.Extensions
{
 public struct TriggerOnCharacterHitParams
 {
  public CharacterState Character,DamagedCharacter;public int OriginalDamage;
  public CharacterState.ApplyDamageParams DamageParams;
 }
 public struct QueueTriggerParams{public CharacterState.FireTriggersData fireTriggersData;}
 public delegate bool TriggerOnCharacterHitDelegate(TriggerOnCharacterHitParams hit,out QueueTriggerParams? queued);
 public static class CharacterTriggerExtensions
 {
  public static TriggerOnCharacterHitDelegate? Registered;
  public static void SetToTriggerOnCharacterHit(this CharacterTriggerData.Trigger trigger,TriggerOnCharacterHitDelegate callback){Registered=callback;}
 }
}
// 2026-10-26-2035||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/TriggerEvents/NativeDoubles.cs||Dobles de firma del registro instalado Conductor 0.5.16
// 2026-10-27-1429||codex-ww-inc||salidas/build/ww-inc/tests/TriggerEvents/NativeDoubles.cs||Alinear flags de equipos del doble con juego para comprobar bajas aliadas
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/TriggerEvents/NativeDoubles.cs||Comprobar que Arcane Slay no encola eventos para unidades sin trigger
