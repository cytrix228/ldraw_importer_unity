using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LDraw
{
	public class LDrawTriangle : LDrawCommand
	{
		public override int PrepareMeshData( List<List<int>> meshes, List<Vector3> verts)
		{
			int iCount = 0;
			var vertLen = verts.Count;

			if(meshes.Count == 0)
			{
				meshes.Add(new List<int>());
			}

			//Console.Write( "Triangle : " );
			for (int i = 0; i < 3; i++)
			{
				int iIndex = vertLen + i;
				meshes[0].Add(iIndex);
				//Console.Write( "[" + iIndex + "]" );
				iCount++;
			}

			int iMeshCount = meshes[0].Count - 3;
			//Console.Write(" M: " + iMeshCount + " V: " + vertLen + " : ");

			// for(int i = 0; i < 3; i++)
			// {
			// 	Console.Write( "[" + meshes[0][iMeshCount + i] + "]" );
			// }
			// Console.WriteLine();

			for (int i = 0; i < _Verts.Length; i++)
			{
				verts.Add(_Verts[i]);
			}

			return iCount;
		}

		public override void Deserialize(string serialized)
		{
			var args = serialized.Split(' ');
			float[] param = new float[9];
			for (int i = 0; i < param.Length; i++)
			{
				int argNum = i + 2;
				if (!Single.TryParse(args[argNum], out param[i]))
				{
					Debug.LogError(
						String.Format(
							"Something wrong with parameters in line drawn command. ParamNum:{0}, Value:{1}",
							argNum,
							args[argNum]));
				}
			}

			_Verts = new Vector3[]
			{
				new Vector3(param[0], param[1], param[2]),
				new Vector3(param[3], param[4], param[5]),
				new Vector3(param[6], param[7], param[8])
			};
		}

	}
	
}
