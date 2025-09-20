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

    [Header("Audio")]
    public AudioSource audioSource; // one-shots
    public AudioSource loopSource;  // looping (e.g., block creation)
    public AudioClip[] walkClips;
    public AudioClip[] jumpClips;
    public AudioClip[] landClips;
    public AudioClip[] kickClips;
    public AudioClip[] kickHitClips;
    public AudioClip[] slideClips;
    public AudioClip[] createClips;
    public float footstepInterval = 0.5f;
    public Vector2 pitchJitter = new Vector2(0.95f, 1.05f); // min/max pitch for variation

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

    // Step offset handling
    private float originalStepOffset;
    private float centerX, centerZ;

    // Jump assist timers
    private float lastGroundedTime;
    private float lastJumpPressedTime;

    // Audio timers
    private float footstepTimer;

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

        // Pass slide state to camera
        if (playerCamera != null) playerCamera.SetSliding(isSliding);
    }

    private void LateUpdate()
    {
        float currentSpeed = controller.velocity.magnitude;
        bool isMoving = moveInput.sqrMagnitude > 0.0001f;

        // Footsteps
        if (isGrounded && isMoving && !isSliding)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                PlayRandomClip(walkClips, 0.7f);
                // Faster speed -> shorter interval
                footstepTimer = footstepInterval / Mathf.Max(1f, currentSpeed);
            }
        }

        if (playerCamera != null)
        {
            playerCamera.UpdateEffects(
                speed: currentSpeed,
                isRunning: true,
                isMoving: isMoving,
                grounded: isGrounded,
                yVelocity: velocity.y
            );
        }
    }

    private void HandleMovement()
    {
        bool wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            if (velocity.y < 0f) velocity.y = -2f;

            if (!wasGrounded) // just landed
                PlayRandomClip(landClips);
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
            PlayRandomClip(jumpClips);

            if (isSliding)
            {
                isSliding = false;
                controller.stepOffset = originalStepOffset;
                if (playerCamera != null) playerCamera.SetSliding(false);
            }
        }
    }

    private void DoKick()
    {
        if (Time.time < lastKickTime + kickCooldown) return;
        if (cam == null) { Debug.LogWarning("Kick: Camera not assigned!"); return; }

        PlayRandomClip(kickClips);

        Vector3 origin = cam.transform.position - cam.transform.forward * 0.05f;
        Vector3 direction = cam.transform.forward;

        Vector3 point1 = origin;
        Vector3 point2 = origin + direction * 0.1f;

        // Overlap check
        Collider[] overlaps = Physics.OverlapCapsule(point1, point2, kickRadius, kickMask, QueryTriggerInteraction.Ignore);
        if (overlaps.Length > 0)
        {
            HandleKick(overlaps[0].ClosestPoint(origin), overlaps[0]);
            return;
        }

        // Forward cast
        if (Physics.CapsuleCast(point1, point2, kickRadius, direction,
            out RaycastHit hit, kickRange, kickMask, QueryTriggerInteraction.Ignore))
        {
            HandleKick(hit.point, hit.collider);
        }
    }

    private void HandleKick(Vector3 hitPoint, Collider hitCollider)
    {
        lastKickTime = Time.time;

        // Player movement effects
        velocity.y = kickUpForce;
        kickVelocity = -cam.transform.forward * kickBackForce;

        if (isSliding)
        {
            isSliding = false;
            controller.stepOffset = originalStepOffset;
            if (playerCamera != null) playerCamera.SetSliding(false);
        }

        // Apply force to rigidbody if present
        Rigidbody rb = hitCollider.attachedRigidbody;
        if (rb != null && !rb.isKinematic)
        {
            Vector3 forceDir = cam.transform.forward;
            float forceStrength = 10f;
            rb.AddForceAtPosition(forceDir * forceStrength, hitPoint, ForceMode.Impulse);
            PlayRandomClip(kickHitClips);
        }

        if (playerCamera != null) playerCamera.DoKickShake();
    }

    private void StartSlide()
    {
        if (!isSliding)
        {
            isSliding = true;
            controller.stepOffset = 0f;
            if (playerCamera != null) playerCamera.SetSliding(true);

            PlayRandomClip(slideClips);

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
            if (playerCamera != null) playerCamera.SetSliding(false);
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

    // ---------- Audio helpers ----------

    private void PlayRandomClip(AudioClip[] clips, float volume = 1f)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;
        int index = Random.Range(0, clips.Length);
        audioSource.pitch = Random.Range(pitchJitter.x, pitchJitter.y);
        audioSource.PlayOneShot(clips[index], volume);
    }

    // Call these from ObjectCreator when starting/ending creation hold
    public void StartCreateLoop()
    {
        if (loopSource == null || createClips == null || createClips.Length == 0) return;
        loopSource.clip = createClips[Random.Range(0, createClips.Length)];
        loopSource.pitch = Random.Range(pitchJitter.x, pitchJitter.y);
        loopSource.loop = true;
        loopSource.Play();
    }

    public void StopCreateLoop()
    {
        if (loopSource == null) return;
        loopSource.Stop();
        loopSource.clip = null;
    }
}
