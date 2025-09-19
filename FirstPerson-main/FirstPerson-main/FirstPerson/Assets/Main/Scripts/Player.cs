using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    [Header("References")]
    public PlayerCamera playerCamera;
    public Camera cam; // assign your main camera in inspector

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float crouchSpeed = 2f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.4f;

    [Header("Slide / Crouch")]
    public float slideFriction = 5f;
    public float slideBoost = 5f;
    public float crouchHeight = 1f;
    public float standingHeight = 2f;
    public float crouchTransitionSpeed = 10f;
    public LayerMask crouchObstructionMask = ~0;
    public float slideGravityMultiplier = 2f;

    [Header("Jump Assist")]
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("Kick Ability")]
    public float kickUpForce = 8f;
    public float kickBackForce = 6f;
    public float kickCooldown = 0.2f;
    public float kickRange = 3f;
    public float kickRadius = 0.5f;
    public float kickFriction = 5f;
    public LayerMask kickMask = ~0;

    private CharacterController controller;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isGrounded;

    // Slide state
    private bool isSliding;
    private bool slideHeld;
    private Vector3 slideVelocity;

    // Kick state
    private Vector3 kickVelocity;
    private float lastKickTime;

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

        inputActions.Player.Jump.performed += ctx => lastJumpPressedTime = Time.time;

        inputActions.Player.Crouch.performed += ctx => { slideHeld = true; StartSlide(); };
        inputActions.Player.Crouch.canceled += ctx => { slideHeld = false; StopSlide(); };

        inputActions.Player.Kick.performed += ctx => DoKick();

        // Lean inputs
        inputActions.Player.LeanLeft.performed += ctx => leanInput = -1f;
        inputActions.Player.LeanLeft.canceled += ctx => { if (leanInput < 0f) leanInput = 0f; };

        inputActions.Player.LeanRight.performed += ctx => leanInput = 1f;
        inputActions.Player.LeanRight.canceled += ctx => { if (leanInput > 0f) leanInput = 0f; };
    }

    private void Update()
    {
        HandleMovement();
        HandleCrouchTransition();

        // Jump assist
        if (Time.time - lastGroundedTime <= coyoteTime &&
            Time.time - lastJumpPressedTime <= jumpBufferTime)
        {
            Jump();
            lastJumpPressedTime = -999f;
        }

        playerCamera.SetLean(leanInput);
        playerCamera.SetSliding(isSliding);
    }

    private void LateUpdate()
    {
        float currentSpeed = controller.velocity.magnitude;
        bool isMoving = moveInput.sqrMagnitude > 0.0001f;

        playerCamera.UpdateEffects(
            speed: currentSpeed,
            isRunning: true,
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
            lastGroundedTime = Time.time;
            if (velocity.y < 0f) velocity.y = -2f;
        }

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        if (isSliding)
        {
            slideVelocity = Vector3.Lerp(slideVelocity, Vector3.zero, slideFriction * Time.deltaTime);

            if (slideVelocity.magnitude > 0.1f)
                controller.Move(slideVelocity * Time.deltaTime);
            else
                controller.Move(move * crouchSpeed * Time.deltaTime);
        }
        else
        {
            controller.Move(move * moveSpeed * Time.deltaTime);
        }

        // Apply kick push if active
        if (kickVelocity.magnitude > 0.1f)
        {
            controller.Move(kickVelocity * Time.deltaTime);
            kickVelocity = Vector3.Lerp(kickVelocity, Vector3.zero, kickFriction * Time.deltaTime);
        }

        // Gravity (heavier while sliding)
        float g = gravity * (isSliding ? slideGravityMultiplier : 1f);
        velocity.y += g * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void Jump()
    {
        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (isSliding)
            {
                isSliding = false;
                controller.stepOffset = originalStepOffset;
                playerCamera.SetSliding(false);
            }
        }
    }

    private void DoKick()
    {
        if (Time.time < lastKickTime + kickCooldown) return;
        if (cam == null) { Debug.LogWarning("Kick: Camera not assigned!"); return; }

        // Use a slight backward offset so we don't start inside walls
        Vector3 origin = cam.transform.position - cam.transform.forward * 0.05f;
        Vector3 direction = cam.transform.forward;

        // Capsule endpoints (tiny separation to ensure a capsule, not a sphere)
        Vector3 point1 = origin;
        Vector3 point2 = origin + direction * 0.1f;

        // 1) Check immediate overlaps (nose pressed to wall)
        Collider[] overlaps = Physics.OverlapCapsule(point1, point2, kickRadius, kickMask, QueryTriggerInteraction.Ignore);
        if (overlaps.Length > 0)
        {
            Vector3 hitPoint = overlaps[0].ClosestPoint(origin);
            HandleKick(hitPoint, overlaps[0]);
            return;
        }

        // 2) Forward capsule cast
        if (Physics.CapsuleCast(point1, point2, kickRadius, direction,
                                out RaycastHit hit, kickRange, kickMask, QueryTriggerInteraction.Ignore))
        {
            HandleKick(hit.point, hit.collider);
        }
    }

    private void HandleKick(Vector3 hitPoint, Collider hitCollider)
    {
        lastKickTime = Time.time;

        // Upward force
        velocity.y = kickUpForce;

        // Backward push (opposite camera forward)
        kickVelocity = -cam.transform.forward * kickBackForce;

        // Cancel slide if sliding
        if (isSliding)
        {
            isSliding = false;
            controller.stepOffset = originalStepOffset;
            playerCamera.SetSliding(false);
        }

        // Camera shake
        playerCamera.DoKickShake();

        Debug.Log($"Kick hit {hitCollider.name} at {hitPoint}");
    }

    private void StartSlide()
    {
        if (!isSliding)
        {
            isSliding = true;
            controller.stepOffset = 0f;
            playerCamera.SetSliding(true);

            Vector3 flatVel = new Vector3(controller.velocity.x, 0, controller.velocity.z);
            if (flatVel.magnitude < 0.1f)
                flatVel = transform.forward * moveSpeed;

            slideVelocity = flatVel + transform.forward * slideBoost;
        }
    }

    private void StopSlide()
    {
        if (isSliding && HasHeadroomToStand())
        {
            isSliding = false;
            controller.stepOffset = originalStepOffset;
            playerCamera.SetSliding(false);
        }
    }

    private void HandleCrouchTransition()
    {
        float targetHeight = isSliding || slideHeld ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(centerX, controller.height * 0.5f, centerZ);
    }

    private bool HasHeadroomToStand()
    {
        float radius = controller.radius - controller.skinWidth - 0.01f;
        Vector3 bottom = transform.position + Vector3.up * radius;
        Vector3 top = transform.position + Vector3.up * (standingHeight - radius);
        return !Physics.CheckCapsule(bottom, top, radius, crouchObstructionMask, QueryTriggerInteraction.Ignore);
    }

    // Debug visualization of the capsule cast volume (Scene view when selected)
    private void OnDrawGizmosSelected()
    {
        if (cam == null) return;

        Vector3 origin = cam.transform.position - cam.transform.forward * 0.05f;
        Vector3 dir = cam.transform.forward;

        // small segment for capsule section
        Vector3 point1 = origin;
        Vector3 point2 = origin + dir * 0.1f;

        // End positions after full cast
        Vector3 end1 = point1 + dir * kickRange;
        Vector3 end2 = point2 + dir * kickRange;

        // Local camera axes for approximating ring
        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;

        Gizmos.color = Color.yellow;

        // Spheres at both capsule ends (start and end)
        Gizmos.DrawWireSphere(point1, kickRadius);
        Gizmos.DrawWireSphere(point2, kickRadius);
        Gizmos.DrawWireSphere(end1, kickRadius);
        Gizmos.DrawWireSphere(end2, kickRadius);

        // Connect start ring to end ring (four cardinal directions) to show the tube
        Gizmos.DrawLine(point1 + up * kickRadius, end1 + up * kickRadius);
        Gizmos.DrawLine(point1 - up * kickRadius, end1 - up * kickRadius);
        Gizmos.DrawLine(point1 + right * kickRadius, end1 + right * kickRadius);
        Gizmos.DrawLine(point1 - right * kickRadius, end1 - right * kickRadius);

        Gizmos.DrawLine(point2 + up * kickRadius, end2 + up * kickRadius);
        Gizmos.DrawLine(point2 - up * kickRadius, end2 - up * kickRadius);
        Gizmos.DrawLine(point2 + right * kickRadius, end2 + right * kickRadius);
        Gizmos.DrawLine(point2 - right * kickRadius, end2 - right * kickRadius);
    }
}
