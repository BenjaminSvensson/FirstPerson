using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{

    private Vector2 lookInput;

    [Header("UI")]
    public RawImage kickIcon; 

    [Header("References")]
    public PlayerCamera playerCamera;
    public Camera cam;
    public KickLeg kickLeg; 

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
    public AudioSource audioSource;
    public AudioSource loopSource;
    public AudioClip[] walkClips;
    public AudioClip[] jumpClips;
    public AudioClip[] landClips;
    public AudioClip[] kickClips;
    public AudioClip[] kickHitClips;
    public AudioClip[] slideClips;
    public AudioClip[] createClips;
    public float footstepInterval = 0.5f;
    public Vector2 pitchJitter = new Vector2(0.95f, 1.05f);

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

    // Loop control
    private enum LoopType { None, Slide, Create }
    private LoopType currentLoop = LoopType.None;

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

        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled  += ctx => lookInput = Vector2.zero;

    }

    private void Update()
    {
        HandleMovement();
        HandleCrouchTransition();
        UpdateKickUI();

        if (Time.time - lastGroundedTime <= coyoteTime &&
            Time.time - lastJumpPressedTime <= jumpBufferTime)
        {
            Jump();
            lastJumpPressedTime = -999f;
        }

        if (playerCamera != null) playerCamera.SetSliding(isSliding);

        if (!isSliding && currentLoop == LoopType.Slide)
        {
            StopLoop();
        }
        if (lookInput.sqrMagnitude > 0.01f)
        {
            Debug.Log($"Look input: {lookInput}");
        }
    }

    private void LateUpdate()
    {
        float currentSpeed = controller.velocity.magnitude;
        bool isMoving = moveInput.sqrMagnitude > 0.0001f;

        if (isGrounded && isMoving && !isSliding)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                PlayRandomClip(walkClips, 0.7f);
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

            if (!wasGrounded) PlayRandomClip(landClips);
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

        if (kickVelocity.magnitude > 0.1f)
        {
            controller.Move(kickVelocity * Time.deltaTime);
            kickVelocity = Vector3.Lerp(kickVelocity, Vector3.zero, kickFriction * Time.deltaTime);
        }

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
                if (currentLoop == LoopType.Slide) StopLoop();
            }
        }
    }

    private void DoKick()
    {
        // First check cooldown
        if (Time.time < lastKickTime + kickCooldown) return;
        if (cam == null) { Debug.LogWarning("Kick: Camera not assigned!"); return; }

        // Mark the kick as started immediately
        lastKickTime = Time.time;

        // Now play sound + animate leg
        PlayRandomClip(kickClips);
        if (kickLeg != null) kickLeg.DoKick();

        // Proceed with hit detection
        Vector3 origin = cam.transform.position - cam.transform.forward * 0.05f;
        Vector3 direction = cam.transform.forward;
        Vector3 point1 = origin;
        Vector3 point2 = origin + direction * 0.1f;

        Collider[] overlaps = Physics.OverlapCapsule(point1, point2, kickRadius, kickMask, QueryTriggerInteraction.Ignore);
        if (overlaps.Length > 0)
        {
            HandleKick(overlaps[0].ClosestPoint(origin), overlaps[0]);
            return;
        }

        if (Physics.CapsuleCast(point1, point2, kickRadius, direction,
            out RaycastHit hit, kickRange, kickMask, QueryTriggerInteraction.Ignore))
        {
            HandleKick(hit.point, hit.collider);
        }
    }



    private void HandleKick(Vector3 hitPoint, Collider hitCollider)
    {
        velocity.y = kickUpForce;
        kickVelocity = -cam.transform.forward * kickBackForce;

        if (isSliding)
        {
            isSliding = false;
            controller.stepOffset = originalStepOffset;
            if (playerCamera != null) playerCamera.SetSliding(false);
            if (currentLoop == LoopType.Slide) StopLoop();
        }

        PlayRandomClip(kickHitClips);

        Rigidbody rb = hitCollider.attachedRigidbody;
        if (rb != null && !rb.isKinematic)
        {
            Vector3 forceDir = cam.transform.forward;
            float forceStrength = 10f;
            rb.AddForceAtPosition(forceDir * forceStrength, hitPoint, ForceMode.Impulse);
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

            StartLoop(LoopType.Slide, slideClips);

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

            if (currentLoop == LoopType.Slide) StopLoop();
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

    private void StartLoop(LoopType type, AudioClip[] clips)
    {
        if (loopSource == null || clips == null || clips.Length == 0) return;

        if (loopSource.isPlaying) loopSource.Stop();

        loopSource.clip = clips[Random.Range(0, clips.Length)];
        loopSource.pitch = Random.Range(pitchJitter.x, pitchJitter.y);
        loopSource.loop = true;
        loopSource.Play();
        currentLoop = type;
    }

    private void StopLoop()
    {
        if (loopSource != null && loopSource.isPlaying) loopSource.Stop();
        if (loopSource != null) loopSource.clip = null;
        currentLoop = LoopType.None;
    }

    // Public hooks for ObjectCreator to control creation loop
    public void StartCreateLoop()
    {
        StartLoop(LoopType.Create, createClips);
    }

    public void StopCreateLoop()
    {
        if (currentLoop == LoopType.Create) StopLoop();
    }
    
    private void UpdateKickUI()
    {
        if (kickIcon == null) return;

        bool ready = Time.time >= lastKickTime + kickCooldown;
        Color c = kickIcon.color;
        c.a = ready ? 1f : 0.3f;
        kickIcon.color = c;
    }

}
