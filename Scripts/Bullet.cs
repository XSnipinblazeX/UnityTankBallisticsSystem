using UnityEngine;
using System.Collections.Generic;
using ArmorSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace GNB
{
    public class Bullet : MonoBehaviour
    {
        // Flag to ensure the instantaneous fuse logic is only triggered once per shot.
        private bool fuseTriggered = false;

        public Bullet Prefab;

        [Header("Advanced Ballistics")]
        [Tooltip("The initial spin rate of the projectile in rotations per second (RPS).")]
        public float MuzzleSpinRPS = 2000f;

        [Tooltip("If true, the projectile is stabilized by its shape (like a dart) instead of spin.")]
        public bool FinStabilized = false;

        [Tooltip("The coefficient for the Magnus lift force.")]
        public float MagnusCoefficient = 0.005f;
        [Tooltip("The coefficient for the aerodynamic restoring torque. Higher values mean the projectile corrects its yaw faster.")]
        public float RestoringCoefficient = 0.1f;
        [Tooltip("The coefficient for yaw damping. Higher values mean the spiral motion is dampened faster.")]
        public float DampingCoefficient = 0.5f;
        [Tooltip("The coefficient for spin decay due to air friction. A small value, as spin decays slowly.")]
        public float SpinDecayCoefficient = 0.0001f;
        [Tooltip("The moment of inertia of the projectile, affecting its resistance to angular acceleration.")]
        public float MomentOfInertia = 0.01f;
        [Tooltip("The ratio of restoring torque to gyroscopic stability at which the projectile becomes unstable.")]
        public float StabilityThreshold = 1.0f;
        [Tooltip("Flag to track if the projectile is tumbling end-over-end.")]
        public bool IsTumbling = false;

        
        [Header("Prefabs")]
        [Tooltip("Effect played when the bullet explodes.")]
        [SerializeField] private ParticleSystem ExplodeFXPrefab = null;
        [Tooltip("Any trails listed here will be cleaned up nicely on the bullet's destruction. " +
            "Used to prevent unsightly deleted trails.")]
        [SerializeField] private List<TrailRenderer> ChildTrails = new List<TrailRenderer>();

        [Header("Motion")]
        [Tooltip("Layers the bullet will normally hit")]
        public LayerMask RayHitLayers = -1;
        [Tooltip("How long (seconds) the bullet lasts")]
        public float TimeToLive = 5f;
        [Tooltip("Gravity applied to the bullet where 1 is normal gravity.")]
        public float GravityModifier = 0f;
        [Tooltip("When true, the bullet automatically aligns itself to its velocity. Useful in arcing motions.")]
        public bool AlignToVelocity = false;
        [Tooltip("Length of bullet assuming the origin is the \"tail\" and a BulletLength's distance forwards is the \"head\".")]
        public float BulletLength = 1f;
        [Tooltip("This should be set to true when using physics based projects.")]
        [SerializeField] private bool MoveInFixedUpdate = true;

        [Header("Thick Bullets")]
        [Tooltip("Use thick hit detection for the bullet. This is run in addition to normal hit detection.")]
        public bool IsThick = false;
        [Tooltip("The layers the bullet will hit using thick hit detection.")]
        public LayerMask ThickHitLayers = 0;
        [Tooltip("Used only when thick hit detection is enabled.")]
        public float BulletDiameter = 1f;

        [Header("Explosions")]
        public bool ExplodeOnImpact = false;
        public bool ExplodeOnTimeout = false;


        [Tooltip("The current rotational speed of the projectile in rotations per second (RPS).")]
        public float CurrentRPS = 0f;
        [Tooltip("The current angle between the projectile's forward vector and its velocity vector.")]
        public float CurrentYawAngle = 0f;
        [Tooltip("The maximum yaw angle the projectile has reached during its flight.")]
        public float PeakYawAngle = 0f;

        [Header("Debugging")]
        [Tooltip("Set to true if this bullet is a spall fragment, used for debug drawing.")]
        public bool IsSpallFragment = false;


        private HashSet<Rigidbody> ignoredRigidbodies = new HashSet<Rigidbody>();
        private HashSet<Collider> ignoredColliders = new HashSet<Collider>();

        private static RaycastHit[] raycastHits = new RaycastHit[32];
        public Vector3 Velocity { get; private set; } = Vector3.zero;

        public Vector3 AngularVelocity { get; private set; } = Vector3.zero;

        public float SecondsSinceFired { get; private set; } = 0f;
        public bool IsFired { get; private set; } = false;

        public int SpallFragmentCap { get; set; }
        public float currentMass { get; private set; }

        float spawnTime = 0.0f;
        float ballisticCoef = 0.4f; // Default ballistic coefficient, can be set externally.
        public ProjectileDataCodex codex;

        float distanceTraveled = 0.0f;

        public GameObject visual = null;

        Vector3 spawnPoint;

        ShotInfo me = new ShotInfo();
        
        // Debug UI tracking for first and last impact
        private float _firstImpactTime = -1f;
        private float _firstImpactDistance = -1f;
        private string _firstImpactObjectName = "";

        const float visualScale = 1f / 0.06f;

        // --- DEBUG VISUALIZATION DATA ---
        #if UNITY_EDITOR
        public static List<FireDebugInfo> fireInfos = new List<FireDebugInfo>();
        public static readonly List<ImpactDebugInfo> impactInfos = new List<ImpactDebugInfo>();
        private const float DEBUG_INFO_LIFETIME = 10f;

        public struct FireDebugInfo
        {
            public Vector3 position;
            public string text;
            public float diameter;
            public float timestamp;
        }
        public struct ImpactDebugInfo
        {
            public Vector3 position;
            public float radius;
            public Color color;
            public float timestamp;
            public string text;
        }
        #endif

        public void UpdateShotInfo(ProjectileData data)
        {
            me.projectileName = codex.name;
            me.spawnTime = spawnTime;
            me.muzzleVelocity = codex.projectileData.muzzleVelocity;
            me.caliber = data.d * 1000.0f;
            me.shellType = data.projectileType.ToString();
            me.shellMass = data.m;
            me.spawnPosition = transform.position;
        }



        private void Update()
        {
            if (IsFired && !MoveInFixedUpdate)
                UpdateBullet(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (IsFired && MoveInFixedUpdate)
                UpdateBullet(Time.fixedDeltaTime);
        }
        public void LogEvent(string eventType, string message)
        {
            Debug.Log($"[{eventType}] {message}");
        }

        private void LogShotResult(ShotResult result)
        {
            Debug.Log($"SHOT RESULT for {result.projectileName} hitting {result.hitArmor}:\nPenetration: {result.penetration:F1}mm vs {result.effectiveThickness:F1}mm | Penetrated: {result.penetrated}, Exit Velo: {result.exitVelocity.magnitude:F1}m/s, Spalled: {result.HasSpalled}, Fused: {result.Fused}");
        }

        /// <param name="position">Position the bullet will start at.</param>
        /// <param name="rotation">Rotation the bullet will start at.</param>
        /// <param name="inheritedVelocity">Any extra velocity to add to the bullet that it might
        /// be inheriting from its firer.</param>
        /// <param name="muzzleVelocity">Starting forward velocity of the bullet.</param>
        /// <param name="deviation">Maximum random deviation in degrees to apply to the bullet.</param>
        public void Fire(Vector3 position, Quaternion rotation, Vector3 inheritedVelocity, float muzzleVelocity, float deviation, ProjectileDataCodex _codex, Vector3 _angular, float _spawnTime, float overrideVelocity = 0.0f, float lifetimeOverride = 0.0f)
        {
            // Reset fuse state on fire
            fuseTriggered = false;

            // Determine if this is a spall fragment BEFORE any logging.
            // A projectile is considered spall if its codex refers to itself as its own spall data.
            // This is a self-referential check to identify codices specifically designed for spall.
            IsSpallFragment = (_codex.projectileData.spallData != null && _codex.name == _codex.projectileData.spallData.name) ||
                              _codex.name.Equals("spall", System.StringComparison.OrdinalIgnoreCase) ||
                              gameObject.name.Equals("spall", System.StringComparison.OrdinalIgnoreCase);

            // Initialize the spall fragment cap based on caliber.
            SpallFragmentCap = Mathf.CeilToInt(_codex.projectileData.d * 1000f);

            currentMass = _codex.projectileData.m; // Initialize current mass from the codex

            previousPenetration = 0f;
            isInsideCompartment = false;

            // If a lifetime override is provided, use it. Otherwise, use the default from the prefab.
            if (lifetimeOverride > 0)
            {
                this.TimeToLive = lifetimeOverride;
            }
            //LogEvent("Bullet Spawned", $"Bullet spawned at {position}, with muzzle velocity of {muzzleVelocity}m/s");
            spawnTime = _spawnTime;
            Prefab = this;

            // Reset debug UI tracking for a new bullet
            _firstImpactTime = -1f;
            _firstImpactDistance = -1f;
            _firstImpactObjectName = "";
            // Start position.
            spawnPoint = position;
            transform.position = position;
            codex = _codex;
            ballisticCoef = codex.projectileData.ballisticCoefficient;
            FinStabilized = codex.projectileData.FinStabilized;
            // Set the initial spin rate and spin axis
            MomentOfInertia = codex.projectileData.MomentOfInertia;
            MagnusCoefficient = codex.projectileData.MagnusCoefficient;
            SpinDecayCoefficient = codex.projectileData.SpinDecayCoefficient;
            RestoringCoefficient = codex.projectileData.RestoringCoefficient;
            StabilityThreshold = codex.projectileData.StabilityThreshold;
            DampingCoefficient = codex.projectileData.DampingCoefficient;
            MuzzleSpinRPS = codex.projectileData.MuzzleSpinRPS;
            Vector3 deviationAngle = Vector3.zero;
            deviationAngle.x = Random.Range(-deviation, deviation);
            deviationAngle.y = Random.Range(-deviation, deviation);
            Quaternion deviationRotation = Quaternion.Euler(deviationAngle);
            transform.rotation = rotation * deviationRotation;
            // Reset tumbling state
            IsTumbling = false;
            if (_angular != Vector3.zero)
                AngularVelocity = _angular;
            else
            {
                if (MuzzleSpinRPS > 0)
                {
                    AngularVelocity = transform.forward * MuzzleSpinRPS * 2f * Mathf.PI;
                }
                else
                {
                    AngularVelocity = Vector3.zero;
                   // LogEvent("Bullet Notification", $"{gameObject.name} has spawned with no spin, if this is not fin stabilized please check the data asset");
                }
            }
            // Calculate a random deviation.


            // Rotate the bullet to the direction requested, plus some random deviation.
            

            // Use overrideVelocity if it's provided (e.g., for spall fragments), otherwise use standard muzzle velocity.
            float finalMuzzleVelocity = (overrideVelocity > 0.0f) ? overrideVelocity : muzzleVelocity;
            Velocity = (transform.forward * finalMuzzleVelocity) + inheritedVelocity;

            //set up visualmesh
            if (visual == null && _codex.projectileData.VisualMesh != null)
            {
                visual = VisualPool.Instance.GetVisual(codex.projectileData.VisualMesh);

                if (visual != null)
                {
                    // If we got a visual, parent it and reset its transform.
                    visual.transform.SetParent(this.transform);
                    Vector3 scale = new Vector3((_codex.projectileData.d) * visualScale, (_codex.projectileData.d) * visualScale, (_codex.projectileData.d) * visualScale);
                    visual.transform.localScale = scale;
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    Tracer tracer = visual.GetComponentInChildren<Tracer>();
                    if (tracer != null)
                    {
                        if (_codex.projectileData.tracerColor != null)
                        {
                            tracer.ApplyColorPreset(_codex.projectileData.tracerColor);
                        }
                        tracer.Fired();
                    }
                }
                else {
                    LogEvent("Pool Warning", "Visual pool is full! Bullet will be spawned without visuals.");
                }
            }



            IsFired = true;

            UpdateShotInfo(codex.projectileData);

            #if UNITY_EDITOR
            if (!IsSpallFragment)
            {
                string fireData = $"Caliber: {codex.projectileData.d * 1000f:F1}mm\n" +
                                  $"Muzzle Velo: {Velocity.magnitude:F1}m/s\n" +
                                  $"Mass: {codex.projectileData.m:F2}kg";
                fireInfos.Add(new FireDebugInfo { position = transform.position, text = fireData, diameter = codex.projectileData.d, timestamp = Time.time });
            }
            #endif

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ShotDebugUI.Instance != null && !IsSpallFragment)
            {
                ShotDebugUI.Instance.StartNewShotLog(
                    codex.name,
                    finalMuzzleVelocity,
                    codex.projectileData.d * 1000f,
                    currentMass);
            }
            #endif
            
        }




        public Vector3 MagnusAcceleration()
        {
            if (AngularVelocity.sqrMagnitude < 0.01f)
                return Vector3.zero;

            // Get wind from the WorldSystem to calculate relative velocity
            Vector3 wind = (WorldSystem.Instance != null) ? WorldSystem.Instance.Wind : Vector3.zero;
            Vector3 relativeVelocity = Velocity - wind;

            // The Magnus force is perpendicular to the relative velocity and spin axis
            Vector3 crossProduct = Vector3.Cross(AngularVelocity, relativeVelocity);

            // Calculate Magnus acceleration
            // The magnitude is proportional to the square of the relative velocity.
            // The MagnusCoefficient is a simplified term representing the lift characteristics.
            // For full realism, this would also factor in air density.
            Vector3 magnusAcceleration = crossProduct.normalized * (MagnusCoefficient * relativeVelocity.sqrMagnitude);

            return magnusAcceleration;
        }

        /// <summary>
        /// A static version of MagnusAcceleration for use in prediction simulations.
        /// </summary>
        public static Vector3 CalculateMagnusAcceleration(Vector3 angularVelocity, Vector3 velocity, float magnusCoefficient)
        {
            // This static version assumes the provided 'velocity' is the RELATIVE velocity to the air.
            if (angularVelocity.sqrMagnitude < 0.01f) return Vector3.zero;

            Vector3 crossProduct = Vector3.Cross(angularVelocity, velocity);
            // The magnus coefficient is used to approximate the combined terms in the lift force equation.
            Vector3 magnusAcceleration = crossProduct.normalized * (magnusCoefficient * velocity.sqrMagnitude);

            return magnusAcceleration;
        }

        /// <summary>
        /// A static, self-contained acceleration calculation for prediction simulations.
        /// </summary>
        public static Vector3 CalculatePredictionAcceleration(ProjectileDataCodex codex, Vector3 velocity, Vector3 angularVelocity, float gravityModifier)
        {
            if (codex == null || codex.projectileData == null) return Physics.gravity * gravityModifier;

            // Get world conditions from the WorldSystem singleton
            float airDensity = (WorldSystem.Instance != null) ? WorldSystem.Instance.AirDensity : 1.225f;
            Vector3 wind = (WorldSystem.Instance != null) ? WorldSystem.Instance.Wind : Vector3.zero;

            // Calculate relative velocity for drag and magnus effects
            Vector3 relativeVelocity = velocity - wind;

            // Magnus Effect
            Vector3 magnusEffect = CalculateMagnusAcceleration(angularVelocity, relativeVelocity, codex.projectileData.MagnusCoefficient);

            // Drag
            float ballisticCoef = codex.projectileData.ballisticCoefficient;
            float currentMass = codex.projectileData.m;
            float diameter = codex.projectileData.d;
            Vector3 dragAccel = ballisticCoef * (Mathf.PI * Mathf.Pow(diameter / 2f, 2f)) * airDensity / currentMass * -1f * (relativeVelocity.normalized * relativeVelocity.sqrMagnitude);

            return (Physics.gravity * gravityModifier) + dragAccel + magnusEffect;
        }

        public Vector3 SpinDecayTorque(float deltaTime)
        {
            if (AngularVelocity.sqrMagnitude < 0.01f)
                return Vector3.zero;

            // The torque is opposing the spin, causing it to slow down.
            // This is a simplified model of air friction on the spinning projectile.
            Vector3 spinDecayTorque = -AngularVelocity.normalized * (SpinDecayCoefficient * AngularVelocity.sqrMagnitude);

            return spinDecayTorque;
        }


        /// <summary>
        /// Calculates the motion of the bullet using Verlet Integration.
        /// Returns a tuple of the resulting position and velocity.
        /// </summary>
        /// <param name="deltaTime">The time to simulate forwards.</param>
        public Vector3 _acceleration = Vector3.zero;
        public Vector3 calculateAcceleration(ProjectileDataCodex codex, Vector3 velocity)
        {
            // Get world conditions from the WorldSystem singleton
            float airDensity = (WorldSystem.Instance != null) ? WorldSystem.Instance.AirDensity : 1.225f;
            Vector3 wind = (WorldSystem.Instance != null) ? WorldSystem.Instance.Wind : Vector3.zero;

            // Calculate relative velocity for drag and magnus effects
            Vector3 relativeVelocity = velocity - wind;

            // Calculate individual acceleration components
            Vector3 magnusEffect = MagnusAcceleration();
            Vector3 dragAccel = ballisticCoef * (Mathf.PI * Mathf.Pow((codex.projectileData.d / 2f), 2f)) * airDensity / currentMass * -1f * (relativeVelocity.normalized * relativeVelocity.sqrMagnitude);
            
            // Combine all forces: gravity, drag, and magnus effect
            Vector3 acceleration = (Physics.gravity * GravityModifier) + dragAccel + magnusEffect;
            return acceleration;
        }

        public (Vector3 position, Vector3 velocity) CalculateBulletMotion(Vector3 currentPosition, Vector3 currentVelocity, float deltaTime, Vector3 currentAcceleration)
        {
            // --- Velocity Verlet Integration ---

            // 1. Update position based on current velocity and acceleration.
            // x(t + dt) = x(t) + v(t) * dt + 0.5 * a(t) * dt^2
            Vector3 nextPosition = currentPosition + currentVelocity * deltaTime + 0.5f * currentAcceleration * deltaTime * deltaTime;

            // 2. Calculate the acceleration at the new position/velocity.
            // Since our acceleration depends on velocity, we first predict the new velocity
            // to get the new acceleration.
            // v_intermediate = v(t) + a(t) * dt
            Vector3 intermediateVelocity = currentVelocity + currentAcceleration * deltaTime;
            Vector3 nextAcceleration = calculateAcceleration(codex, intermediateVelocity);

            // 3. Update velocity using the average of the old and new acceleration.
            // v(t + dt) = v(t) + 0.5 * (a(t) + a(t + dt)) * dt
            Vector3 nextVelocity = currentVelocity + 0.5f * (currentAcceleration + nextAcceleration) * deltaTime;

            _acceleration = nextAcceleration; // for debugging

            return (nextPosition, nextVelocity);
        }

        /// <summary>
        /// A static, self-contained motion calculation for prediction simulations.
        /// </summary>
        public static (Vector3 position, Vector3 velocity) CalculatePredictionMotion(Vector3 currentPosition, Vector3 currentVelocity, Vector3 currentAngularVelocity, float deltaTime, ProjectileDataCodex codex, float gravityModifier)
        {
            // --- Velocity Verlet Integration ---
            Vector3 currentAcceleration = CalculatePredictionAcceleration(codex, currentVelocity, currentAngularVelocity, gravityModifier);

            // 1. Update position
            Vector3 nextPosition = currentPosition + currentVelocity * deltaTime + 0.5f * currentAcceleration * deltaTime * deltaTime;

            // 2. Predict next acceleration
            Vector3 intermediateVelocity = currentVelocity + currentAcceleration * deltaTime;
            Vector3 nextAcceleration = CalculatePredictionAcceleration(codex, intermediateVelocity, currentAngularVelocity, gravityModifier);

            // 3. Update velocity
            Vector3 nextVelocity = currentVelocity + 0.5f * (currentAcceleration + nextAcceleration) * deltaTime;

            return (nextPosition, nextVelocity);
        }

        // Overload for compatibility with Gun.cs and other scripts expecting 3 arguments.
        public (Vector3 position, Vector3 velocity) CalculateBulletMotion(Vector3 currentPosition, Vector3 currentVelocity, float deltaTime)
        {
            Vector3 currentAcceleration = calculateAcceleration(codex, currentVelocity);
            return CalculateBulletMotion(currentPosition, currentVelocity, deltaTime, currentAcceleration);
        }

        // Overload for prediction, allowing an external codex to be used.
        public (Vector3 position, Vector3 velocity) CalculateBulletMotion(Vector3 currentPosition, Vector3 currentVelocity, float deltaTime, ProjectileDataCodex overrideCodex)
        {
            Vector3 currentAcceleration = calculateAcceleration(overrideCodex, currentVelocity);
            return CalculateBulletMotion(currentPosition, currentVelocity, deltaTime, currentAcceleration);
        }

        // Overload for compatibility with legacy scripts like Bullet_Generator_CS expecting 5 arguments (original Verlet).
        public (Vector3 position, Vector3 velocity) CalculateBulletMotion(Vector3 currentPosition, Vector3 previousPosition, float deltaTime, ProjectileDataCodex over = null, Vector3 overrideVelocity = new Vector3())
        {
            // Adapt the old Verlet-style call to the new Velocity Verlet method.
            Vector3 currentVelocity = (currentPosition - previousPosition) / deltaTime;
            Vector3 currentAcceleration = calculateAcceleration(over ?? codex, overrideVelocity != Vector3.zero ? overrideVelocity : currentVelocity);
            return CalculateBulletMotion(currentPosition, currentVelocity, deltaTime, currentAcceleration);
        }

        /// <summary>
        /// Runs hit detection by projecting if the bullet will hit something when it moves this frame.
        /// </summary>
        /// <param name="position">Position of the bullet right now</param>
        /// <param name="velocity">Velocity of the bullet right now</param>
        /// <param name="deltaTime">Expected frame time for the bullet to move in</param>
        public (bool hitSomething, RaycastHit hitInfo) RunHitDetection(Vector3 position, Vector3 velocity, float deltaTime)
        {
            return IsThick
                ? RunThickHitDetection(position, velocity, deltaTime)
                : RunRayHitDetection(position, velocity, deltaTime);
        }

        /// <summary>
        /// Prevents collision with any colliders owned by this Rigidbody. A common use for this is
        /// to prevent a gun from shooting its owner. Prefer using this to ignore objects when possible.
        /// </summary>
        public void AddIgnoredRigidbody(Rigidbody rigidbody)
        {
            if (rigidbody != null)
                ignoredRigidbodies.Add(rigidbody);
        }

        /// <summary>
        /// Prevents collision with any colliders owned by the Rigidbodies in this list. A common use
        /// for this is to prevent a gun from shooting its owner. Prefer using this to ignore objects
        /// when possible.
        /// </summary>
        /// <param name="rigidbodies"></param>
        public void AddIgnoredRigidbodies(IEnumerable<Rigidbody> rigidbodies)
        {
            foreach (var rigidbody in rigidbodies)
                ignoredRigidbodies.Add(rigidbody);
        }

        /// <summary>
        /// Prevents collision the given collider. Commonly used to prevent a gun from shooting its
        /// owner. When possible, prefer <see cref="AddIgnoredRigidbody(Rigidbody)"/> rather than
        /// naming individual colliders.
        /// </summary>
        public void AddIgnoredCollider(Collider collider)
        {
            if (collider != null)
                ignoredColliders.Add(collider);
        }

        /// <summary>
        /// Prevents collision the given colliders. Commonly used to prevent a gun from shooting its
        /// owner. When possible, prefer <see cref="AddIgnoredRigidbody(Rigidbody)"/> rather than
        /// naming individual colliders.
        /// </summary>
        public void AddIgnoredColliders(IEnumerable<Collider> colliders)
        {
            foreach (var collider in colliders)
                ignoredColliders.Add(collider);
        }

        /// <summary>
        /// Explodes the bullet. Typically used for air bursting explosive weapons.
        /// </summary>
        public void ExplodeBullet(Vector3 explodePosition, Quaternion explodeRotation, bool generateFragments = true)
        {
            if (ExplodeFXPrefab != null)
                Instantiate(ExplodeFXPrefab, explodePosition, explodeRotation).Play();

            if (generateFragments)
            {
                int fragments = 16;
                if (codex.projectileData.penetratorType == ProjectileData.PenetratorType.CH)
                {
                    SpawnPhysicalSpall(fragments, explodePosition, -Velocity.normalized);
                    FireSpallFragments(fragments, explodePosition, -Velocity.normalized, 75, 400f);
                }
                else
                {
                    SpawnPhysicalSpall(fragments, explodePosition, Velocity.normalized);
                    FireSpallFragments(fragments, explodePosition, Velocity.normalized, 75f, 400f);
                }
            }
            else
            {
                LogEvent("Explosion Event", "Explosion contained within armor, no fragments generated.");
            }

            //R=k×W ^1/3
            float radius = 3.66f * Mathf.Pow(codex.projectileData.ExplosiveMassKG, (1f / 3f)); //1kg of TNT will rupture lungs at 3.66 meters
            HandleExplosionDamage(explodePosition, radius);

            #if UNITY_EDITOR
            // Add debug visualization for the explosion
            if (!IsSpallFragment)
            {
                string detoText = $"Detonated\nExplosive Mass: {codex.projectileData.ExplosiveMassKG:F2}kg";
                impactInfos.Add(new ImpactDebugInfo { position = explodePosition, radius = radius, color = Color.magenta, timestamp = Time.time, text = detoText });
            }
            #endif

            CleanUpTrails();
            DestroyBulletSilently(); // This already handles returning the bullet to the pool.
        }

        /// <summary>
        /// Destroys the bullet as if it hit something.
        /// </summary>
        public void DestroyBulletFromImpact(RaycastHit hitInfo)
        {
            // Check for an ImpactMaterial component on the hit object to spawn effects.
            ImpactMaterial impactMaterial = hitInfo.collider.GetComponentInParent<ImpactMaterial>(); 
            if (impactMaterial != null) 
            {
                float currentKineticEnergy = 0.5f * currentMass * Velocity.sqrMagnitude;
                float impactScaleMultiplier = CalculateImpactScale(currentKineticEnergy, codex.projectileData.d);
                impactMaterial.HandleImpact(hitInfo.point, hitInfo.normal, impactScaleMultiplier);
            }

            CleanUpTrails();
            DestroyBulletSilently();
        }

        /// <summary>
        /// Calculates a dynamic scale multiplier for impact effects based on projectile energy and size.
        /// </summary>
        /// <param name="kineticEnergy">The kinetic energy of the impact in Joules.</param>
        /// <param name="diameter">The diameter of the projectile in meters.</param>
        /// <returns>A scale multiplier for the impact effect.</returns>
        public static float CalculateImpactScale(float kineticEnergy, float diameter)
        {
            // Baseline values for a "standard" impact effect scale of 1.0
            const float baselineKineticEnergy = 6318000f; // Joules (from a 15.6kg shell traveling at 900m/s)

            // --- Energy Factor ---
            // The visual *area* of the effect should scale with energy, so we use Sqrt for the scale multiplier.
            // This means if the energy is 4x the baseline, the effect will be 2x the size.
            float impactScaleMultiplier = Mathf.Sqrt(kineticEnergy / baselineKineticEnergy);
            
            // Clamp to prevent excessively small or large effects.
            return Mathf.Clamp(impactScaleMultiplier, 0.1f, 10f);
        }

        /// <summary>
        /// Handles the visual impact effects for a non-physical spall fragment.
        /// This is separate from DestroyBulletFromImpact to use spall-specific properties for scaling.
        /// </summary>

        public Vector3 CalculateRicochetDirection(Vector3 incomingDirection, Vector3 surfaceNormal)
        {
            // Using Vector3.Reflect to get the new direction of the projectile.
            // The normal of the surface is critical for this calculation.
            return Vector3.Reflect(incomingDirection, surfaceNormal);
        }

        public float CalculateYawFactor(float yawAngle)
        {
            // This new model handles tumbling projectiles, like long-rod penetrators.
            // A hit at 0 degrees (nose-first) or 180 degrees (tail-first) presents the smallest
            // cross-section and thus has the highest penetration multiplier (1.0).
            // A hit at 90 degrees (side-on) presents the largest cross-section and has the
            // lowest penetration multiplier (0.0).

            // We use a cosine wave that completes a full cycle over 180 degrees.
            // Cos(2 * angle) gives a value from 1 (at 0 deg) to -1 (at 90 deg) and back to 1 (at 180 deg).
            float cosValue = Mathf.Cos(2f * yawAngle * Mathf.Deg2Rad);

            // We map the range [-1, 1] to [0, 1] to get our penetration factor.
            return (cosValue + 1f) / 2f;
        }

        /// <summary>
        /// Destroys the bullet with no effect.
        /// </summary>
        public void DestroyBulletSilently()
        {
            IsFired = false;
            CleanUpTrails();
            BulletPool.Instance.ReturnBullet(this);
        }

        private void MoveBulletFor(float deltaTime)
        {
            // Bullet continues motion.
            Vector3 currentPosition = transform.position;
            // For Velocity Verlet, we need the acceleration from the beginning of the step.
            Vector3 currentAcceleration = calculateAcceleration(codex, Velocity);

            var (nextPosition, nextVelocity) = CalculateBulletMotion(currentPosition, Velocity, deltaTime, currentAcceleration);

            // Draw debug line for the bullet's path
            Color debugColor = IsSpallFragment ? Color.yellow : Color.green;
            Debug.DrawLine(currentPosition, nextPosition, debugColor, 5f);
            transform.position = nextPosition;
            Velocity = nextVelocity;

            // Apply spin effects
            if (FinStabilized)
                FinStabilization(deltaTime); //we stabilize with fin
            else
                SpinStabilization(deltaTime); //we stabilize with spin
                                              // ===== UPDATE DEBUG VALUES =====
                                              // Update CurrentRPS
            CurrentRPS = Vector3.Dot(AngularVelocity, transform.forward) / (2f * Mathf.PI);

            // Update CurrentYawAngle
            if (Velocity.sqrMagnitude > 0.01f)
            {
                CurrentYawAngle = Vector3.Angle(transform.forward, Velocity);
            }

            // Update PeakYawAngle
            if (CurrentYawAngle > PeakYawAngle)
            {
                PeakYawAngle = CurrentYawAngle;
            }

            if (!IsTumbling && PeakYawAngle > 45.0f)
            {
                LogEvent("Bullet Event", $"{gameObject.name} has started to tumble at {distanceTraveled} meters from last known initialization point");
                IsTumbling = true;
                if (codex.projectileData.penetratorType == ProjectileData.PenetratorType.JET)
                {
                    LogEvent("Bullet Event", $"Shaped charge jet has been broken up");
                    DestroyBulletSilently();
                }
                TimeToLive *= 0.3f;
            }

            // Apply rotation based on stability
            if (IsTumbling)
            {
                // When tumbling, apply the full, chaotic angular velocity.
                Vector3 deltaR = AngularVelocity * deltaTime * Mathf.Rad2Deg;
                if (deltaR.sqrMagnitude > 0.001f) // Avoid creating invalid quaternions from zero vectors
                {
                    Quaternion deltaRotation = Quaternion.AngleAxis(deltaR.magnitude, deltaR.normalized);
                    transform.rotation *= deltaRotation;
                }
            }
            else
            {
                // When stable, align perfectly with the velocity vector.
                if (Velocity.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(Velocity.normalized, transform.up);
                }

                // Then, apply only the spin component around that axis.
                float spinRate = Vector3.Dot(AngularVelocity, Velocity.normalized);
                Quaternion spinRotation = Quaternion.AngleAxis(spinRate * Mathf.Rad2Deg * deltaTime, Velocity.normalized);
                transform.rotation = spinRotation * transform.rotation;
            }

            updateBallisticCoefficient();
        }

         private void UpdateBullet(float deltaTime)
        {
            // --- 1. Liveliness Check ---
            SecondsSinceFired += deltaTime;
            distanceTraveled += (Velocity.magnitude) * deltaTime;

            if (SecondsSinceFired > TimeToLive || Velocity.sqrMagnitude < 0.01f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ShotDebugUI.Instance != null && !IsSpallFragment && _firstImpactTime == -1f)
                {
                    _firstImpactTime = SecondsSinceFired; // Mark that an outcome has been logged
                    float distance = Vector3.Distance(spawnPoint, transform.position);
                    ShotDebugUI.Instance.AddLogEntry($"<color=grey>Timeout</color> @ {distance:F1}m after {SecondsSinceFired:F2}s");
                }
#endif
                if (ExplodeOnTimeout)
                    ExplodeBullet(transform.position, transform.rotation);
                else
                    DestroyBulletSilently();
                return; // Return, as bullet is no longer valid.
            }

            // --- 3. Iterative Hit Detection & Movement Loop ---
            float timeRemaining = deltaTime;
            for (int i = 0; i < 5; i++) // Max 5 iterations to prevent infinite loops
            {
                var (hitSomething, hitInfo) = RunHitDetection(transform.position, Velocity, timeRemaining);

                if (hitSomething)
                {
                    float travelDistance = hitInfo.distance;
                    float timeToImpact = (Velocity.magnitude > 0.001f) ? travelDistance / Velocity.magnitude : timeRemaining;
                    
                    // Clamp time to avoid overshooting
                    if (timeToImpact > timeRemaining) {
                        timeToImpact = timeRemaining;
                    }

                    // Move bullet for the partial time step until impact
                    MoveBulletFor(timeToImpact);

                    // Handle the impact itself
                    HandleImpactDamage(hitInfo, timeToImpact);
                    AddIgnoredCollider(hitInfo.collider);
                    // If bullet was destroyed, stop everything
                    if (!IsFired)
                    {
                        return;
                    }

                    timeRemaining -= timeToImpact - 0.0001f;
                    if (timeRemaining < 0.0001f)
                    {
                        
                        break; // No time left in this frame
                    }
                }
                else
                {
                    // No hit, move for the rest of the time and exit the loop
                    MoveBulletFor(timeRemaining);
                    break;
                }
            }
        }

        void updateBallisticCoefficient()
        {
            float mach = Velocity.magnitude / 343.3f; //approximate mach
            float multiplier = 1f;
            //get G1 multiplier
            if (codex.projectileData.dragModel == ProjectileData.DragModel.G1)
            {
                multiplier = ExternalBallistics.G1.GetMultiplier(mach);
            }
            if (codex.projectileData.dragModel == ProjectileData.DragModel.G7)
            {
                multiplier = ExternalBallistics.G7.GetMultiplier(mach);
            }
            if (codex.projectileData.dragModel == ProjectileData.DragModel.OTHER)
            {
                multiplier = ExternalBallistics.OTHER.GetMultiplier(mach);
            }
            ballisticCoef /= multiplier;
        }



        void FinStabilization(float deltaTime)
        {
            Vector3 spinAxis = transform.forward;
            Vector3 velocityDirection = Velocity.normalized;
            
            Vector3 restoringTorque = Vector3.Cross(spinAxis, velocityDirection) * RestoringCoefficient * Velocity.sqrMagnitude;
            Vector3 dampingTorque = -AngularVelocity * DampingCoefficient * Velocity.magnitude;
            
            Vector3 totalTorque = restoringTorque + dampingTorque;
            
            Vector3 angularAcceleration = totalTorque / MomentOfInertia;
            AngularVelocity += angularAcceleration * deltaTime;
        }


        private void FireSpallFragments(int count, Vector3 origin, Vector3 direction, float coneAngle, float velocity, int previousBounces = 0) //this handles non-physical spall
        {
            if (count <= 0 || velocity <= 1.0f) return;

            Collider[] ignoreCollisions = GetRadiusColliders(origin, codex.projectileData.d * 2f);

            bool SpawnPhysicalFirst = count < 15 ? true : false;
            if (SpawnPhysicalFirst)
            {
                // Generate a random direction within a cone using uniform spherical cap distribution
                float z = Random.Range(Mathf.Cos(Mathf.Deg2Rad * coneAngle / 2.0f), 1);
                float phi = Random.Range(0, 2 * Mathf.PI);
                float x = Mathf.Sqrt(1 - z * z) * Mathf.Cos(phi);
                float y = Mathf.Sqrt(1 - z * z) * Mathf.Sin(phi);
                Vector3 localDir = new Vector3(x, y, z);

                Quaternion rotationToConeAxis = Quaternion.LookRotation(direction);
                Vector3 spallDirection = rotationToConeAxis * localDir;

                float maxDistance = velocity * 0.02f;
                if (Physics.Raycast(origin, spallDirection, out RaycastHit hit, maxDistance))
                {
                    LogEvent("Impact Event",$"Spall fragment hit {hit.collider.name}");
                    
                    // --- UNIFIED FX LOGIC ---
                    // This logic is now inlined to ensure non-physical spall uses the same scaling as physical projectiles.
                    ImpactMaterial impactMaterial = hit.collider.GetComponentInParent<ImpactMaterial>();
                    if (impactMaterial != null)
                    {
                        ProjectileData spallData = codex.projectileData.spallData.projectileData;
                        float spallKineticEnergy = 0.5f * spallData.m * (velocity * velocity);
                        float impactScaleMultiplier = CalculateImpactScale(spallKineticEnergy, spallData.d);
                        impactMaterial.HandleImpact(hit.point, hit.normal, impactScaleMultiplier);
                    }
                    // --- END UNIFIED FX LOGIC ---

                    if (hit.collider.gameObject.GetComponent<Compartment>()) //hit a compartment
                    {
                        Compartment hitCompartment = hit.collider.gameObject.GetComponent<Compartment>();
                        LogEvent("Impact Result", $"Bullet entered {hitCompartment.label} compartment");
                        AddIgnoredCollider(hit.collider);
                        float explosiveEquivalent = hitCompartment.CalculateTNTEquivalent(0.006f, maxDistance);
                        hitCompartment.Explosion(explosiveEquivalent, 0.5f);
                        return;
                    }
                    if (hit.collider.gameObject.GetComponent<Human>()) //we hit a human
                    {
                        float damage = 0.5f * 0.006f * maxDistance * maxDistance;
                        Human human = hit.collider.gameObject.GetComponent<Human>();
                        human.OnHit(damage, hit.point, spallDirection);
                        return;
                    }
                    if (hit.collider.gameObject.GetComponent<Module>())
                    {
                        float damage = 0.5f * 0.006f * maxDistance * maxDistance;
                        Module module = hit.collider.gameObject.GetComponent<Module>();
                        module.Damage(damage);
                        if (!module.StopSpall)
                        {
                            FireSpallFragments(1, hit.point, spallDirection, Random.Range(15f, 60f), Mathf.Abs(velocity * Vector3.Dot(spallDirection, hit.normal) * 0.5f), 0);
                        }
                    }

                    Debug.DrawLine(origin, hit.point, Color.yellow, 5f, true);
                    if (Vector3.Angle(spallDirection, hit.normal) >= (60.0f * Random.Range(0.75f, 1.0f)))
                    {
                        //spall has ricocheted
                        if (previousBounces >= 1)
                        {
                            return;
                        }
                        else
                        {
                            int newBounce = previousBounces + 1;
                            FireSpallFragments(1, hit.point, Vector3.Reflect(spallDirection, hit.normal), Random.Range(15f, 60f), Mathf.Abs(velocity * Vector3.Dot(spallDirection, hit.normal) * 0.5f), newBounce);
                            LogEvent("Spall event","Spall ricocheted");
                        }
                    }

                }
                else // we didnt hit anything
                Debug.DrawRay(origin, spallDirection * Vector3.Distance(origin, hit.point), Color.yellow, 5f);
            }
        }



        public Vector3 SpinStabilization(float deltaTime, bool returnTorque = false)
        {
            // Convert world-space vectors to the bullet's local space for calculation.
            Vector3 localVelocity = transform.InverseTransformDirection(Velocity);
            Vector3 localAngularVelocity = transform.InverseTransformDirection(AngularVelocity);

            // In local space, the spin axis is always Vector3.forward (local Z).
            Vector3 localSpinAxis = Vector3.forward;
            Vector3 localVelocityDirection = localVelocity.normalized;

            // Calculate torques in local space.
            Vector3 crossProduct = Vector3.Cross(localSpinAxis, localVelocityDirection);
            Vector3 restoringTorque = -crossProduct * RestoringCoefficient * localVelocity.sqrMagnitude;

            // Spin rate is the Z component of the local angular velocity.
            float spinRate = localAngularVelocity.z;
            float gyroscopicResistance = MomentOfInertia * Mathf.Abs(spinRate);

            if (!IsTumbling && restoringTorque.magnitude > gyroscopicResistance * StabilityThreshold)
            {
                IsTumbling = true;
                LogEvent("Bullet Event", $"Projectile began tumbling due to instability. Gyroscopic resistance: {gyroscopicResistance}");
            }

            if (IsTumbling)
            {
                RestoringCoefficient = Random.Range(1f, 5f) * 0.003f;
            }

            // Simplified spin decay applied directly to the local spin component.
            localAngularVelocity.z *= (1.0f - SpinDecayCoefficient * deltaTime);

            Vector3 angularAcceleration = restoringTorque / MomentOfInertia;
            localAngularVelocity += angularAcceleration * deltaTime;

            if (returnTorque)
            {
                return transform.TransformDirection(localAngularVelocity);
            }
            else
            {
                AngularVelocity = transform.TransformDirection(localAngularVelocity);
            }
            return AngularVelocity;
        }


        float previousPenetration = 0f; //reduce penetration of subsequent plates
        bool isInsideCompartment = false;
        private void HandleImpactDamage(RaycastHit hitInfo, float deltaTime)
        {            


            // --- GENERIC IMPACT FX ---
            // Check for an ImpactMaterial component on ANY hit object at the start.
            ImpactMaterial impactMaterial = hitInfo.collider.GetComponentInParent<ImpactMaterial>();
            if (impactMaterial != null)
            {
                // Calculate the kinetic energy of the current impact
                float currentKineticEnergy = 0.5f * currentMass * Velocity.sqrMagnitude;
                // Use the unified scaling logic
                float impactScaleMultiplier = CalculateImpactScale(currentKineticEnergy, codex.projectileData.d);

                LogEvent("Impact FX", $"Found ImpactMaterial on '{impactMaterial.gameObject.name}'. KE: {currentKineticEnergy / 1000000f:F2}MJ, Cal: {codex.projectileData.d * 1000f:F0}mm. Spawning custom effects with scale {impactScaleMultiplier:F2}.");

                impactMaterial.HandleImpact(hitInfo.point, hitInfo.normal, impactScaleMultiplier);
            }

            //set up the logs
            ShotResult result = new ShotResult();
            result.projectileName = codex.name;
            result.impactVelocity = Velocity;
            result.impactTime = spawnTime + SecondsSinceFired;
            result.impactAngle = Vector3.Angle(hitInfo.normal, Velocity.normalized);
            result.impactLocation = hitInfo.point;
            result.projectileType = codex.projectileData.penetratorType.ToString();

            result.fragmented = false;
            result.timeOfFragment = 0.0f;

            //hit object properies
            result.hitArmor = "n/a";
            result.thickness = 0.0f;
            result.HasSpalled = false;
            result.timeOfSpall = 0.0f;
            

            result.KineticEnergy = 0.5f * currentMass * Velocity.sqrMagnitude;
            result.penetration = 0.0f;
            result.effectiveThickness = 0.0f;

            result.penetrated = false;
            //result.exitVelocity;
            result.Fused = false;
            result.timeOfFuse = 0.0f;
            result.fusedDistance = 0.0f;

            result.Detonated = false;
            result.timeOfDetonation = 0.0f;
            //result.detonationPoint;

            // Helper function to log the final shot data to the UI
            void LogFinalShotDataToUI(string outcome)
            {
                if (ShotDebugUI.Instance != null && !IsSpallFragment && _firstImpactTime == -1f)
                {
                    _firstImpactTime = Time.time - spawnTime;
                    _firstImpactDistance = Vector3.Distance(spawnPoint, hitInfo.point);
                    _firstImpactObjectName = hitInfo.collider.name;
                    ShotDebugUI.Instance.AddLogEntry($"<b>Outcome: <color=lightblue>{outcome}</color></b> @ {_firstImpactDistance:F1}m on {_firstImpactObjectName}");
                }
            }


            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string impactText = ""; // To be filled in by specific impact logic
#endif

            // Helper function to build the debug string
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string BuildImpactDebugText(float impactVelo, float impactAngle, float penetration, float plateThickness, float effectiveThickness, float yaw, float shatterChance, string impactResult, bool spalled)
            {
                return $"V: {impactVelo:F0} | A: {impactAngle:F1}°\n" +
                       $"P: {penetration:F0} | pT: {plateThickness:F0}mm | eT: {effectiveThickness:F0}mm\n" +
                       $"Y: {yaw:F1}° | sC: {shatterChance:P0}\n" +
                       $"Result: {impactResult} | {(spalled ? "Spalled" : "No Spall")}";
            }
#endif
            


            string shotDebugResult = "";


            // end log setup

            if (hitInfo.collider.gameObject.tag == "ground")
            {
                shotDebugResult = "Impact";
                DestroyBulletFromImpact(hitInfo);
            }

            if (hitInfo.collider.gameObject.GetComponent<Human>()) //we hit a human
            {
                float damage = 0.5f * currentMass * Velocity.sqrMagnitude;
                Human human = hitInfo.collider.gameObject.GetComponent<Human>();
                human.OnHit(damage, hitInfo.point, Velocity.normalized);
                LogEvent("Impact", $"Hit a human {human.name}");
                shotDebugResult = "Impact";
                AddIgnoredCollider(hitInfo.collider);

            }

            // we check to see if we entered a compartment
            if (hitInfo.collider.gameObject.GetComponent<Compartment>()) //hit a compartment
            {
                Compartment hitCompartment = hitInfo.collider.gameObject.GetComponent<Compartment>();
                LogEvent("Impact Result", $"Bullet entered {hitCompartment.label} compartment");
                AddIgnoredCollider(hitInfo.collider);
                float explosiveEquivalent = hitCompartment.CalculateTNTEquivalent(currentMass, Velocity.magnitude);
                hitCompartment.Explosion(explosiveEquivalent, codex.projectileData.d * 100.0f);
            }
            if (hitInfo.collider.gameObject.GetComponent<Module>())
            {
                Module module = hitInfo.collider.gameObject.GetComponent<Module>();
                if (module != null)
                {
                    float damage = 0.5f * currentMass * Velocity.sqrMagnitude;
                    module.Damage(damage);
                    AddIgnoredCollider(hitInfo.collider);

                    if (module.StopBullet)
                    {
                        LogEvent("Impact Result", $"Bullet stopped by hard module '{module.name}'.");
                        shotDebugResult = "Impact";
                        DestroyBulletFromImpact(hitInfo);
                        return; // Bullet is destroyed, exit the entire impact handling.
                    }
                    
                    LogEvent("Impact Result", $"Bullet passed through soft module '{module.name}'.");
                    // If it doesn't stop the bullet, we return to the iterative movement loop.
                    // The bullet will continue its travel in the next iteration.
                    return;
                }
            }

            //we check to see if we have an armor component on the hit object
                if (hitInfo.collider.gameObject.GetComponent<Armor>())
                {
                    // ==========================================================
                    // FIXED LOGIC: FUSES ARE NOW HANDLED SEPARATELY AND CORRECTLY
                    // ==========================================================

                    // This is the logic for pure HE and HEAT shells. They detonate on impact.
                    if (codex.projectileData.penetratorType == ProjectileData.PenetratorType.CH ||
                        codex.projectileData.penetratorType == ProjectileData.PenetratorType.JET)
                    {
                        Armor _hitArmor = hitInfo.collider.gameObject.GetComponent<Armor>();
                        float _penetration = TerminalBallistics.HighExplosive.GetPenetration(codex.projectileData.ExplosiveMassKG);
                        result.hitArmor = hitInfo.collider.gameObject.name;
                        float normalThickness = _hitArmor.thicknessMM;
                        result.effectiveThickness = normalThickness;
                        result.thickness = normalThickness;
                        bool jet = false;
                        if (codex.projectileData.ShapedCharge)
                        {
                            _penetration = TerminalBallistics.HighExplosive.HeatPenetration(codex.projectileData.d * 1000.0f, codex.projectileData.ExplosiveMassKG);
                            normalThickness /= Mathf.Cos(Mathf.Deg2Rad * Vector3.Angle(Velocity, hitInfo.normal));
                            jet = true;
                            result.effectiveThickness = normalThickness;
                        }

                        result.penetration = _penetration;
                        if (_penetration > normalThickness * 0.75f)
                        {
                            result.penetrated = true;
                            shotDebugResult = "Penetrated";
                            LogEvent("Explosion Result", $"Explosion has penetrated the plate, Jet status is {jet}");                            
                            if (jet && codex.projectileData.jetPrefab != null)
                            {
                                // Spawn the specialized HEAT jet projectile
                                Vector3 exitPoint = hitInfo.point + (hitInfo.normal * (normalThickness / 1000f));
                                Quaternion jetRotation = Quaternion.LookRotation(hitInfo.normal);
                                var jetProjectile = BulletPool.Instance.GetBullet(codex.projectileData.jetPrefab, exitPoint, jetRotation, null);
                                if (jetProjectile != null)
                                {
                                    // The jet continues with high velocity but loses penetration over distance.
                                    // The jetData should be configured for this behavior.
                                    float jetVelocity = 2000f; // A representative velocity for a HEAT jet.
                                    jetProjectile.Fire(
                                        position: exitPoint,
                                        rotation: jetRotation,
                                        inheritedVelocity: Vector3.zero, // Jet velocity is independent of shell velocity.
                                        muzzleVelocity: jetVelocity,
                                        deviation: 0f,
                                        _codex: codex.projectileData.jetData,
                                        _angular: Vector3.zero,
                                        _spawnTime: Time.time
                                    );
                                    jetProjectile.AddIgnoredCollider(hitInfo.collider);
                                }
                            }
                            else
                            {
                                // For standard HE, generate generic spall.
                                _hitArmor.GenerateSpall(this, hitInfo.point + (hitInfo.normal * (normalThickness / 1000.0f)), hitInfo.normal, hitInfo.normal * 300f);
                            }
                        }
                        else
                        {
                            result.penetrated = false;
                            shotDebugResult = "Non-Penetrated";
                            LogEvent("Explosion Result", $"Explosion did not penetrate the plate");
                        }
                        ExplodeBullet(hitInfo.point + (-Velocity.normalized * 0.05f), transform.rotation);
                        DestroyBulletSilently();
                        return;
                    }

                    // =================================================================
                    // CORRECTED LOGIC: KE and APHE handling is now contained in one block.
                    // =================================================================
                    if (codex.projectileData.penetratorType == ProjectileData.PenetratorType.KE && codex.projectileData.d >= 0.005f) // Handle KE and APHE rounds
                    {
                        Armor hitArmor = hitInfo.collider.gameObject.GetComponent<Armor>();

                        // --- ROBUSTNESS CHECK ---
                        // This prevents a NullReferenceException if an Armor component in the scene is missing its data.
                        if (hitArmor.data == null)
                        {
                            LogEvent("Armor Error", $"The armor piece '{hitArmor.name}' on GameObject '{hitInfo.collider.gameObject.name}' is missing its ArmorCodex data. Bullet destroyed.");
                            DestroyBulletFromImpact(hitInfo);
                            return;
                        }

                        ArmorCodex _armorData = hitArmor.data;
                        AddIgnoredCollider(hitInfo.collider);
                        result.hitArmor = hitInfo.collider.gameObject.name;
    
                        bool kinetic = codex.projectileData.penetratorType == ProjectileData.PenetratorType.KE;
                        float impactLLos = hitArmor.GetLosThickness(kinetic, hitInfo.point, hitInfo.normal, Velocity, false);
                        float effectiveThickness = Mathf.Abs(impactLLos / (Mathf.Cos(Mathf.Deg2Rad * Vector3.Angle(Velocity, hitInfo.normal))));
                        result.thickness = impactLLos;
                        result.effectiveThickness = effectiveThickness;
    
                        // Calculate raw penetration before yaw penalties
                        float rawPenetration = SelectPenetrationForm.Penetration(this, _armorData);

                        // Overmatch checks
                        bool forcePenetration = false;
                        // 1. Caliber overmatch: shell diameter is 3x or more the physical armor thickness
                        if (codex.projectileData.d * 1000f >= hitArmor.thicknessMM * 3f)
                        {
                            forcePenetration = true;
                            LogEvent("Overmatch", $"Caliber overmatch: {codex.projectileData.d * 1000f:F1}mm vs {hitArmor.thicknessMM:F1}mm armor.");
                        }
                        // 2. Penetration overmatch: raw penetration is 10% or more than effective thickness
                        if (!forcePenetration && rawPenetration >= effectiveThickness * 1.1f)
                        {
                            forcePenetration = true;
                            LogEvent("Overmatch", $"Penetration overmatch: {rawPenetration:F1}mm vs {effectiveThickness:F1}mm effective armor.");
                        }

                        float impactYawAngle = Vector3.Angle(Velocity.normalized, transform.forward);
                        // Apply the grace angle. Any yaw below the grace angle is treated as zero.
                        float effectiveYawAngle = Mathf.Max(0f, impactYawAngle - codex.projectileData.yawGraceAngle);

                        // NEW LOGIC: Reduce yaw penalty based on impact obliquity.
                        // High obliquity impacts are dominated by angle effects, so yaw becomes less of a factor.
                        float obliquityAngle = Vector3.Angle(-Velocity.normalized, hitInfo.normal); // Angle from the normal

                        float yawPenaltyMultiplier = 1f;
                        if (obliquityAngle > codex.projectileData.fullYawPenaltyObliquity)
                        {
                            yawPenaltyMultiplier = 1f - Mathf.Clamp01((obliquityAngle - codex.projectileData.fullYawPenaltyObliquity) / (codex.projectileData.noYawPenaltyObliquity - codex.projectileData.fullYawPenaltyObliquity));
                        }

                        // The final yaw angle used for calculation is scaled by the obliquity.
                        float finalEffectiveYaw = effectiveYawAngle * yawPenaltyMultiplier;

                        float yawFactor = Mathf.Pow(CalculateYawFactor(finalEffectiveYaw), 2f);

                        // The previous shatter chance calculation caused a divide-by-zero error when yawFactor was 1 (a perfect hit).
                        // This new logic is more robust and physically correct.
                        // A perfect hit (yawFactor = 1) has 0 shatter chance. As yaw increases, shatter chance approaches infinity.
                        float shatterChance;
                        if (yawFactor > 0.001f)
                        {
                            shatterChance = (1f / yawFactor) - 1f;
                        }
                        else
                        {
                            shatterChance = float.PositiveInfinity; // Effectively guarantees a shatter for extreme yaw angles.
                        }
                        LogEvent("Impact Calc", $"Impact Obliquity: {obliquityAngle:F1}° | Yaw: {impactYawAngle:F1}° -> Final Yaw: {finalEffectiveYaw:F1}° => Yaw Factor: {yawFactor:F2} | Shatter Chance: {shatterChance:P1}");
                        float penetration = rawPenetration * yawFactor;
    
                        result.penetration = penetration; // Store the final penetration after yaw effects.

                        float ratio = penetration / effectiveThickness;
                        float num = penetration - effectiveThickness;
                        float veloRatio = Mathf.Clamp01(num / effectiveThickness);
    
                        // Ricochet logic
                        float _dot = Vector3.Dot(Velocity.normalized, hitInfo.normal);
                        float ricochetChance = codex.projectileData.ricochetChance.Evaluate(Mathf.Abs(_dot));
                        if (!forcePenetration && Random.Range(0.0f, 1.0f) <= ricochetChance && ratio > 0.4f)
                        {
                            float damp = TerminalBallistics.RicochetUtils.CalculateDynamicDamping(Velocity, hitInfo.normal);
                            Vector3 newDirection = CalculateRicochetDirection(Velocity, hitInfo.normal);
                            Vector3 velocityParallel = Vector3.ProjectOnPlane(Velocity, hitInfo.normal);
                            Vector3 frictionForceDirection = -velocityParallel.normalized;

                            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (ShotDebugUI.Instance != null && !IsSpallFragment) 
                            {
                                ShotDebugUI.Instance.AddLogEntry($"<color=yellow>Ricochet</color> on {hitInfo.collider.name} @ {distanceTraveled:F1}m");
                            }
                            #endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (!IsSpallFragment)
                            {
                                shotDebugResult = "Bounced";
                                impactText = BuildImpactDebugText(Velocity.magnitude, result.impactAngle, penetration, result.thickness, effectiveThickness, impactYawAngle, shatterChance, "Ricochet", false);
                                impactInfos.Add(new ImpactDebugInfo { position = hitInfo.point, radius = codex.projectileData.d, color = Color.yellow, timestamp = Time.time, text = impactText });
                            }
#endif
                            
                            Vector3 ricochetTorque = Vector3.Cross(frictionForceDirection, newDirection) * 0.4f * velocityParallel.magnitude;
    
                            float newMomentOfInertia = MomentOfInertia;
                            Vector3 angularAcceleration = ricochetTorque / newMomentOfInertia;
                            AngularVelocity += angularAcceleration * (Time.fixedDeltaTime);
                            Velocity = newDirection * Velocity.magnitude * damp;
                            
                            // Update position and rotation to reflect the ricochet at the point of impact.
                            transform.position = hitInfo.point;
                            transform.rotation = Quaternion.LookRotation(newDirection);
    
                            LogShotResult(result);
                            LogEvent("Impact Result", $"Bounced off armor at {distanceTraveled} meters");
                            LogFinalShotDataToUI(shotDebugResult);
            
                            return; // Return to the iterative loop in UpdateBullet
                        }
    
                        if (!forcePenetration && shatterChance >= Random.Range(0.0f, 1.0f))
                        {
                            result.penetrated = false;
                            LogEvent("Impact Result", "Shell Shattered");

                            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (ShotDebugUI.Instance != null && !IsSpallFragment) 
                            {
                                ShotDebugUI.Instance.AddLogEntry($"<color=red>Shattered</color> on {hitInfo.collider.name} @ {distanceTraveled:F1}m");
                            }
                            #endif

                            #if UNITY_EDITOR
                            if (!IsSpallFragment)
                            {
                                shotDebugResult = "Jammed";
                                impactText = BuildImpactDebugText(Velocity.magnitude, result.impactAngle, penetration, result.thickness, effectiveThickness, impactYawAngle, shatterChance, "Shattered", false);
                                impactInfos.Add(new ImpactDebugInfo { position = hitInfo.point, radius = codex.projectileData.d, color = Color.black, timestamp = Time.time, text = impactText });
                                LogShotResult(result);
                            }
                            #endif
                            DestroyBulletFromImpact(hitInfo);
                            LogFinalShotDataToUI(shotDebugResult);
            
                            return; // Exit, bullet is gone.
                        }
    
                        // Penetration check
                        if (ratio >= 0.9f || forcePenetration)
                        {
                            // PENETRATED!
                            result.penetrated = true;

                            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (ShotDebugUI.Instance != null && !IsSpallFragment) 
                            {
                                ShotDebugUI.Instance.AddLogEntry($"<color=green>Penetrated</color> {hitInfo.collider.name} ({effectiveThickness:F0}mm) @ {distanceTraveled:F1}m");
                            }
                            #endif

                            shotDebugResult = "Penetrated";
                            // Calculate exit point for spall generation
                            Vector3 exitPoint = hitInfo.point + (Velocity.normalized * (effectiveThickness / 1000.0f));

                            // Generate spall immediately upon penetration, before handling fuse logic.
                            if (codex.projectileData.d * 1000.0f > 25.0f)
                            {
                                hitArmor.GenerateSpall(this, exitPoint, Velocity.normalized, Velocity);
                                result.HasSpalled = true;
                                #if UNITY_EDITOR
                                if (!IsSpallFragment)
                                {
                                    impactText = BuildImpactDebugText(Velocity.magnitude, result.impactAngle, penetration, result.thickness, effectiveThickness, impactYawAngle, shatterChance, "Pen", true);
                                    impactInfos.Add(new ImpactDebugInfo { position = hitInfo.point, radius = codex.projectileData.d, color = Color.green, timestamp = Time.time, text = impactText });
                                }
                                #endif
                                result.timeOfSpall = Time.time;
                            }

                            // --- Instantaneous APHE Fuse Logic ---
                            // This runs AFTER spall from the initial penetration has been created.
                            if (!fuseTriggered && codex.projectileData.penetratorType == ProjectileData.PenetratorType.KE && codex.projectileData.fuseType == ProjectileData.FuseType.Impact)
                            {
                                fuseTriggered = true; // Ensure this logic runs only once.

                                Vector3 traceVelocity = this.Velocity; // Use a copy for tracing calculations
                                bool canDetonate = true;

                                // The check for subsequent plates starts from the initial impact point and extends for the full fuse distance.
                                float fuseDistance = codex.projectileData.targetFuseDistance;

                                if (fuseDistance > 0)
                                {
                                    RaycastHit[] subsequentHits = Physics.RaycastAll(hitInfo.point, traceVelocity.normalized, fuseDistance, ThickHitLayers | RayHitLayers);
                                    if (subsequentHits.Length > 0)
                                    {
                                        System.Array.Sort(subsequentHits, (x, y) => x.distance.CompareTo(y.distance));

                                        foreach (var subsequentHit in subsequentHits)
                                        {
                                            // We must explicitly ignore the collider of the plate we just penetrated.
                                            if (subsequentHit.collider == hitInfo.collider || ignoredColliders.Contains(subsequentHit.collider))
                                            {
                                                continue;
                                            }

                                            if (subsequentHit.collider.TryGetComponent<Armor>(out Armor subsequentArmor))
                                            {
                                                // --- ROBUSTNESS CHECK for fuse logic ---
                                                if (subsequentArmor.data == null)
                                                {
                                                    LogEvent("Fuse Error", $"A subsequent armor piece '{subsequentArmor.name}' on GameObject '{subsequentHit.collider.gameObject.name}' is missing its ArmorCodex data. Fuse is now a dud.");
                                                    canDetonate = false;
                                                    break; // Stop checking further plates.
                                                }

                                                float subsequentPenetration = SelectPenetrationForm.Penetration(this, subsequentArmor.data);
                                                float subsequentLOS = subsequentArmor.GetLosThickness(true, subsequentHit.point, subsequentHit.normal, traceVelocity, false);
                                                float subsequentEffective = Mathf.Abs(subsequentLOS / (Mathf.Cos(Mathf.Deg2Rad * Vector3.Angle(traceVelocity, subsequentHit.normal))));

                                                if (subsequentPenetration >= subsequentEffective)
                                                {
                                                    // Penetrated this plate, reduce velocity for the next check.
                                                    float subsequentVeloRatio = Mathf.Clamp01((subsequentPenetration - subsequentEffective) / subsequentPenetration);
                                                    traceVelocity *= subsequentVeloRatio;
                                                }
                                                else
                                                {
                                                    // Failed to penetrate a subsequent plate, fuse is a dud.
                                                    canDetonate = false;
                                                    LogEvent("Fuse Logic", $"APHE dud. Failed to penetrate subsequent plate '{subsequentArmor.name}'.");
                                                    break; // Stop checking further plates.
                                                }
                                            }
                                        }
                                    }
                                }

                                if (canDetonate)
                                {
                                    // Calculate the exact detonation point relative to the initial impact.
                                    Vector3 fuseDetonationPoint = hitInfo.point + Velocity.normalized * codex.projectileData.targetFuseDistance;

                                    LogEvent("Fuse Logic", "APHE path cleared. Detonating at fuse distance.");

                                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                                    if (ShotDebugUI.Instance != null && !IsSpallFragment) 
                                    {
                                        ShotDebugUI.Instance.AddLogEntry($"<color=orange>Fused & Detonated</color> @ {distanceTraveled + codex.projectileData.targetFuseDistance:F1}m");
                                    }
                                    #endif

                                    // Teleport the bullet to the exact detonation point before exploding.
                                    // This ensures the bullet's final position is correct and prevents it from "traveling too far".
                                    transform.position = fuseDetonationPoint;

                                    // Check if detonation point is inside an armor plate to suppress fragments
                                    Collider[] colliders = Physics.OverlapSphere(fuseDetonationPoint, codex.projectileData.d, ThickHitLayers | RayHitLayers);
                                    bool insideArmor = false;
                                    foreach (var col in colliders) { if (col.GetComponent<Armor>() != null) { insideArmor = true; break; } }

                                    LogFinalShotDataToUI("Fused");
                                    ExplodeBullet(fuseDetonationPoint, transform.rotation, !insideArmor);
                                    IsFired = false; // Explicitly mark as destroyed to stop further processing.
                                    return; // Bullet is destroyed, stop all processing.
                                }
                                // If canDetonate is false, we just fall through and let the bullet continue its life as a normal KE round.
                            }
    
                            // Common penetration logic for all penetrating rounds (KE and non-exploded APHE)
                            transform.position = exitPoint; // Teleport to exit point
    
                            if (veloRatio >= 0.05f)
                            {
                                Velocity *= veloRatio;
                                result.exitVelocity = Velocity;

                                // --- MASS LOSS & DISINTEGRATION LOGIC ---
                                // This only applies to full-caliber rounds governed by the DeMarre formula.
                                if (codex.projectileData.projectileType == ProjectileData.ProjectileType.fullCaliber)
                                {
                                    float remainingPenetration = penetration - effectiveThickness;
                                    float newMass = TerminalBallistics.DeMarrePenetration.CalculateNewMass(
                                        remainingPenetration,
                                        codex.projectileData.DeMarreeCoefficients.y,
                                        codex.projectileData.d * 1000f,
                                        Velocity.magnitude
                                    );

                                    if (newMass <= 0.01f) // Use a small threshold for floating point inaccuracies
                                    {
                                        LogEvent("Impact Result", "Projectile has run out of mass and disintegrated.");
                                        LogEvent("Impact Result", $"Projectile disintegrated after penetration. Remaining mass was calculated to be {newMass:F4}kg.");

                                        #if UNITY_EDITOR || DEVELOPMENT_BUILD
                                        if (ShotDebugUI.Instance != null && !IsSpallFragment) 
                                        {
                                            ShotDebugUI.Instance.AddLogEntry($"<color=grey>Disintegrated</color> (mass depleted)");
                                        }
                                        #endif
                                        LogFinalShotDataToUI("Disintegrated");
                                        DestroyBulletFromImpact(hitInfo);
                                        return; // Exit, bullet is gone.
                                    }
                                    else
                                    {
                                        LogEvent("Impact Result", $"Mass reduced from {currentMass:F2}kg to {newMass:F2}kg due to penetration.");
                                        currentMass = newMass;

                                        // --- POST-PENETRATION DEVIATION LOGIC ---
                                        if (codex.projectileData.BallisticCap)
                                        {
                                            // Normalize trajectory towards the armor plate's normal.
                                            // Effect is stronger on less angled impacts.
                                            // Recalculate the angle with the new post-penetration velocity.
                                            obliquityAngle = Vector3.Angle(-Velocity.normalized, hitInfo.normal);
                                            float normalizationStrength = 0.75f * (1f - (obliquityAngle / 90f)); // Max 75% normalization at 0 degrees

                                            Vector3 newDirection = Vector3.Slerp(Velocity.normalized, -hitInfo.normal, normalizationStrength);
                                            Velocity = newDirection * Velocity.magnitude;
                                            LogEvent("Impact Result", $"Normalized trajectory. Strength: {normalizationStrength:P1}");
                                        }
                                        else
                                        {
                                            // Apply a random, slight deviation for non-capped projectiles.
                                            float maxRandomDeviation = 5f; // degrees
                                            Quaternion randomRotation = Quaternion.Euler(
                                                Random.Range(-maxRandomDeviation, maxRandomDeviation),
                                                Random.Range(-maxRandomDeviation, maxRandomDeviation),
                                                0);
                                            Velocity = randomRotation * Velocity;
                                            LogEvent("Impact Result", "Applied random post-penetration deviation.");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result.penetrated = false;
                                shotDebugResult = "Jammed";
                                LogFinalShotDataToUI(shotDebugResult);
                                DestroyBulletFromImpact(hitInfo);
                                return; // Bullet is destroyed, exit.
                            }
    
                            // Return to the UpdateBullet loop to continue travel
                            return;
                        }
                        else
                        {
                            // Did not penetrate
                            result.penetrated = false;
                            shotDebugResult = "Jammed"; // Default to Jammed/Crushed

                            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (ShotDebugUI.Instance != null && !IsSpallFragment) 
                            {
                                ShotDebugUI.Instance.AddLogEntry($"<color=red>Non-Penetration</color> on {hitInfo.collider.name} ({effectiveThickness:F0}mm)");
                            }
                            #endif

                            LogEvent("Impact Result", "Crushed into armor surface");
                        if (result.KineticEnergy * 2f >= result.effectiveThickness * 1000.0f * (codex.projectileData.d * 1000.0f) * 0.5f)
                        {
                            LogEvent("Impact Result", "KE caused spalling despite non-penetration");
                            hitArmor.GenerateSpall(this, hitInfo.point + (Velocity.normalized * (effectiveThickness / 1000.0f)), hitInfo.normal, Velocity);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                if (!IsSpallFragment)
                                {
                                    shotDebugResult = "Non-Penetrated";
                                    impactText = BuildImpactDebugText(Velocity.magnitude, result.impactAngle, penetration, result.thickness, effectiveThickness, impactYawAngle, shatterChance, "No Pen", true);
                                    impactInfos.Add(new ImpactDebugInfo { position = hitInfo.point, radius = codex.projectileData.d, color = Color.red, timestamp = Time.time, text = impactText });
                                    result.HasSpalled = true;
                                    result.timeOfSpall = Time.time;
                                }
#endif
                        }
                        else
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                if (!IsSpallFragment)
                                {
                                    shotDebugResult = "Jammed";
                                    impactText = BuildImpactDebugText(Velocity.magnitude, result.impactAngle, penetration, result.thickness, effectiveThickness, impactYawAngle, shatterChance, "Crushed", false);
                                    impactInfos.Add(new ImpactDebugInfo { position = hitInfo.point, radius = codex.projectileData.d, color = Color.black, timestamp = Time.time, text = impactText });
                                }
#endif
                            }
                            DestroyBulletFromImpact(hitInfo);
                            LogFinalShotDataToUI(shotDebugResult);
                            return; // Exit, bullet is gone.
                        }
                    }
                    else // Non-penetrating hit on non-armor, or KE round < 5mm
                    {
                        #if UNITY_EDITOR
                        if (!IsSpallFragment)
                        {
                            shotDebugResult = "Impact";
                            impactInfos.Add(new ImpactDebugInfo { position = hitInfo.point, radius = codex.projectileData.d, color = Color.black, timestamp = Time.time, text = "Impact (Non-Armor)" });
                        }
                        #endif
                    }
                }

            
            // ==========================================================
            // YOUR ORIGINAL LOGIC FOR NON-ARMORED OBJECTS
            // ==========================================================
            // This is the section of your code that handles impacts with things like dirt, trees, etc.
            // It has not been modified.

            // if ExplodeOnImpact is true, then we explode
            if (ExplodeOnImpact)
                ExplodeBullet(hitInfo.point, transform.rotation);

            // otherwise, we check to see if we have a component that needs damage
            //I_RecievableDamage damageScript = hitInfo.collider.GetComponentInParent<I_RecievableDamage>();
            //if (damageScript != null)
            //{
             //   damageScript.RecieveDamage(TerminalBallistics.HighExplosive.GetDamage(codex.projectileData.ExplosiveMassKG));
            //}

            result.penetrated = false;
            LogShotResult(result);
            LogFinalShotDataToUI("Impact");
            DestroyBulletFromImpact(hitInfo);
        }
    private void SpawnPhysicalSpall(int count, Vector3 origin, Vector3 direction)
    {

        Collider[] ignoreCollisions = GetRadiusColliders(origin, codex.projectileData.d * 2f);
        for (int i = 0; i < count; i++)
        {
            if (codex.projectileData.spallPrefab != null)
            {
                Quaternion rotation = Quaternion.LookRotation(direction);
                var spall = BulletPool.Instance.GetBullet(codex.projectileData.spallPrefab, origin, rotation, null);
                
                if (codex.projectileData.penetratorType == ProjectileData.PenetratorType.KE)
                { spall.AddIgnoredCollider(gameObject.GetComponent<Collider>()); }

                for (int a = 0; a < ignoreCollisions.Length; a++)
                {
                    spall.AddIgnoredCollider(ignoreCollisions[a]); //this way we dont hit overlapping colliders
                }

                spall.Fire(position: origin, rotation: rotation, Vector3.zero, codex.projectileData.spallData.projectileData.muzzleVelocity, 56f, codex.projectileData.spallData, Vector3.zero, Time.time);
                spall.IsSpallFragment = true; // Mark as spall for debug drawing
            }//spawn physical spall
        }

        LogEvent("Bullet Update", $"{name} has fragmented");
    }


    public Collider[] GetRadiusColliders(Vector3 position, float radius)
    {
        // Use Physics.OverlapSphere to get all colliders within the radius.
        return Physics.OverlapSphere(position, radius);
    }


        private void HandleExplosionDamage(Vector3 explodePos, float radius = 1f)
        {
            
            LogEvent("Explosion Event",$"Running explosive damage with a radius of {radius} meters");
            Collider[] hitCollider = GetRadiusColliders(explodePos, radius);
            for (int i = 0; i < hitCollider.Length; i++)
            {
                if (hitCollider[i].GetComponent<Compartment>())
                {
                    Compartment hitCompartment = hitCollider[i].gameObject.GetComponent<Compartment>();
                    hitCompartment.Explosion(codex.projectileData.ExplosiveMassKG, 0.0f); //we dont pass the diameter because its an explosion not a puncture
                }
                if (hitCollider[i].GetComponent<Human>())
                {
                    Human human = hitCollider[i].gameObject.GetComponent<Human>();
                    human.OnShockwave(explodePos, radius, 0.5f * codex.projectileData.ExplosiveMassKG * 1600f);
                }
            }
            // ==========================================================
            // TODO: Bullet exploded, insert damage handling here!
            // ==========================================================
        }

        /// <summary>
        /// Checks the ignore list to see if this given hit is allowed.
        /// </summary>
        private bool IsHitAllowed(RaycastHit hit)
        {
            bool isHitAllowed = true;

            var hitRigidbody = hit.rigidbody;
            if (hitRigidbody != null && ignoredRigidbodies.Contains(hitRigidbody))
                isHitAllowed = false;
            else if (ignoredColliders.Contains(hit.collider))
                isHitAllowed = false;

            return isHitAllowed;
        }

        private (bool hitSomething, RaycastHit hitInfo) RunThickHitDetection(Vector3 position, Vector3 velocity, float deltaTime)
        {
            // For thick bullets, first do collision detection only on things considered targets.
            int hitCount = Physics.SphereCastNonAlloc(
                origin: position,
                direction: velocity.normalized,
                radius: BulletDiameter * .5f,
                maxDistance: BulletLength + velocity.magnitude * deltaTime,
                results: raycastHits,
                layerMask: ThickHitLayers);

            var (bulletHitSomething, closestHit) = GetClosestValidHit(raycastHits, hitCount);
            if (!bulletHitSomething)
            {
                // If the bullet didn't hit anything, then do normal raycast style hit detection
                // against other objects that we don't care about having generous hit detection.
                // This typically prevents unusual looking hit detection against large objects like
                // terrain or buildings.
                hitCount = Physics.RaycastNonAlloc(
                    origin: position,
                    direction: velocity.normalized,
                    maxDistance: BulletLength + velocity.magnitude * deltaTime,
                    layerMask: RayHitLayers,
                    results: raycastHits);

                (bulletHitSomething, closestHit) = GetClosestValidHit(raycastHits, hitCount);
            }

            return (bulletHitSomething, closestHit);
        }

        private (bool hitSomething, RaycastHit hitInfo) RunRayHitDetection(Vector3 position, Vector3 velocity, float deltaTime)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin: position,
                direction: velocity,
                maxDistance: BulletLength + velocity.magnitude * deltaTime,
                layerMask: ThickHitLayers | RayHitLayers,
                results: raycastHits);

            return GetClosestValidHit(raycastHits, hitCount);
        }

        private (bool hitSomething, RaycastHit closestHit) GetClosestValidHit(RaycastHit[] listOfHits, int hitCount)
        {
            if (hitCount == 0)
                return (false, new RaycastHit());

            RaycastHit closestHit = new RaycastHit();
            float closestDistance = float.MaxValue;
            bool hitSomething = false;

            if (IsHitAllowed(listOfHits[0]))
            {
                closestHit = listOfHits[0];
                closestDistance = listOfHits[0].distance;
                hitSomething = true;
            }

            for (int i = 0; i < hitCount; ++i)
            {
                if (IsHitAllowed(listOfHits[i]))
                {
                    if (listOfHits[i].distance < closestDistance)
                    {
                        closestDistance = listOfHits[i].distance;
                        closestHit = listOfHits[i];
                        hitSomething = true;
                    }
                }
            }

            return (hitSomething, closestHit);
        }


        /// <summary>
        /// Calculates the drag acceleration vector from the provided velocity and ballistic coefficient.
        /// </summary>
        /// <param name="bulletVel">The current velocity of the projectile.</param>
        /// <param name="ballisticCoeff">The ballistic coefficient, which encapsulates drag properties.</param>
        /// <returns>A Vector3 representing the drag acceleration.</returns>
        public static Vector3 GetDragAcceleration(Vector3 bulletVel, float ballisticCoeff)
        {
            // This is a direct translation of the logic from the C++ code
            float deltaSpeed = (ballisticCoeff * 0.02f) * bulletVel.magnitude;
            float velocityMult = (Mathf.Abs(deltaSpeed) < 1.0f) ? (deltaSpeed / (1.0f - deltaSpeed)) + 1.0f : 1.0f;

            // Calculate the change in velocity caused by drag
            // New Velocity (Drag Only) = Old Velocity * velocityMult
            Vector3 newVelDragOnly = new Vector3(
                bulletVel.x * velocityMult,
                bulletVel.y * velocityMult,
                bulletVel.z * velocityMult
            );
            
            // Acceleration = (New Velocity - Old Velocity) / deltaTime
            Vector3 dragAcceleration = newVelDragOnly;
            //Debug.Log($"Drag Acceleration is {dragAcceleration}, delta speed is {deltaSpeed} multiplier is {velocityMult}");
            return dragAcceleration;
        }




        private void CleanUpTrails()
        {
            foreach (var trail in ChildTrails)
            {
                trail.emitting = false;
                trail.autodestruct = true;
                trail.transform.SetParent(null);
            }
        }

#if UNITY_EDITOR
        // This method draws debug visuals for this specific bullet instance.
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (!IsFired) return;

            // The rest of the gizmos are for the bullet's own debug visuals
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.DrawLine(Vector3.right, Vector3.left);
            Gizmos.DrawLine(Vector3.up, Vector3.down);
            Gizmos.DrawLine(Vector3.zero, transform.forward * BulletLength);

            var bulletHead = new Vector3(0f, 0f, BulletLength);
            Gizmos.DrawLine(bulletHead + Vector3.right, bulletHead + Vector3.right);
            Gizmos.DrawLine(bulletHead + Vector3.up, bulletHead + Vector3.down);

            Gizmos.matrix = Matrix4x4.identity;

            if (IsThick)
            {
                var velocity = MoveInFixedUpdate ? Velocity * Time.fixedDeltaTime : Velocity * Time.deltaTime;

                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position - velocity, transform.position);
                Gizmos.DrawWireSphere(transform.position, BulletDiameter * .5f);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position + velocity, transform.position);
                Gizmos.DrawWireSphere(transform.position + velocity, BulletDiameter * .5f);
            }
            else
            {
                var velocity = MoveInFixedUpdate ? Velocity * Time.fixedDeltaTime : Velocity * Time.deltaTime;

                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position - velocity, transform.position);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position + velocity, transform.position);
            }
        }
#endif
    }
}