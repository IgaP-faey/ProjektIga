using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CameraSwitcher : MonoBehaviour
{
    [System.Serializable]
    public enum ViewMode
    {
        FirstPerson,   // kamera w „oczach” postaci
        ThirdPerson,   // kamera za plecami postaci
        CustomAnchor   // kamera na wskazanym anchorze (Transform)
    }

    [System.Serializable]
    public class ViewTarget
    {
        [Header("Cel")]
        public string label = "Postaæ / Punkt";
        public Transform root;                 // np. transform postaci (obowi¹zkowe dla FP/TP)

        [Header("Tryb widoku")]
        public ViewMode mode = ViewMode.ThirdPerson;
        public Transform customAnchor;         // u¿ywane w CustomAnchor

        [Header("Offsety i kierunek patrzenia")]
        public Vector3 thirdPersonOffset = new Vector3(0f, 1.8f, -3f);
        public Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0.05f);
        public Transform lookAtOverride;      // jeœli ustawione – patrz w ten punkt
        public float lookHeight = 1.6f;       // gdy brak lookAtOverride: root.position + (0, lookHeight, 0)

        [Header("Parametry optyki")]
        [Range(10f, 120f)] public float fov = 60f;

        [Header("Orientacja")]
        public bool alignWithTargetForward = false; // Jeœli true, kamera alignuje siê do forward celu (przydatne dla FP/„ramiê” TP)
        public Vector3 additionalEuler = Vector3.zero; // ekstra obrócenie

        [Header("Follow po przejœciu")]
        public bool continuousFollow = true;  // œledzenie celu po zakoñczeniu blendu
        [Range(0f, 30f)] public float followPosLerp = 10f;
        [Range(0f, 30f)] public float followRotLerp = 12f;
    }

    [Header("Kamera")]
    public Camera cam;                         // jeœli puste – u¿yje Camera.main

    [Header("Sterowanie")]
    public KeyCode nextKey = KeyCode.Tab;
    public KeyCode prevKey = KeyCode.BackQuote; // lub KeyCode.Backspace / LeftShift + Tab -> obs³u¿ po swojemu
    public bool loop = true;

    [Header("Przejœcie (blend)")]
    [Range(0f, 5f)] public float transitionTime = 0.6f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Start")]
    public int startIndex = 0;                 // który cel na starcie
    public bool switchOnStart = true;          // automatycznie prze³¹cz na startIndex przy starcie

    [Header("Zdarzenia")]
    public UnityEvent<int> OnCameraSwitched;   // odpala siê po zakoñczeniu prze³¹czenia (z indexem celu)

    public List<ViewTarget> targets = new List<ViewTarget>();

    int _currentIndex = -1;
    Coroutine _blendCo;
    bool _isBlending = false;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!cam)
        {
            Debug.LogError("[CameraSwitcher] Brak kamery! Pod³¹cz Camera w inspektorze albo dodaj Camera.main.");
        }
    }

    void Start()
    {
        if (switchOnStart && targets.Count > 0)
        {
            startIndex = Mathf.Clamp(startIndex, 0, targets.Count - 1);
            SwitchTo(startIndex, instant: true);
        }
    }

    void Update()
    {
        if (targets.Count == 0 || cam == null) return;

        if (Input.GetKeyDown(nextKey)) Next();
        if (Input.GetKeyDown(prevKey)) Prev();

        // P³ynne follow po zakoñczeniu blendu
        if (!_isBlending && _currentIndex >= 0 && _currentIndex < targets.Count)
        {
            var t = targets[_currentIndex];
            if (t.continuousFollow)
            {
                // liczymy bie¿¹c¹ docelow¹ pozycjê i rotacjê
                Vector3 targetPos;
                Quaternion targetRot;
                float targetFov;
                ComputeTargetPose(t, out targetPos, out targetRot, out targetFov);

                // pozycja
                cam.transform.position = Vector3.Lerp(
                    cam.transform.position,
                    targetPos,
                    1f - Mathf.Exp(-t.followPosLerp * Time.deltaTime)
                );

                // rotacja
                cam.transform.rotation = Quaternion.Slerp(
                    cam.transform.rotation,
                    targetRot,
                    1f - Mathf.Exp(-t.followRotLerp * Time.deltaTime)
                );

                // FOV (te¿ ³agodnie)
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
        }
    }

    /// <summary>Prze³¹cz na nastêpny cel</summary>
    public void Next()
    {
        if (targets.Count == 0) return;
        int next = _currentIndex + 1;
        if (next >= targets.Count) next = loop ? 0 : targets.Count - 1;
        SwitchTo(next, instant: false);
    }

    /// <summary>Prze³¹cz na poprzedni cel</summary>
    public void Prev()
    {
        if (targets.Count == 0) return;
        int prev = _currentIndex - 1;
        if (prev < 0) prev = loop ? targets.Count - 1 : 0;
        SwitchTo(prev, instant: false);
    }

    /// <summary>G³ówna metoda prze³¹czania</summary>
    public void SwitchTo(int index, bool instant = false)
    {
        if (cam == null || targets.Count == 0) return;
        index = Mathf.Clamp(index, 0, targets.Count - 1);
        if (_blendCo != null) StopCoroutine(_blendCo);

        var target = targets[index];

        if (instant || transitionTime <= 0f)
        {
            Vector3 pos; Quaternion rot; float fov;
            ComputeTargetPose(target, out pos, out rot, out fov);
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            _currentIndex = index;
            _isBlending = false;
            OnCameraSwitched?.Invoke(_currentIndex);
        }
        else
        {
            _blendCo = StartCoroutine(BlendTo(index));
        }
    }

    IEnumerator BlendTo(int index)
    {
        _isBlending = true;

        var startPos = cam.transform.position;
        var startRot = cam.transform.rotation;
        var startFov = cam.fieldOfView;

        Vector3 endPos; Quaternion endRot; float endFov;
        ComputeTargetPose(targets[index], out endPos, out endRot, out endFov);

        float t = 0f;
        float dur = Mathf.Max(0.0001f, transitionTime);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float e = ease != null ? ease.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

            cam.transform.position = Vector3.Lerp(startPos, endPos, e);
            cam.transform.rotation = Quaternion.Slerp(startRot, endRot, e);
            cam.fieldOfView = Mathf.Lerp(startFov, endFov, e);

            // Jeœli cel porusza siê w trakcie blendu, odœwie¿amy end pose (lekkie pod¹¿anie)
            ComputeTargetPose(targets[index], out endPos, out endRot, out endFov);

            yield return null;
        }

        cam.transform.SetPositionAndRotation(endPos, endRot);
        cam.fieldOfView = endFov;

        _currentIndex = index;
        _isBlending = false;
        _blendCo = null;

        OnCameraSwitched?.Invoke(_currentIndex);
    }

    void ComputeTargetPose(ViewTarget t, out Vector3 pos, out Quaternion rot, out float fov)
    {
        fov = Mathf.Clamp(t.fov, 10f, 120f);

        // 1) Pozycja
        switch (t.mode)
        {
            case ViewMode.CustomAnchor:
                if (t.customAnchor)
                {
                    pos = t.customAnchor.position;
                    rot = t.customAnchor.rotation;
                    rot *= Quaternion.Euler(t.additionalEuler);
                    return;
                }
                // fallback do TP jeœli anchor pusty
                goto case ViewMode.ThirdPerson;

            case ViewMode.FirstPerson:
                {
                    Vector3 basePos = t.root ? t.root.position : Vector3.zero;
                    Vector3 forward = t.root ? t.root.forward : Vector3.forward;
                    pos = basePos + t.firstPersonOffset.x * (t.root ? t.root.right : Vector3.right)
                                  + t.firstPersonOffset.y * Vector3.up
                                  + t.firstPersonOffset.z * forward;

                    // Kierunek patrzenia: override -> w punkt, inaczej w kierunku forward
                    Vector3 lookPoint = t.lookAtOverride ? t.lookAtOverride.position
                                      : (t.root ? t.root.position + Vector3.up * t.lookHeight : pos + forward);
                    Vector3 dir = t.alignWithTargetForward && t.root ? t.root.forward : (lookPoint - pos).normalized;

                    rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(t.additionalEuler);
                    return;
                }

            case ViewMode.ThirdPerson:
            default:
                {
                    Vector3 basePos = t.root ? t.root.position : Vector3.zero;
                    // budujemy offset w lokalnych osiach celu (jeœli brak celu – w osiach œwiata)
                    Vector3 right = t.root ? t.root.right : Vector3.right;
                    Vector3 up = Vector3.up;
                    Vector3 fwd = t.root ? t.root.forward : Vector3.forward;

                    pos = basePos + right * t.thirdPersonOffset.x
                                  + up * t.thirdPersonOffset.y
                                  + fwd * t.thirdPersonOffset.z;

                    Vector3 lookPoint = t.lookAtOverride ? t.lookAtOverride.position
                                      : (t.root ? t.root.position + Vector3.up * t.lookHeight : basePos);

                    Vector3 dir = (lookPoint - pos).sqrMagnitude > 0.0001f
                                ? (lookPoint - pos).normalized
                                : (t.root ? t.root.forward : Vector3.forward);

                    if (t.alignWithTargetForward && t.root)
                        dir = t.root.forward;

                    rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(t.additionalEuler);
                    return;
                }
        }
    }

    /// <summary>Aktualny indeks celu (lub -1 jeœli nie ustawiono)</summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>Zwraca bie¿¹cy cel (mo¿e byæ null jeœli brak)</summary>
    public ViewTarget CurrentTarget => (_currentIndex >= 0 && _currentIndex < targets.Count) ? targets[_currentIndex] : null;
}
