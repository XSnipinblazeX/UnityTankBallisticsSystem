using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmmoRack : Module
{
    public override void Damage(float damage)
    {
        OnDamage();
        DamageRelay damageRelay = lastShot;
        Debug.Log("Ammo Rack Hit Tank Detonated");
        damageRelay.Exploded = true;
        damageRelay.OnFire = true;
        damageRelay.CanShoot = false;
        damageRelay.CanDrive = false;
        damageRelay.CanTurret = false;

        lastShot = damageRelay;
        UpdateParent(lastShot);
    }

    protected override void OnDamage()
    {
        IsFieldRepaired = false; // Reset repair state on new damage.
    }
}
