using UnityEngine;

// A robbanás-prefab gyökerére kerül. A particle systemek mellett fényvillanást és hangot is kezel.
public class ExplosionEffect : MonoBehaviour
{
    [SerializeField] Light flashLight;           // opcionális Point Light a robbanás közepén
    [SerializeField] float flashDuration = 0.25f;
    [SerializeField] AudioClip sound;            // opcionális robbanáshang
    [SerializeField] float lifetime = 4f;        // ennyi mp után törlődik az egész prefab

    float startTime;
    float startIntensity;

    void Start()
    {
        startTime = Time.time;
        if (flashLight) startIntensity = flashLight.intensity;
        if (sound) AudioSource.PlayClipAtPoint(sound, transform.position);
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (!flashLight) return;
        float k = 1f - (Time.time - startTime) / flashDuration;
        flashLight.intensity = startIntensity * Mathf.Clamp01(k);
    }
}