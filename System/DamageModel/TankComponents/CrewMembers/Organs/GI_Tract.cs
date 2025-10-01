using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GI_Tract : Organ
{
    public override void TakeDamage(float damage, Human _parent)
    {
        if (Random.Range(0.0f, 1.0f) >= largeVesselHitChance)
        {
            Debug.Log("Major artery was hit");
            damage *= 5f;
        }
        currentHealth -= Mathf.Abs(damage);
        // The heart is a vital organ, if it's destroyed, the human dies instantly.
        if (currentHealth <= 0)
        {
            // Trigger a death event on the parent Human class.
            Debug.Log("Intenstines destroyed. Human is dead.");
            _parent.Die();
        }
        else
        {
            // Apply a severe bleeding or health drain effect.
            Debug.Log("Intenstines hit! Severe damage taken.");
        }


    }
}
