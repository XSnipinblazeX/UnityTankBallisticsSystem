using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gun : Module
{
    public override void Damage(float damage)
    {
        OnDamage();
        DamageRelay damageRelay = lastShot;
        Debug.Log("Gun Hit Tank firing disabled");
        damageRelay.CanShoot = false;
        lastShot = damageRelay;
        UpdateParent(lastShot);
    }

    protected override void OnDamage()
    {
        IsFieldRepaired = false; // Reset repair state on new damage.
    }
}
