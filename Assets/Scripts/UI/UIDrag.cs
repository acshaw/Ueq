using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIDrag : MonoBehaviour, IBeginDragHandler, IDragHandler,
                                     IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] RectTransform target;
    [SerializeField] Texture2D     cursor;
    [SerializeField] Vector2       cursorHotspot = new Vector2(16, 16);

    Image   _img;
    Color   _normalColor;
    Vector2 _grabOffset;

    public void Init(RectTransform moveTarget) => target = moveTarget;

    void Awake()
    {
        _img = GetComponent<Image>();
        if (_img != null) _normalColor = _img.color;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_img != null)
            _img.color = new Color(
                Mathf.Min(1f, _normalColor.r + 0.12f),
                Mathf.Min(1f, _normalColor.g + 0.12f),
                Mathf.Min(1f, _normalColor.b + 0.12f),
                _normalColor.a);
        if (cursor != null) Cursor.SetCursor(cursor, cursorHotspot, CursorMode.Auto);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_img != null) _img.color = _normalColor;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)target.parent, e.position, e.pressEventCamera, out var local);
        _grabOffset = local - target.anchoredPosition;
    }

    public void OnDrag(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)target.parent, e.position, e.pressEventCamera, out var local);
        target.anchoredPosition = local - _grabOffset;
    }
}
