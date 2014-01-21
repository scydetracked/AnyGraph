using UnityEngine;
using System.Collections;

/// <summary>
/// Settings used by AnyGraph.
/// </summary>
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

	/// <summary>
	/// If set to true, the editor will allow connecting nodes together.
	/// </summary>
	public bool allowNodeLinking = false;


	public bool useCurvedConnectors = true;

	public GraphOrganizingMode structuringMode = GraphOrganizingMode.Pack;

	/// <summary>
	/// The width used when drawing a link.
	/// </summary>
	public float linkWidth = 3f;

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