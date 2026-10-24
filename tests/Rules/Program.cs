using WwInc;
using System.Text.Json;
var errors=new List<string>();int checks=0;
void Check(bool condition,string description){checks++;if(!condition)errors.Add(description);}

Check(AcademicMath.ExcessHealing(4,30,30)==4,"Full-health Emeritus converts all four healing to Armor");
Check(AcademicMath.ExcessHealing(4,28,30)==2,"Partial healing converts only two excess points");
Check(AcademicMath.ExcessHealing(4,20,30)==0,"Healing below missing health grants no Armor");
Check(AcademicMath.ExcessHealing(0,30,30)==0,"Blocked healing grants no Armor");
Check(AcademicMath.ExcessHealing(9998,30,30)==9998,"Healing below the full-heal threshold still converts excess");
Check(AcademicMath.ExcessHealing(9999,30,30)==0,"Full-heal sentinel does not grant Armor or hostile damage");
Check(AcademicMath.ExcessHealing(10000,28,30)==0,"Healing above 9999 does not trigger Overheal");
Check(AcademicMath.ExcessHealing(int.MaxValue,30,30)==0,"Extreme healing cannot generate Overheal");


Check(AcademicMath.ExcessHealing(22,13,20)==15,"Hostile Medicine converts 22 healing at 13/20 health into fifteen damage per enemy");
Check(AcademicMath.ExcessHealing(22,20,20)==22,"Hostile Medicine at full health converts the whole heal");
Check(AcademicMath.ExcessHealing(7,13,20)==0,"Healing exactly to maximum causes no hostile damage");


Check(AcademicMath.SharedHealing(100,4)==25,"First Aid splits one hundred damage among four allies as twenty-five each");
Check(AcademicMath.SharedHealing(100,5)==20,"First Aid splits one hundred damage among five allies as twenty each");
Check(AcademicMath.SharedHealing(100,3)==33,"Equal shares use normal integer division without favoring front units");
Check(AcademicMath.SharedHealing(100,0)==0&&AcademicMath.SharedHealing(0,4)==0,"Empty floor and zero damage cannot heal or divide by zero");

Check(AcademicMath.OverhealArmor(130,50)==50,"130 excess healing at maximum health 50 grants 50 Armor");
Check(AcademicMath.OverhealArmor(20,50)==20,"Excess below the maximum-health cap is preserved");
Check(AcademicMath.OverhealArmor(50,50)==50,"Excess equal to maximum health is unchanged");
Check(AcademicMath.OverhealArmor(130,50)+AcademicMath.OverhealArmor(130,50)==100,"Separate Overheal activations each have their own cap");
Check(AcademicMath.OverhealArmor(130,75)==75,"Current increased maximum health raises the per-event cap");
Check(AcademicMath.OverhealArmor(0,50)==0&&AcademicMath.OverhealArmor(130,0)==0,"No excess or no maximum health grants no Armor");

int[] Separate(int[] source)
{
    var units=source.ToList();
    for(int i=0;i+1<units.Count&&AcademicMath.CanInsertChaperone(units.Count);i++)
        if(AcademicMath.Mixed(units[i],units[i+1])){units.Insert(i+1,0);i++;}
    return units.ToArray();
}
Check(Separate(new[]{1,2,1}).SequenceEqual(new[]{1,0,2,0,1}),"Every adjacent mixed pair separated left to right");
Check(Separate(new[]{2,1,2,1}).SequenceEqual(new[]{2,0,1,0,2,0,1}),"Multiple pairs and reverse orientation reach capacity 7");
Check(Separate(new[]{1,2,1,2,1,2}).SequenceEqual(new[]{1,0,2,1,2,1,2}),"With six units only the leftmost pair is separated");
Check(Separate(new[]{1,2,1,2,1,2,1}).Length==7,"A full floor spawns no chaperones");
Check(Separate(new[]{1,0,2}).SequenceEqual(new[]{1,0,2}),"An existing chaperone prevents another spawn");
Check(Separate(new[]{1,1,2}).SequenceEqual(new[]{1,1,0,2}),"Same-subtype neighbors remain together");
Check(AcademicMath.Mixed(2,1)&&AcademicMath.Mixed(1,2),"Both Wizard and Witch orientations work");
Check(!AcademicMath.Mixed(0,1)&&!AcademicMath.Mixed(1,1),"Non-mixed pairs do not qualify");
Check(AcademicMath.Tuition(29,1)==(1,0),"The 30th damage point grants one discount");
Check(AcademicMath.Tuition(0,89)==(2,29),"Multiple discounts preserve the remainder");
Check(AcademicMath.Tuition(29,0)==(0,29),"Blocked or zero damage grants no discount");
Check(new[]{0,1,2,3}.Select(AcademicMath.RedirectionDamage).SequenceEqual(new[]{0,10,20,30}),"Redirection uses previous Dazed stacks");
Check(!AcademicMath.CanInsertChaperone(7)&&AcademicMath.CanInsertChaperone(6),"Capacity is a unit count, independent of pips");
Check(AcademicMath.ServingStats(0)==(0,1),"No Conduit produces 0/1");
Check(AcademicMath.ServingStats(5)==(5,6),"Conduit 5 produces 5/6");
Check(AcademicMath.ServingStats(5+10+1)==(16,17),"All floor Conduit sources contribute");
Check(AcademicMath.ServingStats(-3)==(0,1),"Negative Conduit cannot create invalid stats");
Check(AcademicMath.ServingStats(long.MaxValue)==(int.MaxValue-1,int.MaxValue),"Summed Conduit cannot overflow Health");
Check(AcademicMath.ChampionRank(new[]{"david.wwinc-Upgrade-WW_FratBro1","david.wwinc-Upgrade-WW_FratBro3","WW_FratBro2"},"WW_FratBro")==3,"Only highest rank of same path contributes");
Check(AcademicMath.ChampionRank(new[]{"WW_FratBro10","WW_Other3"},"WW_FratBro")==0,"Unrelated and malformed rank IDs grant no aura");
Check(AcademicMath.ChampionAura(2,3)==30,"Frat Bro or Queen Bee rank II counts three living opposite mages");
Check(AcademicMath.ChampionAura(3,0)==0,"Last opposite mage dying removes social aura");
Check(AcademicMath.ChampionAura(0,5)==0,"Incant-only Witch gains no Queen Bee aura");
var permit=new RingPermitLedger();
object stampA=new string("WW_E01".ToCharArray()),stampB=new string("WW_E01".ToCharArray());
permit.BeginBattle("run:0:1",false);
Check(permit.Available("run:0:1",stampA)&&permit.Available("run:0:1",stampB),"Both Stamp copies start with a Purge");
permit.MarkUsed("run:0:1",stampA);
Check(!permit.Available("run:0:1",stampA),"A spent equipment cannot grant a second Purge");
Check(permit.Available("run:0:1",stampB),"Same card data ID on a second physical copy has its own Purge");
permit.BeginBattle("run:0:1",false);
Check(!permit.Available("run:0:1",stampA)&&permit.Available("run:0:1",stampB),"Same ring setup preserves per-copy use");
permit.MarkUsed("run:0:1",stampB);
Check(!permit.Available("run:0:1",stampB)&&!permit.Available("run:0:1",stampA),"Each copy independently spends its own Purge");
object movedStamp=stampA;
Check(!permit.Available("run:0:1",movedStamp),"Returning and re-equipping the same card does not refresh its use");
permit.BeginBattle("run:0:1",true);
Check(permit.Available("run:0:1",stampA)&&permit.Available("run:0:1",stampB),"Replay can reconstruct use of both copies");
permit.MarkUsed("run:0:1",stampA);
Check(!permit.Available("run:0:1",stampA)&&permit.Available("run:0:1",stampB),"Replayed first copy does not spend second copy");
permit.BeginBattle("run:0:2",false);
Check(permit.Available("run:0:2",stampA)&&permit.Available("run:0:2",stampB),"New ring refreshes both equipment copies");
SnapshotFixture.Values["card"]=new(){3,2,1};SnapshotFixture.Stack.Push("first");SnapshotFixture.Stack.Push("last");SnapshotFixture.Scalar=4;SnapshotFixture.Used.Add("real");
var snapshot=new CollectionSnapshot(typeof(SnapshotFixture));
SnapshotFixture.Values["card"].RemoveAt(0);SnapshotFixture.Values["other"]=new(){99};SnapshotFixture.Stack.Pop();SnapshotFixture.Scalar=42;SnapshotFixture.Used.Remove("real");SnapshotFixture.Used.Add("preview");
snapshot.Restore();
Check(SnapshotFixture.Values["card"].SequenceEqual(new[]{3,2,1}),"Preview rollback restores nested attack counters");
Check(SnapshotFixture.Used.SetEquals(new[]{"real"}),"Preview cannot spend real once-per-turn triggers");
Check(!SnapshotFixture.Values.ContainsKey("other")&&SnapshotFixture.Scalar==4,"Preview rollback removes simulated entries and restores scalar state");
Check(SnapshotFixture.Stack.ToArray().SequenceEqual(new[]{"last","first"}),"Preview rollback preserves stack order");
var outer=new CollectionSnapshot(typeof(SnapshotFixture));SnapshotFixture.Scalar=8;
var inner=new CollectionSnapshot(typeof(SnapshotFixture));SnapshotFixture.Scalar=12;inner.Restore();
Check(SnapshotFixture.Scalar==8,"Nested preview restores its parent simulation");outer.Restore();Check(SnapshotFixture.Scalar==4,"Outer preview restores real state");
var cleanup=new List<Action>();var costs=new Dictionary<string,int>();
void Fixed(int cost)
{
    bool had=costs.TryGetValue("spell",out int previous);costs["spell"]=cost;
    cleanup.Add(()=>{if(had)costs["spell"]=previous;else costs.Remove("spell");});
}
Fixed(0);Fixed(1);TurnCleanup.Run(cleanup);
Check(!costs.ContainsKey("spell"),"Overlapping temporary fixed costs restore absence of an original override");
costs["spell"]=3;Fixed(0);Fixed(1);TurnCleanup.Run(cleanup);
Check(costs["spell"]==3,"Overlapping temporary fixed costs restore an existing battle override");
Check(cleanup.Count==0,"Cleanup releases executed callbacks rather than retaining prior-turn state");
Check(AcademicMath.EffectiveX(0,0)==0,"Spike X=0 has no weakness or cooldown reduction");
Check(AcademicMath.EffectiveX(1,0)==1&&AcademicMath.EffectiveX(3,0)==3,"Spike scales one-for-one with Ember spent");
Check(AcademicMath.EffectiveX(2,1)==3,"Native X-cost upgrade bonuses contribute to spike strength");
Check(AcademicMath.EffectiveX(0,-1)==0&&AcademicMath.EffectiveX(int.MaxValue,int.MaxValue)==int.MaxValue,"X calculation clamps invalid negative values and overflow");
Check(AcademicMath.ConsumedDamage(10,10,0)==10,"Empty consume pile preserves Bibliography base damage");
Check(AcademicMath.ConsumedDamage(10,10,2)==30,"Two consumed cards add twenty damage");
Check(AcademicMath.ConsumedDamage(15,10,0)==15,"Chair or stone bonus remains with no consumed cards");
Check(AcademicMath.ConsumedDamage(35,10,2)==55,"Power including Attuned is not counted twice");
Check(AcademicMath.ConsumedDamage(int.MaxValue,10,2)==int.MaxValue,"Consumed bonus cannot overflow into zero damage");
Check(AcademicMath.EducationPower(15,1)==7,"Education rounds front Attack down");
Check(AcademicMath.EducationPower(20,2)==20,"Two professors stack their first-spell bonus");
Check(AcademicMath.EducationPower(0,1)==0,"Zero Attack gives no bonus and preserves spell base power");
var medicineTurns=new HashSet<string>();
Check(!AcademicMath.FirstQualifyingSpell(medicineTurns,"damage",0,0),"Zero damage does not consume first qualifying spell");
Check(AcademicMath.FirstQualifyingSpell(medicineTurns,"damage",0,10),"First damaging spell qualifies automatically");
Check(!AcademicMath.FirstQualifyingSpell(medicineTurns,"damage",0,20),"Second damaging spell on the same floor does not qualify");
Check(AcademicMath.FirstQualifyingSpell(medicineTurns,"healing",0,5),"Healing has an independent first-spell record");
Check(AcademicMath.FirstQualifyingSpell(medicineTurns,"damage",1,10),"Other floors have independent first-spell records");
medicineTurns.Clear();
Check(AcademicMath.FirstQualifyingSpell(medicineTurns,"damage",0,10),"New turn re-enables each passive");
int refundCount=0,refunds=0;
for(int spell=1;spell<=12;spell++)
{
    var next=AcademicMath.RefundProgress(refundCount,spell==3?10:1);
    refundCount=next.count;if(next.refund)refunds++;
    Check(next.count==spell%5&&next.refund==(spell%5==0),"Refund counts spells, not enemies; cycles correctly at spell "+spell);
}
Check(refunds==2&&refundCount==2,"Two refunds after twelve qualifying spells");
var noKill=AcademicMath.RefundProgress(4,0);
Check(noKill.count==4&&!noKill.refund,"A spell without kills does not advance or spend a ready counter");
var fifth=AcademicMath.RefundProgress(4,25);
Check(fifth.count==0&&fifth.refund,"Fifth spell resets even if it kills many enemies");
Console.WriteLine(JsonSerializer.Serialize(new {success=errors.Count==0,checks,errors},new JsonSerializerOptions{WriteIndented=true}));return errors.Count==0?0:1;
// 2026-10-10-0020||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Casos de comportamiento para orden determinista, adyacencia, umbrales y Dazed
// 2026-10-10-0058||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Casos de producción con cero, múltiples fuentes y límites de Conduit

internal static class SnapshotFixture
{
    internal static int Scalar;
    internal static readonly Dictionary<string,List<int>> Values=new();
    internal static readonly Stack<string> Stack=new();
    internal static readonly HashSet<string> Used=new();
}
// 2026-10-10-2358||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Once casos de rollback anidado y permiso por ring durante replay
// 2026-10-10-2359||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Siete casos de multisenda, rangos válidos y aura por aliados vivos
// 2026-10-11-0002||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Probar que simular no consume triggers de una vez por turno
// 2026-10-11-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Casos de doble Extinguish, retorno posterior y copias distintas
// 2026-10-11-0010||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Sustituir prueba de mano simulada por contrato del flujo real de recuperación

// 2026-10-11-1039||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Tres regresiones de costes temporales anidados y limpieza de callbacks

// 2026-10-11-1104||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Cuatro casos de Spike X cero, inversión, bono y límites

// 2026-10-11-1651||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Actualizar regresiones de umbral exacto 30, varios descuentos y daño bloqueado

// 2026-10-12-0055||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Regresiones de daño base con cero consumidas y preservación única de Power y Attuned

// 2026-10-12-0915||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Probar bono pasivo de Educación, redondeo, copias y ataque cero

// 2026-10-12-0923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Probar primer hechizo apto, cero, segundo hechizo, tipos independientes, pisos y reinicio de turno

// 2026-10-12-1808||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Probar exceso con vida completa, parcial, sin exceso e inmunidad

// 2026-10-12-2053||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Probar ejemplo exacto 13/20 +22 y ausencia de exceso al curar 7

// 2026-10-12-2056||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Probar repartos 100/4 y 100/5, resto entero y piso vacío
// 2026-10-12-2232||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Retirar dos expectativas de balance reemplazado del antiguo robo Dean
// 2026-10-17-0910||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Pruebas de ciclos 5/10, no bajas y multibaja contada como un hechizo
// 2026-10-19-2259||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Precisar que el ledger controla la fase Purge y no bloquea los usos Consume
// 2026-10-19-2359||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Ledger vuelve a documentar límite de una concesión Purge por ring
// 2026-10-20-0017||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Regresiones reales de copias con mismo ID, reequipo, replay y nuevo ring
// 2026-10-21-2043||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Regresiones Overheal: 9998 permitido; 9999, 10000 e int.MaxValue excluidos
// 2026-10-21-2059||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Program.cs||Seis regresiones de límite de armadura por evento y salud máxima variable
