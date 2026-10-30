using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuUI; // A szünet menü Panel-je
    public Image backgroundPanel;     
    public GameObject inGameMenuUI;

    private bool isPaused = false;

    void Start()
    {
        Resume();
    }

    void Update()
    {
        // Az Escape billentyű lenyomásának figyelése
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        Cursor.lockState = CursorLockMode.Locked;
        isPaused = false;
        Time.timeScale = 1f; 

        // Pause gombok kikapcsolása
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);    

        // In-game UI bekapcsolása
        if (inGameMenuUI != null) inGameMenuUI.SetActive(true);

        // Panel átlátszóságának beállítása 0-ra (láthatatlan)
        if (backgroundPanel != null)
        {
            Color panelColor = backgroundPanel.color;
            panelColor.a = 0f; 
            backgroundPanel.color = panelColor;
        }
    }

    public void Pause()
    {
        Cursor.lockState = CursorLockMode.None;
        isPaused = true;
        Time.timeScale = 0f; 

        // Pause gombok kikapcsolása
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);   

        // In-game UI kikapcsolása
        if (inGameMenuUI != null) inGameMenuUI.SetActive(false);

        // Panel átlátszóságának beállítása 101-re (101/255)
        if (backgroundPanel != null)
        {
            Color panelColor = backgroundPanel.color;
            panelColor.a = 101f / 255f; 
            backgroundPanel.color = panelColor;
        }
    }

    public void LoadMainMenu(int menuSceneIndex = 0)
    {
        // Kritikus: A Time.timeScale-t vissza kell állítani 1-re scene betöltés előtt, 
        // különben a következő betöltött jelenetben is fagyva marad az idő.
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneIndex);
    }

    
}
