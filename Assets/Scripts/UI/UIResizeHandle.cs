using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Flags]
public enum ResizeEdge { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

public class UIResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler,
                                              IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] RectTransform panel;
    [SerializeField] ResizeEdge    edges;
    [SerializeField] Vector2       minSize = new Vector2(200, 100);
    [SerializeField] Texture2D     cursor;
    [SerializeField] Vector2       cursorHotspot = new Vector2(16, 16);

    static readonly Color HighlightColor = new Color(1f, 1f, 1f, 0.18f);
    static readonly Color NormalColor    = new Color(0f, 0f, 0f, 0f);

    Canvas  _canvas;
    Image   _img;
    Vector2 _startSize;
    Vector2 _startPos;
    Vector2 _startMouse;

    void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _img    = GetComponent<Image>();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_img != null) _img.color = HighlightColor;
        if (cursor != null) Cursor.SetCursor(cursor, cursorHotspot, CursorMode.Auto);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_img != null) _img.color = NormalColor;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        _startSize  = panel.sizeDelta;
        _startPos   = panel.anchoredPosition;
        _startMouse = e.position;
    }

    public void OnDrag(PointerEventData e)
    {
        Vector2 delta = (e.position - _startMouse) / _canvas.scaleFactor;

        Vector2 pos  = _startPos;
        Vector2 size = _startSize;

        if ((edges & ResizeEdge.Right) != 0)
            size.x = Mathf.Max(minSize.x, _startSize.x + delta.x);

        if ((edges & ResizeEdge.Top) != 0)
            size.y = Mathf.Max(minSize.y, _startSize.y + delta.y);

        if ((edges & ResizeEdge.Left) != 0)
        {
            float newW = Mathf.Max(minSize.x, _startSize.x - delta.x);
            pos.x  += _startSize.x - newW;
            size.x  = newW;
        }

        if ((edges & ResizeEdge.Bottom) != 0)
        {
            float newH = Mathf.Max(minSize.y, _startSize.y - delta.y);
            pos.y  += _startSize.y - newH;
            size.y  = newH;
        }

        panel.sizeDelta        = size;
        panel.anchoredPosition = pos;
    }
}
