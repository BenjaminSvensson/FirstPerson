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

    private float xRotation = 0f;
    private Vector2 currentMouseDelta;

    private float bobTimer;
    private Vector3 startLocalPos;

    private float landingOffset;
    private float lastYVelocity;

    private float jumpTilt; // smoothed tilt value

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        startLocalPos = transform.localPosition;
    }

    private void Update()
    {
        HandleLook();
    }

    private void HandleLook()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * sensitivity * Time.deltaTime * 60f;
        currentMouseDelta = Vector2.Lerp(currentMouseDelta, mouseDelta, 1f / smoothing);

        xRotation -= currentMouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        transform.localRotation = Quaternion.Euler(xRotation + jumpTilt, 0f, 0f);
        playerBody.Rotate(Vector3.up * currentMouseDelta.x);
    }

    public void UpdateBobbing(float speed, bool isRunning, bool isMoving, bool grounded, float yVelocity)
    {
        Vector3 localPos = startLocalPos;

        // --- Bobbing ---
        if (isMoving && grounded)
        {
            float bobSpeed = isRunning ? runBobbingSpeed : walkBobbingSpeed;
            float bobAmount = isRunning ? runBobbingAmount : walkBobbingAmount;

            bobTimer += Time.deltaTime * bobSpeed;
            localPos.y += Mathf.Sin(bobTimer) * bobAmount;
            localPos.x += Mathf.Cos(bobTimer / 2) * bobAmount * 0.5f;
        }
        else
        {
            bobTimer = 0;
        }

        // --- Jump tilt ---
        if (!grounded && lastYVelocity > 0.1f) // just jumped
        {
            jumpTilt = Mathf.Lerp(jumpTilt, jumpTiltAmount, Time.deltaTime * jumpTiltSpeed);
        }
        else
        {
            jumpTilt = Mathf.Lerp(jumpTilt, 0, Time.deltaTime * jumpTiltSpeed);
        }

        // --- Landing impact ---
        if (grounded && lastYVelocity < -3f)
        {
            landingOffset = -landingShakeAmount; // strong dip down
        }
        landingOffset = Mathf.Lerp(landingOffset, 0, Time.deltaTime * landingShakeSpeed);
        localPos.y += landingOffset;

        // Apply
        transform.localPosition = localPos;

        // Save velocity for next frame
        lastYVelocity = yVelocity;
    }
}
