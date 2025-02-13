using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LDraw
{
	public class LDrawOptionalLine : LDrawCommand
	{
		public override void PrepareMeshData( List<int> lines, List<Vector3> verts)
		{
			var vertLen = verts.Count;

			//Debug.Log( "LDrawOptionalLine  at vert count : " + vertLen );

			// for (int i = 0; i < 2; i++)
			// {
			// 	lines.Add(vertLen + i);
			// }

			// for (int i = 0; i < _Verts.Length; i++)
			// {
			// 	verts.Add(_Verts[i]);
			// }

			// check the last vertext of the previous line
			// if the last vertext of the previous line is the same as the first vertext of this line
			// then add the last vertext of this line to the previous line
			// otherwise, add two vertices of this line to the vertices list
			if (vertLen > 1)
			{
				if (verts[vertLen - 1] == _Verts[0])
				{
					verts.Add(_Verts[1]);
					lines.Add(vertLen);
				}
				else
				{
					verts.Add(_Verts[0]);
					lines.Add(vertLen);
					verts.Add(_Verts[1]);
					lines.Add(vertLen + 1);
				}
			}
			else
			{
				verts.Add(_Verts[0]);
				lines.Add(vertLen);
				verts.Add(_Verts[1]);
				lines.Add(vertLen + 1);
			}

		}

		public override void Deserialize(string serialized)
		{
			//Debug.Log("LDrawOptionalLines.Deserialize : " + serialized);

			var args = serialized.Split(' ');
			float[] param = new float[6];
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
				new Vector3(param[3], param[4], param[5])
			};
		}

	}
	
}
