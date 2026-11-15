using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Character select screen shown before the world loads.
// Buttons: Previous() / Next() / Confirm() / Back(), or Select(index) for one button per character.
// Starting weapon: PreviousWeapon() / NextWeapon().
// Keyboard: A/D or arrows = switch character, Q/E = switch weapon, Enter = start, Esc = back.
// Primary weapons: only the kinds the character is good with are offered (CharacterData.primarySlots).
// A character with two primary slots also uses the second picker: PreviousSecondWeapon() / NextSecondWeapon(), keys Z/C.
public class CharacterSelectMenu : MonoBehaviour
{
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
    [Tooltip("Every selectable primary weapon. Each character only sees the kinds allowed in its primary slots.")]
    [SerializeField] PrimaryWeaponData[] primaryWeapons;
    [SerializeField] WeaponPreviewStage weaponStage;
    [SerializeField] TMP_Text weaponNameText;
    [Tooltip("Optional: hidden when the character has no primary weapon (fists only).")]
    [SerializeField] GameObject weaponPanel;
    [SerializeField] string fistsOnlyText = "Fists";

    [Header("Second primary weapon (only for characters with two primary slots, all optional)")]
    [SerializeField] WeaponPreviewStage secondWeaponStage;
    [SerializeField] TMP_Text secondWeaponNameText;
    [Tooltip("Hidden when the character has fewer than two primary slots.")]
    [SerializeField] GameObject secondWeaponPanel;

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
    bool loading;

    // Weapons the current character can pick per primary slot, and the picked option per slot
    List<PrimaryWeaponData>[] slotOptions = new List<PrimaryWeaponData>[0];
    int[] slotChoice = new int[0];
    readonly Dictionary<CharacterData, int[]> rememberedChoices = new Dictionary<CharacterData, int[]>();

    void Start()
    {
        BuildList();

        // Keep the previous choice (e.g. when coming back from the game)
        if (CharacterSelection.Selected != null && characters != null)
        {
            int previous = System.Array.IndexOf(characters, CharacterSelection.Selected);
            if (previous >= 0) index = previous;
        }

        Show();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Next();
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Previous();
        if (Input.GetKeyDown(KeyCode.E)) NextWeapon();
        if (Input.GetKeyDown(KeyCode.Q)) PreviousWeapon();
        if (Input.GetKeyDown(KeyCode.C)) NextSecondWeapon();
        if (Input.GetKeyDown(KeyCode.Z)) PreviousSecondWeapon();
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
        CharacterSelection.Primaries = GetChosenWeapons();
        SceneManager.LoadScene(gameSceneName);
    }

    public void NextWeapon() => StepWeapon(0, 1);
    public void PreviousWeapon() => StepWeapon(0, -1);
    public void NextSecondWeapon() => StepWeapon(1, 1);
    public void PreviousSecondWeapon() => StepWeapon(1, -1);

    void StepWeapon(int slot, int step)
    {
        if (slot >= slotOptions.Length || slotOptions[slot].Count == 0) return;
        int count = slotOptions[slot].Count;
        slotChoice[slot] = (slotChoice[slot] + step + count) % count;
        ShowWeapons();
    }

    // Collects the weapons allowed in each primary slot of the character and restores the previous pick
    void BuildWeaponOptions(CharacterData c)
    {
        int slots = c.primarySlots != null ? c.primarySlots.Length : 0;
        slotOptions = new List<PrimaryWeaponData>[slots];
        slotChoice = new int[slots];

        rememberedChoices.TryGetValue(c, out int[] remembered);
        for (int s = 0; s < slots; s++)
        {
            slotOptions[s] = new List<PrimaryWeaponData>();
            if (primaryWeapons != null)
                foreach (PrimaryWeaponData weapon in primaryWeapons)
                    if (weapon != null && weapon.Fits(c.primarySlots[s])) slotOptions[s].Add(weapon);

            // The character's own default weapon is always offered, even if it is missing from the Primary Weapons list
            PrimaryWeaponData fallback = c.defaultWeapons != null && s < c.defaultWeapons.Length ? c.defaultWeapons[s] : null;
            if (fallback != null && !slotOptions[s].Contains(fallback)) slotOptions[s].Insert(0, fallback);

            if (slotOptions[s].Count == 0)
                Debug.LogWarning("CharacterSelectMenu: no Primary Weapon fits slot " + (s + 1) + " of " + c.characterName + " (" + c.primarySlots[s] + ").", this);

            int choice = -1;
            if (remembered != null && s < remembered.Length) choice = remembered[s];
            else if (CharacterSelection.Selected == c && CharacterSelection.Primaries != null && s < CharacterSelection.Primaries.Length)
                choice = slotOptions[s].IndexOf(CharacterSelection.Primaries[s]);
            if (choice < 0 && c.defaultWeapons != null && s < c.defaultWeapons.Length)
                choice = slotOptions[s].IndexOf(c.defaultWeapons[s]);
            slotChoice[s] = Mathf.Clamp(choice, 0, Mathf.Max(0, slotOptions[s].Count - 1));
        }
        rememberedChoices[c] = slotChoice;
    }

    void ShowWeapons()
    {
        ShowSlot(0, weaponStage, weaponNameText, weaponPanel);
        ShowSlot(1, secondWeaponStage, secondWeaponNameText, secondWeaponPanel);
    }

    void ShowSlot(int slot, WeaponPreviewStage stage, TMP_Text label, GameObject panel)
    {
        bool hasSlot = slot < slotOptions.Length;
        if (panel) panel.SetActive(hasSlot);

        PrimaryWeaponData weapon = hasSlot && slotOptions[slot].Count > 0 ? slotOptions[slot][slotChoice[slot]] : null;
        if (label)
        {
            if (weapon != null)
                label.text = slotOptions[slot].Count > 1
                    ? weapon.displayName + "  (" + (slotChoice[slot] + 1) + "/" + slotOptions[slot].Count + ")"
                    : weapon.displayName;
            else
                label.text = hasSlot ? "-" : (slot == 0 ? fistsOnlyText : "");
        }
        if (stage)
        {
            stage.SetVisible(hasSlot);
            if (hasSlot) stage.Show(weapon != null ? weapon.PreviewModel : null);
        }
    }

    PrimaryWeaponData[] GetChosenWeapons()
    {
        PrimaryWeaponData[] chosen = new PrimaryWeaponData[slotOptions.Length];
        for (int s = 0; s < slotOptions.Length; s++)
            chosen[s] = slotOptions[s].Count > 0 ? slotOptions[s][slotChoice[s]] : null;
        return chosen;
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

        BuildWeaponOptions(c);
        ShowWeapons();
    }

    static void SetBar(StatBar bar, float normalized, string text)
    {
        if (bar != null) bar.SetValue(normalized, text);
    }
}
