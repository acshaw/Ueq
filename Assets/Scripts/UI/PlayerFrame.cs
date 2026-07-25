using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerFrame : MonoBehaviour
{
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] Image    healthFill;
    [SerializeField] TMP_Text healthText;
    [SerializeField] Image    combatBorder; // red rim, pulses while in combat (1.6.1)

    Health      _health;
    CombatState _combat;
    bool        _inCombat;

    // Bind via the central LocalPlayer service (1.7) instead of polling FindObjectsByType.
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
        if (_health != null) _health.OnHealthUpdated -= Refresh;
        if (_combat != null) _combat.OnCombatChanged -= OnCombatChanged;
        _health = null;
        _combat = null;
    }

    void OnLocalSpawned(NetworkedPlayer p)
    {
        _health = p.GetComponent<Health>();
        var np = p.GetComponent<Nameplate>();
        nameLabel.text = np?.Label;
        if (string.IsNullOrEmpty(nameLabel.text)) nameLabel.text = "Player";
        if (_health != null)
        {
            _health.OnHealthUpdated += Refresh;
            Refresh(_health.Current, _health.Max);
        }

        _combat = p.GetComponent<CombatState>();
        if (_combat != null)
        {
            _combat.OnCombatChanged += OnCombatChanged;
            OnCombatChanged(_combat.InCombat);
        }
    }

    void OnLocalDespawned()
    {
        // Player object is being destroyed (camp/disconnect) — drop the refs; events die with it.
        if (_combat != null) _combat.OnCombatChanged -= OnCombatChanged;
        _health = null;
        _combat = null;
        OnCombatChanged(false);
    }

    void OnCombatChanged(bool inCombat)
    {
        _inCombat = inCombat;
        if (!inCombat) SetBorderAlpha(0f); // pulse handled in Update while in combat
    }

    void Update()
    {
        // Self-heal fallback: if the one-shot LocalPlayer.Spawned event was missed for any reason
        // (5.10 finding: observed 0 subscribers at Spawned-invoke time in a real standalone
        // client/server pair — root mechanism not fully pinned down), bind as soon as a local
        // player exists instead of staying unbound for the rest of the session.
        if (_health == null && LocalPlayer.Current != null) OnLocalSpawned(LocalPlayer.Current);

        if (_inCombat && combatBorder != null)
            SetBorderAlpha(0.35f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 4f)));
    }

    void SetBorderAlpha(float a)
    {
        if (combatBorder == null) return;
        var c = combatBorder.color;
        c.a = a;
        combatBorder.color = c;
    }

    void Refresh(int current, int max)
    {
        float pct = max > 0 ? (float)current / max : 0f;
        healthFill.rectTransform.anchorMax = new Vector2(pct, 1f);
        healthText.text = $"{current} / {max}";
    }
}
