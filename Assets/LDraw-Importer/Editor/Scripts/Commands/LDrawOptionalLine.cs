using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LDraw
{
	public class LDrawOptionalLine : LDrawCommand
	{
		public override int PrepareMeshData( List<List<int>> polylines, List<Vector3> verts)
		{
			var vertLen = verts.Count;

			int iCount = 0;
			int iLineCount = polylines.Count;
			List<int> lines = null;
			if (iLineCount == 0)
			{
				lines = new List<int>();
				polylines.Add(lines);
			}
			else if (iLineCount > 0)
			{
				lines = polylines[iLineCount - 1];
			}
			
			
			if (vertLen > 1)
			{
				if (verts[vertLen - 1] == _Verts[0])
				{
					verts.Add(_Verts[1]);
					lines.Add(vertLen);
					iCount += 1;
				}
				else
				{
					lines = new List<int>();
					polylines.Add(lines);
					verts.Add(_Verts[0]);
					lines.Add(vertLen);
					verts.Add(_Verts[1]);
					lines.Add(vertLen + 1);
					iCount += 2;
				}
			}
			else
			{
				verts.Add(_Verts[0]);
				lines.Add(vertLen);
				verts.Add(_Verts[1]);
				lines.Add(vertLen + 1);
				iCount += 2;
			}

			return iCount;
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
