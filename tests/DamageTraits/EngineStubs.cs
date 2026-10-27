using System.Collections;
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) {} }
}
public static class Damage { public enum Type { Default, DirectAttack, Trample, SpellOverflow } }
public enum CardType{Spell,Monster,Equipment}
public class CardState {
 public CardType Type=CardType.Spell;public bool Ability;public CardType GetCardType()=>Type;public bool IsAnyAbility()=>Ability;
 public string AssetName = "";public CardStateModifiers Modifiers=new();
 public CardStateModifiers GetTemporaryCardStateModifiers()=>Modifiers;
 public void RemoveUpgrade(CardUpgradeState upgrade,CardStateModifiers modifiers)=>modifiers.Upgrades.Remove(upgrade);
 public void UpdateCardBodyText(){}
}
public abstract class CardTraitState
{
    public struct ApplyingDamageParameters { public int damage,damagePreventedByLifeLink;public CharacterState? targetCharacter;public Damage.Type damageType; public CardState? damageSourceCard; }
    public virtual IEnumerator OnDamageWasApplied(ApplyingDamageParameters damage){yield break;}
    public virtual PropDescriptions CreateEditorInspectorDescriptions()=>new();
    public virtual string GetCardText()=>"";public virtual string GetCardTooltipText()=>"";public CardState Associated=new();public CardState GetCard()=>Associated;
    public virtual IEnumerator OnCardPlayed(CardState card,ICoreGameManagers core){yield break;}
    public CardTraitData Own=new();public CardTraitData GetCardTraitData()=>Own;
}
public abstract class CardTraitTrample : CardTraitState {}
public abstract class CardTraitDamageOverflow : CardTraitState {}
namespace WwInc
{
    internal static class AcademicRuntime { internal static bool Simulating; internal static string Id(CardState? card) => card?.AssetName ?? ""; }
}
// 2026-10-17-1448||script-corregir-trample-explosive.py||repos/MT2/repos-propios/mt2-ww-inc/tests/DamageTraits/EngineStubs.cs||Dobles mínimos de API; firmas reales verificadas además por compilación y contratos
// 2026-10-26-2038||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/DamageTraits/EngineStubs.cs||Dobles registran mejoras exactas retiradas por código de producción
// 2026-10-26-2038||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/DamageTraits/EngineStubs.cs||Firmas nativas para probar marcador real sin Unity
// 2026-10-26-2038||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/DamageTraits/EngineStubs.cs||Bloqueo de preview para marcador de próximo uso en arnés

public class PropDescriptions{public static PropDescriptions DisplayNone=>new();}
public class CardTraitData
{
 public string State="";public string GetTraitStateName()=>State;
}
public class CardUpgradeData
{
 public readonly List<CardTraitData> Traits=new();public List<CardTraitData> GetTraitDataUpgrades()=>Traits;
}
public class CardUpgradeState:CardUpgradeData{}
public class CardStateModifiers
{
 public readonly List<CardUpgradeState> Upgrades=new();public List<CardUpgradeState> GetCardUpgrades()=>Upgrades;
}
public class SaveManager{public bool PreviewMode;}
public class CardManager{public void RefreshCardInHand(CardState card){}}
public interface ICoreGameManagers{SaveManager GetSaveManager();CardManager GetCardManager();CombatManager GetCombatManager();}
public class FakeCore:ICoreGameManagers
{
 public readonly CombatManager Combat=new();public CombatManager GetCombatManager()=>Combat;
 public readonly SaveManager Save=new();public readonly CardManager Cards=new();
 public SaveManager GetSaveManager()=>Save;public CardManager GetCardManager()=>Cards;
}
namespace ShinyShoe
{
 public static class TypeNameCache{public static Type? GetType(string name)=>Type.GetType(name);}
}
// 2026-10-26-2038||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/DamageTraits/EngineStubs.cs||Dobles de datos nativos de mejoras, marcadores y contexto de lanzamiento
// 2026-10-27-1539||codex-ww-inc||salidas/build/ww-inc/tests/DamageTraits/EngineStubs.cs||Modelar daño postmitigación y víctima para ejecutar cadena real de Trample
// 2026-10-27-1539||codex-ww-inc||salidas/build/ww-inc/tests/DamageTraits/EngineStubs.cs||Modelar fuente asociada y tooltip de trait nativo
// 2026-10-27-1539||codex-ww-inc||salidas/build/ww-inc/tests/DamageTraits/EngineStubs.cs||Exponer daño nativo en arnés de Trample
// 2026-10-27-1539||codex-ww-inc||salidas/build/ww-inc/tests/DamageTraits/EngineStubs.cs||Instancia de combate con mitigación explícita para regresiones

public class CharacterState
{
 public int HP=10,Armor,Weakness,Floor;public bool Shield,IsDestroyed;public bool IsAlive=>HP>0;
 public int GetHP()=>HP;public int GetCurrentRoomIndex()=>Floor;
}
public class CombatManager
{
 public class ApplyDamageToTargetParameters{public CardState? playedCard;public Damage.Type damageType;}
 public readonly List<CharacterState> Board=new();public readonly List<(CharacterState target,int damage,Damage.Type type,CardState? card)> Hits=new();
 public IEnumerator ApplyDamageToTarget(int amount,CharacterState target,ApplyDamageToTargetParameters p)
 {
  int damage=p.damageType==Damage.Type.SpellOverflow?amount:amount*(target.Weakness+1);
  if(target.Shield){target.Shield=false;damage=0;}
  int armor=Math.Min(target.Armor,damage);target.Armor-=armor;damage-=armor;
  WwInc.SpellTrample.Observe(target,p.playedCard,damage);
  target.HP=Math.Max(0,target.HP-damage);Hits.Add((target,damage,p.damageType,p.playedCard));yield break;
 }
}
namespace WwInc
{
 internal static class SpellReplay
 {
  internal static CharacterState? FrontEnemy(ICoreGameManagers core,int floor,List<CharacterState>? skip=null)
   =>core.GetCombatManager().Board.FirstOrDefault(unit=>unit.Floor==floor&&unit.IsAlive&&!unit.IsDestroyed&&(skip==null||!skip.Contains(unit)));
 }
}
namespace ShinyShoe{public static class Loc{public static string Localize(this string key)=>key;}}
// 2026-10-27-1539||codex-ww-inc||salidas/build/ww-inc/tests/DamageTraits/EngineStubs.cs||Dobles de impactos con armadura, escudo y weakness para probar código de transferencia real
// 2026-10-27-1545||codex-ww-inc||salidas/build/ww-inc/tests/DamageTraits/EngineStubs.cs||Modelar tipo y habilidad para comprobar alcance de Trample de hechizos
