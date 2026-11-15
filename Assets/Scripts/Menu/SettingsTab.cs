using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A tab of the settings window: the button on top and the page it shows.
public class SettingsTab : MonoBehaviour
{
    public Button button;
    public TMP_Text label;
    public Image underline;
    public GameObject page;
    [TextArea] public string note;
}
