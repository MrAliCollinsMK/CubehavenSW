using System.Threading.Tasks;
using UnityEngine;

public class InteractButtonCube : MonoBehaviour, IInteractable
{
    public LogicBlock target;
    public bool oneShot = true;
    [HideInInspector]
    public bool used = false;

    [Header("Toggle Settings")]
    public bool toggleMode = false; // NEW: allows On/Off toggling
    private bool currentState = false; // false = Off, true = On

    [Header("Animation")]
    public Animation anim;
    public string pressAnimName = "Press";
    public bool shouldDelayBeforePlayingAnim;
    public float delayTime = 0.35f;

    public AudioSource msource;

    

    public async void Interact(SimplePlayerController player)
    {
        if(oneShot && used)
            return;

        used = true;
        msource=GetComponent<AudioSource>();

        if(target != null)
        {
            if(toggleMode && !oneShot)
            {
                // Flip state
                currentState = !currentState;
                target.SetState(currentState ? LogicState.On : LogicState.Off);
            }
            else
            {
                // Default behaviour (force ON)
                target.SetState(LogicState.On);
            }
        }

        if(anim == null || anim.GetClip(pressAnimName) == null)
            return;

        if(shouldDelayBeforePlayingAnim && delayTime > 0f)
            await Task.Delay(Mathf.RoundToInt(delayTime * 1000f));

        if(this != null && gameObject.activeInHierarchy)
            anim.Play(pressAnimName);
        if (msource!=null)
        {
            if(msource.clip!=null)
            {
                msource.Play();
            }
        }
    }
}