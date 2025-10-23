using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BulbScrew : MonoBehaviour
{
    [Header("Refs")]
    public Transform stemTopSocket;               // Stem/StemTopSocket
    public XRGrabInteractable bulbGrab;           // this object's grab
    public XRGrabInteractable baseGrabForMerge;   // Base's grab (to merge into)

    [Header("Engagement")]
    public float engageDistance = 0.03f;          // meters
    public float alignAngle = 20f;                // degrees

    [Header("Threading")]
    public float threadDepth = 0.025f;            // meters of travel
    public float pitchPerTurn = 0.0025f;          // meters per 360°
    public bool clockwiseToTighten = true;

    [Header("Next Step")]
    public GameObject shadeSocketToEnable;        // Bulb/ShadeSnapSocket (disabled at start)

    bool engaged, complete;
    Vector3 localStart;
    Quaternion localRotStart;

    void Reset()
    {
        bulbGrab = GetComponent<XRGrabInteractable>();
    }

    void Update()
    {
        if (complete || stemTopSocket == null || bulbGrab == null) return;
        if (!bulbGrab.isSelected) return; // only drive while held

        // Check engage (distance + axis alignment)
        var toSocket = stemTopSocket.position - transform.position;
        if (!engaged)
        {
            float dist = toSocket.magnitude;
            float ang  = Vector3.Angle(transform.up, stemTopSocket.up); // assumes bulb up = thread axis
            if (dist <= engageDistance && ang <= alignAngle)
            {
                engaged = true;
                localStart = stemTopSocket.InverseTransformPoint(transform.position);
                localRotStart = Quaternion.Inverse(stemTopSocket.rotation) * transform.rotation;
            }
            else return;
        }

        // Compute desired axial travel from twist amount
        Vector3 axis = stemTopSocket.up;

        // Reference start forward projected on plane
        Vector3 startFwd = Vector3.ProjectOnPlane((stemTopSocket.rotation * localRotStart) * Vector3.forward, axis).normalized;
        Vector3 nowFwd   = Vector3.ProjectOnPlane(transform.forward, axis).normalized;

        float signed = Vector3.SignedAngle(startFwd, nowFwd, axis); // degrees since engagement
        float travel = (clockwiseToTighten ? 1f : -1f) * (signed / 360f) * pitchPerTurn;
        travel = Mathf.Clamp(travel, 0f, threadDepth);

        // Pose along the socket axis
        Vector3 local = localStart + Vector3.up * travel;
        transform.position = stemTopSocket.TransformPoint(local);
        transform.rotation = Quaternion.Slerp(transform.rotation, stemTopSocket.rotation, Mathf.InverseLerp(0, threadDepth, travel));

        if (travel >= threadDepth - 0.0005f)
            Finish();
    }

    void Finish()
    {
        complete = true;

        // Snap to final pose
        transform.position = stemTopSocket.TransformPoint(localStart + Vector3.up * threadDepth);
        transform.rotation = stemTopSocket.rotation;

        // Merge bulb into the Base so grabbing the bulb grabs the whole lamp
        if (baseGrabForMerge)
            LampAssemblyUtils.MergePartIntoBase(transform, baseGrabForMerge);

        // Enable the shade step
        if (shadeSocketToEnable) shadeSocketToEnable.SetActive(true);

        // Optional: disable further grabbing of the bulb (already done by merge)
        if (bulbGrab) bulbGrab.enabled = false;
    }
}