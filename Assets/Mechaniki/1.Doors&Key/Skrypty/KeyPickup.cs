using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KeyPickup : MonoBehaviour
{
    public string keyId = "Red";     // dopasuj do drzwi
    public string prompt = "E – podnieœ klucz";

    private bool playerIn;
    private Inventory playerInv;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIn = true;
            playerInv = other.GetComponent<Inventory>();
            InteractionPromptUI.Instance?.Show(prompt);
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
        if (!playerIn || playerInv == null) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            playerInv.AddKey(keyId);
            InteractionPromptUI.Instance?.Hide();
            Destroy(gameObject);
        }
    }

    // (opcjonalnie) lekki efekt „lewitacji”
    private void LateUpdate()
    {
        transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
    }
}
