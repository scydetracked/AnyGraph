using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace AnyGraph{
	public class AnyGraphSavedSettings : ScriptableObject {
		[SerializeField, HideInInspector] private List<AnyGraphSettings> allSettings;

		public AnyGraphSavedSettings(){allSettings = new List<AnyGraphSettings>();}

		public AnyGraphSettings GetSettings(System.Type type){
			for(int i = 0; i < allSettings.Count; i++){
				if(allSettings[i].SettingsType == type){
					return allSettings[i];
				}
			}

			AnyGraphSettings newSettings = new AnyGraphSettings(type);
			allSettings.Add (newSettings);
			UnityEditor.EditorUtility.SetDirty (this);
			return newSettings;
		}
	}
}