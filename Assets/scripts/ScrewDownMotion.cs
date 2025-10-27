using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ScrewDownMotion : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Target position lower on the stem when fully screwed in.")]
    public Transform screwTarget;

    [Tooltip("Interactor that represents the player's hand (assign in Inspector).")]
    public XRBaseInteractor handInteractor;

    [Header("Screw Parameters")]
    public float totalTurns = 3f;
    public float totalDistance = 1.6f;

    private XRBaseInteractable interactable;
    private Quaternion lastHandRotation;
    private float accumulatedAngle = 0f;
    private Vector3 startPos;
    private bool isScrewing = false;

    private void Start()
    {
        interactable = GetComponent<XRBaseInteractable>();
    }

    /// <summary>
    /// Called by the socket when the bulb snaps into place.
    /// </summary>
    public void BeginScrewing()
    {
        startPos = transform.position;
        accumulatedAngle = 0f;
        isScrewing = true;
        Debug.Log("🔩 BeginScrewing() called on " + name);

        if (handInteractor != null)
            lastHandRotation = handInteractor.attachTransform.rotation;
        else
            Debug.LogWarning("⚠️ No hand interactor assigned on ScrewDownMotion.");
    }

    private void Update()
    {
        if (!isScrewing || handInteractor == null)
            return;

        float handDistance = Vector3.Distance(
        handInteractor.attachTransform.position,
        transform.position);

        if (handDistance > 0.3f)
        {
            Debug.Log("🛑 Hand moved too far away, stopping screwing.");
            isScrewing = false;
            return;
        }
        // Get rotation difference since last frame
        Quaternion currentRot = handInteractor.attachTransform.rotation;
        float angleChange = Quaternion.Angle(lastHandRotation, currentRot);

        if (angleChange > 0.1f)
            Debug.Log("Hand rotated " + angleChange.ToString("F1") + " degrees");

        Quaternion delta = currentRot * Quaternion.Inverse(lastHandRotation);

        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (float.IsNaN(angle) || angle < 0.1f) return; // ignore small noise

        // Determine screw direction relative to bulb's up axis
        float direction = Mathf.Sign(Vector3.Dot(axis, transform.up));
        float deltaDegrees = angle * direction;

        // Accumulate total rotation
        accumulatedAngle = Mathf.Clamp(accumulatedAngle + deltaDegrees, 0f, totalTurns * 360f);

        // Compute downward progress (0–1)
        float t = accumulatedAngle / (totalTurns * 360f);
        transform.position = Vector3.Lerp(startPos, screwTarget.position, t);

        // Visually rotate the bulb as it screws down
        transform.Rotate(Vector3.up, deltaDegrees, Space.Self);

        // Store rotation for next frame
        lastHandRotation = currentRot;

        // ✅ Optional: stop when fully screwed
        if (t >= 1f)
        {
            isScrewing = false;
            Debug.Log("✅ Bulb fully screwed in!");
        }
    }
}


