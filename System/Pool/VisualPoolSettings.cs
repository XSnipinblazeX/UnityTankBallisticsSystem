using UnityEngine;

[CreateAssetMenu(fileName = "VisualPoolSettings", menuName = "Bullet System/Visual Pool Settings")]
public class VisualPoolSettings : ScriptableObject
{
    public GameObject visualPrefab;
    public int poolSize = 10;
}