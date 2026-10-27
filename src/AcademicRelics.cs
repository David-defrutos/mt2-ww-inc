using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace WwInc;

internal static class AcademicRelics
{
    internal static List<RelicEffectAcademic> Effects(ICoreGameManagers core)
        =>core.GetRelicManager().GetRelicEffects(new List<RelicEffectAcademic>());
    internal static void BeforePower(AcademicRuntime.SpellResolution frame,List<CharacterState> units)
    {foreach(var relic in Effects(AcademicRuntime.Core))relic.BeforePower(frame,units);}
    internal static void BeforeRules(AcademicRuntime.SpellResolution frame,CharacterState? target)
    {foreach(var relic in Effects(AcademicRuntime.Core))relic.BeforeRules(frame,target);}
    internal static void EndTurn(bool damagePlayed)
    {foreach(var relic in Effects(AcademicRuntime.Core))relic.EndTurn(damagePlayed);}
    internal static void UnitDied(bool chaperone,int floor)
    {foreach(var relic in Effects(AcademicRuntime.Core))relic.UnitDied(chaperone,floor);}
    internal static IEnumerator BeginTurn()
    {
        foreach(var relic in Effects(AcademicRuntime.Core).OrderBy(relic=>relic.TurnOrder))
            yield return relic.BeginTurn();
    }
}

// Keep the former binary name for old data/third-party references. New JSON uses concrete effects.
// Legacy ID dispatch is isolated here; normal effects never identify themselves by an ID string.
public class RelicEffectAcademic : RelicEffectBase
{
    private RelicEffectAcademic? Legacy()
    {
        if(GetType()!=typeof(RelicEffectAcademic))return null;
        string id=SourceRelicData?.name??string.Empty;
        if(id.EndsWith("WW_A01",StringComparison.Ordinal))return new RelicEffectStrengthChair();
        if(id.EndsWith("WW_A02",StringComparison.Ordinal))return new RelicEffectConsumeCopy();
        if(id.EndsWith("WW_A04",StringComparison.Ordinal))return new RelicEffectQuietStudy();
        if(id.EndsWith("WW_A05",StringComparison.Ordinal))return new RelicEffectLethalRefund();
        if(id.EndsWith("WW_A06",StringComparison.Ordinal))return new RelicEffectExtendedExam();
        if(id.EndsWith("WW_A07",StringComparison.Ordinal))return new RelicEffectRelationsCommittee();
        if(id.EndsWith("WW_A08",StringComparison.Ordinal))return new RelicEffectAdministrativeSilence();
        if(id.EndsWith("WW_A09",StringComparison.Ordinal))return new RelicEffectComplaintsOffice();
        if(id.EndsWith("WW_A10",StringComparison.Ordinal))return new RelicEffectOffensiveMedicine();
        return null;
    }
    public override PropDescriptions CreateEditorInspectorDescriptions()=>PropDescriptions.DisplayNone;
    internal virtual void BeforePower(AcademicRuntime.SpellResolution frame,List<CharacterState> units)=>Legacy()?.BeforePower(frame,units);
    internal virtual void BeforeRules(AcademicRuntime.SpellResolution frame,CharacterState? target)=>Legacy()?.BeforeRules(frame,target);
    internal virtual void EndTurn(bool damagePlayed)=>Legacy()?.EndTurn(damagePlayed);
    internal virtual void UnitDied(bool chaperone,int floor)=>Legacy()?.UnitDied(chaperone,floor);
    internal virtual int TurnOrder=>Legacy()?.TurnOrder??100;
    internal virtual IEnumerator BeginTurn(){var legacy=Legacy();if(legacy!=null)yield return legacy.BeginTurn();}
    internal virtual bool Refund(CardState card,int kills,ICoreGameManagers core)=>Legacy()?.Refund(card,kills,core)??false;
    internal virtual bool Count(StringBuilder builder,CardStatistics statistics)=>Legacy()?.Count(builder,statistics)??false;
    internal void RecordKillingSpell(CardState card,int kills,ICoreGameManagers core)
    {
        if(!core.GetSaveManager().PreviewMode&&Refund(card,kills,core))NotifyRelicTriggeredScreenCenter(core.GetRelicManager());
    }
    public override void AppendConditionCountDisplay(StringBuilder builder,CardStatistics statistics)
    {if(!Count(builder,statistics))base.AppendConditionCountDisplay(builder,statistics);}
}
// 2026-10-27-2019||codex-ww-inc||salidas/build/ww-inc/src/AcademicRelics.cs||Crear despacho explícito de eventos de reliquias y fachada antigua compatible con datos guardados
// 2026-10-27-2020||codex-ww-inc||salidas/build/ww-inc/src/AcademicRelics.cs||Limitar resolución por ID a instancias antiguas de la fachada, nunca efectos concretos
