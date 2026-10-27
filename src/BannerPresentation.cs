using UnityEngine;
using UnityEngine.UI;
namespace WwInc;

// Retain the game's entire banner geometry/material/hover animation. Only add the clan emblem.
internal static class BannerPresentation
{
    internal static void Configure(RewardNodeData node,Sprite emblem)
    {
        var prefab=node.GetMapIconPrefab();
        Add(prefab.transform.Find("Art root/IconSprite_Enabled"),emblem,Color.white);
        Add(prefab.transform.Find("Art root/IconSprite_Disabled"),emblem,new Color(.45f,.45f,.45f,1));
        Add(prefab.transform.Find("Art root/IconSprite_Frozen"),emblem,new Color(.7f,.85f,1,.7f));
        // Visited banners are empty poles in the native presentation.
    }
    private static void Add(Transform parent,Sprite emblem,Color tint)
    {
        if(parent==null||parent.Find("W&W emblem")!=null)return;
        var badge=new GameObject("W&W emblem",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        badge.transform.SetParent(parent,false);
        var rect=(RectTransform)badge.transform;
        rect.anchorMin=new Vector2(.405f,.44f);rect.anchorMax=new Vector2(.555f,.59f);
        rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
        var image=badge.GetComponent<Image>();image.sprite=emblem;image.preserveAspect=true;
        image.color=tint;image.raycastTarget=false;
    }
}
// 2026-10-26-2035||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/BannerPresentation.cs||Reutilizar silueta y hover nativos; añadir emblema de clan como UI sin editar imágenes
