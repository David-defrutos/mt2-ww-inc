using System.Collections;
using System.Text.Json;
using WwInc;
var errors=new List<string>();int checks=0;
void Check(bool ok,string message){checks++;if(!ok)errors.Add(message);}
void Run(IEnumerator e){try{while(e.MoveNext())if(e.Current is IEnumerator next)Run(next);}finally{(e as IDisposable)?.Dispose();}}
var core=new FakeCore();AcademicRuntime.Core=core;
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
Console.WriteLine(JsonSerializer.Serialize(new{success=errors.Count==0,checks,errors}));return errors.Count==0?0:1;
// 2026-10-24-2102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Regresiones ejecutables de código real: curación, daño, estados, oro, ambos bandos, bajas y errores
// 2026-10-24-2102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Runner libera enumeradores anidados al propagar excepciones como el motor
// 2026-10-24-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Regresión de efecto mixto que hereda de curación
// 2026-10-25-1641||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Regresiones de diálogos mezclados, cambios tras vínculo, conservación de carta y objetivos seguros
// 2026-10-25-1832||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Regresiones de modos aleatorios, overrides nativos, efectos mixtos y conservación de carta inválida
// 2026-10-25-1833||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Dissertation/Program.cs||Restaurar portador vivo antes de casos UI y RNG para comprobar el filtro y no el guard de muerte
