using System.Collections.Generic;
using UnityEngine;


namespace ArmorSystem.Generator
{

[RequireComponent(typeof(MeshFilter))]
public class ArmorColliderGenerator : MonoBehaviour
{
    [Tooltip("The desired thickness of the generated armor plate in millimeters. This will be converted to meters (1mm = 0.001m).")]
    public float thicknessMM = 20f;

    [Tooltip("List of selected triangle indices. Used by the editor tool to generate new meshes.")]
    public List<int> selectedTriangles = new List<int>();

    [HideInInspector]
    public bool inEditMode = false;
}
}