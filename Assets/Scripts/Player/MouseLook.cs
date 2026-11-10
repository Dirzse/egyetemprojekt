using UnityEngine;
using UnityEngine.Rendering;

public class MouseLook : MonoBehaviour
{
    [Header("Nézet beállítások")]
    // Az egér érzékenysége
    public float mouseSensitivity = 800f;

    // Hivatkozás a játékos fő testére (ezt fogjuk forgatni az X tengely mentén)
    public Transform playerBody;

    [Tooltip("How far the player can look up (degrees).")]
    public float maxLookUp = 80f;
    [Tooltip("How far the player can look down (degrees). Lower = the player cannot look down at their own body.")]
    public float maxLookDown = 55f;

    [Tooltip("First person: the own body is not drawn for the camera, only its shadow. Weapons stay visible.")]
    public bool hideOwnBody = true;

    // A kamera fel-le forgásának belső értéke
    private float xRotation = 0f;

    private Vector3 firstPersonPosition;
    private SkinnedMeshRenderer[] bodyRenderers;
    private ShadowCastingMode[] originalShadowModes;
    private bool bodyHidden;

    void Start()
    {
        // Játék indulásakor az egeret középre zárjuk és eltüntetjük a képernyőről
        Cursor.lockState = CursorLockMode.Locked;

        firstPersonPosition = transform.localPosition;
        EnsurePlayerBody();
    }

    // playerBody must be the Player (the object with PlayerMovement), not the character model
    void EnsurePlayerBody()
    {
        if (playerBody != null) return;
        PlayerMovement movement = GetComponentInParent<PlayerMovement>();
        if (movement != null) playerBody = movement.transform;
    }

    void Update()
    {
        // Egér bemenetek lekérése, szorozva az érzékenységgel és az idővel (függetlenítve a framerate-től)
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Az Y egérmozgás (fel-le) kivonása a belső forgási értékből (ha hozzáadnánk, fordítva működne a kamera)
        xRotation -= mouseY;

        // A forgás korlátozása -90 és 90 fok közé, hogy ne tudjuk "áthajtani" a fejet a hátunkba
        xRotation = Mathf.Clamp(xRotation, -maxLookUp, maxLookDown);

        // A kamera fel-le forgatása a korlátozott érték alapján
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // A teljes játékos karakter elforgatása jobbra-balra az X egérmozgás alapján
        EnsurePlayerBody();
        if (playerBody != null) playerBody.Rotate(Vector3.up * mouseX);
    }

    void LateUpdate()
    {
        // A third person camera is moved away from the head: then the body is shown again
        bool firstPerson = (transform.localPosition - firstPersonPosition).sqrMagnitude < 0.09f;
        SetBodyHidden(hideOwnBody && firstPerson);
    }

    void SetBodyHidden(bool hide)
    {
        if (bodyRenderers == null || bodyRenderers.Length == 0 || bodyRenderers[0] == null) FindBody();
        if (bodyRenderers == null || hide == bodyHidden) return;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] == null) continue;
            bodyRenderers[i].shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : originalShadowModes[i];
        }
        bodyHidden = hide;
    }

    // Only the character's skinned meshes: weapons and other attached props stay visible
    void FindBody()
    {
        bodyRenderers = null;
        bodyHidden = false;
        if (playerBody == null) return;

        Animator animator = playerBody.GetComponentInChildren<Animator>();
        if (animator == null) return;

        bodyRenderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        originalShadowModes = new ShadowCastingMode[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++)
            originalShadowModes[i] = bodyRenderers[i].shadowCastingMode;
    }
}