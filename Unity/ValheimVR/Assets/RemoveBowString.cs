using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RemoveBowString : MonoBehaviour
{

	float maxDistance = 0.00965f;
	
	void Awake ()
	{
		
		Mesh mesh = GetComponent<MeshFilter>().mesh;
		var trilist = new List<int>();

		for (int i = 0; i < mesh.triangles.Length / 3 ; i++)
		{

			bool drawTriangle = false;
			Vector3 v1 = mesh.vertices[mesh.triangles[i * 3]];
			Vector3 v2 = mesh.vertices[mesh.triangles[i * 3 + 1]];
			Vector3 v3 = mesh.vertices[mesh.triangles[i * 3 + 2]];

			if (Vector3.Distance(v1, v2) < maxDistance &&
			    Vector3.Distance(v2, v3) < maxDistance &&
			    Vector3.Distance(v3, v1) < maxDistance) {
				drawTriangle = true;
			}
			
			for (int j = 0; j < 3; j++)
			{
				if (drawTriangle)
				{
					trilist.Add(mesh.triangles[i * 3 + j]);
				}
			}
		}

		GetComponent<MeshFilter>().mesh.triangles = trilist.ToArray();
		
	}
}
