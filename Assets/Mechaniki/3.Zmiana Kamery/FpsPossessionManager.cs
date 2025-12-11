using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.Rendering;   // <= Volume
using System.Collections;

public class FpsPossessionManager : MonoBehaviour
{
    [System.Serializable]
    public class Pawn
    {
        public GameObject root;
        public CharacterController characterController;
        public MonoBehaviour firstPersonController;
        public PlayerInput playerInput;
        public CinemachineCamera vcam;                   // CM3
        public CinemachineInputAxisController cineInput; // opcjonalnie
        public Volume postProcessVolume;                 // <= DODANE: Global Volume dla tej postaci
    }

    public Pawn[] pawns;

    [Header("Camera priorities")]
    public int activePriority = 20;
    public int inactivePriority = 10;

    [Header("Input System")]
    public string actionMapName = "Player";

    [Header("Post-process blend")]
    [Range(0f, 5f)] public float ppBlendDuration = 1.0f;

    [Header("Start")]
    public int startIndex = 0;

    private int currentIndex = -1;
    private Coroutine enableCo;
    private Coroutine ppBlendCo;

    void Start()
    {
        // wy³¹cz wszystkich + zresetuj volumy
        for (int i = 0; i < pawns.Length; i++)
        {
            if (pawns[i].vcam) pawns[i].vcam.Priority = inactivePriority;
            SafeEnablePawn(pawns[i], false);
            if (pawns[i].postProcessVolume) pawns[i].postProcessVolume.weight = 0f;
        }

        Possess(Mathf.Clamp(startIndex, 0, pawns.Length - 1), instant: true);
    }

    void Update()
    {
        for (int i = 0; i < pawns.Length && i < 9; i++)
            if (Keyboard.current[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Possess(i);

        if (Keyboard.current.qKey.wasPressedThisFrame)
            Possess((currentIndex - 1 + pawns.Length) % pawns.Length);

        if (Keyboard.current.eKey.wasPressedThisFrame)
            Possess((currentIndex + 1) % pawns.Length);
    }

    public void Possess(int index, bool instant = false)
    {
        if (index == currentIndex && !instant) return;

        // ustaw priorytety kamer
        for (int i = 0; i < pawns.Length; i++)
            if (pawns[i].vcam) pawns[i].vcam.Priority = (i == index) ? activePriority : inactivePriority;

        // blend PP: od starego do nowego
        Volume from = (currentIndex >= 0 && currentIndex < pawns.Length) ? pawns[currentIndex].postProcessVolume : null;
        Volume to = pawns[index].postProcessVolume;

        if (ppBlendCo != null) StopCoroutine(ppBlendCo);
        if (instant || ppBlendDuration <= 0f)
        {
            if (from) from.weight = 0f;
            if (to) to.weight = 1f;
        }
        else
        {
            ppBlendCo = StartCoroutine(BlendVolumes(from, to, ppBlendDuration));
        }

        // WY£¥CZ wszystkich, potem w³¹cz docelowego w kolejnej klatce (fix na blokowanie inputu)
        for (int i = 0; i < pawns.Length; i++) SafeEnablePawn(pawns[i], false);
        currentIndex = index;

        if (enableCo != null) StopCoroutine(enableCo);
        enableCo = StartCoroutine(EnablePawnNextFrame(pawns[currentIndex], instant));

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator EnablePawnNextFrame(Pawn p, bool instant)
    {
        if (!instant) yield return null;

        SafeEnablePawn(p, true);

        if (p.playerInput)
        {
            p.playerInput.ActivateInput();
            if (!string.IsNullOrEmpty(actionMapName))
                p.playerInput.SwitchCurrentActionMap(actionMapName);
            p.playerInput.actions?.Enable();
        }
        enableCo = null;
    }

    private IEnumerator BlendVolumes(Volume from, Volume to, float duration)
    {
        float t = 0f;
        float fromStart = from ? from.weight : 0f;
        float toStart = to ? to.weight : 0f;

        while (t < duration)
        {
            float a = t / duration;
            if (from) from.weight = Mathf.Lerp(fromStart, 0f, a);
            if (to) to.weight = Mathf.Lerp(toStart, 1f, a);
            t += Time.deltaTime;
            yield return null;
        }
        if (from) from.weight = 0f;
        if (to) to.weight = 1f;
        ppBlendCo = null;
    }

    private void SafeEnablePawn(Pawn p, bool enable)
    {
        if (p.playerInput) p.playerInput.enabled = enable;
        if (p.firstPersonController) p.firstPersonController.enabled = enable;
        if (p.characterController) p.characterController.enabled = enable;
        if (p.cineInput) p.cineInput.enabled = enable; // jeœli u¿ywasz
    }
}
