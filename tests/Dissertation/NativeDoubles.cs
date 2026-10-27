using System.Collections;
using System.Reflection;
namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatchAttribute:Attribute{public HarmonyPatchAttribute(Type t,string method,Type[]? args=null){_=t;_=method;_=args;}} }
namespace ShinyShoe { public static class Loc { public static string Localize(this string key)=>key=="david.wwinc.ReservedAttacks"?"Remaining attacks: {0}":key; } }
[Flags]public enum Teams { None=0,Monsters=1,Heroes=2 }
public static class Team { [Flags] public enum Type{None=0,Monsters=1,Heroes=2} }
public enum TargetMode{Self,Room,DropTargetCharacter,FrontInRoom,RandomInRoom}
public class CardData{public string Id=Guid.NewGuid().ToString();}
public class CardState(CardData data,SaveManager save)
{
 public string Body="";public string GetCardDataID()=>Data.Id;public string GetTitle()=>Data.Id;
 public CardData Data=data;public SaveManager Save=save;public List<CardEffectState> Effects=new();
 public List<CardEffectState> GetEffectStates()=>Effects;
 public CardType GetCardType()=>CardType.Spell;public bool IsAnyAbility()=>false;
 public Team.Type GetTargetedTeamType()=>Effects.Aggregate(Team.Type.None,(a,e)=>a|e.Team);
}
public class CharacterState
{
 public bool IsDestroyed,IsDead;public bool IsAlive=>!IsDead;public Dictionary<string,int> Statuses=new();
 public int GetStatusEffectStacks(string id)=>Statuses.GetValueOrDefault(id);
 public void AddStatusEffect(string id,int amount,bool allowModification=true)=>Statuses[id]=GetStatusEffectStacks(id)+amount;
 public void RemoveStatusEffect(string id,int amount,bool allowModification=true)=>Statuses[id]=GetStatusEffectStacks(id)-amount;
}
public interface ICardEffectUiDialog{}
public class DialogEffect:CardEffectBase,ICardEffectUiDialog{}
public class CardEffectBase{public virtual bool HasRandomTarget(CardEffectState effect,CombatManager combat)=>effect.Mode==TargetMode.RandomInRoom;}
public class RandomTargetEffect:CardEffectBase{public override bool HasRandomTarget(CardEffectState effect,CombatManager combat)=>true;}
public class CardEffectDamage:CardEffectBase{}
public class CardEffectHeal:CardEffectBase{}
public class CardEffectHealAndDamageRelative:CardEffectHeal{}
public class CardEffectRewardGold:CardEffectBase{}
public class CardEffectAdjustDragonsHoard:CardEffectBase{}
public class CardEffectDraw:CardEffectBase{}
public class CardEffectDrawType:CardEffectBase{}
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
public class SaveManager{public bool PreviewMode;}
public enum CardType{Spell,Monster}
public interface ICoreGameManagers{SaveManager GetSaveManager();CardManager GetCardManager();CombatManager GetCombatManager();AllGameData GetAllGameData();RoomManager GetRoomManager();}
public interface ISystemManagers{}
public class FakeCore:ICoreGameManagers,ISystemManagers
{
 public RoomManager Rooms=new();public RoomManager GetRoomManager()=>Rooms;
 public AllGameData Data=new();public AllGameData GetAllGameData()=>Data;
 public CardManager Cards=new();public CombatManager Combat=new();public SaveManager Save=new();
 public SaveManager GetSaveManager()=>Save;public CardManager GetCardManager()=>Cards;public CombatManager GetCombatManager()=>Combat;
}
public class CardManager
{
 public List<CardState> Hand=new(),Standby=new();public List<CardState> GetHand()=>Hand;
 public void MoveToStandByPile(CardState c,bool a,bool b){Hand.Remove(c);Standby.Add(c);}
}
public static class TargetHelper
{
 public static void CheckTargetsOverride(){}
 public struct CollectTargetsData
 {
  public TargetMode targetMode;public Team.Type targetTeamType;public int roomIndex;public bool inCombat,ignorePyre,isCardEffectDamage;public List<CharacterState>? skipCharacters;
  public void Reset(TargetMode mode)=>targetMode=mode;
 }
 public static void CollectTargets(CollectTargetsData data,ICoreGameManagers core,ref List<CharacterState> targets)
 {
  var room=core.GetRoomManager().GetRoom(data.roomIndex);if(room==null)return;
  var target=room.Units.FirstOrDefault(u=>!u.IsDestroyed&&u.IsAlive&&(data.skipCharacters==null||!data.skipCharacters.Contains(u)));
  if(target!=null)targets.Add(target);
 }
}
public class CardEffectParams{public CardState playedCard=null!;}
public class RoomState{public bool Enabled=true;public List<CharacterState> Units=new();public bool IsRoomEnabled()=>Enabled;}
public class RoomManager
{
 public readonly Dictionary<int,RoomState> Rooms=new();public RoomUI UI=new();public int GetSelectedRoom()=>UI.Selected;
 public RoomState? GetRoom(int floor)=>Rooms.GetValueOrDefault(floor);public RoomUI GetRoomUI()=>UI;
}
public class RoomUI
{
 public int Selected,Restores;public IEnumerator SetSelectedRoom(int floor){Selected=floor;Restores++;yield break;}
}
public class CombatManager
{
 public bool PlayerAction=true;public bool IsPlayerActionPhase()=>PlayerAction;public static CardState? GetResponsibleCard()=>null;
 public List<(CardEffectState effect,CharacterState? target,CharacterState? self)> Calls=new();public bool Throw;
 public IEnumerator ApplyEffects(List<CardEffectState> effects,int floor,CardState card,CharacterState? selfTarget,CharacterState overrideTargetCharacter)
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
 // DissertationMarker is the production implementation linked by the project.
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
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Formatear contador localizado en pruebas de tooltip real
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Identidades independientes para varias cartas vinculadas y distintas mejoras
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Representar contenido dinámico y origen real de cartas reservadas
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Permitir código real de actualización del marcador sin stub que oculta su comportamiento
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Dobles de tipos nativos seguros sin objetivo, no reconocimiento por nombres de carta
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Contrato nativo de resolución de CardData para formateador
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||GameData explícito del mismo contexto de batalla
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Eliminar stub de marcador al probar implementación real

public class AllGameData
{
 public Dictionary<string,CardData> Cards=new();
 public CardData? FindCardData(string id)=>Cards.GetValueOrDefault(id);
}
public class StatusEffectState{public string Id="";public string GetStatusId()=>Id;}
public struct TooltipContent{public string body;}
public static class TooltipUI{public static void GetStatusEffectCharacterContent(){}}
public static class CardTooltipHelper
{
 public static CardState? LastState;
 public static string GetTooltipContentForGeneratedCard(CardData data,CardState card,SaveManager save)
 {LastState=card;return data.Id+"<br>"+card.Body;}
}
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Dobles mínimos del formateador nativo con CardState real y tooltip de status
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Modelar atributos de targeting del puente de repeticiones
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Modelar tipo y autoría de carta para ejecutar SpellReplay real
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Modelar vida de objetivos de repetición
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Modelar simulación nativa y tipo de carta en pruebas de reproducción
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Exponer contexto de pisos en dobles de repetición
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Pisos independientes por contexto de prueba
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Dobles de selección nativa por piso, restauración visual y datos de targeting
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Modelar fase de batalla y getter de fuente para atributos Harmony
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Permitir copias sin atacante físico en registros de efectos
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/NativeDoubles.cs||Alinear doble con selfTarget opcional del motor
