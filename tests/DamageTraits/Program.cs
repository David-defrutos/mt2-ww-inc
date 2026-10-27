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
void Run(IEnumerator routine){try{while(routine.MoveNext())if(routine.Current is IEnumerator next)Run(next);}finally{(routine as IDisposable)?.Dispose();}}
CardTraitData Trait(Type type)=>new(){State=type.FullName!};
var expiry=new CardTraitAcademicNextUseUpgrade();
var expTrait=Trait(typeof(CardTraitDamageOverflow));var marker=Trait(typeof(CardTraitAcademicNextUseUpgrade));
var marked=new CardUpgradeState();marked.Traits.AddRange([expTrait,marker]);
var unrelated=new CardUpgradeState();unrelated.Traits.Add(expTrait);
var lasting=new CardUpgradeData();lasting.Traits.Add(expTrait);
var ownership=new CardState();ownership.Modifiers.Upgrades.AddRange([marked,unrelated]);
CardTraitAcademicNextUseUpgrade.ReplaceCoveredBy(ownership,lasting);
Check(!ownership.Modifiers.Upgrades.Contains(marked)&&ownership.Modifiers.Upgrades.Contains(unrelated),"Lasting Explosive replaces only the marked next-use grant, preserving independent sources");
var compound=new CardUpgradeState();compound.Traits.AddRange([expTrait,marker,Trait(typeof(CardTraitTrample))]);
ownership.Modifiers.Upgrades.Add(compound);CardTraitAcademicNextUseUpgrade.ReplaceCoveredBy(ownership,lasting);
Check(ownership.Modifiers.Upgrades.Contains(compound),"A grant of Explosive cannot remove a marked upgrade that also owns another keyword");
var plain=new CardState();plain.Modifiers.Upgrades.Add(unrelated);CardTraitAcademicNextUseUpgrade.ReplaceCoveredBy(plain,lasting);
Check(plain.Modifiers.Upgrades.SequenceEqual([unrelated]),"An existing lasting Explosive source survives a redundant grant");
var expiringCard=new CardState();expiringCard.Modifiers.Upgrades.AddRange([marked,unrelated]);expiry.Own=marker;
var nativeCore=new FakeCore();Run(expiry.OnCardPlayed(expiringCard,nativeCore));
Check(!expiringCard.Modifiers.Upgrades.Contains(marked)&&expiringCard.Modifiers.Upgrades.Contains(unrelated),"After use, expiry removes only its exact native upgrade");
expiringCard.Modifiers.Upgrades.Add(marked);nativeCore.Save.PreviewMode=true;Run(expiry.OnCardPlayed(expiringCard,nativeCore));
Check(expiringCard.Modifiers.Upgrades.Contains(marked),"Hover cannot expire a real next-use grant");
nativeCore.Save.PreviewMode=false;AcademicRuntime.Simulating=true;Run(expiry.OnCardPlayed(expiringCard,nativeCore));
Check(expiringCard.Modifiers.Upgrades.Contains(marked),"Simulation cannot expire a real next-use grant either");AcademicRuntime.Simulating=false;

void Trample(CardState spell,CharacterState victim,int damage,FakeCore context)
{
 IEnumerator result=TrampleOverflowGuard.Empty();
 bool native=TrampleFromOverflowHook.Prefix(new(){damageSourceCard=spell,targetCharacter=victim,damage=damage,damageType=Damage.Type.Default},ref result,context);
 Check(!native,"Own Trample uses approved chain instead of abandoned one-hop native trait");Run(result);
}
var chained=new FakeCore();var initial=new CharacterState{HP=10};var second=new CharacterState{HP=20};var third=new CharacterState{HP=40};chained.Combat.Board.AddRange([initial,second,third]);
Trample(own,initial,70,chained);
Check(initial.HP==10&&second.HP==0&&third.HP==0&&chained.Combat.Hits.Count==2,"Trample transfers 60 through two enemies while original hit remains owned by native caller");
Check(chained.Combat.Hits.All(hit=>hit.type==Damage.Type.SpellOverflow&&hit.card==own),"Secondary damage retains spell attribution and uses native non-amplified overflow rules");
var backCore=new FakeCore();var front=new CharacterState{HP=20};var middle=new CharacterState{HP=30};var back=new CharacterState{HP=5};backCore.Combat.Board.AddRange([front,middle,back]);
Trample(own,back,60,backCore);
Check(front.HP==0&&middle.HP==0&&backCore.Combat.Hits.Count==2,"Targeting back enemy still transfers to living front enemies in deterministic order");
var allyCore=new FakeCore();var sacrificedAlly=new CharacterState{HP=5};var enemyA=new CharacterState{HP=10};var enemyB=new CharacterState{HP=20};allyCore.Combat.Board.AddRange([enemyA,enemyB]);
Trample(own,sacrificedAlly,30,allyCore);
Check(enemyA.HP==0&&enemyB.HP==5,"Excess from an allied target transfers to enemies");
foreach(bool shield in new[]{false,true})
{
 var mitigation=new FakeCore();var first=new CharacterState{HP=5};var defense=new CharacterState{HP=10,Armor=shield?0:10,Shield=shield};var survivor=new CharacterState{HP=20};mitigation.Combat.Board.AddRange([first,defense,survivor]);
 Trample(own,first,25,mitigation);
 Check(survivor.HP==20&&mitigation.Combat.Hits.Count==1,shield?"Damage Shield absorbs transfer and stops remaining damage":"Armor and Health absorb transfer before computing further excess");
}
var weakCore=new FakeCore();var weakInitial=new CharacterState{HP=5};var weakEnemy=new CharacterState{HP=10,Weakness=4};var lastEnemy=new CharacterState{HP=20};weakCore.Combat.Board.AddRange([weakInitial,weakEnemy,lastEnemy]);
Trample(own,weakInitial,25,weakCore);
Check(lastEnemy.HP==10&&weakEnemy.Weakness==4,"Spell Weakness does not multiply or get consumed by transferred overflow damage");
Check(SpellTrample.Excess(int.MaxValue,1)==int.MaxValue-1&&SpellTrample.Excess(-10,1)==0,"Excess arithmetic cannot overflow or create negative transfer");
var foreignResult=TrampleOverflowGuard.Empty();
Check(TrampleFromOverflowHook.Prefix(new(){damageSourceCard=foreign,targetCharacter=new CharacterState(),damage=100},ref foreignResult,new FakeCore()),"Other clans retain their native Trample behavior even with live managers");

Check(!SpellTrample.Own(new CardState{AssetName=own.AssetName,Type=CardType.Monster}),"Unit cards do not gain the spell-specific Trample rule");
Check(!SpellTrample.Own(new CardState{AssetName=own.AssetName,Ability=true}),"Unit abilities do not gain the spell-specific Trample rule");
Console.WriteLine(JsonSerializer.Serialize(new { success = errors.Count == 0, checks, errors }, new JsonSerializerOptions { WriteIndented = true }));
return errors.Count == 0 ? 0 : 1;
// 2026-10-17-1448||script-corregir-trample-explosive.py||repos/MT2/repos-propios/mt2-ww-inc/tests/DamageTraits/Program.cs||Regresión de recursión cruzada, conservación de impactos iniciales y aislamiento por propietario
// 2026-10-26-2038||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/DamageTraits/Program.cs||Regresiones de propiedad duradera, keyword independiente, cobertura parcial y predicción
// 2026-10-26-2043||codex-ww-inc||salidas/build/ww-inc/tests/DamageTraits/Program.cs||Cubrir señal de simulación independiente de SaveManager y eliminar aviso de campo sin asignar
// 2026-10-27-1539||codex-ww-inc||salidas/build/ww-inc/tests/DamageTraits/Program.cs||Regresiones reales de cadena completa, objetivo trasero/aliado, armadura, escudo, weakness, autoría y aislamiento
// 2026-10-27-1545||codex-ww-inc||salidas/build/ww-inc/tests/DamageTraits/Program.cs||Verificar aislamiento de unidades y habilidades frente a Trample de hechizos
