using System.Linq;
using System.Collections.Generic;

public static class AnyGraphNodeExt {
	public static bool IsRedundant(this IAnyGraphNode node, List<IAnyGraphNode> scanned){
		scanned.Add (node);

		if(node.ConnectedNodes.Count == 0){
			return false;
		}

		foreach(IAnyGraphNode current in node.ConnectedNodes.Where (x => x.connection != null).Select (x => x.connection)){
			if(scanned.Contains (current)){
				return true;
			}

			List<IAnyGraphNode> next = new List<IAnyGraphNode>();
			next.AddRange (scanned);
			if(current.IsRedundant (next)){
				return true;
			}
		}
		return false;
	}
}