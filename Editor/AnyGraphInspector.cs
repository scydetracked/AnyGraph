using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(IAnyGraphable), true)]
public class AnyGraphInspector : Editor {
	public override void OnInspectorGUI (){
		if(GUILayout.Button ("View In AnyGraph")){
			EditorWindow.GetWindow<AnyGraph>();
		}
		base.OnInspectorGUI ();
	}
}