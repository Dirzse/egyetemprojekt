using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Character select screen shown before the world loads.
// Buttons: Previous() / Next() / Confirm() / Back(), or Select(index) for one button per character.
// Starting weapon: PreviousWeapon() / NextWeapon().
// Keyboard: A/D or arrows = switch character, Q/E = switch weapon, Enter = start, Esc = back.
public class CharacterSelectMenu : MonoBehaviour
{
    [System.Serializable]
    public class StartingWeaponOption
    {
        public string displayName = "Weapon";
        [Tooltip("Index in the WeaponHolder's weapons array: 0 = bow, 1 = sword, 2 = knife.")]
        public int weaponSlot;
        [Tooltip("Model shown in the small weapon preview (empty = \"no model yet\" label).")]
        public GameObject previewModel;
    }

    [SerializeField] CharacterData[] characters;
    [SerializeField] string gameSceneName = "Alcatraz1";
    [SerializeField] string mainMenuSceneName = "MainMenu";
    [SerializeField] CharacterPreviewStage previewStage;

    [Header("Character list (one button is generated per character from the template)")]
    [SerializeField] Transform listContainer;
    [Tooltip("Inactive button with a TMP text child and an optional Image child named \"Portrait\".")]
    [SerializeField] Button listButtonTemplate;
    [SerializeField] Color normalButtonColor = new Color(0.08f, 0.14f, 0.2f, 0.9f);
    [SerializeField] Color selectedButtonColor = new Color(0.12f, 0.45f, 0.6f, 1f);
    [Tooltip("Buttons are stacked from the top of the list container.")]
    [SerializeField] float buttonHeight = 72f;
    [SerializeField] float buttonSpacing = 10f;

    [Header("Info (all optional)")]
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] Image portraitImage;

    [Header("Stat bars (all optional)")]
    [SerializeField] StatBar healthBar;
    [SerializeField] StatBar strengthBar;
    [SerializeField] StatBar speedBar;
    [SerializeField] StatBar throwPowerBar;
    [SerializeField] StatBar reloadBar;
    [SerializeField] StatBar drawSpeedBar;
    [SerializeField] StatBar knivesBar;

    [Header("Starting weapon")]
    [SerializeField] StartingWeaponOption[] weaponOptions;
    [SerializeField] WeaponPreviewStage weaponStage;
    [SerializeField] TMP_Text weaponNameText;

    [Header("Bar scale (the value that fills a bar completely)")]
    [SerializeField] float maxHealthValue = 200f;
    [SerializeField] float maxDamageMultiplier = 2f;
    [SerializeField] float maxRunSpeed = 15f;
    [SerializeField] float maxThrowPower = 2f;
    [SerializeField] float maxReloadSpeed = 2f;
    [SerializeField] float maxDrawSpeed = 2f;
    [SerializeField] float maxKnivesValue = 8f;

    Button[] listButtons;
    int index;
    int weaponIndex;
    bool loading;

    void Start()
    {
        BuildList();

        // Keep the previous choice (e.g. when coming back from the game)
        if (CharacterSelection.Selected != null && characters != null)
        {
            int previous = System.Array.IndexOf(characters, CharacterSelection.Selected);
            if (previous >= 0) index = previous;
        }

        // Same for the starting weapon
        if (weaponOptions != null)
        {
            for (int i = 0; i < weaponOptions.Length; i++)
            {
                if (weaponOptions[i].weaponSlot == CharacterSelection.StartingWeaponSlot) weaponIndex = i;
            }
        }

        Show();
        ShowWeapon();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Next();
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Previous();
        if (Input.GetKeyDown(KeyCode.E)) NextWeapon();
        if (Input.GetKeyDown(KeyCode.Q)) PreviousWeapon();
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
        if (Input.GetKeyDown(KeyCode.Escape)) Back();
    }

    public void Next()
    {
        if (characters == null || characters.Length == 0) return;
        index = (index + 1) % characters.Length;
        Show();
    }

    public void Previous()
    {
        if (characters == null || characters.Length == 0) return;
        index = (index - 1 + characters.Length) % characters.Length;
        Show();
    }

    public void Select(int characterIndex)
    {
        if (characters == null || characterIndex < 0 || characterIndex >= characters.Length) return;
        index = characterIndex;
        Show();
    }

    public void Confirm()
    {
        if (loading || characters == null || characters.Length == 0) return;
        loading = true;
        CharacterSelection.Selected = characters[index];
        if (weaponOptions != null && weaponOptions.Length > 0)
            CharacterSelection.StartingWeaponSlot = weaponOptions[weaponIndex].weaponSlot;
        SceneManager.LoadScene(gameSceneName);
    }

    public void NextWeapon()
    {
        if (weaponOptions == null || weaponOptions.Length == 0) return;
        weaponIndex = (weaponIndex + 1) % weaponOptions.Length;
        ShowWeapon();
    }

    public void PreviousWeapon()
    {
        if (weaponOptions == null || weaponOptions.Length == 0) return;
        weaponIndex = (weaponIndex - 1 + weaponOptions.Length) % weaponOptions.Length;
        ShowWeapon();
    }

    void ShowWeapon()
    {
        if (weaponOptions == null || weaponOptions.Length == 0) return;
        StartingWeaponOption option = weaponOptions[weaponIndex];
        if (weaponNameText) weaponNameText.text = option.displayName;
        if (weaponStage) weaponStage.Show(option.previewModel);
    }

    public void Back()
    {
        if (loading || string.IsNullOrEmpty(mainMenuSceneName)) return;
        loading = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void BuildList()
    {
        if (listContainer == null || listButtonTemplate == null || characters == null) return;

        listButtonTemplate.gameObject.SetActive(false);
        listButtons = new Button[characters.Length];

        for (int i = 0; i < characters.Length; i++)
        {
            CharacterData c = characters[i];
            Button button = Instantiate(listButtonTemplate, listContainer);
            button.gameObject.SetActive(true);

            // Stack the buttons from the top: full width, fixed height
            RectTransform rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -i * (buttonHeight + buttonSpacing));
            rt.sizeDelta = new Vector2(0f, buttonHeight);
            button.name = "Character_" + c.characterName;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = c.characterName;

            Transform portrait = button.transform.Find("Portrait");
            if (portrait != null && portrait.TryGetComponent(out Image portraitIcon))
            {
                portraitIcon.sprite = c.portrait;
                portraitIcon.enabled = c.portrait != null;
            }

            int captured = i;   // the lambda needs its own copy of i
            button.onClick.AddListener(() => Select(captured));
            listButtons[i] = button;
        }
    }

    void Show()
    {
        if (characters == null || characters.Length == 0) return;
        CharacterData c = characters[index];

        if (nameText) nameText.text = c.characterName;
        if (descriptionText) descriptionText.text = c.description;
        if (portraitImage)
        {
            portraitImage.sprite = c.portrait;
            portraitImage.enabled = c.portrait != null;
        }

        SetBar(healthBar, c.maxHealth / maxHealthValue, Mathf.RoundToInt(c.maxHealth).ToString());
        SetBar(strengthBar, c.damageMultiplier / maxDamageMultiplier, "x" + c.damageMultiplier.ToString("0.0#"));
        SetBar(speedBar, c.runSpeed / maxRunSpeed, c.runSpeed.ToString("0.#"));
        SetBar(throwPowerBar, c.throwPowerMultiplier / maxThrowPower, "x" + c.throwPowerMultiplier.ToString("0.0#"));
        SetBar(reloadBar, c.reloadSpeedMultiplier / maxReloadSpeed, "x" + c.reloadSpeedMultiplier.ToString("0.0#"));
        SetBar(drawSpeedBar, c.drawSpeedMultiplier / maxDrawSpeed, "x" + c.drawSpeedMultiplier.ToString("0.0#"));
        SetBar(knivesBar, c.startingKnives / maxKnivesValue, c.startingKnives + " / " + c.maxKnives);

        if (listButtons != null)
        {
            for (int i = 0; i < listButtons.Length; i++)
            {
                if (listButtons[i] != null && listButtons[i].targetGraphic != null)
                    listButtons[i].targetGraphic.color = i == index ? selectedButtonColor : normalButtonColor;
            }
        }

        if (previewStage) previewStage.Show(c);
    }

    static void SetBar(StatBar bar, float normalized, string text)
    {
        if (bar != null) bar.SetValue(normalized, text);
    }
}
