using UnityEngine;

[CreateAssetMenu(menuName = "Ueq/Spawn Timer")]
public class SpawnTimer : ScriptableObject
{
    public float baseSeconds = 300f;
    public float variance    = 60f;

    public float Roll() => Mathf.Max(0f, baseSeconds + Random.Range(-variance, variance));
}
