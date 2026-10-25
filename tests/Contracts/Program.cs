using System.Text.Json;
using Mono.Cecil;
var root=Path.GetFullPath(args[0]);
using var resolver=new DefaultAssemblyResolver();
resolver.AddSearchDirectory("C:/Users/david/AppData/Roaming/Thunderstore Mod Manager/DataFolder/MonsterTrain2/profiles/Default/BepInEx/core");
resolver.AddSearchDirectory("C:/Juegos/Steam/steamapps/common/Monster Train 2/MonsterTrain2_Data/Managed");
var reader=new ReaderParameters{AssemblyResolver=resolver};
using var game=AssemblyDefinition.ReadAssembly("C:/Juegos/Steam/steamapps/common/Monster Train 2/MonsterTrain2_Data/Managed/Assembly-CSharp.dll",reader);
using var mod=AssemblyDefinition.ReadAssembly(Path.Combine(root,"src/bin/Release/netstandard2.1/WwInc.Plugin.dll"),reader);
using var conductor=AssemblyDefinition.ReadAssembly("C:/Users/david/AppData/Roaming/Thunderstore Mod Manager/DataFolder/MonsterTrain2/profiles/Default/BepInEx/plugins/Conductor-Conductor/Conductor.dll",reader);
var gameTypes=game.MainModule.Types.ToDictionary(t=>t.FullName);
var types=mod.MainModule.Types.Concat(game.MainModule.Types).Concat(conductor.MainModule.Types).GroupBy(t=>t.Name).ToDictionary(g=>g.Key,g=>g.First());
var errors=new List<string>();int checks=0;
void Check(bool condition,string message){checks++;if(!condition)errors.Add(message);}
bool Derives(TypeDefinition? type,string name)
{
    var seen=new HashSet<string>();
    while(type!=null&&seen.Add(type.FullName))
    {if(type.Name==name)return true;if(type.BaseType==null)break;types.TryGetValue(type.BaseType.Name,out type);}
    return false;
}
foreach(var path in Directory.GetFiles(Path.Combine(root,"content/json"),"*.json",SearchOption.AllDirectories))
{
    var text=File.ReadAllText(path).TrimStart('\uFEFF');using var doc=JsonDocument.Parse(text);
    foreach(var pair in new[]{("effects","CardEffectBase"),("traits","CardTraitState"),("room_modifiers","RoomStateModifierBase"),("relic_effects","RelicEffectBase")})
    {
        if(!doc.RootElement.TryGetProperty(pair.Item1,out var rows))continue;
        foreach(var row in rows.EnumerateArray())
        {
            var n=row.GetProperty("name");var name=n.ValueKind==JsonValueKind.String?n.GetString()!:n.GetProperty("id").GetString()!;name=name.TrimStart('@');
            Check(types.TryGetValue(name,out var type)&&Derives(type,pair.Item2),$"{path}: {name} must inherit {pair.Item2}");
        }
    }
}
foreach(var patch in mod.MainModule.Types)
{
    var attr=patch.CustomAttributes.FirstOrDefault(a=>a.AttributeType.Name=="HarmonyPatch"&&a.ConstructorArguments.Count>=2);
    if(attr==null)continue;
    var targetType=(TypeReference)attr.ConstructorArguments[0].Value;var methodName=(string)attr.ConstructorArguments[1].Value;
    Check(gameTypes.ContainsKey(targetType.FullName),$"Unknown Harmony type {targetType}");
    if(!gameTypes.TryGetValue(targetType.FullName,out var target))continue;
    var methods=target.Methods.Where(m=>m.Name==methodName).ToArray();
    if(attr.ConstructorArguments.Count>=3&&attr.ConstructorArguments[2].Value is CustomAttributeArgument[] signature)
    {
        var parameterTypes=signature.Select(a=>((TypeReference)a.Value).FullName).ToArray();
        methods=methods.Where(m=>m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(parameterTypes)).ToArray();
    }
    Check(methods.Length==1,$"Ambiguous Harmony target {target.Name}.{methodName}: {methods.Length} overloads");
    foreach(var hook in patch.Methods.Where(m=>m.Name is "Prefix" or "Postfix"))foreach(var p in hook.Parameters)
        if(!p.Name.StartsWith("__"))Check(methods.Any(m=>m.Parameters.Any(a=>a.Name==p.Name)),$"{patch.Name}.{hook.Name}: invalid parameter {p.Name}");
}
foreach(var name in new[]{"CardTraitStrongerMagicPower","CardTraitEphemeral","CardTraitDamageOverflow","CardTraitSelfPurge","CardTraitFreeze"})Check(types.ContainsKey(name),"Unknown native trait "+name);
var plugin=mod.MainModule.Types.Single(t=>t.Name=="Plugin");
foreach(var guid in new[]{"TrainworksReloaded.Plugin","Conductor"})Check(plugin.CustomAttributes.Any(a=>a.AttributeType.Name=="BepInDependency"&&(string)a.ConstructorArguments[0].Value==guid),"Wrong dependency "+guid);
// Native spawning without a selected location omits pip checks. The mod must add one.
var catering=mod.MainModule.Types.Single(t=>t.Name=="CateringRules");
var gate=catering.Methods.Single(m=>m.Name=="CanSpawn");
var calls=gate.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(calls.Contains("WillBeAtCapacity"),"Catering explicitly checks pips before spawning");
Check(calls.Contains("IsRoomEnabled")&&calls.Contains("IsRoomSummonBlocked"),"Catering respects disabled and summon-blocked floors");
Check(calls.Contains("GetCanSpawnMonster"),"Catering also checks a native unit slot");
var begin=catering.NestedTypes.Single(t=>t.Name.StartsWith("<BeginTurn>"));
var production=begin.Methods.Single(m=>m.Name=="MoveNext");
var productionCalls=production.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
var conduitReader=production.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>()
    .Where(m=>m.DeclaringType.Name=="<>c").Select(m=>m.Resolve())
    .Any(m=>m.HasBody&&m.Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.Name=="GetStatusEffectStacks"));
Check(productionCalls.Contains("CanSpawn")&&productionCalls.Contains("Sum")&&conduitReader,"Production consults capacity and current Conduit every turn");
Check(productionCalls.Contains("ApplyCardUpgrade"),"Food stats are applied at spawn instead of a dynamic aura");
var upgrades=new Dictionary<string,JsonElement>();
foreach(var path in Directory.GetFiles(Path.Combine(root,"content/json"),"*.json",SearchOption.AllDirectories))
{
    using var doc=JsonDocument.Parse(File.ReadAllText(path).TrimStart('\uFEFF'));
    if(doc.RootElement.TryGetProperty("upgrades",out var rows))
        foreach(var row in rows.EnumerateArray())upgrades[row.GetProperty("id").GetString()!]=row.Clone();
}
foreach(var prefix in new[]{"FratBro","Explosive","Attuned","QueenBee","PublishOrIncant","TenureOfProtection"})
{
    int lastHP=-1,lastAttack=-1;
    for(int rank=1;rank<=3;rank++)
    {
        var id="WW_"+prefix+rank;Check(upgrades.TryGetValue(id,out var upgrade),"Champion rank registered: "+id);
        if(!upgrades.ContainsKey(id))continue;
        Check((upgrade.TryGetProperty("descriptions",out var description)&&description.GetProperty("english").GetString()!.Length>0)||(upgrade.TryGetProperty("trigger_upgrades",out var nativeTriggers)&&nativeTriggers.GetArrayLength()>0),"Champion node describes effect directly or through native triggers: "+id);
        Check(upgrade.GetProperty("does_not_replace_ability").GetBoolean()&&!upgrade.TryGetProperty("ability_upgrade",out _),"Champion paths preserve shared ability: "+id);
        int hp=upgrade.GetProperty("bonus_hp").GetInt32(),attack=upgrade.GetProperty("bonus_damage").GetInt32();
        Check(hp>lastHP&&attack>=lastAttack,"Champion stats progress by rank: "+id);lastHP=hp;lastAttack=attack;
    }
}
var save=gameTypes["SaveManager"];
Check(save.Methods.Count(m=>m.Name=="set_PreviewMode")==1,"Native preview flag has one setter");
Check(save.Methods.Any(m=>m.Name=="get_IsInUndoModeStatic"),"Native replay flag is available for Purge reconstruction");
var deck=gameTypes["DeckScreen"];
var selection=deck.Methods.Single(m=>m.Name=="DoCardSelection");
Check(selection.Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.Name=="ReplayRecordSelectedCard"),"Pile selection uses native replay recording");
var cardState=gameTypes["CardState"];
Check(cardState.Methods.Count(m=>m.Name=="RemoveEquippedCharacterState")==1,"Equipment owner cleanup has one native method");
var character=gameTypes["CharacterState"];
var removeEquipment=character.NestedTypes.Single(t=>t.Name.StartsWith("<RemoveEquipment>"));
var removeEquipmentCalls=removeEquipment.Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(removeEquipmentCalls.Contains("RemoveEquippedCharacterState")&&removeEquipmentCalls.Contains("RemoveCardUpgrade"),"Native equipment removal clears owner and removes bonuses");
Check(!removeEquipmentCalls.Contains("SetRemoveFromStandByPileOverride")&&!removeEquipmentCalls.Contains("DrawSpecificCard"),"Native equipment removal does not itself change card piles");
var removeAbility=character.NestedTypes.Single(t=>t.Name.StartsWith("<RemoveUnitAbility>"));
Check(removeAbility.Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.Name=="AddUnitAbilityInternal"),"Unequipping restores the previous unit ability natively");
var wizard=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicWizardDraw");
Check(wizard.Methods.Single(m=>m.Name=="TestEffect").Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.Name=="GetDrawingDeckCardsDisabledThisTurn"),"Wizard respects native draw prevention");
var previewTargets=mod.MainModule.Types.Single(t=>t.Name=="AcademicPreviewTargetsHook");
var previewTargetCalls=previewTargets.Methods.Single(m=>m.Name=="Postfix").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(previewTargetCalls.Contains("GetHeroManager")&&previewTargetCalls.Contains("GetMonsterManager"),"Preview also copies manager-owned exterior units");
var returnEffect=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicEquipmentReturn");
var previewProperty=returnEffect.Methods.Single(m=>m.Name=="get_CanApplyInPreviewMode");
Check(previewProperty.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_1),"Equipment return supports preview");
var versionAttribute=plugin.CustomAttributes.Single(a=>a.AttributeType.Name=="BepInPlugin");
var registeredVersion=(string)versionAttribute.ConstructorArguments[2].Value;
Check(mod.Name.Version.ToString(3)==registeredVersion,"Assembly and BepInEx versions match");
var restoreRules=mod.MainModule.Types.Single(t=>t.Name=="SpellRules");
var restoreCoroutine=restoreRules.NestedTypes.Single(t=>t.Name.StartsWith("<Restore>"));
var restoreInstructions=restoreCoroutine.Methods.Single(m=>m.Name=="MoveNext").Body.Instructions;
int handRead=restoreInstructions.ToList().FindIndex(i=>i.Operand is MethodReference call&&call.Name=="GetHand");
int drawIndex=restoreInstructions.ToList().FindIndex(i=>i.Operand is MethodReference call&&call.Name=="DrawSpecificCard");
Check(handRead>=0&&drawIndex>handRead&&restoreInstructions.Skip(handRead).Take(drawIndex-handRead).Any(i=>i.Operand is MethodReference call&&call.Name=="Contains"),"Restore consults the real hand before drawing a specific card");
var guard=restoreInstructions.Skip(handRead).Take(drawIndex-handRead).FirstOrDefault(i=>i.OpCode.FlowControl==Mono.Cecil.Cil.FlowControl.Cond_Branch);
Check(guard!=null&&restoreInstructions.Skip(restoreInstructions.IndexOf(guard)+1).Take(4).Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ret),"Existing hand card can exit the coroutine without drawing again");
var redirect=mod.MainModule.Types.Single(t=>t.Name=="RedirectTargetHook").Methods.Single(m=>m.Name=="Prefix");
var redirectIL=redirect.Body.Instructions.ToList();
int repeatGuard=redirectIL.FindIndex(i=>i.Operand is FieldReference f&&f.Name=="EffectFloor");
int redirectedFrame=redirectIL.FindIndex(i=>i.Operand is FieldReference f&&f.Name=="Frames");
Check(repeatGuard>=0&&redirectedFrame>repeatGuard&&redirectIL.Skip(repeatGuard).Take(redirectedFrame-repeatGuard).Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ret),"Explicit repeat target is preserved before reading original redirected frame");
var pathText=mod.MainModule.Types.Single(t=>t.Name=="ChampionPathText");
var pathCalls=pathText.Methods.Single(m=>m.Name=="Selected").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(pathCalls.Contains("GetCardStateModifiers")&&pathCalls.Contains("GetTemporaryCardStateModifiers")&&pathCalls.Contains("GetAppliedCardUpgrades")&&pathCalls.Contains("ChampionRank"),"Path text uses permanent, temporary and deployed upgrades at highest rank");
Check(pathText.Methods.Single(m=>m.Name=="Description").Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.Name=="Localize"),"Champion path text uses native localized upgrade descriptions");
var ruleEffect=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicSpellRule");
Check(ruleEffect.Methods.Single(m=>m.Name=="get_CanApplyInPreviewMode").Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_1),"Noninteractive spell rules allow native previews");
var previewRules=ruleEffect.Methods.Single(m=>m.Name=="CanPreview").Body.Instructions
    .Where(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldstr).Select(i=>(string)i.Operand).ToHashSet();
Check(previewRules.SetEquals(new[]{"floors","double","power","healing_area"}),"Preview whitelist excludes every selection and pile-changing rule");
var testRuleIL=ruleEffect.Methods.Single(m=>m.Name=="TestEffect").Body.Instructions.ToList();
int testPreview=testRuleIL.FindIndex(i=>i.Operand is MethodReference call&&call.Name=="CanPreview");
int handAccess=testRuleIL.FindIndex(i=>i.Operand is MethodReference call&&call.Name=="GetCardManager");
Check(testPreview<0&&handAccess>=0,"Cast eligibility checks cards even when native target pruning requests a preview test");
var applyRuleIL=ruleEffect.NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.ToList();
int applyPreview=applyRuleIL.FindIndex(i=>i.Operand is MethodReference call&&call.Name=="CanPreview");
int coreMutation=applyRuleIL.FindIndex(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Stsfld&&i.Operand is FieldReference f&&f.Name=="Core");
Check(applyPreview>=0&&coreMutation>applyPreview&&applyRuleIL.Skip(applyPreview).Take(coreMutation-applyPreview).Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ret),"ApplyEffect independently rejects excluded previews before any state mutation or selector");
var nativeRuleTest=gameTypes["GameEffectHelper"].Methods.Single(m=>m.Name=="TestEffect"&&m.Parameters[0].ParameterType.Name=="CardEffectState");
Check(nativeRuleTest.Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.Name=="get_CanApplyInPreviewMode"),"Native card effect test consults preview capability");
var bannerData=new Dictionary<string,Dictionary<string,JsonElement>>();
foreach(var path in Directory.GetFiles(Path.Combine(root,"content/json"),"*.json",SearchOption.AllDirectories))
{
    using var doc=JsonDocument.Parse(File.ReadAllText(path).TrimStart('\uFEFF'));
    foreach(var group in new[]{"cards","characters","effects","card_pools","character_triggers","character_trigger_types","card_triggers","traits","upgrades","relics","upgrade_masks"})
    {
        if(!doc.RootElement.TryGetProperty(group,out var rows))continue;
        if(!bannerData.ContainsKey(group))bannerData[group]=new();
        foreach(var row in rows.EnumerateArray())bannerData[group][row.GetProperty("id").GetString()!]=row.Clone();
    }
}
using var bannerCatalog=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"design/catalog.json")).TrimStart('\uFEFF'));
var bannerUnits=bannerCatalog.RootElement.GetProperty("cards").EnumerateArray().Where(c=>c.GetProperty("type").GetString()=="unit").ToArray();
var expectedBanner=bannerUnits.Where(c=>c.TryGetProperty("banner",out var b)&&b.GetBoolean()).Select(c=>c.GetProperty("id").GetString()!).ToHashSet();
var poolMembers=bannerData["card_pools"]["WW_BannerPool"].GetProperty("cards").EnumerateArray().Select(c=>c.GetString()!.TrimStart('@')).ToHashSet();
Check(poolMembers.SetEquals(expectedBanner),"Dedicated banner pool contains exactly the selected banner units");
foreach(var unit in bannerUnits)
{
    string id=unit.GetProperty("id").GetString()!;var card=bannerData["cards"][id];
    bool nativeBanner=card.GetProperty("effects").EnumerateArray().Select(e=>bannerData["effects"][e.GetString()!.TrimStart('@')])
        .Where(e=>e.TryGetProperty("param_character",out _)).Select(e=>bannerData["characters"][e.GetProperty("param_character").GetString()!.TrimStart('@')])
        .Any(c=>c.GetProperty("subtypes").EnumerateArray().Any(t=>t.GetString()=="SubtypesData_BannerUnit"));
    Check(nativeBanner==expectedBanner.Contains(id),id+": native BannerUnit subtype matches selected role");
    Check(card.GetProperty("pools").EnumerateArray().Any(p=>p.GetString()=="UnitsAllBanner")==expectedBanner.Contains(id),id+": global banner pool matches selected role");
}
Check(gameTypes["CardData"].Methods.Single(m=>m.Name=="IsBannerUnit").Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldstr&&(string)i.Operand=="SubtypesData_BannerUnit"),"Native IsBannerUnit tests the exact subtype key used by the mod");
foreach(var id in new[]{"WW_C01","WW_U06","WW_U10","WW_U11","WW_E01"})
 Check(!bannerData["cards"][id].TryGetProperty("descriptions",out _),id+": ability-only card uses native Ability reference instead of repeating its effect");
foreach(var id in new[]{"WW_C01_Ability","WW_U06_Ability","WW_U10_Ability","WW_U11_Ability","WW_U18_Ability","WW_E01_Ability"})
 Check(bannerData["cards"][id].GetProperty("descriptions").GetProperty("english").GetString()!.Length>0,id+": ability keeps its effect text for native tooltips");
Check(!bannerData["upgrades"]["WW_E01_Upgrade"].TryGetProperty("descriptions",out _),"Withdrawal Stamp upgrade grants native ability reference without repeating its action");
var wand=bannerData["cards"]["WW_E03"];
Check(wand.GetProperty("card_type").GetString()=="equipment"&&wand.GetProperty("cost").GetInt32()==1&&wand.GetProperty("rarity").GetString()=="rare","Tenured Wand is rare equipment costing one Ember");
Check(bannerData["upgrades"]["WW_E03_Upgrade"].GetProperty("trigger_upgrades")[0].GetString()=="@WW_E03_ArcaneSlay"&&bannerData["character_triggers"]["WW_E03_ArcaneSlay"].GetProperty("trigger").GetString()=="@WW_ArcaneSlay","Equipping Tenured Wand grants the existing Arcane Slay trigger");
var wandGrowth=bannerData["effects"]["WW_E03_GrowthEffect"];
Check(wandGrowth.GetProperty("name").GetString()=="CardEffectAddCardUpgradeToUnits"&&wandGrowth.GetProperty("target_mode").GetString()=="self"&&wandGrowth.GetProperty("param_int_3").GetInt32()==2,"Wand uses native permanent unit upgrade lifetime, targeting only its owner");
Check(gameTypes["UnitUpgradeLifetime"].Fields.Single(f=>f.Name=="Permanent").Constant is int permanentLifetime&&permanentLifetime==2,"Native Permanent lifetime enum matches the JSON parameter");
Check(bannerData["upgrades"]["WW_E03_Growth"].GetProperty("status_effect_upgrades")[0].GetProperty("status").GetString()=="conduit"&&bannerData["upgrades"]["WW_E03_Growth"].GetProperty("status_effect_upgrades")[0].GetProperty("count").GetInt32()==1,"Wand gain is one native Conduit per spell kill");
Check(!wand.TryGetProperty("descriptions",out _),"Equipment trigger text is generated natively without a duplicate card override");
Check(bannerData["cards"]["WW_S09"].GetProperty("rarity").GetString()=="rare"&&bannerData["cards"]["WW_S10"].GetProperty("rarity").GetString()=="uncommon","Scholarship is rare; retired Publication keeps its prior rarity");
Check(bannerData["cards"]["WW_S09"].GetProperty("descriptions").GetProperty("english").GetString()!.EndsWith("1[ember]."),"Scholarship wording omits default battle duration");
var scholarshipRules=mod.MainModule.Types.Single(t=>t.Name=="SpellRules");
var applyScholarship=scholarshipRules.Methods.Single(m=>m.Name=="ApplyHitDiscount");
Check(applyScholarship.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="CardUpgradeHelper"&&c.Name=="TryApplyUpgradeToCard")&&!applyScholarship.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="EndTurnActions"),"Scholarship uses native upgrade application without end-turn expiry");
Check(!scholarshipRules.Methods.Any(m=>m.Name=="RemoveHitDiscount"),"Scholarship no longer clears its battle-long cost reduction when played");
Check(bannerData["upgrades"]["WW_S09_Discount"].GetProperty("cost_reduction").GetInt32()==1,"Each Scholarship hit reduces cost by one");
Check(bannerData["cards"]["WW_S11"].GetProperty("pools").GetArrayLength()==0&&bannerData["cards"]["WW_S11"].GetProperty("hide_in_logbook").GetBoolean(),"Credit Recovery is retired from rewards and logbook");
var extension=bannerData["effects"]["WW_S29_Effect"];
Check(extension.GetProperty("name").GetString()=="CardEffectAddStatusEffect"&&extension.GetProperty("target_mode").GetString()=="room"&&extension.GetProperty("target_team").GetString()=="heroes", "Extension Spike applies native status to every enemy on its floor");
Check(extension.GetProperty("param_status_effects")[0].GetProperty("status").GetString()=="spell weakness"&&extension.GetProperty("param_status_effects")[0].GetProperty("count").GetInt32()==0,"Spike base stacks are zero before native X scaling");
var extensionX=bannerData["traits"]["WW_S29_WeaknessX"];
Check(extensionX.GetProperty("name").GetString()=="CardTraitScalingAddStatusEffect"&&extensionX.GetProperty("param_tracked_value").GetString()=="played_cost"&&extensionX.GetProperty("param_int").GetInt32()==1,"Spike uses the official status-X trait with multiplier one");
Check(!mod.MainModule.Types.Any(t=>t.Name=="CardEffectAcademicSpike"),"Spike no longer requires custom C# or a cooldown modification");
Check(!bannerData["cards"]["WW_S29"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("cooldown"),"Spike wording no longer describes cooldown");
foreach(var id in new[]{"WW_S10","WW_S06","WW_S04","WW_S32","WW_S03","WW_S08"})
 Check(bannerData["cards"][id].GetProperty("pools").GetArrayLength()==0&&bannerData["cards"][id].GetProperty("hide_in_logbook").GetBoolean(),id+": retired from rewards and logbook");
var medicalBefore=mod.MainModule.Types.Single(t=>t.Name=="SpellRules").NestedTypes.Single(t=>t.Name.StartsWith("<Before>")).Methods.Single(m=>m.Name=="MoveNext");
Check(medicalBefore.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="IsHealingSpell")&&medicalBefore.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldstr&&(string)i.Operand=="A10"),"Medicine relic reserves the first native healing spell before resolution");
Check(bannerData["relics"]["WW_A10"].GetProperty("descriptions").GetProperty("english").GetString()!.StartsWith("Your first healing spell each turn also"),"Medicine relic text preserves healing and restricts to first healing spell per turn");
Check(bannerData["relics"]["WW_A10"].GetProperty("extensions")[0].GetProperty("collectable").GetProperty("pools")[0].GetString()=="MegaRelicPool", "Medicine relic is available through the native relic reward pool");
var healingClassifier=mod.MainModule.Types.Single(t=>t.Name=="SpellRules").Methods.Single(m=>m.Name=="HealingEffect");
Check(healingClassifier.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Isinst&&i.Operand is TypeReference t&&t.Name=="CardEffectHeal")&&healingClassifier.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="OtherHealingCardEffects"),"First-heal relic uses native Heal subclasses and Conductor's shared healing registry");
foreach(var id in new[]{"WW_S19","WW_S01","WW_S09","WW_S22","WW_S20"})Check(bannerData["cards"][id].GetProperty("rarity").GetString()=="rare",id+": approved rare rarity");
Check(bannerData["cards"]["WW_S21"].GetProperty("rarity").GetString()=="uncommon","Armored Honors is uncommon");
Check(bannerData["cards"]["WW_S27"].GetProperty("pools").GetArrayLength()==0&&bannerData["cards"]["WW_S27"].GetProperty("hide_in_logbook").GetBoolean(),"Campus Ice Cream Stand retired from rewards and logbook");
Check(bannerData["cards"]["WW_S22"].GetProperty("cost").GetInt32()==0,"Emergency Office Hours costs zero");
Check(bannerData["effects"]["WW_S19_Draw"].GetProperty("name").GetString()=="CardEffectDraw"&&bannerData["effects"]["WW_S19_Draw"].GetProperty("param_int").GetInt32()==1&&bannerData["cards"]["WW_S19"].GetProperty("effects")[1].GetString()=="@WW_S19_Draw","Course Withdrawal adds native draw one after its original effect");
Check(bannerData["effects"]["WW_S17_Effect"].GetProperty("param_int").GetInt32()==10,"Sabbatical adds ten to missing-health healing");
using(var dissertationDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/cards/WW_S02.json")).TrimStart('\uFEFF')))
{
 var marker=dissertationDoc.RootElement.GetProperty("status_effects")[0];
 Check(marker.GetProperty("id").GetString()=="ww_dissertation"&&!marker.GetProperty("hidden").GetBoolean()&&!marker.GetProperty("is_propagatable").GetBoolean(),"Dissertation marker is visible and cannot propagate separately from its bound spell");
 Check(marker.GetProperty("trigger_stage").GetString()=="none"&&marker.GetProperty("is_stackable").GetBoolean(),"Dissertation marker displays a counter without adding combat triggers");
}
var dissertationMarker=mod.MainModule.Types.Single(t=>t.Name=="DissertationMarker");
Check(dissertationMarker.Methods.Single(m=>m.Name=="Refresh").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="RemoveStatusEffect"),"Dissertation marker is removed when no bound attacks remain");
Check(dissertationMarker.Methods.Single(m=>m.Name=="Details").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Join"),"Dissertation tooltip can list multiple bound spells");
var attackCallback=mod.MainModule.Types.Single(t=>t.Name=="AttackSequenceHook");
Check(attackCallback.NestedTypes.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="DissertationMarker"&&c.Name=="Refresh"),"Every native attack updates the Dissertation counter");
using(var scholarshipDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/cards/WW_S09.json")).TrimStart('\uFEFF')))
{
 var marker=scholarshipDoc.RootElement.GetProperty("status_effects")[0];
 Check(marker.GetProperty("id").GetString()=="ww_scholarship"&&!marker.GetProperty("hidden").GetBoolean()&&!marker.GetProperty("is_propagatable").GetBoolean(),"Scholarship status is visible and cannot propagate");
 Check(marker.GetProperty("character_tooltips").GetProperty("english").GetString()!.Contains("random card in your draw pile")&&marker.GetProperty("card_tooltips").EnumerateObject().Count()==10,"Scholarship explains its effect in character and card tooltips across ten languages");
}
Check(bannerData["effects"]["WW_S09_Effect"].GetProperty("name").GetString()=="CardEffectAddStatusEffect"&&bannerData["effects"]["WW_S09_Effect"].GetProperty("param_status_effects")[0].GetProperty("status").GetString()=="@ww_scholarship","Scholarship applies its state with a native effect");
Check(!mod.MainModule.Types.Single(t=>t.Name=="SpellRules").Fields.Any(f=>f.Name=="HitDiscounts"),"Scholarship state replaces the invisible separate unit flag");
Check(mod.MainModule.Types.Single(t=>t.Name=="Plugin").NestedTypes.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Count(i=>i.Operand is MethodReference c&&c.Name=="GetStatusId")>=2,"Both custom unit markers use Trainworks-registered status IDs instead of local JSON IDs");
var scholarshipHit=mod.MainModule.Types.Single(t=>t.Name=="UnitAttackHook").NestedTypes.Single(t=>t.Name.StartsWith("<Run>")).Methods.Single(m=>m.Name=="MoveNext");
Check(scholarshipHit.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetDrawPile")&&!scholarshipHit.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetHand"),"Scholarship hit selects from the draw pile rather than the discarded combat hand");
foreach(var id in new[]{"WW_S09","WW_S02","WW_S25"})
using(var iconDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/cards/"+id+".json")).TrimStart('\uFEFF')))
{
 var definition=iconDoc.RootElement;var iconId=definition.GetProperty("status_effects")[0].GetProperty("icon").GetString()!.TrimStart('@');
 Check(definition.GetProperty("sprites").EnumerateArray().Any(s=>s.GetProperty("id").GetString()==iconId)&&definition.GetProperty("atlas_icons").EnumerateArray().Any(s=>s.GetProperty("id").GetString()==iconId),id+": native status icon and TMP sprite atlas share the same registered ID");
}
var deanDraw=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicWizardDraw");
Check(deanDraw.Interfaces.Any(i=>i.InterfaceType.Name=="ICardTargetingCardEffect")&&deanDraw.Interfaces.Any(i=>i.InterfaceType.Name=="ICardEffectDrawsCards"),"Dean declares native card targeting and draw interfaces like Tethys");
var deanTestCalls=deanDraw.Methods.Single(m=>m.Name=="TestEffect").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(deanTestCalls.Contains("GetDrawPile")&&deanTestCalls.Contains("GetDiscardPile")&&deanTestCalls.Contains("GetParamCardFilter"),"Dean eligibility searches both native piles with the draw filter");
var deanApply=deanDraw.NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.ToList();
int queueDrawUpgrade=deanApply.FindIndex(i=>i.Operand is MethodReference call&&call.Name=="AddTempCardUpgradeToNextDrawnCard");
int nativeDrawReceiver=deanApply.FindIndex(i=>i.Operand is FieldReference field&&field.Name=="nativeDraw"&&field.FieldType.Name=="CardEffectDrawType");
int nativeDrawCall=nativeDrawReceiver<0?-1:deanApply.FindIndex(nativeDrawReceiver,i=>i.Operand is MethodReference call&&call.Name=="ApplyEffect");
Check(queueDrawUpgrade>=0&&nativeDrawReceiver>queueDrawUpgrade&&nativeDrawCall>nativeDrawReceiver,"Dean queues shared Explosive before invoking the native Tethys draw effect");
Check(!deanApply.Any(i=>i.Operand is MethodReference c&&c.Name=="Rank"),"Shared Dean draw does not depend on a selected path");
Check(bannerData["traits"]["WW_C01_NextUseExplosive"].GetProperty("name").GetString()=="CardTraitDamageOverflow"&&bannerData["upgrades"]["WW_C01_NextUseUpgrade"].GetProperty("trait_upgrades").GetArrayLength()==2,"Dean grants Explosive plus next-use marker without an Attuned bonus");
foreach(var prefix in new[]{"WW_Explosive","WW_Attuned"})
for(int rank=1;rank<=3;rank++)
{
 var triggerId=prefix+rank+"_ArcaneSlay";
 Check(bannerData["upgrades"][prefix+rank].GetProperty("trigger_upgrades")[0].GetString()=="@"+triggerId&&bannerData["character_triggers"][triggerId].GetProperty("trigger").GetString()=="@WW_ArcaneSlay",prefix+rank+": all ranks use Arcane Slay, never melee Slay");
 if(prefix=="WW_Explosive")
 {
  var e=bannerData["effects"][prefix+rank+"_SlayEffect"];
  Check(e.GetProperty("name").GetString()=="CardEffectAddStatusEffect"&&e.GetProperty("target_mode").GetString()=="self"&&e.GetProperty("param_status_effects")[0].GetProperty("status").GetString()=="conduit"&&e.GetProperty("param_status_effects")[0].GetProperty("count").GetInt32()==rank,"Practical Magic grants rank-scaled Conduit with native effect");
 }
 else
 {
  var e=bannerData["effects"][prefix+rank+"_SlayEffect"];var u=bannerData["upgrades"][prefix+rank+"_HandPower"];
  Check(e.GetProperty("target_mode").GetString()=="self"&&e.GetProperty("name").GetString()=="@CardEffectAcademicUpgradeSlayingSpell","Spell Tuning upgrades the responsible spell, not the hand");
  Check(u.GetProperty("bonus_damage").GetInt32()==rank&&u.GetProperty("bonus_heal").GetInt32()==rank&&u.GetProperty("filters")[0].GetString()=="OnlySpells","Spell Tuning grants Magic Power to spells, including healing spells, and excludes units");
 }
}
Check(!bannerData["cards"]["WW_C01"].TryGetProperty("descriptions",out _),"Dean exposes native ability and trigger texts without an overriding body");
Check(bannerData["cards"]["WW_C01_Ability"].GetProperty("descriptions").GetProperty("english").GetString()=="Draw a damage spell. Give it [explosive] for its next play.","Dean ability states the grant lasts for its next play");

var nativeTypedDraw=gameTypes["CardEffectDrawType"].NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext");
var nativeTypedDrawCalls=nativeTypedDraw.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(nativeTypedDrawCalls.Contains("GetDrawPile")&&nativeTypedDrawCalls.Contains("GetDiscardPile")&&nativeTypedDrawCalls.Contains("DrawSpecificCard"),"Native DrawType really draws from draw and discard piles");
var deanAbility=bannerData["cards"]["WW_C01_Ability"];
Check(!deanAbility.TryGetProperty("targets_room",out var deanTargetsRoom)||!deanTargetsRoom.GetBoolean(),"Dean ability targets a character spawn point, like native Runespeak");
Check(deanAbility.TryGetProperty("initial_keyboard_target",out var deanKeyboard)&&deanKeyboard.GetString()=="front_friendly","Dean has native friendly keyboard targeting");
var deanEffect=bannerData["effects"]["WW_C01_Ability_Effect"];
using var nativeRunespeak=JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(root,"../../../../datos-juego/MonsterTrain2GameData/CardData/Runespeak.json"))).TrimStart('\uFEFF'));
string nativeDamageFilter=nativeRunespeak.RootElement.GetProperty("effects")[1].GetProperty("param_card_filter").GetString()!;
Check(deanEffect.TryGetProperty("param_card_filter",out var deanFilter)&&deanFilter.GetString()==nativeDamageFilter&&deanEffect.TryGetProperty("param_int",out var deanCount)&&deanCount.GetInt32()==1,"Dean uses Runespeak's actual native damage-spell filter and draw count");
// Casting must remain valid while simulation must never execute a card selector.
var selectionEffects=new[]{"CardEffectAcademicWizardDraw","CardEffectAcademicConsumeCredits","CardEffectAcademicConduitTransfer","CardEffectAcademicPurgePermit","CardEffectAcademicDiscardForEmber"};
foreach(var name in selectionEffects)
{
    var type=mod.MainModule.Types.Single(t=>t.Name==name);
    Check(type.Methods.Single(m=>m.Name=="get_CanApplyInPreviewMode").Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_1),name+": native preview target validation accepts eligibility");
    Check(type.Methods.Any(m=>m.Name=="TestEffect"),name+": explicit eligible-card test prevents empty selectors");
    var il=type.NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.ToList();
    int preview=il.FindIndex(i=>i.Operand is MethodReference c&&c.Name=="get_PreviewMode");
    int mutate=il.FindIndex(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Stsfld&&i.Operand is FieldReference f&&f.Name=="Core");
    Check(preview>=0&&mutate>preview&&il.Skip(preview).Take(mutate-preview).Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ret),name+": simulation exits before state mutation and card selection");
}
foreach(var ability in bannerData["cards"].Values.Where(c=>c.TryGetProperty("is_an_ability",out var a)&&a.GetBoolean()))
{
    Check(ability.TryGetProperty("targets_room",out var room)&&!room.GetBoolean(),ability.GetProperty("id").GetString()+": targetless unit ability keeps the activating character target");
    Check(ability.TryGetProperty("initial_keyboard_target",out var keyboard)&&keyboard.GetString()=="front_friendly",ability.GetProperty("id").GetString()+": keyboard target matches native friendly point");
}
Check(!mod.MainModule.Types.Any(t=>t.Name=="CardEffectAcademicCurrentCooldown"),"S22 delegates cooldown removal to the native effect");
Check(gameTypes["CharacterState"].Methods.Single(m=>m.Name=="GetUnitAbilityAvailability").Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldstr&&(string)i.Operand=="cooldown"),"Native ability availability is blocked by cooldown status, not its base duration");
var s22effects=bannerData["cards"]["WW_S22"].GetProperty("effects").EnumerateArray().Select(e=>bannerData["effects"][e.GetString()!.TrimStart('@')]).ToArray();
Check(s22effects.Any(e=>e.GetProperty("name").GetString()=="CardEffectRemoveStatusEffect"&&e.GetProperty("param_status_effects")[0].GetProperty("status").GetString()=="cooldown"&&e.GetProperty("param_status_effects")[0].GetProperty("count").GetInt32()==1&&e.GetProperty("disallow_status_effect_modifiers").GetBoolean())&&s22effects.Any(e=>e.GetProperty("name").GetString()=="CardEffectAddStatusEffect"&&e.GetProperty("param_status_effects")[0].GetProperty("status").GetString()=="dazed"),"S22 removes exactly one current cooldown stack and still applies Dazed");
var powerCalls=mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").Methods.Single(m=>m.Name=="ModifyCard").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(powerCalls.Contains("PowerUpgrade")&&powerCalls.Contains("GetMagicPowerMultiplierFromTraits"),"Added Magic Power boosts both damage and healing");
var frozenHook=mod.MainModule.Types.Single(t=>t.Name=="FrozenCardDrawHook");
Check(frozenHook.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPatch"&&a.ConstructorArguments.Count>=2&&(string)a.ConstructorArguments[1].Value=="PlayAnyCard"),"Frozen card relic covers successful native plays of every card type");
Check(frozenHook.Methods.Single(m=>m.Name=="Postfix").Parameters.Any(p=>p.Name=="__result")&&!frozenHook.Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetCardType"),"Frozen card relic tests cast success and does not restrict itself to spells");
var discardReturn=mod.MainModule.Types.Single(t=>t.Name=="DiscardRuleHook").NestedTypes.Single(t=>t.Name.StartsWith("<Run>")).Methods.Single(m=>m.Name=="MoveNext");
Check(discardReturn.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="SpellRules"&&c.Name=="Restore"),"Consume kill return uses the shared full-hand and duplicate guard");
foreach(var name in new[]{"EligibleRedirectHook","RedirectTargetHook"})
    Check(!mod.MainModule.Types.Single(t=>t.Name==name).Methods.SelectMany(m=>m.HasBody?m.Body.Instructions.AsEnumerable():Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Isinst&&i.Operand is TypeReference type&&type.Name=="CardEffectDamage"),name+": selected area damage spell supports native damage-effect subclasses");
Check(bannerData["cards"]["WW_S12"].GetProperty("effects").GetArrayLength()==3,"Triple Impact Exam really has three damage resolutions");
Check(!bannerData["cards"]["WW_S08"].GetProperty("effects").EnumerateArray().Any(e=>bannerData["effects"][e.GetString()!.TrimStart('@')].GetProperty("name").GetString()=="CardEffectRecursion"),"Consumed Bibliography has no unconditional recovery effect");
var drawRankCalls=deanDraw.Methods.Single(m=>m.Name=="Rank").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(drawRankCalls.Any(c=>c.Name=="GetSpawnerCard")&&drawRankCalls.Any(c=>c.DeclaringType.Name=="ChampionPathText"&&c.Name=="Selected"),"Dean reads champion-card upgrades, not only upgrades applied after deployment");
var auraReader=mod.MainModule.Types.Single(t=>t.Name=="SocialRules").Methods.Single(m=>m.Name=="RefreshAuras");
Check(auraReader.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="CardEffectAcademicWizardDraw"&&c.Name=="Rank"),"Both social champion paths share the same complete upgrade reader");
var powerFactory=mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").Methods.Single(m=>m.Name=="PowerUpgrade");
Check(powerFactory.Body.Instructions.Count(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldstr&&new[]{"bonusDamage","bonusHeal","sourceCardUpgradeData"}.Contains((string)i.Operand))==3,"Custom Power retains native unscaled source bonuses for subsequent Attuned recalibration");
var attunedCleanup=mod.MainModule.Types.Single(t=>t.Name=="SpellRules").NestedTypes.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(attunedCleanup.Any(c=>c.Name=="RemoveTemporaryTrait"&&c.Parameters.Count==1&&c.Parameters[0].ParameterType.Name=="CardTraitState"),"Temporary Attuned removes its exact instance, preserving independently added traits");
Check(mod.MainModule.Types.Single(t=>t.Name=="SpellRules").Methods.Single(m=>m.Name=="Attuned").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="ReapplyMagicPowerScalingFromTraitsToAllExistingUpgrades"),"Adding temporary Attuned recalibrates earlier native and custom Magic Power upgrades");
Check(gameTypes["CardState"].Methods.Single(m=>m.Name=="ReapplyMagicPowerScalingFromTraitsToAllExistingUpgrades").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetSourceCardUpgradeData"),"Native recalibration reads raw bonuses from source upgrade data");
Check(bannerData["characters"]["WW_T01_Character"].GetProperty("attack_damage").GetInt32()==2,"Wizard starter can attack to trigger Impact");
Check(bannerData["effects"]["WW_T01_ImpactEffect"].GetProperty("target_mode").GetString()=="last_attacked_character","Starter weakness targets the enemy actually hit");
Check(!bannerData["effects"].ContainsKey("WW_T01_Aura"),"Wizard starter no longer grants Conduit");
Check(bannerData["effects"]["WW_T02_Aura"].GetProperty("param_status_effects")[0].GetProperty("count").GetInt32()==5,"Witch starter retains Conduit 5");
Check(bannerData["effects"]["WW_T02_AuraWizard"].GetProperty("target_subtype").GetString()=="@WW_Wizard"&&bannerData["effects"]["WW_T02_AuraWizard"].GetProperty("param_status_effects")[0].GetProperty("count").GetInt32()==5,"Chaperone grants Conduit 5 to Wizards");
Check(bannerData["effects"]["WW_T02_Aura"].GetProperty("target_subtype").GetString()=="@WW_Witch","Chaperone retains Witch aura");
Check(bannerData["character_triggers"]["WW_T02_Enchant"].GetProperty("effects").EnumerateArray().Select(e=>e.GetString()).SequenceEqual(new[]{"@WW_T02_Aura","@WW_T02_AuraWizard"}),"Native enchant trigger registers both restricted auras");
foreach(var id in new[]{"WW_S29","WW_S30"})Check(bannerData["cards"][id].GetProperty("traits").EnumerateArray().Any(t=>t.GetString()=="@WW_Consume")&&bannerData["cards"][id].GetProperty("rarity").GetString()=="rare",id+": rare with Consume");
Check(bannerData["cards"]["WW_S29"].GetProperty("cost_type").GetString()=="x","Extension Spike really costs X");
Check(bannerData["effects"]["WW_S28_Effect"].GetProperty("name").GetString()=="CardEffectDamage"&&bannerData["effects"]["WW_S28_Effect"].GetProperty("param_int").GetInt32()==10,"Deferred Project is a native damage spell eligible for Dean draw");
foreach(var hookName in new[]{"RedirectUiHook","RedirectTargetHook"})
{
    var hookMethod=mod.MainModule.Types.Single(t=>t.Name==hookName).Methods.Single(m=>m.Name is "Prefix" or "Postfix");
    Check(!hookMethod.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.DeclaringType.Name=="AcademicRuntime"&&f.Name=="Core"),hookName+": selection does not depend on retained managers across turns");
    Check(hookMethod.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="RedirectTargets"&&c.Name=="InRoom"),hookName+": resolves targets in the native current room");
}
var redirectEligibility=mod.MainModule.Types.Single(t=>t.Name=="EligibleRedirectHook").Methods.Single(m=>m.Name=="Postfix");
Check(!redirectEligibility.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="AcademicRuntime"&&c.Name=="Units"),"Redirect validation uses caller managers rather than a retained room");
var teamValidator=gameTypes["CharacterTargetValidator"].Methods.Single(m=>m.Name=="IsValidTarget");
Check(teamValidator.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetTargetedTeamType")&&teamValidator.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetTargetCharacters"),"Native UI validates both team flags and target list");
var teamHook=mod.MainModule.Types.Single(t=>t.Name=="RedirectTeamValidationHook");
Check(teamHook.Methods.Single(m=>m.Name=="Finalizer").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Pop"),"Receiver team exception is removed even on native validation failure");
Check(teamHook.Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="IsAnyAbility"),"Receiver team exception excludes abilities");
var flagHook=mod.MainModule.Types.Single(t=>t.Name=="RedirectCardTeamHook").Methods.Single(m=>m.Name=="Postfix");
Check(flagHook.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Peek"),"Team exception is limited to the card currently being validated");
var recovery=mod.MainModule.Types.Single(t=>t.Name=="SpellRules").Methods.Single(m=>m.Name=="RecoveryCandidates");
var recoveryCalls=recovery.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(c=>c.Name).ToHashSet();
Check(recoveryCalls.Contains("GetDrawPile")&&recoveryCalls.Contains("GetDiscardPile")&&!recoveryCalls.Contains("GetHand"),"Credit Recovery candidates come from draw and discard only");
Check(recoveryCalls.Contains("Distinct"),"Combined recovery candidates are unique");
Check(bannerData["cards"]["WW_S11"].GetProperty("names").GetProperty("english").GetString()=="Credit Recovery","Credit Recovery name omits redundant X");
var tooltipType=mod.MainModule.Types.Single(t=>t.Name=="AcademicTooltips");
var tooltipFactory=tooltipType.Methods.Single(m=>m.Name=="TryCreate");
var tooltipCalls=tooltipFactory.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(tooltipCalls.Contains("GetOverrideDescriptionKey")&&tooltipCalls.Contains("Localize"),"Custom tooltip reads the localized card effect description");
Check(tooltipCalls.Contains("HasTranslation"),"Missing descriptions do not produce empty tooltips");
Check(tooltipFactory.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="CardEffectLocalizationContext"),"Tooltip resolves dynamic effect values with native localization context");
Check(!mod.MainModule.Types.Any(t=>t.Name=="AcademicCardTooltipHook"),"Spell cards do not receive an extra tooltip duplicating their body");
Check(mod.MainModule.Types.Any(t=>t.Name=="AcademicCharacterTooltipHook"),"Code-driven unit passive tooltips remain available");
foreach(var effectId in new[]{"WW_S26_Return","WW_S20_Effect","WW_U11_Ability_Effect","WW_S08_Recover"})
 Check(bannerData["effects"][effectId].GetProperty("hide_tooltip").GetBoolean(),effectId+": hide internal effect tooltip like the official equivalent");
Check(!bannerData["effects"]["WW_S22_Dazed"].TryGetProperty("status_effect_tooltips_suppressed",out var suppressDazed)||!suppressDazed.GetBoolean(),"Emergency Office Hours keeps native Dazed tooltip");
var tooltipClass=gameTypes["CardTooltipContainer"];
Check(tooltipClass.Methods.Single(m=>m.Name=="AddTooltipsForCardState").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="AddTooltipsCharacter"),"Card tooltip hook covers the native card and unit preview route");
Check(!mod.MainModule.Types.Any(t=>t.Name=="AcademicProject"),"Deferred Project uses native Reserve rather than a custom hand scanner");
var tome=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicTome");
var tomeIL=tome.NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.ToList();
int tomePreview=tomeIL.FindIndex(i=>i.Operand is MethodReference c&&c.Name=="get_PreviewMode");
int tomeSelection=tomeIL.FindIndex(i=>i.Operand is MethodReference c&&c.Name=="ChooseHand");
Check(tomePreview>=0&&tomeSelection>tomePreview&&tomeIL.Skip(tomePreview).Take(tomeSelection-tomePreview).Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ret),"Tome preview cannot open selection or change the real hand");
var examEffect=bannerData["effects"]["WW_S20_Effect"];
Check(examEffect.GetProperty("name").GetString()=="CardEffectDrawType", "Exam Week uses native typed draw across draw and discard");
Check(examEffect.GetProperty("target_card_type").GetString()=="spell"&&examEffect.GetProperty("param_int").GetInt32()==3, "Exam Week draws three spells, not arbitrary cards or a full hand");
Check(!mod.MainModule.Types.Single(t=>t.Name=="SpellRules").Fields.Any(f=>f.Name=="BottomDeckTurn"), "Exam Week no longer redirects end-turn discarded spells to the bottom of draw");
Check(!bannerData["cards"]["WW_S20"].GetProperty("traits").EnumerateArray().Any(t=>t.GetString()=="@WW_Consume"), "Exam Week no longer has Consume");
var deferredReturn=restoreRules.NestedTypes.Single(t=>t.Name.StartsWith("<BeginTurn>")).Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.ToList();
int deferredRestore=deferredReturn.FindIndex(i=>i.Operand is MethodReference call&&call.Name=="Restore");
Check(deferredRestore>=5&&deferredReturn[deferredRestore-5].OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_0, "Deferred spell return does not grant Ephemeral");
var tomeConfirmed=tome.NestedTypes.SelectMany(t=>t.Methods).Single(m=>m.HasBody&&m.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="TryApplyUpgradeToCard"));
Check(tomeConfirmed.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="CardEffectAddTempCardUpgradeToCardsInHand"),"Tome uses native upgrade helper for confirmed card");
var tomeData=bannerData["upgrades"]["WW_S30_Upgrade"];
Check(tomeData.GetProperty("bonus_damage").GetInt32()==10&&tomeData.GetProperty("bonus_heal").GetInt32()==10&&!tomeData.TryGetProperty("cost_reduction",out _),"Tome gives Magic Power without changing spell cost");
Check(tomeData.GetProperty("filters").EnumerateArray().Select(f=>f.GetString()).SequenceEqual(new[]{"OnlyDamageCard","OnlySpells"})&&bannerData["traits"]["WW_S30_Attuned"].GetProperty("stack_mode").GetString()=="none","Tome uses native damage-spell filters and cannot stack existing Attuned");
foreach(var designedCard in bannerCatalog.RootElement.GetProperty("cards").EnumerateArray())
{
    string selectedId=designedCard.GetProperty("id").GetString()!;
    Check(bannerData["cards"][selectedId].GetProperty("rarity").GetString()==designedCard.GetProperty("rarity").GetString(), selectedId+": actual card rarity matches approved design, not the first auxiliary ability");
}
var archiveCandidates=restoreRules.Methods.Single(m=>m.Name=="ArchiveCandidates").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(archiveCandidates.Contains("RecoveryCandidates")&&archiveCandidates.Contains("GetExhaustedPile")&&archiveCandidates.Contains("Distinct"), "Archivist includes draw, discard and consumed piles with unique candidates");
var archiveSelector=restoreRules.NestedTypes.Single(t=>t.Name.StartsWith("<SelectPile>")).Methods.Single(m=>m.Name=="MoveNext");
Check(archiveSelector.DeclaringType.Methods.Any(m=>m.Name.Contains("Finally")&&m.HasBody&&m.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Remove")), "Custom combined selection always cleans up its screen scope");
var reservedTooltip=tooltipType.Methods.Single(m=>m.Name=="Reserved");
var reservedFields=reservedTooltip.Body.Instructions.Select(i=>i.Operand).OfType<FieldReference>().Select(f=>f.Name).ToHashSet();
Check(reservedFields.Contains("Archived")&&!reservedFields.Contains("BoundAttacks")&&dissertationMarker.Methods.Single(m=>m.Name=="Details").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="BoundAttacks"), "Archived spells and bound spell details use separate native tooltip routes without duplication");
Check(reservedTooltip.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetTitle"), "Reserved spell list uses localized card titles");
var appendTooltip=tooltipType.NestedTypes.Single(t=>t.Name.StartsWith("<Append>")).Methods.Single(m=>m.Name=="MoveNext");
Check(appendTooltip.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Reserved"), "Unit hover route includes dynamic reserved-spell tooltip");
var reservedMarker=mod.MainModule.Types.Single(t=>t.Name=="ReservedSpellMarkers").Methods.Single(m=>m.Name=="Marker");
var markerCalls=reservedMarker.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(markerCalls.Contains("AreMainSceneOnlyManagersInitialized"), "Reserved marker protects deck/shop rendering without battle managers");
Check(!markerCalls.Contains("AddUpgrade"), "Reserved marker does not add a gameplay upgrade");
var cemeteryHook=mod.MainModule.Types.Single(t=>t.Name=="ReservedCemeteryViewHook").Methods.Single(m=>m.Name=="Postfix");
var cemeteryCalls=cemeteryHook.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(cemeteryCalls.Contains("Distinct")&&!cemeteryCalls.Contains("GetExhaustedPile"), "Cemetery merges a display list without mutating the consumed pile or duplicating cards");
Check(bannerData["cards"]["WW_U06_Ability"].GetProperty("cooldown").GetInt32()==2,"Shared Damages actual auxiliary ability cooldown is 2");
var projectCharge=bannerData["upgrades"]["WW_S28_Charge"];
Check(projectCharge.GetProperty("bonus_damage").GetInt32()==5&&projectCharge.GetProperty("bonus_heal").GetInt32()==5&&!projectCharge.TryGetProperty("upgrade_will_be_scaled_by_non_magic_power_trait",out _),"Reserve charge adds five Magic Power and allows native Attuned scaling");

var projectSlay=bannerData["card_triggers"]["WW_S28_Slay"];
Check(projectSlay.GetProperty("trigger").GetString()=="on_kill"&&bannerData["cards"]["WW_S28"].GetProperty("triggers").EnumerateArray().Any(x=>x.GetString()=="@WW_S28_Slay"),"Deferred Project returns only from native Slay");
Check(projectSlay.GetProperty("descriptions").EnumerateObject().Count()==10&&projectSlay.GetProperty("descriptions").GetProperty("english").GetString()!.Contains("<b>Unplayable</b> until next turn"),"Deferred Project localizes Freeze and temporary Unplayable");
var frozenReturnType=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicReturnFrozen");
var frozenMark=frozenReturnType.NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext");
Check(frozenMark.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="AddRule")&&!frozenMark.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="DrawSpecificCard"),"Slay marks a single return instead of drawing the casting card before discard");
var frozenUnlock=frozenReturnType.NestedTypes.Single(t=>t.Name.StartsWith("<Unlock>")).Methods.Single(m=>m.Name=="MoveNext");
Check(frozenUnlock.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="RemoveTemporaryTrait"&&c.Parameters[0].ParameterType.Name=="CardTraitState"),"Next-turn unlock removes only the Unplayable instance added by Slay");
Check(frozenReturnType.Methods.Single(m=>m.Name=="ProtectUntilNextTurn").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="NextTurnActions"),"Deferred Project unlock uses next-turn cleanup rather than end-turn cleanup");


var emeritusHeal=mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").Methods.Single(m=>m.Name=="RecordHeal");
Check(!emeritusHeal.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="AddStatusEffect"),"Heal accounting no longer applies hardcoded unit bonuses");
Check(!mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").Fields.Any(f=>f.Name is "Overheal" or "AttackBonuses"),"Emeritus no longer stores healing as pending attack damage");
Check(!bannerData["cards"]["WW_U13"].TryGetProperty("descriptions",out _)&&bannerData["upgrades"]["WW_U13_HarvestUpgrade"].GetProperty("bonus_hp").GetInt32()==5,"Emeritus uses native trigger wording and preserves Harvest health five");
var healFlow=mod.MainModule.Types.Single(t=>t.Name=="HealHook").NestedTypes.Single(t=>t.Name.StartsWith("<Run>")).Methods.Single(m=>m.Name=="MoveNext");
Check(healFlow.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="ExcessHealing"),"Heal hook measures excess before heal triggers and delegates modifiers to native healing");
Check(healFlow.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="OverhealEvent"&&c.Name=="Dispatch"),"Every healed unit can dispatch the reusable Overheal event");

var expulsion=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicPurgePermit");
Check(bannerData["effects"]["WW_E01_Ability_Effect"].GetProperty("param_card_filter").GetString()=="@WW_E01_PurgeableMask","Expulsion Permit references a declared local mask");
var purgeMaskJson=bannerData["upgrade_masks"]["WW_E01_PurgeableMask"];
Check(purgeMaskJson.GetProperty("card_type").GetString()=="invalid","Purge mask has no card type restriction");
Check(purgeMaskJson.GetProperty("excluded_traits").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"CardTraitUnpurgable"})&&Derives(gameTypes["CardTraitUnpurgable"],"CardTraitState"),"Purge mask rejects the actual native Unpurgable trait");
Check(File.ReadAllText(Path.Combine(root,"src/Plugin.cs")).Contains("permitEffect.GetParamCardFilter()!=purgeMask"),"Registration checks effect filter binding instead of accepting a missing mask");
Check(!expulsion.Methods.Concat(expulsion.NestedTypes.SelectMany(t=>t.Methods)).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference c&&c.Name=="GetCardType"),"Expulsion Permit can select cards of every type");
Check(bannerData["cards"]["WW_E01_Ability"].GetProperty("descriptions").GetProperty("english").GetString()=="Once per ring: give a card in hand Purge.","Expulsion Permit text allows only one Purge per ring");


var hostile=mod.MainModule.Types.Single(t=>t.Name=="CardEffectOverhealDamage").NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext");
Check(hostile.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="CardEffectDamage"&&c.Name==".ctor")&&hostile.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="ApplyEffect")&&!hostile.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="FirstQualifyingSpell"),"Overheal damage delegates to the native damage effect without a first-spell quota");
Check(!hostile.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldstr&&(string)i.Operand=="WW_U03"),"Overheal damage can be reused by units without a hardcoded card ID");
Check(bannerData["effects"]["WW_U03_OverhealEffect"].GetProperty("target_mode").GetString()=="room"&&bannerData["effects"]["WW_U03_OverhealEffect"].GetProperty("target_team").GetString()=="heroes", "Hostile Overheal targets every enemy on its own floor through native targeting");
Check(bannerData["character_triggers"]["WW_U03_Overheal"].GetProperty("descriptions").GetProperty("english").GetString()=="Deal the excess healing as damage to all enemies.","Hostile Medicine wording uses the shared Overheal keyword");
var witchBaseTriggers=bannerData["characters"]["WW_C02_Character"].GetProperty("triggers").EnumerateArray().Select(t=>t.GetString()).ToArray();
Check(witchBaseTriggers.Count(t=>t=="@WW_MineMineMine_Claim")==1&&!witchBaseTriggers.Contains("@WW_U03_Overheal"),"Witch champion base has one MineMineMine and no unconditional Overheal");
for(int rank=1;rank<=3;rank++)
{
 Check(upgrades["WW_TenureOfProtection"+rank].GetProperty("trigger_upgrades").EnumerateArray().Count(t=>t.GetString()=="@WW_U03_Overheal")==1,"Protective Tenure alone grants Overheal at rank "+rank);
 foreach(var path in new[]{"QueenBee","PublishOrIncant"})
  Check(!upgrades["WW_"+path+rank].TryGetProperty("trigger_upgrades",out var granted)||!granted.EnumerateArray().Any(t=>t.GetString()=="@WW_U03_Overheal"),path+rank+" does not grant Overheal");
}
var overheal=mod.MainModule.Types.Single(t=>t.Name=="OverhealEvent");
var dispatch=overheal.NestedTypes.Single(t=>t.Name.StartsWith("<Dispatch>")).Methods.Single(m=>m.Name=="MoveNext");
Check(dispatch.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="QueueAndRunTrigger")&&dispatch.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="paramInt"),"Overheal amount travels with each event in the native queue, rather than triggerCount");
var scope=overheal.NestedTypes.Single(t=>t.Name.StartsWith("<Run>")).Methods.Single(m=>m.Name=="MoveNext");
Check(scope.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Push")&&overheal.NestedTypes.Single(t=>t.Name.StartsWith("<Run>")).Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference c&&c.Name=="Pop"),"Overheal execution scopes restore the outer event after nested events or disposal");
Check(overheal.Methods.Single(m=>m.Name=="Amount").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetTrigger"),"Overheal amount is available only to the correct owner and trigger");
Check(mod.MainModule.Types.Single(t=>t.Name=="OverhealEffects").Methods.Single(m=>m.Name=="Copy").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Setup"),"Dynamic effects use a fresh native state and preserve shared effect definitions");
var armorEffect=mod.MainModule.Types.Single(t=>t.Name=="CardEffectOverhealStatus");
Check(armorEffect.NestedTypes.SelectMany(t=>t.Methods).Concat(armorEffect.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="CardEffectAddStatusEffect"&&c.Name==".ctor"),"Overheal Armor delegates application to native AddStatusEffect");
Check(mod.MainModule.Types.SelectMany(t=>t.NestedTypes.SelectMany(n=>n.Methods).Concat(t.Methods)).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="StatusEffectStackData"&&c.Name=="Copy"),"Dynamic status scaling copies each stack rather than mutating the shared template");
Check(bannerData["character_trigger_types"]["WW_Overheal"].GetProperty("names").GetProperty("english").GetString()=="Overheal"&&bannerData["character_trigger_types"]["WW_Overheal"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("missing health"),"Overheal keyword explains healing beyond missing health");
foreach(var id in new[]{"WW_U03","WW_U13"})
{
 Check(bannerData["characters"][id+"_Character"].GetProperty("triggers").EnumerateArray().Any(t=>t.GetString()=="@"+id+"_Overheal"),id+": character owns its native Overheal trigger");
 Check(bannerData["character_triggers"][id+"_Overheal"].GetProperty("trigger").GetString()=="@WW_Overheal",id+": same shared event with independently configured effect");
}

var sharedHeal=mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").NestedTypes.Single(t=>t.Name.StartsWith("<HealShared>")).Methods.Single(m=>m.Name=="MoveNext");
Check(sharedHeal.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="SharedHealing")&&sharedHeal.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="ApplyHeal")&&!sharedHeal.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetHP"),"PhD heals equal native shares without capping to missing health before ApplyHeal");
Check(bannerData["cards"]["WW_U02"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("split evenly"),"PhD wording states equal division instead of front-to-rear overflow");
Check(hostile.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="HostileHealingDepth"),"Hostile Medicine accepts PhD excess healing while preventing its own damage feedback");


var boundFilter=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicDissertation").Methods.Single(m=>m.Name=="EligibleBoundSpell");
var boundCalls=boundFilter.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(boundCalls.Contains("FilterCard")&&boundCalls.Contains("CanBind")&&!boundCalls.Contains("GetIsDamageCard"),"Dissertation keeps native spell mask and delegates ally/enemy eligibility");
Check(bannerData["effects"]["WW_S02_Effect"].GetProperty("param_card_filter").GetString()=="OnlySpells", "Enchanted Dissertation excludes units with native spell mask");
using(var bindingTooltipDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/cards/WW_S02.json")).TrimStart('\uFEFF')))
 Check(bindingTooltipDoc.RootElement.GetProperty("status_effects")[0].GetProperty("character_tooltips").GetProperty("english").GetString()!.Contains("beneficial spells target this unit"),
    "Dissertation state tooltip explains friendly spell targeting without repeating it in the body");


var dissertationTargeting=mod.MainModule.Types.Single(t=>t.Name=="DissertationTargeting");
var dissertationIntent=dissertationTargeting.Methods.Single(m=>m.Name=="Intent");
Check(dissertationIntent.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetStatusEffectDataById")&&dissertationIntent.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetDisplayCategory"),"Dissertation infers statuses from native metadata, not hardcoded status names");
Check(gameTypes["ReplayEntryChooseCardSelection"].BaseType.Name=="ReplayEntryChooseCard"&&gameTypes["DeckScreen"].Methods.Any(m=>m.Name=="FindCardUISelectable"),"Native side options reuse serialized card selection and its DeckScreen replay lookup");
foreach(var option in new[]{"WW_Dissertation_Allies","WW_Dissertation_Enemies"})
 Check(bannerData["cards"][option].GetProperty("pools").GetArrayLength()==0&&bannerData["cards"][option].GetProperty("hide_in_logbook").GetBoolean(),option+": selector-only, hidden and excluded from rewards");
var dissertationOverride=mod.MainModule.Types.Single(t=>t.Name=="DissertationTargetOverride").Methods.Single(m=>m.Name=="Prefix");
Check(dissertationOverride.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetTargetMode")&&dissertationOverride.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetParentCardState"),"Dissertation override is scoped to its own single-target effects");
var boundReplay=mod.MainModule.Types.Single(t=>t.Name=="UnitAttackHook").NestedTypes.Single(t=>t.Name.StartsWith("<Run>" )).Methods.Single(m=>m.Name=="MoveNext");
Check(boundReplay.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="DissertationTargeting"&&c.Name=="Repeat"),"Bound attack routes through owner/enemy-aware replay");
var platonic=mod.MainModule.Types.Single(t=>t.Name=="PlatonicCooldownHook").Methods.Single(m=>m.Name=="Postfix");
Check(platonic.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="SocialRules"&&c.Name=="Mixed")&&!platonic.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="IsTrio"),"Strictly Platonic uses adjacent Wizard-Witch pairs without Chaperone");
Check(bannerData["cards"]["WW_R01"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("Wizard–Witch pairs")&&!bannerData["cards"]["WW_R01"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("Chaperone"),"Strictly Platonic text describes duos rather than trios");


Check(!bannerData["cards"]["WW_E02"].TryGetProperty("descriptions",out _)&&!bannerData["upgrades"]["WW_E02_Upgrade"].TryGetProperty("descriptions",out _),"Faculty Robe relies on its single native Conduit line without duplicate overrides");
Check(projectSlay.GetProperty("descriptions").EnumerateObject().All(t=>!t.Value.GetString()!.Contains("[unplayable]")),"Deferred Project uses explicit localized Unplayable text instead of an unresolved token");

var projectReserve=bannerData["card_triggers"]["WW_S28_Reserve"];
Check(projectReserve.GetProperty("trigger").GetString()=="on_unplayed_positive"&&projectReserve.GetProperty("effects")[0].GetString()=="@WW_S28_ReserveEffect","Deferred Project uses native Reserve trigger");
var reserveEffect=bannerData["effects"]["WW_S28_ReserveEffect"];
Check(reserveEffect.GetProperty("name").GetProperty("id").GetString()=="@CardEffectAddCardUpgradeToTargetCards"&&reserveEffect.GetProperty("name").GetProperty("mod_reference").GetString()=="Conductor"&&reserveEffect.GetProperty("target_mode").GetProperty("id").GetString()=="@played_card"&&!reserveEffect.GetProperty("param_bool").GetBoolean(),"Reserve upgrades only its triggering card using Conductor");
Check(projectReserve.GetProperty("descriptions").GetProperty("english").GetString()=="+5 [magicpower]."&&!bannerData["cards"]["WW_S28"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("Reserve"),"Reserve keyword and fixed five Magic Power are presented by the native trigger");
foreach(var damageId in new[]{"WW_S28_Effect","WW_S08_Damage","WW_S14_Damage"})
    Check(bannerData["effects"][damageId].GetProperty("target_team").GetString()=="both",damageId+" damages either friendly or enemy units");
Check(bannerData["effects"]["WW_S14_Heal"].GetProperty("name").GetString()=="CardEffectHeal"&&bannerData["effects"]["WW_S14_Heal"].GetProperty("target_team").GetString()=="monsters"&&bannerData["effects"]["WW_S14_Heal"].GetProperty("target_mode").GetString()=="room"&&bannerData["effects"]["WW_S14_Heal"].GetProperty("param_int").GetInt32()==4, "Practicum restores four health only to friendly units on its floor with native healing");
foreach(var starterId in new[]{"WW_T01","WW_T02"})
{
    var starter=bannerData["cards"][starterId];
    Check(starter.GetProperty("rarity").GetString()=="common",starterId+": starter uses native common rarity");
    Check(starter.GetProperty("pools").EnumerateArray().Select(p=>p.GetString()).SequenceEqual(new[]{"StarterCardsOnly"}), starterId+": starter belongs exclusively to StarterCardsOnly");
}
foreach(var bannerId in poolMembers)
    Check(bannerData["cards"][bannerId].GetProperty("pools").EnumerateArray().Select(p=>p.GetString()).SequenceEqual(new[]{"UnitsAllBanner"}),bannerId+": banner excluded from normal rewards");
var areaPredicate=mod.MainModule.Types.Single(t=>t.Name=="SpellRules").Methods.Single(m=>m.Name=="IsAreaEffect");
Check(areaPredicate.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="HasFlag"),"Shared Damages accepts enemy-capable Both team flags");
foreach(var hookName in new[]{"EligibleRedirectHook","RedirectTargetHook"})
    Check(mod.MainModule.Types.Single(t=>t.Name==hookName).Methods.SelectMany(m=>m.HasBody?m.Body.Instructions.AsEnumerable():Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Any(i=>i.Operand is MethodReference c&&c.Name=="IsAreaEffect"),hookName+": shared area predicate controls targeting");
var beforeIterator=mod.MainModule.Types.Single(t=>t.Name=="SpellRules").NestedTypes.Single(t=>t.Name.StartsWith("<Before>")).Methods.Single(m=>m.Name=="MoveNext");
Check(beforeIterator.Body.Instructions.Count(i=>i.Operand is MethodReference c&&c.Name=="TargetsEnemy")==2,"Both cross-floor and rear-enemy repetitions require an actual enemy target");
Check(bannerData["cards"]["WW_S22"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("[effect1.status0.power]"),"Emergency Office Hours displays upgraded Dazed stacks");
Check(mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").NestedTypes.Single(t=>t.Name.StartsWith("<ChooseHand>")).Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.Any(i=>i.Operand is string text&&text=="david.wwinc.ChooseCard"),"Hand choices do not falsely announce Freeze");
Check(mod.MainModule.Types.Single(t=>t.Name=="SpellRules").Methods.Single(m=>m.Name=="SelectPile").Parameters.Last().Constant as string=="david.wwinc.ChooseSpell","Pile choices retain generic localized default while allowing a specific instruction");
var inspectorPierce=bannerData["characters"]["WW_U16_Character"].GetProperty("starting_status_effects")[0];
Check(inspectorPierce.GetProperty("count").GetInt32()==1&&inspectorPierce.GetProperty("status").GetProperty("id").GetString()=="@pierce"&&inspectorPierce.GetProperty("status").GetProperty("mod_reference").GetString()=="Conductor","Inspector starts with Conductor Pierce 1, not vanilla Piercing");
var pierceSelector=conductor.MainModule.Types.Single(t=>t.Name=="NUnits").Methods.Single(m=>m.Name=="FilterTargets");
Check(pierceSelector.Body.Instructions.Any(i=>i.Operand is string status&&status=="conductor_pierce")&&pierceSelector.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Add),"Installed Pierce target selector counts additional targets beyond the front enemy");
// Visible upgrade icons must have translated titles and bodies, including room-applied upgrades.
foreach(var path in Directory.GetFiles(Path.Combine(root,"content/json"),"*.json",SearchOption.AllDirectories))
{
    using var doc=JsonDocument.Parse(File.ReadAllText(path).TrimStart('\uFEFF'));
    if(!doc.RootElement.TryGetProperty("upgrades",out var visibleUpgradeRows))continue;
    foreach(var upgrade in visibleUpgradeRows.EnumerateArray())
    {
        if(!upgrade.TryGetProperty("icon",out _) || (upgrade.TryGetProperty("hide_icon_on_card",out var hidden)&&hidden.GetBoolean()))continue;
        foreach(var field in new[]{"titles","descriptions"})
            Check(upgrade.TryGetProperty(field,out var terms)&&new[]{"english","spanish","french","german","portuguese","russian","chinese","chinese_traditional","korean","japanese"}.All(lang=>terms.TryGetProperty(lang,out var term)&&!string.IsNullOrWhiteSpace(term.GetString())),upgrade.GetProperty("id").GetString()+": visible upgrade requires localized "+field);
    }
}
var postdocCopy=restoreRules.Methods.Single(m=>m.Name=="CopyEphemeral");
var postdocCalls=postdocCopy.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(postdocCalls.Any(m=>m.Name=="AddNewCardCopy")&&!postdocCalls.Any(m=>m.Name is "RestoreExhaustedOrEatenCard" or "DrawSpecificCard" or "Remove"),"Postdoc creates a new card without moving the original between piles");
Check(postdocCopy.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="copyModifiersFromCard"),"Postdoc copy inherits original upgrades through native copy modifiers");
var deathRun=mod.MainModule.Types.Single(t=>t.Name=="DeathHook").NestedTypes.Single(t=>t.Name.StartsWith("<Run>")).Methods.Single(m=>m.Name=="MoveNext");
var postdocEffectRun=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicCopyLastCommon").NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext");
Check(postdocEffectRun.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CopyEphemeral")&&!deathRun.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CopyEphemeral"),"Postdoc copies via its native trigger effect, without duplicate removal-hook activation");
Check(postdocCopy.Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.Name=="GrantConfiguredTraits")&&postdocCopy.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.Name=="SetFixedCost").Previous.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_0,"Postdoc generated copy receives Ephemeral and a fixed cost that does not expire at turn end");
var roomEligibility=mod.MainModule.Types.Single(t=>t.Name=="RoomPowerEligibility");
var affectedMethod=roomEligibility.Methods.Single(m=>m.Name=="Affected");
var affectedCalls=affectedMethod.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(affectedCalls.Contains("CanScaleWithMagicPower")&&affectedCalls.Contains("GetEffectStates")&&affectedCalls.Contains("GetTriggers"),"Room power checks actual effects including triggers, not spell type alone");
var powerEffectTypes=roomEligibility.NestedTypes.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Where(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Isinst).Select(i=>(i.Operand as TypeReference)?.Name).ToHashSet();
Check(powerEffectTypes.SetEquals(new[]{"CardEffectDamage","CardEffectHeal","CardEffectNULL"}),"Room power accepts native damage/heal scaling classes and excludes typed draw");
Check(mod.MainModule.Types.Single(t=>t.Name=="RoomStateAcademicPower").Methods.Single(m=>m.Name=="ApplyRoomUpgrade").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Affected"),"Room upgrade attachment uses the actual scaling predicate");
foreach(var hook in new[]{"PairPowerIconHook","PairPowerTooltipHook"})
    Check(mod.MainModule.Types.Single(t=>t.Name==hook).Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Affected"),hook+": inherited upgrades on unaffected spells do not produce icons or tooltips");
using(var dissertationDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/cards/WW_S13.json")).TrimStart('\uFEFF')))
{
    var data=dissertationDoc.RootElement;
    var slay=data.GetProperty("card_triggers")[0];
    Check(slay.GetProperty("trigger").GetString()=="on_kill"&&!slay.TryGetProperty("trigger_keyword_card_text_suppressed",out _),"Dissertation uses native visible Slay");
    var buff=slay.GetProperty("trigger_effects")[0];
    Check(buff.GetProperty("trigger_effect").GetString()=="CardTriggerEffectBuffSpellDamage"&&buff.GetProperty("persistence_mode").GetString()=="single_run"&&buff.GetProperty("param_int").GetInt32()==2,"Dissertation gains native permanent +2 only on kill");
    Check(data.GetProperty("effects").GetArrayLength()==1&&data.GetProperty("effects")[0].GetProperty("param_int").GetInt32()==4&&!data.TryGetProperty("upgrades",out _),"Dissertation deals 4 and removes unconditional battle upgrade");
    Check(data.GetProperty("cards")[0].GetProperty("triggers")[0].GetString()=="@WW_S13_Slay"&&slay.GetProperty("descriptions").EnumerateObject().Count()==10,"Dissertation links permanent trigger with ten-language text");
}
using(var insuranceDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/cards/WW_S25.json")).TrimStart('\uFEFF')))
{
    var heal=insuranceDoc.RootElement.GetProperty("effects")[0];
    Check(heal.GetProperty("name").GetString()=="CardEffectHeal"&&heal.GetProperty("param_int").GetInt32()==10&&heal.GetProperty("target_mode").GetString()=="drop_target_character"&&heal.GetProperty("target_team").GetString()=="monsters","Insurance restores 10 to the selected ally through native healing");
    Check(insuranceDoc.RootElement.GetProperty("cards")[0].GetProperty("effects")[0].GetString()=="@WW_S25_Heal","Insurance restores health before applying temporary coverage");
    var marker=insuranceDoc.RootElement.GetProperty("status_effects")[0];
    Check(marker.GetProperty("id").GetString()=="insured"&&!marker.GetProperty("hidden").GetBoolean()&&marker.GetProperty("icon").GetString()=="@WW_InsuranceIcon","Insurance has a visible status marker icon");
    Check(marker.GetProperty("trigger_stage").GetString()=="none"&&!marker.GetProperty("is_propagatable").GetBoolean()&&!marker.GetProperty("is_stackable").GetBoolean(),"Insurance marker does not add combat or propagation behavior");
    foreach(var field in new[]{"names","card_tooltips","character_tooltips"})
        Check(marker.GetProperty(field).EnumerateObject().Count()==10,"Insurance marker has ten-language "+field);
    var insuranceUpgrade=insuranceDoc.RootElement.GetProperty("upgrades").EnumerateArray().Single(u=>u.GetProperty("id").GetString()=="WW_Retribution");
    Check(insuranceUpgrade.GetProperty("status_effect_upgrades")[0].GetProperty("status").GetString()=="@insured","Marker belongs to the same temporary upgrade as Retribution");
}
Check(Derives(mod.MainModule.Types.Single(t=>t.Name=="StatusEffectAcademicInsurance"),"StatusEffectState"),"Insurance marker type derives from native status state");
var nativeRemoval=character.NestedTypes.Single(t=>t.Name.StartsWith("<RemoveCardUpgrade>")).Methods.Single(m=>m.Name=="MoveNext");
Check(nativeRemoval.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetStatusEffectUpgrades")&&nativeRemoval.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="RemoveStatusEffect"),"Native removal expires attached status marker with the temporary upgrade");
using(var pathsDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/04_champion_paths.json")).TrimStart('\uFEFF')))
{
    foreach(var prefix in new[]{"WW_FratBro","WW_Explosive","WW_Attuned"})
    for(int rank=1;rank<=3;rank++)
    {
        var upgrade=pathsDoc.RootElement.GetProperty("upgrades").EnumerateArray().Single(u=>u.GetProperty("id").GetString()==prefix+rank);
        var conduit=upgrade.GetProperty("status_effect_upgrades")[0];
        Check(conduit.GetProperty("status").GetString()=="conduit"&&conduit.GetProperty("count").GetInt32()==rank*5,prefix+rank+": Dean has native fixed Conduit at every rank");
    }
}
var conversionRun=mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").NestedTypes.Single(t=>t.Name.StartsWith("<AfterSpell>")).Methods.Single(m=>m.Name=="MoveNext");
var conversionCalls=conversionRun.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();

var arcaneType=bannerData["character_trigger_types"]["WW_ArcaneSlay"];
Check(arcaneType.GetProperty("names").GetProperty("english").GetString()=="Arcane Slay"&&arcaneType.GetProperty("descriptions").GetProperty("english").GetString()!.Contains("spell kills an enemy"),"Arcane Slay has reusable name and tooltip describing spell kills on its floor");
Check(bannerData["character_triggers"]["WW_U05_ArcaneSlay"].GetProperty("trigger").GetString()=="@WW_ArcaneSlay"&&!bannerData["cards"]["WW_U05"].TryGetProperty("descriptions",out _),"Explosive Lab Assistant uses native trigger text without duplicate override");
Check(bannerData["effects"]["WW_U05_ArcaneSlayEffect"].GetProperty("name").GetString()=="CardEffectAddStatusEffect"&&bannerData["effects"]["WW_U05_ArcaneSlayEffect"].GetProperty("param_status_effects")[0].GetProperty("status").GetString()=="burst"&&bannerData["effects"]["WW_U05_ArcaneSlayEffect"].GetProperty("param_status_effects")[0].GetProperty("count").GetInt32()==1,"Arcane Slay effect is native Burst one rather than a bespoke coded reward");
Check(conversionCalls.Contains("Dispatch")&&!conversionRun.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldstr&&(string)i.Operand=="WW_U05"),"Spell kills dispatch the reusable trigger through native room queue without U05 ID checks");

Check(conversionCalls.Contains("HealShared")&&!conversionCalls.Contains("HealOverflow")&&!conversionCalls.Contains("DamageOverflow"),"First-spell damage-to-healing passive remains; Hostile Medicine no longer uses trample conversion");
foreach(var id in new[]{"WW_U02"})
using(var medicineDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/cards",id+".json")).TrimStart('\uFEFF')))
    Check(medicineDoc.RootElement.GetProperty("cards").EnumerateArray().All(c=>c.GetProperty("descriptions").GetProperty("english").GetString()!.Contains("also")),id+": unit and ability texts explicitly say also");
var deskRun=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicDeskSwap").NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext");
var deskInstructions=deskRun.Body.Instructions;
Check(deskInstructions.Any(i=>i.Operand is MethodReference c&&c.Name=="RunUnitTurn"),"Desk Swap executes the native immediate unit turn");
Check(!deskInstructions.Any(i=>i.Operand is string s&&s=="armor")&&!deskInstructions.Any(i=>i.Operand is MethodReference c&&c.Name=="RemoveStatusEffect"),"Desk Swap leaves Armor untouched");
Check(!deskInstructions.Any(i=>i.Operand is string s&&s=="dazed"),"Desk Swap grants no Dazed");
var resolutionPowerMethod=mod.MainModule.Types.Single(t=>t.Name=="SpellRules").Methods.Single(m=>m.Name=="ResolutionPower");
var powerInstructions=resolutionPowerMethod.Body.Instructions;
var modifyPowerIndex=powerInstructions.Select((instruction,index)=>(instruction,index)).First(x=>x.instruction.Operand is MethodReference call&&call.Name=="ModifyCard").index;
Check(powerInstructions.Take(modifyPowerIndex).Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ret),"Zero resolution bonus exits before attaching any card modifier or cleanup");
Check(powerInstructions.Take(modifyPowerIndex).Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_0)&&powerInstructions.Take(modifyPowerIndex).Any(i=>i.OpCode.FlowControl==Mono.Cecil.Cil.FlowControl.Cond_Branch),"Resolution power guards nonpositive bonuses before native card mutation");
var consumedTrait=mod.MainModule.Types.Single(t=>t.Name=="CardTraitAcademicConsumedDamage");
Check(Derives(consumedTrait,"CardTraitState")&&consumedTrait.Methods.Single(m=>m.Name=="OnApplyingDamage").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="damage"),"Consumed trait preserves incoming upgraded damage");
Check(consumedTrait.Methods.Single(m=>m.Name=="OnApplyingDamage").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetStatValue"),"Consumed damage uses the native consumed-pile statistic");
using(var bibliographyDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/cards/WW_S08.json")).TrimStart('\uFEFF')))
    Check(bibliographyDoc.RootElement.GetProperty("traits")[0].GetProperty("name").GetString()=="@CardTraitAcademicConsumedDamage"&&bibliographyDoc.RootElement.GetProperty("effects")[0].GetProperty("param_int").GetInt32()==10,"Bibliography retains ten base damage and references additive trait");
foreach(var redirectHookName in new[]{"EligibleRedirectHook","RedirectTargetHook","RedirectUiHook","RedirectTeamValidationHook","SpellResolutionHook"})
{
    var hookType=mod.MainModule.Types.Single(t=>t.Name==redirectHookName);
    var hookCalls=hookType.Methods.Concat(hookType.NestedTypes.SelectMany(t=>t.Methods)).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
    Check(hookCalls.Any(c=>c.Name=="HasFlag"||c.DeclaringType.Name=="LightningRodSafety"&&c.Name=="Eligible"),redirectHookName+": enemy capability uses shared safe predicate or native flags");
}
using(var redirectBibliographyDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/cards/WW_S08.json")).TrimStart('\uFEFF')))
    Check(redirectBibliographyDoc.RootElement.GetProperty("effects")[0].GetProperty("target_team").GetString()=="both","Bibliography keeps direct ally targeting while also supporting Intern redirection");
Check(mod.MainModule.Types.Single(t=>t.Name=="EligibleRedirectHook").NestedTypes.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference c&&c.Name=="Contains"),"Both targeting does not add a second copy of the Intern target");
Check(bannerData["cards"]["WW_S31"].GetProperty("cost").GetInt32()==1&&bannerData["cards"]["WW_S31"].GetProperty("rarity").GetString()=="common","New direct damage spell is common and costs one Ember");
Check(bannerData["effects"]["WW_S31_Damage"].GetProperty("param_int").GetInt32()==8&&bannerData["effects"]["WW_S31_Damage"].GetProperty("target_team").GetString()=="heroes"&&bannerData["effects"]["WW_S31_Damage"].GetProperty("target_mode").GetString()=="room","General Examination deals eight to all enemy units on its floor");
Check(bannerData["cards"]["WW_E02"].GetProperty("rarity").GetString()=="common"&&!bannerData["effects"]["WW_E02_Equip"].TryGetProperty("target_subtype",out _)&&bannerData["upgrades"]["WW_E02_Upgrade"].GetProperty("status_effect_upgrades")[0].GetProperty("status").GetString()=="conduit"&&bannerData["upgrades"]["WW_E02_Upgrade"].GetProperty("status_effect_upgrades")[0].GetProperty("count").GetInt32()==5,"Common Faculty Robe has no subtype restriction and grants native Conduit five");
Check(bannerData["characters"]["WW_U08_Character"].GetProperty("starting_status_effects").EnumerateArray().Any(s=>s.GetProperty("status").GetString()=="undying"&&s.GetProperty("count").GetInt32()==1),"Postdoc starts with Undying one");
Check(bannerData["cards"]["WW_U11_Ability"].GetProperty("cooldown").GetInt32()==3&&bannerData["effects"]["WW_U11_Ability_Effect"].GetProperty("name").GetString()=="CardEffectRecursion"&&bannerData["effects"]["WW_U11_Ability_Effect"].GetProperty("target_mode").GetString()=="exhaust","Archivist recovers a consumed spell through native selection with cooldown three");
Check(!bannerData["characters"]["WW_U04_Character"].TryGetProperty("ability",out _)&&!bannerData["cards"].ContainsKey("WW_U04_Ability"),"Education is passive and has no active ability card");
Check(mod.MainModule.Types.Single(t=>t.Name=="SpellRules").NestedTypes.Single(t=>t.Name.StartsWith("<Before>")).Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="EducationPowerOnFloor"),"First-spell pipeline applies the Education passive");
Check(!mod.MainModule.Types.Single(t=>t.Name=="SocialRules").NestedTypes.Single(t=>t.Name.StartsWith("<BeginTurn>")).Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.Any(i=>i.Operand is string s&&s=="WW_E02"),"Replacement equipment cannot retain old shrink behavior");
foreach(var id in new[]{"WW_U02","WW_U03"})
{
    Check(!bannerData["characters"][id+"_Character"].TryGetProperty("ability",out _)&&!bannerData["cards"].ContainsKey(id+"_Ability"),id+": medicine passive has no ability or cooldown");
    if(id=="WW_U02")Check(bannerData["cards"][id].GetProperty("descriptions").GetProperty("english").GetString()!.StartsWith("The first spell each turn"),id+": wording identifies the first qualifying spell");
}
Check(conversionCalls.Contains("FirstQualifyingSpell"),"Post-spell medicine uses the tested first-positive predicate");
Check(reserveEffect.GetProperty("param_upgrade").GetString()=="@WW_S28_Charge","Reserve uses a raw damage-and-healing upgrade, independent of spell damage stones");
Check(bannerData["cards"]["WW_S05"].GetProperty("pools").GetArrayLength()==0&&bannerData["cards"]["WW_S05"].GetProperty("hide_in_logbook").GetBoolean(),"Consume and Re-enroll is retired from rewards and logbook");
Check(bannerData["cards"]["WW_S32"].GetProperty("cost").GetInt32()==1&&bannerData["cards"]["WW_S32"].GetProperty("rarity").GetString()=="common", "Pair Assignment is common and costs one Ember");
Check(bannerData["effects"]["WW_S32_Damage"].GetProperty("param_int").GetInt32()==5&&bannerData["effects"]["WW_S32_Damage"].GetProperty("target_team").GetString()=="heroes", "Pair Assignment deals five damage to an enemy");
Check(bannerData["cards"]["WW_S24"].GetProperty("pools").GetArrayLength()==0&&bannerData["cards"]["WW_S24"].GetProperty("hide_in_logbook").GetBoolean(), "Loaned Equipment Return is retired");
Check(!bannerData["cards"]["WW_U11"].TryGetProperty("descriptions",out _), "Archivist unit does not repeat its active ability as passive text");
Check(bannerData["cards"]["WW_U11_Ability"].GetProperty("names").GetProperty("english").GetString()=="Spell Recovery", "Archivist ability has a specific recovery title");
var activeAbilityTitles=bannerData["cards"].Values.Where(c=>c.TryGetProperty("is_an_ability",out var active)&&active.GetBoolean()).Select(c=>c.GetProperty("names").GetProperty("english").GetString()!).ToArray();
Check(!activeAbilityTitles.Contains("Magic Administration")&&activeAbilityTitles.Distinct().Count()==activeAbilityTitles.Length, "Each active ability has a distinct specific title");
var pairPreview=mod.MainModule.Types.Single(t=>t.Name=="PairAssignmentPreview");
var pairPreviewCalls=pairPreview.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().Select(c=>c.Name).ToArray();
Check(!pairPreviewCalls.Any(n=>n.Contains("AddTemporary")||n.Contains("RemoveTemporary")||n=="AddUpgrade"||n=="ReapplyMagicPowerScalingFromTraitsToAllExistingUpgrades"), "Prospective Pair Assignment text cannot mutate card traits or upgrades");
Check(pairPreviewCalls.Contains("GetHand")&&pairPreviewCalls.Contains("IsPlayerActionPhase")&&pairPreviewCalls.Contains("GetSelectedRoom")&&pairPreviewCalls.Contains("Mixed"), "Prospective keywords are scoped to a hand card and the selected floor's mixed pair");
Check(pairPreviewCalls.Contains("HasTrait"), "Preview omits keywords already held by the card");
Check(mod.MainModule.Types.Single(t=>t.Name=="PairAssignmentRefreshHook").Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference c&&c.Name=="UpdateTextContent"), "Pair Assignment refreshes UI when prospective keywords change");
var chooseHandState=mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").NestedTypes.Single(t=>t.Name.StartsWith("<ChooseHand>"));
var chooseInstructions=chooseHandState.Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.ToList();
int selectHandIndex=chooseInstructions.FindIndex(i=>i.Operand is MethodReference call&&call.Name=="SelectCardFromHand");
Check(!chooseInstructions.Any(i=>i.Operand is MethodReference call&&call.Name=="AddSelectCardFromHandOnCompletedCallback")&&!chooseInstructions.Any(i=>i.Operand is FieldReference f&&f.Name=="completed"),"Shared selector does not keep card-play resolution blocked while awaiting hand input");
Check(selectHandIndex>0&&chooseInstructions[selectHandIndex-1].OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_0,"Hand selection disables native last-card auto-pick that bypasses filters");
var applyChoice=mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").NestedTypes.Single(t=>t.Name.StartsWith("<ApplyChoice>")).Methods.Single(m=>m.Name=="MoveNext");
Check(applyChoice.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Invoke")&&applyChoice.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Contains"),"Confirmed callback verifies hand membership before running selected-card resolution");
var withdrawalState=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicDiscardForEmber").NestedTypes.Single(t=>t.Name.StartsWith("<ResolveWithdrawal>"));
var withdrawalCalls=withdrawalState.Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(c=>c.Name).ToList();
Check(withdrawalCalls.IndexOf("Cost")>=0&&withdrawalCalls.IndexOf("Cost")<withdrawalCalls.IndexOf("DiscardCard")&&withdrawalCalls.IndexOf("DiscardCard")<withdrawalCalls.IndexOf("AddEnergy"),"Course Withdrawal confirmation snapshots current cost, discards, then grants Ember");
Check(withdrawalCalls.LastIndexOf("Contains")>withdrawalCalls.IndexOf("DiscardCard")&&withdrawalCalls.LastIndexOf("Contains")<withdrawalCalls.IndexOf("AddEnergy"),"Ember is conditional on the selected card leaving the hand");
var pairKeywords=mod.MainModule.Types.Single(t=>t.Name=="SpellRules").Methods.Single(m=>m.Name=="ApplyPairKeywords");
var pairKeywordCalls=pairKeywords.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(c=>c.Name).ToArray();
Check(!pairKeywordCalls.Contains("get_Simulating")&&pairKeywordCalls.Contains("Add"), "Pair Assignment schedules cleanup during real casting as well as previews");
Check(pairKeywordCalls.Contains("Except")&&pairKeywords.Body.ExceptionHandlers.Any(h=>h.HandlerType==Mono.Cecil.Cil.ExceptionHandlerType.Finally), "Pair Assignment tracks only newly added trait instances, also when preparation fails");
Check(bannerData["cards"]["WW_S32"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("for this cast"), "Pair Assignment text states exceptional cast-only duration");
Check(postdocEffectRun.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="LastPlayedCommonSpell"),"Postdoc Extinguish reads the global last common spell");
var lastSpellRun=mod.MainModule.Types.Single(t=>t.Name=="SpellResolutionHook").NestedTypes.Single(t=>t.Name.StartsWith("<Run>")).Methods.Single(m=>m.Name=="MoveNext");
var lastSpellInstructions=lastSpellRun.Body.Instructions.ToList();
int lastSpellWrite=lastSpellInstructions.FindIndex(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Stsfld&&i.Operand is FieldReference f&&f.Name=="LastPlayedCommonSpell");
int spellAfter=lastSpellInstructions.FindIndex(i=>i.Operand is MethodReference call&&call.Name=="AfterSpell");
Check(lastSpellWrite>=0&&lastSpellWrite<spellAfter,"Global last common spell is set before completion, so spell-caused deaths can copy the current spell");
Check(!bannerData["character_triggers"]["WW_U08_Extinguish"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("on this floor"),"Postdoc text has no floor restriction");
int commonRarityRead=lastSpellInstructions.FindIndex(i=>i.Operand is MethodReference call&&call.Name=="GetRarity");
Check(commonRarityRead>=0&&commonRarityRead<lastSpellWrite&&lastSpellInstructions.Skip(commonRarityRead+1).Take(lastSpellWrite-commonRarityRead-1).Any(i=>i.OpCode.FlowControl==Mono.Cecil.Cil.FlowControl.Cond_Branch),"Postdoc last-spell assignment is guarded by native rarity before writing the record");
Check(bannerData["character_triggers"]["WW_U08_Extinguish"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("last common spell played"),"Postdoc text specifies the last common spell");
Check(bannerData["characters"]["WW_U08_Character"].GetProperty("triggers").EnumerateArray().Any(t=>t.GetString()=="@WW_U08_Extinguish")&&bannerData["character_triggers"]["WW_U08_Extinguish"].GetProperty("trigger").GetString()=="on_death","Postdoc registers the native Extinguish trigger");
var nativeRevive=character.NestedTypes.Single(t=>t.Name.StartsWith("<ReviveFromUndyingStatus>")).Methods.Single(m=>m.Name=="MoveNext");
Check(nativeRevive.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="QueueTrigger"),"Native Undying revival queues character triggers independently of removal");
Check(!bannerData["cards"]["WW_U08"].TryGetProperty("descriptions",out _),"Postdoc leaves Extinguish label and effect composition to native UI");
// Bound originals must leave standby only after the final spell has resolved.
var releaseBound=mod.MainModule.Types.Single(t=>t.Name=="SpellRules").NestedTypes.Single(t=>t.Name.StartsWith("<ReleaseBoundSpell>"));
var releaseCalls=releaseBound.Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
Check(releaseCalls.Any(c=>c.DeclaringType.Name=="CardManager"&&c.Name=="ChangeStandbyCondition")&&releaseCalls.Any(c=>c.DeclaringType.Name=="RemoveFromStandByCondition"&&c.Name==".ctor"),"Bound spell uses native standby return instead of manually modifying card piles");
Check(releaseCalls.Any(c=>c.Name=="get_Simulating")&&releaseCalls.Count(c=>c.Name=="IsCardInStandByPile")==2,"Bound return excludes preview and verifies native return before clearing reservation");
Check(!releaseCalls.Any(c=>c.Name is "GetDiscardPile" or "SetFixedCost" or "Trait" or "Restore"),"Bound original preserves cost, upgrades and traits and does not manually add a duplicate");
Check(gameTypes["CardManager"].Methods.Single(m=>m.Name=="CheckStandByConditions").Parameters.Select(p=>p.ParameterType.Name).SequenceEqual(new[]{"CharacterState","CardState"}),"Native standby return bridge matches actual game signature");
var boundSequence=attackCallback.NestedTypes.Single(t=>t.Name.StartsWith("<Run>"));
Check(boundSequence.Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="ReleaseBoundSpell"),"Bound spells are returned after the complete native attack sequence");
foreach(var name in new[]{"DeathHook","UnitAttackHook"})
 Check(mod.MainModule.Types.Single(t=>t.Name==name).NestedTypes.Where(t=>t.Name.StartsWith("<Run>")).SelectMany(t=>t.Methods.Where(m=>m.HasBody)).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference c&&c.Name=="ReleaseBoundSpell"),$"{name} returns bound spells when the owner dies or an alternate attack completes");
Check(bannerData["cards"]["WW_S02"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("discard pile when the effect ends"),"Dissertation states the original spell returns to discard");
var slayingEffect=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicUpgradeSlayingSpell");
var slayingIL=slayingEffect.NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.ToList();
int permanentCall=slayingIL.FindIndex(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="CardUpgradeHelper"&&c.Name=="TryApplyUpgradeToCard");
Check(permanentCall>0&&slayingIL.Take(permanentCall).Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_0),"Slaying spell upgrade uses native helper with permanent lifetime");
Check(slayingIL.Any(i=>i.Operand is MethodReference c&&c.Name=="get_PreviewMode")&&slayingIL.Any(i=>i.Operand is MethodReference c&&c.Name=="get_Simulating"),"Permanent slaying upgrade cannot mutate a preview");
var nativePermanent=gameTypes["CardUpgradeHelper"].Methods.Single(m=>m.Name=="ApplyUpgradeToCardImpl");
Check(nativePermanent.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="ApplyPermanentUpgrade"),"Native upgrade helper persists card improvements through SaveManager");
var slayDispatch=mod.MainModule.Types.Single(t=>t.Name=="ArcaneSlayEvent").NestedTypes.Single(t=>t.Name.StartsWith("<Dispatch>"));
Check(slayDispatch.Methods.Single(m=>m.Name=="MoveNext").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="ApplyCharacterEffectsForRoom"),"Arcane Slay retains the native room queue and one trigger per kill");
var slayTest=slayingEffect.Methods.Single(m=>m.Name=="TestEffect");
Check(!slayTest.Body.Instructions.Any(i=>i.Operand is MethodReference)&&slayTest.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_1),"Slaying effect accepts native prevalidation without sourceCharacterTriggerState");
var slayData=mod.MainModule.Types.Single(t=>t.Name=="ArcaneSlayEvent").NestedTypes.Single(t=>t.Name=="Data");
Check(slayData.BaseType.Name=="FireTriggersData"&&slayData.Fields.Single(f=>f.Name=="Source").FieldType.Name=="CardState","Responsible spell travels in the queued native event, not a transient dispatch scope");
var slayRun=mod.MainModule.Types.Single(t=>t.Name=="ArcaneSlayEvent").NestedTypes.Single(t=>t.Name.StartsWith("<Run>"));
Check(slayRun.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions:Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Any(i=>i.Operand is MethodReference c&&c.Name=="Pop"),"Executing Arcane Slay source is restored even on exception");
Check(bannerData["effects"]["WW_U18_Ability_Effect"].GetProperty("name").GetString()=="CardEffectFreezeCard"&&bannerData["cards"]["WW_U18_Ability"].GetProperty("cooldown").GetInt32()==2,"Frozen Tuition reuses native Cuttlehex Freeze with cooldown two");
Check(bannerData["cards"]["WW_U18_Ability"].GetProperty("descriptions").GetProperty("english").GetString()=="Apply [freeze] to a card in hand."&&bannerData["cards"]["WW_U18"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("all [freeze] cards in hand"),"Frozen Tuition separates its activated Freeze from its independent damage passive");
Check(!mod.MainModule.Types.Any(t=>t.Name=="CardEffectAcademicFrozenTuition")&&!mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").Fields.Any(f=>f.Name=="FrozenBindings"),"Tuition needs no prior ability activation or single-card binding");
var nextUse=mod.MainModule.Types.Single(t=>t.Name=="CardTraitAcademicNextUseUpgrade");
Check(nextUse.Methods.Any(m=>m.Name=="OnCardPlayed")&&!nextUse.Methods.Any(m=>m.Name is "OnCardDiscarded" or "OnTraitStateReset"),"Requisition expires on play, not unplayed discard or turn boundary");
var nextUseRun=nextUse.NestedTypes.Single(t=>t.Name.StartsWith("<OnCardPlayed>")).Methods.Single(m=>m.Name=="MoveNext");
Check(nextUseRun.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="CardState"&&c.Name=="RemoveUpgrade")&&!nextUseRun.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="RemoveTrait"),"Requisition removes its own upgrade via native bookkeeping, never all Explosive traits");
Check(deanApply.Any(i=>i.Operand is MethodReference c&&c.Name=="SetAvoidClobberingExistingTraits"),"Requisition preserves Explosive which existed before the draw");
Check(bannerData["traits"]["WW_C01_NextUseExpiry"].GetProperty("tooltip_suppressed").GetBoolean()&&bannerData["traits"]["WW_C01_NextUseExpiry"].GetProperty("effect_text_suppressed").GetBoolean(),"Expiry marker adds no redundant keyword or tooltip");
var educationText=File.ReadAllText(Path.Combine(root,"src/EducationPreview.cs"));
Check(educationText.Contains("AcademicRuntime.PowerUpgrade")&&educationText.Contains("includeCurrentTraitEffectText=true"),"Education text uses native scaled upgrade and native dynamic formatting");
Check(educationText.Contains("OnceTurn.Contains")&&!educationText.Contains("OnceTurn.Add"),"Reading Education bonus never spends the first-spell trigger");
Check(educationText.Contains("static void Finalizer")&&educationText.Contains("if(finished)return")&&educationText.Contains("RemoveUpgrade(upgrade)"),"Prospective power is removed once on success or exception");
Check(educationText.Contains("GetHand().Contains(card)")&&educationText.Contains("AcademicRuntime.Simulating")&&educationText.Contains("frame.Card==card"),"Education text excludes collection, simulation and resolving spell");
Check(educationText.Contains("view.Power==power")&&educationText.Contains("UpdateTextContent"),"Education refreshes the hand when selected floor bonus changes");
var refundRules=File.ReadAllText(Path.Combine(root,"src/SpellRules.cs"));
using var refundJson=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/relics/WW_A05.json")).TrimStart('\uFEFF'));
Check(refundJson.RootElement.GetProperty("relics")[0].GetProperty("extensions")[0].GetProperty("collectable").GetProperty("force_update_count_label").GetBoolean(),"Refund enables native relic counter refresh");
Check(refundRules.Contains("AppendConditionCountDisplay")&&refundRules.Contains("TextFormat_Divide"),"Refund shows native localized count / 5");
Check(refundRules.Contains("OnceTurn.Clear();TuitionRefundCount=0;")&&!refundRules.Contains("OnceBattle.Add(\"A05\")"),"Refund resets per battle without a first-battle-only gate");
Check(refundRules.Contains("!frame.Indirect&&kills>0&&!AcademicRuntime.Simulating")&&refundRules.Contains("RecordKillingSpell(card,kills,core)"),"Refund counts one completed lethal cast, excluding previews and indirect replay frames");
Check(refundRules.Contains("GetLastPlayedCost()")&&refundRules.Contains("RefundProgress"),"Refund gives fifth spell paid cost using the tested counter rule");
var permitAbilityJson=bannerData["cards"]["WW_E01_Ability"];
Check(permitAbilityJson.GetProperty("initial_cooldown").GetInt32()==0&&permitAbilityJson.GetProperty("cooldown").GetInt32()==8,"Withdrawal Stamp starts ready and has cooldown eight after use");
Check(!bannerData["effects"]["WW_E01_Ability_Effect"].TryGetProperty("param_card_filter_2",out _)&&!bannerData["upgrade_masks"].ContainsKey("WW_E01_ConsumeMask"),"Withdrawal Stamp no longer has a Consume phase or its mask");
var permitCode=File.ReadAllText(Path.Combine(root,"src/IntegrationEffects.cs"));
Check(permitCode.Contains("Permit.Available(Ring(managers),equipment)&&filter!=null")&&!permitCode.Contains("purge?\"CardTraitSelfPurge\":\"CardTraitExhaustState\""),"Ring ledger blocks further Purge grants and never grants Consume");
Check(bannerData["cards"]["WW_E01"].GetProperty("names").GetProperty("english").GetString()=="Withdrawal Stamp","Equipment has its approved shorter name");
Check(permitCode.Contains("parameters.characterThatActivatedAbility??parameters.selfTarget")&&permitCode.Contains("GetEquipment().LastOrDefault")&&permitCode.Contains("Permit.MarkUsed(ring,equipment)"),"Stamp binds use to the activating unit equipment instead of a shared ability card");

var excess=mod.MainModule.Types.Single(t=>t.Name=="ExcessCredit");
var createCredit=excess.Methods.Single(m=>m.Name=="Generate");
var createCalls=createCredit.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToList();
Check(createCalls.IndexOf("get_Simulating")>=0&&createCalls.IndexOf("get_Simulating")<createCalls.IndexOf("AddNewCard"),"Extra Credit checks preview before native card generation");
Check(createCalls.Contains("GetSpawnerCard")&&createCalls.Contains("ScaleOverdamage")&&createCalls.Contains("IncrementAdditionalDamage"),"Generated damage uses native mitigated excess and records its creating champion card");
var authored=mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicAuthoredDamage");
Check(Derives(authored,"CardEffectDamage"),"Attributed spell remains a native damage card for targeting, Magic Power and spell triggers");
var authoredFlow=authored.NestedTypes.Single(t=>t.Name.StartsWith("<ApplyEffect>")).Methods.Single(m=>m.Name=="MoveNext");
Check(authored.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="CardEffectDamage"&&c.Name=="ApplyEffect")&&authoredFlow.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Stfld&&i.Operand is FieldReference f&&f.Name=="selfTarget"),"Spell attributes through native selfTarget rather than manually duplicating Slay");
var handCredit=excess.Methods.Single(m=>m.Name=="RewardHandOwner");
Check(handCredit.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldstr&&(string)i.Operand=="PyreHeartSavagery")&&handCredit.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="IncrementAdditionalDamage"),"Pathogens hand-champion Savagery exception retains permanent kill growth");
Check(handCredit.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Owner")&&handCredit.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="Contains"),"Hand reward requires the creating card in hand and excludes live owners already rewarded natively");
Check(bannerData["cards"]["WW_Token_ExtraCredit"].GetProperty("pools").GetArrayLength()==0,"Extra Credit is generated only and does not enter reward pools");
for(int level=1;level<=3;level++)Check(bannerData["character_triggers"]["WW_PublishOrIncant"+level+"_Trigger"].GetProperty("trigger").GetString()=="@WW_Overdamage","Advanced Spellwork uses reusable Overdamage instead of Incant rank "+level);
var overdamage=mod.MainModule.Types.Single(t=>t.Name=="OverdamageEvent");
var overdamageDispatch=overdamage.NestedTypes.Single(t=>t.Name.StartsWith("<Dispatch>")).Methods.Single(m=>m.Name=="MoveNext");
Check(overdamageDispatch.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="QueueAndRunTrigger")&&overdamageDispatch.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="paramInt"),"Overdamage sends excess as payload in the native trigger queue, not as repetitions");
Check(!createCredit.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldstr&&(string)i.Operand=="WW_C02")&&!createCalls.Contains("Rank"),"Overdamage consumer works on any carrier without champion or path checks");
Check(bannerData["character_trigger_types"]["WW_Overdamage"].GetProperty("names").GetProperty("english").GetString()=="Overdamage","Overdamage exposes its registered reusable keyword and tooltip");
Check(overdamage.Methods.Single(m=>m.Name=="Amount").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetTrigger"),"Only the matching owner and Overdamage trigger can consume its payload");

var creditCode=File.ReadAllText(Path.Combine(root,"src/ExcessCredit.cs"));
Check(createCalls.IndexOf("GetHand")<createCalls.IndexOf("AddNewCard")&&creditCode.Contains("FirstOrDefault(IsCredit)")&&creditCode.Contains("if(spell==null)"),"Accumulation searches hand before creating and reuses an existing Extra Credit");
Check(creditCode.Contains("fromRelic:false,permanent:true")&&!createCalls.Contains("ModifyCard"),"Credit creation enters the native run deck and accumulated damage uses permanent modifiers");
var creditCard=bannerData["cards"]["WW_Token_ExtraCredit"];
Check(creditCard.GetProperty("cost").GetInt32()==0&&creditCard.GetProperty("traits").EnumerateArray().Any(t=>t.GetString()=="@WW_ExtraCredit_Purge")&&!creditCard.GetProperty("traits").EnumerateArray().Any(t=>t.GetString()=="@WW_ExtraCredit_Ephemeral"),"Extra Credit costs zero with Purge and Frozen instead of Ephemeral");
var creditSave=mod.MainModule.Types.Single(t=>t.Name=="ExcessCreditSave");
Check(creditSave.Interfaces.Any(i=>i.InterfaceType.FullName=="Conductor.Interfaces.ISaveData")&&plugin.Methods.Single(m=>m.Name=="Awake").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.DeclaringType.Name=="SaveDataRegistry"&&c.Name=="Register"),"Authorship uses Conductor save API rather than a private save file");
Check(!mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").Methods.Single(m=>m.Name=="Reset").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.DeclaringType.Name=="ExcessCredit"&&f.Name=="Origins"),"Battle setup no longer clears between-ring authorship");
Check(creditSave.Methods.Single(m=>m.Name=="Deserialize").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetDeckState")&&creditSave.Methods.Single(m=>m.Name=="Serialize").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="IndexOf"),"Save recomputes current deck identity mapping and restores origins against the loaded native deck");

var mine=mod.MainModule.Types.Single(t=>t.Name=="MineMineMine");
Check(gameTypes["CharacterState"].Properties.Any(p=>p.Name=="PrimaryStateInformation")&&gameTypes["CharacterState"].NestedTypes.Any(t=>t.Fields.Any(f=>f.Name=="lastAttackerCharacter")),"Native death-credit fields used by MineMineMine exist in installed DLL");
Check(bannerData["character_trigger_types"]["WW_MineMineMine"].GetProperty("names").GetProperty("english").GetString()=="MineMineMine"&&bannerData["character_triggers"]["WW_MineMineMine_Claim"].GetProperty("effects")[0].GetString()=="@WW_MineMineMine_ClaimEffect","Reusable MineMineMine trigger and default claiming effect are registered together");
var mineCode=File.ReadAllText(Path.Combine(root,"src/MineMineMine.cs"));
Check(mineCode.Contains("parameters.damageType==Damage.Type.DirectAttack")&&mineCode.Contains("spell.IsAnyAbility()")&&mineCode.Contains("victim.GetTeamType()!=Team.Type.Heroes"),"MineMineMine excludes attacks, abilities and friendly victims");
Check(!mineCode.Contains("SetParamInt(")&&!mineCode.Contains("ref int damage")&&!mineCode.Contains("damageParams.attacker="),"MineMineMine changes death attribution after damage instead of changing mitigation or spell power");
Check(mineCode.Contains("MineMineMineRules.FirstHolder")&&mineCode.Contains("AcademicRuntime.Units(context.Floor)"),"Recipient comes from native front-to-back order on the victim floor");
Check(mineCode.Contains("context.SlayGranted")&&mineCode.Contains("grantingSlay.Push")&&mineCode.Contains("finally {grantingSlay.Pop();}"),"Native Slay is granted once, with a scoped bypass of original-credit suppression");
Check(File.ReadAllText(Path.Combine(root,"src/ExcessCredit.cs")).Contains("!MineMineMine.Claimed.Contains(u)"),"Claimed spell kills do not also reward an Extra Credit creator in hand");
Check(bannerData["characters"].Where(pair=>pair.Key!="WW_C02_Character").All(pair=>!pair.Value.TryGetProperty("triggers",out var ts)||!ts.EnumerateArray().Any(t=>t.GetString()=="@WW_MineMineMine_Claim")),"Only the Witch champion receives MineMineMine; other units are unchanged");

for(int rank=1;rank<=3;rank++)
{
 var creditEffect=bannerData["effects"]["WW_PublishOrIncant"+rank+"_Effect"];
 Check(creditEffect.GetProperty("param_int").GetInt32()==100,"Advanced Spellwork always converts 100 percent at rank "+rank);
 if(rank==1)Check(!creditEffect.TryGetProperty("param_upgrade",out _),"Rank one does not grant a keyword upgrade");
 else Check(creditEffect.GetProperty("param_upgrade").GetString()==(rank==2?"@WW_ExtraCredit_AttunedUpgrade":"@WW_ExtraCredit_AttunedExplosiveUpgrade"),"Rank keyword improvement matches JSON rank "+rank);
}
Check(createCalls.Contains("TryApplyUpgradeToCard")&&createCalls.Contains("SetAvoidClobberingExistingTraits")&&createCalls.Contains("ReapplyMagicPowerScalingFromTraitsToAllExistingUpgrades"),"Keyword grants use native upgrade helper, preserve existing traits and recalibrate Attuned");
Check(bannerData["traits"]["WW_ExtraCredit_AttunedTrait"].GetProperty("name").GetString()=="CardTraitStrongerMagicPower"&&bannerData["traits"]["WW_ExtraCredit_ExplosiveTrait"].GetProperty("name").GetString()=="CardTraitDamageOverflow","Rank bonuses use native Attuned and Explosive traits");

for(int rank=1;rank<=3;rank++)
{
 var prefix="WW_PublishOrIncant"+rank;
 var grants=upgrades[prefix].GetProperty("trigger_upgrades").EnumerateArray().Select(t=>t.GetString()).ToArray();
 Check(grants.Count(t=>t=="@"+prefix+"_IncantTrigger")==1&&grants.Count(t=>t=="@"+prefix+"_Trigger")==1,"Advanced Spellwork keeps one Incant and one Overdamage at rank "+rank);
 Check(bannerData["character_triggers"][prefix+"_IncantTrigger"].GetProperty("trigger").GetString()=="card_spell_played"&&bannerData["effects"][prefix+"_IncantEffect"].GetProperty("name").GetString()=="CardEffectBuffDamage"&&bannerData["effects"][prefix+"_IncantEffect"].GetProperty("param_int").GetInt32()==(rank==1?5:rank==2?10:20),"Restored Incant uses original native attack trigger and value at rank "+rank);
}

for(int rank=1;rank<=3;rank++)
{
 var prefix="WW_TenureOfProtection"+rank;
 var effects=bannerData["character_triggers"][prefix+"_Trigger"].GetProperty("effects").EnumerateArray().Select(e=>e.GetString()!.TrimStart('@')).ToArray();
 Check(effects.Length==2,"Protective Tenure Incant executes two effects at rank "+rank);
 var native=effects.Select(id=>bannerData["effects"][id]).ToArray();
 Check(native.All(e=>e.GetProperty("name").GetString()=="CardEffectAddStatusEffect"&&e.GetProperty("target_mode").GetString()=="self"&&e.GetProperty("param_status_effects").GetArrayLength()==1),"Each Incant status has its own native effect, eliminating random selection at rank "+rank);
 Check(native.Any(e=>e.GetProperty("param_status_effects")[0].GetProperty("status").GetString()=="armor"&&e.GetProperty("param_status_effects")[0].GetProperty("count").GetInt32()==(rank==1?3:rank==2?6:12))&&native.Any(e=>e.GetProperty("param_status_effects")[0].GetProperty("status").GetString()=="spikes"&&e.GetProperty("param_status_effects")[0].GetProperty("count").GetInt32()==(rank==1?1:rank==2?2:4)),"Incant grants both approved Armor and Spikes at rank "+rank);
}
var statusSelector=gameTypes["CardEffectAddStatusEffect"].Methods.Single(m=>m.Name=="GetStatusEffectStack");
Check(statusSelector.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="RandomElement"),"Installed native AddStatusEffect chooses one entry from a multi-status list; lists are not simultaneous effects");

// Shop eligibility is based on effect-name masks, not simply inheritance.
var registrationCalls=mod.MainModule.Types.SelectMany(t=>t.Methods).Concat(mod.MainModule.Types.SelectMany(t=>t.NestedTypes).SelectMany(t=>t.Methods)).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(registrationCalls.Any(c=>c.DeclaringType.FullName=="Conductor.Utilities"&&c.Name=="MarkEffectAsADamageEffect")&&registrationCalls.Any(c=>c.DeclaringType.FullName=="Conductor.Utilities"&&c.Name=="MarkEffectAsHealEffect"),"Custom damage and delayed healing register with installed Conductor upgrade masks");
Check(registrationCalls.Any(c=>c.DeclaringType.Name=="Railend"&&c.Name=="ConfigurePostAction"),"Effect masks register in the library's documented post-container phase");
var conduitUtilities=conductor.MainModule.Types.Single(t=>t.FullName=="Conductor.Utilities");
foreach(var method in new[]{"MarkEffectAsADamageEffect","MarkEffectAsHealEffect"})
 Check(conduitUtilities.Methods.Single(m=>m.Name==method).Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="MagicPowerSpellsRequiredCardEffects"),method+": installed library extends actual Magic Power mask");
Check(Derives(types["CardEffectAcademicSabbatical"],"CardEffectHeal"),"Delayed Sabbatical inherits native healing to receive +Heal upgrades");
var modifiedScalar=gameTypes["CardEffectState"].Methods.Single(m=>m.Name=="GetModifiedParam");
Check(modifiedScalar.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Isinst&&i.Operand is TypeReference t&&t.Name=="CardEffectHeal"),"Native power getter scales subclasses of Heal including Sabbatical");
foreach(var fileId in new[]{"WW_S02","WW_S09","WW_S25"})
{
 using var iconDocument=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/cards/"+fileId+".json")).TrimStart('\uFEFF'));
 var data=iconDocument.RootElement;var icon=data.GetProperty("status_effects")[0].GetProperty("icon").GetString()!.TrimStart('@');
 var sprite=data.GetProperty("sprites").EnumerateArray().Single(x=>x.GetProperty("id").GetString()==icon).GetProperty("path").GetString()!;
 var atlas=data.GetProperty("atlas_icons").EnumerateArray().Single(x=>x.GetProperty("id").GetString()==icon).GetProperty("path").GetString()!;
 Check(sprite!=atlas,fileId+": sprite and atlas use different art variants");
 foreach(var asset in new[]{sprite,atlas})
 {
  var bytes=File.ReadAllBytes(Path.Combine(root,"content",asset));
  Check(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4))==50&&System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4))==50,asset+": native status icon is 50x50");
 }
}
using(var foundationDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"content/json/00_foundation.json")).TrimStart('\uFEFF')))
 foreach(var id in new[]{"WW_WizardDisplay","WW_WitchDisplay"})
 {
  var scale=foundationDoc.RootElement.GetProperty("game_objects").EnumerateArray().Single(g=>g.GetProperty("id").GetString()==id).GetProperty("extensions").GetProperty("character_art").GetProperty("transform").GetProperty("scale");
  Check(scale.GetProperty("x").GetDouble()==1.6&&scale.GetProperty("y").GetDouble()==1.6,id+": run selection display uses full-size art scale");
 }
var rodFeedback=mod.MainModule.Types.Single(t=>t.Name=="LightningRodSafety").NestedTypes.Single(t=>t.Name.StartsWith("<Feedback>")).Methods.Single(m=>m.Name=="MoveNext");
Check(rodFeedback.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="get_PreviewMode")&&rodFeedback.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="get_IsRunningCardPlayPreview"),"Refund checks both native preview flags before real PlayerManager writes");
Check(rodFeedback.Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="get_TemporaryPreviewsEnabled"),"Preview feedback requires a prepared temporary character state");
var representativeHook=mod.MainModule.Types.Single(t=>t.Name=="RepresentativeDamageHook");
Check(representativeHook.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPatch"&&a.ConstructorArguments.Any(v=>v.Value is string name&&name=="DoNotifyDamageWasApplied")),"Representative reads native postmitigation damage before HP clipping");
var afterSpellStats=mod.MainModule.Types.Single(t=>t.Name=="AcademicRuntime").NestedTypes.Single(t=>t.Name.StartsWith("<AfterSpell>")).Methods.Single(m=>m.Name=="MoveNext");
Check(afterSpellStats.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="RepresentativeDamage"),"Representative damage is separate from effective-health healing input");
Check(bannerData["cards"]["WW_U17"].GetProperty("descriptions").GetProperty("english").GetString()!.Contains("on any floor"),"Representative wording states global floor scope");
var generatedPool=bannerData["card_pools"]["WW_ExtraCredit_GeneratedPool"];
Check(generatedPool.GetProperty("cards").GetArrayLength()==1&&generatedPool.GetProperty("cards")[0].GetString()=="@WW_Token_ExtraCredit","Extra Credit uses a singleton generated-card pool, not a reward pool");
foreach(var rank in new[]{1,2,3})
 Check(bannerData["effects"]["WW_PublishOrIncant"+rank+"_Effect"].GetProperty("param_card_pool").GetString()=="@WW_ExtraCredit_GeneratedPool","All Witch ranks expose the same generated card to native tooltips");
foreach(var dataType in new[]{"CardEffectState","CardEffectData"})
 Check(gameTypes[dataType].Methods.Single(m=>m.Name=="GetGeneratedCardDataForTooltip").Body.Instructions.Any(i=>i.Operand is MethodReference c&&c.Name=="GetNumCards"),dataType+": installed native generated-card tooltip resolves singleton pools");
Check(!mod.MainModule.Types.Single(t=>t.Name=="ExcessCredit").Fields.Any(f=>f.Name=="Template"),"Generation does not depend on a global hardcoded card template");
var metadataBridge=mod.MainModule.Types.Single(t=>t.Name=="GrantedEffectTooltips").NestedTypes.Single(t=>t.Name.StartsWith("<Append>")).Methods.Single(m=>m.Name=="MoveNext");
var metadataCalls=metadataBridge.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToHashSet();
Check(metadataCalls.Contains("GetTraitDataUpgrades")&&metadataCalls.Contains("GetCardTooltipText")&&metadataCalls.Contains("GetCardTooltipTitle"),"Granted traits reuse native localized tooltip formatting");
Check(metadataCalls.Contains("GetParamStatusEffects")&&metadataCalls.Contains("TryGetStatusEffectTooltipContent")&&metadataCalls.Contains("GenerateTooltipContentForInternalUpgrade"),"Granted states/triggers reuse structured metadata and native generators");
Check(metadataCalls.Contains("GetHideTooltip")&&metadataCalls.Contains("get_Assembly"),"Grant bridge honors hidden tooltips and restricts effects by owning assembly");
Check(!mod.MainModule.Types.Any(t=>t.Name=="AcademicCardTooltipHook"),"Missing keyword repair never restores duplicate whole-spell text");
foreach(var id in new[]{"WW_S30_Effect","WW_S07_Effect","WW_U08_CopyLastCommon"})
 Check(bannerData["effects"][id].TryGetProperty("param_upgrade",out _),id+": granted keywords are configured in JSON");
// The installed game requires both flags before it marks invalid hand cards.
Check(chooseInstructions.Any(i=>i.Operand is FieldReference f&&f.Name=="selectionHandState"),
    "Shared selector supplies the native selection state required for NotEligible rendering");
foreach(var nativeTypeName in new[]{"HandUI","HandSelectionUI"})
{
 var method=gameTypes[nativeTypeName].Methods.Single(x=>x.Name==(nativeTypeName=="HandUI"?
    "SetIneligibleCardsForDuplicationByFilter":"ApplyCardSelectionEligibilityByFilter"));
 Check(method.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="get_HasValue"),
    nativeTypeName+": installed native eligibility rendering requires selectionHandState.HasValue");
}
foreach(var name in new[]{"CardEffectAcademicTome","CardEffectAcademicPurgePermit","CardEffectAcademicDiscardForEmber",
    "CardEffectAcademicConsumeCredits","CardEffectAcademicConduitTransfer","CardEffectAcademicInteractiveRule","CardEffectAcademicDissertation","CardEffectAcademicCollectiveAssessment"})
 Check(mod.MainModule.Types.Single(t=>t.Name==name).Interfaces.Any(i=>i.InterfaceType.Name=="ICardEffectUiDialog"),
    name+": declares native interactive-effect contract");
Check(!ruleEffect.Interfaces.Any(i=>i.InterfaceType.Name=="ICardEffectUiDialog"),
    "Noninteractive spell rules do not falsely advertise a selection dialog");
foreach(var effect in bannerData["effects"].Values.Where(e=>e.TryGetProperty("param_str",out var rule)&&
    new[]{"defer_hand","recover_x","archive","bind_attack","area","kill_return"}.Contains(rule.GetString())))
 Check(effect.GetProperty("name").GetString()=="@CardEffectAcademicInteractiveRule",
    effect.GetProperty("id").GetString()+": selection rule uses the interactive subclass");
Check(bannerData["effects"]["WW_S07_Effect"].GetProperty("additional_tooltips").EnumerateArray()
    .Any(t=>t.GetString()=="@WW_MagicPowerReference"),"Conduit Credit Transfer exposes native Magic Power explanation");
Check(bannerData["effects"]["WW_C01_Ability_Effect"].GetProperty("param_upgrade").GetString()=="@WW_C01_NextUseUpgrade",
    "Requisition exposes the actual next-use Explosive upgrade for native tooltips");
Check(!mod.MainModule.Types.Single(t=>t.Name=="CardEffectAcademicWizardDraw").Fields.Any(f=>f.Name=="NextUseUpgrade"),
    "Requisition no longer hides the granted upgrade in a global field");
foreach(var coreEvent in new[]{overheal,mod.MainModule.Types.Single(t=>t.Name=="ArcaneSlayEvent")})
{
 Check(coreEvent.Namespace=="MonsterTrain2.SharedTriggers"&&coreEvent.IsPublic,coreEvent.Name+": independent public namespace");
 var nested=coreEvent.NestedTypes.SelectMany(t=>t.NestedTypes.Prepend(t)).Prepend(coreEvent);
 Check(!nested.SelectMany(t=>t.Methods).Where(method=>method.HasBody).SelectMany(method=>method.Body.Instructions)
    .Select(i=>i.Operand).OfType<MemberReference>().Any(member=>member.DeclaringType?.Namespace=="WwInc"),
    coreEvent.Name+": event core has no call or field dependency on clan types");
}
Check(bannerData["effects"]["WW_S02_Effect"].GetProperty("name").GetString()=="@CardEffectAcademicDissertation"&&
    bannerData["effects"]["WW_U06_Ability_Effect"].GetProperty("name").GetString()=="@CardEffectAcademicCollectiveAssessment"&&
    bannerData["effects"]["WW_S01_Effect"].GetProperty("name").GetString()=="@CardEffectAcademicInterdepartmentalMemo",
    "Current interactive and repeat cards resolve to dedicated effects instead of legacy string dispatch");
Console.WriteLine(JsonSerializer.Serialize(new {success=errors.Count==0,checks,errors},new JsonSerializerOptions{WriteIndented=true}));
return errors.Count==0?0:1;

// 2026-10-10-0012||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Prueba de contratos de JSON y parches Harmony contra DLLs reales
// 2026-10-10-0013||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Resolver dependencias reales al inspeccionar metadatos de BepInEx
// 2026-10-10-0109||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de invocación para capacidad, pisos bloqueados y estadísticas fijas de tokens
// 2026-10-10-0110||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar el lector de Conduit en el delegado compilado de la suma
// 2026-10-11-0001||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de dieciocho rangos, habilidad compatible, replay y selector nativo
// 2026-10-11-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos nativos de retirar equipo, restaurar habilidad, bloqueo de robo y unidades exteriores
// 2026-10-11-0010||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar versión coherente y guardia real contra duplicación de cartas en MoveNext

// 2026-10-11-0019||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos del objetivo repetido, texto localizado y fuentes reales de rangos

// 2026-10-11-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de filtro nativo, whitelist y guardias previas a consultas y mutaciones

// 2026-10-11-0029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de elegibilidad nativa de banner y pertenencia coherente a pools

// 2026-10-11-0037||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresión del robo del decano comparada con Runespeak, interfaces y ambos montones

// 2026-10-11-0039||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar receptor DrawType y llamada virtual declarada en CardEffectBase

// 2026-10-11-0943||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de lanzabilidad, selectores, nueve habilidades, Power, recarga restante, Freeze y retorno seguro

// 2026-10-11-0945||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresión de persistencia de talla y estadísticas al combinar E02 con S26

// 2026-10-11-1032||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresión de Explosive ausente tras invocar y lectura común de auras desde carta de campeón

// 2026-10-11-1039||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contrastar Power y ciclo de Attuned con datos y retirada nativos

// 2026-10-11-1104||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de starter Impact, tres cartas aprobadas y selección segura del tomo

// 2026-10-11-1117||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar Conduit para ambos subtipos y registro del trigger

// 2026-10-11-1118||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Incluir character_triggers en el lector de contratos del aura

// 2026-10-11-1445||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar rutas nativas y contexto dinámico de tooltips personalizados

// 2026-10-11-1503||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar selección y validación con pisos nativos entre turnos

// 2026-10-11-1510||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar doble filtro nativo, alcance por carta y limpieza de excepción de bando

// 2026-10-11-1552||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar ambos montones, unicidad y nombre de Credit Recovery

// 2026-10-11-1618||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de Exam Week: robo nativo de tres hechizos, Consume y eliminación de traslado al fondo

// 2026-10-11-1619||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Corregir consulta Cecil de SpellRules en regresión de Exam Week

// 2026-10-11-1622||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresión de argumento Ephemeral falso al restaurar hechizos aplazados

// 2026-10-11-1623||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresión de mejora del tomo sin modificación del coste del objetivo

// 2026-10-11-1629||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comparar rarezas de las 51 cartas reales por ID con catálogo; prevenir confusión con habilidades

// 2026-10-11-1631||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de tres pilas y limpieza del selector de Archivera

// 2026-10-11-1631||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar bloque finally generado del iterador y retirada de su ámbito

// 2026-10-11-1633||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones del tooltip de reservas por unidad y nombres localizados

// 2026-10-11-1637||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de protección de gestores, icono sin mejoras reales y cementerio sin duplicados

// 2026-10-11-1651||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar cooldown 2 de la habilidad real de Shared Damages

// 2026-10-11-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de carga Reserve fija +10 y separación del daño modificado por piedras

// 2026-10-11-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Incluir upgrades en lector de contratos para comprobar WW_S28_Charge

// 2026-10-11-1700||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar daño de ambos bandos en tres hechizos y curación aliada intacta

// 2026-10-11-1734||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresión de rareza y pool nativos de ambos starters

// 2026-10-11-1742||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de pools exclusivos, objetivos de ambos bandos y texto Dazed dinámico

// 2026-10-11-1743||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de mensajes correctos en selección de mano y pilas

// 2026-10-11-1800||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar Pierce 1 inicial y selector de objetivos en DLL Conductor instalado

// 2026-10-11-1813||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresión de título y descripción en diez idiomas para toda mejora con icono visible

// 2026-10-11-1814||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Evitar colisión de variable upgrades en regresión de tooltips

// 2026-10-11-1816||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de copia de Postdoc, conservación de original y mejoras nativas

// 2026-10-11-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de escalado real y filtros visuales para Exam Week y copias

// 2026-10-11-2108||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos del marcador visible, localización, clase y retirada nativa al expirar

// 2026-10-11-2127||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Añadir regresiones de Slay nativo permanente y curación inmediata del seguro

// 2026-10-11-2128||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Omitir flag falso predeterminado de Slay mal ubicado en esquema de Trainworks

// 2026-10-11-2130||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Proteger Conduit de los nueve rangos y efectos médicos adicionales

// 2026-10-12-0036||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de ataque inmediato antes de Dazed y ausencia de modificación de Armor en Desk Swap

// 2026-10-12-0053||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Proteger salida antes de mutar cartas cuando bono de resolución es cero

// 2026-10-12-0055||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contrastar enlace del trait aditivo, daño base y estadística nativa de consumidas

// 2026-10-12-0100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contrato de Emergency Office Hours de reducción 2 a 1

// 2026-10-12-0111||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de los cinco filtros de redirección Both y unicidad del becario como objetivo

// 2026-10-12-0915||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Retirar prueba de encogimiento descartado y proteger las cinco mecánicas aprobadas

// 2026-10-12-0918||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar restricción de Magos y bono de equipo Conduit 5 nativo

// 2026-10-12-0923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Proteger eliminación de habilidades médicas, wording y enlace al contador probado

// 2026-10-12-0925||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contrato de Desk Swap para ausencia de Dazed

// 2026-10-12-0927||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contratos de +5 Magic Power, Attuned y persistencia tras jugar

// 2026-10-12-0938||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Proteger exclusión de recompensas y libro de Consume and Re-enroll

// 2026-10-12-0946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar daño coste rareza S32 y retirada S24

// 2026-10-12-1021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar nombre específico y ausencia de descripción de pasiva duplicada en Archivera

// 2026-10-12-1022||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Proteger títulos de habilidades distintos y ausencia del nombre genérico

// 2026-10-12-1303||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Proteger vista previa sin mutación, ámbito de mano/piso, no duplicación y refresco visual

// 2026-10-12-1304||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Validar firma explícita Harmony Type array para no rechazar overload seleccionado

// 2026-10-12-1427||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de espera asíncrona y orden selección coste descarte Ember

// 2026-10-12-1430||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Proteger cleanup en lanzamientos reales, instantánea de instancias nuevas y duración explícita

// 2026-10-12-1438||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Proteger equipo sin subtipo y Postdoc global registrado durante resolución

// 2026-10-12-1543||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Proteger filtro de rareza antes del registro global y texto Common de Postdoc

// 2026-10-12-1554||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Proteger Extinguish nativo con Undying, efecto de copia y ausencia de activación doble

// 2026-10-12-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Sustituir contratos de espera bloqueante por callback nativo y conservar regresiones de descarte Ember

// 2026-10-12-1657||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar coste y Attuned del tomo dentro del callback confirmado

// 2026-10-12-1712||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contratos para componentes nativos de recarga, Reserve y mejora del tomo

// 2026-10-12-1713||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Incluir triggers de cartas y rasgos en índice de contratos

// 2026-10-12-1733||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar expectativa de daño de Examen General de 3 a 8.

// 2026-10-12-1740||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contrato de curación de Practicum: ambos bandos, piso completo, base 4

// 2026-10-12-1758||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar Slay nativo, devolución posterior al descarte, Freeze y expiración exacta de Unplayable

// 2026-10-12-1808||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar Armor nativa, eliminación de bono de ataque y conservación de Harvest

// 2026-10-12-1815||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar permiso para cualquier tipo, filtro nativo y límite de ring

// 2026-10-12-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Exigir monsters en curación S14; excluir enemigos.

// 2026-10-12-2053||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar Hostile Medicine exceso propio, enemigos del piso y ausencia de cuota primer hechizo/trample

// 2026-10-12-2054||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Limitar contrato de also a U02; U03 tiene nueva pasiva de exceso

// 2026-10-12-2056||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar reparto nativo y sin recorte de exceso; interacción Hostile/PhD sin realimentación

// 2026-10-12-2103||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar máscara nativa, flag Heroes/Both y texto de objetivos enemigos en S02

// 2026-10-12-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar dúos Mixed y eliminación de condición Chaperone en Strictly Platonic

// 2026-10-12-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar que solo Strictly Platonic deja de usar tríos; conservar reliquia A09

// 2026-10-12-2115||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar texto explícito Unplayable y ausencia de overrides duplicados en Faculty Robe

// 2026-10-12-2200||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar registro y tooltip Arcane Slay, efecto Burst nativo y despacho sin ID U05
// 2026-10-12-2213||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos Overheal: cola con cantidad, propietario, scopes anidados, copias, efectos nativos y tooltips
// 2026-10-12-2213||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar construcción del efecto nativo y llamada heredada ApplyEffect en IL
// 2026-10-12-2233||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos Dean: habilidad común, seis rangos Arcane Slay, Conduit nativo y Power filtrado a hechizos
// 2026-10-12-2235||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos contra regresión de habilidad duplicada en portadores; conservar texto y tooltip auxiliar
// 2026-10-12-2242||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos varita: equipo, Arcane Slay, Conduit 1 y enum nativo de permanencia
// 2026-10-12-2255||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos Scholarship: coste hasta jugar, mejora nativa identificada y rarezas S09/S10
// 2026-10-12-2258||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos Spike de área, debilidad nativa X y eliminación cooldown; Credit Recovery retirada
// 2026-10-12-2300||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de seis bajas y nueva reliquia de primera curación de turno
// 2026-10-12-2301||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar clasificación de hechizos mediante efectos nativos y extensiones de Conductor
// 2026-10-12-2302||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Incluir reliquias en inventario de contratos y verificar clasificación nativa/Conductor de curación
// 2026-10-12-2322||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contratos de rarezas, efectos, retiro Campus y Scholarship hasta final de batalla
// 2026-10-13-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de marca visible, no propagación, contador decreciente y múltiples hechizos
// 2026-10-13-0024||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contrato de reservas: vínculos en tooltip propio de marca Dissertation
// 2026-10-13-0031||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos Scholarship: estado visible, tooltip localizado y aplicación nativa sin flag duplicado
// 2026-10-13-0031||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contrato de referencia local @ww_scholarship
// 2026-10-13-0033||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contrato de resolución de IDs completos para Scholarship y Dissertation
// 2026-10-13-0051||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contrato de objetivo pila de robo en Scholarship y tooltip actualizado
// 2026-10-13-0054||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos sprite/atlas de Scholarship, Dissertation e Insurance
// 2026-10-13-0106||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de retorno nativo, firma real, preservación, preview y fin de secuencia/muerte
// 2026-10-13-0108||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contrato de tooltip Scholarship para cualquier carta
// 2026-10-13-2134||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contrato de bando enemigo del efecto nativo de área
// 2026-10-13-2138||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contratos de origen y persistencia; excluir preview y conservar cola de Arcane Slay
// 2026-10-13-2144||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de validación nativa sin contexto y atribución persistente en cola
// 2026-10-13-2257||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Reforzar contrato existente para que coste 0 de copia no caduque al acabar turno
// 2026-10-13-2353||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de Freeze nativo, cooldown 2 y pasiva independiente sin vínculo de carta
// 2026-10-15-2142||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de próximo uso, descarte sin jugar, protección de Explosive previo y marcador oculto
// 2026-10-15-2226||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de texto Education, limpieza, no consumo y no duplicación durante resolución
// 2026-10-17-0910||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de contador nativo, reinicio de batalla, coste pagado y exclusión de preview/repeticiones internas
// 2026-10-19-2254||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresión del filtro faltante: definición, referencia, tipo nativo excluido y enlace al cargar
// 2026-10-19-2259||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de recargas inicial/normal, máscaras por fase, traits concedidos y nuevo nombre
// 2026-10-19-2359||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de un solo Purge por ring y ausencia de fase/máscara Consume
// 2026-10-20-0000||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Withdrawal Stamp sin recarga; mantiene único Purge por ring y nuevo nombre
// 2026-10-20-0012||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Withdrawal Stamp: recarga inicial 0 y posterior 8; conserva un Purge por ring
// 2026-10-20-0017||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contrato de resolución del equipo físico y consumo del permiso por copia
// 2026-10-24-1824||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar trigger de daño único en campeona base sin duplicación por rango o multisenda
// 2026-10-24-1840||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de generación real, atribución nativa, Savagery sin doble recompensa y retirada de Incant anterior
// 2026-10-24-1841||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar delegación base de coroutine mediante helper generado por C#; atribución selfTarget conserva comprobación
// 2026-10-24-1843||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de cola, cantidad, propietario y trigger genérico reutilizable; consumidor sin hardcode de campeón
// 2026-10-24-1923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de reutilización en mano, persistencia de daño/creación, Purge y API SaveDataRegistry
// 2026-10-24-1932||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de atribución sin modificar daño, receptor frontal, Slay único, API nativa y ausencia de portadores asignados
// 2026-10-24-1943||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Probar MineMineMine base único, Overheal en todos los rangos de protección y ausencia en las otras sendas
// 2026-10-24-1946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos de conversión 100% en tres rangos, mejoras nativas y preservación de traits existentes
// 2026-10-24-1959||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar Incant nativo +5/+10/+20 una vez por rango junto con Overdamage
// 2026-10-24-2025||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresión contra selector RandomElement nativo; Incant debe tener efectos individuales para ambos estados en tres rangos
// 2026-10-24-2100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar contrato de Dissertation para hechizos aliados manteniendo filtro nativo
// 2026-10-24-2102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contratos del puente nativo, clasificación de estados, opciones fuera de pools y callback de ataque
// 2026-10-24-2103||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Adaptar contratos a instrucciones parametrizadas y verificar replay nativo de selección
// 2026-10-25-1547||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de no duplicación de hechizos, retención de pasivas y hide_tooltip oficial
// 2026-10-25-1548||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar efecto Dazed por su ID real y retirar aserción redundante
// 2026-10-25-1559||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de registro Conductor para piedras, herencia de curación, dos iconos 50x50 y escala de selección
// 2026-10-25-1607||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Comprobar flags nativos de preview, estado temporal, lectura postmitigación y texto global de representante
// 2026-10-25-1617||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Regresiones de pool generador, tres rangos, formato nativo y separación de keywords frente a texto redundante
// 2026-10-25-1641||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contrastar selector con requisitos del motor instalado y contratos UI declarados sin marcar reglas pasivas
// 2026-10-25-1643||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Verificar referencia autoritativa de mejora de Requisition sin global invisible a tooltips
// 2026-10-25-1743||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Adaptar contratos de texto a propuestas aprobadas manteniendo comprobación de semántica y tooltip de Dissertation
// 2026-10-25-1744||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Leer tooltip de Dissertation del JSON real; índice del arnés no incluye status_effects
// 2026-10-25-1745||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Actualizar texto esperado de Hostile Medicine a la forma breve aprobada
// 2026-10-25-1849||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Adaptar contratos a starter 2 de ataque y clonación de efectos en consumidor separado
// 2026-10-25-1852||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Program.cs||Contrastar núcleos sin dependencias al clan y conexión de los tres efectos activos dedicados
