using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.Events;

public class ScrewDownMotion : MonoBehaviour
{

    [Header("Audio")]
    [Tooltip("Sound to play when bulb is fully screwed in.")]
    public AudioSource success;

    [Header("References")]
    public Transform screwTarget;
    public XRBaseInteractor handInteractor;

    [Header("Screw Parameters")]
    [Tooltip("Total screw turns to fully tighten.")]
    public float totalTurns = 3f;

    [Tooltip("Distance the bulb moves downward over totalTurns.")]
    public float totalDistance = 1.6f;

    [HideInInspector] public bool IsFullyScrewed { get; private set; }
    public UnityEvent onFullyScrewed = new UnityEvent();

    [Tooltip("Maximum hand distance before pausing screwing.")]
    public float maxHandDistance = 0.5f;

    [Tooltip("How far the hand must twist to resume after pausing.")]
    public float resumeAngleThreshold = 5f;

    [Tooltip("How much rotation (deg) is required to register one full screw turn virtually.")]
    public float virtualTurnSensitivity = 90f;

    [Header("Next assembly step")]
    [Tooltip("Socket to activate when bulb is fully screwed in (e.g., the lamp shade socket).")]

    public XRSocketInteractor nextSocketToActivate;

    private XRBaseInteractable interactable;
    private Quaternion lastHandRotation;
    private Vector3 lastForwardDir;
    private float accumulatedAngle = 0f;
    private Vector3 startPos;
    private bool isScrewing = false;
    private bool hasStarted = false;
    private bool waitingForResume = false;

    private void Start()
    {
        interactable = GetComponent<XRBaseInteractable>();
    }

    public void BeginScrewing()
    {
        IsFullyScrewed = false;
        startPos = transform.position;
        accumulatedAngle = 0f;
        isScrewing = true;
        hasStarted = true;
        waitingForResume = false;

        if (handInteractor != null)
        {
            lastHandRotation = handInteractor.attachTransform.rotation;
            lastForwardDir = handInteractor.attachTransform.forward;
        }

        Debug.Log("🔩 BeginScrewing() called on " + name);
    }

    private void Update()
    {
        if (handInteractor == null)
            return;

        float handDistance = Vector3.Distance(
            handInteractor.attachTransform.position,
            transform.position);

        // ⛔ Pause when too far
        if (isScrewing && handDistance > maxHandDistance)
        {
            Debug.Log("🛑 Hand moved too far away, pausing screwing.");
            isScrewing = false;
            waitingForResume = true;
            transform.position = Vector3.Lerp(startPos, screwTarget.position,
                accumulatedAngle / (totalTurns * 360f));
            return;
        }

        // 💤 Waiting until twist again
        if (waitingForResume)
        {
            if (handDistance > maxHandDistance * 0.9f)
                return;

            float angleDiff = Quaternion.Angle(lastHandRotation, handInteractor.attachTransform.rotation);
            if (angleDiff > resumeAngleThreshold)
            {
                Debug.Log("🔁 Twist detected — resuming screwing.");
                waitingForResume = false;
                isScrewing = true;
                lastHandRotation = handInteractor.attachTransform.rotation;
                lastForwardDir = handInteractor.attachTransform.forward;
            }
            return;
        }

        if (!isScrewing)
            return;


        // 🔄 Compute RELATIVE rotation delta, not absolute world spin
        Quaternion currentRot = handInteractor.attachTransform.rotation;
        Quaternion delta = currentRot * Quaternion.Inverse(lastHandRotation);

        // Project rotation around local up axis (simulate twisting wrist)
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (float.IsNaN(angle) || angle < 0.1f)
            return;

        float direction = Mathf.Sign(Vector3.Dot(axis, transform.up));
        float deltaDegrees = angle * direction;

        // Apply virtual scaling so user doesn’t need full rotations
        float scaledDelta = deltaDegrees * (360f / virtualTurnSensitivity);
        accumulatedAngle = Mathf.Clamp(accumulatedAngle + scaledDelta, 0f, totalTurns * 360f);

        // Apply movement/visual rotation
        float t = accumulatedAngle / (totalTurns * 360f);
        transform.position = Vector3.Lerp(startPos, screwTarget.position, t);
        transform.Rotate(Vector3.up, deltaDegrees, Space.Self); // small visible spin

        lastHandRotation = currentRot;

        if (t >= 1f)
        {
            isScrewing = false;
            Debug.Log("✅ Bulb fully screwed in!");
            if (nextSocketToActivate != null)
            {
                nextSocketToActivate.socketActive = true;
                Debug.Log("➡️ Activated next socket: " + nextSocketToActivate.name);
            }
        }
        
        if (t >= 1f && !IsFullyScrewed)
        {
            IsFullyScrewed = true;
            isScrewing = false;
            Debug.Log("✅ Bulb fully screwed in!");
            
            if (success) success.Play();

            onFullyScrewed.Invoke();          // <-- NEW

            if (nextSocketToActivate != null)
            {
                nextSocketToActivate.socketActive = true;
                Debug.Log("➡️ Activated next socket: " + nextSocketToActivate.name);
            }

            // Optional: disable this behaviour after completion
            // enabled = false;
            
        }
    }
}