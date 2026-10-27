namespace WwInc;

// Freeze is removed before native post-play relic tests. Only that timing bridge is custom.
public sealed class RelicEffectAcademicFrozenDraw : RelicEffectBase
{
    public override PropDescriptions CreateEditorInspectorDescriptions()=>PropDescriptions.DisplayNone;

    internal void OnFrozenCardPlayed(ICoreGameManagers core)
    {
        if(core.GetSaveManager().PreviewMode||core.GetCombatManager().IsRunningCardPlayPreview)return;
        var statistics=core.GetCardStatistics();
        if(!AreConditionsTrue(statistics))return;
        OnTrigger(statistics); // Native per-turn conditions own counting, reset and copying.
        core.GetCardManager().DrawCards(_srcRelicEffectData.GetParamInt());
        NotifyRelicTriggeredScreenCenter(core.GetRelicManager());
    }
}
// 2026-10-25-2140||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/FrozenEnrollmentScholarship.cs||Usar contador y condiciones nativos para primera carta Frozen; solo puente temporal propio
