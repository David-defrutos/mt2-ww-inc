using System.Collections;
using System.Text.Json;
using WwInc;
var errors=new List<string>();int checks=0;
void Check(bool ok,string message){checks++;if(!ok)errors.Add(message);}
void Run(IEnumerator e){try{while(e.MoveNext())if(e.Current is IEnumerator next)Run(next);}finally{(e as IDisposable)?.Dispose();}}
var core=new FakeCore();var damage=new CardState();damage.Effects.Add(new(Team.Type.Heroes,TargetMode.DropTargetCharacter));
var mixed=new CardState();mixed.Effects.Add(new(Team.Type.Monsters|Team.Type.Heroes,TargetMode.DropTargetCharacter));
var area=new CardState();area.Effects.Add(new(Team.Type.Heroes,TargetMode.Room));
var trade=new CardState();trade.Effects.Add(new(Team.Type.Monsters,TargetMode.DropTargetCharacter));trade.Effects.Add(new(Team.Type.Monsters|Team.Type.Heroes,TargetMode.LastTargetedCharacters));
Check(LightningRodSafety.Eligible(damage)&&LightningRodSafety.Eligible(mixed)&&LightningRodSafety.Eligible(area),"Direct, Both and area enemy spells remain eligible");
Check(!LightningRodSafety.Eligible(trade),"Immortal Trade is ally-only despite its Both last-target effect");
var unit=new CharacterState{TeamType=Team.Type.Monsters};
core.Save.PreviewMode=true;unit.Preview();Run(LightningRodSafety.Feedback(unit,damage,core));
Check(core.Player.Energy==0&&unit.GetStatusEffectStacks("dazed")==1&&unit.Real.Count==0,"Hover predicts Dazed without granting real Ember or changing real statuses");
unit.TemporaryPreviewsEnabled=false;Check(unit.GetStatusEffectStacks("dazed")==0&&unit.HP==40,"Leaving preview restores live unit");
unit.Real["dazed"]=1;unit.Preview();Run(LightningRodSafety.Feedback(unit,damage,core));
Check(unit.HP==30&&unit.RealHP==40&&unit.Real["dazed"]==1&&core.Player.Energy==0,"Second preview predicts ten damage only in temporary state");
unit.TemporaryPreviewsEnabled=false;Run(LightningRodSafety.Feedback(unit,damage,core));
Check(unit.HP==40&&unit.Real["dazed"]==1,"Unprepared preview target cannot mutate live HP or Dazed");
core.Save.PreviewMode=false;core.Combat.IsRunningCardPlayPreview=true;unit.Preview();Run(LightningRodSafety.Feedback(unit,damage,core));
Check(core.Player.Energy==0&&unit.RealHP==40,"Native preview-in-progress flag also blocks real refunds");
core.Combat.IsRunningCardPlayPreview=false;unit.TemporaryPreviewsEnabled=false;Run(LightningRodSafety.Feedback(unit,damage,core));
Check(core.Player.Energy==1&&unit.HP==30&&unit.Real["dazed"]==2,"Real cast charges previous Dazed damage and refunds exactly once");
int hp=unit.HP,daze=unit.Real["dazed"],energy=core.Player.Energy;Run(LightningRodSafety.Feedback(unit,trade,core));
Check(unit.HP==hp&&unit.Real["dazed"]==daze&&core.Player.Energy==energy,"Ally-only spell cannot spend redirection or refund Ember");
var ability=new CardState{Ability=true};ability.Effects.Add(new(Team.Type.Heroes,TargetMode.DropTargetCharacter));Check(!LightningRodSafety.Eligible(ability),"Abilities never redirect");
Check(RepresentativeDamage.Total(50,0,0)==50,"Overkill is not capped to victim HP");
Check(RepresentativeDamage.Total(0,30,10)==20,"Armor-only damage is counted");
Check(RepresentativeDamage.Total(15,20,0)==35,"Armor and postmitigation health damage are added once");
Check(RepresentativeDamage.Total(0,20,20)==0,"Damage Shield contributes zero blocked damage");
Check(RepresentativeDamage.Total(0,20,30)==0,"Armor gained during mitigation does not create negative or phantom damage");
Check(RepresentativeDamage.Total(int.MaxValue,int.MaxValue,0)==int.MaxValue,"Large totals saturate safely");
var victim=new CharacterState();victim.Real["armor"]=20;
var frame=new AcademicRuntime.SpellResolution{Card=damage};AcademicRuntime.Frames.Push(frame);
var hit=RepresentativeDamage.Begin(victim);victim.Real["armor"]=0;RepresentativeDamage.Record(victim,damage,15);RepresentativeDamage.End(hit);
Check(frame.RepresentativeDamage==35&&frame.EffectiveDamage==7,"Representative uses independent total without modifying PhD effective damage");
var ally=new CharacterState{TeamType=Team.Type.Monsters};hit=RepresentativeDamage.Begin(ally);RepresentativeDamage.Record(ally,damage,100);RepresentativeDamage.End(hit);Check(frame.RepresentativeDamage==35,"Friendly fire is not credited");
hit=RepresentativeDamage.Begin(victim);RepresentativeDamage.Record(victim,area,100);RepresentativeDamage.End(hit);Check(frame.RepresentativeDamage==35,"Unrelated spell source cannot contaminate the frame");
AcademicRuntime.ConversionDepth=1;hit=RepresentativeDamage.Begin(victim);RepresentativeDamage.Record(victim,damage,100);RepresentativeDamage.End(hit);AcademicRuntime.ConversionDepth=0;Check(frame.RepresentativeDamage==35,"Healing conversion damage cannot recurse into Representative");
AcademicRuntime.Frames.Pop();
var sizeHook=typeof(SpatialCreditNotification).Assembly.GetType("WwInc.SpatialCreditNotificationHook")!.GetMethod("Postfix",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
string Format(string asset,int n,string original)
{
 object?[] args=[new CardUpgradeState{Asset=asset,Size=n},original];sizeHook.Invoke(null,args);return (string)args[1]!;
}
Check(Format("david.wwinc-Upgrade-WW_S23_Upgrade",-1,"+-1 size")=="-1 size","Spatial Credit displays one negative sign and keeps native notification format");
Check(Format("other.clan-Upgrade-WW_S23_Upgrade",-1,"+-1 size")=="+-1 size","Foreign upgrades are untouched");
Check(Format("david.wwinc-Upgrade-WW_S23_Upgrade",1,"+1 size")=="+1 size","Positive size keeps native plus formatting");
Check(Format("david.wwinc-Upgrade-WW_S23_Upgrade",0,"0 size")=="0 size","Zero size is untouched");
Check(Format("david.wwinc-Upgrade-WW_Other_Upgrade",-1,"+-1 size")=="+-1 size","Other W&W upgrades are untouched");
Console.WriteLine(JsonSerializer.Serialize(new{success=errors.Count==0,checks,errors}));return errors.Count==0?0:1;
// 2026-10-25-1606||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/Program.cs||Regresiones reales de hover, daño temporal, devolución real, Immortal Trade, armadura, exceso y escudos
// 2026-10-25-1850||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/Program.cs||Probar signo único, preservación de aviso nativo y aislamiento de mejoras ajenas y valores no negativos
