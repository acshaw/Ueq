using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M3.1.1 (SF4) — full-screen fade overlay for screen transitions. A black <see cref="CanvasGroup"/> that
/// fades in (covers), runs a swap callback while opaque, then fades out. Built at runtime by
/// <see cref="UIScreenManager"/> as the top-most child of the menu canvas so it also covers the in-world HUD
/// during a transition.
/// </summary>
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    CanvasGroup _group;

    public float Duration = 0.28f;

    void Awake()
    {
        var img = GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true; // block clicks mid-transition

        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }

    /// <summary>Fade to black → invoke <paramref name="swap"/> → fade back. Runs on the caller's coroutine.</summary>
    public IEnumerator Transition(System.Action swap)
    {
        _group.blocksRaycasts = true;
        yield return FadeTo(1f);
        swap?.Invoke();
        yield return FadeTo(0f);
        _group.blocksRaycasts = false;
    }

    /// <summary>Fade to black and hold (blocks input) — for a scripted exit that must cover a teardown before
    /// the swap (3.1.8 camp: fade first, despawn under black, then reveal on Character Select).</summary>
    public IEnumerator Cover()
    {
        _group.blocksRaycasts = true;
        yield return FadeTo(1f);
    }

    /// <summary>Fade back from black and release input.</summary>
    public IEnumerator Reveal()
    {
        yield return FadeTo(0f);
        _group.blocksRaycasts = false;
    }

    IEnumerator FadeTo(float target)
    {
        float start = _group.alpha;
        float t = 0f;
        float dur = Mathf.Max(0.01f, Duration);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        _group.alpha = target;
    }
}
