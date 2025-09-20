using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerCamera : MonoBehaviour
{
    [Header("Look Settings")]
    public Transform playerBody;
    public float sensitivity = 2f;
    public float smoothing = 5f;

    [Header("FOV Settings")]
    public Camera cam;
    public float baseFOV = 60f;
    public float slideFOV = 75f;
    public float fovLerpSpeed = 6f;

    [Header("Slide Camera")]
    public float slideCamOffset = -0.5f;
    public float slideCamSpeed = 8f;

    [Header("Bobbing")]
    public float bobFrequency = 8f;
    public float bobAmplitude = 0.05f;
    public float bobSpeedScale = 0.12f;
    public float bobSmoothingOut = 8f;

    [Header("Jump/Land Spring")]
    public float springStiffness = 120f;
    public float springDamping = 18f;
    public float jumpImpulse = -0.05f;
    public float landImpulse = 0.12f;
    public float landVelocityThreshold = -3f;

    [Header("Jump/Land Tilt")]
    [Tooltip("How stiff the tilt spring is (higher = snappier).")]
    public float tiltStiffness = 80f;
    [Tooltip("How much damping the tilt spring has (higher = less oscillation).")]
    public float tiltDamping = 12f;
    [Tooltip("Impulse tilt (in degrees) applied when jumping. Negative tilts the camera up.")]
    public float jumpTiltImpulse = -5f;
    [Tooltip("Impulse tilt (in degrees) applied when landing. Positive tilts the camera down.")]
    public float landTiltImpulse = 7f;
    [Tooltip("Minimum downward velocity required to trigger a land tilt.")]
    public float landVelThreshold = -3f;
    [Tooltip("Clamp for maximum tilt angle in either direction.")]
    public float maxTiltAngle = 15f;

    [Header("Kick Shake")]
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.2f;

    // internals
    private float xRotation;
    private Vector2 currentMouseDelta;
    private Vector3 baseLocalPos;

    private float targetYOffset;
    private float currentYOffset;
    private float targetFOV;
    private Coroutine shakeRoutine;

    // bob
    private float bobTimer;
    private float bobValue;

    // spring (jump/land)
    private float springOffset;
    private float springVelocity;

    // tilt
    private float tiltOffset;
    private float tiltVelocity;

    // state tracking
    private bool prevGrounded;
    private float prevYVel;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cam != null)
        {
            targetFOV = baseFOV;
            cam.fieldOfView = baseFOV;
        }

        baseLocalPos = transform.localPosition;
    }

    private void Update()
    {
        HandleLook();

        // Smooth slide offset and FOV
        currentYOffset = Mathf.Lerp(currentYOffset, targetYOffset, Time.deltaTime * slideCamSpeed);
        if (cam != null)
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
    }

    private void LateUpdate()
    {
        ApplyFinalPose();
    }

    private void HandleLook()
    {
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        mouseDelta *= sensitivity * 60f * Time.deltaTime;
        currentMouseDelta = Vector2.Lerp(currentMouseDelta, mouseDelta, 1f / Mathf.Max(1f, smoothing));

        xRotation -= currentMouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        if (playerBody != null)
            playerBody.Rotate(Vector3.up * currentMouseDelta.x);
    }

    // Called by Player.cs every LateUpdate
    public void UpdateEffects(float speed, bool isRunning, bool isMoving, bool grounded, float yVelocity)
    {
        // 1) Bobbing
        if (isMoving && grounded)
        {
            float freq = bobFrequency * (1f + speed * bobSpeedScale);
            bobTimer += Time.deltaTime * Mathf.Max(0.001f, freq);
            bobValue = Mathf.Sin(bobTimer) * bobAmplitude;
        }
        else
        {
            bobTimer = 0f;
            bobValue = Mathf.Lerp(bobValue, 0f, Time.deltaTime * bobSmoothingOut);
        }

        // 2) Jump/Land spring
        if (!prevGrounded && grounded)
        {
            if (prevYVel < landVelocityThreshold)
                springVelocity += landImpulse * Mathf.Clamp01(-prevYVel / 10f);
        }
        else if (prevGrounded && !grounded)
        {
            if (yVelocity > 0.1f)
                springVelocity += jumpImpulse;
        }

        float accel = -springStiffness * springOffset - springDamping * springVelocity;
        springVelocity += accel * Time.deltaTime;
        springOffset += springVelocity * Time.deltaTime;
        springOffset = Mathf.Clamp(springOffset, -0.3f, 0.3f);

        // 3) Jump/Land tilt
        if (!prevGrounded && grounded)
        {
            if (prevYVel < landVelThreshold)
                tiltVelocity += landTiltImpulse;
        }
        else if (prevGrounded && !grounded)
        {
            if (yVelocity > 0.1f)
                tiltVelocity += jumpTiltImpulse;
        }

        float tiltAccel = -tiltStiffness * tiltOffset - tiltDamping * tiltVelocity;
        tiltVelocity += tiltAccel * Time.deltaTime;
        tiltOffset += tiltVelocity * Time.deltaTime;
        tiltOffset = Mathf.Clamp(tiltOffset, -maxTiltAngle, maxTiltAngle);

        prevGrounded = grounded;
        prevYVel = yVelocity;
    }

    public void SetSliding(bool sliding)
    {
        targetYOffset = sliding ? slideCamOffset : 0f;
        if (cam != null)
            targetFOV = sliding ? slideFOV : baseFOV;
    }

    public void DoKickShake()
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(KickShake());
    }

    private IEnumerator KickShake()
    {
        Vector3 basePos = baseLocalPos;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            transform.localPosition = ComposeLocalPosition(new Vector3(x, y, 0f));
            transform.localRotation = Quaternion.Euler(xRotation + tiltOffset, 0f, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyFinalPose();
    }

    private void ApplyFinalPose()
    {
        transform.localPosition = ComposeLocalPosition(Vector3.zero);
        transform.localRotation = Quaternion.Euler(xRotation + tiltOffset, 0f, 0f);
    }

    private Vector3 ComposeLocalPosition(Vector3 extra)
    {
        Vector3 pos = baseLocalPos;
        pos.y += currentYOffset;   // slide dip
        pos.y += bobValue;         // walk bob
        pos.y += springOffset;     // jump/land spring
        pos += extra;              // shake
        return pos;
    }
}
