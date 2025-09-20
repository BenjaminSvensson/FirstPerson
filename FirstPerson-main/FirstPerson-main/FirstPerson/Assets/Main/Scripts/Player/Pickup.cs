using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupDistance = 3f;
    public LayerMask pickupMask;
    public Transform holdPoint; // empty GameObject in front of camera

    [Header("Debug")]
    public bool debugRay = true;
    public Color rayColorNoHit = Color.red;
    public Color rayColorHit = Color.green;
    public Color hitPointColor = Color.yellow;
    public float hitPointGizmoRadius = 0.05f;

    private Camera cam;
    private PlayerInputActions inputActions;

    private Rigidbody heldObject;
    private Collider heldCollider;

    // Cache last raycast info for gizmo drawing
    private bool lastHadHit;
    private Vector3 lastRayOrigin;
    private Vector3 lastRayDir;
    private float lastRayLength;
    private Vector3 lastHitPoint;
    private Collider lastHitCollider;

    private void Awake()
    {
        cam = Camera.main;
        if (cam == null)
            Debug.LogWarning("PlayerPickup: Camera.main not found. Assign a Camera tagged MainCamera.");

        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();

        inputActions.Player.Interact.performed += ctx => TryPickup();
        inputActions.Player.Drop.performed += ctx => Drop();
    }
    private void OnDestroy()
    {
        inputActions.Player.Interact.performed -= ctx => TryPickup();
        inputActions.Player.Drop.performed -= ctx => Drop();
    }


    private void Update()
    {
        // Always draw the ray for debugging
        if (debugRay && cam != null)
        {
            var origin = cam.transform.position;
            var dir = cam.transform.forward;
            lastRayOrigin = origin;
            lastRayDir = dir;
            lastRayLength = pickupDistance;

            // Perform a dry run raycast just for visualization/logging
            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, pickupDistance, pickupMask))
            {
                lastHadHit = true;
                lastHitPoint = hit.point;
                lastHitCollider = hit.collider;

                Debug.DrawRay(origin, dir * hit.distance, rayColorHit);
                Debug.DrawRay(origin + dir * hit.distance, Vector3.zero, rayColorHit); // segment end
            }
            else
            {
                lastHadHit = false;
                lastHitCollider = null;
                Debug.DrawRay(origin, dir * pickupDistance, rayColorNoHit);
            }
        }
    }
    
    private void TryPickup()
    {
        Debug.Log("Gooning pickup initaaited");
        if (heldObject != null) return;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, pickupDistance, pickupMask))
        {
            // Debug line to hit point
            if (debugRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, rayColorHit, 0.5f);
                Debug.Log($"[Pickup] Ray hit: {hit.collider.name} (tag: {hit.collider.tag}) at {hit.point}");
            }

            if (hit.collider.CompareTag("Pickup"))
            {
                var rb = hit.rigidbody != null ? hit.rigidbody : hit.collider.attachedRigidbody;
                if (rb == null)
                {
                    Debug.LogWarning($"[Pickup] '{hit.collider.name}' has no Rigidbody. Add one to pick up.");
                    return;
                }

                heldObject = rb;
                heldCollider = hit.collider;

                // Disable physics while held
                heldObject.isKinematic = true;
                if (heldCollider != null) heldCollider.enabled = false;

                // Parent to hold point
                if (holdPoint == null)
                {
                    Debug.LogWarning("[Pickup] HoldPoint not assigned. Create an empty child on the camera and assign it.");
                    return;
                }

                heldObject.transform.SetParent(holdPoint);
                heldObject.transform.localPosition = Vector3.zero;
                heldObject.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.Log($"[Pickup] Hit '{hit.collider.name}' but tag is '{hit.collider.tag}', not 'Pickup'.");
            }
        }
        else
        {
            if (debugRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * pickupDistance, rayColorNoHit, 0.5f);
                Debug.Log("[Pickup] Raycast hit nothing within range/mask.");
            }
        }
    }

    private void Drop()
    {
        if (heldObject == null) return;

        heldObject.transform.SetParent(null);

        heldObject.isKinematic = false;
        if (heldCollider != null) heldCollider.enabled = true;

        // Small forward impulse
        if (cam != null)
            heldObject.AddForce(cam.transform.forward * 2f, ForceMode.Impulse);

        heldObject = null;
        heldCollider = null;
    }

    private void OnDrawGizmos()
    {
        if (!debugRay) return;

        // Draw the full ray path as gizmo (in Edit/Play with Gizmos on)
        Gizmos.color = lastHadHit ? rayColorHit : rayColorNoHit;
        Gizmos.DrawLine(lastRayOrigin, lastRayOrigin + lastRayDir * lastRayLength);

        if (lastHadHit)
        {
            Gizmos.color = hitPointColor;
            Gizmos.DrawSphere(lastHitPoint, hitPointGizmoRadius);

            // Label-ish log on scene view when selected
            #if UNITY_EDITOR
            UnityEditor.Handles.color = hitPointColor;
            UnityEditor.Handles.Label(lastHitPoint + Vector3.up * 0.05f,
                lastHitCollider != null ? $"Hit: {lastHitCollider.name}" : "Hit");
            #endif
        }
    }
}
