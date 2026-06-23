using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Image    background;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text quantityText;
    [SerializeField] int      slotIndex;

    public int SlotIndex => slotIndex;

    static readonly Color EmptyColor  = new Color(0.15f, 0.15f, 0.15f);
    static readonly Color FilledColor = new Color(0.22f, 0.30f, 0.22f);
    static readonly Color HeldColor   = new Color(0.40f, 0.36f, 0.10f);

    public void Refresh(InventorySlot slot, bool isHeld)
    {
        if (slot.IsEmpty)
        {
            background.color  = isHeld ? HeldColor : EmptyColor;
            nameText.text     = "";
            quantityText.text = "";
        }
        else
        {
            background.color  = isHeld ? HeldColor : FilledColor;
            var def           = ItemRegistry.Instance?.Get(slot.itemId);
            nameText.text     = def != null ? def.displayName : slot.itemId;
            quantityText.text = slot.quantity > 1 ? $"x{slot.quantity}" : "";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            InventoryUI.Instance?.OnSlotClicked(slotIndex);
        else if (eventData.button == PointerEventData.InputButton.Right)
            InventoryUI.Instance?.OnSlotRightClicked(slotIndex);
    }
}
