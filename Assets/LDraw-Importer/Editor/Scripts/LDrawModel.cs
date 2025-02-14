﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector3 = UnityEngine.Vector3;

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


	public static class PolylineMeshUtil
	{
		/// <summary>
		/// Creates a Mesh that combines multiple polylines as submeshes.
		/// Each polyline (defined as a List of Vector3 points) is added as a separate submesh.
		/// A material is generated for each submesh.
		/// </summary>
		/// <param name="polylines">List of polylines, where each polyline is a List of Vector3 points.</param>
		/// <param name="materials">Output array of materials, one per submesh.</param>
		/// <returns>The combined Mesh with multiple submeshes.</returns>
		public static Mesh CreateMultiplePolylinesMesh(List<List<Vector3>> polylines)
		{
			// Combine all vertices from each polyline into one list.
			List<Vector3> allVertices = new List<Vector3>();
			// List to store the index array for each polyline (each becomes a submesh).
			List<int[]> submeshIndices = new List<int[]>();

			foreach (List<Vector3> polyline in polylines)
			{
				int startIndex = allVertices.Count;
				allVertices.AddRange(polyline);

				// Build an index array for this polyline.
				int[] indices = new int[polyline.Count];
				for (int i = 0; i < polyline.Count; i++)
				{
					indices[i] = startIndex + i;
				}
				submeshIndices.Add(indices);
			}

			// Create the mesh and assign vertices.
			Mesh mesh = new Mesh { name = "CombinedPolylinesMesh" };
			mesh.SetVertices(allVertices);

			// Set the number of submeshes equal to the number of polylines.
			mesh.subMeshCount = submeshIndices.Count;
			for (int i = 0; i < submeshIndices.Count; i++)
			{
				// Use LineStrip topology so each submesh draws a continuous polyline.
				mesh.SetIndices(submeshIndices[i], MeshTopology.LineStrip, i);
			}


			return mesh;
		}

		/// <summary>
		/// Adds polyline data as additional submeshes to an existing mesh.
		/// The existing mesh's vertices and submeshes are preserved, and new submeshes
		/// are appended for each polyline (rendered as a LineStrip).
		/// </summary>
		/// <param name="existingMesh">The original mesh.</param>
		/// <param name="existingMaterials">
		/// The materials corresponding to the existing mesh's submeshes.
		/// Can be null, in which case a default material is used.
		/// </param>
		/// <param name="polylines">
		/// A list of polylines, where each polyline is a List of Vector3 points.
		/// </param>
		/// <param name="combinedMaterials">
		/// Output parameter returning the combined materials array (original materials followed by polyline materials).
		/// </param>
		/// <returns>The new combined Mesh containing both the original geometry and the polyline submeshes.</returns>
		public static Mesh AddPolylinesToExistingMesh(Mesh existingMesh, List<List<Vector3>> polylines)
		{
			// --- Step 1: Retrieve existing mesh data ---
			List<Vector3> vertices = new List<Vector3>(existingMesh.vertices);
			int existingVertexCount = vertices.Count;
			int existingSubmeshCount = existingMesh.subMeshCount;
			List<int[]> existingIndices = new List<int[]>();
			for (int i = 0; i < existingSubmeshCount; i++)
			{
				existingIndices.Add(existingMesh.GetIndices(i));
			}

			// --- Step 2: Append polyline vertices and build their index arrays ---
			List<int[]> polylineIndices = new List<int[]>();
			//Debug.Log("add polylines : " + polylines.Count );
			int startIndex = existingVertexCount;
			foreach (List<Vector3> polyline in polylines)
			{
				vertices.AddRange(polyline);
				int[] indices = new int[polyline.Count];
				//Debug.Log("add polyline count : " + polyline.Count );
				for (int i = 0; i < polyline.Count; i++)
				{
					indices[i] = startIndex + i;
				}
				startIndex += polyline.Count;
				polylineIndices.Add(indices);
			}

			// --- Step 3: Create the new combined mesh ---
			Mesh combinedMesh = new Mesh();
			combinedMesh.name = "CombinedMesh_WithPolylines";
			combinedMesh.SetVertices(vertices);

			// Total submesh count is the sum of existing submeshes and new polyline submeshes.
			int totalSubmeshCount = existingSubmeshCount + polylineIndices.Count;
			combinedMesh.subMeshCount = totalSubmeshCount;

			// Assign existing submeshes (assumed to use Triangles).
			for (int i = 0; i < existingSubmeshCount; i++)
			{
				combinedMesh.SetIndices(existingIndices[i], MeshTopology.Triangles, i);
			}

			if(existingSubmeshCount > 0 ) {
				combinedMesh.RecalculateNormals();
			// Assign polyline submeshes using LineStrip topology.
			for (int i = 0; i < polylineIndices.Count; i++)
			{
				combinedMesh.SetIndices(polylineIndices[i], MeshTopology.LineStrip, existingSubmeshCount + i);
			}

			// Recalculate mesh bounds and normals.
//			combinedMesh.RecalculateNormals();
//			combinedMesh.RecalculateBounds();


			combinedMesh.RecalculateBounds();			}
			return combinedMesh;
		}

	}


    public class LDrawModel
    {
        /// FileFormatVersion 1.0.2;

        #region factory

        public static LDrawModel Create(string name, string path)
        {
			Debug.Log("Create LDrawModel : " + name + " path : " + path );


            if (_models.ContainsKey(name)) {
				Debug.Log("      >> Model already exists : " + name );
				return _models[name];
			}
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
			//Debug.Log("Init LDrawModel : " + name + " serialized : " + serialized );
			
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
        
			//Debug.Log("   in CreateMeshGameObject : " + _Name );

            var triangles = new List<int>();
            var verts = new List<Vector3>();

			var polylines = new List<List<Vector3>>();
			List<Vector3> lines = null;
		
			

			LDrawPart partCommand = null;
        

			//Debug.Log( "   _Commands.Count : " + _Commands.Count );
			bool isLineStarted = false;
            for (int i = 0; i < _Commands.Count; i++)
            {
                //var sfCommand = _Commands[i] as LDrawSubFile;
				switch( _Commands[i].GetCommandType() ) {
					case CommandType.Line:
					case CommandType.OptionalLine:
						var lineCommand = _Commands[i] as LDrawLine;
						if(lineCommand != null ) {
							if( !isLineStarted) {
								//Debug.Log("Line Started : " + lineCommand.GetVert(0) + "  / " + lineCommand.GetVert(1) );

								isLineStarted = true;
								lines = new List<Vector3>
								{
									lineCommand.GetVert(0),
									lineCommand.GetVert(1)
								};
							}
							else {
								Vector3 vStart = lineCommand.GetVert(0);
								Vector3 vLast = lines[lines.Count - 1];
								if( vStart == vLast ) {
									//Debug.Log("Line Continued : " + lineCommand.GetVert(0) + "  / " + lineCommand.GetVert(1) );
									lines.Add( lineCommand.GetVert(1) );
								}
								else {
									//Debug.Log("Line Ended 1 : " + lineCommand.GetVert(0) + "  / " + lineCommand.GetVert(1) );
									polylines.Add(lines);

									lines = new List<Vector3>
									{
										lineCommand.GetVert(0),
										lineCommand.GetVert(1)
									};
									
									//Debug.Log( "   last line vert 0 : " + polylines[ polylines.Count - 1 ][0] );
									//Debug.Log( "   last line vert 1 : " + polylines[ polylines.Count - 1 ][1] );
								}
							}

							if( i == _Commands.Count - 1 ) {
								//Debug.Log("Line Ended 3 : " + lines[0] + "  / " + lines[1] );
								polylines.Add(lines);
							}
						}
						break;
					
					default:
					case CommandType.Triangle:
					case CommandType.Quad:
						if( isLineStarted ) {
							//Debug.Log("Line Ended 2 : " + lines[0] + "  / " + lines[1] );
							polylines.Add(lines);
							isLineStarted = false;
						}
						var trinalgeCommand = _Commands[i] as LDrawTriangle;
						if(trinalgeCommand != null ) {
							trinalgeCommand.PrepareMeshData(triangles, verts);
						}

						var quadCommand = _Commands[i] as LDrawQuad;
						if(quadCommand != null ) {
							quadCommand.PrepareMeshData(triangles, verts);
						}
						
					
						break;
					case CommandType.SubFile:
						var sfCommand = _Commands[i] as LDrawSubFile;
						sfCommand.GetModelGameObject(go.transform);
						break;

					case CommandType.PartDesc:
						partCommand = _Commands[i] as LDrawPart;
						if(partCommand != null) {
							break;
						}
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

			GameObject visualGO = null;
			MeshFilter meshFilter = null;
			if (verts.Count > 0 || polylines.Count > 0)
			{
				visualGO = new GameObject("mesh");
				visualGO.transform.SetParent(go.transform);
				meshFilter = visualGO.AddComponent<MeshFilter>();
		
				//Debug.Log("PrepareMesh : " + verts.Count + "  / " + triangles.Count + "  / " + polylines.Count );
				meshFilter.sharedMesh = PrepareMesh(verts, triangles, polylines); // save mesh to disk
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
				//Debug.Log("GameObject name : " + go.name );
				//Mesh mergedMesh = MergeChildrenMeshes.Merge(go);
        		//Debug.Log("Merged mesh has " + mergedMesh.vertexCount + " vertices.");				
			}

            return go;
        }
        private Mesh PrepareMesh(List<Vector3> verts, List<int> triangles, List<List<Vector3>> polylines )
        {  
            
            Mesh mesh = LDrawConfig.Instance.GetMesh(_Name);
            if (mesh != null)
				return mesh;
            
          
            mesh = new Mesh();
      
            mesh.name = _Name;
            var frontVertsCount = verts.Count;

			if( verts.Count > 0 ) {
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
				
				Debug.Log("Name : " + _Name + "  / verts.Count : " + verts.Count + "  / triangles.Count : " + triangles.Count );
				mesh.SetVertices(verts);
				mesh.SetTriangles(triangles, 0);
				
				mesh.RecalculateNormals();
				mesh.RecalculateBounds();
			}


			if( polylines.Count > 0  ) {

				// if there is a mesh, add the polylines as additional submeshes
				if( verts.Count > 0 ) {

					mesh = PolylineMeshUtil.AddPolylinesToExistingMesh(mesh, polylines );
				}
				else {
					// if there is no mesh, create a new mesh with the polylines
					mesh = PolylineMeshUtil.CreateMultiplePolylinesMesh(polylines);
				}

			}

            mesh.name = _Name;
            LDrawConfig.Instance.SaveMesh(mesh);
            return mesh;
        }
  
        #endregion

        private LDrawModel()
        {
            
        }
    }
}