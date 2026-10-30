using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Kötelező az új rendszerhez

public class SplashToMenu : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    
    private InputAction anyKeyAction;

    void Awake()
    {
        // Létrehozunk egy univerzális érzékelőt
        anyKeyAction = new InputAction(type: InputActionType.Button);
        
        // Hozzáadjuk a billentyűzetet, egeret és kontrollert is
        anyKeyAction.AddBinding("<Keyboard>/anyKey");
        anyKeyAction.AddBinding("<Mouse>/press");
        anyKeyAction.AddBinding("<Gamepad>/<Button>");
    }

    void OnEnable()
    {
        anyKeyAction.Enable(); // Bekapcsoljuk az érzékelőt
    }

    void OnDisable()
    {
        anyKeyAction.Disable(); // Kikapcsoljuk, ha elhagyjuk a jelenetet
    }

    void Update()
    {
        // anyKeyAction.triggered pontosan AKKOR lesz TRUE, amikor MEGNYOMTAK egy gombot
        if (anyKeyAction.triggered)
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}