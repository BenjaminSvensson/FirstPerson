using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ObjectCreator : MonoBehaviour
{
    [Header("UI")]
    public RawImage createIcon;

    [Header("References")]
    public Transform holdPoint;         
    public GameObject prefab;           
    public Camera cam;                  
    public Player player; // drag your Player object here in Inspector

    [Header("Scaling")]
    public float minScale = 0.1f;
    public float maxScale = 2f;
    public float growSpeed = 1f;

    [Header("Placement")]
    public float placeRange = 3f;       

    [Header("Visuals")]
    [Tooltip("Starting alpha when object is first created.")]
    [Range(0f,1f)] public float startAlpha = 0.4f;

    [Header("Cooldown")]
    public float createCooldown = 1f;   // ✅ how long to wait before creating again
    private float lastCreateTime;

    private PlayerInputActions input;
    private GameObject currentObject;
    private bool isHolding;
    private float currentScale;
    private Material currentMaterial;
    private Color baseColor;

    private void Awake()
    {
        input = new PlayerInputActions();
        input.Player.Enable();

        input.Player.Interact.started += ctx => TryStartCreate();
        input.Player.Interact.canceled += ctx => ReleaseObject();
    }

    private void Update()
    {
        UpdateCreateUI();

        if (isHolding && currentObject != null)
        {
            // Grow until max
            if (currentScale < maxScale)
            {
                currentScale += growSpeed * Time.deltaTime;
                currentScale = Mathf.Min(currentScale, maxScale);
                currentObject.transform.localScale = Vector3.one * currentScale;
            }

            // Fade alpha based on growth progress
            if (currentMaterial != null)
            {
                float t = Mathf.InverseLerp(minScale, maxScale, currentScale);
                float alpha = Mathf.Lerp(startAlpha, 1f, t);
                Color c = baseColor;
                c.a = alpha;
                currentMaterial.color = c;
            }

            // Continuously clamp position against walls/floor
            currentObject.transform.position = GetSafePlacementPosition(currentScale);
            currentObject.transform.rotation = holdPoint.rotation;
        }
    }

    private void TryStartCreate()
    {
        // Check cooldown
        if (Time.time < lastCreateTime + createCooldown) return;

        if (isHolding || currentObject != null) return;
        StartCreate();
    }
    private void UpdateCreateUI()
    {
        if (createIcon == null) return;

        bool ready = Time.time >= lastCreateTime + createCooldown;
        Color c = createIcon.color;
        c.a = ready ? 1f : 0.3f;
        createIcon.color = c;
    }


    private void StartCreate()
    {
        // ✅ Mark cooldown start
        lastCreateTime = Time.time;

        Vector3 spawnPos = GetSafePlacementPosition(minScale);
        Quaternion spawnRot = holdPoint.rotation;

        currentObject = Instantiate(prefab, spawnPos, spawnRot);
        currentObject.transform.localScale = Vector3.one * minScale;
        currentScale = minScale;

        // Disable physics + collider while held
        Rigidbody rb = currentObject.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Collider col = currentObject.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Grab material instance for fading
        Renderer rend = currentObject.GetComponent<Renderer>();
        if (rend != null)
        {
            currentMaterial = rend.material; // unique instance
            baseColor = currentMaterial.color;
            Color c = baseColor;
            c.a = startAlpha;
            currentMaterial.color = c;
        }

        isHolding = true;

        // Start creation loop sound
        if (player != null) player.StartCreateLoop();
    }

    private void ReleaseObject()
    {
        if (!isHolding || currentObject == null) return;

        // Enable physics + collider
        Rigidbody rb = currentObject.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        Collider col = currentObject.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        // Force material fully opaque
        if (currentMaterial != null)
        {
            Color c = baseColor;
            c.a = 1f;
            currentMaterial.color = c;
        }

        currentObject = null;
        currentMaterial = null;
        isHolding = false;

        // Stop creation loop sound
        if (player != null) player.StopCreateLoop();
    }

    private Vector3 GetSafePlacementPosition(float scale)
    {
        Vector3 spawnPos = holdPoint.position;
        float radius = scale * 0.5f;

        if (Physics.SphereCast(cam.transform.position, radius, cam.transform.forward,
                               out RaycastHit hit, placeRange, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 adjusted = hit.point - cam.transform.forward * radius;

            // Snap to floor if horizontal, else keep Y
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.7f)
                adjusted.y = hit.point.y + radius;
            else
                adjusted.y = holdPoint.position.y;

            spawnPos = adjusted;
        }

        return spawnPos;
    }
}
