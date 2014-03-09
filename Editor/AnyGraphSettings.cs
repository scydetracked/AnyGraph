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

/// <summary>
/// Settings used by AnyGraph.
/// </summary>
namespace AnyGraph{
	[System.Serializable]
	public sealed class AnyGraphSettings {
		public enum NodeColors{
			Grey,
			Gray = 0,
			Blue,
			Aqua,
			Green,
			Yellow,
			Orange,
			Red
		}
		
		public enum GraphOrganizingMode{
			SpreadOut,
			Pack,
		}

		public AnyGraphSettings(System.Type type){
			SettingsType = type.ToString ();
		}

		public string SettingsType;

		/// <summary>
		/// The structuring mode used by the graph.
		/// </summary>
		public GraphOrganizingMode structuringMode = GraphOrganizingMode.Pack;

		/// <summary>
		/// The width used when drawing a link.
		/// </summary>
		public float linkWidth = 5f;

		/// <summary>
		/// The color used for links.
		/// </summary>
		public Color baseLinkColor = Color.green;

		/// <summary>
		/// if set to true, the editor will color links and nodes going to the selected node.
		/// </summary>
		public bool colorToSelected = true;

		/// <summary>
		/// If set to true, the editor will color links and nodes coming from the selected node.
		/// </summary>
		public bool colorFromSelected = true;

		/// <summary>
		/// The color of a link between two selected nodes.
		/// </summary>
		public Color selectedLinkColor = new Color(1f, 1f, 0f, 1f);

		/// <summary>
		/// The color of a link going from the selected node.
		/// </summary>
		public Color fromLinkColor = new Color(1, 140f/255f, 0, 1);

		/// <summary>
		/// The color of link going to the selected node.
		/// </summary>
		public Color toLinkColor = new Color(0.3f, 0.6f, 1f, 1f);

		/// <summary>
		/// The color of a selected node.
		/// </summary>
		public NodeColors selectedNodeColor = NodeColors.Yellow;

		/// <summary>
		/// The color of a node targetted by the selected node.
		/// </summary>
		public NodeColors toNodeColor = NodeColors.Blue;

		/// <summary>
		/// The color of a node targetting the selected node.
		/// </summary>
		public NodeColors fromNodeColor = NodeColors.Orange;

		/// <summary>
		/// Will draw the links over the nodes if true;
		/// </summary>
		public bool drawLinkOnTop = false;

		/// <summary>
		/// The offset used by the editor when repositionning nodes.
		/// </summary>
		public Vector2 nodePlacementOffset = new Vector2(150, 30);
	}
}