using UnityEngine;
using System.Collections.Generic;

public class MergeChildrenMeshes : MonoBehaviour
{
    // Option to remove children after merging
    public bool destroyChildMeshesAfterMerging = true;

    void Start()
    {
        // Get or add a MeshFilter and MeshRenderer on the parent.
        MeshFilter parentMeshFilter = GetComponent<MeshFilter>();
        if (parentMeshFilter == null)
            parentMeshFilter = gameObject.AddComponent<MeshFilter>();

        MeshRenderer parentRenderer = GetComponent<MeshRenderer>();
        if (parentRenderer == null)
            parentRenderer = gameObject.AddComponent<MeshRenderer>();

        // List to hold CombineInstance data.
        List<CombineInstance> combineInstances = new List<CombineInstance>();

        // Get all MeshFilters in the children (including nested children).
        MeshFilter[] childMeshFilters = GetComponentsInChildren<MeshFilter>();

        foreach (MeshFilter childMF in childMeshFilters)
        {
            // Skip the parent's own MeshFilter (if it has one)
            if (childMF.gameObject == this.gameObject)
                continue;

            if (childMF.sharedMesh == null)
                continue;

            CombineInstance ci = new CombineInstance();
            ci.mesh = childMF.sharedMesh;
            // Use the child's localToWorldMatrix so vertices are in world positions.
            ci.transform = childMF.transform.localToWorldMatrix;
            combineInstances.Add(ci);
        }

        // Create a new mesh for the parent and combine all child meshes into it.
        Mesh combinedMesh = new Mesh();
        combinedMesh.name = "CombinedMesh";
        combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

        // Optionally, remove duplicate vertices if needed (see note below).
        // combinedMesh = RemoveDuplicateVertices(combinedMesh);

        // Assign the combined mesh to the parent's MeshFilter.
        parentMeshFilter.mesh = combinedMesh;

        // Optionally disable or destroy the child GameObjects so they are no longer rendered.
        if (destroyChildMeshesAfterMerging)
        {
            foreach (MeshFilter childMF in childMeshFilters)
            {
                if (childMF.gameObject != this.gameObject)
                {
                    // Either disable the child or destroy it:
                    childMF.gameObject.SetActive(false);
                    // or, to remove completely:
                    // Destroy(childMF.gameObject);
                }
            }
        }
    }

    // Optional: Function to remove duplicate vertices for optimization.
    // (See the earlier answer for details.)
    public static Mesh RemoveDuplicateVertices(Mesh mesh)
    {
        Vector3[] oldVertices = mesh.vertices;
        int[] oldTriangles = mesh.triangles;

        Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();
        List<Vector3> newVertices = new List<Vector3>();
        List<int> newTriangles = new List<int>();

        for (int i = 0; i < oldTriangles.Length; i++)
        {
            Vector3 vertex = oldVertices[oldTriangles[i]];
            if (!vertexMap.ContainsKey(vertex))
            {
                vertexMap[vertex] = newVertices.Count;
                newVertices.Add(vertex);
            }
            newTriangles.Add(vertexMap[vertex]);
        }

        Mesh newMesh = new Mesh();
        newMesh.vertices = newVertices.ToArray();
        newMesh.triangles = newTriangles.ToArray();
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        return newMesh;
    }
}
