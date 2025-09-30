using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using GNB;


[Serializable]
public class ArmorData
{
    [Serializable]
    public enum RHA_source
    {
        BHN,
        MULT
    }

    public Bullet spallPrefab;
    public ProjectileDataCodex spallData;

    public float BHN = 350f;
    public float rho_t = 7850f; // Density of the armor material in kg/m^3 (default for steel)
    public float kineticMultiplier = 1.0f;
    public float chemicalMultiplier = 0.5f;

    public RHA_source thicknessSource;

    public float spallAngleMultiplier = 1.0f;
    public float spallPowerMultiplier = 1.0f;

    public int maxSpallCount = 100;

    public float spallAngle = 75f;

    public bool CanRicochet = true;
    public bool HarderIsBetter = true;

    private const float DEFAULT_RHA_BHN = 350f;

    private const float BHN_EFFECT_MAX_THICKNESS = 100f;

    private const float BHN_EFFECT_POWER = 0.25f;


    public float GetEffectiveness(bool kinetic, float layerThicknessMm)
    {
        if (thicknessSource == RHA_source.MULT || layerThicknessMm >= 100f)
        {
            if (!kinetic)
            {
                return chemicalMultiplier;
            }
            return kineticMultiplier;
        }
        float num = 1f - layerThicknessMm / 100f;
        float num2 = 0.25f * num;
        float num3 = (HarderIsBetter ? (BHN / 350f) : (350f / BHN));
        return num2 * num3 + (1f - num2);
    }



}
