using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class JoystickWorldUIPassthrough : MonoBehaviour, ICanvasRaycastFilter
{
    private static readonly List<RaycastResult> RaycastResults = new();
    private static readonly List<GraphicRaycaster> WorldSpaceRaycasters = new();

    public static void RegisterWorldSpaceCanvas(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            return;

        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (!WorldSpaceRaycasters.Contains(raycaster))
            WorldSpaceRaycasters.Add(raycaster);
    }

    public static void UnregisterWorldSpaceCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            WorldSpaceRaycasters.Remove(raycaster);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (WorldSpaceRaycasters.Count == 0)
            return true;

        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return true;

        var pointerData = new PointerEventData(eventSystem) { position = screenPoint };

        foreach (var raycaster in WorldSpaceRaycasters)
        {
            if (raycaster == null || !raycaster.isActiveAndEnabled)
                continue;

            RaycastResults.Clear();
            raycaster.Raycast(pointerData, RaycastResults);

            if (RaycastResults.Count > 0)
                return false;
        }

        return true;
    }
}
