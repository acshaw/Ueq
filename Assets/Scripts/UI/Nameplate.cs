using Mirror;
using TMPro;
using UnityEngine;

public class Nameplate : NetworkBehaviour
{
    [SerializeField] float heightOffset = 2.2f;
    [SerializeField] float fontSize     = 3f;
    [SerializeField] Color labelColor   = Color.white;

    [SyncVar(hook = nameof(OnLabelChanged))]
    string _label = "";

    public string Label => _label;

    TextMeshPro _tmp;

    void Start() => BuildLabel();

    [Server]
    public void SetLabel(string label) => _label = label;

    void OnLabelChanged(string _, string newVal)
    {
        if (_tmp != null) _tmp.text = newVal;
    }

    void BuildLabel()
    {
        var go = new GameObject("_Nameplate");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, heightOffset, 0f);

        go.AddComponent<Billboard>();
        _tmp = go.AddComponent<TextMeshPro>();
        _tmp.text      = _label;
        _tmp.fontSize  = fontSize;
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.color     = labelColor;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
