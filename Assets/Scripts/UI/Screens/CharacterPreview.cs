using UnityEngine;

/// <summary>
/// 3.1.6 (PV1/PV2) — a self-contained 3D character viewport rendered to a <see cref="RenderTexture"/> for the
/// character-create screen. A small staging setup (root + camera + directional light) lives far below the
/// world at <see cref="StageY"/> so it never collides with menu or gameplay objects; during Character Select
/// no gameplay camera exists and the menu camera renders nothing, so no layer isolation is needed — the
/// preview camera simply looks at an otherwise-empty patch of space.
///
/// <see cref="Show"/> swaps the body (via <see cref="CharacterModelFactory"/>, the same recipe the in-world
/// model uses) whenever the (gender, race, class) tuple changes; the body idles (Animator Speed = 0) and
/// slowly auto-yaws so the player can see it from all sides. A <c>RawImage</c> in the create card displays
/// <see cref="Texture"/>.
/// </summary>
public class CharacterPreview : MonoBehaviour
{
    const float StageY     = -5000f; // clear of every zone (base + offset zones live at 0 / +5000 / +10000)
    const float YawSpeed   = 25f;    // degrees/second

    RenderTexture _rt;
    Camera        _camera;
    Light         _light;
    Transform     _stage;
    GameObject _body;
    Transform  _weapon;   // the attached prop, re-aligned each frame for live grip tuning

    float  _yaw;
    bool   _active;
    bool   _has;
    Gender _gender;
    string _race = "";
    string _cls  = "";

    public RenderTexture Texture => _rt;

    public static CharacterPreview Create(int width, int height)
    {
        var go = new GameObject("CharacterPreview");
        DontDestroyOnLoad(go);
        var p = go.AddComponent<CharacterPreview>();
        p.Setup(width, height);
        return p;
    }

    void Setup(int width, int height)
    {
        _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        _rt.Create();

        var stageGo = new GameObject("PreviewStage");
        stageGo.transform.SetParent(transform, false);
        stageGo.transform.position = new Vector3(0f, StageY, 0f);
        _stage = stageGo.transform;

        // Body faces +Z (Synty default); the camera sits on +Z looking back so it sees the front.
        var camGo = new GameObject("PreviewCamera");
        camGo.transform.SetParent(_stage, false);
        camGo.transform.localPosition = new Vector3(0f, 0.9f, 3.4f);
        _camera = camGo.AddComponent<Camera>();
        _camera.targetTexture   = _rt;
        _camera.clearFlags      = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0.10f, 0.11f, 0.14f, 1f);
        _camera.fieldOfView     = 34f;
        _camera.nearClipPlane   = 0.1f;
        _camera.farClipPlane    = 12f;
        _camera.transform.LookAt(_stage.position + Vector3.up * 0.9f);

        var lightGo = new GameObject("PreviewLight");
        lightGo.transform.SetParent(_stage, false);
        lightGo.transform.localRotation = Quaternion.Euler(35f, 165f, 0f);
        _light = lightGo.AddComponent<Light>();
        _light.type      = LightType.Directional;
        _light.intensity = 1.15f;

        SetActive(false);
    }

    /// <summary>Show the body for a tuple (rebuilds only when it changes). Enables rendering.</summary>
    public void Show(Gender gender, string race, string cls)
    {
        SetActive(true);
        if (_has && gender == _gender && race == _race && cls == _cls) return;

        _has = true; _gender = gender; _race = race; _cls = cls;
        if (_body != null) Destroy(_body);
        _body = CharacterModelFactory.Build(_stage, gender, race, cls,
            CharacterRosterRegistry.LocomotionController, driveLocomotion: false);
        if (_body != null) _body.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);

        // Cache the attached weapon transform; Update re-reads its grip offsets from CharacterRoster each
        // frame (M2.10, RC4 — moved off ClassDefinition) so tuning them in the Inspector reflects immediately.
        _weapon = FindWeapon(_body);
    }

    static Transform FindWeapon(GameObject body)
    {
        if (body == null) return null;
        foreach (var t in body.GetComponentsInChildren<Transform>())
            if (t.name.StartsWith("Weapon_")) return t;
        return null;
    }

    /// <summary>Toggle rendering + auto-rotation (called when entering/leaving the create form). The light is
    /// toggled too — a directional light is positionless, so leaving it on would bleed into the in-world scene.</summary>
    public void SetActive(bool on)
    {
        _active = on;
        if (_camera != null) _camera.enabled = on;
        if (_light  != null) _light.enabled  = on;
    }

    void Update()
    {
        if (!_active || _body == null) return;
        _yaw += YawSpeed * Time.deltaTime;
        _body.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);

        // Live grip tuning: re-apply the class's offsets each frame so editing them in the Inspector during
        // Play updates the held weapon immediately (that's the point of aligning them in the preview).
        if (_weapon != null)
        {
            var weapon = CharacterRosterRegistry.GetWeaponProp(_cls);
            if (weapon.prop != null)
            {
                _weapon.localPosition = weapon.gripPositionOffset;
                _weapon.localRotation = Quaternion.Euler(weapon.gripEulerOffset);
            }
        }
    }

    void OnDestroy()
    {
        if (_camera != null) _camera.targetTexture = null;
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
    }
}
