using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Attach to the DimmerKnob object (which has an XRGrabInteractable).
/// Rotates only around a chosen local axis and maps angle -> light intensity.
[RequireComponent(typeof(XRGrabInteractable))]
public class KnobDimmer : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Hookups")]
    public Transform pivot;           // the center of rotation (child of the knob)
    public Light targetLight;         // the lamp's Light to control

    [Header("Rotation Limits (degrees)")]
    public Axis rotateAround = Axis.Y;
    public float minAngle = -120f;    // leftmost stop
    public float maxAngle = 120f;     // rightmost stop
    public float startAngle = 0f;     // where the knob sits at start (between min/max)

    [Header("Light Mapping")]
    public float minIntensity = 0.0f;
    public float maxIntensity = 3.0f;

    [Tooltip("Extra softness so small hand jitter doesn't spam updates.")]
    public float deadZoneDegrees = 0.5f;

    [Header("Events (optional)")]
    public UnityEvent<float> onValueChanged; // 0..1 normalized

    XRGrabInteractable grab;
    Transform interactor;
    Quaternion pivotLocalStartRot;
    float currentAngle;      // clamped
    float angleAtGrab;       // angle when we grabbed
    float startAngleAtGrab;  // interactor-based reference
    Vector3 worldAxis;       // rotation axis in world space

    void Reset()
    {
        pivot = transform; // default to self if user forgets
    }

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (!pivot) pivot = transform;

        // freeze position by not tracking it
        grab.trackPosition = false;
        grab.trackRotation = true;

        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);

        pivotLocalStartRot = pivot.localRotation;
        SetAngle(startAngle);
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        interactor = args.interactorObject.transform;

        // compute world axis from chosen local axis
        switch (rotateAround)
        {
            case Axis.X: worldAxis = pivot.TransformDirection(Vector3.right);  break;
            case Axis.Y: worldAxis = pivot.TransformDirection(Vector3.up);     break;
            default:     worldAxis = pivot.TransformDirection(Vector3.forward);break;
        }

        // reference plane perpendicular to our axis
        startAngleAtGrab = GetInteractorAngleOnPlane(interactor.position);
        angleAtGrab = currentAngle; // remember where the knob was
    }

    void OnRelease(SelectExitEventArgs _)
    {
        interactor = null;
    }

    void Update()
    {
        if (!interactor) return;

        // where is the hand on our rotation plane now?
        float handAngle = GetInteractorAngleOnPlane(interactor.position);
        float delta = Mathf.DeltaAngle(startAngleAtGrab, handAngle);

        if (Mathf.Abs(delta) < deadZoneDegrees) return;

        SetAngle(Mathf.Clamp(angleAtGrab + delta, minAngle, maxAngle));
    }

    float GetInteractorAngleOnPlane(Vector3 worldPos)
    {
        // plane through pivot, normal = worldAxis
        Vector3 center = pivot.position;
        // two basis vectors on the plane (orthonormal)
        Vector3 basisA = Vector3.Cross(worldAxis, Vector3.up);
        if (basisA.sqrMagnitude < 1e-4f) basisA = Vector3.Cross(worldAxis, Vector3.right);
        basisA.Normalize();
        Vector3 basisB = Vector3.Cross(worldAxis, basisA);

        Vector3 v = worldPos - center;
        float x = Vector3.Dot(v, basisA);
        float y = Vector3.Dot(v, basisB);

        // angle in degrees, -180..180
        return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
    }

    void SetAngle(float angle)
    {
        currentAngle = angle;

        // apply local rotation only around chosen axis, preserving other axes
        Vector3 e = pivotLocalStartRot.eulerAngles;
        switch (rotateAround)
        {
            case Axis.X: pivot.localRotation = Quaternion.Euler(angle, e.y, e.z); break;
            case Axis.Y: pivot.localRotation = Quaternion.Euler(e.x, angle, e.z); break;
            default:     pivot.localRotation = Quaternion.Euler(e.x, e.y, angle); break;
        }

        // map to intensity
        float t = Mathf.InverseLerp(minAngle, maxAngle, currentAngle); // 0..1
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        if (targetLight) targetLight.intensity = intensity;

        onValueChanged?.Invoke(t);
    }
}