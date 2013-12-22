using UnityEngine;
using System.Collections;

/// <summary>
/// A node that references another node. Used to prevent looping.
/// </summary>
[System.Serializable]
public class AnyGraphAliasNode : IAnyGraphNode {
	private IAnyGraphNode _refNode;
	private Rect _edPos = new Rect();

	public AnyGraphAliasNode(IAnyGraphNode refNode){
		_refNode = refNode;
		_edPos = refNode.EditorPos;
		_edPos.height = 50;
	}

	#region IAnyGraphNode implementation

	public string Name {
		get {
			return _refNode.Name + " (Alias)";
		}
		set {
			_refNode.Name = value;
		}
	}

	public Rect EditorPos {
		get {
			return _edPos;
		}
		set {
			_edPos = value;
		}
	}

	public Object EditorObj {
		get {
			return _refNode.EditorObj;
		}
	}

	public System.Collections.Generic.List<AnyGraphLink> ConnectedNodes {
		get {
			return new System.Collections.Generic.List<AnyGraphLink>();
		}
		set{}
	}

	public bool Active {
		get {
			return _refNode.Active;
		}
		set {
			_refNode.Active = value;
		}
	}

	#endregion
}
