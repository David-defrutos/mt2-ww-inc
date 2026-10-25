using MonsterTrain2.SharedTriggers;
using System;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using TrainworksReloaded.Core;
using TrainworksReloaded.Core.Extensions;
using TrainworksReloaded.Base;

namespace WwInc;

[BepInPlugin(Guid, "W&W Inc.", "0.1.154")]
[BepInDependency("TrainworksReloaded.Plugin")]
[BepInDependency("Conductor")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "david.wwinc";
    internal static Plugin Instance = null!;
    public void Awake()
    {
        Instance = this;
        Conductor.Data.Registers.SaveDataRegistry.Register(Guid,new ExcessCreditSave());
        string root = Path.GetDirectoryName(Info.Location)!;
        var files = Directory.GetFiles(Path.Combine(root, "json"), "*.json", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => p.Substring(root.Length + 1).Replace('\\', '/')).ToArray();
        if(files.Length==0) throw new InvalidOperationException("W&W Inc. has no JSON content.");
        Railhead.GetBuilder().Configure(Guid, c => c.AddMergedJsonFile(files.ToList()));
        // Conductor extends the native exact-name upgrade masks; inheritance alone is insufficient.
        Railend.ConfigurePostAction(_=> {
            Conductor.Utilities.MarkEffectAsADamageEffect(typeof(CardEffectAcademicAuthoredDamage));
            Conductor.Utilities.MarkEffectAsHealEffect(typeof(CardEffectAcademicSabbatical));
        });
        Roundhouse.WhenReady(Guid,ctx=> {
            var missingCards=new[]{"WW_C01","WW_C02","WW_T01","WW_T02","WW_U01","WW_U02","WW_U03","WW_U04","WW_U05","WW_U06","WW_U08","WW_U10","WW_U11","WW_U13","WW_U16","WW_U17","WW_U18","WW_S01","WW_S02","WW_S03","WW_S04","WW_S05","WW_S06","WW_S07","WW_S08","WW_S09","WW_S10","WW_S11","WW_S12","WW_S13","WW_S14","WW_S15","WW_S16","WW_S17","WW_S18","WW_S19","WW_S20","WW_S21","WW_S22","WW_S23","WW_S24","WW_S25","WW_S26","WW_E01","WW_E02","WW_E03","WW_R01","WW_R02","WW_S27","WW_S28","WW_S29","WW_S30","WW_S31","WW_S32"}.Where(id=>ctx.GetCard(id)==null).ToArray();
            var missingRelics=new[]{"WW_A01","WW_A02","WW_A03","WW_A04","WW_A05","WW_A06","WW_A07","WW_A08","WW_A09","WW_A10"}.Where(id=>ctx.GetRelic(id)==null).ToArray();
            if(missingCards.Length>0||missingRelics.Length>0||ctx.GetClass("WW_Class")==null)
                throw new InvalidOperationException("W&W Inc. incomplete registration: "+string.Join(",",missingCards.Concat(missingRelics)));
            var purgeMask=ctx.GetCardUpgradeMask("WW_E01_PurgeableMask");
            var permitEffect=ctx.GetCard("WW_E01_Ability").GetEffects()[0];
            if(purgeMask==null||permitEffect.GetParamCardFilter()!=purgeMask)
                throw new InvalidOperationException("W&W Inc.: Expulsion Permit card filters were not resolved.");
            Logger.LogInfo("W&W Inc. DATA READY: 43 active cards, 10 relics, 4 tokens; localized content finalized.");
            ArcaneSlayEvent.Trigger=ctx.GetCharacterTriggerEnum("WW_ArcaneSlay");
            MineMineMine.Trigger=ctx.GetCharacterTriggerEnum("WW_MineMineMine");
            OverdamageEvent.Trigger=ctx.GetCharacterTriggerEnum("WW_Overdamage");
            OverhealEvent.Trigger=ctx.GetCharacterTriggerEnum("WW_Overheal");
            SpellRules.HitDiscountUpgrade=ctx.GetCardUpgrade("WW_S09_Discount");
            StatusEffectAcademicScholarship.Id=ctx.GetStatusEffect("ww_scholarship").GetStatusId();
            DissertationMarker.StatusId=ctx.GetStatusEffect("ww_dissertation").GetStatusId();
            DissertationTargeting.AllyChoice=ctx.GetCard("WW_Dissertation_Allies");
            DissertationTargeting.EnemyChoice=ctx.GetCard("WW_Dissertation_Enemies");
            CateringRules.Stand=ctx.GetCharacter("WW_Token_IceCreamStand");
            CateringRules.Serving=ctx.GetCharacter("WW_Token_IceCream");
            SocialRules.Chaperon=ctx.GetCharacter("WW_Token_Chaperon");
            SocialRules.Chaperona=ctx.GetCharacter("WW_Token_Chaperona");
            SocialRules.Wizard=ctx.GetSubtype("WW_Wizard");SocialRules.Witch=ctx.GetSubtype("WW_Witch");SocialRules.Chaperone=ctx.GetSubtype("WW_Chaperone");
        });
        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo("W&W Inc.: academic mechanics 0.1.154 loaded; local Trample/Explosive guard enabled");
    }
    internal static void Log(string message) => Instance.Logger.LogInfo(message);
}
// 2026-10-10-0003||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Correcciones de resolución, traits y acumulación de reglas
// 2026-10-10-0058||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Resolver los personajes token del puesto y sus helados
// 2026-10-10-0109||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión local 0.1.5 con puesto y traducciones
// 2026-10-10-0111||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Diagnóstico de catálogo finalizado tras registrar Trainworks para el próximo inicio
// 2026-10-10-2358||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.5 con campeones y simulación aislada
// 2026-10-11-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.5 con recuperación segura y previsualización de equipos

// 2026-10-11-0019||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión local de corrección 0.1.5

// 2026-10-11-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión local 0.1.5 para soporte seguro de preview

// 2026-10-11-0030||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.6 con subtipos nativos de banner

// 2026-10-11-0038||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.7 con robo nativo del decano

// 2026-10-11-0943||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión local 0.1.8 de auditoría de cartas

// 2026-10-11-1032||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.9 con reconocimiento de sendas al invocar

// 2026-10-11-1032||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Diagnóstico de carga actualizado a versión 0.1.9

// 2026-10-11-1039||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.10 de revisión de interacciones

// 2026-10-11-1104||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Registrar catálogo de 51 cartas y versión 0.1.24

// 2026-10-11-1117||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.24

// 2026-10-11-1442||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión a 0.1.24 por revisión de textos

// 2026-10-11-1445||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión de tooltips a 0.1.24

// 2026-10-11-1503||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión de redirección a 0.1.24

// 2026-10-11-1510||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión de filtro de bando a 0.1.24

// 2026-10-11-1552||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión Credit Recovery a 0.1.24

// 2026-10-11-1554||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión del retorno sin coste extra a 0.1.24

// 2026-10-11-1609||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar balance y rarezas a versión 0.1.24

// 2026-10-11-1618||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa de 0.1.19 a 0.1.24

// 2026-10-11-1622||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa de 0.1.20 a 0.1.24

// 2026-10-11-1623||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa de 0.1.21 a 0.1.24

// 2026-10-11-1629||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.24 por corrección de rarezas de unidades

// 2026-10-11-1631||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.24 por Archivera de tres mazos

// 2026-10-11-1633||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa del plugin a 0.1.25

// 2026-10-11-1637||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.26

// 2026-10-11-1651||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.27

// 2026-10-11-1652||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.28

// 2026-10-11-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.29

// 2026-10-11-1700||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión vigente a 0.1.30

// 2026-10-11-1701||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión vigente a 0.1.31

// 2026-10-11-1734||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión vigente a 0.1.32

// 2026-10-11-1742||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión vigente a 0.1.33

// 2026-10-11-1800||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión preparada a 0.1.34

// 2026-10-11-1813||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión a 0.1.35 para corrección de tooltip de Awkward Silence

// 2026-10-11-1816||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión a 0.1.36 para copia de Postdoc

// 2026-10-11-1829||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión a 0.1.37 con terminología de tamaño oficial

// 2026-10-11-1835||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión a 0.1.38 con U17 de 1 pip y texto E02

// 2026-10-11-2029||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión a 0.1.39 con filtro de mejora e icono de sala

// 2026-10-11-2108||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión a 0.1.40 con marcador de Arcane Liability Insurance

// 2026-10-11-2127||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.41 con cambios aprobados en tres hechizos

// 2026-10-11-2130||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.42 con Conduit del Dean y medicina adicional

// 2026-10-12-0021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.43 con estadísticas y Trample aprobados

// 2026-10-12-0036||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.44 con Desk Swap aprobado

// 2026-10-12-0037||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.45 con limpieza de actually y equivalentes

// 2026-10-12-0053||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.46 con guardia de bono cero en Chair

// 2026-10-12-0055||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.47 con corrección de daño base de Consumed Bibliography

// 2026-10-12-0100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.48 con revisión editorial y S22 recarga 1

// 2026-10-12-0105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.49 con bono de Bibliography en línea separada

// 2026-10-12-0111||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.50 con redirección de hechizos Both

// 2026-10-12-0915||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Validar carga del nuevo hechizo S31

// 2026-10-12-0915||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.51 con cinco cambios aprobados

// 2026-10-12-0920||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.52 con Harvest del Emérito

// 2026-10-12-0923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.53 con pasivas médicas del primer hechizo apto

// 2026-10-12-0925||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.54 con Desk Swap sin Dazed

// 2026-10-12-0927||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.55 con Reserve +5 Power conservado al jugar

// 2026-10-12-0932||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.56 con omisión de comportamientos por defecto

// 2026-10-12-0938||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.57 con carta retirada y control de defaults

// 2026-10-12-0946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Registrar S32 y versión 0.1.58

// 2026-10-12-1021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.59 por corrección del texto de Archivera

// 2026-10-12-1022||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.60 con nombres propios de habilidades

// 2026-10-12-1303||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.61 por vista previa de Trabajo en Pareja

// 2026-10-12-1427||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.62 por espera y resolución de selectores de mano

// 2026-10-12-1430||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.63 por duración de propiedades S32

// 2026-10-12-1438||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.64 por Faculty Robe libre y Postdoc global

// 2026-10-12-1543||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.65 por Postdoc de hechizo común

// 2026-10-12-1554||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.66 por Postdoc Extinguish y Undying

// 2026-10-12-1637||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.67 por simplificación de sendas

// 2026-10-12-1639||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.68 sin Conduit repetido en sendas

// 2026-10-12-1656||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa 0.1.69 por selección nativa sin bloqueo

// 2026-10-12-1712||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.70: simplificar hechizos con componentes existentes

// 2026-10-12-1740||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.71: Practicum restaura 4 a todas las unidades

// 2026-10-12-1758||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.72: Deferred Project vuelve con Freeze y bloqueo temporal

// 2026-10-12-1808||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.73: Emeritus convierte exceso a Armor

// 2026-10-12-1815||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.74: Expulsion Permit para cualquier carta elegible

// 2026-10-12-2053||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.75: Hostile Medicine convierte exceso a daño en área

// 2026-10-12-2056||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.76: PhD reparte curación entre aliados

// 2026-10-12-2058||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.77: Conduit Credit Transfer infrecuente

// 2026-10-12-2102||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.78: Permanent Withdrawal Stamp infrecuente

// 2026-10-12-2103||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.79: filtro enemigo de Enchanted Dissertation

// 2026-10-12-2105||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.80: Strictly Platonic usa dúos Wizard–Witch

// 2026-10-12-2115||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.81: textos Deferred Project y Faculty Robe

// 2026-10-12-2200||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Resolver enum de Arcane Slay vía Trainworks; versión activa 0.1.82
// 2026-10-12-2211||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Registrar enum Overheal vía Trainworks y versión 0.1.83
// 2026-10-12-2232||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.84 con sendas Dean Arcane Slay
// 2026-10-12-2235||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.85: habilidades sin descripción duplicada en portadores
// 2026-10-12-2237||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.86: Consumable Credits Intern Mageblade 2
// 2026-10-12-2242||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Registrar WW_E03 y actualizar versión 0.1.87 y diagnóstico de 52 cartas
// 2026-10-12-2245||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.88 con Faculty Robe retirada y 51 cartas activas
// 2026-10-12-2255||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Resolver mejora Scholarship con Trainworks y versión activa 0.1.89
// 2026-10-12-2258||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.90 con retirada Credit Recovery y Spike nativo
// 2026-10-12-2300||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Registrar reliquia A10 y versión activa 0.1.91 con 44 cartas y 10 reliquias
// 2026-10-12-2313||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.92: Arcane Liability Insurance infrecuente
// 2026-10-12-2322||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.93 con rarezas y efectos aprobados
// 2026-10-12-2330||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.94 con ocho reliquias comunes y dos infrecuentes
// 2026-10-13-0023||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.95 con marca y contador de Dissertation
// 2026-10-13-0031||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.96 con estado visible de Concussion Scholarship
// 2026-10-13-0033||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Resolver IDs completos de ambos estados personalizados vía Trainworks; versión 0.1.97
// 2026-10-13-0051||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.98 con Scholarship dirigido a pila de robo
// 2026-10-13-0054||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión activa 0.1.99 por corrección de sprites de estados personalizados
// 2026-10-13-0106||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.100: retorno de hechizo vinculado
// 2026-10-13-0108||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.101: Scholarship rebaja cualquier carta
// 2026-10-13-2124||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.102: tomo coste 2 y reliquia sin adyacencia
// 2026-10-13-2131||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.103: simplificar texto de Office of Complaints
// 2026-10-13-2134||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.104: General Examination solo enemigos
// 2026-10-13-2138||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.105: Spell Tuning permanente al hechizo que mata
// 2026-10-13-2144||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.106: corregir activación de Spell Tuning
// 2026-10-13-2257||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.107: coste 0 de Postdoc persiste entre turnos
// 2026-10-13-2353||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.108: separar Freeze y pasiva de Frozen Tuition
// 2026-10-14-0021||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.109: Awkward Silence +15
// 2026-10-14-0022||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.110: renombrar Explosive Requisition
// 2026-10-14-0038||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.111: Triple Impact Exam infrecuente con Trample
// 2026-10-14-2150||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.112: Triple Impact Exam daño 1
// 2026-10-14-2156||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.113: Inspector aplica Spell Weakness con Strike
// 2026-10-14-2216||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.114: Inspector ataque 5
// 2026-10-14-2237||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.115: mover Trample a Dissertation y Triple Impact común
// 2026-10-15-2142||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Resolver mejora de próximo uso del Dean y subir versión a 0.1.118
// 2026-10-15-2226||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.118 para vista previa de Education en mano
// 2026-10-15-2239||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.118 con nuevo nombre de U13
// 2026-10-17-0910||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.119 con Lethal Tuition Refund repetible cada cinco hechizos letales
// 2026-10-17-0922||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.120 con PhD de 1 pip
// 2026-10-17-1317||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.121 con PhD restablecido a 2 pips
// 2026-10-17-1448||script-corregir-trample-explosive.py||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.122 con protección local de Trample/Explosive
// 2026-10-19-2239||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.123: Course Withdrawal Spell/Blight y doble Ember conservando parche Trample/Explosive
// 2026-10-19-2254||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.124 y verificar que la máscara de Purge existe y está enlazada al efecto
// 2026-10-19-2259||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.125 y comprobar enlace de ambas máscaras al cargar
// 2026-10-19-2359||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.126 y retirar dependencia de máscara Consume manteniendo comprobación de filtro Purge
// 2026-10-20-0000||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.127: Withdrawal Stamp sin recargas, solo Purge por ring
// 2026-10-20-0012||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.128: Withdrawal Stamp con recarga inicial 0 y posterior 8
// 2026-10-20-0017||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.129: Withdrawal Stamp permite un Purge independiente por copia y ring
// 2026-10-21-2043||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.130: ignorar curaciones >=9999 para Overheal
// 2026-10-21-2059||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.131: límite de armadura por Overheal
// 2026-10-21-2245||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.132: Course Withdrawal acepta Scourge
// 2026-10-24-1824||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.133: campeona Witch recibe Overheal de daño
// 2026-10-24-1826||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.134: Protective Tenure otorga Armor y Spikes
// 2026-10-24-1835||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.135: escala Queen Bee +1/+2/+3
// 2026-10-24-1839||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Resolver plantilla de Extra Credit mediante Trainworks
// 2026-10-24-1839||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.136: Advanced Spellwork genera Extra Credit atribuido a la campeona
// 2026-10-24-1843||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Registrar enum Overdamage mediante Trainworks
// 2026-10-24-1843||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.137: trigger Overdamage reutilizable
// 2026-10-24-1900||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.138: ilustración propia de Extra Credit
// 2026-10-24-1923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Registrar persistencia de autoría con API Conductor instalada
// 2026-10-24-1923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.139: Crédito Extra acumulable de coste 0 Frozen/Purge
// 2026-10-24-1930||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Registrar trigger MineMineMine mediante Trainworks
// 2026-10-24-1930||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.140: trigger MineMineMine reutilizable de autoría de bajas
// 2026-10-24-1943||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.141: MineMineMine en todas las sendas Witch y Overheal solo en protección
// 2026-10-24-1946||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.142: Advanced Spellwork 100% con progresión de keywords
// 2026-10-24-1959||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.143: restaurar Incant de ataque junto a Overdamage
// 2026-10-24-2025||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.149: corregir Spikes del Incant de protección
// 2026-10-24-2100||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.149 y resolver dos opciones auxiliares de destino de Dissertation
// 2026-10-25-1547||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.149: corregir tooltips repetidos y claves internas sin traducir
// 2026-10-25-1557||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Registrar daño de Extra Credit y curación Sabbatical con helpers Conductor en postaction; versión 0.1.149
// 2026-10-25-1606||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.149: seguridad de preview de Intern y daño total de Representative
// 2026-10-25-1616||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Versión 0.1.149 y retirar dependencia global de plantilla de Extra Credit
// 2026-10-25-1640||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.150 sin reescribir comentarios históricos
// 2026-10-25-1643||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Eliminar referencia global de mejora de Requisition ya declarada en su efecto
// 2026-10-25-1743||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa editorial a 0.1.151 sin reescribir historial
// 2026-10-25-1832||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.152 por filtro no aleatorio
// 2026-10-25-1843||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.153 por redondeo aprobado de Education
// 2026-10-25-1849||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Conectar consumidores al núcleo independiente con managers explícitos y guardias W&W conservadas
// 2026-10-25-1853||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/Plugin.cs||Actualizar versión activa a 0.1.154: ataques pares, separación de eventos y signo de tamaño
