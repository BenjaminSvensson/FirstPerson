using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [Header("Look Settings")]
    public float sensitivity = 2f;
    public float smoothing = 5f;
    public Transform playerBody;

    [Header("Bobbing Settings")]
    public float walkBobbingSpeed = 8f;
    public float walkBobbingAmount = 0.05f;
    public float runBobbingSpeed = 12f;
    public float runBobbingAmount = 0.1f;

    [Header("Jump & Landing")]
    public float jumpTiltAmount = 5f;       // degrees upward tilt on jump
    public float jumpTiltSpeed = 4f;        // how fast tilt applies
    public float landingShakeAmount = 0.2f; // impact strength
    public float landingShakeSpeed = 8f;    // how fast it stabilizes

    [Header("Leaning Settings")]
    public float leanAmount = 0.5f; // sideways offset (camera local X)
    public float leanTilt = 15f;    // degrees tilt
    public float leanSpeed = 8f;    // smoothing

    [Header("Crouch Camera")]
    [Tooltip("Camera local Y when standing. Leave 0 to auto-use current.")]
    public float cameraStandingLocalY = 0f;
    [Tooltip("Camera local Y when crouched. Leave 0 to auto-derive from controller heights.")]
    public float cameraCrouchingLocalY = 0f;
    [Tooltip("Lerp speed for camera crouch movement.")]
    public float crouchCamSpeed = 10f;

    private float xRotation = 0f;
    private Vector2 currentMouseDelta;

    private float bobTimer;
    private Vector3 startLocalPos;

    private float landingOffset;
    private float lastYVelocity;

    private float jumpTilt;

    // Lean
    private float targetLean;
    private float currentLean;

    // Crouch camera blend handled here (decoupled from collider)
    private float crouchBlendTarget;   // 0 = standing, 1 = crouched
    private float crouchBlendCurrent;  // smoothed

    // Cached for auto-derive
    private float inferredCrouchOffset;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        startLocalPos = transform.localPosition;

        // Auto-fill standing Y if not set
        if (Mathf.Approximately(cameraStandingLocalY, 0f))
            cameraStandingLocalY = startLocalPos.y;
    }

    private void Update()
    {
        HandleLook();

        // Smooth crouch blend
        crouchBlendCurrent = Mathf.Lerp(crouchBlendCurrent, crouchBlendTarget, Time.deltaTime * crouchCamSpeed);
    }

    private void LateUpdate()
    {
        // In case you want to run visual updates here; we keep UpdateEffects called by Player's LateUpdate.
        // Intentionally left empty. You can move UpdateEffects call here if you prefer centralization.
    }

    private void HandleLook()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * sensitivity * Time.deltaTime * 60f;
        currentMouseDelta = Vector2.Lerp(currentMouseDelta, mouseDelta, 1f / Mathf.Max(1f, smoothing));

        // Pitch (up/down)
        xRotation -= currentMouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        // Yaw (left/right) — rotates the player body
        playerBody.Rotate(Vector3.up * currentMouseDelta.x);
    }

    // Public API called from Player
    public void SetLean(float lean)
    {
        targetLean = lean; // -1 left, 1 right
    }

    public void SetCrouchSpeed(float speed)
    {
        crouchCamSpeed = Mathf.Max(0.01f, speed);
    }

    public void SetCrouchState(bool crouched)
    {
        crouchBlendTarget = crouched ? 1f : 0f;
    }

    // Call once at start so camera knows both heights.
    // If cameraCrouchingLocalY is zero, we infer it from controller heights difference.
    public void SetCrouchHeightsFromCamera()
    {
        if (Mathf.Approximately(cameraStandingLocalY, 0f))
            cameraStandingLocalY = transform.localPosition.y;

        // If not set explicitly, infer crouch by subtracting typical height delta
        if (Mathf.Approximately(cameraCrouchingLocalY, 0f))
        {
            // Try to find the Player and CharacterController to infer delta
            var player = GetComponentInParent<Player>();
            if (player != null)
            {
                float delta = player.standingHeight - player.crouchHeight;
                inferredCrouchOffset = -delta;
                cameraCrouchingLocalY = cameraStandingLocalY + inferredCrouchOffset;
            }
            else
            {
                // Fallback: 0.5m drop if no player found
                cameraCrouchingLocalY = cameraStandingLocalY - 0.5f;
            }
        }
    }

    // Called by Player in LateUpdate (after movement) for smooth visuals
    public void UpdateEffects(float speed, bool isRunning, bool isMoving, bool grounded, float yVelocity)
    {
        Vector3 localPos = Vector3.zero;

        // --- Base: crouch Y in camera local space (exact targets, not relative to current collider) ---
        float baseY = Mathf.Lerp(cameraStandingLocalY, cameraCrouchingLocalY, crouchBlendCurrent);
        localPos.y = baseY;

        // --- Bobbing (only when grounded and moving) ---
        if (isMoving && grounded)
        {
            float bobSpeed = isRunning ? runBobbingSpeed : walkBobbingSpeed;
            float bobAmount = isRunning ? runBobbingAmount : walkBobbingAmount;

            bobTimer += Time.deltaTime * bobSpeed;
            localPos.y += Mathf.Sin(bobTimer) * bobAmount;
            localPos.x += Mathf.Cos(bobTimer / 2f) * bobAmount * 0.5f;
        }
        else
        {
            bobTimer = 0f;
        }

        // --- Jump tilt ---
        if (!grounded && lastYVelocity > 0.1f) // just jumped
            jumpTilt = Mathf.Lerp(jumpTilt, jumpTiltAmount, Time.deltaTime * jumpTiltSpeed);
        else
            jumpTilt = Mathf.Lerp(jumpTilt, 0f, Time.deltaTime * jumpTiltSpeed);

        // --- Landing impact ---
        if (grounded && lastYVelocity < -3f)
            landingOffset = -landingShakeAmount; // strong dip down

        landingOffset = Mathf.Lerp(landingOffset, 0f, Time.deltaTime * landingShakeSpeed);
        localPos.y += landingOffset;

        // --- Lean ---
        currentLean = Mathf.Lerp(currentLean, targetLean, Time.deltaTime * leanSpeed);

        // Lean applies purely sideways in camera local space (no forward/back drift)
        localPos += Vector3.right * (currentLean * leanAmount);

        float tiltZ = -currentLean * leanTilt;

        // Apply final position & rotation relative to the original X/Z and the computed Y
        // Keep original startLocalPos.x/z as baseline so only our computed offsets change them
        transform.localPosition = new Vector3(
            startLocalPos.x + localPos.x,
            localPos.y,
            startLocalPos.z + localPos.z
        );

        transform.localRotation = Quaternion.Euler(xRotation + jumpTilt, 0f, tiltZ);

        // Save velocity for next frame
        lastYVelocity = yVelocity;
    }
}
