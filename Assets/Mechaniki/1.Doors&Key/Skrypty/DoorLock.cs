using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DoorLock : MonoBehaviour
{
    public string requiredKeyId = "Red";
    public Transform doorHinge;         // pivot skrzyd³a drzwi
    public float openAngle = 90f;
    public float openSpeed = 2f;
    public string promptLocked = "E – u¿yj klucza";
    public string promptNoKey = "Zablokowane (brak klucza)";

    private bool isOpen;
    private bool playerIn;
    private Inventory playerInv;
    private Quaternion closedRot;
    private Collider doorCollider;

    private void Awake()
    {
        if (!doorHinge) doorHinge = transform; // awaryjnie
        closedRot = doorHinge.rotation;
        doorCollider = GetComponent<Collider>();
        if (doorCollider) doorCollider.isTrigger = false; // blokuje przejœcie do czasu otwarcia
    }

    private void OnTriggerEnter(Collider other)
    {
        // Uwaga: to jest kolider „obszaru interakcji”, NIE ten blokuj¹cy.
        if (other.CompareTag("Player"))
        {
            playerIn = true;
            playerInv = other.GetComponent<Inventory>();
            UpdatePrompt();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIn = false;
            playerInv = null;
            InteractionPromptUI.Instance?.Hide();
        }
    }

    private void Update()
    {
        if (!playerIn || isOpen) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (playerInv != null && playerInv.HasKey(requiredKeyId))
            {
                StartCoroutine(OpenDoor());
            }
            else
            {
                // feedback: brak klucza (krótkie migniêcie)
                StartCoroutine(FlashNoKey());
            }
        }
    }

    private IEnumerator OpenDoor()
    {
        isOpen = true;
        InteractionPromptUI.Instance?.Hide();

        // Otwieraj rotacj¹ wokó³ zawiasu
        Quaternion target = closedRot * Quaternion.Euler(0f, openAngle, 0f);
        float t = 0f;
        // wy³¹cz kolider blokuj¹cy na koñcu (albo w po³owie animacji)
        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;
            doorHinge.rotation = Quaternion.Slerp(closedRot, target, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        if (doorCollider) doorCollider.enabled = false;
    }

    private IEnumerator FlashNoKey()
    {
        InteractionPromptUI.Instance?.Show(promptNoKey);
        yield return new WaitForSeconds(0.8f);
        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (isOpen || !playerIn) { InteractionPromptUI.Instance?.Hide(); return; }
        bool has = playerInv != null && playerInv.HasKey(requiredKeyId);
        InteractionPromptUI.Instance?.Show(has ? promptLocked : promptNoKey);
    }
}
