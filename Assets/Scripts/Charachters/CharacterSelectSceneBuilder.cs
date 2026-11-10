using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Builds the whole character select scene (3D stage + lights + post FX + UI) in one click:
// Tools > Character Select > Build Scene. Running it again rebuilds the scene from scratch.
// Must be inside an "Editor" folder (e.g. Assets/Scripts/Editor).
public static class CharacterSelectSceneBuilder
{
    const string Folder = "Assets/CharacterSelect";
    const string CharactersFolder = Folder + "/Characters";
    const string ScenePath = "Assets/Scenes/UI/CharacterSelect.unity";
    const string DefaultModelPath = "Assets/Prefabs/Human_Models_And_Animations/Survival_Character.fbx";

    static readonly Color Background = new Color(0.035f, 0.055f, 0.08f);
    static readonly Color PanelColor = new Color(0.03f, 0.07f, 0.1f, 0.8f);
    static readonly Color ButtonColor = new Color(0.08f, 0.14f, 0.2f, 0.9f);
    static readonly Color Accent = new Color(0.31f, 0.82f, 1f);
    static readonly Color TextColor = new Color(0.86f, 0.93f, 1f);
    static readonly Color DimText = new Color(0.55f, 0.66f, 0.76f);

    static Sprite uiSprite;

    [MenuItem("Tools/Character Select/Build Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Character Select", "CharacterSelect.unity already exists. Rebuild it from scratch?", "Rebuild", "Cancel"))
            return;

        EnsureFolder("Assets", "CharacterSelect");
        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        CharacterData[] characters = FindOrCreateCharacters();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupEnvironment();
        Camera cam = CreateCamera();
        CharacterPreviewStage stage = CreateStage(cam);
        CreateLights();
        CreatePostProcessing();
        CharacterSelectMenu menu = CreateUI(characters, stage);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = menu.gameObject;
        Debug.Log("Character select scene built: " + ScenePath + " (" + characters.Length + " characters). " +
                  "Set MainMenu > gameSceneName to \"CharacterSelect\".");
    }

    // ---------------------------------------------------------------- Characters

    static CharacterData[] FindOrCreateCharacters()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterData");
        if (guids.Length > 0)
        {
            return guids.Select(g => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(g)))
                        .Where(c => c != null)
                        .OrderBy(c => c.name)
                        .ToArray();
        }

        // No characters yet: create three examples so the screen works right away
        EnsureFolder(Folder, "Characters");
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModelPath);
        return new[]
        {
            CreateCharacter("1_Tank", "Tank", "Slow but tough. Hits hard and pulls the heaviest bow, but draws weapons slowly.",
                            160f, 1.3f, 4f, 9f, 1.2f, 1.3f, 0.8f, 0.7f, model),
            CreateCharacter("2_Hunter", "Hunter", "Balanced survivor. Quick hands: fast reloads and a steady bow.",
                            100f, 1.0f, 5f, 12f, 1.5f, 1.1f, 1.3f, 1.1f, model),
            CreateCharacter("3_Scout", "Scout", "Fast and agile. Jumps high and draws in a blink, but cannot take many hits.",
                            75f, 0.8f, 6.5f, 15f, 2.0f, 0.9f, 1.0f, 1.5f, model),
        };
    }

    static CharacterData CreateCharacter(string fileName, string displayName, string description,
                                         float health, float damage, float walk, float run, float jump,
                                         float throwPower, float reload, float draw, GameObject model)
    {
        CharacterData c = ScriptableObject.CreateInstance<CharacterData>();
        c.characterName = displayName;
        c.description = description;
        c.previewModel = model;
        c.maxHealth = health;
        c.damageMultiplier = damage;
        c.walkSpeed = walk;
        c.runSpeed = run;
        c.jumpHeight = jump;
        c.throwPowerMultiplier = throwPower;
        c.reloadSpeedMultiplier = reload;
        c.drawSpeedMultiplier = draw;
        AssetDatabase.CreateAsset(c, CharactersFolder + "/" + fileName + ".asset");
        return c;
    }

    // ---------------------------------------------------------------- 3D scene

    static void SetupEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.13f, 0.17f, 0.23f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Background;
        RenderSettings.fogDensity = 0.07f;
    }

    static Camera CreateCamera()
    {
        GameObject go = new GameObject("Preview Camera");
        go.tag = "MainCamera";
        Camera cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
        cam.fieldOfView = 35f;
        cam.nearClipPlane = 0.05f;
        go.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        // The stage moves it into place at runtime; this is only the editor view
        go.transform.position = new Vector3(0f, 1.4f, 3.2f);
        go.transform.LookAt(new Vector3(0f, 1.1f, 0f));
        return cam;
    }

    static CharacterPreviewStage CreateStage(Camera cam)
    {
        GameObject stageGo = new GameObject("Character Stage");
        CharacterPreviewStage stage = stageGo.AddComponent<CharacterPreviewStage>();

        Transform modelRoot = new GameObject("Model Root").transform;
        modelRoot.SetParent(stageGo.transform, false);

        Material pedestalMat = CreateMaterial("Pedestal", new Color(0.09f, 0.11f, 0.14f), 0.8f, 0.55f, null);
        Material ringMat = CreateMaterial("PedestalRing", Accent, 0f, 0.5f, Accent * 2.5f);
        Material floorMat = CreateMaterial("Floor", new Color(0.04f, 0.05f, 0.07f), 0.2f, 0.3f, null);

        Primitive("Pedestal", PrimitiveType.Cylinder, stageGo.transform, new Vector3(0f, -0.05f, 0f), new Vector3(1.5f, 0.05f, 1.5f), pedestalMat);
        Primitive("Pedestal Ring", PrimitiveType.Cylinder, stageGo.transform, new Vector3(0f, -0.03f, 0f), new Vector3(1.62f, 0.02f, 1.62f), ringMat);
        Primitive("Floor", PrimitiveType.Plane, stageGo.transform, new Vector3(0f, -0.1f, 0f), new Vector3(6f, 1f, 6f), floorMat);

        SerializedObject so = new SerializedObject(stage);
        so.FindProperty("modelRoot").objectReferenceValue = modelRoot;
        so.FindProperty("previewCamera").objectReferenceValue = cam;
        so.ApplyModifiedPropertiesWithoutUndo();
        return stage;
    }

    static void CreateLights()
    {
        // Soft fill so nothing is pitch black
        Light fill = NewLight("Fill Light", LightType.Directional, new Color(0.75f, 0.85f, 1f), 0.35f);
        fill.transform.rotation = Quaternion.Euler(35f, 200f, 0f);
        fill.shadows = LightShadows.None;

        // Key light from the front-right, casts the shadow on the pedestal
        Light key = NewLight("Key Light", LightType.Spot, new Color(1f, 0.95f, 0.88f), 12f);
        key.range = 10f;
        key.spotAngle = 50f;
        key.shadows = LightShadows.Soft;
        key.transform.position = new Vector3(1.6f, 3.4f, 2.6f);
        key.transform.LookAt(new Vector3(0f, 1f, 0f));

        // Cyan rim light from behind (the ARK-like glow on the edges)
        Light rim = NewLight("Rim Light", LightType.Spot, Accent, 14f);
        rim.range = 8f;
        rim.spotAngle = 60f;
        rim.shadows = LightShadows.None;
        rim.transform.position = new Vector3(-0.8f, 2.6f, -2.2f);
        rim.transform.LookAt(new Vector3(0f, 1.2f, 0f));
    }

    static void CreatePostProcessing()
    {
        string path = Folder + "/CharacterSelectVolume.asset";
        AssetDatabase.DeleteAsset(path);

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.8f);
        bloom.threshold.Override(0.9f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.4f);
        vignette.smoothness.Override(0.5f);

        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        // The effects must be saved inside the profile asset, or they are lost on reload
        foreach (VolumeComponent component in profile.components)
        {
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject go = new GameObject("Global Volume");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    // ---------------------------------------------------------------- UI

    static CharacterSelectMenu CreateUI(CharacterData[] characters, CharacterPreviewStage stage)
    {
        // Canvas + EventSystem (the project uses the new Input System UI module)
        GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();

        Transform root = canvasGo.transform;
        CharacterSelectMenu menu = canvasGo.AddComponent<CharacterSelectMenu>();

        // Title
        NewText(NewRect("Title", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -40f), new Vector2(1000f, 64f)),
             "CHOOSE YOUR SURVIVOR", 48f, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        NewText(NewRect("Subtitle", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -104f), new Vector2(1000f, 34f)),
             "Every character plays differently", 24f, DimText, TextAlignmentOptions.Left);

        // ---- Left panel: character list
        RectTransform left = NewRect("Character List Panel", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 10f), new Vector2(380f, 620f));
        NewPanel(left, PanelColor);
        NewText(NewRect("Header", left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(-48f, 36f)),
             "CHARACTERS", 24f, DimText, TextAlignmentOptions.Left, FontStyles.Bold);
        NewPanel(NewRect("Header Line", left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(-48f, 2f)),
              new Color(Accent.r, Accent.g, Accent.b, 0.5f), false, false);

        RectTransform list = NewRect("List", left, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        list.offsetMin = new Vector2(24f, 24f);
        list.offsetMax = new Vector2(-24f, -78f);
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // List button template (inactive, the menu clones it per character)
        RectTransform templateRect = NewRect("Character Button Template", list, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 72f));
        Button template = MakeButton(templateRect, "Character", 28f, ButtonColor, TextColor);
        templateRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
        RectTransform templateLabel = (RectTransform)template.GetComponentInChildren<TMP_Text>().transform;
        templateLabel.offsetMin = new Vector2(84f, 0f);
        template.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Left;
        RectTransform portrait = NewRect("Portrait", templateRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(56f, 56f));
        Image portraitImage = portrait.gameObject.AddComponent<Image>();
        portraitImage.raycastTarget = false;
        portraitImage.preserveAspect = true;
        templateRect.gameObject.SetActive(false);

        // ---- Right panel: name, description, stats
        RectTransform right = NewRect("Info Panel", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 10f), new Vector2(480f, 740f));
        NewPanel(right, PanelColor);

        TMP_Text nameText = NewText(NewRect("Name", right, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(-60f, 64f)),
                                 "Name", 50f, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        TMP_Text descriptionText = NewText(NewRect("Description", right, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(-60f, 150f)),
                                        "Description", 23f, TextColor, TextAlignmentOptions.TopLeft);
        NewPanel(NewRect("Separator", right, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(-60f, 2f)),
              new Color(Accent.r, Accent.g, Accent.b, 0.5f), false, false);
        NewText(NewRect("Attributes Header", right, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -278f), new Vector2(-60f, 34f)),
             "ATTRIBUTES", 24f, DimText, TextAlignmentOptions.Left, FontStyles.Bold);

        string[] statNames = { "HEALTH", "STRENGTH", "SPEED", "BOW / THROW POWER", "RELOAD SPEED", "DRAW SPEED" };
        StatBar[] bars = new StatBar[statNames.Length];
        float y = -326f;
        for (int i = 0; i < statNames.Length; i++)
        {
            bars[i] = CreateStatRow(right, statNames[i], y);
            y -= 64f;
        }

        // ---- Bottom: arrows, hint, back, start
        Button prev = MakeButton(NewRect("Previous Button", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-260f, 60f), new Vector2(80f, 80f)),
                                 "<", 44f, ButtonColor, Accent);
        Button next = MakeButton(NewRect("Next Button", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(260f, 60f), new Vector2(80f, 80f)),
                                 ">", 44f, ButtonColor, Accent);
        NewText(NewRect("Hint", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(400f, 32f)),
             "Drag to rotate  |  Scroll to zoom", 20f, DimText, TextAlignmentOptions.Center);

        Button back = MakeButton(NewRect("Back Button", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(220f, 64f)),
                                 "BACK", 26f, ButtonColor, TextColor);
        Button start = MakeButton(NewRect("Start Button", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(480f, 80f)),
                                  "START", 34f, Accent, Background);

        UnityEventTools.AddPersistentListener(prev.onClick, new UnityAction(menu.Previous));
        UnityEventTools.AddPersistentListener(next.onClick, new UnityAction(menu.Next));
        UnityEventTools.AddPersistentListener(back.onClick, new UnityAction(menu.Back));
        UnityEventTools.AddPersistentListener(start.onClick, new UnityAction(menu.Confirm));

        // ---- Wire the menu
        SerializedObject so = new SerializedObject(menu);
        SerializedProperty chars = so.FindProperty("characters");
        chars.arraySize = characters.Length;
        for (int i = 0; i < characters.Length; i++) chars.GetArrayElementAtIndex(i).objectReferenceValue = characters[i];
        so.FindProperty("previewStage").objectReferenceValue = stage;
        so.FindProperty("listContainer").objectReferenceValue = list;
        so.FindProperty("listButtonTemplate").objectReferenceValue = template;
        so.FindProperty("normalButtonColor").colorValue = ButtonColor;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("descriptionText").objectReferenceValue = descriptionText;
        so.FindProperty("healthBar").objectReferenceValue = bars[0];
        so.FindProperty("strengthBar").objectReferenceValue = bars[1];
        so.FindProperty("speedBar").objectReferenceValue = bars[2];
        so.FindProperty("throwPowerBar").objectReferenceValue = bars[3];
        so.FindProperty("reloadBar").objectReferenceValue = bars[4];
        so.FindProperty("drawSpeedBar").objectReferenceValue = bars[5];
        so.ApplyModifiedPropertiesWithoutUndo();

        return menu;
    }

    static StatBar CreateStatRow(Transform parent, string label, float y)
    {
        RectTransform row = NewRect("Stat " + label, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(-60f, 54f));

        NewText(NewRect("Label", row, new Vector2(0f, 1f), new Vector2(0.7f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 30f)),
             label, 21f, TextColor, TextAlignmentOptions.Left);
        TMP_Text value = NewText(NewRect("Value", row, new Vector2(0.7f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 30f)),
                              "0", 21f, Accent, TextAlignmentOptions.Right, FontStyles.Bold);

        RectTransform barBg = NewRect("Bar", row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(0f, 10f));
        NewPanel(barBg, new Color(1f, 1f, 1f, 0.08f), true, false);
        RectTransform fill = NewRect("Fill", barBg, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        NewPanel(fill, Accent, true, false);

        StatBar bar = row.gameObject.AddComponent<StatBar>();
        SerializedObject so = new SerializedObject(bar);
        so.FindProperty("fill").objectReferenceValue = fill;
        so.FindProperty("valueText").objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return bar;
    }

    // ---------------------------------------------------------------- Helpers

    static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    static Image NewPanel(RectTransform rt, Color color, bool rounded = true, bool raycast = true)
    {
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = rounded ? uiSprite : null;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    static TextMeshProUGUI NewText(RectTransform rt, string text, float size, Color color, TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
    {
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;   // so text never blocks dragging the model
        return tmp;
    }

    static Button MakeButton(RectTransform rt, string label, float fontSize, Color background, Color textColor)
    {
        Image image = NewPanel(rt, background);
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        // Slightly dimmed by default, full brightness on hover
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.82f, 0.82f, 0.82f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.6f, 0.6f, 0.6f);
        colors.selectedColor = new Color(0.82f, 0.82f, 0.82f);
        button.colors = colors;

        NewText(NewRect("Label", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
             label, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
        return button;
    }

    static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static Light NewLight(string name, LightType type, Color color, float intensity)
    {
        GameObject go = new GameObject(name);
        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        return light;
    }

    static Material CreateMaterial(string name, Color color, float metallic, float smoothness, Color? emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        string path = Folder + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }

    static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;

        // Right after the main menu
        int menuIndex = scenes.FindIndex(s => s.path.EndsWith("MainMenu.unity"));
        scenes.Insert(menuIndex >= 0 ? menuIndex + 1 : scenes.Count, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
