using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RemoveBowString : MonoBehaviour
{

	float y1 = -0.00505f;
	float y2 = 0.0046f;
	
	void Awake ()
	{
		
		Mesh mesh = GetComponent<MeshFilter>().mesh;
		var trilist = new List<int>();

		for (int i = 0; i < mesh.triangles.Length / 3 ; i++)
		{
			bool drawTriangle = false;
			bool above = false;
			bool below = false;
			
			for (int j = 0; j < 3; j++)
			{

				float y = mesh.vertices[mesh.triangles[i * 3 + j]].y;
				
				if (y >= y1 && y <= y2)
				{
					drawTriangle = true;
					break;
				}

				if (y > y2)
				{
					above = true;
				}
				
				if (y < y1)
				{
					below = true;
				}

			}
			
			if (! (above && below))
			{
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
