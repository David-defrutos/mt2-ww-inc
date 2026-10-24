using System;
namespace WwInc;

// Preserve incoming effect damage, which already includes Magic Power and Attuned.
// The native PerCard scaling trait replaces that damage rather than adding to it.
public sealed class CardTraitAcademicConsumedDamage : CardTraitState
{
    public override PropDescriptions CreateEditorInspectorDescriptions()=>PropDescriptions.DisplayNone;
    public override int OnApplyingDamage(ApplyingDamageParameters damageParams,ICoreGameManagers coreGameManagers)
    {
        var tracked=StatValueData;
        tracked.forPreviewText=false;
        int count=coreGameManagers.GetCardStatistics().GetStatValue(tracked);
        return AcademicMath.ConsumedDamage(damageParams.damage,GetParamInt(),count);
    }
}

// 2026-10-12-0055||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ConsumedDamage.cs||Añadir trait de daño consumido aditivo que conserva daño de efecto y Power sin duplicarlo
