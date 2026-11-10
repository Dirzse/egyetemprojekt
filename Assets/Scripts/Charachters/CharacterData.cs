using UnityEngine;

// One playable character's stats. Create with: right click > Create > Game > Character Data
[CreateAssetMenu(fileName = "NewCharacter", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Info")]
    public string characterName = "New Character";
    [TextArea] public string description;
    public Sprite portrait;
    [Tooltip("Model shown on the character select screen (just the model, e.g. the FBX, not the Player prefab).")]
    public GameObject previewModel;

    [Header("In-game model")]
    [Tooltip("Model the player uses in the game. Empty = the preview model is used. " +
             "Use a Humanoid rig, so the player's Animator Controller works on every character.")]
    public GameObject gameModel;
    [Tooltip("Extra scale on top of the default model's scale (for models exported at a different size).")]
    public float modelScale = 1f;

    [Header("Speed")]
    public float walkSpeed = 5f;
    public float runSpeed = 12f;
    public float jumpHeight = 1.5f;

    [Header("Strength")]
    [Tooltip("Multiplies the damage of every weapon (1 = normal, 1.5 = +50%).")]
    public float damageMultiplier = 1f;
    public float maxHealth = 100f;
    [Tooltip("How hard the bow string / the arm throws: multiplies arrow and thrown knife speed (1 = normal).")]
    public float throwPowerMultiplier = 1f;

    [Header("Dexterity")]
    [Tooltip("Reload speed (1 = normal, 2 = twice as fast).")]
    public float reloadSpeedMultiplier = 1f;
    [Tooltip("How fast weapons are drawn (1 = normal, 2 = twice as fast). Also the draw animation speed.")]
    public float drawSpeedMultiplier = 1f;

    [Header("Knives")]
    [Tooltip("Knives the character starts with.")]
    public int startingKnives = 3;
    [Tooltip("Most knives the character can carry (picking thrown knives back up stops here).")]
    public int maxKnives = 5;
}
