#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System;
using System.IO;

public class CreateBillboardAssets : MonoBehaviour
{
    public GameObject objectToRender;
    public int imageWidth = 256;
    public int imageHeight = 512;

    [Range(0.5f, 2.0f)]
    public float scaleOrthographicSize = 1.0f;
    [Range(-4.0f, 4.0f)]
    public float yOffset = 0.0f;

    [Range(0.5f, 4.0f)]
    public float translucencyPower = 2.0f;

    private float scale = 1.0f;

    [Range(-2.0f, 4.0f)]
    public float userScale = 1.0f;

    public RenderTexture renderedTexture;
    public Texture2D tempTexture;
    public Texture2D finalTexture;

    public bool renderNormal = false;
    public bool copyTranslucency = false;

    public bool useVFACE = false;
	public bool[] isBark = new bool [2];
    public ColorSpace userColorSpace;
    
    public string BillboardPath = "";
    public string BillboardName = "";

    public string AlbedoPath = "";
    public string NormalPath = "";

    public float TextureScale = 1.0f;
    public BillboardAsset m_BillboardAsset;
    public Material m_BillboardMaterial;

//
    public float treeHeight;
    public float treeWidth;

    public GameObject m_Billboard;
    public BillboardRenderer m_BillboardRenderer;

    void Awake()
    {
        Init();
    }

    void OnValidate() {
        Init();
    }

    void Init()
    {
        // Set Wind to 0
        Shader.SetGlobalVector("_Wind", new Vector4(0, 0, 0, 0));
        Shader.SetGlobalVector("_TerrainLODWind", new Vector4(0, 0, 0, 0));
        // Set up lighting
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.reflectionIntensity = 0.0f;
        //
        Camera activeCam = Camera.main;
		activeCam.clearFlags = CameraClearFlags.SolidColor;
		activeCam.backgroundColor = Color.black;
    }

    #if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if(objectToRender) {
        //  Set matrix
            Bounds bounds = objectToRender.GetComponent<MeshFilter>().sharedMesh.bounds;
            //Gizmos.matrix = objectToRender.transform.localToWorldMatrix;;
        //  Draw Bounding Box
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        //  Reset matrix
            Gizmos.matrix = Matrix4x4.identity; 
        }
    }
    #endif



    public void CreateBillboardRenderer() {
        if(m_Billboard == null) {
            m_Billboard = new GameObject();
            m_Billboard.transform.position = objectToRender.transform.position;
            m_Billboard.name = "_BillboardRenderer";
            m_BillboardRenderer = m_Billboard.AddComponent<BillboardRenderer>();

            m_Billboard.AddComponent<OptimizeBillboardAsset>();

            if (m_BillboardAsset != null) {
                m_BillboardRenderer.billboard = m_BillboardAsset;
            }
        }
        else {
            if (m_BillboardAsset != null) {
                m_Billboard.transform.position = objectToRender.transform.position;
                m_BillboardRenderer.enabled = true;
                m_BillboardRenderer.billboard = m_BillboardAsset;
            }   
        }
    }


    public void CreateBillBoardAsset() {

    //  Do we have already a filenam and/or directory?
        string fileName = "billboard_asset";
        if (BillboardName != "") {
            fileName = BillboardName;
        }
        string directory = Application.dataPath; 
        if (BillboardPath != "") {
            directory = BillboardPath;
        }

        string filePath = EditorUtility.SaveFilePanel("Save new billboard asset", directory, fileName, "asset");
        if (filePath!="") {    

        //  Set up the paths
            var pathFull = filePath;
            var pathFull_l = pathFull.ToLower();
            pathFull = pathFull.Substring(pathFull_l.IndexOf("/assets/") + 1);
            directory = Path.GetDirectoryName(pathFull);
            BillboardPath = directory;
            var assetName = Path.GetFileNameWithoutExtension(filePath);
            BillboardName = assetName;

        //  --------------------------------------
        //  Render and save textures
        
        //  Albedo Texture
            renderNormal = false;
            CreateBillbordTextures();

            filePath = directory + "/" + assetName + " [Albedo].png";

            var bytes = finalTexture.EncodeToPNG(); 
            File.WriteAllBytes(filePath, bytes);

            AssetDatabase.Refresh();
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
            //  Store filePath
            AlbedoPath = filePath;

        //  Normal Texture
            renderNormal = true;
            CreateBillbordTextures();

            filePath = directory + "/" + assetName + " [Normal] [Trans] [Smoothness].png";
            
            bytes = finalTexture.EncodeToPNG(); 
            File.WriteAllBytes(filePath, bytes);
            AssetDatabase.Refresh();
            ti2 = AssetImporter.GetAtPath(filePath) as TextureImporter;
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
            //  ti2.alphaIsTransparency = true; // breaks in unity 5.4.beta
            AssetDatabase.ImportAsset(filePath);
            AssetDatabase.Refresh();
            //  Store filePath
            NormalPath = filePath;

        //  --------------------------------------
        //  Create Billboard Asset

            m_BillboardAsset = new BillboardAsset();
    
        //  Vertices
            // bottom
            Vector2[] vertices = new Vector2[6];
            vertices[0] = new Vector2(0.0f, 0.0f);
            vertices[3] = new Vector2(1.0f, 0.0f);
            // center
            vertices[1] = new Vector2(0.0f, 0.5f);
            vertices[4] = new Vector2(1.0f, 0.5f);
            // top
            vertices[2] = new Vector2(0.0f, 1.0f);
            vertices[5] = new Vector2(1.0f, 1.0f);

            m_BillboardAsset.SetVertices(vertices);

        //  Indices
            ushort[] indices = new ushort[] {0,3,4,0,4,1,1,4,5,1,5,2};
            m_BillboardAsset.SetIndices(indices);

        //  UVs
            Vector4[] texcoords = new Vector4[1];
            texcoords[0] = new Vector4(0.0f, 0.0f, 0.25f, 0.5f);
            m_BillboardAsset.SetImageTexCoords(texcoords);

        //  Set Dimensions
            m_BillboardAsset.height = treeHeight;
            m_BillboardAsset.width = treeWidth;
            m_BillboardAsset.bottom = -objectToRender.transform.position.y;

        //  --------------------------------------
        //  Set up Material

            m_BillboardMaterial = new Material(Shader.Find("CTI/LOD Billboard"));
            m_BillboardMaterial.SetColor("_HueVariation",
                objectToRender.transform.GetComponent<Renderer>().sharedMaterials[0].GetColor("_HueVariation")
            );
            m_BillboardMaterial.SetTexture("_MainTex",
                (Texture2D)AssetDatabase.LoadAssetAtPath(AlbedoPath, typeof(Texture2D))
            );
            m_BillboardMaterial.SetTexture("_BumpTex",
                (Texture2D)AssetDatabase.LoadAssetAtPath(NormalPath, typeof(Texture2D))
            );

        //  --------------------------------------
        //  Safe Billboard Material
            
            filePath = directory + "/" + assetName + "_BillboardMaterial.mat";

            Material tempBillboardMaterial = (Material)AssetDatabase.LoadAssetAtPath(filePath, typeof(Material));
            //  A) Update existing asset.
            if (tempBillboardMaterial != null) {
                EditorUtility.CopySerialized(m_BillboardMaterial, tempBillboardMaterial);
            }
            else {
                AssetDatabase.CreateAsset(m_BillboardMaterial, filePath);   
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        //  Assign
            m_BillboardMaterial = (Material)AssetDatabase.LoadAssetAtPath(filePath, typeof(Material));
            m_BillboardAsset.material = m_BillboardMaterial;
            
        //  --------------------------------------
        //  Safe Billboard Asset
            
            filePath = directory + "/" + assetName + "_BillboardAsset.asset";

            BillboardAsset tempBillboard = (BillboardAsset)AssetDatabase.LoadAssetAtPath(filePath, typeof(BillboardAsset));
        //  A) Update existing asset.
            if (tempBillboard != null) {
                EditorUtility.CopySerialized(m_BillboardAsset, tempBillboard);
            }
        //  B) Create a new one.
            else {
                AssetDatabase.CreateAsset(m_BillboardAsset, filePath);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        //  Assign
            m_BillboardAsset = (BillboardAsset)AssetDatabase.LoadAssetAtPath(filePath, typeof(BillboardAsset));
        }

        //  --------------------------------------
        //  Create Billboard Renderer
        
        CreateBillboardRenderer();
    }

    public void CreateBillbordTextures()
    {

    //  Hide the Billboardrenderer in case there is any
        if (m_Billboard != null) {
            m_BillboardRenderer.enabled = false;
        }

        finalTexture = new Texture2D( 1024, 1024, TextureFormat.ARGB32, false );
        if(objectToRender)
            ConvertToImage();

    //  Show the Billboardrenderer in case there is any
        if (m_Billboard != null) {
            m_BillboardRenderer.enabled = true;
        }

    }

    public void ShiftCamera()
    {
        if (objectToRender)
        {
            Camera activeCam = Camera.main;
            // Get the size of object
            Bounds bounds = objectToRender.GetComponent<Renderer>().bounds;
            // Set camera size
            float max_y = (bounds.max.y - bounds.min.y);
            // max_xz is not centered around pivot but bounds center -> asymmetrical trees do not fit
            float max_x = (bounds.max.x - bounds.min.x)     + bounds.center.x * 2.0f;
            float max_z = (bounds.max.z - bounds.min.z)     + bounds.center.z * 2.0f;
            float max_xz = Mathf.Max(max_x, max_z);
            scaleOrthographicSize = Mathf.Max(max_y, (max_xz));
            scale = Mathf.Min(max_y, max_xz) / Mathf.Max(max_y, max_xz) * (2.0f - userScale);
            activeCam.orthographicSize = scaleOrthographicSize * scale;
            activeCam.transform.position = new Vector3(activeCam.transform.position.x, scaleOrthographicSize * scale + yOffset, activeCam.transform.position.z);
        }
    }

    public void CenterTree()
    {
        if (objectToRender)
        {
            Init();

            // Set object to 0, 0, 0
            objectToRender.transform.position = new Vector3(0, 0, 0);
            // Get the size of object
            Bounds bounds = objectToRender.GetComponent<Renderer>().bounds;
            // Now set object to 0, -bottom,0
            objectToRender.transform.position = new Vector3(0, -bounds.min.y, 0);

            // Set up camera
            Camera activeCam = Camera.main;
            activeCam.orthographic = true;
            activeCam.rect = new Rect(0, 0, 1, 1);
            // Place camera
    //      activeCam.transform.position = new Vector3(0, 0, -(bounds.min.z * 2.0f));
            float stepZ = Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z));
            activeCam.transform.position = new Vector3(0, 0, (bounds.center.z + stepZ) * 2.0f);

            // We have to look in direction of -z!
            activeCam.transform.rotation = Quaternion.AngleAxis(-180, Vector3.up);
            // Set clip planes and enclose whole mesh
            activeCam.nearClipPlane = 0.5f;
            activeCam.farClipPlane = activeCam.transform.position.z + 10.0f + bounds.max.z;
            
            // Set camera size
            float max_y = bounds.extents.y;
            // max_xz is not centered around pivot but bounds center -> asymmetrical trees do not fit
            float max_x = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)) * 2.0f;
            float max_z = Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)) * 2.0f;           
            float max_xz = Mathf.Max(max_x, max_z);
            scaleOrthographicSize = Mathf.Max(max_y, (max_xz));
            scaleOrthographicSize *= 1.05f; /* add a little safe guard*/

            treeHeight = scaleOrthographicSize; // * 2.0f;
            treeWidth = scaleOrthographicSize;  

            activeCam.orthographicSize = scaleOrthographicSize; // * scale;
            activeCam.transform.position = new Vector3(activeCam.transform.position.x, activeCam.transform.position.y + scaleOrthographicSize /* scale*/ + yOffset, activeCam.transform.position.z);

        }
    }


    void ConvertToImage()
    {

        Camera activeCam = Camera.main;
        userColorSpace = PlayerSettings.colorSpace;
        PlayerSettings.colorSpace = ColorSpace.Linear;

        CenterTree();

        if (renderNormal) {
            renderedTexture = new RenderTexture(imageWidth, imageHeight, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); 
        }
        else {
            renderedTexture = new RenderTexture(imageWidth, imageHeight, 16, RenderTextureFormat.ARGB32); // RenderTextureReadWrite.sRGB); // 
        }
        renderedTexture.filterMode = FilterMode.Point;
        tempTexture = new Texture2D( imageWidth, imageHeight, TextureFormat.ARGB32, false );
        activeCam.targetTexture = renderedTexture;
        

        Renderer rend = objectToRender.GetComponent<Renderer>();
        Shader[] assignedShaders = new Shader[rend.sharedMaterials.Length];

        if (renderNormal)
        {
            Shader shader1 = Shader.Find("Custom/BillboardRenderNormalShader");
            isBark = new bool[rend.sharedMaterials.Length];
            if (useVFACE)
            {
                shader1 = Shader.Find("Custom/BillboardRenderNormalShader Single Sided");
            }

            for (int i = 0; i < rend.sharedMaterials.Length; i++)
            {
                assignedShaders[i] = rend.sharedMaterials[i].shader;
                if (rend.sharedMaterials[i].shader.name.ToLower().Contains("leaves") || rend.sharedMaterials[i].shader.name.ToLower().Contains("foliage"))
                {
                    isBark[i] = false;
                    rend.sharedMaterials[i].shader = shader1;
                    rend.sharedMaterials[i].SetFloat("_IsBark", 0.0f);
                }
                else if (rend.sharedMaterials[i].shader.name.ToLower().Contains("bark")) { 
                    isBark[i] = true;
                    rend.sharedMaterials[i].shader = shader1;
                    rend.sharedMaterials[i].SetFloat("_IsBark", 1.0f);
                }
                else {
                    Debug.Log("Model does not seem to have the correct shaders assigned.");
                    CleanUp(activeCam);
                    return;
                }
            }
        }
        else {
            Shader shader2 = Shader.Find("Custom/BillboardRenderAlbedoShader");
            isBark = new bool[rend.sharedMaterials.Length];
            if (useVFACE)
            {
                shader2 = Shader.Find("Custom/BillboardRenderAlbedoShader Single Sided");
            }
            for (int i = 0; i < rend.sharedMaterials.Length; i++)
            {
                assignedShaders[i] = rend.sharedMaterials[i].shader;
                if (rend.sharedMaterials[i].shader.name.ToLower().Contains("leaves") || rend.sharedMaterials[i].shader.name.ToLower().Contains("foliage"))
                {
                    isBark[i] = false;
                    rend.sharedMaterials[i].shader = shader2;
                    rend.sharedMaterials[i].SetFloat("_IsBark", 0.0f);
                }
                else if (rend.sharedMaterials[i].shader.name.ToLower().Contains("bark")) {
                    isBark[i] = true;
                    bool useDetails = false;
                    bool useArray = false;
                    if(rend.sharedMaterials[i].GetFloat("_DetailMode") > 0) {
 Debug.Log("use details");                       
                        useDetails = true;
                    }
                    else {
                        useDetails = false;
                    }
                    if(rend.sharedMaterials[i].shader.name.ToLower().Contains("array")) {
                        useArray = true;
                    }
                    else {
                        useArray = false; 
                    }
                    rend.sharedMaterials[i].shader = shader2;
                    rend.sharedMaterials[i].SetFloat("_IsBark", 1.0f);
                //  Enable Details
                    if(useArray) {
                        rend.sharedMaterials[i].SetFloat("_UseArrays", 1.0f);    
                    }
                    else {
                       rend.sharedMaterials[i].SetFloat("_UseArrays", 0.0f);  
                    }
                //  Enable Array
                    if(useDetails) {
                        rend.sharedMaterials[i].SetFloat("_UseDetails", 1.0f);    
                    }
                    else {
                        rend.sharedMaterials[i].SetFloat("_UseDetails", 0.0f); 
                    }
                }
                else {
                    Debug.Log("Model does not seem to have the correct shaders assigned.");
                    CleanUp(activeCam);
                    return;
                }
            }
        }

        int col = 0;
        int row = 1;
        for (int number = 0; number < 8; number++) {
            objectToRender.transform.rotation = Quaternion.AngleAxis(270 + 45 * number, Vector3.up);
            activeCam.Render();        
            RenderTexture.active = renderedTexture;
            // Read pixels
            tempTexture.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            tempTexture.Apply();
            setSprite(col,row);
            if (number == 3) {
                col = 0;
                row = 0;
            }
            else {
                col += 1;
            }
        }
    //  Render Translucency
        if (renderNormal) {
            Color bCol = activeCam.backgroundColor;
            activeCam.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1.0f);
            Shader shader1 = Shader.Find("Custom/BillboardRenderTransShader");
            if (useVFACE)
            {
                shader1 = Shader.Find("Custom/BillboardRenderTransShader Single Sided");
				Debug.Log("using single trans shader ");
            }
            for (int i = 0; i < rend.sharedMaterials.Length; i++) {
                rend.sharedMaterials[i].shader = shader1;
				if (isBark[i]==true) {
					rend.sharedMaterials[i].SetFloat("_IsBark", 1.0f);
				}
				else {
					rend.sharedMaterials[i].SetFloat("_IsBark", 0.0f);
				}
            }
            copyTranslucency = true;
            col = 0;
            row = 1;
            for (int number = 0; number < 8; number++) {
                objectToRender.transform.rotation = Quaternion.AngleAxis(270 + 45 * number, Vector3.up); 
                activeCam.Render();
                RenderTexture.active = renderedTexture;
                // Read pixels
                tempTexture.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
                tempTexture.Apply();
                setSprite(col,row);
                if (number == 3) {
                    col = 0;
                    row = 0;
                }
                else {
                    col += 1;
                }
            }
            copyTranslucency = false;
            activeCam.backgroundColor = bCol;
        }
        // Reset Materials
        for (int i = 0; i < rend.sharedMaterials.Length; i++) {
            rend.sharedMaterials[i].shader = assignedShaders[i];
        }
        // Clean up
        CleanUp(activeCam);
    }

    void CleanUp(Camera activeCam)
    {
        activeCam.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(renderedTexture);
        PlayerSettings.colorSpace = userColorSpace;
        objectToRender.transform.rotation = Quaternion.identity;
    }

    public void setSprite(int col, int row) {
        Camera activeCam = Camera.main;
        int offsetx = col * imageWidth;
        int offsety = row * imageHeight;
        Color bCol = activeCam.backgroundColor;

        for(int y = 0; y < imageHeight; y++)
        {
            for(int x = 0; x < imageWidth; x++)
            {
                Color c = tempTexture.GetPixel(x, y);
                if (renderNormal) {
                    Color normCol = new Color();
                    if(copyTranslucency) {
                        Color sourceCol = finalTexture.GetPixel(x + offsetx, y + offsety);
                        // Shader writes global normal to rgb but we have an argb rendertex!?
                        // color.r = Translucency is renderd as negative depth, so we have to reverse it in order to shift work from the shader
                        // color.b stores smoothness
                        normCol = new Color ( Mathf.Pow( (1.0f - c.r), translucencyPower), sourceCol.g, sourceCol.b, sourceCol.a);
                    }
                    else {
                        normCol = new Color (c.r, c.g, c.b, c.r);
                    }
                    finalTexture.SetPixel(x + offsetx, y + offsety, normCol);
                }
                else {
                    if(c.r == bCol.r && c.g == bCol.g && c.b == bCol.b ) {
                        finalTexture.SetPixel(x + offsetx, y + offsety, new Color(0,0,0,0) ); 
                    }
                    else {
						finalTexture.SetPixel(x + offsetx, y + offsety, new Color (c.r, c.g, c.b, c.a)); //1.0f));    
                    }
                }
            }
        }
        finalTexture.Apply();
    } 
}
#endif