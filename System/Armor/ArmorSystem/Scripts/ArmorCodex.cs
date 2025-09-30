using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "UnityTankSystems/ArmorCodex")]
public class ArmorCodex : ScriptableObject
{
    [SerializeField]
    public ArmorData armorData = new ArmorData();
}


