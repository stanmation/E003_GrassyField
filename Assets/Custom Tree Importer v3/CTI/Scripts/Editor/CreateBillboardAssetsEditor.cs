#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;

[CustomEditor (typeof(CreateBillboardAssets))]
public class CreateBillboardAssetsEditor : Editor {
	// Serialize
	private SerializedObject CreateBillboardAssets;
    private SerializedProperty objectToRender;
    private SerializedProperty useVFACE;
    private SerializedProperty translucencyPower;
    private SerializedProperty userScale;
    private SerializedProperty yOffset;
    private SerializedProperty BillboardPath;

    private SerializedProperty BillboardAsset;
    private SerializedProperty AlbedoPath;
    private SerializedProperty NormalPath;

    float inspectorWidth = 256.0f;

	public override void OnInspectorGUI () {
        CreateBillboardAssets = new SerializedObject(target);
        GetProperties();
        CreateBillboardAssets script = (CreateBillboardAssets)target;

		GUILayout.Space(10);
        EditorGUILayout.BeginVertical("Box");
            GUILayout.Space(5);
            EditorGUILayout.PropertyField(objectToRender, new GUIContent("Tree to render") );
            GUILayout.Space(5);
        if (!objectToRender.objectReferenceValue)
        {
            EditorGUILayout.HelpBox("Please assign a tree to the slot above to get started.", MessageType.Warning);
            GUI.enabled = false;
        }
        else
        {
            GUI.enabled = true;
        }
		    EditorGUILayout.PropertyField(useVFACE, new GUIContent("Single sided leaves") );
		    EditorGUILayout.PropertyField(translucencyPower, new GUIContent("Power for Translucency Mask") );
            GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Center Tree", GUILayout.Height(30))) {
	            userScale.floatValue = 1.0f;
	            yOffset.floatValue = 0.0f;
	            script.CenterTree();
	            script.renderNormal = false;
	            script.CreateBillbordTextures();
	        }
			if (GUILayout.Button("Create Billboard Assets", GUILayout.Height(30) )) {
				script.CreateBillBoardAsset();
			}
		EditorGUILayout.EndHorizontal();

/*		GUILayout.Space(4);
        EditorGUILayout.BeginVertical();
            EditorGUILayout.PropertyField(userScale, new GUIContent("Scale") );
            EditorGUILayout.PropertyField(yOffset, new GUIContent("Y-Offset") );
            // update camera position
//            script.ShiftCamera();
            GUILayout.Space(4);
        EditorGUILayout.EndVertical();
        */

		GUILayout.Space(8);
		EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Render Albedo Atlas" )) {
				script.renderNormal = false;
				script.CreateBillbordTextures();
			}
			if (GUILayout.Button("Render Normal Atlas" )) {
				script.renderNormal = true;
				script.CreateBillbordTextures();
			}
			GUILayout.Space(10);
			if (GUILayout.Button("Save Texture" )) {
				if (script.renderNormal) {
					saveNormalTexture();
				}
				else {
					saveAlbedoTexture();	
				}
			}
		EditorGUILayout.EndHorizontal();

		GUILayout.Space(8);
		EditorGUILayout.PropertyField(BillboardAsset, new GUIContent("Current Billboard Asset") );
		
		GUILayout.Space(4);
		EditorGUILayout.BeginHorizontal();
			EditorGUILayout.BeginVertical();
				EditorGUILayout.LabelField("Texture Atlas");
                if (script.finalTexture)
                {
                    Rect myRect = EditorGUILayout.GetControlRect(true);
                    // As myRect.width alternates between 1 and the actual width:
                    if (myRect.width > 1.0f)
                    {
                        inspectorWidth = myRect.width;
                    }
                    //  Repaint();
                    Rect PosRect = GUILayoutUtility.GetRect(inspectorWidth, inspectorWidth);
                    PosRect.y -= 10.0f;
                    EditorGUI.DrawPreviewTexture(PosRect, script.finalTexture);
                }
                else
                {
                    EditorGUILayout.HelpBox("No atlas created yet.", MessageType.Info);
                }
            EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;



        CreateBillboardAssets.ApplyModifiedProperties();
        EditorUtility.SetDirty(script);

	}

	void saveAlbedoTexture() {
        CreateBillboardAssets script = (CreateBillboardAssets)target;

		// Save texture
		string directory;
		if (BillboardPath.stringValue == "") {
			directory = Application.dataPath;
		}
		else {
			directory = BillboardPath.stringValue;	
		}
		String filePath = EditorUtility.SaveFilePanel("Save Billboard Atlas [Albedo]", directory, "billboard_atlas [Albedo].png", "png");

		if (filePath!=""){
			var bytes = script.finalTexture.EncodeToPNG(); 
			File.WriteAllBytes(filePath, bytes); 
		
			AssetDatabase.Refresh();
			filePath = filePath.Substring(Application.dataPath.Length-6);
			TextureImporter ti2 = AssetImporter.GetAtPath(filePath) as TextureImporter;
			ti2.anisoLevel = 2;
			#if UNITY_5_5_OR_NEWER
				ti2.textureType = TextureImporterType.Default;
				ti2.textureCompression = TextureImporterCompression.Compressed;
				ti2.sRGBTexture = true;
			#else
				ti2.textureType = TextureImporterType.Image;
				ti2.textureFormat = TextureImporterFormat.DXT5;
				ti2.linearTexture = false;
			#endif
			ti2.alphaIsTransparency = true;
			AssetDatabase.ImportAsset(filePath);
			AssetDatabase.Refresh();
            //// store filePath
            //BillboardPath.stringValue = filePath;
            AlbedoPath.stringValue = filePath;
		}
	}

	void saveNormalTexture() {
        CreateBillboardAssets script = (CreateBillboardAssets)target;

        // save texture
        string directory;
		if (BillboardPath.stringValue == "") {
			directory = Application.dataPath;
		}
		else {
			directory = BillboardPath.stringValue;	
		}
		String filePath = EditorUtility.SaveFilePanel("Save Billboard Atlas [Normal] [Trans] [Smoothness]", directory, "billboard_atlas [Normal] [Trans] [Smoothness].png", "png");

		if (filePath!=""){
			var bytes = script.finalTexture.EncodeToPNG(); 
			File.WriteAllBytes(filePath, bytes);
		
			AssetDatabase.Refresh();
			filePath = filePath.Substring(Application.dataPath.Length-6);
			TextureImporter ti2 = AssetImporter.GetAtPath(filePath) as TextureImporter;
			ti2.anisoLevel = 2;
			#if UNITY_5_5_OR_NEWER
				ti2.textureType = TextureImporterType.Default;
				ti2.textureCompression = TextureImporterCompression.Compressed;
				ti2.sRGBTexture = false;
			#else
				ti2.textureType = TextureImporterType.Image;
				ti2.textureFormat = TextureImporterFormat.DXT5;
				ti2.linearTexture = true;
			#endif
			ti2.filterMode = FilterMode.Trilinear;
			//ti2.alphaIsTransparency = true; // breaks in unity 5.4.beta
			AssetDatabase.ImportAsset(filePath);
			AssetDatabase.Refresh();
            //// store filePath
            //BillboardPath.stringValue = filePath;
            NormalPath.stringValue = filePath;
		}
	}

	private void GetProperties() {
		objectToRender = CreateBillboardAssets.FindProperty("objectToRender");
		useVFACE = CreateBillboardAssets.FindProperty("useVFACE");
		translucencyPower = CreateBillboardAssets.FindProperty("translucencyPower");
		userScale = CreateBillboardAssets.FindProperty("userScale");
		yOffset = CreateBillboardAssets.FindProperty("yOffset");
		BillboardPath = CreateBillboardAssets.FindProperty("BillboardPath");

		BillboardAsset = CreateBillboardAssets.FindProperty("m_BillboardAsset");
		AlbedoPath = CreateBillboardAssets.FindProperty("AlbedoPath");
    	NormalPath = CreateBillboardAssets.FindProperty("NormalPath");
	}
}
#endif
