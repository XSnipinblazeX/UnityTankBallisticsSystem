using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


[Serializable]
public class DamageRelay
{
    public bool Exploded = false;
    public bool OnFire = false;
    public bool CanShoot = true;
    public bool CanDrive = true;
    public bool CanTurret = true;

    public bool CanLoad = true;
}
