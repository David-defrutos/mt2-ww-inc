using System;
namespace WwInc;

internal static class TooltipIdentity
{
    // Match native ShowTooltip's title/body fallback before the card path calls
    // InstantiateTooltip, which dereferences tooltipId before testing for null.
    internal static bool Prepare(ref TooltipContent content)
    {
        if(content.IsEmpty())return false;
        if(!string.IsNullOrWhiteSpace(content.tooltipId))return true;
        string text=string.IsNullOrWhiteSpace(content.title)?content.body:content.title;
        if(string.IsNullOrWhiteSpace(text))return false;
        content.tooltipId="david.wwinc.tooltip."+text;
        return true;
    }
}
// 2026-10-26-2313||codex-ww-inc||salidas/build/ww-inc/src/TooltipIdentity.cs||Normalizar identidad de tooltips propios con fallback nativo título/cuerpo antes de InstantiateTooltip
