using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class LampAssemblyUtils
{
    // When a part is "final", make the Base the only grab/rigidbody and include the part's colliders.
    public static void MergePartIntoBase(Transform partRoot, XRGrabInteractable baseGrab)
    {
        if (!partRoot || !baseGrab) return;

        // 1) Disable the part’s grab
        var partGrab = partRoot.GetComponent<XRGrabInteractable>();
        if (partGrab) partGrab.enabled = false;

        // 2) Add all colliders on this part to the Base grab's collider list
        var partCols = partRoot.GetComponentsInChildren<Collider>(includeInactive: true);
        foreach (var c in partCols)
        {
            if (c && !baseGrab.colliders.Contains(c))
                baseGrab.colliders.Add(c);
        }

        // 3) Remove the part's rigidbody (Base becomes the only RB)
        var rb = partRoot.GetComponent<Rigidbody>();
        if (rb) Object.Destroy(rb);

        // 4) Parent under Base to move as one
        partRoot.SetParent(baseGrab.transform, true);
    }
}
