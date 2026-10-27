using System.Collections;
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]public class HarmonyPatchAttribute:Attribute{public HarmonyPatchAttribute(Type t,string name){_=t;_=name;}}}
public static class Team{[Flags]public enum Type{None=0,Monsters=1,Heroes=2}}
public enum CardType{Spell,Monster,Equipment}
public enum TargetMode{DropTargetCharacter,Room,RandomInRoom,FrontInRoom,BackInRoom,FrontWithStatus,Weakest,Tower,FrontInAllRooms,FrontInRoomAndRoomAbove,WeakestAllRooms,StrongestAllRooms,RandomFromAnyRoom,LastTargetedCharacters,Self,Hand}
public class CardEffectState(Team.Type team,TargetMode mode){public CardEffectState():this(Team.Type.Monsters,TargetMode.Self){}public CardEffectData Data=new();public int Param=5;public StatusEffectStackData[]? Override;public void Setup(CardEffectData data,Team.Type source,SaveManager save){Data=data;Param=data.Multiplier;}public CardEffectData GetSourceCardEffectData()=>Data;public int GetParamInt()=>Param;public void SetParamInt(int v)=>Param=v;public void SetParamStatusEffects(StatusEffectStackData[] s)=>Override=s;public StatusEffectStackData[] GetParamStatusEffectStackData()=>Override??Data.Statuses;public object? Effect;public object? GetCardEffect()=>Effect;public Team.Type GetTargetTeamType()=>team;public TargetMode GetTargetMode()=>mode;public CardState? Parent=new();public CardState? GetParentCardState()=>Parent;}
public class CardTraitFreeze{}
public class CharacterData{public string name="";}
public class CardState{public enum UpgradeDisabledReason{NONE,NotEligible}public bool Duality;public bool HasDualityStatusEffect()=>Duality;public bool IsEquipmentCard()=>Type==CardType.Equipment;public static readonly CardState None=new();public bool Frozen;public bool Consumed,Damage=true,Healing,SingleEnemy=true;public bool GetIsDamageCard()=>Damage;public bool HasTrait(Type type)=>(Frozen&&type==typeof(CardTraitFreeze))||(Consumed&&type==typeof(CardTraitExhaustState));public CardType Type=CardType.Spell;public bool Ability;public int Cost=1;public List<CardEffectState> Effects=new();public CardType GetCardType()=>Type;public bool IsAnyAbility()=>Ability;public List<CardEffectState> GetEffectStates()=>Effects;public int GetCostWithoutAnyModifications()=>Cost;public int GetLastPlayedCost()=>Cost;}
public class CharacterState
{
 public bool Outer;public bool IsOuterTrainBoss()=>Outer;public bool HasStatusEffect(string id)=>GetStatusEffectStacks(id)>0;public void RemoveStatusEffect(string id,int n){Real[id]=Math.Max(0,Real.GetValueOrDefault(id)-n);}
 public bool HasUI=true;public object? GetCharacterUI()=>HasUI?this:null;
 public int Floor;public int GetCurrentRoomIndex()=>Floor;
 public void ApplyCardUpgrade(){}public void RemoveCardUpgrade(){}
 public CharacterData Source=new();public CharacterData GetSourceCharacterData()=>Source;
 public bool PreviewMode;public bool IsDestroyed,TemporaryPreviewsEnabled;public Team.Type TeamType=Team.Type.Heroes;
 public Dictionary<string,int> Real=new(),Temporary=new();public int RealHP=40,PreviewHP=40;
 public int HP{get=>TemporaryPreviewsEnabled?PreviewHP:RealHP;set{if(TemporaryPreviewsEnabled)PreviewHP=value;else RealHP=value;}}
 public bool IsDead=>HP<=0;public bool IsAlive=>!IsDead;
 public int Attack,Buff;
 public int GetAttackDamage()=>Attack+Buff;
 public int GetAttackDamageWithoutStatusEffectBuffs()=>Attack;
 public void SetAttackDamage(int value)=>Attack=value;
 public readonly List<(int amount,CardState? source)> Healing=new();public Action? OnHeal;
 public IEnumerator ApplyHeal(int amount,CardState? responsibleCard=null){Healing.Add((amount,responsibleCard));OnHeal?.Invoke();yield break;}

 public Team.Type GetTeamType()=>TeamType;
 public int GetStatusEffectStacks(string id)=>(TemporaryPreviewsEnabled?Temporary:Real).GetValueOrDefault(id);
 public void AddStatusEffect(string id,int n){var map=TemporaryPreviewsEnabled?Temporary:Real;map[id]=map.GetValueOrDefault(id)+n;}
 public void Preview(){Temporary=new(Real);PreviewHP=RealHP;TemporaryPreviewsEnabled=true;}
}
public class SaveManager{public int Moved,Unlocks;public void IncrementTrackedValueForUnlocks(MetagameSaveData.TrackedValue value){Moved++;}public void CheckForNewUnlocks(){Unlocks++;}public bool PreviewMode;public int DisplayedRing=1;public int GetDisplayDistance()=>DisplayedRing;}
public class PlayerManager{public int Energy;public void AddEnergy(int n){Energy+=n;}}
public class CombatManager
{
 public class ApplyDamageToTargetParameters{}
 private IEnumerator DoUnitCombat(){yield break;}
 public List<CharacterState> Sent=new();public IEnumerator SentryCharacterTrigger(CharacterState unit,int floor){Sent.Add(unit);yield break;}
 public int QueueRuns;public IEnumerator RunTriggerQueue(){QueueRuns++;yield break;}
 public bool IsRunningCardPlayPreview;
 public IEnumerator ApplyDamageToTarget(int amount,CharacterState target,ApplyDamageToTargetParameters p){target.HP-=amount;yield break;}
}
public interface ICoreGameManagers{SaveManager GetSaveManager();PlayerManager GetPlayerManager();CombatManager GetCombatManager();CardManager GetCardManager();RoomManager GetRoomManager();CardStatistics GetCardStatistics();RelicManager GetRelicManager();HandUI GetHandUI();HeroManager GetHeroManager();}
public class FakeCore:ICoreGameManagers
{
 public HeroManager Heroes=new();public HeroManager GetHeroManager()=>Heroes;
 public HandUI Hand=new();public HandUI GetHandUI()=>Hand;
 public SaveManager Save=new();public PlayerManager Player=new();public CombatManager Combat=new();public CardManager Cards=new();public RoomManager Rooms=new();public CardStatistics Statistics=new();public RelicManager Relics=new();
 public SaveManager GetSaveManager()=>Save;public PlayerManager GetPlayerManager()=>Player;public CombatManager GetCombatManager()=>Combat;public CardManager GetCardManager()=>Cards;public RoomManager GetRoomManager()=>Rooms;public CardStatistics GetCardStatistics()=>Statistics;public RelicManager GetRelicManager()=>Relics;
}
namespace WwInc
{
 internal static class AcademicRuntime
 {
  internal class SpellResolution{internal CardState Card=null!;internal int Floor,BonusPower;internal bool Indirect;internal HashSet<string> Rules=new();internal int RepresentativeDamage;internal int EffectiveDamage=7;}
  internal static List<CharacterState> Units(int floor)=>UnitSpellPassives.Units(Core,floor);
  internal static readonly HashSet<CharacterState> ZeroAttack=new();
  internal static ICoreGameManagers Core=null!;internal static bool Simulating=false;internal static bool Active=true;internal static int ConversionDepth;internal static readonly Stack<SpellResolution> Frames=new();
 }
}
// 2026-10-25-1606||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Dobles con energía real no aislada y estados temporales por unidad para reproducir fuga de hover

namespace ShinyShoe{public static class Loc{public static string Localize(this string key)=>key=="TextFormat_Divide"?"{0}/{1}":key=="SizeNotificationText"?"{0} size":key;public static string Localize(this string key,CardEffectLocalizationContext context)=>"Conduit {0}";}}
public class CardEffectAddCardUpgradeToUnits{}
public class CardUpgradeState
{
 public string Asset="";public int Size;public CardUpgradeData? Source;
 public readonly List<StatusEffectStackData> Statuses=new();
 public string GetAssetName()=>Asset;public int GetAdditionalSize()=>Size;
 public CardUpgradeData? GetSourceCardUpgradeData()=>Source;
 public void SetStatusCount(string id,int count){var item=Statuses.First(s=>s.statusId==id);item.count=count;}
}
// 2026-10-25-1850||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Dobles de mejora y localización nativa para reproducir el signo +−1 sin ejecutar Unity

public class CardUI
{
 public enum CardUIState{HandDiscardCard,Ineligible,Hand,Screen}
 public CardUIState State;public bool Disabled=true;public CardUIState GetUIState()=>State;
 public void SetUIState(CardUIState state)=>State=state;
 public void SetDisabled(CardState.UpgradeDisabledReason reason)=>Disabled=reason!=CardState.UpgradeDisabledReason.NONE;
}
public class HandUI
{
 public enum DiscardEffect{Exhausted}
 public bool Selecting;public List<CardUI> Cards=new();public CardUI? Focused;
 public bool IsHandSelectionActive()=>Selecting;public List<CardUI> GetCardUIList()=>Cards;
 public CardUI GetFocusedCardUI()=>Focused!;
}
public class HandSelectionUI
{
 public class Params
 {
  public string descriptionKey="";
  public Func<CardState,bool> filterCallback=null!;
  public Func<CardState,CardManager,IEnumerator> cardChosenCallback=null!;
  public bool disableIneligibleCards;
  public CardUI.CardUIState? selectionHandState;
 }
}
public class CardManager
{
 public CombatManager? Combat;public class DiscardCardParams{public CardState discardCard=null!;public HandUI.DiscardEffect overrideDiscardEffect;}
 public IEnumerator ConsumeCardWithoutPlaying(DiscardCardParams p){Hand.Remove(p.discardCard);if(Combat!=null)yield return Combat.RunTriggerQueue();}

 public int DrawModifier;public void AdjustDrawCountModifier(int n)=>DrawModifier+=n;
 public readonly List<CardState> Hand=new();
 public Action? Completed;public void AddSelectCardFromHandOnCompletedCallback(Action callback)=>Completed+=callback;
 public HandSelectionUI.Params? Selection;
 public int OpenCount;public bool AutoPick;public int Drawn;public void DrawCards(int n){Drawn+=n;}
 public List<CardState> GetHand()=>Hand;
 public Action? Refreshed;public int Refreshes;public void RefreshHandCards(bool resetHandState=false){Refreshes++;Refreshed?.Invoke();}
 public IEnumerator SelectCardFromHand(HandSelectionUI.Params parameters,bool automaticallySelectLastCard=true)
 { Selection=parameters;AutoPick=automaticallySelectLastCard;OpenCount++;yield break; }
}
// 2026-10-25-2009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Dobles de selector nativo con confirmación diferida y dos contextos de combate
// 2026-10-25-2036||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Dobles de identidad nativa de personaje y cartas Frozen de cualquier tipo

public class RoomState
{
 public int Floor;public int GetRoomIndex()=>Floor;
 public void RearrangeCharacter(Team.Type team,int from,int to){var unit=Units[from];Units.RemoveAt(from);Units.Insert(to,unit);}public void UpdateSpawnPointPositions(Team.Type team){}
 public readonly List<CharacterState> Units=new();
 public void AddCharactersToList(List<CharacterState> output,Team.Type team)=>output.AddRange(Units.Where(u=>team.HasFlag(u.TeamType)));
}
public class RoomManager{public int OrderUpdates;public IEnumerator HandleRoomUnitOrderPossiblyChanged(){OrderUpdates++;yield break;}public int GetNumRooms()=>Rooms.Count;public readonly Dictionary<int,RoomState> Rooms=new();public RoomState? GetRoom(int floor)=>Rooms.GetValueOrDefault(floor);}
public class CardStatistics{}
public class RelicManager{public List<CharacterState> Moved=new();public Action<CharacterState>? OnMove;public IEnumerator CharacterFloorRearranged(CharacterState unit,CardState? card){Moved.Add(unit);OnMove?.Invoke(unit);yield break;}public readonly List<WwInc.RelicEffectAcademic> Effects=new();public List<T> GetRelicEffects<T>(List<T> list){list.AddRange(Effects.OfType<T>());return list;}}
public class PropDescriptions{public static PropDescriptions DisplayNone=>new();}
public class RelicEffectData{public int Amount=1;public int GetParamInt()=>Amount;}
public class RelicEffectBase
{
 public class RelicData{public string name="";}public RelicData? SourceRelicData;public virtual void AppendConditionCountDisplay(System.Text.StringBuilder builder,CardStatistics statistics){}
 protected RelicEffectData _srcRelicEffectData=new();
 public int Triggers;
 public virtual PropDescriptions CreateEditorInspectorDescriptions()=>PropDescriptions.DisplayNone;
 public bool AreConditionsTrue(CardStatistics statistics)=>Triggers==0;
 public void OnTrigger(CardStatistics statistics){Triggers++;}
 protected void NotifyRelicTriggeredScreenCenter(RelicManager manager){}
}
// 2026-10-25-2142||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Dobles de unidades, piso y API de condiciones nativas para probar fuentes, reparto y preview
// 2026-10-26-2037||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Añadir piso de unidad para prueba de duración por ronda
// 2026-10-26-2037||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Firma de ronda nativa para atributos Harmony en pruebas
// 2026-10-26-2037||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Conservar estado cero Attack real en arnés de instantáneas
// 2026-10-26-2037||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Piso nativo del combate para prueba de expiración
// 2026-10-26-2230||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Representar anillo visible nativo y avance sin depender del contador interno
// 2026-10-26-2230||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Firmas nativas de conexión de upgrade para atributos Harmony
// 2026-10-26-2230||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar copia local de estados de upgrade independiente de la plantilla de datos
// 2026-10-26-2230||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Doble de localización con contexto nativo del efecto de equipo

public class StatusEffectStackData{public string statusId="";public int count;public StatusEffectStackData Copy()=>new(){statusId=statusId,count=count};}
public class CardUpgradeData
{
 public readonly List<StatusEffectStackData> Statuses=new();
 public List<StatusEffectStackData> GetStatusEffectUpgrades()=>Statuses;
}
public class AllGameManagers
{
 public static AllGameManagers? Instance;public SaveManager Save=new();public SaveManager GetSaveManager()=>Save;
 public bool Initialized,HasCombat=true,HasCards=true,HasRooms=true;
 public FakeCore Core=new();public int CoreReads,ManagerReads;
 public bool AreMainSceneOnlyManagersInitialized()=>Initialized;
 public CombatManager? GetCombatManager(){ManagerReads++;return HasCombat?Core.Combat:null;}
 public CardManager? GetCardManager(){ManagerReads++;return HasCards?Core.Cards:null;}
 public RoomManager? GetRoomManager(){ManagerReads++;return HasRooms?Core.Rooms:null;}
 public ICoreGameManagers GetCoreManagers(){CoreReads++;if(!Initialized)throw new InvalidOperationException("Native assertion before scene initialization");return Core;}
}
public class CardEffectAttachEquipment
{
 public virtual string GetCardText(CardEffectState state,RelicManager? relicManager=null)=>"";
}
public class CardEffectLocalizationContext(CardState state){public readonly CardState State=state;}
// 2026-10-26-2230||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Dobles mínimos de fuente, upgrade y contexto de texto para varita dinámica
// 2026-10-26-2231||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Conservar parámetro del contexto y evitar advertencias del arnés
// 2026-10-26-2234||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Reflejar acceso nativo a la carta padre para localización de equipo
// 2026-10-26-2234||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Alinear doble con contexto de carta y evitar aceptar firmas que no existen en juego
// 2026-10-26-2236||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Incluir sentinel nativo para la ruta de tooltip sin carta padre
// 2026-10-26-2237||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Ejercitar tooltips construidos sin carta padre como en compendio

public struct TooltipContent
{
 public string title,body,tooltipId;
 public bool IsEmpty()=>string.IsNullOrWhiteSpace(title)&&string.IsNullOrWhiteSpace(body);
}
// 2026-10-26-2313||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Añadir doble de contenido nativo para reproducir IDs nulos sin Unity
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar carta de equipo para probar separación de línea nativa y descripción
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Permitir identificación de efecto de varita en doble nativo
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar Duality y tipo de equipo en pruebas de formateo
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Copiar descriptores de estado para verificar ausencia de mutación de plantilla

public static class StatusEffectDualityState{public static bool IsStatusEffectedByDuality(string id)=>id=="conduit";}
public static class CardTextHelper{public static void SetUnupgradedStatusEffectText(){}public static void SetStatusEffectsUpgradeText(){}}
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Dobles mínimos de formateadores nativos y Duality
// 2026-10-27-0008||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar accessors raíz nullable y assertion de Core antes de inicialización para reproducir log fuera de combate
// 2026-10-27-1428||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Simular ausencia de UI en unidades durante transición para probar preview
// 2026-10-27-1428||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar número de pisos usado por ampliación de preview
// 2026-10-27-1431||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar acceso nativo a mano del contexto
// 2026-10-27-1431||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Añadir mano independiente por combate para pruebas de limpieza
// 2026-10-27-1431||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar UI ineligible y carta enfocada sin depender de Unity
// 2026-10-27-1431||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar motivo nativo NONE al quitar marca de inelegibilidad
// 2026-10-27-1431||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Conservar callback real de cierre para regresión del selector
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Registrar cola resuelta al completar selección
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Distinguir preview de combate y copia temporal de unidad
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Representar aislamiento de mutaciones de pasivas
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Simular refresco nativo en lugar de limpieza manual de CardUI
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Identificar personajes del mod sin tratar todos los clanes como dependencias W&W
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar consulta nativa de efectos de reliquias para dependencias de preview

namespace WwInc
{

 internal static class SpellRules
 {
  internal class Rule{internal string Kind="";internal int Floor=-1;}
  internal static HashSet<string> OnceTurn=new();internal static int TuitionRefundCount;
  internal static List<(CardState card,string rule)> Rules=new();
  internal static void AddRule(CardState card,string rule)=>Rules.Add((card,rule));
  internal static bool IsHealingSpell(CardState card)=>card.Healing;
  internal static bool TargetsEnemy(CharacterState? target)=>target?.GetTeamType()==Team.Type.Heroes;
  internal static bool MonoEnemy(CardState card)=>card.SingleEnemy;
  internal static void ResolutionPower(AcademicRuntime.SpellResolution frame,int power)=>frame.BonusPower+=power;
  internal static List<Rule> Pending=new();
 }
}
// 2026-10-27-1536||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar reglas pendientes y reliquias propias para ejecutar dependencia real de preview
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Inicializar explícitamente señal de simulación del arnés
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Datos nativos dinámicos para probar el efecto extraído de créditos
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar coste base independiente de descuentos
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar motivo nativo de consumo de carta
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Representar consumo nativo seguido de su cola antes de concesión Conduit

public interface ISystemManagers{}
public class FakeSystems:ISystemManagers{}
public class CardEffectData{public int Multiplier=5;public StatusEffectStackData[] Statuses=[new(){statusId="conduit",count=1}];}
public class CardEffectParams{public int selectedRoom;public CardState? playedCard;public CharacterState? selfTarget,characterThatActivatedAbility;public List<CharacterState> targets=new();}
namespace WwInc
{
 public abstract class AcademicEffectBase
 {
  public virtual bool CanApplyInPreviewMode=>false;
  public virtual bool TestEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m)=>true;
  public abstract IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers sys);
 }
}
public class CardEffectAddStatusEffect
{
 public static int LastChance;public static CardState? LastSource;
 public IEnumerator ApplyEffect(CardEffectState s,CardEffectParams p,ICoreGameManagers m,ISystemManagers systems)
 {
  LastChance=s.GetParamInt();LastSource=p.playedCard;
  foreach(var unit in p.targets)foreach(var entry in s.GetParamStatusEffectStackData())unit.AddStatusEffect(entry.statusId,entry.count);
  yield return m.GetCombatManager().RunTriggerQueue();
 }
}
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Dobles mínimos para ejecutar cálculo y delegación real de Credit Conversion
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Contexto explícito del efecto de créditos en pruebas

public interface ICardEffectUiDialog{}
// 2026-10-27-1539||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Añadir interfaz nativa de selector al arnés del efecto de créditos
// 2026-10-27-1540||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar alcance de reglas Memo y otras reglas de piso

public class CardTraitExhaustState{}
public interface ICardEffectChangesUnitPosition{}
public static class MetagameSaveData{public enum TrackedValue{UnitsMoved,SoulUnitsMoved}}
public class HeroManager{public List<CharacterState> Shifted=new();public IEnumerator ShiftCharacterTrigger(CharacterState unit){Shifted.Add(unit);yield break;}}
public class CardEffectAttackWithUnit
{
 public static bool ThrowAttack;public static List<CharacterState> Attacked=new();
 public IEnumerator ApplyEffect(CardEffectState state,CardEffectParams p,ICoreGameManagers core,ISystemManagers systems){Attacked.AddRange(p.targets);if(ThrowAttack)throw new InvalidOperationException("native attack");yield break;}
}
namespace WwInc
{
 internal static class SocialRules
 {
  internal static readonly List<string> Events=new();
  internal static IEnumerator CreateSeparators(){Events.Add("separators");yield break;}
  internal static IEnumerator TransferComplaints(){Events.Add("complaints");yield break;}
 }
}
// 2026-10-27-2021||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar únicamente APIs nativas de movimiento, reliquias y callbacks para ejecutar adaptadores productivos
// 2026-10-27-2024||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Modelar excepción nativa durante ataque para verificar limpieza de parámetros
// 2026-10-27-2024||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/NativeDoubles.cs||Permitir fallo de ataque en doble sin interceptar error de producción
