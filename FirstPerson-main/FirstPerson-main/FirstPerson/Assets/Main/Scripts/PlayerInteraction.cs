using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    public Camera playerCamera;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E; // Or use Input System actions
    public AudioSource audioSource;
    public AudioClip lockedSound;

    private void Update()
    {
        if (Input.GetKeyDown(interactKey))
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                if (hit.collider.CompareTag("Door"))
                {
                    DoorScript door = hit.collider.GetComponent<DoorScript>();
                    if (door != null)
                    {
                        if (door.locked)
                        {
                            // Play locked feedback
                            if (audioSource && lockedSound)
                                audioSource.PlayOneShot(lockedSound);

                            // Trigger shake
                            StartCoroutine(door.ShakeDoor());
                        }
                        else
                        {
                            // Toggle open/close
                            door.ToggleDoor();
                        }
                    }
                }
            }
        }
    }
}
