using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerCamera : MonoBehaviour
{
    [Header("Look Settings")]
    public float sensitivity = 2f;
    public float smoothing = 5f;
    public Transform playerBody;

    [Header("FOV Settings")]
    public Camera cam;
    public float baseFOV = 60f;
    public float slideFOV = 75f;
    public float fovLerpSpeed = 6f;

    [Header("Slide Camera")]
    public float slideCamOffset = -0.5f;
    public float slideCamSpeed = 8f;

    [Header("Leaning Settings")]
    public float leanAmount = 0.5f; // local X shift
    public float leanTilt = 15f;    // roll in degrees
    public float leanSpeed = 8f;

    [Header("Kick Shake")]
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.2f;

    // internals
    private float xRotation = 0f;
    private Vector2 currentMouseDelta;

    private Vector3 startLocalPos;

    private float targetLean;
    private float currentLean;

    private float targetYOffset;
    private float currentYOffset;

    private float targetFOV;
    private Coroutine shakeRoutine;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        startLocalPos = transform.localPosition;

        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cam != null)
        {
            targetFOV = baseFOV;
            cam.fieldOfView = baseFOV;
        }
    }

    private void Update()
    {
        HandleLook();

        // Smooth slide offset
        currentYOffset = Mathf.Lerp(currentYOffset, targetYOffset, Time.deltaTime * slideCamSpeed);

        // Smooth FOV lerp
        if (cam != null)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
        }
    }

    private void HandleLook()
    {
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        mouseDelta *= sensitivity * Time.deltaTime * 60f;
        currentMouseDelta = Vector2.Lerp(currentMouseDelta, mouseDelta, 1f / Mathf.Max(1f, smoothing));

        xRotation -= currentMouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        if (playerBody != null)
            playerBody.Rotate(Vector3.up * currentMouseDelta.x);
    }

    // Public API
    public void SetLean(float lean) => targetLean = Mathf.Clamp(lean, -1f, 1f);

    public void SetSliding(bool sliding)
    {
        targetYOffset = sliding ? slideCamOffset : 0f;
        targetFOV = (cam != null) ? (sliding ? slideFOV : baseFOV) : targetFOV;
    }

    public void UpdateEffects(float speed, bool isRunning, bool isMoving, bool grounded, float yVelocity)
    {
        // Lean smoothing
        currentLean = Mathf.Lerp(currentLean, targetLean, Time.deltaTime * leanSpeed);
        float tiltZ = -currentLean * leanTilt;
        float leanX = currentLean * leanAmount;

        // Final position relative to start
        Vector3 localPos = startLocalPos;
        localPos.x += leanX;
        localPos.y += currentYOffset;

        transform.localPosition = localPos;
        transform.localRotation = Quaternion.Euler(xRotation, 0f, tiltZ);
    }

    // Kick screen shake
    public void DoKickShake()
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(KickShake());
    }

    private IEnumerator KickShake()
    {
        Vector3 basePos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            transform.localPosition = basePos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = basePos;
    }
}
