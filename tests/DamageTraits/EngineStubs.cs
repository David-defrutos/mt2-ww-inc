using System.Collections;
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) {} }
}
public static class Damage { public enum Type { Default, DirectAttack, Trample, SpellOverflow } }
public class CardState { public string AssetName = ""; }
public abstract class CardTraitState
{
    public struct ApplyingDamageParameters { public Damage.Type damageType; public CardState? damageSourceCard; }
    public abstract IEnumerator OnDamageWasApplied(ApplyingDamageParameters damage);
}
public abstract class CardTraitTrample : CardTraitState {}
public abstract class CardTraitDamageOverflow : CardTraitState {}
namespace WwInc
{
    internal static class AcademicRuntime { internal static string Id(CardState? card) => card?.AssetName ?? ""; }
}
// 2026-10-17-1448||script-corregir-trample-explosive.py||repos/MT2/repos-propios/mt2-ww-inc/tests/DamageTraits/EngineStubs.cs||Dobles mínimos de API; firmas reales verificadas además por compilación y contratos
