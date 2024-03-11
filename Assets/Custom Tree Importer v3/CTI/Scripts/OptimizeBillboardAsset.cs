#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class OptimizeBillboardAsset : MonoBehaviour {

	public bool GetBillbordAsset = false;
	public BillboardAsset m_BillboardAsset;
	private Vector2[] vertices;

	[Header("Tweak Vertices")]
	[Space(5)]
	[Range (0,1)]
	public float Top_X = 0.0f;
	[Range (0,1)]
	public float Top_Y = 1.0f;
	[Range (0,1)]

	[Space(5)]
	public float Middle_X = 0.0f;
	[Range (0,1)]
	public float Middle_Y = 0.5f;
	[Range (0,1)]

	[Space(5)]
	public float Bottom_X = 0.0f;
	[Range (0,1)]
	public float Bottom_Y = 0.0f;

	void OnEnable() {
		try {
			m_BillboardAsset = GetComponent<BillboardRenderer>().billboard;
		}
		catch {
			Debug.Log("No BillboardAsset found.");
			return;
		}
		if (m_BillboardAsset != null) {
			vertices = m_BillboardAsset.GetVertices();
		}
	}

	void OnValidate () {
		if(GetBillbordAsset == true) {
			GetBillbordAsset = false;
		}
		m_BillboardAsset = GetComponent<BillboardRenderer>().billboard;
		if (m_BillboardAsset == null) {
			return;
		}
		vertices = m_BillboardAsset.GetVertices();

		if (vertices.Length > 0) {
			
			vertices[2].x = Top_X;
			vertices[5].x = 1.0f - Top_X;
			vertices[2].y = Top_Y;
			vertices[5].y = Top_Y;

			vertices[1].x = Middle_X;
			vertices[4].x = 1.0f - Middle_X;
			vertices[1].y = Middle_Y;
			vertices[4].y = Middle_Y;

			vertices[0].x = Bottom_X;
			vertices[3].x = 1.0f - Bottom_X;
			vertices[0].y = Bottom_Y;
			vertices[3].y = Bottom_Y;

			m_BillboardAsset.SetVertices(vertices);

		}
	}
}
#endif