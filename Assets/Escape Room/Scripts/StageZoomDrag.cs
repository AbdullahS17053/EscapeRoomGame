using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Sits on the canvas so drags that start anywhere on the stage, buttons included, pan it.
/// A drag also cancels the click of the button it started on.
/// </summary>
public class StageZoomDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public StageZoomController owner;

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner != null) owner.Pan(eventData.delta);
    }

    public void OnEndDrag(PointerEventData eventData) { }
}
