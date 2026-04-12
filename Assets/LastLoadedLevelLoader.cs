using UnityEngine;
using UnityEngine.SceneManagement;

public class LastLoadedLevelLoader : MonoBehaviour
{
    public void Load()
    {
        int a = PlayerPrefs.GetInt("currentLevel");
        SceneManager.LoadScene(a);
    }
}
