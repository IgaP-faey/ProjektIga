using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public enum Dir { Up, Down, Left, Right, Forward, Back, Custom }
    public Dir direction = Dir.Forward;
    public Vector3 customDirection = Vector3.zero; // u¿ywane gdy Dir=Custom
    public float distance = 5f;
    public float speed = 2f;
    public bool localSpace = true;           // ruch w lokalnym czy œwiatowym uk³adzie
    public bool returnWhenReleased = true;   // wraca po zejœciu z p³yty

    [Tooltip("Tylko podgl¹d")][SerializeField] private float t; // 0..1
    public bool IsActivated { get; set; }

    private Vector3 startPos;
    private Vector3 targetPos;

    void Awake()
    {
        startPos = transform.position;
        var dirVec = GetDirVector().normalized;
        var delta = dirVec * distance;
        targetPos = localSpace ? startPos + transform.TransformDirection(delta) : startPos + delta;
    }

    void Update()
    {
        float targetT = IsActivated ? 1f : (returnWhenReleased ? 0f : t);
        t = Mathf.MoveTowards(t, targetT, speed * Time.deltaTime / Mathf.Max(distance, 0.0001f));
        transform.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, t));
    }

    Vector3 GetDirVector()
    {
        switch (direction)
        {
            case Dir.Up: return Vector3.up;
            case Dir.Down: return Vector3.down;
            case Dir.Left: return Vector3.left;
            case Dir.Right: return Vector3.right;
            case Dir.Forward: return Vector3.forward;
            case Dir.Back: return Vector3.back;
            case Dir.Custom: return customDirection;
            default: return Vector3.forward;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 from = Application.isPlaying ? startPos : transform.position;
        Vector3 dir = (direction == Dir.Custom ? customDirection : GetDirVector()).normalized;
        Vector3 to = from + (localSpace ? transform.TransformDirection(dir) : dir) * distance;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireCube(to, Vector3.one * 0.25f);
    }
#endif
}
