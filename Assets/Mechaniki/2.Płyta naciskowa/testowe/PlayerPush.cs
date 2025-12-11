using UnityEngine;

public class PlayerPush : MonoBehaviour
{
    public Camera cam;
    public float interactDistance = 2.0f;
    public LayerMask interactMask;
    public float pushForce = 80f;      // si�a pchania
    public float maxBoxSpeed = 3.5f;   // limit pr�dko�ci skrzyni
    public float alignDot = 0.5f;      // jak bardzo �od przodu� musisz patrze�

    Rigidbody activeBox;
    bool pushing;

    void Reset()
    {
        cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        // Detekcja skrzyni przed nami (ray)
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            var rb = hit.rigidbody;
            var pick = hit.collider.GetComponentInParent<Pickupable>();
            bool cand = rb && pick;
            if (cand && !pushing)
                InteractionPromptUI.Instance?.Show("E � pchaj   F � podnie�");
            else if (!pushing)
                InteractionPromptUI.Instance?.Hide();

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (!pushing && cand && Vector3.Dot(cam.transform.forward, (rb.worldCenterOfMass - transform.position).normalized) > alignDot)
                {
                    activeBox = rb;
                    pushing = true;
                    InteractionPromptUI.Instance?.Show("E � przesta� pcha�");
                }
                else if (pushing)
                {
                    pushing = false;
                    activeBox = null;
                    InteractionPromptUI.Instance?.Hide();
                }
            }
        }
        else if (!pushing)
        {
            InteractionPromptUI.Instance?.Hide();
        }
    }

    void FixedUpdate()
    {
        if (!pushing || !activeBox) return;

        // Pchamy tylko gdy jeste�my skierowani do skrzyni i blisko
        Vector3 toBox = (activeBox.worldCenterOfMass - transform.position);
        if (toBox.magnitude > interactDistance * 1.2f || Vector3.Dot(cam.transform.forward, toBox.normalized) < alignDot)
        {
            pushing = false; activeBox = null;
            InteractionPromptUI.Instance?.Hide();
            return;
        }

        // Kierunek pchania = forward gracza, ale �przy ziemi�
        Vector3 dir = cam.transform.forward; dir.y = 0f; dir.Normalize();
        Vector3 pushPoint = activeBox.worldCenterOfMass + Vector3.down * 0.2f;

        // Limit pr�dko�ci
        if (activeBox.linearVelocity.magnitude < maxBoxSpeed)
            activeBox.AddForceAtPosition(dir * pushForce * Time.fixedDeltaTime, pushPoint, ForceMode.VelocityChange);
    }
}
