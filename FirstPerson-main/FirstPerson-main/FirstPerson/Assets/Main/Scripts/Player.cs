using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    [Header("References")]
    public PlayerCamera playerCamera;

    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float crouchSpeed = 2f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.4f;

    [Header("Crouch")]
    public float crouchHeight = 1f;
    public float standingHeight = 2f;
    public float crouchTransitionSpeed = 10f;

    [Tooltip("Layers considered solid for headroom check when uncrouching.")]
    public LayerMask crouchObstructionMask = ~0;

    private CharacterController controller;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isRunning;
    private bool isCrouching;
    private bool isGrounded;

    // Lean
    private float leanInput;

    // Step offset handling to reduce jitter on slopes while crouched
    private float originalStepOffset;

    // Cache X/Z of center so we only control Y
    private float centerX, centerZ;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        originalStepOffset = controller.stepOffset;
        centerX = controller.center.x;
        centerZ = controller.center.z;

        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Player.Sprint.performed += ctx => isRunning = true;
        inputActions.Player.Sprint.canceled += ctx => isRunning = false;

        inputActions.Player.Jump.performed += ctx => Jump();
        inputActions.Player.Crouch.performed += ctx => ToggleCrouch();

        // Lean inputs
        inputActions.Player.LeanLeft.performed += ctx => leanInput = -1f;
        inputActions.Player.LeanLeft.canceled += ctx => { if (leanInput < 0f) leanInput = 0f; };

        inputActions.Player.LeanRight.performed += ctx => leanInput = 1f;
        inputActions.Player.LeanRight.canceled += ctx => { if (leanInput > 0f) leanInput = 0f; };
    }

    private void Start()
    {
        // Initialize camera crouch blending targets (camera handles the visuals)
        playerCamera.SetCrouchHeightsFromCamera(); // uses its serialized standing/crouching Y or auto-infers
        playerCamera.SetCrouchSpeed(crouchTransitionSpeed);
    }

    private void Update()
    {
        HandleMovement();
        HandleCrouchTransition();

        // Send lean to camera
        playerCamera.SetLean(leanInput);
    }

    private void LateUpdate()
    {
        // Update camera effects after movement so it reads settled values (reduces jitter)
        float currentSpeed = controller.velocity.magnitude;
        bool isMoving = new Vector2(moveInput.x, moveInput.y).sqrMagnitude > 0.0001f;

        playerCamera.UpdateEffects(
            speed: currentSpeed,
            isRunning: isRunning,
            isMoving: isMoving,
            grounded: isGrounded,
            yVelocity: velocity.y
        );
    }

    private void HandleMovement()
    {
        // Ground check
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f; // Keeps grounded
        }

        // Move input to world space
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        float targetSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
        controller.Move(move * targetSpeed * Time.deltaTime);

        // Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void Jump()
    {
        if (isGrounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    private void ToggleCrouch()
    {
        if (isCrouching)
        {
            // Attempt to stand up — only if there is headroom
            if (HasHeadroomToStand())
            {
                isCrouching = false;
                controller.stepOffset = originalStepOffset;
                playerCamera.SetCrouchState(false);
            }
            // else remain crouched (camera stays)
        }
        else
        {
            // Go into crouch
            isCrouching = true;
            controller.stepOffset = 0f; // reduce slope jitter while crouched
            playerCamera.SetCrouchState(true);
        }
    }

    private bool HasHeadroomToStand()
    {
        crouchObstructionMask &= ~(1 << gameObject.layer);

        float radius = controller.radius - controller.skinWidth;

        Vector3 bottom = transform.position + Vector3.up * radius;
        Vector3 top = transform.position + Vector3.up * (standingHeight - radius);

        return !Physics.CheckCapsule(bottom, top, radius, crouchObstructionMask, QueryTriggerInteraction.Ignore);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        if (body == null || body.isKinematic)
            return;

        if (hit.moveDirection.y < -0.3f)
            return;

        // Calculate push direction (horizontal only)
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        // Apply velocity change
        body.linearVelocity = pushDir * 2f; // tweak multiplier for push strength
    }





    private void HandleCrouchTransition()
    {
        // Smoothly change collider height
        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);

        // Force center so the bottom stays fixed: center.y = height/2
        controller.center = new Vector3(centerX, controller.height * 0.5f, centerZ);
    }
}
