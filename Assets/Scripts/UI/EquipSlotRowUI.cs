using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipSlotRowUI : MonoBehaviour
{
    [SerializeField] TMP_Text slotLabel;
    [SerializeField] TMP_Text itemLabel;
    [SerializeField] Button   unequipBtn;
    [SerializeField] int      slotIndex;

    public void Init(int index, NetworkedPlayer player)
    {
        slotIndex = index;
        unequipBtn.onClick.RemoveAllListeners();
        unequipBtn.onClick.AddListener(() => player?.CmdUnequipItem(index));
    }

    public void Refresh(string itemId)
    {
        var def = ItemRegistry.Instance?.Get(itemId);
        itemLabel.text          = def != null ? def.displayName : "---";
        unequipBtn.interactable = !string.IsNullOrEmpty(itemId);
    }
}
