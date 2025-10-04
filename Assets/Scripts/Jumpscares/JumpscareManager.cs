using UnityEngine;
using System.Collections.Generic;

public class JumpscareManager : MonoSingleton<JumpscareManager>
{
    private Dictionary<string, BaseJumpscareSO> jumpscareMap;

    private void Start()
    {
        LoadAllJumpscares();
    }

    private void LoadAllJumpscares()
    {
        jumpscareMap = new Dictionary<string, BaseJumpscareSO>();

        // Loads all ScriptableObjects of type BaseJumpscareSO from any folder named "Resources"
        BaseJumpscareSO[] scares = Resources.LoadAll<BaseJumpscareSO>("");

        foreach (var js in scares)
        {
            if (js == null) continue;

            if (!jumpscareMap.ContainsKey(js.id))
            {
                jumpscareMap.Add(js.id, js);
                // Debug.Log($"Loaded jumpscare: {js.id}");
            }
            else
            {
                Debug.LogWarning($"Duplicate Jumpscare ID detected: {js.id}. Ignoring duplicate.");
            }
        }

        Debug.Log($"JumpscareManager: Loaded {jumpscareMap.Count} jumpscares from Resources.");
    }

    public void Trigger(string id, Transform player = null)
    {
        if (jumpscareMap == null || jumpscareMap.Count == 0)
        {
            Debug.LogWarning("JumpscareManager: No jumpscares loaded!");
            return;
        }

        if (jumpscareMap.TryGetValue(id, out var scare))
        {
            scare.Trigger(player);
        }
        else
        {
            Debug.LogWarning($"JumpscareManager: Jumpscare ID '{id}' not found.");
        }
    }
}
