using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using GNB;

[Serializable]
public class ProjectileData
{

    public enum DragModel
    {
        G1,
        G7,
        OTHER
    }


    public enum PenetratorType //is this a kinetic or chemical round
    {
        KE,
        CH,
        JET
    }

    public enum ProjectileType //this determines the penetration formulas to use
    {
        fullCaliber,
        subCaliber,
        longRod
    }

    public enum PenetratorMaterial
    {
        Steel,
        Tungsten,
        DU
    }

    public enum FuseType
    {
        _Time,
        Impact,
        Proxy,
        None
    }

    public Bullet spallPrefab;
    public ProjectileDataCodex spallData;


    public GameObject VisualMesh;
    public TracerColor tracerColor;

    //enums end

    [Header("Advanced Ballistics")]
    [Tooltip("The initial spin rate of the projectile in rotations per second (RPS).")]
    public float MuzzleSpinRPS = 2000f;
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

    [Tooltip("The angle (in degrees) below which yaw has no effect on penetration. A value of 5 means any yaw from 0-5 degrees is treated as 0.")]
    public float yawGraceAngle = 5.0f;

    [Tooltip("The impact obliquity angle (from normal) below which yaw has its full penalizing effect.")]
    public float fullYawPenaltyObliquity = 5.0f;

    [Tooltip("The impact obliquity angle (from normal) above which yaw has no penalizing effect.")]
    public float noYawPenaltyObliquity = 60.0f;



    [Header("Projectile Settings")]

    public float muzzleVelocity = 800.0f; //m/s, this is the muzzle velocity of the projectile
    public float ballisticCoefficient = 1.0f; //this is the ballistic coefficient of the projectile, if you don't know what this is then leave it at 0


    public DragModel dragModel = DragModel.G1;


    [Tooltip("Set the projectile to be a kinetic or chemical round")]
    public PenetratorType penetratorType = PenetratorType.KE;

    [Tooltip("Is the projectile full caliber, sub caliber, or a long rod")]
    public ProjectileType projectileType = ProjectileType.fullCaliber;

    [Tooltip("Projectile length in meters")]
    public float lw = 1.0f;
    [Tooltip("Projectile diameter in meters")]
    public float d = 0.035f;

    [Tooltip("X is the Thickness exponent, Y is the DeMarre constant")]
    public Vector2 DeMarreeCoefficients = new Vector2(1.4f, 2000f); //this is the DeMarre coefficients for the projectile, if you don't know what this is then leave it at 0,0

    [Tooltip("Projectile mass in KG")]
    public float m = 4.3f;

    [Tooltip("If true, the projectile will attempt to normalize its trajectory after penetrating angled armor.")]
    public bool BallisticCap = false;

    [Tooltip("Pallet Mass for sub Caliber")]
    public float pM = 0.0f; // default is to not have a pallet

    [Header("Long Rod Settings")]

    [Tooltip("Projectile density in kg/m3")]
    public float rho_p = 750.0f;
    [Tooltip("Projectile Brinell Hardness")]
    public float bhnp = 500.0f;

    [Tooltip("Set the material of the Long Rod")]
    public PenetratorMaterial material = PenetratorMaterial.Steel; //set default to steel


    public bool FinStabilized = false;


    [Header("Non-Long Rod Parameters")]

    [SerializeField] public AnimationCurve ricochetChance; //this is the chance of a ricochet from the impact angle 0-90


    [Tooltip("Set the fuse type if it has one")]
    public FuseType fuseType = FuseType.Impact; // set this to be default
    public float ExplosiveMassKG = 0.0f;
    public enum DelayType //this is for making sure if you want the fuse delay/offset to be instant or simulated
    {
        Fixed,
        Dynamic
    }

    [Tooltip("Does the fuse work fixed (hard set explosion point) or does it work dynamically")]
    public DelayType delayType = DelayType.Fixed;

    [Tooltip("How far the shell tries to go before it explodes in meters")]
    public float targetFuseDistance = 1.0f;

    public bool ShapedCharge = false; //if true then this is a HEAT round

    public Bullet jetPrefab = null;
    public ProjectileDataCodex jetData = null;

    [Tooltip("This is the multiplier added to the time to affect effective thickness, a jet that has traveled longer will be penalized")]
    public float jetDissolveRate = 0.5f;


    bool IsAP; //is this a penetrator
    bool IsHE; //is this explosive



    public int ConvertMaterialIntoCase()
    {
        if (material == PenetratorMaterial.Steel)
        {
            return 2;
        }
        if (material == PenetratorMaterial.DU)
        {
            return 1;
        }
        if (material == PenetratorMaterial.Tungsten)
        {
            return 0;
        }
        return 0;
    }

    public void UpdateCompat() //this will make sure the data will be compatable (prevents APFSDS from having APHE components)
    {
        if (penetratorType == PenetratorType.KE) // we have a kinetic round
        {
            IsAP = true;
        }
        if (IsAP && ExplosiveMassKG > 0.0f) //we have an explosive element
        {
            IsHE = true;
        }
        if (penetratorType == PenetratorType.CH)
        {
            IsAP = false;
            IsHE = true;
            if (ExplosiveMassKG <= 0.0f)
            {
                ExplosiveMassKG = 0.05f; //if the shell is an explosive one and I forgot to put the charge default to 50 grams
            }
        }
        if (projectileType == ProjectileType.longRod)
        {
            IsAP = true;
            IsHE = false; //we cannot have explosive long rods...yet
        }
        if (ShapedCharge)
        {
            targetFuseDistance = 0f;
        }
    }

       
    


}
