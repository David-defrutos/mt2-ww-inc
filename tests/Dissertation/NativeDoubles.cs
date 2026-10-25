using System.Collections;
using System.Reflection;
namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatchAttribute:Attribute{public HarmonyPatchAttribute(Type t,string method){_=t;_=method;}} }
namespace ShinyShoe { public static class Loc { public static string Localize(this string key)=>key; } }
[Flags]public enum Teams { None=0,Monsters=1,Heroes=2 }
public static class Team { [Flags] public enum Type{None=0,Monsters=1,Heroes=2} }
public enum TargetMode{Self,Room,DropTargetCharacter,FrontInRoom,RandomInRoom}
public class CardData{public string Id="";}
public class CardState(CardData data,SaveManager save)
{
 public CardData Data=data;public SaveManager Save=save;public List<CardEffectState> Effects=new();
 public List<CardEffectState> GetEffectStates()=>Effects;
 public Team.Type GetTargetedTeamType()=>Effects.Aggregate(Team.Type.None,(a,e)=>a|e.Team);
}
public class CharacterState{public bool IsDestroyed,IsDead;}
public interface ICardEffectUiDialog{}
public class DialogEffect:CardEffectBase,ICardEffectUiDialog{}
public class CardEffectBase{public virtual bool HasRandomTarget(CardEffectState effect,CombatManager combat)=>effect.Mode==TargetMode.RandomInRoom;}
public class RandomTargetEffect:CardEffectBase{public override bool HasRandomTarget(CardEffectState effect,CombatManager combat)=>true;}
public class CardEffectDamage:CardEffectBase{}
public class CardEffectHeal:CardEffectBase{}
public class CardEffectHealAndDamageRelative:CardEffectHeal{}
public class CardEffectRewardGold:CardEffectBase{}
public class CardEffectAddStatusEffect:CardEffectBase{}
public class CardEffectBuffDamage:CardEffectBase{}
public class CardEffectBuffMaxHealth:CardEffectBase{}
public class CardEffectDebuffDamage:CardEffectBase{}
public class CardEffectDebuffMaxHealth:CardEffectBase{}
public class StatusEffectStackData{public string statusId="";public int count;}
public class StatusEffectData
{
 public enum DisplayCategory{Positive,Negative,Persistent}
 public DisplayCategory Category;public DisplayCategory GetDisplayCategory()=>Category;
}
public class StatusEffectManager
{
 public static StatusEffectManager Instance=new();public Dictionary<string,StatusEffectData> Data=new();
 public StatusEffectData? GetStatusEffectDataById(string id)=>Data.GetValueOrDefault(id);
}
public class CardEffectState
{
 public CardEffectBase Effect=new();public Team.Type Team;public TargetMode Mode=TargetMode.DropTargetCharacter;
 public int Int=1;public CardState Parent=null!;public StatusEffectStackData[] Statuses=[];
 public bool HasRandomTarget(CombatManager combat)=>Effect.HasRandomTarget(this,combat);
 public CardEffectBase GetCardEffect()=>Effect;public Team.Type GetTargetTeamType()=>Team;
 public TargetMode GetTargetMode()=>Mode;public int GetParamInt()=>Int;
 public StatusEffectStackData[] GetParamStatusEffectStackData()=>Statuses;
 public CardState GetParentCardState()=>Parent;
}
public class SaveManager{}
public interface ICoreGameManagers{SaveManager GetSaveManager();CardManager GetCardManager();CombatManager GetCombatManager();}
public interface ISystemManagers{}
public class FakeCore:ICoreGameManagers,ISystemManagers
{
 public CardManager Cards=new();public CombatManager Combat=new();public SaveManager Save=new();
 public SaveManager GetSaveManager()=>Save;public CardManager GetCardManager()=>Cards;public CombatManager GetCombatManager()=>Combat;
}
public class CardManager
{
 public List<CardState> Hand=new(),Standby=new();public List<CardState> GetHand()=>Hand;
 public void MoveToStandByPile(CardState c,bool a,bool b){Hand.Remove(c);Standby.Add(c);}
}
public static class TargetHelper{public static void CheckTargetsOverride(){} }
public class CombatManager
{
 public List<(CardEffectState effect,CharacterState? target,CharacterState self)> Calls=new();public bool Throw;
 public IEnumerator ApplyEffects(List<CardEffectState> effects,int floor,CardState card,CharacterState selfTarget,CharacterState overrideTargetCharacter)
 {
  if(Throw)throw new InvalidOperationException("native resolution failed");
  foreach(var effect in effects)
  {
   var targets=new List<CharacterState>();object?[] args=[effect,targets,overrideTargetCharacter];
   var original=(bool)typeof(WwInc.DissertationTargetOverride).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args)!;
   if(!original)continue;
   Calls.Add((effect,effect.Mode==TargetMode.DropTargetCharacter?(CharacterState)args[2]!:null,selfTarget));
  }
  yield break;
 }
}
namespace WwInc
{
 internal static class AcademicRuntime
 {
  internal sealed class SpellResolution{public CardState Card=null!;public int Floor;public CharacterState Target=null!;public bool Indirect;}
  internal static Stack<SpellResolution> Frames=new();internal static ICoreGameManagers Core=null!;
  internal static IEnumerator AfterSpell(SpellResolution f){yield break;}
 }
 internal static class DissertationMarker{internal static void Refresh(CharacterState unit){} }
 internal static class SpellRules
 {
  internal static Dictionary<CardState,Team.Type> BoundSides=new();internal static HashSet<CardState> ReservedCards=new();
  internal static Dictionary<CharacterState,List<(CardState card,int attacks)>> BoundAttacks=new();
  internal static int EffectFloor=-1;internal static bool ChooseAllies=true;internal static List<CardState>? Shown;
  internal static bool HealingEffect(CardEffectState effect)=>effect.Effect is CardEffectHeal;
  internal static IEnumerator SelectPile(ISystemManagers systems,TargetMode pile,Func<CardState,bool> eligible,Action<CardState> picked,bool cancel,List<CardState> candidates,string instructions)
  {Shown=candidates;picked(candidates[ChooseAllies?0:1]);yield break;}
 }
}
// 2026-10-24-2102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/NativeDoubles.cs||Dobles de APIs nativas para probar clasificación, elección, alcance y limpieza ante fallo
// 2026-10-24-2102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/NativeDoubles.cs||Eliminar advertencias del arnés y conservar parámetros de construcción nativa
// 2026-10-24-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/NativeDoubles.cs||Reflejar herencia nativa del efecto mixto de curación y daño
// 2026-10-25-1641||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/NativeDoubles.cs||Reflejar contrato UI nativo con doble de efecto interactivo
// 2026-10-25-1832||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/NativeDoubles.cs||Reflejar detección nativa de objetivos aleatorios y override de efectos
