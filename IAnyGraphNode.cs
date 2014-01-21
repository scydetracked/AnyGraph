using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Interface to apply on a class that can be used as a node.
/// </summary>
public interface IAnyGraphNode {
	/// <summary>
	/// Returns the name to be displayed for this node.
	/// </summary>
	/// <value>
	/// The name.
	/// </value>
	string Name{get;set;}

	/// <summary>
	/// Gets the object to be inspected.
	/// </summary>
	UnityEngine.Object EditorObj{get;}

	/// <summary>
	/// Gets the connected nodes to draw the links.
	/// </summary>
	List<AnyGraphLink> ConnectedNodes{get; set;}

	/// <summary>
	/// Used to display the node is currently active.
	/// </summary>
	/// <value><c>true</c> if active; otherwise, <c>false</c>.</value>
	bool Active{get;set;}
}