using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VehicleStatus : MonoBehaviour
{

    public bool CanDrive = true;

    public bool CanTraverse = true;

    public bool CanShoot = true;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="caseID">The type of damage 0 affects driving, 1 affects turret control, 2 affects shooting</param>
    public void HurtTank(int caseID)
    {
        switch (caseID)
        {
            case 0:
                CanDrive = false;
                break;
            case 1:
                CanTraverse = false;
                break;
            case 2:
                CanShoot = false;
                break;
        }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="caseID">The type of damage 0 affects driving, 1 affects turret control, 2 affects shooting</param>
    public void RestoreTank(int caseID)
    {
        switch (caseID)
        {
            case 0:
                CanDrive = true;
                break;
            case 1:
                CanTraverse = true;
                break;
            case 2:
                CanShoot = true;
                break;
        }
    }


}
