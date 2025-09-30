using UnityEngine;
using GNB;

[CreateAssetMenu(fileName = "BulletPoolSettings", menuName = "Bullet System/Bullet Pool Settings")]
public class BulletPoolSettings : ScriptableObject
{
    public Bullet bulletPrefab;
    public int poolSize = 10; // Default size
}
