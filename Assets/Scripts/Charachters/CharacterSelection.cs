// Holds the character picked in the menu. Static, so it survives scene loads
// (also the reload after death, so you keep the same character).
public static class CharacterSelection
{
    public static CharacterData Selected;

    // WeaponHolder slot to have in hand at the start (0 = bow, 1 = sword, 2 = knife). -1 = not chosen.
    public static int StartingWeaponSlot = -1;
    // (StartingWeaponSlot is not used any more, the starting weapons are in Primaries.)

    // Primary weapon chosen for each of the character's primary slots. null = not chosen (the character's default weapons are used).
    public static PrimaryWeaponData[] Primaries;
}
