using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectPenetrationForm
{
    public static float Penetration(GNB.Bullet bullet, ArmorCodex armorCodex)
    {
        float penetration = 0f;
        var codex = bullet.codex;
        var velocity = bullet.Velocity.magnitude;

        if (codex.projectileData.projectileType == ProjectileData.ProjectileType.longRod) //apfsds
        {
            penetration = TerminalBallistics.PerforationEquation.CalculatePerforation(codex.projectileData.ConvertMaterialIntoCase(), codex.projectileData.lw, codex.projectileData.d, velocity, 0f, codex.projectileData.rho_p, armorCodex.armorData.rho_t, codex.projectileData.bhnp) * 1000f;
        }
        if (codex.projectileData.projectileType == ProjectileData.ProjectileType.fullCaliber || codex.projectileData.projectileType == ProjectileData.ProjectileType.subCaliber) //full caliber AP
        {
            penetration = TerminalBallistics.DeMarrePenetration.CalculatePenetration(codex.projectileData.DeMarreeCoefficients.y, bullet, velocity);
        }

        return penetration;
    }
    
}
