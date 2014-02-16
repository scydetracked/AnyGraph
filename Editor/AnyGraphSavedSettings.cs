//The MIT License (MIT)
//	
//Copyright (c) 2014 Phillipe Côté
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

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