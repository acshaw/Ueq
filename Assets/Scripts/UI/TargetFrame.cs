using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TargetFrame : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text   nameLabel;
    [SerializeField] Image      healthFill;
    [SerializeField] TMP_Text   healthText;

    NetworkedPlayer _player;
    Health          _targetHealth;

    // Bind via the central LocalPlayer service (1.7).
    void OnEnable()
    {
        LocalPlayer.Spawned   += OnLocalSpawned;
        LocalPlayer.Despawned += OnLocalDespawned;
        if (LocalPlayer.Current != null) OnLocalSpawned(LocalPlayer.Current);
    }

    void OnDisable()
    {
        LocalPlayer.Spawned   -= OnLocalSpawned;
        LocalPlayer.Despawned -= OnLocalDespawned;
        if (_player != null) _player.OnTargetChanged -= OnTargetChanged;
        if (_targetHealth != null) _targetHealth.OnHealthUpdated -= Refresh;
        _player = null;
        _targetHealth = null;
    }

    void OnLocalSpawned(NetworkedPlayer p)
    {
        _player = p;
        _player.OnTargetChanged += OnTargetChanged;
    }

    // Self-heal fallback — see PlayerFrame.Update for why.
    void Update()
    {
        if (_player == null && LocalPlayer.Current != null) OnLocalSpawned(LocalPlayer.Current);
    }

    void OnLocalDespawned()
    {
        _player = null;       // event dies with the destroyed player object
        OnTargetChanged(null); // hide the target panel
    }

    void OnTargetChanged(Targetable target)
    {
        if (_targetHealth != null)
        {
            _targetHealth.OnHealthUpdated -= Refresh;
            _targetHealth = null;
        }

        if (target == null)
        {
            panel.SetActive(false);
            return;
        }

        nameLabel.text = target.GetComponentInParent<Nameplate>()?.Label;
        if (string.IsNullOrEmpty(nameLabel.text)) nameLabel.text = target.name;

        _targetHealth = target.GetComponentInParent<Health>();
        if (_targetHealth != null)
        {
            _targetHealth.OnHealthUpdated += Refresh;
            Refresh(_targetHealth.Current, _targetHealth.Max);
        }

        panel.SetActive(true);
    }

    void Refresh(int current, int max)
    {
        float pct = max > 0 ? (float)current / max : 0f;
        healthFill.rectTransform.anchorMax = new Vector2(pct, 1f);
        healthText.text = $"{current} / {max}";
    }

    void OnDestroy()
    {
        if (_player != null)     _player.OnTargetChanged        -= OnTargetChanged;
        if (_targetHealth != null) _targetHealth.OnHealthUpdated -= Refresh;
    }
}
