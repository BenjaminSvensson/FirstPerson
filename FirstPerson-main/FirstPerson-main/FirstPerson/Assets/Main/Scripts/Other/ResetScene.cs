using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ResetHandler : MonoBehaviour
{
    private PlayerInputActions input;

    private void Awake()
    {
        input = new PlayerInputActions();
        input.Player.Enable();

        // Subscribe to the Reset action
        input.Player.Reset.performed += ctx => ReloadScene();
    }

    private void OnDestroy()
    {
        // Always unsubscribe to avoid leaks
        input.Player.Reset.performed -= ctx => ReloadScene();
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
