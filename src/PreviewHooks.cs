using System.Collections.Generic;
using HarmonyLib;
namespace WwInc;
internal static class PreviewScope
{
    static readonly Stack<CollectionSnapshot> snapshots=new();
    internal static bool Active=>snapshots.Count>0;
    internal static void Begin()
    {
        snapshots.Push(new CollectionSnapshot(typeof(AcademicRuntime),typeof(SpellRules),typeof(SocialRules),typeof(FrozenTuition),typeof(EffectLifetimes)));
        AcademicRuntime.Frames.Clear();
    }
    internal static void End()
    {if(snapshots.Count>0)snapshots.Pop().Restore();}
}
// Both battle prediction and card previews toggle the native SaveManager flag.
[HarmonyPatch]
internal static class PreviewStateHook
{
    static System.Reflection.MethodBase TargetMethod()=>AccessTools.PropertySetter(typeof(SaveManager),nameof(SaveManager.PreviewMode));
    static void Prefix(SaveManager __instance,bool value,out bool __state)
    {
        __state=__instance.PreviewMode!=value;
        if(__state&&value)PreviewScope.Begin();
    }
    static void Postfix(bool value,bool __state){if(__state&&!value)PreviewScope.End();}
    static void Finalizer(bool __state,System.Exception __exception)
    {if(__state&&__exception!=null)PreviewScope.End();}
}
// Custom passives can affect a different floor (U17, auras, repeated spells).
// Collect these before native stats are captured, so every affected character has a temporary copy.
[HarmonyPatch(typeof(TargetHelper),nameof(TargetHelper.CollectPreviewTargets))]
internal static class AcademicPreviewTargetsHook
{
    static void Postfix(RoomManager roomManager,ref List<CharacterState> previewTargets)
    {
        if(PreviewDependencies.RequiresExpansion(roomManager))PreviewTargets.Expand(roomManager,previewTargets);
    }
}
// 2026-10-11-0001||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/PreviewHooks.cs||Cubrir predicción de combate completa y copiar unidades afectadas en otros pisos

// CardState is shared by preview and real battle. Preserve its real equipped owner.
[HarmonyPatch(typeof(CardState),nameof(CardState.RemoveEquippedCharacterState))]
internal static class EquipmentPreviewOwnerHook
{
    static bool Prefix()=>AcademicRuntime.EquipmentPreviewDepth==0;
}
// 2026-10-11-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/PreviewHooks.cs||Proteger propietario real del equipo durante S24 y cubrir personajes exteriores en preview
// 2026-10-25-2036||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/PreviewHooks.cs||Incluir estado extraído de Frozen Tuition en la misma instantánea de preview
// 2026-10-27-1428||codex-ww-inc||salidas/build/ww-inc/src/PreviewHooks.cs||Usar contexto actual y unidades visibles válidas al ampliar preview; evitar asserts fuera de combate
// 2026-10-27-1534||codex-ww-inc||salidas/build/ww-inc/src/PreviewHooks.cs||No ampliar todos los pisos en partidas sin cartas/pasivas W&W presentes
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/PreviewHooks.cs||Preservar colas de expiración extraídas al entrar y salir de preview
