#if UNITY_EDITOR

using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

internal class AFSTreeMeshPostprocessor : AssetPostprocessor {

    public const string TreeSuffix = "_afsTREE";
    // Shall the script keep vertex color alpha?
    bool bakedAO = false;
    // Use LeafBending by default
    bool LeafBending = true;
    // Add LeafPivots
    bool LeafPivots = false;
    // Mask bending by vertex color blue
    bool useVertexColorBlueAsMask = false;
    // Shall tumble strength be baked relative to the distance to the leaf plan’s pivot?
    bool LengthLeafPivots = false;
    // Do we store the branch axis?
    bool bestBending = false;
    // Shall we applay the LOD shaders and skip meshes according to the given LOD?
    bool isLODTree = false;
    // Shall branches and leaf planes marked using "_xlod" be skipped?
    bool SkipLODMarkedGeometry = false;
    // Unused
    bool MakeSingleSided = false;
    //  Does the bark material support UV2?
    bool hasUV2 = false;
    //  Shall the script merge bark and leaf material?
    bool mergeMaterials = false;
    //  This indicates if the script shall assign the bark array shader
    bool barkHasSubmeshes = false; 
    //  Shall bending values be converted to be compaible with AFS?
    bool convertToAFS = false;
    //  Shal bark meshes be preparedfor tess
    bool prepareTess = false;
    //
    int currentLODLevel = 0;
    
    // Helper method that converts a float in the
    // range(-1..1) to an 8 bit byte in range(0..255)
    byte ConvertByte(float x) {
        x = (x + 1.0f) * 0.5f;      // Bias
        return (byte)(x*255.0f);    // Scale
    }
    // Helper method to emulate CG's frac(x)
    float Frac(float x) {
        return x - Mathf.Floor(x);
    }
    // Helper method that converts a float in the
    // range(-1..1) to a 10 bit int in range(0..1023)
    int PackFloat(float x)
    {
        x = (x + 1.0f) * 0.5f;      // Bias
        return (int)(x*1023.0f);    // Scale      
    }
    // Helper method that converts a float in the
    // range(-1..1) to a 15 bit int in range(0..32767)
    int PackFloat15bit(float x)
    {
        x = (x + 1.0f) * 0.5f;      // Bias
        return (int)(x*32767.0f);   // Scale      
    }
    // Helper method to pack 3 values into 1 float
    float PackToFloat(byte x, byte y, byte z)
    {
      int packedColor = ((int)x << 16) | ((int)y << 8) | (int)z;
      float packedFloat = (float) ( ((double)packedColor) / ((double) (1 << 24)) );  
      return packedFloat;
    }
    // Helper method which extracts the 2/3 digit values from the control tags
    float GetAttributeVal (float val, string objectName, string attribName) {
        if ( objectName.Contains(attribName) ) {
            int index = objectName.IndexOf(attribName, 0);
            string objectname_Remainder;

            try {
                objectname_Remainder = objectName.Substring(index + attribName.Length, 3);
                if (objectname_Remainder[2] != Convert.ToChar(".")) {
                    val = float.Parse(objectname_Remainder) * 0.01f;    
                }
                else {
                    val = float.Parse(objectname_Remainder) * 0.1f; 
                } 
            }
            catch {
                objectname_Remainder = objectName.Substring(index + attribName.Length, 2);
                try {
                    val = float.Parse(objectname_Remainder) * 0.1f;
                }
                catch {
                    return val;
                }
            }
        }
        return val;
    }


//  Fix Material import settings
    public void  OnPreprocessModel () {
        if (assetPath.Contains(TreeSuffix)) {
            var modelImporter = assetImporter as ModelImporter;
            if (modelImporter.materialName != ModelImporterMaterialName.BasedOnMaterialName) {
                Debug.Log("Fixing material names.");
                modelImporter.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                AssetDatabase.Refresh();
            }
        }
    }

//  Process the tree
    public void OnPostprocessModel(GameObject TreeMesh) {
        if (assetPath.Contains(TreeSuffix)) {

        float pow_wind = 1.5f; // 2.0f
        float pow_turbulence = 1.8f; //1.8f;

        float pow_tumble = 0.5f; // matches old sqrt

        float global_leaf_bending_factor = 1.0f;
        float global_wind_factor = 1.0f;
        float global_flutter_factor = 1.0f;

        float global_edge_tess_seam_factor = 1.0f;

        string attrName;

        //  Global settings
            if (assetPath.Contains("_xao")) {
                bakedAO = true;
            }
            if (assetPath.Contains("_xlp")) {
                LeafPivots = true;
            }
            if (assetPath.Contains("_xlprl")) {
                LeafPivots = true;
                LengthLeafPivots = true;
                pow_tumble = GetAttributeVal(1.0f, assetPath, "_xlprl");
            }
            if (assetPath.Contains("_xmvcb")) {
                useVertexColorBlueAsMask = true;
            }
            // currently not supported
            if (assetPath.Contains("_xvface")) {
                MakeSingleSided = true;
            }
            if (assetPath.Contains("_xlt")) {
                LeafPivots = true;
                bestBending = true;
            }
            if (assetPath.Contains("_uv2")) {
                hasUV2 = true;
            }
            if (assetPath.Contains("_xmm")) {
                mergeMaterials = true;
            }
            if (assetPath.Contains("_xafs")) {
                convertToAFS = true;
            }
            if (assetPath.Contains("_xds")) {
                prepareTess = true;
                global_edge_tess_seam_factor = GetAttributeVal(global_edge_tess_seam_factor, assetPath, "_xds");

            //  Tesellation needs UV3 as float3, so we can enable all features on leaves as well
                LeafPivots = true;
                bestBending = true;
            }

        //  Get global control tags with numerical values
            global_leaf_bending_factor = GetAttributeVal(global_leaf_bending_factor, assetPath, "_xlb");
            global_wind_factor = GetAttributeVal(global_wind_factor, assetPath, "_xmw");
            global_flutter_factor = GetAttributeVal(global_flutter_factor, assetPath, "_xef");

            if (assetPath.Contains("_xlod")) {
                isLODTree = true;
                attrName = "_xlod";
                int index = assetPath.IndexOf(attrName, 0);
                string objectname_Remainder = assetPath.Substring(index + attrName.Length, 2);
                currentLODLevel = int.Parse(objectname_Remainder);
                if(currentLODLevel > 0) {
                    SkipLODMarkedGeometry = true;
                //  Disable Tess on higher LODs
                    prepareTess = false;
                }
            }

            string filename = Path.GetFileNameWithoutExtension(assetPath);
            Debug.Log("Processing Tree: " + filename + " | LOD: " + currentLODLevel);
            Debug.Log(assetPath);

            string AssetPath = Path.GetDirectoryName(assetPath);

            Material Barkmat = null;
            Material Barkmat2nd = null;
            Material Leafmat = null;    

            Component[] filters = TreeMesh.GetComponentsInChildren(typeof(MeshFilter));
// TODO
            // Max = trunk height * 2
            float maxY = filters[0].transform.GetComponent<Renderer>().bounds.max.y * 2.0f;

        //  In case we process a lower LOD we have to get a referendce to LOD0 in order to calculate identical branchAxes
            UnityEngine.Object[] goLOD = new UnityEngine.Object[0];

            if (isLODTree && currentLODLevel > 0 ) {
                string lod0FileName = filename;
                int index = lod0FileName.IndexOf("_xlod", 0);
                StringBuilder sb = new StringBuilder(lod0FileName);
                sb[index + 5] = (char)'0';
                sb[index + 6] = (char)'0';
                lod0FileName = sb.ToString();

                string[] folder = new string[] {AssetPath};
                string[] LOD0GUID = AssetDatabase.FindAssets(lod0FileName + " t:GameObject", folder );
                Debug.Log("LOD00 reference found:" + LOD0GUID.Length);

                if (LOD0GUID.Length > 0) {
                //  We have found a refence, so now we load it as object (not as GameObject because this would contain the already flattended mesh!)
                    goLOD = (UnityEngine.Object[]) AssetDatabase.LoadAllAssetsAtPath( AssetDatabase.GUIDToAssetPath(LOD0GUID[0] ) );
                //  We have to get the trunk's height... but this breaks – as the trunk maybe any go...
                    //Mesh trunk = (Mesh) goLOD[1];
                    //maxY = trunk.bounds.max.y * 2.0f;
                }
            } 

//  ////////////////////////////////
//  Set up Bark and Leaf Combine Instances 
//

            int i;
            int bark_count = 0;
            int leaf_count = 0;

            for (i = 0; i < filters.Length; i++) {
                if (!filters[i].transform.name.ToLower().Contains("bounds")) {
                //  We might have a mesh which simply contains the 2nd bark mat but no submeshes                    
                    if (filters[i].GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("bark2nd")) {
                        Debug.Log("Second bark material detected.");
                        barkHasSubmeshes = true;
                    }
                //  Check for bark mat
                    if (filters[i].GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("bark")) {
                        bark_count++;
                        if (Barkmat == null) {
                            Barkmat = filters[i].GetComponent<Renderer>().sharedMaterial;
                        }
                    //  May be we just have some submeshes using the 2nd bark material
                        if (filters[i].GetComponent<MeshFilter>().sharedMesh.subMeshCount > 1 ) {
                            Debug.Log("Submesh using 2nd bark material: " + filters[i].transform.name);
                            barkHasSubmeshes = true;
                        }
                    }
                //  Check for leaf mat
                    else if (filters[i].GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("leaf") || filters[i].GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("leaves")) {
                        leaf_count++;
                        if (Leafmat == null) {
                            Leafmat = filters[i].GetComponent<Renderer>().sharedMaterial;
                        }
                    }
                    else {
                        Debug.Log("Your Model contains too many/too little materials or bark and leaf materials are not named properly.");
                        Debug.Log("First error occured processing: " + filters[i].transform.name);
                        return;
                    }
                }
            }

            CombineInstance[] combineBark = new CombineInstance[bark_count];    
            CombineInstance[] combineLeaf = new CombineInstance[leaf_count];

            bool isLeaf = false;
        //  Reset Counters
            bark_count = -1;
            leaf_count = -1;

            bool has_trunk;
            if (filters[0].GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("bark")) {
                has_trunk = true;
            }
            else {
                has_trunk = false;
            }

//  ////////////////////////////////
//  Process all parent objects
//
            for (i = 0; i < filters.Length; i ++) {
                // exclude bounds
                if (!filters[i].transform.name.ToLower().Contains("bounds")) {                    
                    MeshFilter filter = (MeshFilter)filters[i];
                    Renderer curRenderer  = filters[i].GetComponent<Renderer>();
                    Renderer levelRenderer = curRenderer;
                    Mesh currentMesh = filter.sharedMesh;
                    Vector3[] vertices = currentMesh.vertices;
                    Color[] colors = currentMesh.colors;


                    
                    int LevelCount = 0;
                //  Test: Bark or Leaf Material
                    if ( !filter.GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("bark") ) {
                        isLeaf = true;
                        leaf_count ++;
                    }
                    else {
                        isLeaf = false;
                        bark_count ++;  
                    }
                //  Create vertex color in case there are none
                    if (colors.Length == 0) {
                        Debug.Log("No Vertex Colors found.");
                        colors = new Color[vertices.Length];
                    //  Init colors
                        for (int p = 0; p < vertices.Length; p++) {
                            if(isLeaf) {
                                colors[p] = new Color(0,0,0,1);
                            }
// Mask tessellation – no                           
                            else {
                                colors[p] = new Color(0,0,0,1); 
                            }
                        }
                    }
                    else {
                        Debug.Log("Vertex colors found: " + filters[i].transform.name);
                    }

                    string objectname;
                    float attrPhase = 0.0f;
                    bool useLODFade = false;
                    int LODSkiplevel = -1; // otherwise 
            
                //  Get number of Levels and find Vertex color values 
                    while (levelRenderer.transform.parent.GetComponent<Renderer>() && levelRenderer.transform.parent.transform.parent.GetComponent<Renderer>()) {
                        LevelCount += 1;
                        objectname = levelRenderer.transform.parent.name.ToLower();
                        levelRenderer = levelRenderer.transform.parent.GetComponent<Renderer>();
                    }

                    float U_Obj = 0.0f;
                    float V_Obj = 0.0f;

                    float U_Obj_withoutLast = 0.0f;                    
                    float V_Obj_withoutLast = 0.0f;

                    Vector3 lastParentPivot = Vector3.zero;
                    float lastParentBendfactor = 1.0f;
                    float lastParentTurbulence = 1.0f;

                    float U;
                    float V;
                    float Dist_Rel;
                    float Length;
                    float twigPhase = 1.0f;
                    int y;

                    float perLevelDisplacement = 1.0f;

                //  Needed by nested leaf planes
                    bool hasChildren = false;
                    bool isChild = false;

                    Transform CurrentTransform = curRenderer.transform;
                    Transform ParentTransform = CurrentTransform;
                    
                //  Calc U_Obj (Main Wind) according to parents (per Object)
                    float bendfactor = 1.0f;
                    for (y = 0; y < LevelCount + 1; y++) {    // needs LevelCount + 1!
                        ParentTransform = ParentTransform.parent.transform;
                        objectname = ParentTransform.name.ToLower();
                        bendfactor = GetAttributeVal(1.0f, objectname, "_xw");
                        
                        if (y == 0) {
                            lastParentBendfactor = bendfactor;
                        }
                        
                        if (y < LevelCount) { // or <= ??
                            Length = (CurrentTransform.position - ParentTransform.position).magnitude;
                        }
                        // If trunk -> Main Wind according only to y-Position
                        else {
                            Length = (CurrentTransform.position.y - ParentTransform.position.y);    
                        }
                        Length = Mathf.Abs(Length);
                        U_Obj += Mathf.Pow(Length * bendfactor * global_wind_factor, pow_wind);
                        
                        if (y > 0) {
                            U_Obj_withoutLast += Mathf.Pow(Length * bendfactor * global_wind_factor, pow_wind);
                        }

                        CurrentTransform = ParentTransform;
                    }

                //  Calc V_Obj (Turbulence) according to parents (per Object)
                    CurrentTransform = curRenderer.transform;
                    ParentTransform = curRenderer.transform;
                    float turbulence = 1.0f;

                //  Needed by nested leaf planes
                    if (CurrentTransform.childCount > 0.0f && (
                            CurrentTransform.GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("leaf") ||
                            CurrentTransform.GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("leaves")
                        )) {
                        hasChildren = true;
                    }

                //  As the parent might not have a renderer
                    if (ParentTransform.parent.GetComponent<Renderer>()) {
                        if (isLeaf && (
                                ParentTransform.parent.GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("leaf") ||
                                ParentTransform.parent.GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("leaves") 
                            )) {
                            isChild = true;   
                        }
                    }

                    for (y = 0; y < LevelCount; y++) {
                        ParentTransform  = ParentTransform.parent;
                        objectname = ParentTransform.name.ToLower();
                        
                        turbulence = GetAttributeVal(1.0f, objectname, "_xt");

                        if (y == 0) {
                            lastParentPivot = ParentTransform.position;
                            lastParentTurbulence = turbulence;
                        }

                        Length = (CurrentTransform.position - ParentTransform.position).magnitude;
                    //  Length has to match calculation that is done per vertex later on!!!!!!!!!!!!! That was a nasty bug...
                        if (!isLeaf || LeafBending == false)
                        {
                            Length = Mathf.Abs(Length) * turbulence;
                        }
                        else {
                            // Leaves as children of leaves
                            if ( ParentTransform.GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("leaf") || ParentTransform.GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("leaves") ) {
                                Length = Mathf.Abs(Length) * turbulence * global_leaf_bending_factor;
                            }
                            // As otherwise we would apply it twice! // What does this comment mean?
                            else {
                                Length = Mathf.Abs(Length) * turbulence;
                            }
                        }
                        V_Obj += Length / maxY;

                        if (y > 0) {
                            V_Obj_withoutLast += Length / maxY;
                        }

                        twigPhase = GetAttributeVal(twigPhase, objectname, "_xlp");
                        CurrentTransform = ParentTransform;
                    }

                //  Calculate phase according to parents (per Object)
                    CurrentTransform = curRenderer.transform;
                    ParentTransform = curRenderer.transform;
                    attrPhase = 0.0f;

                    for (y = 0; y < LevelCount; y++) {
                        ParentTransform = ParentTransform.parent;
                        objectname = ParentTransform.name.ToLower();
                        attrPhase += GetAttributeVal(attrPhase, objectname, "_xp");
                        CurrentTransform = ParentTransform;
                    }

//  ////////////////////////////////
//  Add all attributes of the object itself

                    objectname = curRenderer.transform.name.ToLower();
                    bendfactor = 1.0f;
                    turbulence = 1.0f;
                    
                    float objAttrPhase = 0.0f;
                    objAttrPhase = GetAttributeVal(objAttrPhase, objectname, "_xp");

                    bendfactor = GetAttributeVal(bendfactor, objectname, "_xw");
                    turbulence = GetAttributeVal(turbulence, objectname, "_xt");
                    twigPhase = Mathf.Clamp01( GetAttributeVal(twigPhase, objectname, "_xlp") );


                    if (i > 0) {
                        perLevelDisplacement = (LevelCount + 1 ) * 0.3f;
                    }
                    perLevelDisplacement = GetAttributeVal(perLevelDisplacement, objectname, "_xdp");
                    if(perLevelDisplacement != ((LevelCount + 1 ) * 0.3f)) {
                        perLevelDisplacement = 1.0f - perLevelDisplacement;
                    }
                    
                    attrName = "_xlod";
                    if (objectname.Contains(attrName)) {
                        int index = objectname.IndexOf(attrName, 0);
                        string objectname_Remainder = objectname.Substring(index + attrName.Length, 2);
                        LODSkiplevel = int.Parse(objectname_Remainder);
                        if (LODSkiplevel == currentLODLevel + 1) {
                            useLODFade = true;
                        }
                    }

//  ////////////////////////////////
//  Process all Vertices
//
                //  Create UV2
                    Vector2[] UV2 = new Vector2[vertices.Length];
                //  Create UV3
                    List<Vector3> UV3bb = new List<Vector3>();
                    Vector2[] UV3 = new Vector2[vertices.Length];

                //  Tessellation
                    Vector2[] baseUVs = currentMesh.uv;
                    float perLevelEdgeSeamFactor = GetAttributeVal(global_edge_tess_seam_factor, objectname, "_xds");

                //  Smoothed normals
                    float normalSmoothnessFactor = GetAttributeVal(0.0f, objectname, "_xsn");
                    Vector3[] smoothedNormals =  currentMesh.normals;
                    Mesh parentMesh = null;
                    if ( curRenderer.transform.parent.GetComponent<MeshFilter>() != null) {
                        parentMesh = curRenderer.transform.parent.GetComponent<MeshFilter>().sharedMesh;
                    }

                //  In case we process lower LODs we might need max_Dist and branchAxis from LOD 00 in case we deal with leaves.
                //  This will give us a smooth fading even if the bounds of the given mesh between the LODs have changed.

                    float max_Dist = currentMesh.bounds.size.magnitude;
                    Vector3 branchAxis = currentMesh.bounds.center;

                    if (isLeaf && isLODTree && currentLODLevel > 0) {
                        int matchingObjects = 0;
                        List <float> possibleMax_Dist = new List<float>();
                        List <Vector3> possibleAxis = new List<Vector3>();
                        Bounds origBounds = currentMesh.bounds;

                    //  Loop through all objects of LOD 00 and find the matching one
                        foreach (UnityEngine.Object o in goLOD) {
                            UnityEngine.GameObject go = o as GameObject;
                            // We consider that if the names are equal we have a mesh...
                            if (o.name == currentMesh.name) { // depends on where we are in the loop!!!!
                                Mesh refMesh = (Mesh) o;
                                max_Dist = refMesh.bounds.size.magnitude;
                                branchAxis = refMesh.bounds.center;
                                possibleMax_Dist.Add(max_Dist); 
                                possibleAxis.Add(branchAxis);
                                matchingObjects ++;                               
                            }
                        }
                    //  In case we have found several objects with the same name find the best guess
                        if (matchingObjects > 1) {

                            max_Dist = origBounds.size.magnitude;
                            branchAxis = origBounds.center;
                            float distance = 1000.0f;
                            for (int d = 0; d < possibleMax_Dist.Count; d ++) {
                                var tempDistance = Vector3.Distance( origBounds.center, possibleAxis[d]);
                                if(tempDistance < distance) {
                                    max_Dist = possibleMax_Dist[d];
                                    branchAxis = possibleAxis[d];
                                    distance = tempDistance;
                                }
                            }
                        }
                    }

//  ////////////////////////////////
//  Now loop over all vertices
//
                //  Reset CurrentTransform as it points to the parent after the loop
                    CurrentTransform = curRenderer.transform;

                    for (int j = 0; j < vertices.Length; j++) {
                    //  Calc U and V per Vertex according to the local Position
                        Dist_Rel = Mathf.Abs( (vertices[j]).magnitude );
                    //  Mask bending by vertex color blue if enabled by control tag
                        if (!isLeaf) {
                            if (colors[j].b > 0.0f && useVertexColorBlueAsMask) {
                                Dist_Rel = Mathf.Lerp(Dist_Rel, 0.0f, colors[j].b);
                            }
                        }
                    //  If trunk -> Main Wind according only to y-Position
                        if (i == 0 && has_trunk) {                   
                            U = Mathf.Pow(Mathf.Max(0.0f, vertices[j].y) * bendfactor * global_wind_factor, pow_wind);                  
                        }
                        else {

                    //  Do we use vertex color blue? Then fix bending and smooth normals
                            if (!isLeaf && (colors[j].b > 0.0f) && useVertexColorBlueAsMask) {
                            //  Parent is trunk
                                if(LevelCount == 0) {
                                    /*U =  Mathf.Lerp(
                                        Mathf.Pow(Dist_Rel * bendfactor * global_wind_factor, pow_wind) + U_Obj,
                                        Mathf.Pow( Mathf.Max(0.0f, curRenderer.transform.TransformPoint(vertices[j]).y - lastParentPivot.y) * lastParentBendfactor * global_wind_factor, pow_wind),
                                        colors[j].b
                                    );*/ 
                                //  Doing nothing actually is often better...    
                                    U = Mathf.Pow(Dist_Rel * bendfactor * global_wind_factor, pow_wind) + U_Obj;
                                }
                                else {
                                    U =  Mathf.Lerp(
                                        Mathf.Pow(Dist_Rel * bendfactor * global_wind_factor, pow_wind) + U_Obj,
                                        // Mathf.Pow(Length * bendfactor * global_wind_factor, pow_wind);
                                        U_Obj_withoutLast + Mathf.Pow( Vector3.Distance(curRenderer.transform.InverseTransformPoint(lastParentPivot), vertices[j]) * lastParentBendfactor * global_wind_factor, pow_wind),
                                        colors[j].b
                                    );
                                }

                            //  Smooth normals
                                if (parentMesh != null && normalSmoothnessFactor > 0.0f) {
                                    // Debug.Log("normals");
                                    int verticesLength = parentMesh.vertices.Length;
                                    Vector3 closestVertex = parentMesh.vertices[0];
                                    float shortestDistance = 10000;
                                    Transform parentTransform = curRenderer.transform.parent.GetComponent<Transform>();
                                    Vector3 currentVertexInWorldspace = curRenderer.transform.TransformPoint(vertices[j]);
                                    int indexOfClostestVertex = 0;
                                    for (int n = 0; n < verticesLength; n++) {
                                        Vector3 parentVertexInWorldspace = parentTransform.TransformPoint(parentMesh.vertices[n]);
                                        float distance = Vector3.Distance(currentVertexInWorldspace, parentVertexInWorldspace);
                                        if ( distance < shortestDistance) {
                                            shortestDistance = distance;
                                            indexOfClostestVertex = n;
                                        }
                                    }
                                    smoothedNormals[j] = Vector3.Lerp(smoothedNormals[j], parentMesh.normals[indexOfClostestVertex], colors[j].b * normalSmoothnessFactor);
                                    smoothedNormals[j].Normalize();
                                }
                            }

                            else {
                                U = Mathf.Pow(Dist_Rel * bendfactor * global_wind_factor, pow_wind) + U_Obj;
                            }   
                        }

                    //  Only Bark gets 2nd Bending according to the local Position of Vertex
                        // But this would not allow us to get turbulence on e.g. palm leafs as those are directly connected to the trunk
                        // Enable 2nd bending on branches and if it is enable by control tag (default = true)
                        if (!isLeaf) { // || LeafBending == true) {
                            V = Dist_Rel * turbulence / maxY;
                        }
                        else {
                            V = Dist_Rel * turbulence * global_leaf_bending_factor / maxY;
                        }
                    
                        if (!isLeaf && (colors[j].b > 0.0f) && useVertexColorBlueAsMask) {

                        //  Parent is trunk and the trunk does not have any turbulence
                            if(LevelCount == 0) {
                                V += Mathf.Lerp( V_Obj, 0.0f, colors[j].b);
                            }
                            else {
                                V += Mathf.Lerp(
                                        V_Obj,
                                        V_Obj_withoutLast + Vector3.Distance(curRenderer.transform.InverseTransformPoint(lastParentPivot), vertices[j]) * lastParentTurbulence / maxY,
                                        colors[j].b
                                );
                            }
                        }
                        else {
                            V += V_Obj;
                        }                    

                    //  Exclude trunk from 2nd bending
                        if (i == 0 && has_trunk) {
                            V = 0.0f;
                        }
                    //  Set UV2
                        UV2[j] = new Vector2( U / (maxY*2.0f), Mathf.Pow(V, pow_turbulence));

                    //  Set vertex colors
                    //  Handle Bark 
                        if (!isLeaf) {
                        
                    //  Prepare for Tessellation    
                            if (prepareTess) {
                                UV3[j] = Vector2.zero; //new Vector2( -1234.0f, 0.0f); //
                            //  Tessellation needs UV3 as float3
                                var tempUV3 = Vector3.zero;
                                Vector3[] normals =  currentMesh.normals;
                                for (int e = 0; e < vertices.Length; e++ ) {                       
                                    if (e != j) {
                                    //  Compare vertex positions
                                        if (vertices[e] == vertices[j]) {
                                        //  Same position but different u --> seam!
                                            if (baseUVs[e].x != baseUVs[j].x ) {
                                                colors[j].g = Mathf.Clamp01( colors[j].g + (1.0f - Mathf.Clamp01(perLevelEdgeSeamFactor)) );
                                                // we only write it to the right edge of the seam – dominant edge
                                                if(baseUVs[j].x > baseUVs[e].x) {
                                                    tempUV3.x = baseUVs[e].x - baseUVs[j].x;
                                                    tempUV3.y = baseUVs[e].y - baseUVs[j].y;
                                                    tempUV3.z = 1.0f;
                                                } 
                                            }
                                            if (  Vector3.Dot(normals[e], normals[j]) < 0.99f ) {
                                                colors[j].g = 1.0f;
                                                colors[e].g = 1.0f;  
                                            }
                                        }
                                    }
                                }
                            //  Add uv3
                                UV3bb.Add(tempUV3);
                            //  Reduce displacement – but exclude trunk
                                if (i > 0) {
                                    colors[j].g = Mathf.Clamp01(colors[j].g + perLevelDisplacement);
                                }
                            }

                        //  Skip vertex color green if no tess is used
                            else {
                                colors[j].g = 0.0f;
                            }

                            float vertexBlue = 0.0f;
                        //  Bake 2nd bark material to vertex color blue
                            if (filter.GetComponent<Renderer>().sharedMaterial.name.ToLower().Contains("bark2nd")) {
                                vertexBlue = 1.0f;
                            }
                            if (bakedAO) {
                                colors[j] = new Color(attrPhase + objAttrPhase, colors[j].g, vertexBlue, colors[j].a);
                            }
                            else {
                                colors[j] = new Color(attrPhase + objAttrPhase, colors[j].g, vertexBlue, 1.0f);
                            }
                        }
                    
                    //  Handle Leaves
                        else {
                            float blucolfloat = 0.0f;
                            byte bluecolbyte;
                        //  Handle nested leaf planes and mark vertices of parent leaf planes to not tumble – as we do not store 2 pivots
                            if(hasChildren) {
                                // but now it does not fade out if xlod is assigned??????
                                //bluecolbyte = 0; //(byte)1; // must not be 0 ?
                                twigPhase = 8.0f/255.0f; // smalles possible value ????
                            }
                            if (LengthLeafPivots) {
                                bluecolbyte = (byte)(Mathf.Clamp01(twigPhase * Mathf.Pow(Dist_Rel / max_Dist, pow_tumble )) * 255.0f); // integer 0-255 byte
                            }
                            else {
                                bluecolbyte = (byte)(Mathf.Clamp01(twigPhase) * 255.0f); // integer 0-255 byte
                            }
                            uint bluecolint = (uint)(bluecolbyte >> 1);
                            if (useLODFade) {
                                if(!isChild) {
                                    bluecolint += 128; // 127 / was 128 --> changed because of twigphase when hasChildren!!!!
                                }
                                else {
                                    bluecolint += 127; 
                                }
                            }
                            blucolfloat = (float)bluecolint / 255.0f;

                        //  Calculate the final phase for leaves
                            float leafPhase = attrPhase + objAttrPhase * Mathf.Sqrt(Dist_Rel / max_Dist);
                        //  Here was the bug as far as nested leaves are concerned
                        //  "Or" enables full per leaf phase in case lower levels haven't set any – most likely used by plants which do not have "branches" like ferns.
                            if (hasChildren || attrPhase == 0.0) {
                                leafPhase = attrPhase + objAttrPhase;  
                            } 
                            if (bakedAO) {
                                colors[j] = new Color(leafPhase, colors[j].g * global_flutter_factor, blucolfloat, colors[j].a);
                            }
                            else {
                                colors[j] = new Color(leafPhase, colors[j].g * global_flutter_factor, blucolfloat, 1.0f);
                            }
                        }

                    //  Set UV3
                        if (isLeaf && LeafPivots) {
                            // UV3 = direction + length to pivot
                            // IMPORTANT: We have to use World Coords!
                            Vector3 CurrentTransformPos = curRenderer.transform.position; // is in world space
                                                                                          //    Vector3 CurrentPointPos = curRenderer.transform.TransformPoint(vertices[j]); // to world space
                                                                                          // !!! Do not store distance to tree’s pivot, but distance between pivot and 0,0,0
                                                                                          // Vector3 dir = Vector3.Normalize(CurrentPointPos - CurrentTransformPos);
                            Vector3 dir = Vector3.Normalize(CurrentTransformPos);
                            // Compress normal to single float
                            // Scale Bias values to 8 bit bytes in range of 0 to 255
                            // http://forum.unity3d.com/threads/can-i-send-data-as-w-via-vertex-data.114111/

                            /*byte bx = ConvertByte(dir.x);
                            byte by = ConvertByte(dir.y);
                            byte bz = ConvertByte(dir.z);
                            uint  packedByte  = (uint)((bx << 16) | (by << 8) | bz);
                            float packedFloat = (float)(((double)packedByte) / ((double) (1 << 24))); */

                            // Let’s use 10bit precision instead of 8bit
                            // http://pastebin.com/5DHr4BQU             
                            // uint packedByte  = (uint)((PackFloat(dir.x) << 20) | (PackFloat(dir.y) << 10) | PackFloat(dir.z));
                            // float packedFloat = (float)(((double)packedByte) / ((double)(1<<30)));

                            // Let’s use 15bit precision and only 2 components
                            uint packedByte = (uint)((PackFloat15bit(dir.x) << 15) | PackFloat15bit(dir.z));
                            float packedFloat = (float)(((double)packedByte) / ((double)(1 << 30)));

                        //  Calculate Branch Main Axis
                            if (bestBending) {
                                branchAxis = Vector3.Normalize(branchAxis);
                                branchAxis = Vector3.Normalize(curRenderer.transform.TransformDirection(branchAxis));
                            //  Pack all 3 components into one float
                                float packedFloat1  = PackToFloat(ConvertByte(branchAxis.x), ConvertByte(branchAxis.y), ConvertByte(branchAxis.z));
                                UV3bb.Add( new Vector3(packedFloat, CurrentTransformPos.magnitude, packedFloat1) );
                            }
                            else {
                                UV3[j] = new Vector2(packedFloat, CurrentTransformPos.magnitude);
                            }
                        }
                    
                    }

                //  Update current mesh
                    currentMesh.normals = smoothedNormals;
                    currentMesh.colors = colors;

                //  Handle uv2
                    if (hasUV2) {
                        Vector4 [] UV2Vec4 = new Vector4 [currentMesh.vertices.Length];
                        // does the mesh actualy have uv2?
                        if (isLeaf || currentMesh.uv2.Length == 0) { //(currentMesh.uv2.Length == 0) { // isLeaf) { // || 
                           currentMesh.uv2 = new Vector2 [currentMesh.vertices.Length];
                           for (int uvi = 0; uvi < currentMesh.vertices.Length; uvi ++) {
                                currentMesh.uv2[uvi] = new Vector2(1,1); //Vector2.zero;
                           } 
                        } 
                        for (int uvi = 0; uvi < currentMesh.vertices.Length; uvi ++) {
                            UV2Vec4[uvi] = new Vector4( UV2[uvi].x, UV2[uvi].y, currentMesh.uv2[uvi].x, currentMesh.uv2[uvi].y);
                        }     
                        currentMesh.SetUVs(1, UV2Vec4.ToList() ); // that is uv2...
                        Debug.Log("UV2 written.");
                    }
                    else {
                        currentMesh.uv2 = UV2;   
                    }
                    
                    if (isLeaf && LeafPivots) {
                        if (bestBending) {
                            currentMesh.uv3 = null;
                            currentMesh.SetUVs(2, UV3bb);
                        }
                        else {
                            currentMesh.uv3 = UV3;
                        }
                    }

                //  Tessellated bark needs uv3 too!
                    else {
                        if (bestBending) {   
                            currentMesh.SetUVs(2, UV3bb);
                        }
                    }

                //  Skip branches and leaf planes wich are marked using "_xlod" - not very efficient here but who cares as this is an asset importer
                    if (SkipLODMarkedGeometry && LODSkiplevel <= currentLODLevel && LODSkiplevel != -1) {
                        currentMesh = null;
                    }
                    if (!isLeaf) {
                        combineBark[bark_count].mesh = currentMesh;
                        combineBark[bark_count].transform = filter.transform.localToWorldMatrix;
                    //  Here split bark meshes
                    //  Nn case we drop meshes we have to check if current mesh exists first – due to lods skipping meshes
                        if (currentMesh != null && currentMesh.subMeshCount > 1) {
                            Debug.Log("Submeshes detected. Adding 2nd bark material.");
                            Color[] tempcolors = currentMesh.colors;
                            combineBark[bark_count].mesh.subMeshCount = 2;
                            int[] tri1 = currentMesh.GetTriangles(0);
                            int[] tri2 = currentMesh.GetTriangles(1);
                            int[] triCombined = new int[tri1.Length + tri2.Length];
                            int pointer = 0;
                            // Here we have to find out in which order materials are applied
                            if (filter.GetComponent<Renderer>().sharedMaterials[0].name.ToLower().Contains("bark2nd")) {
                                for (int j = tri1.Length; j < (tri1.Length + tri2.Length); j++) {
                                    triCombined[j] = tri2[pointer];
                                    tempcolors[ tri2[pointer] ].b = 0.0f;
                                    pointer++;
                                }
                                for (int j = 0; j < tri1.Length; j++) {
                                    triCombined[j] = tri1[j];
                                    tempcolors[ tri1[j] ].b = 1.0f;
                                }
                            }
                            else {
                                for (int j = 0; j < tri1.Length; j++) {
                                    triCombined[j] = tri1[j];
                                    tempcolors[ tri1[j] ].b = 0.0f;
                                }
                                for (int j = tri1.Length; j < (tri1.Length + tri2.Length); j++) {
                                    triCombined[j] = tri2[pointer];
                                    tempcolors[ tri2[pointer] ].b = 1.0f;
                                    pointer++;
                                }
                            }
                            combineBark[bark_count].mesh.SetTriangles(triCombined,0);
                            combineBark[bark_count].mesh.colors = tempcolors;
                        }
                    }
                    else {
                        combineLeaf[leaf_count].mesh = currentMesh;
                        combineLeaf[leaf_count].transform = filter.transform.localToWorldMatrix;
                    }
                    // Clean up
                    currentMesh = null;
                }
                //  End: Exclude Bounds
            }
            //  End: Process all Objects
            //  ////////////////////////////////


//  //////////////////
//  Combine the final mesh
            
            Mesh combinedBarkMesh = new Mesh();
            Mesh combinedLeafMesh = new Mesh();
        //  Combine Bark Elements
            if (bark_count > -1) {
                combinedBarkMesh.CombineMeshes(combineBark);
            }
        //  Combine Leaf Elements
            if (leaf_count > -1) {   
                combinedLeafMesh.CombineMeshes(combineLeaf);
            }
            Mesh finalMesh = new Mesh();
        //  Finally combine both into one
            if (bark_count > -1 && leaf_count > -1) {
                CombineInstance[] combineAll = new CombineInstance[2];
                combineAll[0].mesh = combinedBarkMesh;
                combineAll[0].transform = TreeMesh.transform.localToWorldMatrix;
                combineAll[1].mesh = combinedLeafMesh;
                combineAll[1].transform = TreeMesh.transform.localToWorldMatrix;
            //  In case we safe for AFS or merge materials we simply flatten bark and leaf submeshes
                if (convertToAFS || mergeMaterials) {
                    finalMesh.CombineMeshes(combineAll, true);
                }
                else {            
                    finalMesh.CombineMeshes(combineAll, false);
                }
            }
            else if (bark_count > -1) {
                finalMesh = combinedBarkMesh;   
            }
            else if (leaf_count > -1) {
                finalMesh = combinedLeafMesh;
            }

        //  Create final tree object
            TreeMesh.AddComponent<MeshFilter>();
            TreeMesh.AddComponent<MeshRenderer>();

        //  AFS Mesh
            if (convertToAFS) {
                Debug.Log("Tree converted for AFS.");
            //  convert to vertex colors only
                if (assetPath.Contains("_xafs02")) {
                
                    Vector3[] verts = finalMesh.vertices;
                    Color[] colors = finalMesh.colors;
                    int length = verts.Length;
                    for (int v = 0; v < length; v++) {
                        // 
                        colors[v].a = finalMesh.uv2[v].x;
                        colors[v].b = finalMesh.uv2[v].y;
                    }
                    finalMesh.colors = colors;
                }
            //  otherwise the foliage shader will use vertex colors and UV4
                else {
                    finalMesh.uv4 = finalMesh.uv2;

                    Vector3[] verts = finalMesh.vertices;
                    Color[] colors = finalMesh.colors;
                    int length = verts.Length;
                    for (int v = 0; v < length; v++) {
                        colors[v].b = 0.0f;
                    }
                    finalMesh.colors = colors;
                }
                finalMesh.uv2 = null;
                finalMesh.uv3 = null;

            //  Assign afs material
                Shader shader;
                shader = Shader.Find("AFS/Foliage Shader");
                if (shader == null) {
                    shader = Shader.Find("Standard");
                }
                Material mat = Leafmat;
                mat.shader = shader;
                if (assetPath.Contains("_xafs02")) {
                    mat.SetFloat("_BendingControls", 2.0f);
                }
                else {
                   mat.SetFloat("_BendingControls", 1.0f); 
                }
                TreeMesh.GetComponent<Renderer>().material = mat;
            }

        //  Regular CTI setup
            else {
                TreeMesh.AddComponent<Tree>();
            
            //  Assign materials – in case they exist
                if(Barkmat != null && Leafmat != null) {
                    Material[] mats = new Material[1];
                //  Merged materials: the script assumes that the leaf shader will be used
                    if(mergeMaterials) {
                        mats = new Material[1];
                        mats[0] = Leafmat;
                        if (isLODTree) {
                            if ( mats[0].shader != Shader.Find("CTI/LOD Debug") ) {
                                mats[0].shader = Shader.Find("CTI/LOD Leaves");
                            }
                        }
                        else {
                            if (LeafPivots)  {
                                if (MakeSingleSided) {
                                    mats[0].shader = Shader.Find("CTI/Tree Creator Leaves Optimized Tumbling Single Sided");
                                }
                                else {
                                    mats[0].shader = Shader.Find("CTI/Tree Creator Leaves Optimized Tumbling");
                                }
                            }
                            else {
                                mats[0].shader = Shader.Find("Hidden/Nature/Tree Creator Leaves Optimized");
                            }
                        }
                    }
                //  None merged materials
                    else {
                        mats = new Material[2];
                        mats[0] = Barkmat;
                        mats[1] = Leafmat;
                        if (isLODTree) {
                            if(!barkHasSubmeshes) {
                                if ( mats[0].shader != Shader.Find("CTI/LOD Debug") ) {
                                    if (prepareTess) {
                                        mats[0].shader = Shader.Find("CTI/LOD Bark Tessellation");
                                    }
                                    else {
                                        mats[0].shader = Shader.Find("CTI/LOD Bark");
                                    }
                                }
                            }
                            else {
                                mats[0].shader = Shader.Find("CTI/LOD Bark Array");  
                            }
                            // TODO: vface
                            if ( mats[1].shader != Shader.Find("CTI/LOD Debug") ) {
                                mats[1].shader = Shader.Find("CTI/LOD Leaves");
                            }
                        }
                        else {
                            if (LeafPivots)  {
                                mats[0].shader = Shader.Find("CTI/Tree Creator Bark Optimized Tumbling");
                                if (MakeSingleSided)
                                {
                                    mats[1].shader = Shader.Find("CTI/Tree Creator Leaves Optimized Tumbling Single Sided");
                                }
                                else {
                                    mats[1].shader = Shader.Find("CTI/Tree Creator Leaves Optimized Tumbling");
                                }
                            }
                            else {
                                mats[0].shader = Shader.Find("Hidden/Nature/Tree Creator Bark Optimized");
                                mats[1].shader = Shader.Find("Hidden/Nature/Tree Creator Leaves Optimized");
                            }
                        }
                    }
                //  Apply materials
                    TreeMesh.GetComponent<Renderer>().materials = mats;
                }

            //  Create materials in case they are null
                else {
                    if (Barkmat != null) {
                        TreeMesh.GetComponent<Renderer>().sharedMaterial = Barkmat;
                        if (isLODTree) {
                            TreeMesh.GetComponent<Renderer>().sharedMaterial.shader = Shader.Find("CTI/LOD Leaves");
                        }
                        else {
                            if (LeafPivots) {
                                TreeMesh.GetComponent<Renderer>().sharedMaterial.shader = Shader.Find("CTI/Tree Creator Bark Optimized Tumbling");
                            }
                            else {
                                TreeMesh.GetComponent<Renderer>().sharedMaterial.shader = Shader.Find("Hidden/Nature/Tree Creator Bark Optimized");
                            }
                        }
                    }
                    else if (Leafmat != null)
                    {
                        TreeMesh.GetComponent<Renderer>().sharedMaterial = Leafmat;
                        if (isLODTree) {
                        //  Do not overwrite Debug Shader – as it might be pretty nasty
                            if ( TreeMesh.GetComponent<Renderer>().sharedMaterial.shader != Shader.Find("CTI/LOD Debug") ) {
                                TreeMesh.GetComponent<Renderer>().sharedMaterial.shader = Shader.Find("CTI/LOD Leaves");
                            }
                        }
                        else { 
                            if (LeafPivots)
                            {
                                TreeMesh.GetComponent<Renderer>().sharedMaterial.shader = Shader.Find("CTI/Tree Creator Leaves Optimized Tumbling");
                            }
                            else {
                                TreeMesh.GetComponent<Renderer>().sharedMaterial.shader = Shader.Find("Hidden/Nature/Tree Creator Leaves Optimized");
                            }
                        }
                    }
                }
            } // end cti setup

        //  Finalize setup
            TreeMesh.GetComponent<MeshFilter>().sharedMesh = finalMesh;

        //  Get rid of all not needed gameobjects
            foreach (Transform SubGameObject in TreeMesh.transform) {
                UnityEngine.Object.DestroyImmediate(SubGameObject.gameObject, false); // comment this if you want to keep all objects and transforms
            }

        //  Assign default textures for LOD leaves
            if (isLODTree) {
                if (Barkmat != null && Barkmat.HasProperty("_BumpSpecAOMap") ) {
                    if (Barkmat.GetTexture("_BumpSpecAOMap") == null) {
                       Barkmat.SetTexture("_BumpSpecAOMap", Resources.Load("CTI_default_normal_spec_ao") as Texture2D ); 
                    }
                    if (Barkmat.GetTexture("_DetailNormalMapX") == null) {
                       Barkmat.SetTexture("_DetailNormalMapX", Resources.Load("CTI_default_normal_spec_ao") as Texture2D ); 
                    }
                }

                if (Leafmat != null) {
                    if (Leafmat.GetTexture("_BumpSpecMap") == null) {
                       Leafmat.SetTexture("_BumpSpecMap", Resources.Load("CTI_default_normal_spec") as Texture2D ); 
                    }
                    if (Leafmat.GetTexture("_TranslucencyMap") == null) {
                       Leafmat.SetTexture("_TranslucencyMap", Resources.Load("CTI_default_ao_trans_smoothness") as Texture2D ); 
                    }
                }
            }


        //  Save and update FinalMesh
            Mesh testMesh = (Mesh)AssetDatabase.LoadAssetAtPath(AssetPath + "/" + TreeMesh.name + "_modified.asset", typeof(Mesh));
            if (testMesh)
            {
                testMesh.Clear();
                testMesh.vertices = finalMesh.vertices;
                testMesh.subMeshCount = finalMesh.subMeshCount;
                for (int p = 0; p < finalMesh.subMeshCount; p++)
                {
                    testMesh.SetTriangles(finalMesh.GetTriangles(p), p);
                }
                testMesh.tangents = finalMesh.tangents;
                testMesh.normals = finalMesh.normals;
                testMesh.colors = finalMesh.colors;
                testMesh.uv = finalMesh.uv;
            //  Handle AFS uv4
                if (convertToAFS) {
                    if (finalMesh.uv4.Length > 0) {
                        testMesh.uv4 = finalMesh.uv4; 
                    }
                }
            //  Regular CTI update 
                else {
                //  handle uv2 -> which is float4                
                    if(hasUV2) {
                        List<Vector4> UV2float4 = new List<Vector4>();
                        finalMesh.GetUVs(1, UV2float4);
                        testMesh.SetUVs(1, UV2float4);
                    }
                    else {
                        testMesh.uv2 = finalMesh.uv2;  
                    }
                    if (LeafPivots) {
                        if(bestBending) {
                            List<Vector3> UV3bestbending = new List<Vector3>();
                            finalMesh.GetUVs(2, UV3bestbending);
                            testMesh.SetUVs(2, UV3bestbending);
                        }
                        else {
                           testMesh.uv3 = finalMesh.uv3; 
                        }
                    }
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetPath + "/" + TreeMesh.name + "_modified.asset");
            //  Reassign the mesh to the MeshFilter
                TreeMesh.GetComponent<MeshFilter>().sharedMesh = testMesh;
            }
            else
            {
                AssetDatabase.CreateAsset(finalMesh, AssetPath + "/" + TreeMesh.name + "_modified.asset");
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(finalMesh));
            }

            // Clean up
            combinedBarkMesh = null;
            combinedLeafMesh = null;
            finalMesh = null;
        }   
    }
}

#endif
