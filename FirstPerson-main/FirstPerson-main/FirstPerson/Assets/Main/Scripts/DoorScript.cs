using UnityEngine;
using System.Collections;

public class DoorScript : MonoBehaviour
{
    public bool locked = false;
    public float openAngle = 90f;
    public float speed = 2f;

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    private void Start()
    {
        closedRotation = transform.rotation;
        openRotation = Quaternion.Euler(transform.eulerAngles + Vector3.up * openAngle);
    }

    public void ToggleDoor()
    {
        StopAllCoroutines();
        if (isOpen)
            StartCoroutine(RotateDoor(openRotation, closedRotation));
        else
            StartCoroutine(RotateDoor(closedRotation, openRotation));

        isOpen = !isOpen;
    }

    private IEnumerator RotateDoor(Quaternion from, Quaternion to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            transform.rotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }
    }

    public IEnumerator ShakeDoor()
    {
        Vector3 originalPos = transform.position;
        float elapsed = 0f;
        float duration = 0.3f;
        float magnitude = 0.05f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.position = originalPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPos;
    }
}
