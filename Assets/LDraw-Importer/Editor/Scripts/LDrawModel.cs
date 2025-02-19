#define IMPROPERRMATRIX
#define PURE_ROTATION

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;


using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Factorization;

using Vector = MathNet.Numerics.LinearAlgebra.Vector<double>;
using Matrix = MathNet.Numerics.LinearAlgebra.Matrix<double>;




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

        public static LDrawModel Create(string name, string serialized)
        {
			//Debug.Log("Create LDrawModel : " + name );


            if (_models.ContainsKey(name)) {
				//Debug.Log("      >> Model already exists : " + name );
				return _models[name];
			}
            var model = new LDrawModel();
            model.Init(name, serialized);
          
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


		public (Matrix RProper, Vector Normal) DecomposeImproperRotation(Matrix R)
		{
			if (!R.RowCount.Equals(3) || !R.ColumnCount.Equals(3))
				throw new ArgumentException("R must be a 3x3 matrix.");

			// Compute eigenvalues and eigenvectors
			var evd = R.Evd();
			var eigenValues = evd.EigenValues.Real().ToArray();
			var eigenVectors = evd.EigenVectors;

			Console.WriteLine("eigenVectors:");
			Console.WriteLine(eigenVectors.ToString());

			// Find eigenvector corresponding to eigenvalue -1
			int index = Array.FindIndex(eigenValues, val => Math.Abs(val + 1) < 1e-6);
			if (index == -1)
				throw new InvalidOperationException("No eigenvalue -1 found. The matrix might not be an improper rotation matrix.");

			// Extract the normal vector and normalize it
			Vector normal = eigenVectors.Column(index).Normalize(2);

			// Compute the reflection matrix H = I - 2nn^T
			Matrix I = DenseMatrix.CreateIdentity(3);
			Matrix H = I - (2.0 * normal.ToColumnMatrix() * normal.ToRowMatrix());

			// Compute the proper rotation matrix R_rot = R * H
			Matrix RProper = R * H;

			return (RProper, normal);
		}


		public bool IsImproperRotationMatrix(Matrix R)
		{
			// Check if the matrix is 3x3
			if (R.RowCount != 3 || R.ColumnCount != 3)
				throw new ArgumentException("The matrix must be 3x3.");

			// Calculate R * R^T
			var identity = Matrix.Build.DenseIdentity(3);
			Matrix shouldBeIdentity = R * R.Transpose();

			// Check orthogonality: R * R^T should be approximately equal to the identity matrix
			bool isOrthogonal = shouldBeIdentity.Equals(identity);

			// Calculate the determinant of R
			double determinant = R.Determinant();

			Debug.Log( "name : " + _Name + "\n shouldBeIdentity : \n" + shouldBeIdentity
			 + "\n identity : " + identity + "\n is orthogonal : " + isOrthogonal + " \ndeterminant + 1 : " + Math.Abs(determinant + 1));

			// An improper rotation matrix is orthogonal and has a determinant of -1
			return isOrthogonal && Math.Abs(determinant + 1) < 1e-6;
		}

		private Matrix Matrix4x4ToMatrix(Matrix4x4 m)
		{
			Matrix matrix = DenseMatrix.OfArray(new double[,]
			{
				{m.m00, m.m01, m.m02},
				{m.m10, m.m11, m.m12},
				{m.m20, m.m21, m.m22}
			});
			return matrix;
		}

		private (double, double, double, double) GetQuaternionFromMatrix(Matrix R)
		{
			// calculate the quaternion

			double qw, qx, qy, qz, S;

			// get the trace
			double trace = R[0, 0] + R[1, 1] + R[2, 2];
			

			if (trace > 0) {
				// matrix to quaternion
				S = Math.Sqrt(trace + 1) * 2;
				qw = 0.25 * S;

				qx = ( R[2,1] - R[1,2]) / S;
				qy = ( R[0,2] - R[2,0]) / S;
				qz = ( R[1,0] - R[0,1]) / S;
			} else if ((R[0,0] > R[1,1]) && (R[0,0] > R[2,2])) {
				// m00 is the largest
				S = Math.Sqrt(1 + R[0, 0] - R[1, 1] - R[2, 2]) * 2;
				qw = (R[2, 1] - R[1, 2]) / S;
				qx = 0.25 * S;
				qy = (R[0, 1] + R[1, 0]) / S;
				qz = (R[0, 2] + R[2, 0]) / S;

			} else if ((R[1, 1] > R[2, 2])) {
				// R[1,1] is the largest
				S = Math.Sqrt(1 + R[1, 1] - R[0, 0] - R[2, 2]) * 2;
				qw = (R[0, 2] - R[2, 0]) / S;
				qx = (R[0, 1] + R[1, 0]) / S;
				qy = 0.25 * S;
				qz = (R[1, 2] + R[2, 1]) / S;
			} else {
				// R[2,2] is the largest
				S = Math.Sqrt(1 + R[2,2] - R[0,0] - R[1,1]) * 2;
				qw = (R[1, 0] - R[0, 1]) / S;
				qx = (R[0, 2] + R[2, 0]) / S;
				qy = (R[0, 1] + R[1, 0]) / S;
				qz = 0.25f * S;
			}


			// calculate roll, pitch, yaw
			double roll = Math.Atan2(2 * (qw * qx + qy * qz), 1 - 2 * (qx * qx + qy * qy));
			double pitch = Math.Asin(2 * (qw * qy - qz * qx));
			double yaw = Math.Atan2(2 * (qw * qz + qx * qy), 1 - 2 * (qy * qy + qz * qz));

			// set the quaternion
			return (qw, qx, qy, qz);

		}
		private (double, double, double, double) GetQuaternionFromMatrix(Matrix4x4 R)
		{
			// calculate the quaternion

			double qw, qx, qy, qz, S;

			// get the trace
			double trace = R.m00 + R.m11 + R.m22;
			

			if (trace > 0) {
				// matrix to quaternion
				S = 0.5/ Math.Sqrt(trace + 1);
				qw = 0.25 / S;
				qx = ( R.m21 - R.m12) * S;
				qy = ( R.m02 - R.m20) * S;
				qz = ( R.m10 - R.m01) * S;
			} else if ((R.m00 > R.m11) && (R.m00 > R.m22)) {
				// m00 is the largest
				S = Math.Sqrt(1 + R.m00 - R.m11 - R.m22) * 2;
				qw = (R.m21 - R.m12) / S;
				qx = 0.25 * S;
				qy = (R.m01 + R.m10) / S;
				qz = (R.m02 + R.m20) / S;

			} else if ((R.m11 > R.m22)) {
				// R.m11 is the largest
				S = Math.Sqrt(1 + R.m11 - R.m00 - R.m22) * 2;
				qw = (R.m02 - R.m20) / S;
				qx = (R.m01 + R.m10) / S;
				qy = 0.25 * S;
				qz = (R.m12 + R.m21) / S;
			} else {
				// R.m22 is the largest
				S = Math.Sqrt(1 + R.m22 - R.m00 - R.m11) * 2;
				qw = (R.m10 - R.m01) / S;
				qx = (R.m20 + R.m02) / S;
				qy = (R.m01 + R.m10) / S;
				qz = 0.25f * S;
			}


			// calculate roll, pitch, yaw
			double roll = Math.Atan2(2 * (qw * qx + qy * qz), 1 - 2 * (qx * qx + qy * qy));
			double pitch = Math.Asin(2 * (qw * qy - qz * qx));
			double yaw = Math.Atan2(2 * (qw * qz + qx * qy), 1 - 2 * (qy * qy + qz * qz));

			// set the quaternion
			return (qw, qx, qy, qz);

		}
		private (bool, string) CheckAxisAlignment(Vector n, double tol = 1e-6)
		{
			// Normalize the vector
			string axis = "";
			if (n.L2Norm() == 0)
			{
				axis = "Invalid normal vector (zero vector)";
				return (false, axis);
			}

			Vector nNorm = n.Normalize(2);  // L2 normalization
			
			// Standard basis vectors
			Vector eX = Vector.Build.DenseOfArray(new double[] { 1, 0, 0 });
			Vector eY = Vector.Build.DenseOfArray(new double[] { 0, 1, 0 });
			Vector eZ = Vector.Build.DenseOfArray(new double[] { 0, 0, 1 });

			// Compute dot products
			double dX = Math.Abs(nNorm.DotProduct(eX));
			double dY = Math.Abs(nNorm.DotProduct(eY));
			double dZ = Math.Abs(nNorm.DotProduct(eZ));

			// Check alignment and set the axis
			if (Math.Abs(dX - 1) < tol)
			{
				axis = "X-axis";
				return (true, axis);
			}
			if (Math.Abs(dY - 1) < tol)
			{
				axis = "Y-axis";
				return (true, axis);
			}
			if (Math.Abs(dZ - 1) < tol)
			{
				axis = "Z-axis";
				return (true, axis);
			}

			axis = "Not aligned with any primary axis";
			return (false, axis);
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
						var lineCommand = _Commands[i];
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
						LDrawCommand command = _Commands[i];
						if(command != null ) {
							command.PrepareMeshData(triangles, verts);
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

			MeshFilter meshFilter = null;
#if ADD_VISUALGO
			GameObject visualGO = null;
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
#else
			if (verts.Count > 0 || polylines.Count > 0)
			{
				go.AddComponent<MeshFilter>();
		
				meshFilter = go.GetComponent<MeshFilter>();
				
				//Debug.Log("PrepareMesh : " + verts.Count + "  / " + triangles.Count + "  / " + polylines.Count );
				meshFilter.sharedMesh = PrepareMesh(verts, triangles, polylines); // save mesh to disk
				var mr = go.AddComponent<MeshRenderer>();
				if (mat != null)
				{
					mr.sharedMaterial = mat;
				
				}

			}
#endif
			
            //go.transform.ApplyLocalTRS(trs);

#if BY_MATRIX_ROTATION
			{
				// Assume 'm' is your 4x4 transform matrix
				Matrix4x4 m = trs;

				// Extract translation (last column)
				Vector3 position = m.GetColumn(3);

				Vector3 col0 = m.GetColumn(0);
				Vector3 col1 = m.GetColumn(1);
				Vector3 col2 = m.GetColumn(2);

				// Extract scale: the lengths of the first three columns
				Vector3 scale = new Vector3(
					col0.magnitude,
					col1.magnitude,
					col2.magnitude
				);

				// Remove scale from rotation columns to get pure directions
				col0 /= scale.x;
				col1 /= scale.y;
				col2 /= scale.z;

				Matrix4x4 new_m = new Matrix4x4();
				new_m.SetColumn(0, new Vector4(col0.x, col0.y, col0.z, 0f));
				new_m.SetColumn(1, new Vector4(col1.x, col1.y, col1.z, 0f));
				new_m.SetColumn(2, new Vector4(col2.x, col2.y, col2.z, 0f));
				new_m.SetColumn(3, new Vector4(0, 0, 0, 1f));


				// Create the rotation quaternion.
				// Note: Unity's Quaternion.LookRotation expects the forward and upward directions.
				//UnityEngine.Quaternion rotation = UnityEngine.Quaternion.LookRotation(forward, up);
				Quaternion rotation = new_m.rotation;
				if( _Name == "rect2p" && parent.name == "4084" ) {
					// get the rotation from the transform
					// into degrees
					float x = rotation.eulerAngles.x;
					float y = rotation.eulerAngles.y;
					float z = rotation.eulerAngles.z;


					Debug.Log( "trs : " + trs );
					Console.WriteLine( "trs : \n" + trs );
					Debug.Log($"Rotation in degrees - X: {x}, Y: {y}, Z: {z}");
					Console.WriteLine($"  >> Rotation in degrees - X: {x}, Y: {y}, Z: {z}");

				}
				// Now apply to your GameObject's transform
				go.transform.localPosition = position;
				go.transform.localRotation = rotation;
				go.transform.localScale    = scale;

			}
#else
			{
				Quaternion rotation = new Quaternion();
				// Assume 'm' is your 4x4 transform matrix
				Matrix4x4 m = trs;

				// Get the position
				Vector3 position = m.GetColumn(3);
				m.SetColumn(3, new Vector4(0, 0, 0, 1));

				// Get the scale
				Vector3 scale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);

				Matrix4x4 normalized_m = new Matrix4x4();
				// get the transform matrix without scale
				normalized_m.SetColumn(0, m.GetColumn(0) / scale.x);
				normalized_m.SetColumn(1, m.GetColumn(1) / scale.y);
				normalized_m.SetColumn(2, m.GetColumn(2) / scale.z);
				normalized_m.SetColumn(3, new Vector4(0, 0, 0, 1));

				// m is the LDraw model line type 1 transformation matrix
				// calculate the pure rotation matrix given the line type 1 value, from a to i.
				// a is m.m00, i is m.m22

				Matrix rotationMat = Matrix4x4ToMatrix(normalized_m);
				
				if ( IsImproperRotationMatrix(rotationMat) )
				{


					(Matrix properR, Vector normalVec) = DecomposeImproperRotation(rotationMat);

					if( normalVec[0] > 0 ) scale.x = -scale.x;
					if( normalVec[1] > 0 ) scale.y = -scale.y;
					if( normalVec[2] > 0 ) scale.z = -scale.z;

					// if the angle or anglen2 is not 0 or perpendicular
					(bool isAligned, string axis) = CheckAxisAlignment(normalVec);
					if ( !isAligned )
					{
						// modify the meshes
						
						// Copy the mesh from the mesh filter
						MeshFilter innerMeshFilter = go.GetComponent<MeshFilter>();
						if (innerMeshFilter != null) {
							//Mesh meshTransformed = innerMeshFilter.mesh;
							Mesh meshTransformed = Mesh.Instantiate(innerMeshFilter.sharedMesh);
							innerMeshFilter.sharedMesh = meshTransformed;
							meshTransformed.name = go.name + "_transformed";

							Matrix4x4 transformMatrix = trs;
							Vector3[] vertices = meshTransformed.vertices;
							// get the first indices of the submeshes
							int[] subMeshIndices = meshTransformed.GetIndices(0);

							for (int v = 0; v < vertices.Length; v++)
							{
								vertices[v] = transformMatrix.MultiplyPoint3x4(vertices[v]);
							}
							//meshTransformed.vertices = vertices;
							//meshTransformed.RecalculateNormals();
							meshTransformed.RecalculateBounds();

							rotation = Quaternion.identity;
							scale = Vector3.one;

						}
					
					}
					else {
						double qw, qx, qy, qz;
						(qw, qx, qy, qz) = GetQuaternionFromMatrix(normalized_m);
						rotation = new Quaternion((float)qx, (float)qy, (float)qz, (float)qw);

					}

					//normalized_m = Matrix4x4.TRS(position, rotation, scale);

					string msgText =  "IMPROPER ROTATION MATRIX : \n" + _Name + "  / parent : " + parent.name
					+ "\ntrs : " + trs;
					msgText += "\n  normalized_m : \n" + normalized_m;
                    msgText += "\n  rotationMat : \n" + rotationMat;
					msgText += "\n  properR : \n" + properR;
					msgText += "\n  normalVec : (" + normalVec[0] + ", " + normalVec[1] + ", " + normalVec[2] + ")";
					msgText += "\n  is aligned : " + isAligned + "  / axis : " + axis;
					msgText += "\n  scale : " + scale;
					msgText += "\n  rotate X : " + rotation.eulerAngles.x + "  / Y : " + rotation.eulerAngles.y + "  / Z : " + rotation.eulerAngles.z;
					msgText += "\n  position : " + position;
					//msgText += "\n  candidate : " + candidate;

					go.AddComponent<Memo>().memoText = msgText;

					Debug.Log(msgText);


				}
				else
				{
					double qw, qx, qy, qz;
					(qw, qx, qy, qz) = GetQuaternionFromMatrix(normalized_m);
					rotation = new Quaternion((float)qx, (float)qy, (float)qz, (float)qw);

					string msgText =  "PROPER ROTATION MATRIX : \n" + _Name + "  / parent : " + parent.name
					+ "\ntrs : " + trs;
					msgText += "\n  rotationMat : \n" + rotationMat;
					msgText += "\n  normalized_m : \n"
						+ normalized_m.m00 + ", " + normalized_m.m01 + ", " + normalized_m.m02
						+ "\n" + normalized_m.m10 + ", " + normalized_m.m11 + ", " + normalized_m.m12
						+ "\n" + normalized_m.m20 + ", " + normalized_m.m21 + ", " + normalized_m.m22;
                    msgText += "\n  quaternion : (" + qw + ", " + qx + ", " + qy + ", " + qz + ")";
					msgText += "\n  rotation : \n" + rotation;
					msgText += "\n  rotate X : " + rotation.eulerAngles.x + "  / Y : " + rotation.eulerAngles.y + "  / Z : " + rotation.eulerAngles.z;
					

					go.AddComponent<Memo>().memoText = msgText;
				}

				go.transform.localPosition = position;
				go.transform.localRotation = rotation;
				go.transform.localScale    = scale;
			}
#endif


			
			// UnityEngine.Vector4 column3 = trs.GetColumn(3);
			// if( column3[0] == 14.2849 && column3[1] == 20.4009 && column3[2] == -7 ) {
			// 	Debug.Log("trs : " + trs );
			// }

			// get the parent name
			// if( _Name == "rect2p" && parent.name == "4084" ) {
			// 	// get the rotation from the transform
			// 	UnityEngine.Quaternion rotation = go.transform.rotation;

			// 	// into degrees
			// 	float x = rotation.eulerAngles.x;
			// 	float y = rotation.eulerAngles.y;
			// 	float z = rotation.eulerAngles.z;


			// 	Debug.Log( "trs : " + trs );
			// 	Debug.Log($"Rotation in degrees - X: {x}, Y: {y}, Z: {z}");

			// }


        
            go.transform.SetParent(parent);

			if(partCommand != null) {
				// output name of go
				//Debug.Log("GameObject name : " + go.name );
				Console.WriteLine("GameObject name : " + go.name + "  Merge " );
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
#if BACKFACE
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
#endif
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
