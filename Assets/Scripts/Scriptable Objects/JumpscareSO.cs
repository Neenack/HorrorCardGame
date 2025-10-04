using UnityEngine;

[System.Serializable]
public abstract class BaseJumpscareSO : ScriptableObject
{
    public string id;
    public abstract void Trigger(Transform player);
}
