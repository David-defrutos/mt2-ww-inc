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

// Exercise the production selector through the same deferred callback shape as CardManager.
var selectionCore=new FakeCore();var selectionSource=new CardState();
var validChoice=new CardState();var invalidChoice=new CardState{Type=CardType.Monster};
selectionCore.Cards.Hand.AddRange([selectionSource,validChoice,invalidChoice]);
int applied=0;bool choiceEligible=true;
Run(HandSelection.ChooseHand(selectionCore,selectionSource,c=>c.Type==CardType.Spell&&choiceEligible,c=>{applied++;}));
var selection=selectionCore.Cards.Selection!;
Check(selectionCore.Cards.OpenCount==1&&applied==0,"Opening hand selection finishes without applying a premature choice or waiting for input");
Check(selection.disableIneligibleCards&&selection.selectionHandState==CardUI.CardUIState.HandDiscardCard&&!selectionCore.Cards.AutoPick,
 "Native NotEligible state is configured and last-card auto-pick stays disabled");
Check(selection.filterCallback(selectionSource)&&selection.filterCallback(invalidChoice)&&!selection.filterCallback(validChoice),
 "Source and invalid cards are disabled, eligible spell is selectable");
Run(selection.cardChosenCallback(selectionSource,selectionCore.Cards));
Run(selection.cardChosenCallback(invalidChoice,selectionCore.Cards));
Check(applied==0,"Invalid or source-card callbacks cannot apply an effect");
selectionCore.Cards.Hand.Remove(validChoice);Run(selection.cardChosenCallback(validChoice,selectionCore.Cards));
Check(applied==0,"A card removed before confirmation is not modified");
selectionCore.Cards.Hand.Add(validChoice);choiceEligible=false;Run(selection.cardChosenCallback(validChoice,selectionCore.Cards));
Check(applied==0,"Eligibility is rechecked when confirmation arrives");
choiceEligible=true;var unrelatedCore=new FakeCore();unrelatedCore.Cards.Hand.Add(validChoice);
Run(selection.cardChosenCallback(validChoice,unrelatedCore.Cards));
Check(applied==0,"A callback from a different manager cannot mutate the chosen card");
selectionCore.Save.PreviewMode=true;Run(selection.cardChosenCallback(validChoice,selectionCore.Cards));
Check(applied==0,"A confirmation inside save preview cannot modify a card");
selectionCore.Save.PreviewMode=false;selectionCore.Combat.IsRunningCardPlayPreview=true;
Run(selection.cardChosenCallback(validChoice,selectionCore.Cards));
Run(HandSelection.ChooseHand(selectionCore,selectionSource,c=>true,c=>{applied++;}));
Check(applied==0&&selectionCore.Cards.OpenCount==1,"Card-play preview independently prevents UI and confirmation when save preview is false");
selectionCore.Combat.IsRunningCardPlayPreview=false;
Run(selection.cardChosenCallback(validChoice,selectionCore.Cards));
Check(applied==1,"Valid confirmation runs against the original explicit managers");
selectionCore.Save.PreviewMode=true;
Run(HandSelection.ChooseHand(selectionCore,selectionSource,c=>true,c=>{applied++;}));
Check(selectionCore.Cards.OpenCount==1,"Save preview never opens hand UI");
selectionCore.Save.PreviewMode=false;selectionCore.Cards.Hand.Clear();selectionCore.Cards.Hand.Add(selectionSource);
Run(HandSelection.ChooseHand(selectionCore,selectionSource,c=>true,c=>{applied++;}));
Check(selectionCore.Cards.OpenCount==1,"Source-only hand cannot open a selector without a valid target");
selectionCore.Cards.Hand.Add(validChoice);
IEnumerator AsyncChoice(CardState card){applied++;yield return null;applied++;}
Run(HandSelection.ChooseHand(selectionCore,selectionSource,c=>true,new Func<CardState,IEnumerator>(AsyncChoice)));
Run(selectionCore.Cards.Selection!.cardChosenCallback(validChoice,selectionCore.Cards));
Check(applied==3,"Confirmation runs the whole selected-effect coroutine");


// Frozen Tuition keeps per-unit damage credit while delegating the unchanged cost operation.
var tuitionCore=new FakeCore();var tuitionSource=new CharacterData();FrozenTuition.Source=tuitionSource;FrozenTuition.Reset();
var professorA=new CharacterState{Source=tuitionSource};var professorB=new CharacterState{Source=tuitionSource};
var unrelatedUnit=new CharacterState();
var frozenSpell=new CardState{Frozen=true};var frozenUnitCard=new CardState{Frozen=true,Type=CardType.Monster};var unfrozenSpell=new CardState();
tuitionCore.Cards.Hand.AddRange([frozenSpell,frozenUnitCard,unfrozenSpell]);
var reductions=new Dictionary<CardState,int>();
void Discount(CardState card,int amount)=>reductions[card]=reductions.GetValueOrDefault(card)+amount;
void Tuition(CharacterState? unit,int amount)=>FrozenTuition.RecordDamage(tuitionCore,unit,amount,Discount);
Tuition(professorA,29);Check(reductions.Count==0,"Tuition preserves sub-threshold damage without discounting early");
Tuition(professorB,1);Check(reductions.Count==0,"Two professors do not share their damage remainder");
Tuition(professorA,1);Check(reductions.GetValueOrDefault(frozenSpell)==1&&reductions.GetValueOrDefault(frozenUnitCard)==1&&reductions.GetValueOrDefault(unfrozenSpell)==0,
 "Exactly thirty damage discounts every currently Frozen card, including nonspells");
Tuition(professorA,89);Check(reductions[frozenSpell]==3,"A single hit crosses multiple thresholds");
Tuition(professorA,1);Check(reductions[frozenSpell]==4,"Multiple-threshold hit preserves its remainder for the next hit");
Tuition(professorB,29);Check(reductions[frozenSpell]==5,"A second copy keeps its own remainder");
Tuition(unrelatedUnit,90);Tuition(null,90);Tuition(professorA,0);Tuition(professorA,-90);
Check(reductions[frozenSpell]==5,"Other sources, missing attackers, blocked damage and negative values give no credit");
tuitionCore.Save.PreviewMode=true;Tuition(professorA,29);tuitionCore.Save.PreviewMode=false;Tuition(professorA,1);
Check(reductions[frozenSpell]==5,"Save preview neither discounts nor consumes live remainder");
tuitionCore.Combat.IsRunningCardPlayPreview=true;Tuition(professorA,29);tuitionCore.Combat.IsRunningCardPlayPreview=false;Tuition(professorA,1);
Check(reductions[frozenSpell]==5,"Card-play preview alone cannot change tuition credit");
frozenSpell.Frozen=false;unfrozenSpell.Frozen=true;Tuition(professorA,28);
Check(reductions[frozenSpell]==5&&reductions[unfrozenSpell]==1&&reductions[frozenUnitCard]==6,"Targets follow current Frozen state, not old ability bindings");
FrozenTuition.Reset();reductions.Clear();tuitionCore.Cards.Hand.Clear();Tuition(professorA,89);tuitionCore.Cards.Hand.Add(frozenUnitCard);Tuition(professorA,1);
Check(reductions[frozenUnitCard]==1,"An empty hand does not bank unspent discounts, but keeps the damage remainder");
Tuition(professorA,29);var tuitionSnapshot=new CollectionSnapshot(typeof(FrozenTuition));Tuition(professorA,1);tuitionSnapshot.Restore();
reductions.Clear();Tuition(professorA,1);Check(reductions[frozenUnitCard]==1,"Preview snapshot restores extracted per-unit remainder");
FrozenTuition.Reset();reductions.Clear();Tuition(professorA,1);Check(reductions.Count==0,"Battle reset clears credit without losing configured source");
FrozenTuition.Reset();Tuition(professorA,int.MaxValue);Check(reductions[frozenUnitCard]==int.MaxValue/30,"Large damage applies every earned discount without integer overflow");
FrozenTuition.Reset();FrozenTuition.Source=null!;reductions.Clear();Tuition(professorA,90);Check(reductions.Count==0,"Unconfigured source cannot match arbitrary units");



// Registered native character identity replaces string matching; spell timing stays with the caller.
var passiveCore=new FakeCore();var passiveRoom=new RoomState();passiveCore.Rooms.Rooms[0]=passiveRoom;
UnitSpellPassives.Representative=new();UnitSpellPassives.FirstAid=new();UnitSpellPassives.Education=new();
var repA=new CharacterState{Source=UnitSpellPassives.Representative,TeamType=Team.Type.Monsters,Attack=6,Buff=20};
var repB=new CharacterState{Source=UnitSpellPassives.Representative,TeamType=Team.Type.Monsters,Attack=9};
var ordinary=new CharacterState{TeamType=Team.Type.Monsters,Attack=15};
var passiveCleanup=new List<Action>();UnitSpellPassives.ApplyRepresentative([repA,ordinary,repB],35,passiveCleanup);
Check(repA.Attack==41&&repA.GetAttackDamage()==61&&repB.Attack==44&&ordinary.Attack==15,"Representative applies to each carrier without changing native status buffs or other allies");
repA.Attack+=7;repB.IsDestroyed=true;foreach(var action in passiveCleanup.AsEnumerable().Reverse())action();
Check(repA.Attack==13&&repA.GetAttackDamage()==33&&repB.Attack==44,"Representative cleanup preserves subsequent bonuses and tolerates destroyed carriers");
var teacherA=new CharacterState{Source=UnitSpellPassives.Education,TeamType=Team.Type.Monsters};
var teacherB=new CharacterState{Source=UnitSpellPassives.Education,TeamType=Team.Type.Monsters};
passiveRoom.Units.AddRange([ordinary,teacherA,teacherB]);
Check(UnitSpellPassives.EducationPower(passiveCore,0)==16,"Two educators each contribute the rounded-up half of front attack");
teacherB.RealHP=0;Check(UnitSpellPassives.EducationPower(passiveCore,0)==8,"Dead educator does not contribute power");
Check(UnitSpellPassives.EducationPower(passiveCore,99)==0,"Missing floor grants no education bonus");
var patientA=new CharacterState{TeamType=Team.Type.Monsters};var patientB=new CharacterState{TeamType=Team.Type.Monsters};
var patientC=new CharacterState{TeamType=Team.Type.Monsters};var patientD=new CharacterState{TeamType=Team.Type.Monsters};
passiveRoom.Units.Clear();passiveRoom.Units.AddRange([patientA,patientB,patientC,patientD]);
Run(UnitSpellPassives.HealShared(passiveCore,0,100,damage));
Check(passiveRoom.Units.All(u=>u.Healing.Single()==(25,damage)),"PhD divides total damage evenly and preserves the responsible spell");
Check(patientA.RealHP==40&&patientA.Healing[0].amount==25,"Full-health ally still receives the whole requested heal, allowing native Overheal");
var patientE=new CharacterState{TeamType=Team.Type.Monsters};passiveRoom.Units.Add(patientE);
Run(UnitSpellPassives.HealShared(passiveCore,0,103,damage));
Check(passiveRoom.Units.All(u=>u.Healing.Last().amount==20),"Five allies split healing with the existing integer remainder rule");
patientA.OnHeal=()=>patientB.RealHP=0;int beforePatientB=patientB.Healing.Count;
Run(UnitSpellPassives.HealShared(passiveCore,0,100,damage));
Check(patientB.Healing.Count==beforePatientB,"An ally dying during earlier healing is not healed afterwards");
int beforeHealing=patientA.Healing.Count;Run(UnitSpellPassives.HealShared(passiveCore,0,0,damage));Run(UnitSpellPassives.HealShared(passiveCore,99,100,damage));
Check(patientA.Healing.Count==beforeHealing,"Zero healing or a missing floor produces no native heal calls");
var scholarshipCore=new FakeCore();var scholarship=new RelicEffectAcademicFrozenDraw();
scholarshipCore.Save.PreviewMode=true;scholarship.OnFrozenCardPlayed(scholarshipCore);
Check(scholarship.Triggers==0&&scholarshipCore.Cards.Drawn==0,"Scholarship does not spend a native condition or draw during save preview");
scholarshipCore.Save.PreviewMode=false;scholarshipCore.Combat.IsRunningCardPlayPreview=true;scholarship.OnFrozenCardPlayed(scholarshipCore);
Check(scholarship.Triggers==0&&scholarshipCore.Cards.Drawn==0,"Scholarship also rejects card-play preview independently");
scholarshipCore.Combat.IsRunningCardPlayPreview=false;scholarship.OnFrozenCardPlayed(scholarshipCore);scholarship.OnFrozenCardPlayed(scholarshipCore);
Check(scholarship.Triggers==1&&scholarshipCore.Cards.Drawn==1,"Scholarship delegates quota to native conditions and draws only after successful condition check");
scholarship.Triggers=0;scholarship.OnFrozenCardPlayed(scholarshipCore);
Check(scholarship.Triggers==1&&scholarshipCore.Cards.Drawn==2,"Scholarship uses the native condition again after its duration reset");

var singleCore=new FakeCore();var soleSpell=new CardState();
singleCore.Cards.Hand.AddRange([soleSpell,new CardState{Type=CardType.Monster}]);int singleApplied=0;
IEnumerator PickSingle(CardState picked){Check(picked==soleSpell,"Auto-selection picks the eligible spell, not the last card");singleApplied++;yield break;}
Run(HandSelection.ChooseHand(singleCore,null,c=>c.Type==CardType.Spell,new Func<CardState,IEnumerator>(PickSingle),true));
Check(singleApplied==1&&singleCore.Cards.OpenCount==0,"One eligible spell auto-consumes without opening UI");
singleCore.Cards.Hand.Add(new CardState());
Run(HandSelection.ChooseHand(singleCore,null,c=>c.Type==CardType.Spell,new Func<CardState,IEnumerator>(PickSingle),true));
Check(singleApplied==1&&singleCore.Cards.OpenCount==1,"Two eligible spells still require the native selector");
singleCore.Save.PreviewMode=true;
Run(HandSelection.ChooseHand(singleCore,null,c=>true,new Func<CardState,IEnumerator>(PickSingle),true));
Check(singleApplied==1&&singleCore.Cards.OpenCount==1,"Auto-selection never changes cards during preview");

var protectedUnit=new CharacterState{Floor=0};var otherFloorUnit=new CharacterState{Floor=1};
AcademicRuntime.ZeroAttack.Add(protectedUnit);AcademicRuntime.ZeroAttack.Add(otherFloorUnit);
IEnumerator CombatRound(){Check(AcademicRuntime.ZeroAttack.Contains(protectedUnit),"Attack stays zero throughout the protected combat round");yield break;}
var roundRoom=new RoomState{Floor=0};
var roundSnapshot=new CollectionSnapshot(typeof(AcademicRuntime));
Run(ArmorHonorsRound.After(CombatRound(),roundRoom));
Check(!AcademicRuntime.ZeroAttack.Contains(protectedUnit)&&AcademicRuntime.ZeroAttack.Contains(otherFloorUnit),"End of round restores Attack only on the relevant floor");
roundSnapshot.Restore();
Check(AcademicRuntime.ZeroAttack.Contains(protectedUnit),"Prediction snapshot restores real Attack suppression after simulated round cleanup");
Run(ArmorHonorsRound.After(CombatRound(),roundRoom));
Check(!AcademicRuntime.ZeroAttack.Contains(protectedUnit),"Real round then expires suppression before the next Relentless round");
AcademicRuntime.ZeroAttack.Clear();

var ringManagers=new AllGameManagers();AllGameManagers.Instance=ringManagers;
var wandTemplate=new CardUpgradeData();wandTemplate.Statuses.Add(new(){statusId="conduit",count=10});TrainingWand.Template=wandTemplate;
CardUpgradeState Upgrade(CardUpgradeData source,int amount){var u=new CardUpgradeState{Source=source};u.Statuses.Add(new(){statusId="conduit",count=amount});return u;}
foreach(var ring in new[]{0,1,2,5,10,11,20,int.MaxValue,int.MinValue})
{
 ringManagers.Save.DisplayedRing=ring;var equipmentUpgrade=Upgrade(wandTemplate,10);
 TrainingWand.Prepare(equipmentUpgrade);
 Check(equipmentUpgrade.Statuses.Single().count==TrainingWand.Amount(ring),"Wand resolves displayed ring "+ring+" without negative stacks");
 Check(wandTemplate.Statuses.Single().count==10,"Resolving a wand never mutates the shared upgrade template");
}
ringManagers.Save.DisplayedRing=4;var appliedWand=Upgrade(wandTemplate,10);TrainingWand.Prepare(appliedWand);
var removedWand=Upgrade(wandTemplate,10);TrainingWand.Prepare(removedWand);
Check(appliedWand.Statuses.Single().count==7&&removedWand.Statuses.Single().count==7,"Ring four applies and removes the same seven Conduit");
var unrelatedUpgrade=Upgrade(new CardUpgradeData(),5);TrainingWand.Prepare(unrelatedUpgrade);
Check(unrelatedUpgrade.Statuses.Single().count==5,"Other upgrades and equipment stones keep their own Conduit");
ringManagers.Save.PreviewMode=true;var previewWand=Upgrade(wandTemplate,10);TrainingWand.Prepare(previewWand);
Check(previewWand.Statuses.Single().count==7&&appliedWand.Statuses.Single().count==7&&wandTemplate.Statuses.Single().count==10,"Prediction changes only the incoming temporary upgrade, without live/template writes");
wandTemplate.Statuses[0].count=15;Check(TrainingWand.CurrentAmount()==12,"Power is editable in JSON and uses the configured template amount");wandTemplate.Statuses[0].count=10;
Check(TrainingWand.Amount(1)==10&&TrainingWand.Amount(2)==9&&TrainingWand.Amount(11)==0,"First ring, completed-ring decrement and minimum zero match approved design");
var wandText=new CardEffectAcademicTrainingWand().GetCardText(new CardEffectState(Team.Type.None,TargetMode.Self));
Check(wandText=="Conduit 7","Card body shows the same current value as the native equipment upgrade");
Check(new CardEffectAcademicTrainingWand().GetCardText(new CardEffectState(Team.Type.None,TargetMode.Self){Parent=null})=="Conduit 7","Tooltip without a parent card uses the native sentinel without throwing");
AllGameManagers.Instance=null;

AllGameManagers.Instance=new AllGameManagers();AllGameManagers.Instance.Save.DisplayedRing=4;
var wandCard=new CardState{Type=CardType.Equipment};
wandCard.Effects.Add(new CardEffectState(Team.Type.Monsters,TargetMode.DropTargetCharacter){Effect=new CardEffectAcademicTrainingWand()});
var nativeConduit=new StatusEffectStackData{statusId="conduit",count=10};
Check(TrainingWandText.Adjust(wandCard,nativeConduit,true).count==7,"Native wand status line uses ring value instead of stale ten");
Check(nativeConduit.count==10&&TrainingWand.Template.Statuses.Single().count==10,"Rendering does not mutate passed status or shared template");
var boostedConduit=new StatusEffectStackData{statusId="conduit",count=15};
Check(TrainingWandText.Adjust(wandCard,boostedConduit,true).count==12,"Rendering retains five conduit from independent upgrades");
wandCard.Duality=true;
Check(TrainingWandText.Adjust(wandCard,new StatusEffectStackData{statusId="conduit",count=20},true).count==14,"Already doubled status uses doubled ring adjustment");
Check(TrainingWandText.Adjust(wandCard,nativeConduit,false).count==7,"Not-yet doubled status leaves doubling to native formatter");
var foreignCard=new CardState{Type=CardType.Equipment};
Check(ReferenceEquals(TrainingWandText.Adjust(foreignCard,nativeConduit,true),nativeConduit),"Other equipment's conduit is untouched");
var shieldLine=new StatusEffectStackData{statusId="damage shield",count=1};
Check(ReferenceEquals(TrainingWandText.Adjust(wandCard,shieldLine,true),shieldLine),"Other status lines are untouched");
AllGameManagers.Instance.Save.DisplayedRing=20;wandCard.Duality=false;
Check(TrainingWandText.Adjust(wandCard,nativeConduit,true).count==0,"Native line matches minimum zero after enough rings");
AllGameManagers.Instance=null;

AllGameManagers.Instance=null;
Check(!BattlePresentationContext.TryGet(out _),"No game managers in menu does not access Core");
var menuContext=new AllGameManagers();AllGameManagers.Instance=menuContext;
for(int i=0;i<1000;i++)BattlePresentationContext.TryGet(out _);
Check(menuContext.CoreReads==0&&menuContext.ManagerReads==0,"Repeated deck redraws before main scene initialization cannot trigger asserting manager getters");
menuContext.Initialized=true;menuContext.HasCombat=false;
Check(!BattlePresentationContext.TryGet(out _)&&menuContext.CoreReads==0,"Partially unloaded combat manager is rejected before Core access");
menuContext.HasCombat=true;menuContext.HasCards=false;
Check(!BattlePresentationContext.TryGet(out _)&&menuContext.CoreReads==0,"Missing CardManager is rejected before Core access");
menuContext.HasCards=true;menuContext.HasRooms=false;
Check(!BattlePresentationContext.TryGet(out _)&&menuContext.CoreReads==0,"Missing RoomManager is rejected before Core access");
menuContext.HasRooms=true;
Check(BattlePresentationContext.TryGet(out var livePresentation)&&ReferenceEquals(livePresentation,menuContext.Core),"Available scene resolves current native context");
var stalePresentation=menuContext.Core;menuContext.Core=new FakeCore();
Check(BattlePresentationContext.TryGet(out var nextPresentation)&&!ReferenceEquals(nextPresentation,stalePresentation)&&ReferenceEquals(nextPresentation,menuContext.Core),"New battle never reuses a retained previous context");
int previousCoreReads=menuContext.CoreReads;menuContext.Initialized=false;
Check(!BattlePresentationContext.TryGet(out _)&&menuContext.CoreReads==previousCoreReads,"Main scene unload rejects context even when old manager references remain");
AllGameManagers.Instance=null;

var missingTooltipId=new TooltipContent{title="Conduit",body="Spell bonus"};
Check(TooltipIdentity.Prepare(ref missingTooltipId)&&!string.IsNullOrWhiteSpace(missingTooltipId.tooltipId),"Null tooltip ID receives a stable title fallback before native Replace");
Check(missingTooltipId.tooltipId.Replace("_CardText","").Length>0,"Previously failing native dereference succeeds");
Check(missingTooltipId.title=="Conduit"&&missingTooltipId.body=="Spell bonus","Repair preserves native title and body");
var existingTooltipId=new TooltipContent{title="Conduit",body="Spell bonus",tooltipId="conduit"};
Check(TooltipIdentity.Prepare(ref existingTooltipId)&&existingTooltipId.tooltipId=="conduit","Existing native identity is unchanged");
var bodyTooltip=new TooltipContent{body="A bound spell description"};
Check(TooltipIdentity.Prepare(ref bodyTooltip)&&bodyTooltip.tooltipId.Contains(bodyTooltip.body),"Body-only tooltip is retained with identity");
var emptyTooltip=default(TooltipContent);
Check(!TooltipIdentity.Prepare(ref emptyTooltip),"Empty native result is not instantiated");
var whitespaceTooltip=new TooltipContent{title=" ",body=" ",tooltipId=" "};
Check(!TooltipIdentity.Prepare(ref whitespaceTooltip),"Whitespace result is not instantiated");
var repeatedTooltip=new TooltipContent{title="Conduit",body="Spell bonus"};
TooltipIdentity.Prepare(ref repeatedTooltip);
Check(repeatedTooltip.tooltipId==missingTooltipId.tooltipId,"Repeated hover produces the same identity for native deduplication");

var previewTargets=new List<CharacterState>();
var previewRooms=new RoomManager();
PreviewTargets.Expand(previewRooms,previewTargets);
Check(previewTargets.Count==0,"Preview expansion in menu does not access missing managers");
var previewContext=new AllGameManagers();AllGameManagers.Instance=previewContext;
PreviewTargets.Expand(previewContext.Core.Rooms,previewTargets);
Check(previewContext.CoreReads==0,"Uninitialized scene cannot access asserting Core during preview expansion");
previewContext.Initialized=true;
var previewRoom=new RoomState{Floor=0};previewContext.Core.Rooms.Rooms[0]=previewRoom;
var visiblePreviewUnit=new CharacterState();var deadPreviewUnit=new CharacterState{RealHP=0};
var destroyedPreviewUnit=new CharacterState{IsDestroyed=true};var hiddenPreviewUnit=new CharacterState{HasUI=false};
previewRoom.Units.AddRange([visiblePreviewUnit,deadPreviewUnit,destroyedPreviewUnit,hiddenPreviewUnit]);
PreviewTargets.Expand(previewRooms,previewTargets);
Check(previewTargets.Count==0,"Stale RoomManager is never expanded using a new battle");
PreviewTargets.Expand(previewContext.Core.Rooms,previewTargets);
Check(previewTargets.SequenceEqual(new[]{visiblePreviewUnit}),"Only living visible characters enter additional preview targets");
PreviewTargets.Expand(previewContext.Core.Rooms,previewTargets);
Check(previewTargets.Count==1,"Preview expansion does not duplicate native or previously added targets");
AllGameManagers.Instance=null;

var selectionContext=new AllGameManagers{Initialized=true};AllGameManagers.Instance=selectionContext;
var dimmedCard=new CardUI{State=CardUI.CardUIState.Ineligible};
var focusedDimmedCard=new CardUI{State=CardUI.CardUIState.Ineligible};
var untouchedCard=new CardUI{State=CardUI.CardUIState.Screen};
selectionContext.Core.Hand.Cards.AddRange([dimmedCard,untouchedCard]);selectionContext.Core.Hand.Focused=focusedDimmedCard;
HandSelectionPresentation.Restore(new FakeCore());
Check(dimmedCard.Disabled,"Old selection cannot clear another battle's presentation");
selectionContext.Core.Hand.Selecting=true;HandSelectionPresentation.Restore(selectionContext.Core);
Check(dimmedCard.Disabled,"Cleanup cannot override a newly opened native selector");
selectionContext.Core.Hand.Selecting=false;HandSelectionPresentation.Restore(selectionContext.Core);
Check(selectionContext.Core.Cards.Refreshes==1,"Closed selection delegates hand restoration to native RefreshHandCards");
Check(dimmedCard.State==CardUI.CardUIState.Ineligible&&untouchedCard.State==CardUI.CardUIState.Screen,"Our callback leaves individual CardUI mutations to the native refresh");
AllGameManagers.Instance=null;

var creditCore=new FakeCore();creditCore.Cards.Combat=creditCore.Combat;
var consumed=new CardState{Cost=3};creditCore.Cards.Hand.Add(consumed);
var creditUnit=new CharacterState{TeamType=Team.Type.Monsters};var creditEffect=new CardEffectState();var creditSource=new CardState();creditEffect.Parent=creditSource;
Run(CardEffectAcademicConsumeCredits.ResolveCredits(consumed,creditUnit,creditCore,creditEffect,new FakeSystems()));
Check(creditUnit.GetStatusEffectStacks("conduit")==15&&creditCore.Combat.QueueRuns==2,"Credit Conversion consumes then resolves native state-grant queue after adding 15 Conduit");
Check(CardEffectAddStatusEffect.LastChance==0&&CardEffectAddStatusEffect.LastSource==creditSource,"Native status grant is guaranteed, not five-percent chance, and retains ability source");
Check(creditEffect.Data.Statuses[0].count==1&&creditEffect.Param==5,"Credit Conversion leaves shared status template and cost multiplier unchanged");
var oversizedCredit=new CardState{Cost=int.MaxValue};
Run(CardEffectAcademicConsumeCredits.ResolveCredits(oversizedCredit,new CharacterState(),creditCore,creditEffect,new FakeSystems()));
Check(creditEffect.Data.Statuses[0].count==1,"Saturated credit conversion never overwrites template stacks");
var preparedCore=new FakeCore();var unprepared=new CharacterState();
preparedCore.Save.PreviewMode=true;
Check(!PreviewDependencies.CanWrite(unprepared,preparedCore),"Simulating cannot modify an unprepared live character");
unprepared.PreviewMode=true;Check(PreviewDependencies.CanWrite(unprepared,preparedCore),"Native battle-preview state permits simulated writes");
preparedCore.Save.PreviewMode=false;preparedCore.Combat.IsRunningCardPlayPreview=true;
Check(!PreviewDependencies.CanWrite(unprepared,preparedCore),"Preview teardown cannot refresh real auras");
preparedCore.Combat.IsRunningCardPlayPreview=false;unprepared.PreviewMode=false;
Check(PreviewDependencies.CanWrite(unprepared,preparedCore),"Real battle writes resume after preview finishes");
AllGameManagers.Instance=new AllGameManagers{Initialized=true,Core=preparedCore};preparedCore.Rooms.Rooms[0]=new RoomState();
Check(!PreviewDependencies.RequiresExpansion(preparedCore.Rooms),"Other-clan battles do not expand every floor just because W&W is installed");
preparedCore.Rooms.Rooms[0].Units.Add(new CharacterState{Source=new CharacterData{name="david.wwinc-Character-WW_U17_Character"},TeamType=Team.Type.Monsters});
Check(PreviewDependencies.RequiresExpansion(preparedCore.Rooms),"A W&W unit in another clan still enables its required cross-floor preview");
AllGameManagers.Instance=null;

var memos=new List<SpellRules.Rule>{new(){Kind="floors"},new(){Kind="floors"},new(){Kind="power"}};
Check(!PendingSpellRules.ConsumeFloorUse(memos,0,false,false)&&memos.Count==3,"Ineligible spells do not consume Memo charges");
Check(!PendingSpellRules.ConsumeFloorUse(memos,0,true,true)&&memos.Count==3,"Indirect repetitions cannot consume another Memo");
Check(PendingSpellRules.ConsumeFloorUse(memos,0,false,true)&&memos.Count==2,"First eligible cast spends only one of two Memos");
Check(PendingSpellRules.ConsumeFloorUse(memos,1,false,true)&&memos.Single().Kind=="power","Second eligible cast spends second Memo and preserves unrelated rules");
Check(!PendingSpellRules.ConsumeFloorUse(memos,0,false,true),"No Memo charge is fabricated when empty");
SpellRules.Pending.Add(new(){Kind="floors"});
var memoSnapshot=new CollectionSnapshot(typeof(SpellRules));
PendingSpellRules.ConsumeFloorUse(SpellRules.Pending,0,false,true);memoSnapshot.Restore();
Check(SpellRules.Pending.Count==1,"Preview snapshot restores the same real pending Memo use");SpellRules.Pending.Clear();


// Atomic swap exposes only final positions to native movement events.
FakeCore SwapCore()
{
    var c=new FakeCore();c.Rooms.Rooms[0]=new RoomState{Floor=0};
    c.Rooms.Rooms[0].Units.AddRange(new[]{new CharacterState{Floor=0,TeamType=Team.Type.Monsters},
        new CharacterState{Floor=0,TeamType=Team.Type.Monsters},new CharacterState{Floor=0,TeamType=Team.Type.Monsters}});
    return c;
}
var desk=new CardEffectAcademicDeskSwap();var deskEffect=new CardEffectState();var deskParams=new CardEffectParams{selectedRoom=0,playedCard=new CardState()};
var swap=SwapCore();var oldOrder=swap.Rooms.Rooms[0].Units.ToArray();var savedTargets=deskParams.targets;
CardEffectAttackWithUnit.Attacked.Clear();
swap.Relics.OnMove=_=>Check(swap.Rooms.Rooms[0].Units.SequenceEqual(new[]{oldOrder[2],oldOrder[1],oldOrder[0]}),"Movement callbacks see a completed atomic swap");
Check(desk.TestEffect(deskEffect,deskParams,swap),"Two movable endpoints are eligible");
Run(desk.ApplyEffect(deskEffect,deskParams,swap,new FakeSystems()));
Check(swap.Relics.Moved.SequenceEqual(new[]{oldOrder[0],oldOrder[2]})&&swap.Heroes.Shifted.Count==2&&swap.Combat.Sent.Count==2,"Native relic, Shift and Sentry callbacks run once for each moved endpoint");
Check(CardEffectAttackWithUnit.Attacked.SequenceEqual(new[]{oldOrder[2]})&&ReferenceEquals(savedTargets,deskParams.targets),"Native attack uses the original rear and restores effect target collection");
Check(swap.Rooms.OrderUpdates==1&&swap.Save.Moved==2&&swap.Save.Unlocks==1,"Swap notifies native room order and unlock tracking");
var stuck=SwapCore();stuck.Rooms.Rooms[0].Units[2].Real["immobile"]=1;var stuckOrder=stuck.Rooms.Rooms[0].Units.ToArray();
Check(!desk.TestEffect(deskEffect,deskParams,stuck),"An immobile endpoint blocks the complete swap");
Run(desk.ApplyEffect(deskEffect,deskParams,stuck,new FakeSystems()));
Check(stuck.Rooms.Rooms[0].Units.SequenceEqual(stuckOrder)&&stuck.Relics.Moved.Count==0,"Failed revalidation cannot leave a partial swap");
var outer=SwapCore();outer.Rooms.Rooms[0].Units[0].Outer=true;Check(!desk.TestEffect(deskEffect,deskParams,outer),"Outer boss endpoint respects native movement restriction");
var lethalSwap=SwapCore();var doomed=lethalSwap.Rooms.Rooms[0].Units[2];
CardEffectAttackWithUnit.Attacked.Clear();lethalSwap.Relics.OnMove=_=>doomed.RealHP=0;
Run(desk.ApplyEffect(deskEffect,deskParams,lethalSwap,new FakeSystems()));
Check(CardEffectAttackWithUnit.Attacked.Count==0&&lethalSwap.Combat.Sent.Count==1,"Movement death cannot substitute another attacker or trigger Sentry on a dead unit");
var movedSwap=SwapCore();var movedRear=movedSwap.Rooms.Rooms[0].Units[2];movedSwap.Relics.OnMove=_=>movedRear.Floor=1;
Run(desk.ApplyEffect(deskEffect,deskParams,movedSwap,new FakeSystems()));
Check(CardEffectAttackWithUnit.Attacked.Count==0,"Movement reaction sending the rear elsewhere cancels its immediate attack");
var predictionSwap=SwapCore();predictionSwap.Save.PreviewMode=true;
Run(desk.ApplyEffect(deskEffect,deskParams,predictionSwap,new FakeSystems()));
Check(predictionSwap.Save.Moved==0&&predictionSwap.Save.Unlocks==0,"Predicted movement cannot progress metagame unlocks");


var failedSwap=SwapCore();CardEffectAttackWithUnit.ThrowAttack=true;bool attackFailed=false;
try{Run(desk.ApplyEffect(deskEffect,deskParams,failedSwap,new FakeSystems()));}catch(InvalidOperationException){attackFailed=true;}
CardEffectAttackWithUnit.ThrowAttack=false;
Check(attackFailed&&ReferenceEquals(savedTargets,deskParams.targets),"Native attack failure restores original effect parameters and propagates error");
var pairSwap=SwapCore();pairSwap.Rooms.Rooms[0].Units.RemoveAt(1);var pairOriginal=pairSwap.Rooms.Rooms[0].Units.ToArray();
Run(desk.ApplyEffect(deskEffect,deskParams,pairSwap,new FakeSystems()));Check(pairSwap.Rooms.Rooms[0].Units.SequenceEqual(pairOriginal.Reverse()),"Two-unit swap has no duplicate movement or missing second endpoint");

// Each concrete relic owns its rule. Legacy binary name delegates only for legacy instances.
var relicCore=new FakeCore();AcademicRuntime.Core=relicCore;
var relicTarget=new CharacterState{TeamType=Team.Type.Heroes};var frontPower=new CharacterState{Attack=15};
SpellRules.OnceTurn.Clear();
var strength=new RelicEffectStrengthChair();var strengthFrame=new AcademicRuntime.SpellResolution{Card=new CardState(),Floor=0,Indirect=false};
strength.BeforePower(strengthFrame,new(){frontPower});strength.BeforePower(strengthFrame,new(){frontPower});
Check(strengthFrame.BonusPower==7,"Strength Chair preserves first damage spell and existing integer rounding");
SpellRules.OnceTurn.Clear();var oldStrength=new RelicEffectAcademic{SourceRelicData=new(){name="old-WW_A01"}};
var legacyFrame=new AcademicRuntime.SpellResolution{Card=new CardState(),Floor=0};oldStrength.BeforePower(legacyFrame,new(){frontPower});
Check(legacyFrame.BonusPower==7,"Legacy relic type keeps the same first-spell behavior");
SpellRules.OnceTurn.Clear();SpellRules.Rules.Clear();var consumedFrame=new AcademicRuntime.SpellResolution{Card=new CardState{Consumed=true}};
var consumeCopy=new RelicEffectConsumeCopy();consumeCopy.BeforeRules(consumedFrame,relicTarget);consumeCopy.BeforeRules(consumedFrame,relicTarget);
Check(SpellRules.Rules.Count==1&&SpellRules.Rules[0].rule=="spellchain","Consumed copy relic marks only its first qualifying spell");
var quiet=new RelicEffectQuietStudy();quiet.EndTurn(true);Check(relicCore.Cards.DrawModifier==0,"Quiet Study cannot draw after a damage spell");quiet.EndTurn(false);Check(relicCore.Cards.DrawModifier==1,"Quiet Study grants its original next-hand draw modifier");
var refundRelic=new RelicEffectLethalRefund();SpellRules.TuitionRefundCount=0;var costThree=new CardState{Cost=3};
for(int i=0;i<4;i++)refundRelic.RecordKillingSpell(costThree,20,relicCore);
Check(SpellRules.TuitionRefundCount==4&&relicCore.Player.Energy==0,"Lethal Refund counts killing spells, not number of victims");
var refundCounter=new System.Text.StringBuilder();refundRelic.AppendConditionCountDisplay(refundCounter,relicCore.Statistics);Check(refundCounter.ToString()=="4/5","Refund display reads the same counter as its behavior");
refundRelic.RecordKillingSpell(costThree,1,relicCore);Check(SpellRules.TuitionRefundCount==0&&relicCore.Player.Energy==3,"Fifth killing spell refunds paid cost and resets");
relicCore.Save.PreviewMode=true;refundRelic.RecordKillingSpell(costThree,1,relicCore);Check(SpellRules.TuitionRefundCount==0,"Refund preview cannot advance the counter");relicCore.Save.PreviewMode=false;
var oldRefund=new RelicEffectAcademic{SourceRelicData=new(){name="old-WW_A05"}};oldRefund.RecordKillingSpell(costThree,1,relicCore);Check(SpellRules.TuitionRefundCount==1,"Legacy refund uses the same persisted battle counter");
SpellRules.OnceTurn.Clear();var extended=new RelicEffectExtendedExam();var indirectFrame=new AcademicRuntime.SpellResolution{Card=new CardState(),Indirect=true};extended.BeforeRules(indirectFrame,relicTarget);
Check(indirectFrame.Rules.Count==0&&!SpellRules.OnceTurn.Contains("A06"),"Extended Exam cannot spend its charge on an indirect replay");
var extendedFrame=new AcademicRuntime.SpellResolution{Card=new CardState()};extended.BeforeRules(extendedFrame,relicTarget);
Check(extendedFrame.Rules.Contains("behind"),"Extended Exam marks the first eligible direct spell");var extendedCount=new System.Text.StringBuilder();extended.AppendConditionCountDisplay(extendedCount,relicCore.Statistics);Check(extendedCount.ToString()=="1/1","Extended Exam counter reads its actual charge");
SpellRules.OnceTurn.Clear();var medicine=new RelicEffectOffensiveMedicine();var healFrame=new AcademicRuntime.SpellResolution{Card=new CardState{Healing=true,Damage=false}};medicine.BeforeRules(healFrame,null);Check(healFrame.Rules.Contains("offensive_medicine"),"Offensive Medicine keeps the first healing spell conversion rule");
var secondHeal=new AcademicRuntime.SpellResolution{Card=new CardState{Healing=true}};medicine.BeforeRules(secondHeal,null);Check(secondHeal.Rules.Count==0,"Second healing spell receives no conversion rule");
SpellRules.OnceTurn.Clear();relicCore.Rooms.Rooms[0]=new RoomState();var cooldownAlly=new CharacterState{Floor=0,TeamType=Team.Type.Monsters};cooldownAlly.Real["cooldown"]=3;relicCore.Rooms.Rooms[0].Units.Add(cooldownAlly);
var silence=new RelicEffectAdministrativeSilence();silence.UnitDied(false,0);silence.UnitDied(true,0);silence.UnitDied(true,0);Check(cooldownAlly.GetStatusEffectStacks("cooldown")==2,"Only first chaperone death reduces current cooldown");
relicCore.Relics.Effects.AddRange(new RelicEffectAcademic[]{new RelicEffectComplaintsOffice(),new RelicEffectRelationsCommittee()});SocialRules.Events.Clear();Run(AcademicRelics.BeginTurn());Check(SocialRules.Events.SequenceEqual(new[]{"separators","complaints"}),"New separators exist before complaint transfer regardless of relic acquisition order");
var legacyTypes=new[]{"WW_A01","WW_A02","WW_A04","WW_A05","WW_A06","WW_A07","WW_A08","WW_A09","WW_A10"};
foreach(var id in legacyTypes)
{
 var legacy=new RelicEffectAcademic{SourceRelicData=new(){name="legacy-"+id}};
 legacy.BeforePower(new(){Card=new CardState{Damage=false}},new());legacy.BeforeRules(new(){Card=new CardState{Damage=false}},null);
 Check(legacy.TurnOrder==(id=="WW_A07"?0:id=="WW_A09"?1:100),id+": legacy route has a finite fallback and preserves event priority");
}

Console.WriteLine(JsonSerializer.Serialize(new{success=errors.Count==0,checks,errors}));return errors.Count==0?0:1;
// 2026-10-25-1606||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/Program.cs||Regresiones reales de hover, daño temporal, devolución real, Immortal Trade, armadura, exceso y escudos
// 2026-10-25-1850||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/Program.cs||Probar signo único, preservación de aviso nativo y aislamiento de mejoras ajenas y valores no negativos
// 2026-10-25-2009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/Program.cs||Regresiones de selector: confirmación diferida, carta retirada, filtro cambiado, managers ajenos y ambos previews
// 2026-10-25-2036||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/Program.cs||Regresiones de Tuition: varias copias, umbrales, Frozen dinámico, mano vacía, preview, instantáneas y reinicio
// 2026-10-25-2142||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/Program.cs||Regresiones de fuentes y limpieza Representative, Education, curación PhD y condiciones de Scholarship
// 2026-10-26-2033||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/Program.cs||Regresiones: autoelección valida, dos objetivos y bloqueo en preview
// 2026-10-26-2037||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/CombatSafety/Program.cs||Regresiones de duración por ronda, otros pisos e aislamiento de preview
// 2026-10-26-2230||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Regresiones de rings, mínimo cero, texto, retirada, transferencia y aislamiento de mejoras ajenas
// 2026-10-26-2237||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Regresión de localización sin carta padre para eliminar aviso sin ocultar nulos
// 2026-10-26-2313||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Ocho regresiones de null ID, fallback, contenido, estabilidad y descarte de vacío
// 2026-10-26-2335||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Ocho regresiones de Conduit visible, extras, Duality, aislamiento y mínimo cero
// 2026-10-27-0008||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Ocho regresiones de menú, mil refrescos, carga parcial, getters ausentes, contexto nuevo y salida de combate
// 2026-10-27-1428||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Regresiones de menú, escena sin inicializar, gestores antiguos, unidades muertas/sin UI y duplicados
// 2026-10-27-1431||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Regresiones de Not Eligible, copia enfocada, nuevo selector y aislamiento entre combates
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Probar delegación nativa en lugar de simular tintes de Unity
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||No confundir ausencia de implementación visual nativa en doble con fallo de producción
// 2026-10-27-1538||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Regresiones de cola Credit Conversion, plantilla intacta, chance nativa, límites y preview con otros clanes
// 2026-10-27-1540||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Regresiones FIFO de Memo, hechizo no elegible, repetición, otras reglas y snapshot
// 2026-10-27-2021||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Probar movimiento atómico, callbacks nativos, muerte, preview y nueve reliquias concretas con compatibilidad antigua
// 2026-10-27-2024||codex-ww-inc||salidas/build/ww-inc/tests/CombatSafety/Program.cs||Probar intercambio mínimo y restauración de parámetros si el ataque nativo falla
