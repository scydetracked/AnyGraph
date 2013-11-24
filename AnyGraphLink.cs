using UnityEngine;
using System.Collections;

/// <summary>
/// Link used by AnyGraph.
/// </summary>
public struct AnyGraphLink {
	/// <summary>
	/// Text to be displayed in the node.
	/// </summary>
	public string linkText;

	/// <summary>
	/// The node to which it connects.
	/// </summary>
	public IAnyGraphNode connection;

	/// <summary>
	/// This value is used internally by AnyGraph. Changing it won't have any effect.
	/// </summary>
	public float yOffset;
}