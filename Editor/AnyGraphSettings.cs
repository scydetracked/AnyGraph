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
		/// If set to true, the editor will allow connecting nodes together.
		/// </summary>
		public bool allowNodeLinking = false;

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

		public static bool operator !=(AnyGraphSettings left, AnyGraphSettings right){return !(left == right);}
		public static bool operator ==(AnyGraphSettings left, AnyGraphSettings right){
			if(left != null && right == null){
				return false;
			}
			else if(left == null && right != null){
				return false;
			}
			else if(left.SettingsType != right.SettingsType){
				return false;
			}
			else if(left.allowNodeLinking  != right.allowNodeLinking){
				return false;
			}
			else if(left.structuringMode != right.structuringMode){
				return false;
			}
			else if(left.linkWidth != right.linkWidth){
				return false;
			}
			else if(left.baseLinkColor != right.baseLinkColor){
				return false;
			}
			else if(left.colorToSelected != right.colorToSelected){
				return false;
			}
			else if(left.colorFromSelected != right.colorFromSelected){
				return false;
			}
			else if(left.selectedLinkColor != right.selectedLinkColor){
				return false;
			}
			else if(left.fromLinkColor != right.fromLinkColor){
				return false;
			}
			else if(left.toLinkColor != right.toLinkColor){
				return false;
			}
			else if(left.selectedNodeColor != right.selectedNodeColor){
				return false;
			}
			else if(left.toNodeColor != right.toNodeColor){
				return false;
			}
			else if(left.fromNodeColor != right.fromNodeColor){
				return false;
			}
			else if(left.drawLinkOnTop != right.drawLinkOnTop){
				return false;
			}
			else if(left.nodePlacementOffset != right.nodePlacementOffset){
				return false;
			}

			return true;
		}
	}
}