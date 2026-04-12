using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance;

    [SerializeField] Image overlay;
    [SerializeField] float defaultFadeTime = 0.25f;

    Coroutine routine;

    void Awake()
    {
        // Cheap singleton per scene (NOT DontDestroyOnLoad)
        Instance = this;

        if(!overlay) overlay = GetComponentInChildren<Image>(true);
    }

    void Start()
    {
        // Every scene starts with fade-in
        SetAlpha(1f);
        FadeIn(defaultFadeTime);
    }

    public void FadeIn(float time) => StartFade(0f, time);
    public void FadeOut(float time) => StartFade(1f, time);

    public void SetAlpha(float a)
    {
        if(!overlay) return;
        var c = overlay.color;
        c.a = a;
        overlay.color = c;
    }

    void StartFade(float target, float time)
    {
        if(routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FadeRoutine(target, time));
    }

    IEnumerator FadeRoutine(float target, float time)
    {
        if(!overlay) yield break;

        float start = overlay.color.a;
        float t = 0f;

        while(t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.001f, time);
            float a = Mathf.Lerp(start, target, t);
            SetAlpha(a);
            yield return null;
        }

        SetAlpha(target);
    }
}