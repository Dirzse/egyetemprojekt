using UnityEngine;

// Applies the selected character's stats to the player. Put it on the Player object.
// Runs before the other player scripts so CharacterHealth already sees the new maxHealth in Start.
[DefaultExecutionOrder(-100)]
public class PlayerStats : MonoBehaviour
{
    [Tooltip("Used when the game scene is started directly from the editor (nothing was selected in the menu).")]
    [SerializeField] CharacterData defaultCharacter;

    [Tooltip("The character model under the Player (e.g. Survival_Character). It is replaced by the selected character's model.")]
    [SerializeField] Transform characterModel;

    public CharacterData Data { get; private set; }
    public Transform CharacterModel => characterModel;

    public float DamageMultiplier => Data != null ? Data.damageMultiplier : 1f;
    public float ReloadSpeedMultiplier => Data != null ? Data.reloadSpeedMultiplier : 1f;
    public float DrawSpeedMultiplier => Data != null ? Data.drawSpeedMultiplier : 1f;
    public float ThrowPowerMultiplier => Data != null ? Data.throwPowerMultiplier : 1f;

    void Awake()
    {
        Data = CharacterSelection.Selected != null ? CharacterSelection.Selected : defaultCharacter;
        if (Data == null)
        {
            Debug.LogWarning("PlayerStats: no character selected and no default set, using the values on the player.");
            return;
        }

        // Swap the body first, so everything below already works on the new model
        GameObject model = Data.gameModel != null ? Data.gameModel : Data.previewModel;
        if (characterModel == null)
        {
            // Not assigned: use the model with the Animator under the player
            Animator found = GetComponentInChildren<Animator>();
            if (found != null) characterModel = found.transform;
        }
        if (model != null && characterModel != null)
            characterModel = CharacterModelSwap.Swap(transform, characterModel, model, Data.modelScale);

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.walkSpeed = Data.walkSpeed;
            movement.runSpeed = Data.runSpeed;
            movement.jumpHeight = Data.jumpHeight;
        }

        CharacterHealth health = GetComponent<CharacterHealth>();
        if (health != null) health.maxHealth = Data.maxHealth;

        // Knife count depends on the character (true = also finds the knife while it is not equipped)
        ThrowingKnife knives = GetComponentInChildren<ThrowingKnife>(true);
        if (knives != null) knives.SetKnives(Data.startingKnives, Data.maxKnives);
    }
}
