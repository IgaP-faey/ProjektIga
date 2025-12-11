using UnityEngine;

public class Pickupable : MonoBehaviour
{
    [Tooltip("Jeœli > 0, to nadpisze mass na Rigidbody przy starcie (u³atwia konfig).")]
    public float overrideMass = 0f;

    void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb && overrideMass > 0f) rb.mass = overrideMass;
    }
}
