using System.Collections;
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]public class HarmonyPatchAttribute:Attribute{public HarmonyPatchAttribute(Type t,string name){_=t;_=name;}}}
public static class Team{[Flags]public enum Type{None=0,Monsters=1,Heroes=2}}
public enum CardType{Spell,Monster}
public enum TargetMode{DropTargetCharacter,Room,RandomInRoom,FrontInRoom,BackInRoom,FrontWithStatus,Weakest,Tower,FrontInAllRooms,FrontInRoomAndRoomAbove,WeakestAllRooms,StrongestAllRooms,RandomFromAnyRoom,LastTargetedCharacters,Self,Hand}
public class CardEffectState(Team.Type team,TargetMode mode){public Team.Type GetTargetTeamType()=>team;public TargetMode GetTargetMode()=>mode;}
public class CardState{public CardType Type=CardType.Spell;public bool Ability;public int Cost=1;public List<CardEffectState> Effects=new();public CardType GetCardType()=>Type;public bool IsAnyAbility()=>Ability;public List<CardEffectState> GetEffectStates()=>Effects;public int GetLastPlayedCost()=>Cost;}
public class CharacterState
{
 public bool IsDestroyed,TemporaryPreviewsEnabled;public Team.Type TeamType=Team.Type.Heroes;
 public Dictionary<string,int> Real=new(),Temporary=new();public int RealHP=40,PreviewHP=40;
 public int HP{get=>TemporaryPreviewsEnabled?PreviewHP:RealHP;set{if(TemporaryPreviewsEnabled)PreviewHP=value;else RealHP=value;}}
 public bool IsDead=>HP<=0;
 public Team.Type GetTeamType()=>TeamType;
 public int GetStatusEffectStacks(string id)=>(TemporaryPreviewsEnabled?Temporary:Real).GetValueOrDefault(id);
 public void AddStatusEffect(string id,int n){var map=TemporaryPreviewsEnabled?Temporary:Real;map[id]=map.GetValueOrDefault(id)+n;}
 public void Preview(){Temporary=new(Real);PreviewHP=RealHP;TemporaryPreviewsEnabled=true;}
}
public class SaveManager{public bool PreviewMode;}
public class PlayerManager{public int Energy;public void AddEnergy(int n){Energy+=n;}}
public class CombatManager
{
 public class ApplyDamageToTargetParameters{}
 public bool IsRunningCardPlayPreview;
 public IEnumerator ApplyDamageToTarget(int amount,CharacterState target,ApplyDamageToTargetParameters p){target.HP-=amount;yield break;}
}
public interface ICoreGameManagers{SaveManager GetSaveManager();PlayerManager GetPlayerManager();CombatManager GetCombatManager();}
public class FakeCore:ICoreGameManagers
{
 public SaveManager Save=new();public PlayerManager Player=new();public CombatManager Combat=new();
 public SaveManager GetSaveManager()=>Save;public PlayerManager GetPlayerManager()=>Player;public CombatManager GetCombatManager()=>Combat;
}
namespace WwInc
{
 internal static class AcademicRuntime
 {
  internal class SpellResolution{internal CardState Card=null!;internal int RepresentativeDamage;internal int EffectiveDamage=7;}
  internal static bool Active=true;internal static int ConversionDepth;internal static readonly Stack<SpellResolution> Frames=new();
 }
}
// 2026-10-25-1606||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Dobles con energía real no aislada y estados temporales por unidad para reproducir fuga de hover

namespace ShinyShoe{public static class Loc{public static string Localize(this string key)=>key=="SizeNotificationText"?"{0} size":key;}}
public class CardEffectAddCardUpgradeToUnits{}
public class CardUpgradeState{public string Asset="";public int Size;public string GetAssetName()=>Asset;public int GetAdditionalSize()=>Size;}
// 2026-10-25-1850||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/NativeDoubles.cs||Dobles de mejora y localización nativa para reproducir el signo +−1 sin ejecutar Unity
