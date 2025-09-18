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
    public LayerMask crouchObstructionMask = ~0;

    [Header("Jump Assist")]
    public float coyoteTime = 0.15f;       // grace period after leaving ground
    public float jumpBufferTime = 0.15f;   // grace period before landing

    private CharacterController controller;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isRunning;
    private bool isCrouching;
    private bool isGrounded;

    // Lean
    private float leanInput;

    // Step offset handling
    private float originalStepOffset;
    private float centerX, centerZ;

    // Jump assist timers
    private float lastGroundedTime;
    private float lastJumpPressedTime;

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

        // Record jump press time for buffering
        inputActions.Player.Jump.performed += ctx => lastJumpPressedTime = Time.time;

        inputActions.Player.Crouch.performed += ctx => ToggleCrouch();

        // Lean inputs
        inputActions.Player.LeanLeft.performed += ctx => leanInput = -1f;
        inputActions.Player.LeanLeft.canceled += ctx => { if (leanInput < 0f) leanInput = 0f; };

        inputActions.Player.LeanRight.performed += ctx => leanInput = 1f;
        inputActions.Player.LeanRight.canceled += ctx => { if (leanInput > 0f) leanInput = 0f; };
    }

    private void Start()
    {
        playerCamera.SetCrouchHeightsFromCamera();
        playerCamera.SetCrouchSpeed(crouchTransitionSpeed);
    }

    private void Update()
    {
        HandleMovement();
        HandleCrouchTransition();

        // Jump assist check
        if (!isCrouching &&
            Time.time - lastGroundedTime <= coyoteTime &&
            Time.time - lastJumpPressedTime <= jumpBufferTime)
        {
            Jump();
            lastJumpPressedTime = -999f; // reset so it doesn't trigger twice
        }

        // Send lean to camera
        playerCamera.SetLean(leanInput);
    }

    private void LateUpdate()
    {
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
        if (isGrounded)
        {
            lastGroundedTime = Time.time; // update coyote timer
            if (velocity.y < 0f)
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
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    private void ToggleCrouch()
    {
        if (isCrouching)
        {
            if (HasHeadroomToStand())
            {
                isCrouching = false;
                controller.stepOffset = originalStepOffset;
                playerCamera.SetCrouchState(false);
            }
        }
        else
        {
            isCrouching = true;
            controller.stepOffset = 0f;
            playerCamera.SetCrouchState(true);
        }
    }

    private bool HasHeadroomToStand()
    {
        float radius = controller.radius - controller.skinWidth - 0.01f;
        Vector3 bottom = transform.position + Vector3.up * radius;
        Vector3 top = transform.position + Vector3.up * (standingHeight - radius);
        return !Physics.CheckCapsule(bottom, top, radius, crouchObstructionMask, QueryTriggerInteraction.Ignore);
    }
    
    private void HandleCrouchTransition()
    {
        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(centerX, controller.height * 0.5f, centerZ);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        body.linearVelocity = pushDir * 2f;
    }
}
