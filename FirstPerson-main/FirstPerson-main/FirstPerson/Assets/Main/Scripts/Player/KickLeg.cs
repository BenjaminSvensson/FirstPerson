using UnityEngine;

public class KickLeg : MonoBehaviour
{
    [Header("Kick Settings")]
    public float kickDuration = 0.4f;       // total time of the kick
    public float forwardDistance = 0.5f;    // how far the leg moves forward
    public float forwardAngle = 45f;        // how far the leg rotates forward
    public float recoilDistance = -0.2f;    // slight pullback after kick
    public float recoilAngle = -10f;        // slight backward tilt
    public AnimationCurve forwardCurve;     // curve for forward motion
    public AnimationCurve recoilCurve;      // curve for recoil motion

    private Vector3 restPos;
    private Quaternion restRot;
    private Vector3 forwardPos;
    private Quaternion forwardRot;
    private Vector3 recoilPos;
    private Quaternion recoilRot;

    private float timer;
    private bool isKicking;

    private void Awake()
    {
        // Hide leg at start
        gameObject.SetActive(false);

        // Save rest transform
        restPos = transform.localPosition;
        restRot = transform.localRotation;

        // Precompute forward and recoil transforms
        forwardPos = restPos + new Vector3(0f, 0f, forwardDistance);
        forwardRot = restRot * Quaternion.Euler(-forwardAngle, 0f, 0f);

        recoilPos = restPos + new Vector3(0f, 0f, recoilDistance);
        recoilRot = restRot * Quaternion.Euler(recoilAngle, 0f, 0f);
    }

    private void Update()
    {
        if (isKicking)
        {
            timer += Time.deltaTime;
            float t = timer / kickDuration;

            if (t >= 1f)
            {
                // Kick finished
                isKicking = false;
                gameObject.SetActive(false);
                transform.localPosition = restPos;
                transform.localRotation = restRot;
            }
            else
            {
                if (t < 0.5f)
                {
                    // Forward phase
                    float fT = t / 0.5f;
                    float curveT = forwardCurve != null ? forwardCurve.Evaluate(fT) : fT;
                    transform.localPosition = Vector3.Lerp(restPos, forwardPos, curveT);
                    transform.localRotation = Quaternion.Slerp(restRot, forwardRot, curveT);
                }
                else
                {
                    // Recoil phase
                    float rT = (t - 0.5f) / 0.5f;
                    float curveT = recoilCurve != null ? recoilCurve.Evaluate(rT) : rT;
                    transform.localPosition = Vector3.Lerp(forwardPos, recoilPos, curveT);
                    transform.localRotation = Quaternion.Slerp(forwardRot, recoilRot, curveT);
                }
            }
        }
    }

    // Call this from Player.cs when a kick is performed
    public void DoKick()
    {
        if (isKicking) return;

        gameObject.SetActive(true);
        isKicking = true;
        timer = 0f;
    }
}
