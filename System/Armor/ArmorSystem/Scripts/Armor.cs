using UnityEngine;

namespace ArmorSystem
{
    public class Armor : MonoBehaviour
    {
        [Tooltip("The effective thickness of the armor plate in millimeters.")]

        public ArmorCodex data;

        public float thicknessMM = 10f;


        public string Name = "Armor";

        public LayerMask backfaceLayer;

        [Header("Armor Settings")]
        public Transform IdealIncomingThreatAngleZ;
        public float MaxAnglePenaltyRatio = 0.5f;

        public float MaxPenaltyCalcAngle = 90f;

        [SerializeField]
        private bool CreateRenderer = false;

        [SerializeField]
        private bool CopyMaterials = false;

        [SerializeField]
        private bool _debug = true;




        private void Awake()
        {
            MeshCollider meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                Debug.LogError("No mesh collider present on variable armor piece " + Name + " in " + gameObject.name + "! Correcting...", this);
                Collider component = GetComponent<Collider>();
                if (component != null)
                {
                    component.enabled = false;
                }
                meshCollider = base.gameObject.AddComponent<MeshCollider>();
            }
        }



        // This is where your future gameplay logic will go.
        // For example, a method to handle projectile hits.
        public void HandleHit(float penetrationValue)
        {
            if (penetrationValue > thicknessMM)
            {
                Debug.Log($"<color=green>Penetration!</color> Armor: {thicknessMM}mm, Hit: {penetrationValue}");
                // TODO: Apply damage to the tank's main health component.
            }
        }

        public float GetLosThickness(bool kinetic, Vector3 contactPoint, Vector3 surfaceNormal, Vector3 penetrationVector, bool isSpall, bool forcePhysicalDimensionsOnly = false)
        {
            // 1. Get base thickness and apply material effectiveness
            float baseThickness = thicknessMM;
            float effectiveNormalThickness = baseThickness * data.armorData.GetEffectiveness(kinetic, baseThickness);

            if (_debug && !isSpall)
            {
                Debug.Log($"Armor piece {gameObject.name}: Base thickness {baseThickness:F1}mm -> Material-adjusted normal thickness {effectiveNormalThickness:F1}mm.");
            }

            // 2. Apply angle-based performance penalty (if configured)
            if (forcePhysicalDimensionsOnly || IdealIncomingThreatAngleZ == null)
            {
                return effectiveNormalThickness;
            }

            float anglePenalty = 1f;
            float angleToIdeal = Vector3.Angle(penetrationVector, IdealIncomingThreatAngleZ.forward);

            if (angleToIdeal > MaxPenaltyCalcAngle)
            {
                anglePenalty = 1f - MaxAnglePenaltyRatio;
            }
            else
            {
                float ratio = angleToIdeal / MaxPenaltyCalcAngle;
                anglePenalty = 1f - MaxAnglePenaltyRatio * ratio;
            }

            float finalNormalThickness = effectiveNormalThickness * anglePenalty;

            if (_debug && !isSpall)
            {
                Debug.Log($"(Angle-based performance penalty of {anglePenalty:P1} applied, final normal thickness for calculation: {finalNormalThickness:F1} mm)");
            }

            return finalNormalThickness;
        }


        private int getSpallCount(float maxSpallAngle)
        {
            int num = 30;

            num = Mathf.RoundToInt((float)data.armorData.maxSpallCount * (maxSpallAngle / 45f) * data.armorData.spallPowerMultiplier);
            if (num > Mathf.RoundToInt(data.armorData.maxSpallCount))
            {
                num = Mathf.RoundToInt(data.armorData.maxSpallCount);
            }
            return num;
        }

        public Collider[] GetRadiusColliders(Vector3 position, float radius)
        {
            // Use Physics.OverlapSphere to get all colliders within the radius.
            return Physics.OverlapSphere(position, radius);
        }

        public void GenerateSpall(GNB.Bullet bullet, Vector3 projectileExitPoint, Vector3 projectileDirection, Vector3 projectileVelocity)
        {
            // Exclude HE fragmentation from this spall cap logic.
            if (bullet.codex.projectileData.penetratorType != ProjectileData.PenetratorType.KE)
            {
                int heFragmentCount = 30; // Default HE fragment count
                FireSpallFragments(heFragmentCount, projectileExitPoint, projectileDirection, data.armorData.spallAngle * data.armorData.spallAngleMultiplier, projectileVelocity);
                return;
            }

            if (bullet.SpallFragmentCap <= 0) return;

            // Spall fragments are generated from the backface (exit point) of the armor plate.
            int fragmentsToGenerate = 30; // Base number of fragments for this plate.

            int finalFragmentCount = Mathf.Min(fragmentsToGenerate, bullet.SpallFragmentCap);
            bullet.SpallFragmentCap -= finalFragmentCount;

            // Use spallAngle and spallAngleMultiplier to determine the cone of spall.
            float spallConeAngle = data.armorData.spallAngle * data.armorData.spallAngleMultiplier;

            FireSpallFragments(finalFragmentCount, projectileExitPoint, projectileDirection, spallConeAngle, projectileVelocity);
        }

        private void FireSpallFragments(int count, Vector3 origin, Vector3 direction, float coneAngle, Vector3 projectileVelocity)
        {
            if (count <= 0 || projectileVelocity.magnitude <= 1.0f) return;

            for (int i = 0; i < count; i++)
            {
                // Generate a random direction within a cone using uniform spherical cap distribution
                float z = Random.Range(Mathf.Cos(Mathf.Deg2Rad * coneAngle / 2.0f), 1);
                float phi = Random.Range(0, 2 * Mathf.PI);
                float x = Mathf.Sqrt(1 - z * z) * Mathf.Cos(phi);
                float y = Mathf.Sqrt(1 - z * z) * Mathf.Sin(phi);
                Vector3 localDir = new Vector3(x, y, z);

                Quaternion rotationToConeAxis = Quaternion.LookRotation(direction);
                Vector3 spallDirection = rotationToConeAxis * localDir;
                Quaternion rotation = Quaternion.LookRotation(spallDirection);

                var spall = BulletPool.Instance.GetBullet(data.armorData.spallPrefab, origin, rotation, null);
                if (spall != null)
                {
                    spall.AddIgnoredCollider(gameObject.GetComponent<Collider>());

                    // Spall velocity is a fraction of the penetrating projectile's exit velocity, with some randomness.
                    float spallVelocity = projectileVelocity.magnitude * Random.Range(0.8f, 1.2f);

                    spall.Fire(
                    position: origin,
                    rotation: rotation,
                    Vector3.zero, // Spall velocity is self-contained, do not inherit parent velocity to prevent runaway stacking.
                    data.armorData.spallData.projectileData.muzzleVelocity, // This will be ignored if spallVelocity > 0
                    0f, // Spall direction is already calculated, no need for random deviation
                    data.armorData.spallData,
                    Vector3.zero,
                    Time.time,
                    spallVelocity
                ); // spawn physical spall
                    spall.IsSpallFragment = true; // Mark this bullet as a spall fragment for debugging
                }
            }
        }
    }
}
