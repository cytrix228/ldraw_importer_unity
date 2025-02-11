using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LDraw
{

	public static class MergeChildrenMeshes
	{
		/// <summary>
		/// Merges all child meshes of the given parent GameObject into one mesh and assigns it to the parent's MeshFilter.
		/// Optionally disables the child GameObjects after merging.
		/// </summary>
		/// <param name="parent">The parent GameObject whose child meshes will be merged.</param>
		/// <param name="destroyChildMeshesAfterMerging">If true, disables the child GameObjects after merging.</param>
		/// <returns>The merged mesh.</returns>
		public static Mesh Merge(GameObject parent, bool removeChildMeshesAfterMerging = true)
		{
			// Ensure the parent has a MeshFilter.
			MeshFilter parentMeshFilter = parent.GetComponent<MeshFilter>();
			if (parentMeshFilter == null)
				parentMeshFilter = parent.AddComponent<MeshFilter>();

			// Ensure the parent has a MeshRenderer.
			MeshRenderer parentMeshRenderer = parent.GetComponent<MeshRenderer>();
			if (parentMeshRenderer == null)
				parentMeshRenderer = parent.AddComponent<MeshRenderer>();

			// Gather all child MeshFilters (including nested children).
			MeshFilter[] childMeshFilters = parent.GetComponentsInChildren<MeshFilter>();
			List<CombineInstance> combineInstances = new List<CombineInstance>();

			Material commonMaterial = null;
			foreach (MeshFilter mf in childMeshFilters)
			{
				// Skip the parent's own MeshFilter.
				if (mf.gameObject == parent)
					continue;

				if (mf.sharedMesh == null)
					continue;

				// Retrieve a material from the first child that has one.
				if (commonMaterial == null)
				{
					MeshRenderer childRenderer = mf.GetComponent<MeshRenderer>();
					if (childRenderer != null)
						commonMaterial = childRenderer.sharedMaterial;
				}

				CombineInstance ci = new CombineInstance
				{
					mesh = mf.sharedMesh,
					// Convert the child's local-to-world matrix into the parent's local space.
					transform = parent.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix
				};
				combineInstances.Add(ci);
			}

			// Create a new mesh and combine all child meshes.
			Mesh combinedMesh = new Mesh { name = "CombinedMesh" };
			// Using mergeSubMeshes = true combines all geometry into a single submesh.
			combinedMesh.CombineMeshes(combineInstances.ToArray(), mergeSubMeshes: true, useMatrices: true);

			// Assign the combined mesh to the parent's MeshFilter.
			parentMeshFilter.mesh = combinedMesh;

			// Assign the material to the parent's MeshRenderer if available.
			Debug.Log("commonMaterial : " + commonMaterial);
			if (commonMaterial != null)
				parentMeshRenderer.sharedMaterial = commonMaterial;

			// Remove (destroy) the child GameObjects after merging.
			if (removeChildMeshesAfterMerging)
			{
				foreach (MeshFilter mf in childMeshFilters)
				{
					if (mf.gameObject != parent)
					{
						// In Play mode, use Destroy. In Editor scripts, you may consider DestroyImmediate.
						UnityEngine.Object.DestroyImmediate(mf.gameObject);
						
					}
				}
			}

			return combinedMesh;
		}		
		/// <summary>
		/// (Optional) Removes duplicate vertices from a mesh.
		/// Use this method if you suspect there are overlapping vertices after the merge.
		/// </summary>
		/// <param name="mesh">The mesh to optimize.</param>
		/// <returns>The optimized mesh with duplicate vertices removed.</returns>
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


    public class LDrawModel
    {
        /// FileFormatVersion 1.0.2;

        #region factory

        public static LDrawModel Create(string name, string path)
        {
            if (_models.ContainsKey(name)) return _models[name];
            var model = new LDrawModel();
            model.Init(name, path);
          
            return model;
        }

        #endregion

        #region fields and properties

        private string _Name;
        private List<LDrawCommand> _Commands;
        private List<string> _SubModels;
        private static Dictionary<string, LDrawModel> _models = new Dictionary<string, LDrawModel>();
        
        public string Name
        {
            get { return _Name; }
        }
        #endregion

        #region service methods

        private void Init(string name, string serialized)
        {
            _Name = name;
            _Commands = new List<LDrawCommand>();
            using (StringReader reader = new StringReader(serialized))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Regex regex = new Regex("[ ]{2,}", RegexOptions.None);
                    line = regex.Replace(line, " ").Trim();

                    if (!String.IsNullOrEmpty(line))
                    {
                        var command = LDrawCommand.DeserializeCommand(line, this);
                        if (command != null)
                            _Commands.Add(command);
                    }
                }
            }

            if (!_models.ContainsKey(name))
            {
                _models.Add(name, this);
            }
        }

        public GameObject CreateMeshGameObject(Matrix4x4 trs, Material mat = null, Transform parent = null)
        {
            if (_Commands.Count == 0) return null;
            GameObject go = new GameObject(_Name);
        
            var triangles = new List<int>();
            var verts = new List<Vector3>();

			LDrawPart partCommand = null;
        
            for (int i = 0; i < _Commands.Count; i++)
            {
                var sfCommand = _Commands[i] as LDrawSubFile;
                if (sfCommand == null)
                {
                    _Commands[i].PrepareMeshData(triangles, verts);
                }
                else
                {
                    sfCommand.GetModelGameObject(go.transform);
                }

            }

            for (int i = 0; i < _Commands.Count; i++)
            {
				partCommand = _Commands[i] as LDrawPart;
				if(partCommand != null) {
					break;
				}
			}	
        
            if (mat != null)
            {
                var childMrs = go.transform.GetComponentsInChildren<MeshRenderer>();
                foreach (var meshRenderer in childMrs)
                {
                    meshRenderer.material = mat;
                }
            }
        
            if (verts.Count > 0)
            {
                var visualGO = new GameObject("mesh");
                visualGO.transform.SetParent(go.transform);
                var mf = visualGO.AddComponent<MeshFilter>();
        
                mf.sharedMesh = PrepareMesh(verts, triangles); // save mesh to disk
                var mr = visualGO.AddComponent<MeshRenderer>();
                if (mat != null)
                {
                    mr.sharedMaterial = mat;
                  
                }
            }
            
            go.transform.ApplyLocalTRS(trs);
        
            go.transform.SetParent(parent);

			if(partCommand != null) {
				// output name of go
				Debug.Log("GameObject name : " + go.name );
				Mesh mergedMesh = MergeChildrenMeshes.Merge(go);
        		Debug.Log("Merged mesh has " + mergedMesh.vertexCount + " vertices.");				
			}

            return go;
        }
        private Mesh PrepareMesh(List<Vector3> verts, List<int> triangles)
        {  
            
            Mesh mesh = LDrawConfig.Instance.GetMesh(_Name);
            if (mesh != null) return mesh;
            
          
            mesh = new Mesh();
      
            mesh.name = _Name;
            var frontVertsCount = verts.Count;
            //backface
            verts.AddRange(verts);
            int[] tris = new int[triangles.Count];
            triangles.CopyTo(tris);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int temp = tris[i];
                tris[i] = tris[i + 1];
                tris[i + 1] = temp;
            }

            for (int i = 0; i < tris.Length; i++)
            {
                tris[i] = tris[i] + frontVertsCount;
            }
            triangles.AddRange(tris);
            //end backface
            
            mesh.SetVertices(verts);
            mesh.SetTriangles(triangles, 0);
            
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            LDrawConfig.Instance.SaveMesh(mesh);
            return mesh;
        }
  
        #endregion

        private LDrawModel()
        {
            
        }
    }
}