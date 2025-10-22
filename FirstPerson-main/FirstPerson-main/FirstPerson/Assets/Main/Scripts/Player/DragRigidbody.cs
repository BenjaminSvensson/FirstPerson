using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DragRigidbody : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Transform holdPoint;       // Empty GameObject in front of camera
    public Rigidbody grabberRb;       // Kinematic Rigidbody child of camera

    [Header("Settings")]
    public float grabRange = 3f;
    public float holdForce = 150f;
    public float dragSpeed = 10f;

    [Header("Crosshair UI")]
    public RawImage normalCrosshair;
    public RawImage hoverCrosshair;
    public RawImage grabCrosshair;

    private Rigidbody heldObject;
    private SpringJoint joint;

    private PlayerInputActions inputActions;
    private InputAction dragAction;
    private bool isDragging;

    void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    void OnEnable()
    {
        dragAction = inputActions.Player.Drag;
        dragAction.started += OnDragStarted;
        dragAction.canceled += OnDragCanceled;
        inputActions.Enable();
    }

    void OnDisable()
    {
        dragAction.started -= OnDragStarted;
        dragAction.canceled -= OnDragCanceled;
        inputActions.Disable();
    }

    void Update()
    {
        if (isDragging && heldObject != null)
        {
            MoveGrabber();
            ShowCrosshair(grabCrosshair);
        }
        else
        {
            // Check if we're looking at a draggable object
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out RaycastHit hit, grabRange))
            {
                if (hit.collider.CompareTag("Draggable"))
                {
                    ShowCrosshair(hoverCrosshair);
                    return;
                }
            }

            // Default crosshair
            ShowCrosshair(normalCrosshair);
        }
    }

    private void OnDragStarted(InputAction.CallbackContext ctx)
    {
        TryGrabObject();
    }

    private void OnDragCanceled(InputAction.CallbackContext ctx)
    {
        ReleaseObject();
    }

    void TryGrabObject()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, grabRange))
        {
            if (hit.collider.CompareTag("Draggable"))
            {
                Rigidbody rb = hit.collider.attachedRigidbody;
                if (rb != null)
                {
                    heldObject = rb;
                    isDragging = true;

                    joint = grabberRb.gameObject.AddComponent<SpringJoint>();
                    joint.autoConfigureConnectedAnchor = false;
                    joint.connectedBody = rb;
                    joint.anchor = Vector3.zero;
                    joint.connectedAnchor = rb.transform.InverseTransformPoint(hit.point);

                    joint.spring = holdForce;
                    joint.damper = 5f;
                    joint.maxDistance = 0.1f;

                    rb.linearDamping = 10;
                    rb.angularDamping = 5;
                }
            }
        }
    }

    void MoveGrabber()
    {
        grabberRb.MovePosition(Vector3.Lerp(
            grabberRb.position,
            holdPoint.position,
            Time.deltaTime * dragSpeed
        ));
    }

    void ReleaseObject()
    {
        if (joint != null)
        {
            Destroy(joint);
        }

        if (heldObject != null)
        {
            heldObject.linearDamping = 0;
            heldObject.angularDamping = 0.05f;
            heldObject = null;
        }

        isDragging = false;
    }

    void ShowCrosshair(RawImage active)
    {
        // Hide all
        normalCrosshair.enabled = false;
        hoverCrosshair.enabled = false;
        grabCrosshair.enabled = false;

        // Show the one we want
        if (active != null)
            active.enabled = true;
    }
}
