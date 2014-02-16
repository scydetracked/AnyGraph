using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace AnyGraph{
	public class AnyGraphSavedSettings : ScriptableObject {
		[SerializeField] private AnyGraphSettings[] allSettings;

		public AnyGraphSavedSettings(){allSettings = new AnyGraphSettings[0];}

		public AnyGraphSettings GetSettings(System.Type type){
			for(int i = 0; i < allSettings.Length; i++){
				if(allSettings[i].SettingsType == type.ToString ()){
					return allSettings[i];
				}
			}

			List<AnyGraphSettings> settings = new List<AnyGraphSettings>(allSettings);

			AnyGraphSettings newSettings = new AnyGraphSettings(type);
			settings.Add (newSettings);
			allSettings = settings.ToArray ();
			UnityEditor.EditorUtility.SetDirty (this);
			return newSettings;
		}
	}
}