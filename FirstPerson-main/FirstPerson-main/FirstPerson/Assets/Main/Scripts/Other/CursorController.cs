using UnityEngine;
using UnityEngine.UI;

public class CursorController : MonoBehaviour
{
    public Image cursorImage;
    public Color readyColor = Color.white;
    public Color cooldownColor = Color.red;

    [Range(0f,1f)] public float inactiveAlpha = 0.3f; // when kick not viable

    // Called by ObjectCreator
    public void SetCreateReady(bool ready)
    {
        if (cursorImage == null) return;
        cursorImage.color = ready ? readyColor : cooldownColor;
    }

    // Called by Player
    public void SetKickViable(bool viable)
    {
        if (cursorImage == null) return;
        Color c = cursorImage.color;
        c.a = viable ? 1f : inactiveAlpha;
        cursorImage.color = c;
    }
}
