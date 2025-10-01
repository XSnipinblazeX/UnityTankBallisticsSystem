using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Engine : Module
{
    void Awake()
    {
        if (DemoDamge)
        {
            // Enable demo mode
            Damage(Random.Range(120f, 380f));
        }
    }
    public override void Damage(float damage)
    {
        OnDamage();
        currentHealth -= damage;

        // Safely update the parent vehicle if a damage source is known.
        if (lastShot != null)
        {
            Debug.Log("Driving component hit, tank immobilized");
            lastShot.CanDrive = false;
            UpdateParent(lastShot);
        }
    }

    protected override void OnDamage()
    {
        IsFieldRepaired = false; // Reset repair state on new damage.
    }
}
