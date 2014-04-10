- WHAT IS ANYGRAPH? -

AnyGraph is a relatively simple tool to help you visualize a node-based type structure. It has been developed to be as generic as possible so it can be used on as many tools as possible.
New features are being added in as they are needed. Currently, you can visualize the whole graph, collapse nodes that you don't want to see, and add breakpoints that will pause execution when the node becomes active.

This tool was developed mostly with a state machine in mind, but the possibilities are there for any type of hierarchical, node-based, structure type.
You could use it to graph a dialogue tree, a mission progression or skill unlocks, just let your imagination run wild.
--------------------------------------------------------------------------------------------------------

- HOW TO IMPLEMENT -

1- Implement IAnyGraphable on your desired grapahble type.
2- Implement IAnyGraphNode on the nodes used by your graphable type.
3- Open the Anygraph window from the menu bar -> Windows/AnyGraph or use Ctrl+G(windows) or  Cmd+G(Mac).
4- You're done.

--------------------------------------------------------------------------------------------------------

- USING ANYGRAPH -

- To move around the graph, simply hold down the Alt key, then click and drag. You can also use the middle mouse button.
- You can select multiple nodes by holding the Ctrl key on windows or the Cmd key on mac, and selecting more nodes.
- Right-clicking will bring up the context menu.
	- Debug: Here you'll find the "Set Breakpoint" command, which will turn all selected nodes into breakpoints that pause the editor execution when they become active.
	- Format: Here you'll find commands to collapse or expand all selected nodes. Making it easier to view only a single sub-tree or the entire graph.
- On the right of the window, there is an option window that can be opened. The options will be saved according to the inspected object type.

--------------------------------------------------------------------------------------------------------

- CLASS AND INTERFACE/PROPERITES AND METHODS -

IAnyGraphable -> Interface for the node structure root:
The root object type that you wish to be able to inspect in the graph window should implement this interface.
If you were to implement AnyGraph on a state machine, you would put the IAnyGraphable interface on the script that processes all the logic and is aware of all the nodes.

Properties:
	- string -> ExplicitRootNode: If this string has a value, AnyGraph will map-out the nodes from the node that matches the name.
	- List<IAnyGraphNode> -> Nodes: This should return a list of all the nodes you wish to graph.
	- string[] -> ActiveNodePath: This is a path of active nodes (Each node name must be separated by '/').
	
Methods:
	- void -> DrawNode(IAnyGraphNode n): This method will be called to add custom GUI elements in the node. Every node will call this method.
	- void -> AdditionalOptionsGUI(): This method will be called to add custom GUI elements to the options window of the graph.
	- void -> ConnectNodes(UnityEngine.Object n1, UnityEngine.Object n2): This method will be called when connecting two nodes together (if enabled).
	- void -> DisconnectNodes(UnityEngine.Object n1, UnityEngine.Object n2): This method will be called when disconnecting two nodes from each other (if enabled).

You might want to encase AnyGraph in editor-only defines, so that it doesn't cause errors, like so:

********************************************************************
public class MySuperFSM : Monobehaviour
#if UNITY_EDITOR
, IAnyGraphable
#endif
{

	-> All your logic goes here.

	#if UNITY_EDITOR
	-> IAnyGraphable implementation goes here.
	#endif
}
********************************************************************

--------------------------------------------------------------------------------------------------------

IAnyGraphNode -> Interface for the nodes that will be graphed.
The node type that will be used by your AnyGraph-enabled type.

Properties:
	- string -> Name: This is the name that will be displayed by the graph. It will also be used to search the active node path.
	- UnityEngine.Object -> EditorObj: This is the object that will be selected in the project window when selecting this node in the graph.
	- List<AnyGraphLink> -> Links: This is a list of all the links used by this node.
	
Just like the IAnyGraphable implementation, you might want to encase the implementation of IAnyGraphNode in editor-only defines.

--------------------------------------------------------------------------------------------------------

IAnyGraphLink -> Class to create links for node.

Properties:
	- string -> linkText: This is the text that will be displayed in a node using this link.
	- IAnyGraphNode -> connection: This is the node to which it connects.