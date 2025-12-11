using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    public MovingPlatform[] targets;       // przypnij platformy w Inspectorze
    public Transform plateVisual;          // PlateMesh do „ugiêcia”
    public float pressDepth = 0.05f;       // jak g³êboko wciska siê wizualnie
    public float pressSpeed = 8f;          // jak szybko
    public string playerTag = "Player";

    private int pressCount = 0;            // wspiera wiele cia³ na p³ycie
    private Vector3 plateStartPos;

    void Awake()
    {
        if (plateVisual == null) plateVisual = transform;
        plateStartPos = plateVisual.localPosition;
        var c = GetComponent<Collider>();
        c.isTrigger = true; // wa¿ne: wykrywa wejœcie gracza
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        pressCount++;
        if (pressCount == 1) SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        pressCount = Mathf.Max(0, pressCount - 1);
        if (pressCount == 0) SetActive(false);
    }

    void Update()
    {
        // wizualne ugiêcie
        Vector3 target = plateStartPos + Vector3.down * ((pressCount > 0) ? pressDepth : 0f);
        plateVisual.localPosition = Vector3.Lerp(plateVisual.localPosition, target, Time.deltaTime * pressSpeed);
    }

    void SetActive(bool active)
    {
        foreach (var t in targets)
            if (t) t.IsActivated = active;
    }
}
