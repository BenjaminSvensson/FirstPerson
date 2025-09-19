using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectCreator : MonoBehaviour
{
    [Header("References")]
    public Transform holdPoint;         
    public GameObject prefab;           
    public Camera cam;                  

    [Header("Scaling")]
    public float minScale = 0.1f;
    public float maxScale = 2f;
    public float growSpeed = 1f;

    [Header("Placement")]
    public float placeRange = 3f;       // how far forward to check for walls

    [Header("Cooldown")]
    public float createCooldown = 0.5f; // seconds between spawns

    private PlayerInputActions input;
    private GameObject currentObject;
    private bool isHolding;
    private float currentScale;
    private float lastCreateTime;

    private void Awake()
    {
        input = new PlayerInputActions();
        input.Player.Enable();

        input.Player.Interact.started += ctx => TryStartCreate();
        input.Player.Interact.canceled += ctx => ReleaseObject();
    }

    private void Update()
    {
        if (isHolding && currentObject != null)
        {
            // Grow until max
            if (currentScale < maxScale)
            {
                currentScale += growSpeed * Time.deltaTime;
                currentScale = Mathf.Min(currentScale, maxScale);
                currentObject.transform.localScale = Vector3.one * currentScale;
            }

            // Continuously clamp position against walls/floor
            currentObject.transform.position = GetSafePlacementPosition(currentScale);
            currentObject.transform.rotation = holdPoint.rotation;
        }
    }

    private void TryStartCreate()
    {
        // ✅ Cooldown + holding guard
        if (isHolding || currentObject != null) return;
        if (Time.time < lastCreateTime + createCooldown) return;

        StartCreate();
    }

    private void StartCreate()
    {
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

        isHolding = true;
        lastCreateTime = Time.time; // ✅ mark cooldown
    }

    private void ReleaseObject()
    {
        if (!isHolding || currentObject == null) return;

        // Enable physics + collider
        Rigidbody rb = currentObject.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        Collider col = currentObject.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        currentObject = null;
        isHolding = false;
    }

    private Vector3 GetSafePlacementPosition(float scale)
    {
        Vector3 spawnPos = holdPoint.position;
        float radius = scale * 0.5f;

        if (Physics.SphereCast(cam.transform.position, radius, cam.transform.forward,
                               out RaycastHit hit, placeRange, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 adjusted = hit.point - cam.transform.forward * radius;

            // ✅ Snap to floor if surface is horizontal, otherwise keep Y
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.7f)
                adjusted.y = hit.point.y + radius;
            else
                adjusted.y = holdPoint.position.y;

            spawnPos = adjusted;
        }

        return spawnPos;
    }
}
