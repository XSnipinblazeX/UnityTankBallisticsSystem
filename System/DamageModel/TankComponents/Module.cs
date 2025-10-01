using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Module : MonoBehaviour
{
    public enum ModuleType
    {
        Utility,
        Mobility,
        Combat
    }

    public bool DemoDamge = false;

    [SerializeField] public DamageRelay lastShot;    
    [Tooltip("The role of this module, used by the AI Task Manager to prioritize repairs.")]
    public ModuleType moduleType = ModuleType.Utility;
    [Tooltip("Does this module stop spall fragments")]
    public bool StopSpall = true;
    [Tooltip("If true, this module will stop a kinetic projectile that hits it. If false, the projectile will pass through after dealing damage.")]
    public bool StopBullet = false;
    public float DamageMultiplier = 1.0f;
    public float maxHealth = 500f;
    public float currentHealth = 500f;
    public float destroyedHealth { get; protected set; } = 120.0f;
    public float noRepairHealth = 1.0f;
    public bool CanBeRepaired;
    public float repairMultiplier = 0.74f;
    public bool IsFieldRepaired { get; protected set; } = false;
    float lastRepairAmount = 0.0f;

    public abstract void Damage(float damage);
    protected abstract void OnDamage();

    public void UpdateParent(DamageRelay relay)
    {
        BlazeVehicle _parent = GetComponentInParent<BlazeVehicle>();
        Debug.Log($"Vehicle : {_parent}");
        _parent.ReceiveImpulse(relay);
    }

    /// <summary>
    /// Repairs the module by a given amount of health.
    /// </summary>
    /// <param name="repairAmount">The amount of health to restore.</param>
    /// <returns>True if the module is now fully repaired, false otherwise.</returns>
    public bool Repair(float repairAmount)
    {
        // Calculate the maximum health achievable through field repairs.
        float repairCap = maxHealth * repairMultiplier;

        // If it can't be repaired, or has already been field repaired, consider it done.
        if (!CanBeRepaired || IsFieldRepaired)
        {
            return true;
        }

        Debug.Log("repair amount " + repairAmount);
        currentHealth += repairAmount;

        // Check if the repair has now reached or exceeded the repair cap.
        if (currentHealth >= repairCap)
        {
            currentHealth = repairCap; // Clamp to the cap.
            IsFieldRepaired = true; // Mark as repaired to prevent being re-assigned.
            return true; // Repair is complete.
        }
        return false; // Still being repaired.
    }
}
