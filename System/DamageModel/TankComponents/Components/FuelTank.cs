using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FuelTank : Module
{
    public GameObject fireFX;
    public override void Damage(float damage)
    {
        OnDamage();
        DamageRelay damageRelay = lastShot;
        Debug.Log("Fuel Tank Hit Tank on fire");
        if(!damageRelay.OnFire)
            damageRelay.OnFire = true;
            GameObject fire = Instantiate(fireFX, transform.position, transform.rotation, transform);
            Destroy(fire, 120f);

        lastShot = damageRelay;
        UpdateParent(lastShot);
    }

    protected override void OnDamage()
    {
        IsFieldRepaired = false; // Reset repair state on new damage.
    }
}
