using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "GameScene";

    public void StartGame()
    {
        Debug.Log("A kilépés metódus meghívásra került.");
       SceneManager.LoadScene(gameSceneName);
    }

    public void AAA()
    {
        Application.Quit();
        
        Debug.Log("A kilépés metódus meghívásra került.");
    }

    public void QuitGame()
    {
        Application.Quit();
        
        Debug.Log("A kilépés metódus meghívásra került.");
    }
}
