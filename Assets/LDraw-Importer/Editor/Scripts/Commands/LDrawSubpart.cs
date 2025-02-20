using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LDraw
{
	public class LDrawSubpart : LDrawCommand
	{
		public override int PrepareMeshData( List<List<int>> meshes, List<Vector3> verts)
		{
			
			int iCount = 0;
			return iCount;
		}

		public override void Deserialize(string serialized)
		{
		}

	}
	
}
