using UnityEngine;

public class CommonSoundLoader : MonoBehaviour
{
    public static CommonSoundLoader instance;
    private void Awake()
    {
        if(instance != this)
        {
            instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }
        DontDestroyOnLoad(this.gameObject);
    }
}
