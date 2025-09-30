using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ShotResult
{
    //bullet properties
    public string projectileName;

    public Vector3 impactVelocity;

    public float impactTime;
    public float impactAngle;
    public Vector3 impactLocation;
    public string projectileType;

    public bool fragmented;
    public float timeOfFragment;

    //hit object properies
    public string hitArmor;
    public float thickness;
    public bool HasSpalled;
    public float timeOfSpall;
    public string hitModule;

    public float KineticEnergy;
    public float penetration;
    public float effectiveThickness;

    public bool penetrated;
    public Vector3 exitVelocity;
    public bool Fused;
    public float timeOfFuse;
    public float fusedDistance;

    public bool Detonated;
    public float timeOfDetonation;
    public Vector3 detonationPoint;

 



}


