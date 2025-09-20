using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class DestroyAfterTimeWithFade : MonoBehaviour
{
    [Tooltip("How long before this object is destroyed (in seconds).")]
    public float lifetime = 6f;

    [Tooltip("How long the fade-out lasts at the end (in seconds).")]
    public float fadeDuration = 1f;

    private float timer;
    private Material material;
    private Color originalColor;

    private void Start()
    {
        // Get a unique instance of the material so we don’t affect other objects
        material = GetComponent<Renderer>().material;
        originalColor = material.color;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // Start fading when we’re within fadeDuration of destruction
        if (timer >= lifetime - fadeDuration)
        {
            float t = (lifetime - timer) / fadeDuration; // goes from 1 → 0
            Color c = originalColor;
            c.a = Mathf.Clamp01(t);
            material.color = c;
        }

        // Destroy when lifetime is up
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
