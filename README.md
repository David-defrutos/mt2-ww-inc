<!-- Documento generado el 2026-10-10-0024 -->
# W&W Inc.

## Development source — 0.1.154 beta

Source code and assets for **W&W Inc.**, a Monster Train 2 clan. The latest changes have passed code and data validation; their behavior and presentation still need in-game verification.

- `src/`: plugin, card effects and integration hooks.
- `content/`: runtime JSON and artwork.
- `tests/`: seven suites covering rules, native API contracts, damage traits, kill attribution, Dissertation, combat safety and shared trigger events.
- `design/`: card/relic design catalog and original icon sources.
- `shared/Triggers/`: [independent Overheal and Arcane Slay event transport](shared/Triggers/README.md), with a standalone compilation project and a data-side consumer example. Clan-specific detection and reward policies remain in W&W.
- `thunderstore/`: package metadata and changelog.

### Building from a clone

Requires .NET SDK 10, Monster Train 2, BepInEx, Trainworks Reloaded 0.7.33 and Conductor 0.5.15. Set the local assembly paths in `src/WwInc.csproj` and `tests/Contracts/Contracts.csproj`, and the assembly resolver paths at the top of `tests/Contracts/Program.cs`, to your installation. Game and dependency DLLs are not included.

From the repository root:

```powershell
dotnet build src/WwInc.csproj -c Release
dotnet run --project tests/Rules/Rules.csproj
dotnet run --project tests/Contracts/Contracts.csproj -- .
dotnet run --project tests/DamageTraits/DamageTraits.csproj
dotnet run --project tests/KillClaims/KillClaims.csproj
dotnet run --project tests/Dissertation/Dissertation.csproj
dotnet run --project tests/CombatSafety/CombatSafety.csproj
dotnet run --project tests/TriggerEvents/TriggerEvents.csproj
```

The shared-trigger project accepts `MonsterTrain2Managed` and `BepInExCore` MSBuild properties to override its default local paths. It compiles independently of W&W and Conductor; it is not a Conductor plugin or a second DLL to install alongside W&W.

Latest local validation: **1,264 checks**, valid runtime JSON, and both compilation projects finish without errors or warnings. Automated checks do not replace in-game testing. Compiled output, game libraries, local reports and tester feedback notes are excluded from Git.

## Workspace notes

Las secciones siguientes conservan el flujo del workspace original. Sus enlaces a `docs/`, scripts de operaciones y rutas absolutas requieren ese workspace y no se resuelven en un clon independiente. No son pasos necesarios para las pruebas de reglas y daño.


Versión local 0.1.154 del clan Wizards & Witches Inc. Colección activa: 43 cartas, incluidos campeones y starters, y 10 reliquias. Las definiciones retiradas y habilidades auxiliares se conservan aparte.

[Estado y pruebas pendientes](../../../../docs/clanes/ww-inc/hechizos-starter-2026-10-11-1106.md) · [Catálogo seleccionado](design/catalog.json).

Los 263 bloques localizados runtime cubren los diez idiomas admitidos. El validador comprueba cobertura y parámetros; las definiciones retiradas se conservan como compatibilidad. No ejecutar los generadores históricos para reconstruir el contenido vigente.

El runtime está en `content/json` y el código C# en `src`. Las fichas `design/cards` y `design/relics` son documentación y no se cargan en Trainworks.

## Arte

Los recursos distribuidos están en `content/textures`, incluidos cartas, sprites, reliquias e interfaz. Los maestros y variantes del workspace se conservan fuera de este repositorio. El informe enlazado conserva entregas históricas y no sustituye una comprobación actual del encaje dentro de Unity.

[Informe de arte](../../../../docs/clanes/ww-inc/arte-completo-2026-10-11-1019.md).

## Compilar y comprobar

Desde la raíz `D:/Juegos/MT2_mod`:

```powershell
Set-Location 'D:/Juegos/MT2_mod'
dotnet build repos/MT2/repos-propios/mt2-ww-inc/src/WwInc.csproj -c Release
dotnet run --project repos/MT2/repos-propios/mt2-ww-inc/tests/Rules/Rules.csproj
dotnet run --project repos/MT2/repos-propios/mt2-ww-inc/tests/Contracts/Contracts.csproj -- 'D:/Juegos/MT2_mod/repos/MT2/repos-propios/mt2-ww-inc'
python scripts/operaciones/validar-ww-inc-2026-10-09.py
```

El proyecto referencia las DLL del juego y del perfil Default instalados en este ordenador. Compilado con Trainworks 0.7.33 y Conductor 0.5.15. No copia bibliotecas del juego al paquete.

## Paquete e instalación local

```powershell
Set-Location 'D:/Juegos/MT2_mod'
python scripts/operaciones/empaquetar-ww-inc-2026-10-10.py --install
```

La instalación usa únicamente la carpeta `Local-WWInc` del perfil Default. El proceso comprueba rutas y hashes, conserva otros mods y no reinicia el juego. Si el juego está abierto, el plugin se cargará en el siguiente arranque de ese perfil.

**Validado fuera de Unity:** compilación del clan y núcleos independientes sin errores ni advertencias; JSON válidos y 1.264 comprobaciones: 1.004 contratos, 124 reglas, 38 de daño, 16 de autoría, 32 de Dissertation, 25 de CombatSafety y 25 de TriggerEvents. **Pendiente:** carga y combate dentro del juego, pruebas de balance v2, revisión lingüística y encaje visual del arte dentro de Unity.

<!-- 2026-10-10-0024||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||README actualizado con compilación, pruebas y despliegue local -->

<!-- 2026-10-10-0112||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||README 0.1.1 con colección, localización y pruebas actuales -->

<!-- 2026-10-11-0005||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||README 0.1.2 con progresión y resultados actuales -->

<!-- 2026-10-11-0011||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||README 0.1.3 con correcciones y pruebas actualizadas -->

<!-- 2026-10-11-0020||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||README 0.1.4 con texto de sendas y contratos actualizados -->

<!-- 2026-10-11-0024||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||README 0.1.5 con preview determinista y resultados actualizados -->

<!-- 2026-10-11-0030||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||README 0.1.6 con banners y regresiones actualizados -->

<!-- 2026-10-11-0038||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||README 0.1.7 con robo nativo y comprobaciones actualizadas -->

<!-- 2026-10-11-0946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Estado 0.1.8 y resultados de revisión de cartas -->

<!-- 2026-10-11-0948||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar comandos del clan a scripts/operaciones tras reorganización externa -->

<!-- 2026-10-11-1035||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión y estado de corrección 0.1.9 -->

<!-- 2026-10-11-1042||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión, pruebas y revisión integral 0.1.10 -->

<!-- 2026-10-11-1106||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar catálogo 0.1.11, resultados y tres ilustraciones pendientes -->

<!-- 2026-10-11-1117||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión y contratos del aura ampliada -->

Textos de cartas: omitir comportamientos por defecto y keywords ya mostradas por la interfaz; conservar excepciones, objetivos y duraciones relevantes. Criterio permanente en CLAUDE.md del workspace.

<!-- 2026-10-11-1442||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión y criterio de textos concisos -->

Los efectos personalizados también se muestran al pasar el ratón sobre la carta y sobre la unidad invocada. Se reutiliza su descripción localizada con valores dinámicos; los tooltips nativos de estados y sendas se conservan.

<!-- 2026-10-11-1446||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión 0.1.14 y corrección de tooltips -->

<!-- 2026-10-11-1504||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión y validación de selección entre turnos -->

<!-- 2026-10-11-1511||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión y contratos del filtro de bando -->

<!-- 2026-10-11-1553||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión y contratos de Credit Recovery -->

<!-- 2026-10-11-1554||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión de Consume and Re-enroll -->

<!-- 2026-10-11-1609||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión de balance -->

<!-- 2026-10-11-1618||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa de 0.1.19 a 0.1.24 -->

<!-- 2026-10-11-1619||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar validación vigente a 403 contratos para Exam Week -->

<!-- 2026-10-11-1622||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa de 0.1.20 a 0.1.24 -->

<!-- 2026-10-11-1623||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar comprobaciones vigentes de retorno sin Ephemeral -->

<!-- 2026-10-11-1623||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa de 0.1.21 a 0.1.24 -->

<!-- 2026-10-11-1624||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar validación vigente del tomo a 405 contratos -->

<!-- 2026-10-11-1629||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.24 por corrección de rarezas de unidades -->

<!-- 2026-10-11-1629||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar validación por ID de rarezas a 456 contratos -->

<!-- 2026-10-11-1631||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.24 por Archivera de tres mazos -->

<!-- 2026-10-11-1632||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar comprobaciones de Archivera a 460 contratos -->

<!-- 2026-10-11-1633||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión vigente a 0.1.25 para reservas visibles -->

<!-- 2026-10-11-1634||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar resultados vigentes a 463 contratos y 223 términos -->

<!-- 2026-10-11-1637||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión vigente a 0.1.26 -->

<!-- 2026-10-11-1637||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar resultados activos a 475 contratos y 225 términos -->

<!-- 2026-10-11-1651||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión vigente a 0.1.27 -->

<!-- 2026-10-11-1652||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar validación vigente a 476 contratos -->

<!-- 2026-10-11-1652||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión vigente a 0.1.28 -->

<!-- 2026-10-11-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión vigente a 0.1.29 -->

<!-- 2026-10-11-1657||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar validación vigente a 479 contratos -->

<!-- 2026-10-11-1700||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión y regresiones de objetivos de hechizos -->

<!-- 2026-10-11-1701||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión vigente a 0.1.31 -->

<!-- 2026-10-11-1734||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión y regresiones de starters -->

<!-- 2026-10-11-1742||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión preparada y regresiones de revisión documental -->

<!-- 2026-10-11-1743||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar resultados de localización y selectores -->

<!-- 2026-10-11-1800||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión preparada y contratos de Pierce -->

<!-- 2026-10-11-1813||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión vigente a 0.1.35 -->

<!-- 2026-10-11-1816||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión a 0.1.36 para copia de Postdoc -->

<!-- 2026-10-11-1829||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión a 0.1.37 con terminología de tamaño oficial -->

<!-- 2026-10-11-1835||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión a 0.1.38 con U17 de 1 pip y texto E02 -->

<!-- 2026-10-11-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión a 0.1.39 con filtro de mejora e icono de sala -->

<!-- 2026-10-11-2108||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión a 0.1.40 con marcador de Arcane Liability Insurance -->

<!-- 2026-10-11-2127||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.41 con cambios aprobados en tres hechizos -->

<!-- 2026-10-11-2130||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.42 con Conduit del Dean y medicina adicional -->

<!-- 2026-10-12-0021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.43 con estadísticas y Trample aprobados -->

<!-- 2026-10-12-0036||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.44 con Desk Swap aprobado -->

<!-- 2026-10-12-0037||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.45 con limpieza de actually y equivalentes -->

<!-- 2026-10-12-0053||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.46 con guardia de bono cero en Chair -->

<!-- 2026-10-12-0055||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.47 con corrección de daño base de Consumed Bibliography -->

<!-- 2026-10-12-0100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.48 con revisión editorial y S22 recarga 1 -->

<!-- 2026-10-12-0105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.49 con bono de Bibliography en línea separada -->

<!-- 2026-10-12-0111||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.50 con redirección de hechizos Both -->

<!-- 2026-10-12-0915||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.51 con cinco cambios aprobados -->

<!-- 2026-10-12-0920||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.52 con Harvest del Emérito -->

<!-- 2026-10-12-0923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.53 con pasivas médicas del primer hechizo apto -->

<!-- 2026-10-12-0925||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.54 con Desk Swap sin Dazed -->

<!-- 2026-10-12-0927||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.55 con Reserve +5 Power conservado al jugar -->

<!-- 2026-10-12-0932||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.56 con omisión de comportamientos por defecto -->

<!-- 2026-10-12-0938||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.57 con carta retirada y control de defaults -->

<!-- 2026-10-12-0947||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión 0.1.58 y distinguir cartas activas de retiradas y auxiliares -->

<!-- 2026-10-12-0948||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Corregir total activo a 51 incluyendo 2 campeones y 2 starters -->

<!-- 2026-10-12-1021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.59 por corrección del texto de Archivera -->

<!-- 2026-10-12-1022||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.60 con nombres propios de habilidades -->

<!-- 2026-10-12-1303||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.61 por vista previa de Trabajo en Pareja -->

<!-- 2026-10-12-1427||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.62 por espera y resolución de selectores de mano -->

<!-- 2026-10-12-1430||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.63 por duración de propiedades S32 -->

<!-- 2026-10-12-1438||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.64 por Faculty Robe libre y Postdoc global -->

<!-- 2026-10-12-1543||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.65 por Postdoc de hechizo común -->

<!-- 2026-10-12-1554||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.66 por Postdoc Extinguish y Undying -->

<!-- 2026-10-12-1637||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.67 por simplificación de sendas -->

<!-- 2026-10-12-1639||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.68 sin Conduit repetido en sendas -->

<!-- 2026-10-12-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.69 por selección nativa sin bloqueo -->

<!-- 2026-10-12-1712||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local a 0.1.70 -->

<!-- 2026-10-12-1713||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar marca de versión local sin formato Markdown -->

<!-- 2026-10-12-1740||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local a 0.1.71 -->

<!-- 2026-10-12-1758||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.72 -->

<!-- 2026-10-12-1808||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.73 -->

<!-- 2026-10-12-1815||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.74 -->

<!-- 2026-10-12-2053||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.75 -->

<!-- 2026-10-12-2056||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.76 -->

<!-- 2026-10-12-2058||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.77 -->

<!-- 2026-10-12-2102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.78 -->

<!-- 2026-10-12-2103||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.79 -->

<!-- 2026-10-12-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.80 -->

<!-- 2026-10-12-2115||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.81 -->

<!-- 2026-10-12-2200||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión local 0.1.82 -->
<!-- 2026-10-12-2211||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión local 0.1.83; efectos Overheal reutilizables -->
<!-- 2026-10-12-2232||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.84 con sendas Dean Arcane Slay -->
<!-- 2026-10-12-2235||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.85: habilidades sin descripción duplicada en portadores -->
<!-- 2026-10-12-2237||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.86: Consumable Credits Intern Mageblade 2 -->
<!-- 2026-10-12-2242||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.87 con tercer equipo Tenured Wand -->
<!-- 2026-10-12-2245||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.88 con Faculty Robe retirada y 51 cartas activas -->
<!-- 2026-10-12-2255||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.89 con cambios S09/S10 -->
<!-- 2026-10-12-2258||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.90 con retirada Credit Recovery y Spike nativo -->
<!-- 2026-10-12-2300||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.91 tras retirar seis hechizos y convertir Practicum a reliquia -->
<!-- 2026-10-12-2313||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.92: Arcane Liability Insurance infrecuente -->
<!-- 2026-10-12-2322||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.93 con rarezas y efectos aprobados -->
<!-- 2026-10-12-2330||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.94 con ocho reliquias comunes y dos infrecuentes -->
<!-- 2026-10-13-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.95 con marca y contador de Dissertation -->
<!-- 2026-10-13-0031||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.96 con estado visible de Concussion Scholarship -->
<!-- 2026-10-13-0033||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.97 con IDs registrados de estados personalizados -->
<!-- 2026-10-13-0051||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.98 con Scholarship dirigido a pila de robo -->
<!-- 2026-10-13-0054||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión activa 0.1.99 por corrección de sprites de estados personalizados -->
<!-- 2026-10-13-0106||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.100 -->
<!-- 2026-10-13-0108||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.101 -->
<!-- 2026-10-13-2124||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.102 -->
<!-- 2026-10-13-2131||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.103 -->
<!-- 2026-10-13-2134||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.104 -->
<!-- 2026-10-13-2138||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.105 -->
<!-- 2026-10-13-2144||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.106 -->
<!-- 2026-10-13-2257||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.107 -->
<!-- 2026-10-13-2353||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.108 -->
<!-- 2026-10-14-0021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.109 -->
<!-- 2026-10-14-0022||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.110 -->
<!-- 2026-10-14-0038||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.111 -->
<!-- 2026-10-14-2150||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.112 -->
<!-- 2026-10-14-2156||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.113 -->
<!-- 2026-10-14-2216||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.114 -->
<!-- 2026-10-14-2237||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.115 -->
<!-- 2026-10-15-2142||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa 0.1.118 -->
<!-- 2026-10-15-2226||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.118 para vista previa de Education en mano -->
<!-- 2026-10-15-2239||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.118 con nuevo nombre de U13 -->
<!-- 2026-10-17-0910||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.119 con Lethal Tuition Refund repetible cada cinco hechizos letales -->
<!-- 2026-10-17-0922||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.120 con PhD de 1 pip -->
<!-- 2026-10-17-1317||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.121 con PhD restablecido a 2 pips -->

## Protección local de Trample y Explosive

Versión 0.1.129: los callbacks de Trample y Explosive no se reactivan mutuamente en cartas david.wwinc. El impacto inicial conserva ambos efectos. Aplica también a preview y a traits concedidos por Souls; no modifica cartas de otros clanes. Regresión adicional: `dotnet run --project tests/DamageTraits/DamageTraits.csproj` desde este repositorio. El banco prueba prefijos reales con dobles de API y un modelo acotado de callbacks; la combinación Dissertation + Red Hot 3 sigue pendiente de prueba en Unity.
<!-- 2026-10-17-1448||script-corregir-trample-explosive.py||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.122 y alcance de protección de callbacks -->
<!-- 2026-10-19-2239||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.123: Course Withdrawal Spell/Blight y doble Ember conservando parche Trample/Explosive -->
<!-- 2026-10-19-2254||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.124 con filtro de Permanent Withdrawal Stamp corregido -->
<!-- 2026-10-19-2259||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.125 con Withdrawal Stamp y recargas 0/2 -->
<!-- 2026-10-19-2359||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.126 con Withdrawal Stamp solo Purge -->
<!-- 2026-10-20-0000||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.127: Withdrawal Stamp sin recargas, solo Purge por ring -->
<!-- 2026-10-20-0012||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.128: Withdrawal Stamp con recarga inicial 0 y posterior 8 -->
<!-- 2026-10-20-0017||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.129: Withdrawal Stamp permite un Purge independiente por copia y ring -->


## Beta para Thunderstore

Fuentes de la ficha pública en [thunderstore/README.md](thunderstore/README.md), [manifiesto](thunderstore/manifest.json) y [changelog](thunderstore/CHANGELOG.md). La beta conserva 0.1.132 y se empaqueta con DLL/JSON/texturas juntos bajo plugins/. El script local anterior genera un ZIP plano para uso propio; no subirlo como paquete público.

El empaquetador específico es `scripts/operaciones/empaquetar-ww-inc-thunderstore-2026-10-24.py` en la raíz del workspace. Recibe `--report` con el informe deployment de un build validado. El ZIP beta preparado el 24-10-2026, sus hashes y las comprobaciones pendientes están enlazados en la ficha de estado del clan. No se ha publicado ni probado la importación en un perfil limpio.
<!-- 2026-10-24-1621||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar README de desarrollo y enlazar fuentes y flujo del paquete beta -->
<!-- 2026-10-24-1624||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Añadir estructura del repositorio y compilación desde clon; documentar rutas locales -->
<!-- 2026-10-24-1824||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión vigente del código a 0.1.133 -->
<!-- 2026-10-24-1826||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión vigente 0.1.134 -->
<!-- 2026-10-24-1835||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión vigente 0.1.135 -->
<!-- 2026-10-24-1839||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión vigente 0.1.136 -->
<!-- 2026-10-24-1843||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión vigente 0.1.137 -->
<!-- 2026-10-24-1900||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión vigente 0.1.138 -->
<!-- 2026-10-24-1923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión vigente 0.1.139 -->
<!-- 2026-10-24-1930||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión vigente 0.1.140 -->
<!-- 2026-10-24-1943||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión vigente 0.1.141 -->
<!-- 2026-10-24-1946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión vigente 0.1.142 -->
<!-- 2026-10-24-1959||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión vigente 0.1.143 -->
<!-- 2026-10-24-2025||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión vigente 0.1.149 -->
<!-- 2026-10-24-2100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.149: Dissertation admite hechizos de soporte -->
<!-- 2026-10-25-1547||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.149: corregir tooltips repetidos y claves internas sin traducir -->
<!-- 2026-10-25-1557||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.149: presentación de clan, iconos y compatibilidad de mejoras -->
<!-- 2026-10-25-1606||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.149: seguridad de preview de Intern y daño total de Representative -->
<!-- 2026-10-25-1616||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Versión 0.1.149: tooltips de cartas generadas y keywords concedidas -->
<!-- 2026-10-25-1640||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.150 sin reescribir comentarios históricos -->
<!-- 2026-10-25-1743||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa editorial a 0.1.151 sin reescribir historial -->
<!-- 2026-10-25-1832||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.152 por filtro no aleatorio -->
<!-- 2026-10-25-1843||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.153 por redondeo aprobado de Education -->
<!-- 2026-10-25-1853||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Actualizar versión activa a 0.1.154: ataques pares, separación de eventos y signo de tamaño -->
<!-- 2026-10-25-1908||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/README.md||Preparar README público bilingüe con rutas de fuentes actuales, compilación independiente y siete suites; corregir recuentos obsoletos -->
