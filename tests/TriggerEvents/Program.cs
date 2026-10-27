using System.Collections;
using System.Text.Json;
using MonsterTrain2.SharedTriggers;
var errors=new List<string>();int checks=0;
void Check(bool ok,string message){checks++;if(!ok)errors.Add(message);}
void Run(IEnumerator e){try{while(e.MoveNext())if(e.Current is IEnumerator next)Run(next);}finally{(e as IDisposable)?.Dispose();}}
OverhealEvent.Trigger=CharacterTriggerData.Trigger.Overheal;ArcaneSlayEvent.Trigger=CharacterTriggerData.Trigger.ArcaneSlay;
var core=new FakeCore();var outer=new CharacterState();var inner=new CharacterState();var spell=new CardState{Id="spell"};
CardEffectParams Params(CharacterState unit,CharacterTriggerData.Trigger trigger)=>new(){selfTarget=unit,sourceCharacterTriggerState=new(trigger)};
var p=Params(outer,OverhealEvent.Trigger);var q=Params(inner,OverhealEvent.Trigger);
IEnumerator HealBody(CharacterState unit,CharacterTriggerData.Trigger trigger,CharacterState.FireTriggersData data)
{
 _=trigger;_=data;
 if(unit==outer)
 {
  Check(OverhealEvent.Amount(p)==12&&OverhealEvent.Source==spell,"Outer amount and source survive queueing");
  Check(OverhealEvent.Amount(q)==0,"Amount is not exposed to a different owner");
  Check(OverhealEvent.Amount(Params(outer,CharacterTriggerData.Trigger.Other))==0,"Wrong trigger cannot read an amount");
  yield return OverhealEvent.Dispatch(core,inner,7,null);
  Check(OverhealEvent.Amount(p)==12&&OverhealEvent.Source==spell,"Nested healing restores outer payload");
 }
 else Check(OverhealEvent.Amount(q)==7&&OverhealEvent.Source==null,"Nested payload has its own amount and nullable source");
}
core.Combat.Body=HealBody;Run(OverhealEvent.Dispatch(core,outer,12,spell));
Check(core.Combat.Queued.Count==2&&core.Combat.Queued[0].data.paramInt==12,"Native queue holds amount as data, not repeated trigger count");
Check(OverhealEvent.Amount(p)==0&&OverhealEvent.Source==null,"Completed event leaves no execution context");
int before=core.Combat.Queued.Count;Run(OverhealEvent.Dispatch(core,outer,0,spell));Run(OverhealEvent.Dispatch(core,outer,-1,spell));
outer.IsAlive=false;Run(OverhealEvent.Dispatch(core,outer,2,spell));outer.IsAlive=true;outer.IsDestroyed=true;Run(OverhealEvent.Dispatch(core,outer,2,spell));outer.IsDestroyed=false;
Check(core.Combat.Queued.Count==before,"Nonpositive and dead/destroyed events do not queue");
IEnumerator FailBody(CharacterState unit,CharacterTriggerData.Trigger trigger,CharacterState.FireTriggersData data){_=unit;_=trigger;_=data;yield return null;throw new InvalidOperationException("consumer failed");}
core.Combat.Body=FailBody;bool failed=false;try{Run(OverhealEvent.Dispatch(core,outer,9,spell));}catch(InvalidOperationException){failed=true;}
Check(failed&&OverhealEvent.Amount(p)==0&&OverhealEvent.Source==null,"Consumer exception unwinds Overheal scope");
core.Combat.Listeners.AddRange([outer,inner]);var arcaneParams=Params(outer,ArcaneSlayEvent.Trigger);
IEnumerator SlayBody(CharacterState unit,CharacterTriggerData.Trigger trigger,CharacterState.FireTriggersData data)
{
 _=trigger;_=data;Check(ArcaneSlayEvent.Source(Params(unit,ArcaneSlayEvent.Trigger))==spell,"Each native listener receives the responsible spell");
 Check(ArcaneSlayEvent.Source(Params(unit,CharacterTriggerData.Trigger.Other))==null,"Slay source is scoped to the correct trigger");
 Check(ArcaneSlayEvent.Source(Params(new CharacterState(),ArcaneSlayEvent.Trigger))==null,"Slay source is scoped to its listener");yield break;
}
core.Combat.Body=SlayBody;before=core.Combat.Queued.Count;Run(ArcaneSlayEvent.Dispatch(core,spell,0,2));
Check(core.Combat.Queued.Count-before==4,"Two kills trigger two listeners twice through the native room path");
Check(ArcaneSlayEvent.Source(arcaneParams)==null&&ArcaneSlayEvent.EnqueuingSource==null,"Completed Slay dispatch leaves no source behind");
core.Combat.Body=FailBody;failed=false;try{Run(ArcaneSlayEvent.Dispatch(core,spell,0,1));}catch(InvalidOperationException){failed=true;}
Check(failed&&ArcaneSlayEvent.Source(arcaneParams)==null&&ArcaneSlayEvent.EnqueuingSource==null,"Consumer failure unwinds both Slay execution and enqueue scopes");
core.Combat.ThrowBeforeRoom=true;failed=false;try{Run(ArcaneSlayEvent.Dispatch(core,spell,0,1));}catch(InvalidOperationException){failed=true;}
Check(failed&&ArcaneSlayEvent.EnqueuingSource==null,"Native room failure unwinds queued source");
WwInc.ArcaneSlayConductor.Register();
var receiver=new CharacterState();var slain=new CharacterState{TeamType=Team.Type.Heroes,IsAlive=false};
var hit=new Conductor.Extensions.TriggerOnCharacterHitParams {Character=receiver,DamagedCharacter=slain,OriginalDamage=42,DamageParams=new CharacterState.ApplyDamageParams{damageSourceCard=spell}};
var callback=Conductor.Extensions.CharacterTriggerExtensions.Registered!;
Check(callback!=null&&callback(hit,out var _),"Arcane Slay registers with Conductor and accepts a killing spell");
callback!(hit,out var receipt);
var captured=(ArcaneSlayEvent.Data)receipt!.Value.fireTriggersData;
Check(captured.Source==spell&&captured.paramInt==42&&captured.overrideTargetCharacter==slain,"Conductor payload retains spell identity, damage and victim");
receiver.HasTrigger=false;Check(!callback(hit,out _),"Units without Arcane Slay do not receive custom trigger events");receiver.HasTrigger=true;
spell.Ability=true;Check(!callback(hit,out _),"Ability kills do not become Arcane Slay");spell.Ability=false;
slain.IsAlive=true;Check(!callback(hit,out _),"Nonlethal spell damage does not trigger Arcane Slay");slain.IsAlive=false;
slain.TeamType=Team.Type.Monsters;Check(callback(hit,out _),"Friendly-fire spell kills trigger Arcane Slay");slain.TeamType=Team.Type.Heroes;
receiver.TeamType=Team.Type.Heroes;Check(!callback(hit,out _),"Enemy units cannot receive this clan's spell-slay reward");receiver.TeamType=Team.Type.Monsters;
WwInc.AcademicRuntime.ConversionDepth=1;Check(!callback(hit,out _),"Converted healing damage cannot recurse into Arcane Slay");WwInc.AcademicRuntime.ConversionDepth=0;
hit.DamageParams.damageSourceCard=null;Check(!callback(hit,out _),"Attack-only damage has no spell-slay source");

Console.WriteLine(JsonSerializer.Serialize(new{success=errors.Count==0,checks,errors}));return errors.Count==0?0:1;
// 2026-10-25-1850||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/TriggerEvents/Program.cs||Regresiones de reentrada, fuente, propietario, cantidad y limpieza de eventos independientes ante excepción
// 2026-10-26-2035||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/TriggerEvents/Program.cs||Regresiones del adaptador Conductor y conservación de fuente, aliados, habilidades y conversión
// 2026-10-27-1428||codex-ww-inc||salidas/build/ww-inc/tests/TriggerEvents/Program.cs||Verificar baja aliada aprobada sin convertir habilidades ni impactos no letales
// 2026-10-27-1540||codex-ww-inc||salidas/build/ww-inc/tests/TriggerEvents/Program.cs||Verificar ausencia de eventos Arcane Slay en unidades ajenas al trigger
