using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Organ : MonoBehaviour
{
    public string organName;
    public float maxHealth;
    public float currentHealth;
    public bool isVital;
    public float damageMultiplier;
    public float largeVesselHitChance;

    // This method will be overridden by specific organ types
    public abstract void TakeDamage(float damage, Human _parent);
}