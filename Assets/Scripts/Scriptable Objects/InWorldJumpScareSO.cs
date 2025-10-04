using UnityEngine;
using System.Collections;

[System.Serializable]
[CreateAssetMenu(fileName = "Jump Scare", menuName = "Jumpscare/InWorld")]
public class InWorldJumpscareSO : BaseJumpscareSO
{
    public GameObject prefab;
    public float duration = 2f;
    public Vector3 offset;

    public override void Trigger(Transform player)
    {
        JumpscareManager.Instance.StartCoroutine(SpawnRoutine(player));
    }

    private IEnumerator SpawnRoutine(Transform player)
    {
        Vector3 pos = player.position + offset;
        Quaternion rot = Quaternion.LookRotation(-player.forward);
        GameObject instance = Instantiate(prefab, pos, rot);

        yield return new WaitForSeconds(duration);
        Destroy(instance);
    }
}
