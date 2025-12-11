using UnityEngine;
using UnityEngine.UI;

public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance;
    [SerializeField] private Text promptText;
    [SerializeField] private CanvasGroup group;

    private void Awake()
    {
        Instance = this;
        Hide();
    }

    public void Show(string text)
    {
        if (promptText) promptText.text = text;
        if (group) { group.alpha = 1f; group.interactable = false; group.blocksRaycasts = false; }
    }

    public void Hide()
    {
        if (group) { group.alpha = 0f; }
    }
}
