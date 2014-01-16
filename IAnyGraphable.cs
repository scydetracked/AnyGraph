using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using AnyGraph;

/// <summary>
/// Interface to attach on a class to make it graphable.
/// </summary>
public interface IAnyGraphable{
	/// <summary>
	/// The GraphSettings used by the editor(Should be linked to a field in the class implementing it for serialization to work).
	/// </summary>
	/// <value>The graph settings.</value>
	AnyGraphSettings Settings{get; set;}

	/// <summary>
	/// List of nodes that get called for drawing.
	/// This should point to a serializable reference for alias nodes to work.
	/// </summary>
	List<IAnyGraphNode> Nodes{get; set;}
	
	/// <summary>
	/// Function called to draw a node.
	/// </summary>
	/// <returns>
	/// Return true if the gui needs repainting.
	/// </returns>
	/// <param name='n'>Node to draw.</param>
	bool DrawNode(IAnyGraphNode n);
	
	/// <summary>
	/// Any code in this will be drawn by the editor window.
	/// </summary>
	void AdditionalOptionsGUI();
	
	/// <summary>
	/// Called by AnyGraph when connecting two nodes.
	/// </summary>
	/// <param name='n1'>Source Node.</param>
	/// <param name='n2'>Target Node.</param>
	void ConnectNodes(UnityEngine.Object n1, UnityEngine.Object n2);

	/// <summary>
	/// Called by AnyGraph when disconnecting two nodes.
	/// </summary>
	/// <param name="n1">Node 1.</param>
	/// <param name="n2">Node 2.</param>
	void DisconnectNodes(UnityEngine.Object n1, UnityEngine.Object n2);
}