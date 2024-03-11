using UnityEngine;
using UnityEditor;

namespace CTI {
	
	public class AddTreeComponent {
		[MenuItem("Component/CTI/Add Tree Component")]
    	private static void Add()
    	{
    		var selected = (GameObject) Selection.activeObject as GameObject;
    		selected.AddComponent<Tree>();

    	}
    }
}