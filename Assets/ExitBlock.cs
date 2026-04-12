using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitBlock : MonoBehaviour
{
    [Header("Next Scene")]
    public int nextSceneId; // easiest for now

    [Header("Timing")]
    public float fadeOutTime = 0.25f;
    public float postFadeDelay = 0.05f;

    [Header("Safety")]
    public bool requirePlayerTag = true;
    public bool oneShot = true;

    bool used;

    void OnTriggerEnter(Collider other)
    {
        if(oneShot && used) return;
        if(!IsPlayer(other)) return;

        used = true;
        StartCoroutine(ExitRoutine());
    }

    IEnumerator ExitRoutine()
    {
        // Fade to white
        if(ScreenFader.Instance != null)
            ScreenFader.Instance.FadeOut(fadeOutTime);

        yield return new WaitForSeconds(fadeOutTime + postFadeDelay);

        // Async load next scene
        AsyncOperation op = SceneManager.LoadSceneAsync(nextSceneId);
        while(!op.isDone)
            yield return null;
    }
    bool IsPlayer(Collider other)
    {
        // Check on this object
        if(other.GetComponent<SimplePlayerController>())
            return true;

        // Check up the hierarchy (common if collider is on a child)
        if(other.GetComponentInParent<SimplePlayerController>())
            return true;

        return false;
    }
}