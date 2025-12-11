using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    private HashSet<string> keys = new HashSet<string>();
    public event Action OnKeysChanged;

    public void AddKey(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (keys.Add(id)) OnKeysChanged?.Invoke();
    }

    public bool HasKey(string id) => keys.Contains(id);

    // (opcjonalnie) do podgl¹du w Inspectorze:
    [SerializeField] private List<string> debugKeys = new List<string>();
    private void OnValidate()
    {
        debugKeys = new List<string>(keys);
    }
}
