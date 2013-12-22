using System.Linq;
using System.Collections.Generic;

public static class AnyGraphNodeExt {
	public static bool IsNodeRedundant(this IAnyGraphNode currentNode, IAnyGraphNode node, List<IAnyGraphNode> scanned){
		scanned.Add (currentNode);

		if(currentNode.ConnectedNodes.Count == 0){
			return false;
		}

		foreach(IAnyGraphNode current in currentNode.ConnectedNodes.Where (x => x.connection != null && !(x.connection is AnyGraphAliasNode)).Select (x => x.connection)){
			if(current == node){
				return true;
			}
			else if(scanned.Contains (current)){
				continue;
			}


			List<IAnyGraphNode> next = new List<IAnyGraphNode>();
			next.AddRange (scanned);
			if(current.IsNodeRedundant (node, next)){
				return true;
			}
		}
		return false;
	}

	public static bool IsNodeRedundant(this IAnyGraphNode node){
		return node.IsNodeRedundant (node, new List<IAnyGraphNode>());
	}

	public static IAnyGraphNode GetRedundancyInstigator(this IAnyGraphNode currentNode, IAnyGraphNode node, List<IAnyGraphNode> scanned){
		scanned.Add (currentNode);
		
		if(currentNode.ConnectedNodes.Count == 0){
			return null;
		}
		
		foreach(IAnyGraphNode current in currentNode.ConnectedNodes.Where (x => x.connection != null && !(x.connection is AnyGraphAliasNode)).Select (x => x.connection)){
			if(current == node){
				return currentNode;
			}
			else if(scanned.Contains (current)){
				continue;
			}
			
			
			List<IAnyGraphNode> next = new List<IAnyGraphNode>();
			next.AddRange (scanned);
			IAnyGraphNode nextInstigator = current.GetRedundancyInstigator (node, next);
			if(nextInstigator != null){
				return nextInstigator;
			}
		}
		return null;
	}

	public static IAnyGraphNode GetRedundancyInstigator(this IAnyGraphNode currentNode){
		return currentNode.GetRedundancyInstigator (currentNode, new List<IAnyGraphNode>());
	}
}