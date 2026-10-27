using System.Collections;
using System.Text.Json;
using WwInc;
var errors=new List<string>();int checks=0;
void Check(bool ok,string message){checks++;if(!ok)errors.Add(message);}
void Run(IEnumerator e){try{while(e.MoveNext())if(e.Current is IEnumerator next)Run(next);}finally{(e as IDisposable)?.Dispose();}}
var core=new FakeCore();AcademicRuntime.Core=core;DissertationMarker.StatusId="ww_dissertation";
var owner=new CharacterState();var enemy=new CharacterState();
CardState Spell(params (CardEffectBase effect,Team.Type team,TargetMode mode)[] effects)
{
 var card=new CardState(new CardData(),core.Save);
 foreach(var e in effects)card.Effects.Add(new(){Effect=e.effect,Team=e.team,Mode=e.mode,Parent=card});
 return card;
}
var both=Team.Type.Heroes|Team.Type.Monsters;
var damage=Spell((new CardEffectDamage(),both,TargetMode.DropTargetCharacter));
var heal=Spell((new CardEffectHeal(),both,TargetMode.DropTargetCharacter));
var mixed=Spell((new CardEffectDamage(),both,TargetMode.DropTargetCharacter),(new CardEffectHeal(),both,TargetMode.DropTargetCharacter));
Check(DissertationTargeting.PreferredSide(damage)==DissertationSide.Enemies,"Native Both damage selects enemy");
Check(DissertationTargeting.PreferredSide(heal)==DissertationSide.Allies,"Native Both heal selects bearer");
Check(DissertationTargeting.PreferredSide(mixed)==DissertationSide.Choose,"Mixed native effects ask");
var mixedHeal=Spell((new CardEffectHealAndDamageRelative(),both,TargetMode.DropTargetCharacter));
Check(DissertationTargeting.PreferredSide(mixedHeal)==DissertationSide.Choose,"Native mixed healing subclass must not be treated as a pure heal");
StatusEffectManager.Instance.Data["spikes"]=new(){Category=StatusEffectData.DisplayCategory.Positive};
foreach(var id in new[]{"dazed","frostbite","pyregel","decay"})StatusEffectManager.Instance.Data[id]=new(){Category=StatusEffectData.DisplayCategory.Negative};
foreach(var id in new[]{"spikes","dazed","frostbite","pyregel","decay","unknown"})
{
 var status=Spell((new CardEffectAddStatusEffect(),both,TargetMode.DropTargetCharacter));
 status.Effects[0].Statuses=[new(){statusId=id,count=2}];
 Check(DissertationTargeting.PreferredSide(status)==(id=="spikes"?DissertationSide.Allies:id=="unknown"?DissertationSide.Choose:DissertationSide.Enemies),id+": status category determines intent, unknown asks");
}
var gold=Spell((new CardEffectRewardGold(),Team.Type.None,TargetMode.Room));
Check(DissertationTargeting.CanBind(gold,core.Combat),"Targetless native money spell is eligible");
DissertationTargeting.AllyChoice=new(){Id="allies"};DissertationTargeting.EnemyChoice=new(){Id="enemies"};
core.Cards.Hand.Add(mixed);SpellRules.ChooseAllies=true;Run(DissertationTargeting.Bind(mixed,owner,core,core));
Check(SpellRules.BoundSides[mixed]==Team.Type.Monsters&&core.Cards.Standby.Contains(mixed),"Ambiguous ally choice is saved before reservation");
Check(SpellRules.Shown!.Count==2&&SpellRules.Shown.All(c=>!core.Cards.Hand.Contains(c)&&!core.Cards.Standby.Contains(c)),"Side options never enter deck/piles");
var other=Spell((new CardEffectBase(),both,TargetMode.DropTargetCharacter));core.Cards.Hand.Add(other);SpellRules.ChooseAllies=false;Run(DissertationTargeting.Bind(other,owner,core,core));
Check(SpellRules.BoundSides[other]==Team.Type.Heroes,"Unknown effect honors enemy choice");
Run(DissertationTargeting.Repeat(mixed,0,owner,enemy));
Check(core.Combat.Calls.TakeLast(2).All(c=>c.target==owner&&c.self==owner),"Both effects of ambiguous spell use selected ally");
core.Combat.Calls.Clear();
var fixedMixed=Spell((new CardEffectDamage(),Team.Type.Heroes,TargetMode.DropTargetCharacter),(new CardEffectHeal(),Team.Type.Monsters,TargetMode.DropTargetCharacter));
Run(DissertationTargeting.Repeat(fixedMixed,0,owner,enemy));
Check(core.Combat.Calls[0].target==enemy&&core.Combat.Calls[1].target==owner,"Native split damage/heal keeps each effect on its own team");
core.Combat.Calls.Clear();enemy.IsDead=true;Run(DissertationTargeting.Repeat(fixedMixed,0,owner,enemy));
Check(core.Combat.Calls.Count==1&&core.Combat.Calls[0].target==owner,"Benefit still resolves after attack kills enemy, dead enemy is not retargeted");
core.Combat.Calls.Clear();enemy.IsDead=false;
var area=Spell((new CardEffectDamage(),both,TargetMode.Room));Run(DissertationTargeting.Repeat(area,0,owner,enemy));
Check(core.Combat.Calls.Single().target==null&&area.Effects.Single().Team==both,"Area keeps native scope and both teams without single-unit override");
core.Combat.Throw=true;bool threw=false;try{Run(DissertationTargeting.Repeat(heal,0,owner,enemy));}catch(InvalidOperationException){threw=true;}
Check(threw&&DissertationTargeting.Casts.Count==0&&AcademicRuntime.Frames.Count==0&&SpellRules.EffectFloor==-1,"Failure unwinds routing/frame/floor scopes");
core.Combat.Throw=false;core.Combat.Calls.Clear();owner.IsDead=true;Run(DissertationTargeting.Repeat(damage,0,owner,enemy));
Check(core.Combat.Calls.Count==0,"Dead bearer does not recast reserved spells");
owner.IsDead=false;enemy.IsDead=false;
// A chosen-unit target must not make an interactive card safe during attacks.
var dialog=Spell((new DialogEffect(),both,TargetMode.DropTargetCharacter));
var hiddenDialog=Spell((new CardEffectDamage(),Team.Type.Heroes,TargetMode.DropTargetCharacter),
    (new DialogEffect(),Team.Type.None,TargetMode.Room));
Check(!DissertationTargeting.CanBind(dialog,core.Combat),"Unit-target UI effect is ineligible");
Check(!DissertationTargeting.CanBind(hiddenDialog,core.Combat),"Any UI effect excludes the whole mixed card, even a targetless second effect");
var dialogGold=Spell((new CardEffectRewardGold(),Team.Type.None,TargetMode.Room),(new DialogEffect(),Team.Type.None,TargetMode.Room));
Check(!DissertationTargeting.CanBind(dialogGold,core.Combat),"Money exception cannot bypass the interactive-effect exclusion");
core.Cards.Hand.Add(dialog);Run(DissertationTargeting.Bind(dialog,owner,core,core));
Check(core.Cards.Hand.Contains(dialog)&&!core.Cards.Standby.Contains(dialog)&&!SpellRules.BoundSides.ContainsKey(dialog),
    "Invalid interactive bind preserves the original card in hand without reserving it");
core.Combat.Calls.Clear();Run(DissertationTargeting.Repeat(hiddenDialog,0,owner,enemy));
Check(core.Combat.Calls.Count==0,"A bound card changed to include UI never executes any effect during an attack");
var safeGold=Spell((new CardEffectRewardGold(),Team.Type.None,TargetMode.Room));
Check(DissertationTargeting.CanBind(safeGold,core.Combat)&&DissertationTargeting.CanBind(damage,core.Combat)&&DissertationTargeting.CanBind(heal,core.Combat),
    "Safety filter retains existing targetless gold, native damage and friendly healing support");
var randomDamage=Spell((new CardEffectDamage(),Team.Type.Heroes,TargetMode.RandomInRoom));
Check(!DissertationTargeting.CanBind(randomDamage,core.Combat),"Native random target mode is ineligible");
var mixedRandom=Spell((new CardEffectDamage(),Team.Type.Heroes,TargetMode.DropTargetCharacter),
    (new CardEffectDamage(),Team.Type.Heroes,TargetMode.RandomInRoom));
Check(!DissertationTargeting.CanBind(mixedRandom,core.Combat),"One random effect excludes an otherwise targeted spell");
var customRandom=Spell((new RandomTargetEffect(),Team.Type.Heroes,TargetMode.DropTargetCharacter));
Check(!DissertationTargeting.CanBind(customRandom,core.Combat),"Native HasRandomTarget override is honored beyond a mode-name whitelist");
core.Cards.Hand.Add(randomDamage);Run(DissertationTargeting.Bind(randomDamage,owner,core,core));
Check(core.Cards.Hand.Contains(randomDamage)&&!core.Cards.Standby.Contains(randomDamage),"Invalid random bind leaves the original in hand");
core.Combat.Calls.Clear();Run(DissertationTargeting.Repeat(mixedRandom,0,owner,enemy));
Check(core.Combat.Calls.Count==0,"An existing bound card becoming random cannot execute during an attack");
Check(DissertationTargeting.CanBind(area,core.Combat),"Deterministic area spells remain eligible");
var alchemy=Spell((new CardEffectAdjustDragonsHoard(),Team.Type.None,TargetMode.Room));
var draw=Spell((new CardEffectDraw(),Team.Type.None,TargetMode.Room));
var typedDraw=Spell((new CardEffectDrawType(),Team.Type.None,TargetMode.Room));
Check(DissertationTargeting.CanBind(alchemy,core.Combat)&&DissertationTargeting.CanBind(draw,core.Combat)&&DissertationTargeting.CanBind(typedDraw,core.Combat),"Native hoard and draw effects are safe targetless bindings");
var unknownGlobal=Spell((new CardEffectBase(),Team.Type.None,TargetMode.Room));
var mixedGlobal=Spell((new CardEffectAdjustDragonsHoard(),Team.Type.None,TargetMode.Room),(new CardEffectBase(),Team.Type.None,TargetMode.Room));
Check(!DissertationTargeting.CanBind(unknownGlobal,core.Combat)&&!DissertationTargeting.CanBind(mixedGlobal,core.Combat),"Unknown global effects cannot piggyback on a safe resource effect");
var dialogAlchemy=Spell((new CardEffectAdjustDragonsHoard(),Team.Type.None,TargetMode.Room),(new DialogEffect(),Team.Type.None,TargetMode.Room));
Check(!DissertationTargeting.CanBind(dialogAlchemy,core.Combat),"Alchemy support never bypasses dialog exclusion");
core.Cards.Hand.Add(alchemy);Run(DissertationTargeting.Bind(alchemy,owner,core,core));
Check(core.Cards.Standby.Contains(alchemy)&&DissertationTargeting.SideLabel(alchemy)=="david.wwinc.DissertationNoTarget","Targetless Alchemy binds without inventing a unit target");
core.Combat.Calls.Clear();Run(DissertationTargeting.Repeat(alchemy,0,owner,enemy));
Check(core.Combat.Calls.Single().target==null,"Alchemy repeat delegates to native room/global effect without retargeting");
var displayed=Spell((new CardEffectDamage(),Team.Type.Heroes,TargetMode.DropTargetCharacter));displayed.Body="Deal 17 damage. Trample.";
core.Data.Cards[displayed.Data.Id]=displayed.Data;core.Data.Cards[alchemy.Data.Id]=alchemy.Data;alchemy.Body="Gain 2 Dragon's Hoard.";
SpellRules.BoundAttacks[owner]=[(displayed,2),(alchemy,1)];
var details=DissertationMarker.Details(owner);
Check(details.Contains("Deal 17 damage. Trample.")&&details.Contains("Gain 2 Dragon's Hoard.")&&details.Contains("Remaining attacks: 2")&&details.Contains("Remaining attacks: 1"),"Full native card descriptions retain each binding's own remaining attacks");
displayed.Body="Deal 25 damage. Trample.";details=DissertationMarker.Details(owner);
Check(details.Contains("Deal 25 damage.")&&!details.Contains("Deal 17 damage."),"Tooltip renders the current CardState without stale cached damage");
Check(CardTooltipHelper.LastState==alchemy,"Native formatter receives the actual reserved card, not a new unupgraded copy");

var replayCore=new FakeCore();AcademicRuntime.Core=replayCore;
var replaySpell=new CardState(new CardData{Id="replayed"},replayCore.Save);
var replayTarget=new CharacterState();replaySpell.Effects.Add(new CardEffectState{Effect=new CardEffectDamage(),Parent=replaySpell,Team=Team.Type.Heroes});
Run(SpellReplay.Cast(replaySpell,1,replayTarget));
Check(replayCore.Combat.Calls.Last().self==null&&SpellReplay.Casts.Count==0,"Memo repetition passes target separately and leaves no fake physical attacker");
SpellReplay.Casts.Push(new SpellReplay.Context{Card=replaySpell,Core=replayCore,Floor=1});
var unrelatedSource=new CardState(new CardData(),replayCore.Save);
Check(SpellReplay.Responsible(replaySpell,unrelatedSource)==replaySpell,"Bound spell supplies its own damage traits despite an owner self-target");
Check(SpellReplay.Responsible(unrelatedSource,unrelatedSource)==unrelatedSource,"Other sources in nested native callbacks are not overwritten");
SpellReplay.Casts.Pop();
Check(SpellReplay.Responsible(replaySpell,unrelatedSource)==unrelatedSource,"Source correction cannot escape the active replay scope");
replayCore.Rooms.Rooms[0]=new RoomState();replayCore.Rooms.Rooms[1]=new RoomState();
SpellReplay.BeginTargeting(replayCore,1);
Check(SpellReplay.SelectedRoom(replayCore.Rooms,0)==1&&SpellReplay.SelectedRoom(new RoomManager(),0)==0,"Logical targeting uses replay floor only for its matching RoomManager");
SpellReplay.EndTargeting();Check(SpellReplay.SelectedRoom(replayCore.Rooms,0)==0,"Target floor override unwinds after collection");
IEnumerator RemoteEffect(){replayCore.Rooms.UI.Selected=1;yield break;}
replayCore.Rooms.UI.Selected=0;Run(SpellReplay.PreserveView(RemoteEffect()));
Check(replayCore.Rooms.UI.Selected==0&&replayCore.Rooms.UI.Restores==1,"Cross-floor lethal effects restore the player's original room once");
replayCore.Save.PreviewMode=true;Run(SpellReplay.PreserveView(RemoteEffect()));
Check(replayCore.Rooms.UI.Restores==1,"Preview cannot move the camera");
replayCore.Save.PreviewMode=false;replayCore.Rooms.UI.Selected=0;replayCore.Combat.PlayerAction=false;Run(SpellReplay.PreserveView(RemoteEffect()));
Check(replayCore.Rooms.UI.Restores==1,"Battle ending cannot restore camera over the game's victory or defeat view");
replayCore.Combat.Throw=true;bool replayFailed=false;try{Run(SpellReplay.Cast(replaySpell,0,replayTarget));}catch(InvalidOperationException){replayFailed=true;}
Check(replayFailed&&SpellReplay.Casts.Count==0,"Native exception unwinds replay source context without suppressing the error");


// A directly played authored spell shares the same source scope as native replays.
replayCore.Combat.Throw=false;
int authoredChecks=0;
IEnumerator AuthoredBody()
{
    Check(SpellReplay.Responsible(replaySpell,unrelatedSource)==replaySpell,"Direct authored damage keeps spell traits");
    Check(SpellReplay.Casts.Peek().Floor==2,"Authored scope retains selected floor");
    yield return SpellReplay.WithSource(unrelatedSource,replayCore,1,NestedBody());
    Check(SpellReplay.Responsible(replaySpell,unrelatedSource)==replaySpell,"Nested source restores authored parent");
    authoredChecks++;
}
IEnumerator NestedBody()
{
    Check(SpellReplay.Responsible(unrelatedSource,replaySpell)==unrelatedSource,"Nested spell owns its own traits");
    Check(SpellReplay.Responsible(replaySpell,replaySpell)==replaySpell,"Unrelated source is not rewritten by nested scope");
    yield break;
}
Run(SpellReplay.WithSource(replaySpell,replayCore,2,AuthoredBody()));
Check(authoredChecks==1&&SpellReplay.Casts.Count==0,"Direct and nested authored scopes unwind completely");
IEnumerator FailAuthored(){yield return SpellReplay.WithSource(unrelatedSource,replayCore,1,NestedBody());throw new InvalidOperationException("authored");}
bool authoredFailed=false;
try{Run(SpellReplay.WithSource(replaySpell,replayCore,2,FailAuthored()));}catch(InvalidOperationException){authoredFailed=true;}
Check(authoredFailed&&SpellReplay.Casts.Count==0,"Failed authored damage cannot leak its traits to later cards");

Console.WriteLine(JsonSerializer.Serialize(new{success=errors.Count==0,checks,errors}));return errors.Count==0?0:1;
// 2026-10-24-2102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Regresiones ejecutables de código real: curación, daño, estados, oro, ambos bandos, bajas y errores
// 2026-10-24-2102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Runner libera enumeradores anidados al propagar excepciones como el motor
// 2026-10-24-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Regresión de efecto mixto que hereda de curación
// 2026-10-25-1641||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Regresiones de diálogos mezclados, cambios tras vínculo, conservación de carta y objetivos seguros
// 2026-10-25-1832||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Regresiones de modos aleatorios, overrides nativos, efectos mixtos y conservación de carta inválida
// 2026-10-25-1833||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Restaurar portador vivo antes de casos UI y RNG para comprobar el filtro y no el guard de muerte
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/Program.cs||Registrar ID de marcador antes de ejecutar Bind con implementación de producción
// 2026-10-26-2153||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/Program.cs||Regresiones de Alchemy, robo seguro, rechazos mixtos y tooltips completos dinámicos con varios vínculos
// 2026-10-27-1540||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/Program.cs||Regresiones del contexto real de autoría, objetivo/emisor, piso lógico, cámara y limpieza ante excepción
// 2026-10-27-2018||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/Program.cs||Probar ámbito productivo de daño con autor, anidamiento y limpieza por excepción
// 2026-10-27-2021||codex-ww-inc||salidas/build/ww-inc/tests/Dissertation/Program.cs||Probar excepción con fuente anidada correctamente configurada
