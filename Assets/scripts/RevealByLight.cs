using UnityEngine;

// Put this on the Book object (or a child).
public class RevealByLight : MonoBehaviour
{
    [Header("Lamp Light (drag your lamp's Light here)")]
    public Light lampLight;

    [Header("Target")]
    [Tooltip("Collider that bounds the pages. If empty, tries to use a collider on this object.")]
    public Collider bookTarget;

    [Header("What to reveal")]
    [Tooltip("The Text (or GameObject) that should become visible. Start this INACTIVE in the Inspector.")]
    public GameObject codeObject;

    [Header("Detection (generous)")]
    [Tooltip("How close the light must be to the book's pages (meters).")]
    public float triggerRadius = 0.7f;          // generous radius
    [Tooltip("Extra tolerance added to the spot angle (degrees).")]
    public float extraSpotTolerance = 15f;      // generous cone
    [Tooltip("Require a clear line of sight from the light to the book.")]
    public bool requireLineOfSight = false;     // OFF by default so it's easy

    [Header("Timing")]
    [Tooltip("How long the light must be on the book before revealing.")]
    public float requiredLitSeconds = 2f;

    float timer;
    bool revealed;

    void Awake()
    {
        if (!bookTarget) bookTarget = GetComponentInChildren<Collider>();
        // Make sure the text starts hidden by being inactive (user asked for this)
        if (codeObject) codeObject.SetActive(false);
    }

    void Update()
    {
        if (revealed || lampLight == null || bookTarget == null || codeObject == null)
            return;

        if (IsIlluminatedGenerous())
        {
            timer += Time.deltaTime;
            if (timer >= requiredLitSeconds)
            {
                revealed = true;
                codeObject.SetActive(true);   // <<< simply enable the GameObject
            }
        }
        else
        {
            timer = 0f; // reset if the light leaves
        }
    }

    bool IsIlluminatedGenerous()
    {
        if (!lampLight.enabled) return false;

        // Closest point on the pages to the light position
        Vector3 lightPos = lampLight.transform.position;
        Vector3 targetPoint = bookTarget.ClosestPoint(lightPos);

        // 1) Distance check (uses our own generous triggerRadius)
        if (Vector3.Distance(lightPos, targetPoint) > triggerRadius)
            return false;

        // 2) Spot cone check (make generous)
        if (lampLight.type == LightType.Spot)
        {
            Vector3 toTarget = (targetPoint - lightPos).normalized;
            float angle = Vector3.Angle(lampLight.transform.forward, toTarget);
            float halfAngle = lampLight.spotAngle * 0.5f + extraSpotTolerance;
            if (angle > halfAngle) return false;
        }

        // 3) Optional occlusion check
        if (requireLineOfSight)
        {
            if (Physics.Linecast(lightPos, targetPoint, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                // allow the bookTarget or any of its children
                if (hit.collider != bookTarget && !hit.collider.transform.IsChildOf(bookTarget.transform))
                    return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!bookTarget || !lampLight) return;
        Gizmos.color = new Color(1, 1, 0, 0.25f);
        Gizmos.DrawWireSphere(bookTarget.bounds.center, triggerRadius);
    }
#endif
}