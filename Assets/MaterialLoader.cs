using UnityEngine;

public class MaterialLoader : MonoBehaviour
{
    public Material ghostHedge;

    public static MaterialLoader instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
}
