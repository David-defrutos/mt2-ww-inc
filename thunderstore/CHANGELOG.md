<!-- Documento generado el 2026-10-24-1621 -->
# Changelog

## 0.1.154 — Even Attack, shared trigger cores and size notification

- Applied approved even base Attack values. Dean paths use nominal Attack 6/10/16; the Professor’s middle Advanced Spellwork and Protective Tenure ranks use 16 Attack. Other stats and Incant bonuses are unchanged.
- Extracted Overheal and Arcane Slay event transport into independently compiling source with no W&W or Conductor dependency. W&W reward effects, limits and attribution policies remain separate.
- Enchanted Dissertation, Collective Assessment and Interdepartmental Damage Memo now use dedicated effect classes instead of the legacy string dispatcher.
- Spatial Credit Transfer retains its native size notification, displaying −1 rather than +−1. Only its own negative size upgrade is affected.
- Ataques pares aprobados, núcleos de triggers independientes, efectos activos separados y corrección acotada del signo de tamaño. Pendiente comprobación en partida.

## 0.1.153 — Education rounding

- Professor of Magical Education rounds half the front friendly unit’s Attack up. Its shared calculation covers preview, displayed spell values and resolution.
- No rounding explanation was added to card text. Other half-value mechanics, base statistics and Conduit behavior are unchanged.
- Education redondea hacia arriba la mitad del ataque delantero, sin añadir la regla al texto de la carta. Representative conserva el exceso al matar por decisión del usuario.

## 0.1.152 — Non-random Dissertation

- Enchanted Dissertation excludes spells with random targets through the native HasRandomTarget API, including overrides on custom effects.
- Eligibility is rechecked before binding and before casting a reserved spell. The card states non-random in all ten languages.
- Dissertation excluye hechizos de objetivos aleatorios mediante la API nativa, antes de vincular y de repetir. Texto actualizado en diez idiomas.

## 0.1.151 — Shorter card text / Textos más breves

- Simplified approved card, relic, champion path and keyword descriptions across all ten languages.
- Removed redundant local-floor wording and repeated explanations while retaining exceptional duration, global scope, limits and permanent upgrades.
- Enchanted Dissertation keeps targeting details in its existing state tooltip. Committee for Improper Relations now has a shorter body and a separate native tooltip for its summoned chaperones.
- Updated Blight/Scourge translations against the official glossary. Gameplay parameters are unchanged.

- Aplicadas las propuestas editoriales aprobadas en diez idiomas, omitiendo el piso local por defecto y conservando excepciones reales.
- Dissertation mantiene los detalles de objetivos en su estado. Committee usa un cuerpo breve y un tooltip de invocación con estadísticas, variante y desaparición.
- Tipos Ruina y Azote contrastados con el glosario oficial. Sin cambios de mecánica.

## 0.1.150 — Selection and tooltip fixes / Selectores y tooltips

- Invalid cards in hand selectors now receive the native disabled state and Not Eligible indication, including Enchanted Dissertation and Credit Conversion.
- Enchanted Dissertation rejects spells containing dialog effects and rechecks before reserving or casting them during an attack.
- Custom effects that open card selection now declare the native dialog interface; noninteractive rules retain their existing behavior.
- Added native Magic Power explanations to relevant cards, champion upgrades, the room and Chair of Magical Strength. Explosive Requisition now exposes and reads its granted upgrade through its effect parameters.
- No balance, targeting-scope or keyword redesigns. Local code and schema validation do not replace an in-game UI check.

- Los selectores marcan las cartas no elegibles con el estado nativo, incluidos Enchanted Dissertation y Credit Conversion.
- Dissertation rechaza hechizos con diálogos y vuelve a comprobarlo antes de apartarlos o lanzarlos durante ataques.
- Los efectos personalizados que abren selectores declaran la interfaz nativa; las reglas sin selección conservan su comportamiento.
- Explicación nativa de Magic Power en cartas, mejoras de campeón, sala y Chair of Magical Strength. Explosive Requisition declara y lee la mejora concedida desde sus parámetros.
- Sin cambios de balance, alcance o rediseño de keywords. Pendiente comprobación visual en partida.

## 0.1.149 — Generated card and granted keyword tooltips

- Advanced Spellwork now exposes Extra Credit through its configured generation pool, enabling the native generated-card tooltip.
- Added native explanations for traits and statuses granted by custom effects, including Attuned, Explosive, Ephemeral, Stasis, Armor, Dazed, and Conduit.
- Generation and grants now read their card/trait/status references from JSON. No generic duplicate spell-description tooltip is restored.

- Advanced Spellwork declara Crédito Extra en su pool de generación y muestra el tooltip nativo de la carta generada.
- Explicaciones nativas de rasgos y estados concedidos por efectos C#: Attuned, Explosive, Ephemeral, Stasis, Armor, Dazed y Conduit.
- Generación y concesión leen sus referencias de carta/rasgo/estado desde JSON. No se recupera el tooltip genérico que repetía la descripción del hechizo.

## 0.1.148 — Preview safety and spell damage / Previsualización y daño

- Lightning Rod Intern no longer refunds real Ember during card previews. Its damage and Dazed predictions are restricted to temporary character states.
- Ally-only spells such as Immortal Trade no longer trigger redirection or its penalties.
- First Spell Class Representative counts damage to Armor and overkill from the first damage spell each turn, across all floors. Damage Shield still blocks damage. Other damage-to-healing conversions retain their effective-health-damage calculation.

- Lightning Rod Intern ya no devuelve Ember real durante la previsualización. Sus predicciones de daño y Dazed se limitan al estado temporal del personaje.
- Los hechizos exclusivos de aliados, como Immortal Trade, ya no consumen la redirección ni aplican sus penalizaciones.
- First Spell Class Representative cuenta armadura y exceso de daño del primer hechizo de daño del turno, en cualquier piso. Damage Shield sigue bloqueando el daño. Las conversiones de daño a curación mantienen su cálculo de vida efectivamente perdida.

## 0.1.147 — Display and upgrade fixes / Presentación y mejoras

- Enlarged both champion displays in the clan selection screen.
- Added separate 50×50 character and tooltip icons for Concussion Scholarship, Arcane Liability Insurance, and Enchanted Dissertation.
- Extra Credit now registers with Conductor’s damage-spell filters for shop upgrades. One Turn Sabbatical uses native healing scaling: Magic Power increases its flat healing bonus, while its half-missing-health component remains unchanged.

- Ampliadas las imágenes de ambos campeones al seleccionar clan.
- Iconos separados de 50×50 para unidad y tooltip en Concussion Scholarship, Arcane Liability Insurance y Enchanted Dissertation.
- Crédito Extra se registra en los filtros de daño de Conductor para las mejoras de tienda. One Turn Sabbatical usa el escalado de curación nativo: Magic Power mejora la parte fija, sin modificar la mitad de la salud que falta.

## 0.1.146 — Tooltip fixes / Corrección de tooltips

- Removed redundant keyword-style tooltips that repeated the full spell description. Unit passive explanations, abilities, keywords, and status tooltips remain available.
- Fixed untranslated internal tooltip keys on Enrollment Recall. Applied the matching official tooltip settings to Exam Week and spell-recovery effects.

- Eliminados los tooltips de tipo keyword que repetían la descripción completa del hechizo. Se conservan las explicaciones de pasivas, habilidades, keywords y estados.
- Corregidas las claves internas sin traducir de Enrollment Recall. Exam Week y los efectos de recuperación de hechizos usan también la configuración de tooltips de sus equivalentes oficiales.

## 0.1.145 — Beta update

Changes since 0.1.132.

### Professor of Advanced Witchcraft

- All paths gain **MineMineMine**: spell kills on her floor count as her kills when she is the frontmost unit with this trigger. This includes Slay and Savagery credit.
- **Queen Bee:** Mageblade +1/+2/+3 per Wizard in the train.
- **Advanced Spellwork:** Incant grants +5/+10/+20 Attack. Overdamage converts 100% of excess attack damage into Extra Credit at every rank; rank II grants it Attuned, and rank III grants it Attuned and Explosive.
- **Protective Tenure:** Incant grants 3 Armor and 1 Spikes / 6 Armor and 2 Spikes / 12 Armor and 4 Spikes. Fixed an issue that randomly applied only one of the two statuses.
- Her Overheal damage effect is exclusive to Protective Tenure: excess healing deals damage to all enemies on her floor.

### Extra Credit

- New generated damage spell with its own illustration: **0 Ember, Frozen, Purge**.
- Excess damage accumulates on an Extra Credit already in hand. A new card is created only if none is in hand: 20 excess damage followed by 30 becomes one 50-damage card.
- Unplayed copies remain in the run deck between battles. Creator attribution is saved, and their kills count toward the creating champion unless MineMineMine claims them.

### Enchanted Dissertation

- Now accepts beneficial spells as well as offensive spells. Beneficial single-target spells target its bearer; harmful spells target the attacked enemy.
- Keeps each effect’s native team restrictions and area targeting. Ambiguous spells offer an Allies/Enemies choice when bound.
- Its marker displays the bound spells, remaining attacks, and target side. Spells return to the discard pile when the effect ends.
- Beneficial effects can still resolve after the attack kills its enemy target.

Card text and tooltips updated in all ten languages. This remains a beta; balance and in-game interaction testing continue.

---

## 0.1.145 — Actualización de la beta

Cambios desde la 0.1.132.

### Professor of Advanced Witchcraft

- Todas las sendas reciben **MíoMíoMío**: las bajas de hechizos de su piso cuentan como suyas cuando es la primera unidad desde delante con este trigger. Incluye el crédito para Slay y Savagery.
- **Queen Bee:** Mageblade +1/+2/+3 por cada Mago en el tren.
- **Advanced Spellwork:** Incant otorga +5/+10/+20 de ataque. Overdamage convierte el 100% del daño sobrante de sus ataques en Crédito Extra en todos los niveles; el nivel II le concede Attuned y el III, Attuned y Explosive.
- **Protective Tenure:** Incant otorga 3 Armor y 1 Spikes / 6 Armor y 2 Spikes / 12 Armor y 4 Spikes. Corregido el error que aplicaba solo uno de los dos estados al azar.
- Su efecto de Sobrecuración queda exclusivamente en Protective Tenure: la curación sobrante inflige daño a todos los enemigos de su piso.

### Crédito Extra

- Nuevo hechizo generado de daño con ilustración propia: **coste 0, Frozen y Purge**.
- El daño sobrante se acumula en el Crédito Extra que ya esté en la mano. Solo crea una carta nueva si no hay ninguna en mano: 20 de exceso y después 30 producen una sola carta de 50 de daño.
- Las copias sin jugar permanecen en la baraja entre combates. Se guarda su autoría y sus bajas cuentan para la campeona creadora, salvo que MíoMíoMío las reclame.

### Tesis Encantada

- Admite hechizos beneficiosos además de ofensivos. Los beneficiosos de objetivo único se lanzan sobre su portador; los perjudiciales, sobre el enemigo atacado.
- Respeta las restricciones de bando y el alcance de cada efecto. Los hechizos ambiguos permiten elegir Aliados/Enemigos al vincularlos.
- La marca muestra los hechizos vinculados, los ataques restantes y el bando. Los hechizos vuelven al descarte cuando termina el efecto.
- Los efectos beneficiosos pueden ejecutarse aunque el ataque mate al enemigo objetivo.

Textos y tooltips actualizados en los diez idiomas. Continúa siendo una beta; el equilibrio y las interacciones siguen en pruebas.

---

## 0.1.132 — Public beta package

- Two champions with three paths each; 43 active cards and 10 relics.
- Spell-focused combat with Arcane Slay, Overheal, and Wizard–Witch interactions.
- Course Withdrawal can discard a spell, Blight, or Scourge; gain twice its current cost in Ember and draw one card.
- Overheal ignores healing amounts of 9999 or more. Armor generated by Overheal is capped at the unit's current maximum health per activation.
- Includes localized card text in ten languages. Balance and interaction testing continue during beta.

---

## 0.1.132 — Paquete de beta pública

- Dos campeones con tres sendas cada uno; 43 cartas activas y 10 reliquias.
- Combate centrado en hechizos con Arcane Slay, Sobrecuración e interacciones entre Magos y Brujas.
- Course Withdrawal admite hechizos, Blights y Scourges; devuelve el doble de su coste actual en Ember y roba una carta.
- Sobrecuración ignora cantidades de curación de 9999 o más. La armadura generada está limitada a la salud máxima actual de la unidad en cada activación.
- Textos en diez idiomas. El equilibrio y las interacciones siguen en pruebas durante la beta.
<!-- 2026-10-24-1621||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial público bilingüe de beta 0.1.132 -->
<!-- 2026-10-24-1824||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Documentar nuevo Overheal de la campeona en historial público -->
<!-- 2026-10-24-1826||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe del nuevo balance de Protective Tenure -->
<!-- 2026-10-24-1835||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe del ajuste Queen Bee -->
<!-- 2026-10-24-1839||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe de nueva senda Advanced Spellwork -->
<!-- 2026-10-24-1843||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe del trigger Overdamage -->
<!-- 2026-10-24-1900||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe del arte propio de Extra Credit -->
<!-- 2026-10-24-1923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe de acumulación, Purge y persistencia -->
<!-- 2026-10-24-1930||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe de MineMineMine -->
<!-- 2026-10-24-1943||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe de habilidades compartidas y exclusivas de protección -->
<!-- 2026-10-24-1946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe de progresión Advanced Spellwork -->
<!-- 2026-10-24-1959||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe de Incant restaurado -->
<!-- 2026-10-24-2025||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Historial bilingüe de corrección de Spikes -->
<!-- 2026-10-24-2100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Registrar ampliación de Dissertation y elección de bando -->
<!-- 2026-10-24-2204||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Consolidar novedades finales desde beta 0.1.132 en changelog público bilingüe 0.1.145 -->
<!-- 2026-10-25-1547||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Changelog bilingüe de corrección visual 0.1.146 -->
<!-- 2026-10-25-1557||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Changelog bilingüe de escala de selección, variantes de iconos y mejoras nativas -->
<!-- 2026-10-25-1606||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Changelog bilingüe de aislamiento de hover, filtros de redirección y contador separado de daño -->
<!-- 2026-10-25-1616||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Changelog bilingüe de metadatos y tooltips de cartas/rasgos/estados generados -->
<!-- 2026-10-25-1643||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Añadir changelog 0.1.150 con correcciones reales de selección, seguridad de diálogos y referencias de tooltips -->
<!-- 2026-10-25-1743||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Añadir changelog editorial 0.1.151 con alcance y límites de cambios -->
<!-- 2026-10-25-1832||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Documentar exclusión aprobada de hechizos aleatorios sin otros cambios de diseño -->
<!-- 2026-10-25-1843||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Documentar redondeo de Education y ratificación de Representative sin cambio de su código -->
<!-- 2026-10-25-1853||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/thunderstore/CHANGELOG.md||Documentar cambios aprobados y alcance real de refactorización 0.1.154 -->
