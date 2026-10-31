namespace WwInc;

// Text generation also runs in menus and after the main scene is unloaded.
// Root accessors are nullable and do not assert; ICoreGameManagers accessors do.
internal static class BattlePresentationContext
{
    internal static bool TryGet(out ICoreGameManagers core)
    {
        core=null!;
        var all=AllGameManagers.Instance;
        if(all==null||!all.AreMainSceneOnlyManagersInitialized()||all.GetSaveManager()?.GetGameSequence()!=SaveData.GameSequence.InBattle||
            all.GetCombatManager()==null||all.GetCardManager()==null||all.GetRoomManager()==null)return false;
        core=all.GetCoreManagers();
        return true;
    }
}
// 2026-10-27-0007||codex-ww-inc||salidas/build/ww-inc/src/BattlePresentationContext.cs||Bloquear consultas de presentación antes de inicializar gestores; usar accessors nullable raíz y contexto actual
// 2026-10-27-2203||codex-ww-inc||salidas/build/ww-inc/src/BattlePresentationContext.cs||Rechazar mapa aunque conserve gestores del escenario principal; exigir fase nativa IsInBattle
// 2026-10-29-2037||codex-ww-inc||salidas/build/ww-inc/src/BattlePresentationContext.cs||Excluir recompensas además de mapa: IsInBattle nativo también incluye BattlePostCombatRewards
