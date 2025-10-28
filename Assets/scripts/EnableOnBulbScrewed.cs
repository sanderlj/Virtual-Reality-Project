using UnityEngine;
using UnityEngine.Events;

using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class EnableOnBulbScrewed : MonoBehaviour
{
    [Header("Bulb Screw")]
    [SerializeField] private ScrewDownMotion screw;    // drag the bulb's ScrewDownMotion here

    [Header("Next Sockets")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor[] socketsToActivate; // e.g., the Shade socket

    [Header("Options")]
    [SerializeField] private bool deactivateBulbSocketWhenDone = true;
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor bulbSocket; // the socket that holds the bulb (optional)

    [Header("Events")]
    public UnityEvent onActivated;

    bool fired;

    void Reset()
    {
        screw = GetComponentInChildren<ScrewDownMotion>();
    }

    void OnEnable()
    {
        if (screw) screw.onFullyScrewed.AddListener(HandleScrewed);
    }

    void OnDisable()
    {
        if (screw) screw.onFullyScrewed.RemoveListener(HandleScrewed);
    }

    void HandleScrewed()
    {
        if (fired) return;
        fired = true;

        // Turn on the next stage(s)
        foreach (var s in socketsToActivate)
            if (s) s.socketActive = true;

        if (deactivateBulbSocketWhenDone && bulbSocket)
            bulbSocket.socketActive = false;

        onActivated?.Invoke();
    }
}