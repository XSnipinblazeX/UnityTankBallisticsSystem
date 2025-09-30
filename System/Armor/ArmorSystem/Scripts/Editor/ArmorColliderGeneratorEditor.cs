using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;


namespace ArmorSystem.Generator
{ [CustomEditor(typeof(ArmorColliderGenerator))]
public class ArmorColliderGeneratorEditor : Editor
{
    private ArmorColliderGenerator _generator;
    private MeshFilter _meshFilter;
    private Mesh _mesh;
    private Transform _transform;

    private void OnEnable()
    {
        _generator = (ArmorColliderGenerator)target;
        _meshFilter = _generator.GetComponent<MeshFilter>();
        if (_meshFilter != null)
        {
            // Use sharedMesh to avoid instantiating a new mesh in the editor
            _mesh = _meshFilter.sharedMesh;
        }
        _transform = _generator.transform;
    }

    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields
        DrawDefaultInspector();

        if (_mesh == null)
        {
            EditorGUILayout.HelpBox("This component requires a MeshFilter with a valid mesh.", MessageType.Error);
            return;
        }

        // Button to toggle face selection mode
        string buttonText = _generator.inEditMode ? "Exit Face Selection Mode" : "Enter Face Selection Mode";
        if (GUILayout.Button(buttonText))
        {
            _generator.inEditMode = !_generator.inEditMode;
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();

        // We disable the buttons if no faces are selected
        GUI.enabled = _generator.selectedTriangles.Count > 0;

        if (GUILayout.Button("Generate Collider From Selection"))
        {
            GenerateColliderMesh();
        }

        if (GUILayout.Button("Clear Selection"))
        {
            Undo.RecordObject(_generator, "Clear Face Selection");
            _generator.selectedTriangles.Clear();
            EditorUtility.SetDirty(_generator);
            SceneView.RepaintAll();
        }
        GUI.enabled = true;
    }

    private void OnSceneGUI()
    {
        if (!_generator.inEditMode || _mesh == null)
        {
            return;
        }

        // This prevents deselection of the object when clicking in the scene view
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // Draw a wireframe overlay to make face selection easier
        DrawWireframe();

        // Draw the selected faces with a highlight
        DrawSelectedFaces();

        // Handle mouse clicks for selecting faces
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            // We need to check if the mouse is over the scene view, not some other window
            if (!HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).direction.Equals(Vector3.zero))
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                if (RaycastMesh(ray, out int hitTriangleIndex))
                {
                    Undo.RecordObject(_generator, "Toggle Face Selection");

                    if (_generator.selectedTriangles.Contains(hitTriangleIndex))
                    {
                        _generator.selectedTriangles.Remove(hitTriangleIndex);
                    }
                    else
                    {
                        _generator.selectedTriangles.Add(hitTriangleIndex);
                    }

                    EditorUtility.SetDirty(_generator);
                    Event.current.Use(); // Consume the mouse event to prevent other actions
                }
            }
        }

        // Repaint the scene view to show selection changes immediately.
        // This can be costly, but for a tool like this, responsiveness is key.
        SceneView.RepaintAll();
    }

    private void DrawWireframe()
    {
        // Store the original depth test setting and set it to respect depth.
        // This prevents the wireframe from drawing through the mesh (X-ray effect).
        var originalZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        Vector3[] vertices = _mesh.vertices;
        int[] triangles = _mesh.triangles;

        // A subtle color for the wireframe so it doesn't overpower the view
        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Increased alpha slightly for better visibility

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v1 = _transform.TransformPoint(vertices[triangles[i]]);
            Vector3 v2 = _transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 v3 = _transform.TransformPoint(vertices[triangles[i + 2]]);

            // Draw the three edges of the triangle
            Handles.DrawLine(v1, v2);
            Handles.DrawLine(v2, v3);
            Handles.DrawLine(v3, v1);
        }

        // Restore the original depth test setting
        Handles.zTest = originalZTest;
    }

    private void DrawSelectedFaces()
    {
        Vector3[] vertices = _mesh.vertices;
        int[] triangles = _mesh.triangles;

        Handles.color = new Color(0, 1, 0, 0.4f); // Semi-transparent green

        foreach (int triIndex in _generator.selectedTriangles)
        {
            int rootIndex = triIndex * 3;
            Vector3 v1 = _transform.TransformPoint(vertices[triangles[rootIndex]]);
            Vector3 v2 = _transform.TransformPoint(vertices[triangles[rootIndex + 1]]);
            Vector3 v3 = _transform.TransformPoint(vertices[triangles[rootIndex + 2]]);

            Handles.DrawAAConvexPolygon(v1, v2, v3);
        }
    }

    private bool RaycastMesh(Ray ray, out int hitTriangleIndex)
    {
        hitTriangleIndex = -1;
        if (_mesh == null) return false;

        Vector3[] vertices = _mesh.vertices;
        int[] triangles = _mesh.triangles;
        float minDistance = float.MaxValue;

        // Transform ray to local space to avoid transforming all vertices every frame
        Ray localRay = new Ray(
            _transform.InverseTransformPoint(ray.origin),
            _transform.InverseTransformDirection(ray.direction)
        );

        for (int i = 0; i < triangles.Length / 3; i++)
        {
            int rootIndex = i * 3;
            Vector3 v0 = vertices[triangles[rootIndex]];
            Vector3 v1 = vertices[triangles[rootIndex + 1]];
            Vector3 v2 = vertices[triangles[rootIndex + 2]];

            if (IntersectRayTriangle(localRay, v0, v1, v2, out float distance))
            {
                if (distance < minDistance)
                {
                    minDistance = distance;
                    hitTriangleIndex = i;
                }
            }
        }

        return hitTriangleIndex != -1;
    }

    // Möller–Trumbore intersection algorithm for ray-triangle intersection
    private bool IntersectRayTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float distance)
    {
        distance = 0;
        const float Epsilon = 1e-6f;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 h = Vector3.Cross(ray.direction, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -Epsilon && a < Epsilon)
            return false; // Ray is parallel to the triangle

        float f = 1.0f / a;
        Vector3 s = ray.origin - v0;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0f || u > 1.0f)
            return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(ray.direction, q);

        if (v < 0.0f || u + v > 1.0f)
            return false;

        float t = f * Vector3.Dot(edge2, q);
        if (t > Epsilon)
        {
            distance = t;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generates a volumetric mesh from the selected faces, creating a front, back, and connecting side geometry.
    /// </summary>
    private void GenerateColliderMesh()
    {
        if (_generator.selectedTriangles.Count == 0)
        {
            Debug.LogWarning("No triangles selected to generate a mesh from.");
            return;
        }

        // --- 1. Extract Front Face Data & Calculate Average Normal ---
        var oldVertices = _mesh.vertices;
        var oldTriangles = _mesh.triangles;
        var oldNormals = _mesh.normals;

        var frontVertices = new List<Vector3>();
        var frontTriangles = new List<int>();
        var oldToNewVertexIndexMap = new Dictionary<int, int>();
        var edgeUseCount = new Dictionary<KeyValuePair<int, int>, int>();
        var averageNormal = Vector3.zero;

        // Process selected triangles to build the front face
        foreach (var triIndex in _generator.selectedTriangles)
        {
            int rootIndex = triIndex * 3;
            // Calculate the normal for this triangle to contribute to the average
            Vector3 triNormal = (oldNormals[oldTriangles[rootIndex]] +
                                 oldNormals[oldTriangles[rootIndex + 1]] +
                                 oldNormals[oldTriangles[rootIndex + 2]]).normalized;
            averageNormal += triNormal;

            var currentTriangleIndices = new int[3];
            for (int i = 0; i < 3; i++)
            {
                int oldVertexIndex = oldTriangles[rootIndex + i];
                if (!oldToNewVertexIndexMap.TryGetValue(oldVertexIndex, out int newVertexIndex))
                {
                    newVertexIndex = frontVertices.Count;
                    frontVertices.Add(oldVertices[oldVertexIndex]);
                    oldToNewVertexIndexMap.Add(oldVertexIndex, newVertexIndex);
                }
                currentTriangleIndices[i] = newVertexIndex;
            }
            frontTriangles.AddRange(currentTriangleIndices);

            // Track edge usage to find the outline for stitching sides later
            AddEdge(currentTriangleIndices[0], currentTriangleIndices[1], edgeUseCount);
            AddEdge(currentTriangleIndices[1], currentTriangleIndices[2], edgeUseCount);
            AddEdge(currentTriangleIndices[2], currentTriangleIndices[0], edgeUseCount);
        }

        averageNormal.Normalize();
        float thicknessInMeters = _generator.thicknessMM / 1000.0f;

        // --- 2. Create Back Face Data ---
        var backVertices = new List<Vector3>();
        foreach (var vert in frontVertices)
        {
            // Offset back along the INVERSE of the average normal
            backVertices.Add(vert - averageNormal * thicknessInMeters);
        }

        var backTriangles = new List<int>();
        for (int i = 0; i < frontTriangles.Count; i += 3)
        {
            // Add triangles with reversed winding order to face outwards
            backTriangles.Add(frontTriangles[i]);
            backTriangles.Add(frontTriangles[i + 2]);
            backTriangles.Add(frontTriangles[i + 1]);
        }

        // --- 3. Stitch Sides ---
        var sideTriangles = new List<int>();
        int frontVertexCount = frontVertices.Count;
        foreach (var edge in edgeUseCount)
        {
            if (edge.Value == 1) // An edge used by only one triangle is an outline edge
            {
                int v1_front = edge.Key.Key;
                int v2_front = edge.Key.Value;

                // The indices for back vertices are the same as the front, but will be offset later
                int v1_back = v1_front;
                int v2_back = v2_front;

                // Create two triangles for the side quad. Indices for back vertices must be offset.
                sideTriangles.Add(v1_front);
                sideTriangles.Add(v2_back + frontVertexCount);
                sideTriangles.Add(v2_front);

                sideTriangles.Add(v1_front);
                sideTriangles.Add(v1_back + frontVertexCount);
                sideTriangles.Add(v2_back + frontVertexCount);
            }
        }

        // --- 4. Combine into Final Mesh ---
        var finalVertices = new List<Vector3>();
        finalVertices.AddRange(frontVertices);
        finalVertices.AddRange(backVertices);

        var finalTriangles = new List<int>();
        finalTriangles.AddRange(frontTriangles);
        // Offset back triangle indices to point to the correct vertices in the final list
        for (int i = 0; i < backTriangles.Count; i++)
        {
            finalTriangles.Add(backTriangles[i] + frontVertexCount);
        }
        finalTriangles.AddRange(sideTriangles);

        // --- 5. Create GameObject and Components ---
        // Create and save the new mesh as an asset to ensure it persists
        var newMesh = new Mesh { name = _mesh.name + "_ArmorPlate" };
        newMesh.SetVertices(finalVertices);
        newMesh.SetTriangles(finalTriangles, 0);
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        string originalPath = AssetDatabase.GetAssetPath(_mesh);
        string directory = string.IsNullOrEmpty(originalPath) ? "Assets/GeneratedMeshes" : Path.GetDirectoryName(originalPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        string meshPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, newMesh.name + ".asset"));
        AssetDatabase.CreateAsset(newMesh, meshPath);
        AssetDatabase.SaveAssets();

        // Create a new child GameObject for the armor piece with a unique name.
        string newObjectName = GameObjectUtility.GetUniqueNameForSibling(_generator.transform, _mesh.name + "_ArmorPiece");
        var armorPieceObject = new GameObject(newObjectName);
        armorPieceObject.transform.SetParent(_generator.transform, false);

        // Add MeshFilter and assign the new mesh.
        var meshFilter = armorPieceObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = newMesh;

        // Add a MeshRenderer to make the new mesh visible.
        var meshRenderer = armorPieceObject.AddComponent<MeshRenderer>();

        // Copy the material(s) from the original object to the new child.
        var originalRenderer = _generator.GetComponent<MeshRenderer>();
        if (originalRenderer != null)
        {
            meshRenderer.sharedMaterials = originalRenderer.sharedMaterials;
        }

        // Add MeshCollider and assign the new mesh.
        var meshCollider = armorPieceObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = newMesh;

        // Add the Armor script and set its thickness. this will need to be your own armor script
        var armorComponent = armorPieceObject.AddComponent<Armor>();
        armorComponent.thicknessMM = _generator.thicknessMM;

        // Select the new object to make it obvious it was created/updated
        Selection.activeObject = armorPieceObject;
        EditorUtility.SetDirty(_generator);

        Debug.Log($"Generated and saved new armor plate mesh at: {meshPath}", armorPieceObject);
    }

    /// <summary>
    /// Helper method to count edge usage for finding mesh outlines.
    /// </summary>
    private void AddEdge(int v1, int v2, Dictionary<KeyValuePair<int, int>, int> edgeCount)
    {
        // Ensure the key is always ordered the same way (smaller index first)
        var edge = new KeyValuePair<int, int>(Mathf.Min(v1, v2), Mathf.Max(v1, v2));
        if (edgeCount.ContainsKey(edge))
        {
            edgeCount[edge]++;
        }
        else
        {
            edgeCount[edge] = 1;
        }
    }
}}