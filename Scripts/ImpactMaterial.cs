using System;
using UnityEngine;

namespace GNB
{
    /// <summary>
    /// Defines the impact effects for a surface. Attach this to any object that should produce
    /// custom particle effects when struck by a bullet.
    /// </summary>
    public class ImpactMaterial : MonoBehaviour
    {
        [Serializable]
        public class ImpactEffect
        {
            public GameObject Prefab;
            [Tooltip("The default scale of this effect, corresponding to the baseline impact (100mm, 15.6kg, 900m/s).")]
            public float DefaultScale = 1.0f;
        }

        [Header("Impact Effects")]
        public ImpactEffect SprayEffect;
        public ImpactEffect SmokeEffect;
        public ImpactEffect SparkEffect;
        public ImpactEffect DebrisEffect;

        /// <summary>
        /// Spawns the defined impact effects at a specific point and orientation.
        /// </summary>
        /// <param name="position">The world-space point of impact.</param>
        /// <param name="normal">The normal of the surface that was hit.</param>
        /// <param name="scaleMultiplier">A multiplier to scale the effects based on impact energy and size.</param>
        public void HandleImpact(Vector3 position, Vector3 normal, float scaleMultiplier)
        {
            // Create a rotation that aligns the effect's Y-axis (up) with the surface normal.
            // This is useful for effects that spray "up" from a surface.
            // We can't just use LookRotation's upward parameter, as we need a forward vector that isn't parallel to the normal.
            Vector3 randomForward = Vector3.Slerp(Vector3.forward, Vector3.right, 0.5f); // A vector not parallel to the normal
            Quaternion impactRotation = Quaternion.LookRotation(Vector3.Cross(normal, randomForward), normal);
            
            SpawnEffect(SprayEffect, position, impactRotation, scaleMultiplier);
            SpawnEffect(SmokeEffect, position, impactRotation, scaleMultiplier);
            SpawnEffect(SparkEffect, position, impactRotation, scaleMultiplier);
            SpawnEffect(DebrisEffect, position, impactRotation, scaleMultiplier);
        }

        private void SpawnEffect(ImpactEffect effect, Vector3 position, Quaternion rotation, float scaleMultiplier)
        {
            if (effect.Prefab != null)
            {
                GameObject instance = Instantiate(effect.Prefab, position, rotation);
                float finalScale = effect.DefaultScale * scaleMultiplier;

                // Apply the scale to all ParticleSystem transforms in the hierarchy to ensure consistent scaling.
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.transform.localScale *= finalScale;
                }
            }
        }
    }
}