using UnityEngine;

// Marks something attached to the character model that must survive a model swap,
// e.g. an empty socket on a bone that no script references.
// Objects with a renderer or a script on them, or that a script references, are kept automatically.
public class ModelAttachment : MonoBehaviour
{
}
