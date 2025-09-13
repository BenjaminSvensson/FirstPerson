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
    public float crouchTransitionSpeed = 8f;

    private CharacterController controller;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isRunning;
    private bool isCrouching;
    private bool isGrounded;

    // Lean
    private float leanInput;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

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
        inputActions.Player.LeanLeft.canceled += ctx => { if (leanInput < 0) leanInput = 0f; };

        inputActions.Player.LeanRight.performed += ctx => leanInput = 1f;
        inputActions.Player.LeanRight.canceled += ctx => { if (leanInput > 0) leanInput = 0f; };
    }

    private void Update()
    {
        HandleMovement();
        HandleCrouchTransition();

        // Send lean value to camera
        playerCamera.SetLean(leanInput);
    }

    private void HandleMovement()
    {
        // Ground check
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
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

        // Feed camera bobbing
        float currentSpeed = new Vector2(move.x, move.z).magnitude * targetSpeed;
        playerCamera.UpdateBobbing(currentSpeed, isRunning, moveInput != Vector2.zero, isGrounded, velocity.y);
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
        isCrouching = !isCrouching;
    }

    private void HandleCrouchTransition()
    {
        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
    }
}
