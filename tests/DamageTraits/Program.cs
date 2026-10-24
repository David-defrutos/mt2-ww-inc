using System.Collections;
using System.Text.Json;
using WwInc;

var errors = new List<string>(); int checks = 0;
void Check(bool value, string label) { checks++; if (!value) errors.Add(label); }
var own = new CardState { AssetName = "david.wwinc-Card-WW_S13" };
var foreign = new CardState { AssetName = "another.mod-Card-TrampleSpell" };
var similar = new CardState { AssetName = "david.wwinc.other-Card-WW_S13" };
foreach (var card in new CardState?[] { own, foreign, similar, null })
foreach (var type in Enum.GetValues<Damage.Type>())
{
    var input = new CardTraitState.ApplyingDamageParameters { damageSourceCard = card, damageType = type };
    IEnumerator sentinel = new[] { 42 }.GetEnumerator(), result = sentinel;
    bool pass = TrampleFromOverflowHook.Prefix(input, ref result);
    bool blocked = card == own && type == Damage.Type.SpellOverflow;
    Check(pass == !blocked && (blocked ? result != null && !result.MoveNext() : ReferenceEquals(result, sentinel)),
        $"Trample callback preserves native enumerator except own SpellOverflow: {card?.AssetName}/{type}");
    result = sentinel;
    pass = OverflowFromTrampleHook.Prefix(input, ref result);
    blocked = card == own && type == Damage.Type.Trample;
    Check(pass == !blocked && (blocked ? result != null && !result.MoveNext() : ReferenceEquals(result, sentinel)),
        $"Explosive callback preserves native enumerator except own Trample: {card?.AssetName}/{type}");
}

// Bounded model of the two native callback edges, assuming lethal excess damage.
// It exercises production prefixes; it does not simulate HP, targets, Souls or Unity.
int Resolve(bool guarded, bool trample, bool explosive, CardState card)
{
    var pending = new Queue<Damage.Type>(); pending.Enqueue(Damage.Type.Default);
    int steps = 0;
    while (pending.Count > 0 && steps < 128)
    {
        var type = pending.Dequeue(); steps++;
        var input = new CardTraitState.ApplyingDamageParameters { damageSourceCard = card, damageType = type };
        IEnumerator result = TrampleOverflowGuard.Empty();
        if (trample && type != Damage.Type.Trample && (!guarded || TrampleFromOverflowHook.Prefix(input, ref result)))
            pending.Enqueue(Damage.Type.Trample);
        if (explosive && type != Damage.Type.SpellOverflow && (!guarded || OverflowFromTrampleHook.Prefix(input, ref result)))
            pending.Enqueue(Damage.Type.SpellOverflow);
    }
    return pending.Count > 0 ? -1 : steps;
}
Check(Resolve(false, true, true, own) == -1, "Unprotected native callback graph reproduces unbounded cross activation");
Check(Resolve(true, true, true, own) == 3, "Both initial secondary effects run once and their cross activation terminates");
Check(Resolve(true, true, false, own) == 2, "Trample alone retains its secondary impact");
Check(Resolve(true, false, true, own) == 2, "Explosive alone retains its secondary impact");
Check(Resolve(true, false, false, own) == 1, "Ordinary damage has no added secondary effect");
Check(Resolve(true, true, true, foreign) == -1, "Other clans keep their original behavior; protection is local");
Console.WriteLine(JsonSerializer.Serialize(new { success = errors.Count == 0, checks, errors }, new JsonSerializerOptions { WriteIndented = true }));
return errors.Count == 0 ? 0 : 1;
// 2026-10-17-1448||script-corregir-trample-explosive.py||repos/MT2/repos-propios/mt2-ww-inc/tests/DamageTraits/Program.cs||Regresión de recursión cruzada, conservación de impactos iniciales y aislamiento por propietario
