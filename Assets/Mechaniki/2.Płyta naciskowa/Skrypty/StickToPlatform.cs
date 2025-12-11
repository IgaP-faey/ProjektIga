// Na graczu
using UnityEngine;

public class StickToPlatform : MonoBehaviour
{
    public string platformTag = "MovingPlatform";
    private Transform originalParent;

    void Awake() { originalParent = transform.parent; }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.CompareTag(platformTag))
        {
            // Tylko gdy faktycznie „stoimy” (uderzenie od góry)
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.5f)
                transform.SetParent(hit.collider.transform, true);
        }
    }

    void Update()
    {
        // Jeœli nie dotykamy ¿adnej platformy – odczep
        // Proœciutko: raycast w dó³
        if (!Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out var hit, 0.3f))
            transform.SetParent(originalParent, true);
        else if (!hit.collider.CompareTag(platformTag))
            transform.SetParent(originalParent, true);
    }
}
