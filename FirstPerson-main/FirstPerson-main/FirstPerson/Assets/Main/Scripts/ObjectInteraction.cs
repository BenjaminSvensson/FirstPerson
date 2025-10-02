using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectInteraction : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;              // Assign your main camera
    public Transform interactionTarget;      // Assign the empty GameObject in Inspector

    [Header("Settings")]
    public float moveSpeed = 5f;             // Smooth transition speed

    private bool isInteracting = false;

    void Update()
    {
        if (isInteracting && interactionTarget != null)
        {
            // Smoothly move camera to target position
            playerCamera.transform.position = Vector3.Lerp(
                playerCamera.transform.position,
                interactionTarget.position,
                Time.deltaTime * moveSpeed
            );

            // Smoothly rotate camera to target rotation
            playerCamera.transform.rotation = Quaternion.Slerp(
                playerCamera.transform.rotation,
                interactionTarget.rotation,
                Time.deltaTime * moveSpeed
            );
        }
    }

    // This will be called by PlayerInput when Interact is pressed
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            isInteracting = !isInteracting; // Toggle interaction on/off
        }
    }
}
