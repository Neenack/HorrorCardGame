using Unity.Netcode;
using UnityEngine;

public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    public static bool exists
    {
        get
        {
            return instance != null;
        }
    }

    private static T instance = null;
    public static T Instance
    {
        get
        {
            //if it has no prior instance, find one
            if (instance == null)
            {
                instance = GameObject.FindFirstObjectByType(typeof(T)) as T;
            }

            return instance;
        }
        set
        {
            if (instance == null)
            {
                instance = value;
            }
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
        }
    }

    public void OnDestroy()
    {
        instance = null;
    }

    private void OnApplicationQuit()
    {
        instance = null;
    }
}