using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LDraw
{
	public class LDrawPrimitive : LDrawCommand
	{
		public override int PrepareMeshData( List<List<int>> meshes, List<Vector3> verts)
		{
			return 0;
		}

		public override void Deserialize(string serialized)
		{
		}

	}
	
}
