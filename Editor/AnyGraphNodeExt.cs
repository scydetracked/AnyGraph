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

using System.Linq;
using System.Collections.Generic;
using AnyGraph;

namespace AnyGraph{
	public static class AnyGraphNodeExt {
		public static bool IsNodeRedundant(this IAnyGraphNode currentNode, IAnyGraphNode node, List<IAnyGraphNode> scanned){
			scanned.Add (currentNode);

			if(currentNode.Links.Count == 0){
				return false;
			}

			foreach(IAnyGraphNode current in currentNode.Links.Where (x => x.connection != null).Select (x => x.connection)){
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
			
			if(currentNode.Links.Count == 0){
				return null;
			}
			
			foreach(IAnyGraphNode current in currentNode.Links.Where (x => x.connection != null).Select (x => x.connection)){
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
}