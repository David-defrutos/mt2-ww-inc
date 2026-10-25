<!-- Documento generado el 2026-10-25-1853 -->
# Shared Overheal and Arcane Slay event cores

This project proves that the event transport used by W&W compiles without the clan and without Conductor. It links exactly two production source files: ../../src/OverhealEvent.cs and ../../src/ArcaneSlayEvent.cs, in namespace MonsterTrain2.SharedTriggers.

Build: `dotnet build SharedTriggers.csproj -c Release`. Override MonsterTrain2Managed and BepInExCore MSBuild properties for another installation. Only game, Unity and Harmony references are needed. Do not install this verification DLL alongside W&W: W&W already compiles these same sources into its plugin. No extra runtime DLL is shipped by this change.

## Host responsibilities

Register trigger enums before installing the corresponding Harmony patches and assign OverhealEvent.Trigger and ArcaneSlayEvent.Trigger. The host detects healing/spell kills and supplies an ICoreGameManagers instance:

```csharp
yield return OverhealEvent.Dispatch(core, healedUnit, excessHealing, sourceCard);
yield return ArcaneSlayEvent.Dispatch(core, sourceSpell, floor, kills);
```

Consumers read OverhealEvent.Amount(effectParams), OverhealEvent.Source and ArcaneSlayEvent.Source(effectParams). The native queue transports the payload; the execution scope is restricted by owner and trigger and is restored after nested calls, disposal or exceptions. Arcane Slay uses native room triggerCount, not a reimplemented trigger loop.

Detection and gameplay policy are not magically exported by this compilation. W&W still owns its heal observation, spell-resolution accounting, full-heal exclusion, Armor cap, damage conversion recursion guards and MineMineMine attribution. These belong outside the shared event transport. JSON effects awarding Armor, damage, Burst, Conduit or permanent spell upgrades are consumers, not part of the generic event definition.

## Validation and integration status

TriggerEvents executes these actual source files with minimal native doubles, checking queue payloads, multiple listeners/kills, nested events and exception cleanup. The independent project compiles them against installed native assemblies. These checks do not run Unity. This is a source extraction and integration boundary, not a Conductor release, accepted contribution or complete replacement for its trigger registration API. The library maintainer must choose its final namespace/registration convention and review detection hooks before adoption.
<!-- 2026-10-25-1853||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/shared/Triggers/README.md||Documentar compilación independiente, contrato host/consumidor y límites; no atribuir integración a Conductor -->


## Minimal JSON consumer example

After the host registers LibraryOverheal and assigns its enum to OverhealEvent.Trigger, the data side can reuse native effects:

```json
{
  "effects": [{"id":"ExampleArmor", "name":"CardEffectAddStatusEffect", "target_mode":"self", "target_team":"monsters", "param_status_effects":[{"status":"armor", "count":1}]}],
  "character_triggers": [{"id":"ExampleOnOverheal", "trigger":"@LibraryOverheal", "effects":["@ExampleArmor"]}]
}
```

This example awards Armor 1 per event. It intentionally does not pretend that native fixed Armor automatically scales with the excess amount. A proportional consumer uses OverhealEvent.Amount(parameters), copies its configured native effect state, and applies that native effect. W&W examples of such consumers live in src/OverhealEffects.cs, outside this independent project. Arcane Slay can similarly award native Burst 1 per kill; its event count is carried by the native room queue. No card IDs, W&W reward classes or balance policies belong in the shared transport.
<!-- 2026-10-25-1856||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/shared/Triggers/README.md||Añadir ejemplo JSON nativo con límite explícito entre evento, cantidad y efecto consumidor -->
