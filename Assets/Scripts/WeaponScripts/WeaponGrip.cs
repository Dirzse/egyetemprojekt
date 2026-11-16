using UnityEngine;

// Puts the weapon's model into the character's hand at runtime. The character model is only spawned when the game
// starts (character select), so there is no hand bone to drag the weapon under in the editor: this finds it by itself
// on any Humanoid character (Mixamo, etc.), also after a model swap.
// Every weapon model has its own grip (position, rotation, size): put this on each weapon prefab (next to Bow / Sword / ThrowingKnife ...).
// The Weapon object itself stays under the WeaponHolder, only the model moves; it is shown and hidden together with the weapon.
// Tune the grip in Play mode: select the model under the hand bone, move / rotate / scale it, then right click this
// component > "Save Grip" (and copy the values out of Play mode: Copy Component, after Play Paste Component Values on the prefab).
[DefaultExecutionOrder(200)]   // after the camera scripts
public class WeaponGrip : MonoBehaviour
{
    [Tooltip("The visible model of the weapon (a child of this object). Empty = the first child.")]
    [SerializeField] Transform model;
    [Tooltip("Bow: left hand. Sword, knife, gun: right hand.")]
    [SerializeField] HumanBodyBones hand = HumanBodyBones.RightHand;

    [Header("Grip of this weapon model")]
    [Tooltip("Position of the model relative to the hand bone.")]
    [SerializeField] Vector3 position;
    [Tooltip("Rotation of the model relative to the hand bone.")]
    [SerializeField] Vector3 rotation;
    [Tooltip("Size of the model in the hand. 0, 0, 0 = keep the size it has under the WeaponHolder.")]
    [SerializeField] Vector3 scale;

    [Tooltip("In first person the model stays where it is placed under the WeaponHolder (in front of the camera); " +
             "it only goes into the hand in third person. Off = always in the hand.")]
    [SerializeField] bool onlyInThirdPerson = true;

    Transform originalParent;
    Vector3 originalPosition;
    Quaternion originalRotation;
    Vector3 originalScale;
    bool inHand;
    WeaponHolder holder;
    ThirdPersonCamera thirdPersonCamera;

    void Awake()
    {
        if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        if (model == null) return;
        originalParent = model.parent;
        originalPosition = model.localPosition;
        originalRotation = model.localRotation;
        originalScale = model.localScale;
    }

    void OnEnable()
    {
        if (model != null) model.gameObject.SetActive(true);
    }

    // The model is not a child of the weapon while it is in the hand, so it has to be hidden with the weapon
    void OnDisable()
    {
        if (model != null && inHand) model.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (model != null && inHand) Destroy(model.gameObject);
    }

    void LateUpdate()
    {
        if (model == null) return;

        Transform bone = WantsHand() ? FindHand() : null;
        if (bone != null)
        {
            // Only when it goes into the hand (or the character changes), so it can be moved by hand in Play mode
            if (!inHand || model.parent != bone)
            {
                Vector3 worldSize = originalParent != null ? Vector3.Scale(originalParent.lossyScale, originalScale) : originalScale;
                model.SetParent(bone, false);
                ApplyGrip(bone, worldSize);
                inHand = true;
            }
        }
        else if (inHand)
        {
            model.SetParent(originalParent, false);
            model.localPosition = originalPosition;
            model.localRotation = originalRotation;
            model.localScale = originalScale;
            inHand = false;
        }
    }

    void ApplyGrip(Transform bone, Vector3 worldSize)
    {
        model.localPosition = position;
        model.localRotation = Quaternion.Euler(rotation);

        if (scale != Vector3.zero)
        {
            model.localScale = scale;
        }
        else
        {
            // Same size as before, whatever scale the character's bones have
            Vector3 boneScale = bone.lossyScale;
            model.localScale = new Vector3(Divide(worldSize.x, boneScale.x), Divide(worldSize.y, boneScale.y), Divide(worldSize.z, boneScale.z));
        }
    }

    static float Divide(float a, float b) => Mathf.Abs(b) > 0.0001f ? a / b : a;

    bool WantsHand()
    {
        if (!onlyInThirdPerson) return true;
        if (thirdPersonCamera == null)
        {
            PlayerMovement player = GetComponentInParent<PlayerMovement>();
            if (player != null) thirdPersonCamera = player.GetComponentInChildren<ThirdPersonCamera>();
        }
        return thirdPersonCamera != null && thirdPersonCamera.IsThirdPerson;
    }

    Transform FindHand()
    {
        if (holder == null) holder = GetComponentInParent<WeaponHolder>();
        Animator animator = holder != null ? holder.CharacterAnimator : null;
        if (animator == null || !animator.isHuman) return null;
        return animator.GetBoneTransform(hand);
    }

    [ContextMenu("Save Grip")]
    void SaveGrip()
    {
        if (model == null || !inHand)
        {
            Debug.LogWarning("WeaponGrip: the model is not in the hand right now (Play mode, third person, weapon in hand).", this);
            return;
        }

        position = model.localPosition;
        rotation = model.localEulerAngles;
        scale = model.localScale;
        Debug.Log("WeaponGrip: grip saved for " + name + ". Copy the component values before stopping Play.", this);
    }
}
