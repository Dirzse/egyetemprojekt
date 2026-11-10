using UnityEngine;

// Knives carried on the body (e.g. a strap on the thigh). Put this on an empty object that is a child of a
// bone (e.g. thigh_r), and the knife models as its children: they follow the animation automatically.
// ThrowingKnife tells it how many knives are in the strap; the first N slots are shown, the rest hidden.
public class KnifeHolster : MonoBehaviour
{
    [Tooltip("One knife model per slot, in the order they should disappear from last to first.")]
    [SerializeField] GameObject[] knifeSlots;

    public int Capacity => knifeSlots != null ? knifeSlots.Length : 0;

    public void Show(int count)
    {
        if (knifeSlots == null) return;
        for (int i = 0; i < knifeSlots.Length; i++)
        {
            if (knifeSlots[i] != null) knifeSlots[i].SetActive(i < count);
        }
    }
}
