using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//mathematics namespace here
using System;


namespace TerminalBallistics
{

    /// <summary>
    /// This contains all the formulas required in the terminal ballistics regarding calculating penetration for
    /// -Hyper Velocity Rounds 1300 m/s or higher that include
    /// -----Long Rods
    /// -----APDS
    /// -----APCR
    /// -Full Caliber Shots
    /// -HE
    /// -HEAT jet penetration
    /// </summary>
    public static class TungstenPenetrationCalculator
    {
        // Material independent coefficients
        private const float b0 = 0.283f;
        private const float b1 = 0.0656f;

        // Material dependent coefficients (for Tungsten)
        private const float a = 0.921f;
        private const float c0 = 138f;
        private const float c1 = -0.10f;

        /// <summary>
        /// Calculates the penetration depth (P) of a tungsten long rod projectile.
        /// This formula is valid for an obliquity of 0 degrees.
        /// </summary>
        /// <param name="lw">Projectile length (m)</param>
        /// <param name="d">Projectile diameter (m)</param>
        /// <param name="v_i">Impact velocity (m/s)</param>
        /// <param name="rho_p">Projectile density (kg/m^3)</param>
        /// <param name="rho_t">Target density (kg/m^3)</param>
        /// <param name="bhnt">Target Brinell Hardness Number (BHNT)</param>
        /// <returns>Penetration depth (P) in meters.</returns>
        public static float CalculatePenetration(float lw, float d, float v_i, float rho_p, float rho_t, float bhnt)
        {
            // Check for the range of validity
            if (lw / d < 4)
            {
                Debug.LogError("Aspect Ratio (Lw/D) must be >= 4.");
                return 0;
            }

            if (bhnt > 500 || bhnt < 150)
            {
                Debug.LogError("Brinell Hardness Number (BHNT) must be between 150 and 500.");
                return 0;
            }

            // Calculate s^2
            float s2_numerator = c0 + c1 * bhnt;
            float s2 = s2_numerator * bhnt / rho_p;

            // Calculate the penetration
            float p_numerator = a;

            float tanh_denominator = b0 + b1 * (lw / d);
            float tanh_term = (float)System.Math.Tanh(tanh_denominator);

            float p_denominator = tanh_term;

            float exp_numerator = -s2;
            float exp_denominator = v_i * v_i;
            float exp_term = (float)System.Math.Exp(exp_numerator / exp_denominator);

            float p = lw * (p_numerator / p_denominator) * exp_term;

            return p;
        }
    }

    public static class PerforationLimitCalculator
    {
        /// <summary>
        /// Calculates the normalized penetration depth (P_norm) based on a test result.
        /// This formula adjusts for differences in impact angle and Brinell Hardness Number (BHN).
        /// </summary>
        /// <param name="p_test">Penetration depth of the test (m)</param>
        /// <param name="theta_norm">Normalized impact angle (radians)</param>
        /// <param name="theta_test">Test impact angle (radians)</param>
        /// <param name="m">Exponent for the angle ratio</param>
        /// <param name="c0">Coefficient c0</param>
        /// <param name="c1">Coefficient c1</param>
        /// <param name="bhn_norm">Normalized Brinell Hardness Number (BHN)</param>
        /// <param name="bhn_test">Test Brinell Hardness Number (BHN)</param>
        /// <param name="rho">Density of the target material (kg/m^3)</param>
        /// <param name="v_test">Impact velocity of the test (m/s)</param>
        /// <returns>The normalized penetration depth (P_norm) in meters.</returns>
        public static float CalculatePerforationLimit(float p_test, float theta_norm, float theta_test, float m, float c0, float c1, float bhn_norm, float bhn_test, float rho, float v_test)
        {
            // Angle ratio term
            double angleRatio = Math.Cos(theta_norm) / Math.Cos(theta_test);
            double angleTerm = Math.Pow(angleRatio, m);

            // Numerator of the exponent term
            double expNumerator = -(c0 + c1 * bhn_norm) * bhn_norm + (c0 + c1 * bhn_test) * bhn_test;

            // Denominator of the exponent term
            double expDenominator = rho * v_test * v_test;

            // Exponent term
            double expTerm = Math.Exp(expNumerator / expDenominator);

            // Final formula
            float p_norm = p_test * (float)angleTerm * (float)expTerm;

            return p_norm;
        }
    }

    public static class PerforationEquation
    {
        // Material independent coefficients
        private const float b0 = 0.283f;
        private const float b1 = 0.0656f;
        private const float m = -0.224f;

        /// <summary>
        /// Calculates the perforation depth (P) of a long-rod projectile.
        /// The formula accounts for different penetrator materials and impact obliquity.
        /// </summary>
        /// <param name="material">The penetrator material (Tungsten, DU, or Steel).</param>
        /// <param name="lw">Projectile length (m).</param>
        /// <param name="d">Projectile diameter (m).</param>
        /// <param name="v_i">Impact velocity (m/s).</param>
        /// <param name="obliquity">Impact obliquity angle in degrees (0 to 75).</param>
        /// <param name="rho_p">Projectile density (kg/m^3).</param>
        /// <param name="rho_t">Target density (kg/m^3).</param>
        /// <param name="bhnt">Target Brinell Hardness Number (BHNT).</param>
        /// <param name="bhnp">Penetrator Brinell Hardness Number (BHNP), only for steel.</param>
        /// <returns>The perforation depth (P) in meters.</returns>
        public static float CalculatePerforation(int material, float lw, float d, float v_i, float obliquity, float rho_p, float rho_t, float bhnt, float bhnp = 0)
        {
            // Check for range of validity
            if (lw / d < 4)
            {
                Debug.LogError("Aspect Ratio (Lw/D) must be >= 4.");
                return 0;
            }

            float a, c0, c1;
            float s2;

            // Convert obliquity to radians for trigonometric functions
            float obliquityRad = obliquity * Mathf.Deg2Rad;

            switch (material)
            {
                case 0:
                    if (bhnt > 500 || bhnt < 150)
                    {
                        Debug.LogError("Brinell Hardness Number (BHNT) for Tungsten penetrator must be between 150 and 500.");
                        return 0;
                    }
                    a = 0.994f;
                    c0 = 134.5f;
                    c1 = -0.148f;
                    s2 = (c0 + c1 * bhnt) * bhnt / rho_p;
                    break;

                case 1:
                    if (bhnt > 500 || bhnt < 150)
                    {
                        Debug.LogError("Brinell Hardness Number (BHNT) for DU penetrator must be between 150 and 500.");
                        return 0;
                    }
                    a = 0.825f;
                    c0 = 90.0f;
                    c1 = -0.0849f;
                    s2 = (c0 + c1 * bhnt) * bhnt / rho_p;
                    break;

                case 2:
                    if (bhnt > 750 || bhnt < 200)
                    {
                        Debug.LogError("Brinell Hardness Number (BHNT) for Steel penetrator must be between 200 and 750.");
                        return 0;
                    }
                    a = 1.104f;
                    c0 = 9874.0f;
                    float k = 0.3598f;
                    float n = -0.2342f;
                    s2 = (c0 * Mathf.Pow(bhnt, k) * Mathf.Pow(bhnp, n)) / rho_p;
                    break;

                default:
                    Debug.LogError("Invalid penetrator material specified. Use 'Tungsten', 'DU', or 'Steel'.");
                    return 0;
            }

            // Main formula components
            float p_lw_ratio = a / (float)Math.Tanh(b0 + b1 * (lw / d));
            float cos_term = Mathf.Pow(Mathf.Cos(obliquityRad), m);
            float exp_term_numerator = -s2;
            float exp_term_denominator = v_i * v_i;
            float exp_term_sqrt = (float)Math.Sqrt(rho_p / rho_t) * (float)Math.Exp(exp_term_numerator / exp_term_denominator);

            // Final calculation
            float p = lw * p_lw_ratio * cos_term * exp_term_sqrt;

            return p;
        }
    }

    public static class DeMarrePenetration
    {
        /// <summary>
        /// Calculates penetration depth using a variant of the Lanz-Odermatt empirical formula,
        /// which is often used for full-caliber AP projectiles.
        /// </summary>
        /// <param name="deMarreConstant">The empirically-derived constant (K) for the projectile-armor pairing. Should be a large value, often in the thousands.</param>
        /// <param name="bullet">The bullet object, used to access its dynamic properties like current mass and diameter.</param>
        /// <param name="impactVelocity_mps">The impact velocity in meters per second.</param>
        /// <returns>The calculated penetration depth in millimeters (P).</returns>
        public static float CalculatePenetration(float deMarreConstant, GNB.Bullet bullet, float impactVelocity_mps)
        {
            // Formula: P = K_scaled * V^1.43 * m^0.71 * D^-1.07
            // This is a variant of the Lanz-Odermatt equation, not the classic DeMarre formula.

            float projectileDiameter_mm = bullet.codex.projectileData.d * 1000f;
            float projectileMass_kg = bullet.currentMass;

            if (deMarreConstant <= 0 || projectileDiameter_mm <= 0 || projectileMass_kg <= 0 || impactVelocity_mps <= 0)
            {
                if (projectileMass_kg <= 0) return 0f; // Don't log an error if mass is just zero.
                Debug.LogError($"DeMarre input invalid: K={deMarreConstant}, D={projectileDiameter_mm}, m={projectileMass_kg}, V={impactVelocity_mps}");
                return 0f;
            }

            float diameterTerm = Mathf.Pow(projectileDiameter_mm, -1.07f);
            float massTerm = Mathf.Pow(projectileMass_kg, 0.71f);
            float velocityTerm = Mathf.Pow(impactVelocity_mps, 1.43f);

            // The DeMarre K constant is often expressed as a large number (e.g., in the thousands).
            // This formula is balanced around a small decimal constant. We apply a scaling factor
            // to allow using the larger, more conventional K value in the inspector.
            float scaledConstant = deMarreConstant / 10000f;

            // Calculate the final penetration depth
            float penetrationDepth = scaledConstant * diameterTerm * massTerm * velocityTerm;
            // Debug.Log($"DeMarre/Lanz-Odermatt Calc: K={deMarreConstant}, D={projectileDiameter_mm}, m={projectileMass_kg}, V={impactVelocity_mps} => P={penetrationDepth:F1} mm");
            return penetrationDepth;
        }

        /// <summary>
        /// Calculates the new mass of a projectile after penetration, based on its remaining penetration potential.
        /// This is the reverse of the CalculatePenetration formula.
        /// </summary>
        /// <param name="remainingPenetration_mm">The projectile's penetration potential minus the armor it just defeated.</param>
        /// <param name="deMarreConstant">The DeMarre constant (K) for the projectile.</param>
        /// <param name="projectileDiameter_mm">The projectile's diameter in millimeters (D).</param>
        /// <param name="exitVelocity_mps">The projectile's velocity after exiting the armor (V).</param>
        /// <returns>The new, reduced mass of the projectile in kilograms (m). Returns 0 if the projectile is spent.</returns>
        public static float CalculateNewMass(float remainingPenetration_mm, float deMarreConstant, float projectileDiameter_mm, float exitVelocity_mps)
        {
            if (remainingPenetration_mm <= 0 || deMarreConstant <= 0 || projectileDiameter_mm <= 0 || exitVelocity_mps <= 0)
            {
                return 0f; // Projectile is spent if it has no remaining penetration or velocity.
            }

            // Rearranged formula: m = ( P / ( (K/10000) * D^-1.07 * V^1.43 ) ) ^ (1 / 0.71)
            float scaledConstant = deMarreConstant / 10000f;
            float diameterTerm = Mathf.Pow(projectileDiameter_mm, -1.07f);
            float velocityTerm = Mathf.Pow(exitVelocity_mps, 1.43f);

            float denominator = scaledConstant * diameterTerm * velocityTerm;

            // If the denominator is zero or negative, the calculation is invalid, meaning no mass remains.
            if (denominator <= 0.0001f) return 0f;

            float massTerm = remainingPenetration_mm / denominator;

            // 1 / 0.71 is approximately 1.40845
            return Mathf.Pow(massTerm, 1f / 0.71f);
        }
    }

    public static class HighExplosive
    {


        /// <summary>
        /// Calculates RHA penetration based on TNT equivalent mass using linear interpolation
        /// from a predefined data table.
        /// </summary>
        /// <param name="tntEquivalentKg">The mass of the explosive in kilograms (TNT equivalent).</param>
        /// <returns>The calculated penetration depth in millimeters.</returns>
        public static float GetPenetration(float tntEquivalentKg)
        {
            float P = 32.6f * Mathf.Pow(tntEquivalentKg, 0.34f);
            return P;
        }
        public static float HeatPenetration(float caliber, float explosiveMassKg)
        {
            // This is an arbitrary scaling constant. It can be tuned to balance the gameplay.
            // The value of 'k' represents the overall efficiency of the shaped charge liner and explosive.
            const float k = 1.776f;

            // The formula is a simple arbitrary model:
            // Penetration = k * Caliber * sqrt(ExplosiveMass)
            // This models that penetration is directly proportional to the charge's diameter
            // and scales with the square root of the explosive energy.
            float penetration = k * caliber * Mathf.Sqrt(explosiveMassKg);

            return penetration;
        }

    }



    public static class RicochetUtils
    {
        /// <summary>
        /// Calculates a dynamic damping factor based on the impact angle.
        /// A steeper angle (closer to 90 degrees) results in a higher damping factor.
        /// A shallower angle (closer to 0 degrees) results in a lower damping factor.
        /// </summary>
        /// <param name="incomingVelocity">The velocity vector of the projectile before impact.</param>
        /// <param name="surfaceNormal">The normalized vector of the surface hit.</param>
        /// <returns>A float between 0.0 and 1.0 representing the velocity multiplier after impact.</returns>
        public static float CalculateDynamicDamping(Vector3 incomingVelocity, Vector3 surfaceNormal)
        {
            // Get the dot product between the incoming velocity and the surface normal.
            // The dot product will be between 0 (perpendicular) and -1 (parallel, because velocity is opposite the normal).
            float dotProduct = Vector3.Dot(incomingVelocity.normalized, surfaceNormal);

            // Use a high damping factor for steeper angles and a low one for shallower angles.
            // We clamp the dot product to ensure it's in the valid range for our linear mapping.
            // A steeper impact angle (dotProduct closer to 0) will result in a lower velocity multiplier.
            // A shallower impact angle (dotProduct closer to -1) will result in a higher velocity multiplier.
            // For example, at 90 degrees (dotProduct = 0), damping might be 0.5. At 0 degrees (dotProduct = -1), damping might be 0.9.
            float clampedDot = Mathf.Abs(dotProduct);
            float minDamping = 0.5f; // Damping at a 90-degree impact (steepest)
            float maxDamping = 0.9f; // Damping at a 0-degree impact (shallowest)



            return Mathf.Max(Mathf.Lerp(minDamping, maxDamping, 1.0f - clampedDot), 0.1f);
        }
    }

}
