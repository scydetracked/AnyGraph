using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace AnyGraph{
	public sealed class AnyGraph : EditorWindow {
		private enum SelectionDragType{
			None,
			Rect,
			pick,
		}

		private const float kZoomMin = 0.1f;
		private const float kZoomMax = 1f;
		
		private Rect _zoomArea;
		private float _zoom = 1.0f;
		private Vector2 _zoomCoordsOrigin = Vector2.zero;

		private Vector2 _nodeDragDistance = new Vector2();
		private Vector2 _initMousePos = new Vector2();

		private Vector2 scrollPos = new Vector2();
		private Rect _graphExtents;
		private Rect _lastGraphExtents;
		private Vector2 _dragStartPoint;
		private SelectionDragType _dragType = SelectionDragType.None;

		private bool _optionWindowOpen = false;
		private Rect _optionWindowRect;
		private Vector2 _optionWindowScrollPos = new Vector2();
		private bool _showOptionsGraphSettings = false;


		// Non anygraph specific.
		private Dictionary<Node, Rect> _initialDragNodePosition = new Dictionary<Node, Rect>();
		private List<Rect> _allNodePos = new List<Rect>();
		private List<IAnyGraphNode> _cachedNodes = new List<IAnyGraphNode>();
		private IAnyGraphable _selected = null;
		private bool _linkingNode = false;
		private Node _nodeToLink = null;

		// New Node & Link systems
		private static List<Node> _allNodes = new List<Node>();
		private List<Node> _selection = new List<Node>();
		private List<Node> _oldSelection = new List<Node>();

		private int _updateThrottler = 0;

		private bool _needRearrange = false;

		[MenuItem("Window/AnyGraph")]
		public static void Openwindow(){
			AnyGraph window = EditorWindow.GetWindow<AnyGraph>("Any Graph", true);
			window.Reset ();
		}

		private Vector2 ConvertScreenCoordsToZoomCoords(Vector2 screenCoords){
			return (screenCoords - _zoomArea.TopLeft()) / _zoom + _zoomCoordsOrigin;
		}

		/// <summary>
		/// Resets important values for the AnyGraph window.
		/// </summary>
		private void Reset(){
			_zoomArea = new Rect();
			_zoom = 1.0f;
			_zoomCoordsOrigin = Vector2.zero;
			
			_nodeDragDistance = new Vector2();
			_initMousePos = new Vector2();
			
			scrollPos = new Vector2();
			_graphExtents = new Rect();
			_lastGraphExtents = new Rect();
			_dragStartPoint = new Vector2();
			_dragType = SelectionDragType.None;

			_optionWindowRect = new Rect();
			_optionWindowScrollPos = new Vector2();
			
			
			// Non anygraph specific.
			_initialDragNodePosition = new Dictionary<Node, Rect>();
			_allNodePos = new List<Rect>();
			_cachedNodes = new List<IAnyGraphNode>();
			_selected = null;
			_linkingNode = false;
			_nodeToLink = null;
			
			// New Node & Link systems
			_allNodes = new List<Node>();
			_selection = new List<Node>();
			_oldSelection = new List<Node>();

			System.GC.Collect ();
		}

		/// <summary>
		/// Handles the input events for zooming and cancelling node linking.
		/// </summary>
		private void HandleEvents(){
			if (Event.current.type == EventType.ScrollWheel){
				Vector2 screenCoordsMousePos = Event.current.mousePosition;
				Vector2 delta = Event.current.delta;
				Vector2 zoomCoordsMousePos = ConvertScreenCoordsToZoomCoords(screenCoordsMousePos);
				float zoomDelta = -delta.y / 150.0f;
				float oldZoom = _zoom;
				_zoom += zoomDelta;
				_zoom = Mathf.Clamp(_zoom, kZoomMin, kZoomMax);
				_zoomCoordsOrigin += (zoomCoordsMousePos - _zoomCoordsOrigin) - (oldZoom / _zoom) * (zoomCoordsMousePos - _zoomCoordsOrigin);
				
				Event.current.Use();
			}

			if(_linkingNode && Event.current.type == EventType.MouseDown && Event.current.button == 1){
				_linkingNode = false;
				_nodeToLink = null;
			}

			if(Event.current.modifiers == EventModifiers.Control){
				switch(Event.current.keyCode){
				case KeyCode.Alpha0:
					_zoom = 1;
					break;
				case KeyCode.Equals:
					_zoom += 0.05f;
					break;
				case KeyCode.Minus:
					_zoom -= 0.05f;
					break;
				}

				_zoom = Mathf.Clamp (_zoom, kZoomMin, kZoomMax);
			}
		}

		private void Update(){
			if(_updateThrottler%4 == 0){
				Repaint ();
			}
			_updateThrottler = _updateThrottler++%10000;
		}

		private void GenerateCompleteNodeMap(List<IAnyGraphNode> nodes, string rootStateName = ""){
			Debug.Log ("Regen");
			_cachedNodes.Clear();
			_cachedNodes.AddRange (nodes);

			_allNodes = new List<Node>();
			List<IAnyGraphNode> rootNodes = new List<IAnyGraphNode>();

			if(!string.IsNullOrEmpty(rootStateName)){
				IAnyGraphNode root = _cachedNodes.Find (x => x.Name == rootStateName);

				if(root != null){
					rootNodes.Add (root);
				}
			}

			if(rootNodes.Count == 0){
				rootNodes.AddRange(_cachedNodes);
				foreach(IAnyGraphNode n in _cachedNodes){
					foreach(AnyGraphLink l in n.Links){
						rootNodes.Remove (l.connection);
					}
				}
			}

			foreach(IAnyGraphNode root in rootNodes.Where (x => x != null)){
				Node newNode = new Node(){
					representedNode = root,
					isRoot = true,
					guid = System.Guid.NewGuid ().ToString (),
					links = new List<Link>(),
					nodePos = new Rect(),
				};

				_allNodes.Add (newNode);
				_allNodes.AddRange (newNode.SetupRecursively ());
			}

			Debug.Log (_allNodes.Count);
			_needRearrange = true;
		}

		private void OnGUI(){
			// Set the option window rect if it is open.
			if(_optionWindowOpen){
				_optionWindowRect = new Rect(position.width - 280, 0, 280, position.height);
			}
			else{
				_optionWindowRect = new Rect(position.width - 30, 0, 30, position.height);
			}

			HandleEvents ();
			_zoomArea = new Rect(0, 0, position.width - _optionWindowRect.width, position.height);
			List<IAnyGraphable> availableToDraw = new List<IAnyGraphable>();
			if(Selection.activeGameObject != null){
				MonoBehaviour[] mono = Selection.activeGameObject.GetComponents<MonoBehaviour>();
				for(int i = 0; i < mono.Length; i++){
					if(mono[i] is IAnyGraphable){
						availableToDraw.Add(mono[i] as IAnyGraphable);
					}
				}
			}

			// Change the selected object if it can be drawn.
			if((_selected == null || _selected != Selection.activeObject as IAnyGraphable) && Selection.activeObject is IAnyGraphable){
				Reset ();
				_selected = Selection.activeObject as IAnyGraphable;
				GenerateCompleteNodeMap (_selected.Nodes, _selected.ExplicitRootNodeName);
				Repaint ();
			}
			else if(!availableToDraw.Contains (_selected)){
				for(int i = 0; i < availableToDraw.Count; i++){
					if(availableToDraw[i] != null && availableToDraw[i] as IAnyGraphable != _selected){
						Reset ();
						_selected = availableToDraw[i];
						GenerateCompleteNodeMap (_selected.Nodes, _selected.ExplicitRootNodeName);
						return;
					}
				}
			}
			/*else if(Selection.activeObject != null && Selection.activeObject is GameObject && Selection.activeGameObject.GetComponent<MonoBehaviour>().w != (_selected as Component).gameObject){
				// Grab all IAnyGraphable instances on the selected object.
				availableToDraw = Selection.activeGameObject.GetComponents<MonoBehaviour>().Where (x => x is IAnyGraphable).ToList ();
				if(availableToDraw.Count == 1){
					_selected = availableToDraw[0] as IAnyGraphable;
				}
				else if(availableToDraw.Count > 0){
					_selected = availableToDraw[0] as IAnyGraphable;
					// TODO: Draw a selection box for user to choose which instance to draw.
				}
				GenerateCompleteNodeMap (_selected.Nodes);
			}*/

			// Draw Background and grid.
			GUI.Box (new Rect(0, 0, position.width, position.height), "", UnityEditor.Graphs.Styles.graphBackground);
			DrawGrid ();
			
			if(_selected == null){
				string text = "Graph is null, cannot draw";
				Rect textSize = GUILayoutUtility.GetRect (new GUIContent(text), "Button");
				GUI.Label (new Rect((position.width / 2) - (textSize.width / 2), (position.height / 2) - (textSize.height / 2), textSize.width, textSize.height), text, "Button");
				Reset ();
				return;
			}
			else if((_selected as UnityEngine.Component) == null){
				_selected = null;
				Reset ();
				return;
			}

			// Create a new settings instance if one doesn't exist.
			if(_selected.Settings == null){
				_selected.Settings = new AnyGraphSettings();
			}

			CheckNodes ();
			CheckNodeLinks ();

			Rect scrollViewRect = EditorZoomArea.Begin (_zoom, new Rect(0, 0, position.width - _optionWindowRect.width, position.height));
			scrollViewRect.y -= 21;
			scrollPos = GUI.BeginScrollView (scrollViewRect, scrollPos, _graphExtents, GUIStyle.none, GUIStyle.none);

			DrawLinks ();
			DrawNodes ();

			DragSelection(new Rect(-5000f, -5000f, 10000f, 10000f));

			UpdateScrollPosition ();
			DragGraph();

			GUI.EndScrollView();
			EditorZoomArea.End ();

			if(_needRearrange){
				RearrangeNodesAsTree(_selected.Settings.nodePlacementOffset.x, _selected.Settings.nodePlacementOffset.y, true);
			}

			// Options window.
			if(DrawOptionsWindow()){
				Repaint ();
			}
		}

		private void DrawContextMenu(Node n){
			GenericMenu menu = new GenericMenu();
			if(n.Collapsed){
				menu.AddItem (new GUIContent("Expand"), false, delegate() {
					n.Collapsed = false;
					RearrangeNodesAsTree (_selected.Settings.nodePlacementOffset.x, _selected.Settings.nodePlacementOffset.y);
				});
			}
			else{
				menu.AddItem (new GUIContent("Collapse"), false, delegate() {
					n.Collapsed = true;
					RearrangeNodesAsTree (_selected.Settings.nodePlacementOffset.x, _selected.Settings.nodePlacementOffset.y);
				});
			}

			Vector2 mousePos = Event.current.mousePosition - _zoomCoordsOrigin + (_zoomCoordsOrigin * _zoom);// - new Vector2(n.nodePos.x, n.nodePos.y);
			menu.DropDown (new Rect(mousePos.x, mousePos.y, 0, 0));

			//menu.ShowAsContext ();
		}

		private void DrawContextMenu(Node[] n){
			GenericMenu menu = new GenericMenu();
			menu.AddItem (new GUIContent("Expand Multiple"), false, delegate() {
				for(int i = 0; i < n.Length; i ++){
					n[i].Collapsed = false;
				}
				RearrangeNodesAsTree (_selected.Settings.nodePlacementOffset.x, _selected.Settings.nodePlacementOffset.y);
			});

			menu.AddItem (new GUIContent("Collapse Multiple"), false, delegate() {
				for(int i = 0; i < n.Length; i++){
					n[i].Collapsed = true;
				}
				RearrangeNodesAsTree (_selected.Settings.nodePlacementOffset.x, _selected.Settings.nodePlacementOffset.y);
			});
			
			Vector2 mousePos = (Event.current.mousePosition);
			menu.DropDown (new Rect(mousePos.x, mousePos.y, 0, 0));

			menu.ShowAsContext ();
		}

		#region Drawing Functions
		/// <summary>
		/// Draws the options window. All custom drawing from IAnyGraphable will be called in here as well.
		/// </summary>
		/// <returns><c>true</c>, if Repaint() should be called, <c>false</c> otherwise.</returns>
		private bool DrawOptionsWindow(){
			GUI.Box (_optionWindowRect, GUIContent.none);
			GUILayout.BeginArea (_optionWindowRect);
			if(!_optionWindowOpen){
				if(GUI.Button (new Rect(0, 0, _optionWindowRect.width, _optionWindowRect.height), "O\nP\nE\nN\n\nO\nP\nT\nI\nO\nN\nS\n\nW\nI\nN\nD\nO\nW")){
					_optionWindowOpen = true;
				}
			}
			else{
				_optionWindowScrollPos = GUILayout.BeginScrollView(_optionWindowScrollPos, GUILayout.MaxHeight (_optionWindowRect.height - 20));

				if(GUILayout.Button ("Structure")){
					RearrangeNodesAsTree (_selected.Settings.nodePlacementOffset.x, _selected.Settings.nodePlacementOffset.y);
				}

				if(GUILayout.Button ("Force Refresh")){
					Reset ();
					return true;
					//GenerateCompleteNodeMap (_selected.Nodes);
				}

				// Buttons for manual linking/unlinking.
				if(_selected.Settings.allowNodeLinking && _selection.Count == 2){
					if(_selection[0].representedNode.Links.Select (x => x.connection).Contains (_selection[1].representedNode)){
						if(GUILayout.Button (string.Format("Disconnect '{0}' --X--> '{1}'", _selection[0].representedNode.Name, _selection[1].representedNode.Name))){
							_selected.DisconnectNodes (_selection[0].representedNode.EditorObj, _selection[1].representedNode.EditorObj);
							return true;
						}
					}
					else{
						if(GUILayout.Button (string.Format("Connect '{0}' -----> '{1}'", _selection[0].representedNode.Name, _selection[1].representedNode.Name))){
							_selected.ConnectNodes (_selection[0].representedNode.EditorObj, _selection[1].representedNode.EditorObj);
							return true;
						}
					}
					
					if(_selection[1].representedNode.Links.Select (x => x.connection).Contains (_selection[0].representedNode)){
						if(GUILayout.Button (string.Format("Disconnect '{0}' --X--> '{1}'", _selection[1].representedNode.Name, _selection[0].representedNode.Name))){
							_selected.DisconnectNodes (_selection[1].representedNode.EditorObj, _selection[0].representedNode.EditorObj);
							return true;
						}
					}
					else{
						if(GUILayout.Button (string.Format("Connect '{0}' ----> '{1}'", _selection[1].representedNode.Name, _selection[0].representedNode.Name))){
							_selected.ConnectNodes (_selection[1].representedNode.EditorObj, _selection[0].representedNode.EditorObj);
							return true;
						}
					}
				}

				// Foldout containing graph settings.
				_showOptionsGraphSettings = EditorGUILayout.Foldout (_showOptionsGraphSettings, "AnyGraph Settings");
				if(_showOptionsGraphSettings){
					EditorGUI.indentLevel++;
					_selected.Settings.nodePlacementOffset = EditorGUILayout.Vector2Field ("Auto-Placement Offset", _selected.Settings.nodePlacementOffset);
					_selected.Settings.structuringMode = (AnyGraphSettings.GraphOrganizingMode)EditorGUILayout.EnumPopup ("Placement Type", _selected.Settings.structuringMode);
					_selected.Settings.allowNodeLinking = EditorGUILayout.Toggle ("Allow Node Linking", _selected.Settings.allowNodeLinking);
					_selected.Settings.drawLinkOnTop = EditorGUILayout.Toggle ("Draw Links On Top", _selected.Settings.drawLinkOnTop);
					_selected.Settings.linkWidth = EditorGUILayout.FloatField ("Link Width", _selected.Settings.linkWidth);
					_selected.Settings.baseLinkColor = EditorGUILayout.ColorField ("Base Link Color", _selected.Settings.baseLinkColor);
					_selected.Settings.selectedNodeColor = (AnyGraphSettings.NodeColors)EditorGUILayout.EnumPopup("Selected Node Color", _selected.Settings.selectedNodeColor);
					_selected.Settings.selectedLinkColor = EditorGUILayout.ColorField ("Selected Link Color", _selected.Settings.selectedLinkColor);

					EditorGUILayout.Space ();
					_selected.Settings.colorFromSelected = EditorGUILayout.Toggle ("Color Links From Selected", _selected.Settings.colorFromSelected);
					if(_selected.Settings.colorFromSelected){
						EditorGUI.indentLevel++;
						_selected.Settings.fromNodeColor = (AnyGraphSettings.NodeColors)EditorGUILayout.EnumPopup("Selected Node Color", _selected.Settings.fromNodeColor);
						_selected.Settings.fromLinkColor = EditorGUILayout.ColorField ("From Link Color", _selected.Settings.fromLinkColor);
						EditorGUI.indentLevel--;
					}
					_selected.Settings.colorToSelected = EditorGUILayout.Toggle ("Color Links To Selected", _selected.Settings.colorToSelected);
					if(_selected.Settings.colorToSelected){
						EditorGUI.indentLevel++;
						_selected.Settings.toNodeColor = (AnyGraphSettings.NodeColors)EditorGUILayout.EnumPopup("Selected Node Color", _selected.Settings.toNodeColor);
						_selected.Settings.toLinkColor = EditorGUILayout.ColorField ("To Link Color", _selected.Settings.toLinkColor);
						EditorGUI.indentLevel--;
					}

					EditorGUILayout.Space ();

					if(GUILayout.Button ("Reset Settings")){
						_selected.Settings = new AnyGraphSettings ();
					}
					EditorGUI.indentLevel--;
				}

				// Draw custom gui implemented by user.
				_selected.AdditionalOptionsGUI ();
				
				// Consume the event.
				if(Event.current.type == EventType.MouseDown){
					Event.current.Use ();
				}

				GUILayout.EndScrollView();

				// Options close button.
				if(GUI.Button (new Rect(0, _optionWindowRect.height - 20, _optionWindowRect.width, 20), "Close Options Window")){
					_optionWindowOpen = false;
				}
			}
			GUILayout.EndArea ();

			return false;
		}

		/// <summary>
		/// Draws the background grid.
		/// </summary>
		private void DrawGrid (){
			if (Event.current.type != EventType.Repaint){
				return;
			}
			Profiler.BeginSample ("DrawGrid");
			GL.PushMatrix ();
			GL.Begin (1);
			DrawGridLines (15f, Color.white);
			DrawGridLines (150f, Color.gray);
			GL.End ();
			GL.PopMatrix ();
			Profiler.EndSample ();
		}

		/// <summary>
		/// Draws the grid lines.
		/// </summary>
		/// <param name="gridSize">Line spacing.</param>
		/// <param name="gridColor">Line color.</param>
		private void DrawGridLines (float gridSize, Color gridColor){
			Rect extents = new Rect(-50, -50, position.width + 100, position.height + 100);
			
			GL.Color (gridColor);
			for (float num = extents.xMin - extents.xMin % gridSize; num < extents.xMax; num += gridSize){
				DrawGridLine (new Vector2 (num, extents.yMin), new Vector2 (num, extents.yMax));
			}
			GL.Color (gridColor);
			for (float num2 = extents.yMin - extents.yMin % gridSize; num2 < extents.yMax; num2 += gridSize){
				DrawGridLine (new Vector2 (extents.xMin, num2), new Vector2 (extents.xMax, num2));
			}
		}

		/// <summary>
		/// Draws a line.
		/// </summary>
		/// <param name="p1">Start.</param>
		/// <param name="p2">End.</param>
		private void DrawGridLine (Vector2 p1, Vector2 p2){
			GL.Vertex (p1);
			GL.Vertex (p2);
		}

		/// <summary>
		/// Draws the links.
		/// </summary>
		private void DrawLinks(){
			foreach(Node n in _allNodes){
				if(n.Collapsed){
					continue;
				}
				foreach(Link l in n.links){
					if(string.IsNullOrEmpty(l.guid)){
						continue;
					}
					Color linkColor = _selected.Settings.baseLinkColor;
					if(_selected.Settings.colorFromSelected && _selection.Find (x => x.links.Contains (l)) != null &&
					   _selected.Settings.colorToSelected && _selection.Select (x => x.guid).Contains (l.guid)){
						linkColor = _selected.Settings.selectedLinkColor;
					}
					else if(_selected.Settings.colorFromSelected && _selection.Find (x => x.links.Contains (l)) != null){
						linkColor = _selected.Settings.fromLinkColor;
					}
					else if(_selected.Settings.colorToSelected && _selection.Select (x => x.guid).Contains (l.guid)){
						linkColor = _selected.Settings.toLinkColor;
					}

					DrawLink(n, _allNodes.Find (x => x.guid == l.guid), l.yOffset, linkColor);
				}
			}

			if(_linkingNode){
				if(_nodeToLink == null){
					_linkingNode = false;
				}
				else{
					Color linkColor = _selected.Settings.baseLinkColor;
					Vector3 nodePos = new Vector3(_nodeToLink.nodePos.x + _nodeToLink.nodePos.width, _nodeToLink.nodePos.y);
					DrawLink (nodePos, new Vector3(Event.current.mousePosition.x, Event.current.mousePosition.y, 0), linkColor);
				}
			}
		}

		/// <summary>
		/// Draws the nodes.
		/// </summary>
		private void DrawNodes(){
			BeginWindows ();
	
			_allNodePos = new List<Rect>();
			foreach(Node n in _allNodes){
				if(n.Parents.Count == 0 || !n.Parents.Last ().Collapsed){
					DrawNode (n);
				}
			}

			EndWindows ();
		}

		/// <summary>
		/// Draws a specific node.
		/// </summary>
		/// <param name="node">Node to draw.</param>
		private void DrawNode(Node node){
			UnityEditor.Graphs.Styles.Color nodeColor = UnityEditor.Graphs.Styles.Color.Gray;

			// Color if the node is going to the selected node.
			if(_selected.Settings.colorToSelected){
				foreach(Link l in node.links){
					if(_selection.Select (x => x.guid).Contains (l.guid)){
						nodeColor = (UnityEditor.Graphs.Styles.Color)(int)_selected.Settings.toNodeColor;
						break;
					}
				}
			}

			// Color if the node is coming from the selected node.
			if(_selected.Settings.colorFromSelected){
				foreach(Node selectedNode in _selection){
					if(selectedNode.links.Select (x => x.guid).Contains (node.guid)){
						nodeColor = (UnityEditor.Graphs.Styles.Color)(int)_selected.Settings.fromNodeColor;
						break;
					}
				}
			}

			// Color if node is selected.
			if(_selection.Contains (node)){
				nodeColor = (UnityEditor.Graphs.Styles.Color)(int)_selected.Settings.selectedNodeColor;
			}

			// Draw node.
			if(node.representedNode == null){
				return;
			}

			node.nodePos = GUILayout.Window (_allNodes.FindIndex (x => x.Equals(node)), node.nodePos, delegate{
				float width = GUILayoutUtility.GetRect (new GUIContent(node.representedNode.Name), "Label").width;
				SelectNode (node);

				EditorGUILayout.BeginHorizontal ();
				if(_selected.Settings.allowNodeLinking && GUILayout.Button ("Link To...")){
					_nodeToLink = node;
					_linkingNode = true;
				}

				bool repaint = _selected.DrawNode (node.representedNode);
				
				EditorGUILayout.BeginVertical ();
				for(int i = 0; i < node.links.Count; i++){
					GUILayout.Label (node.links[i].linkName);
					//Link temp = node.links[i];
					Rect lastRect = GUILayoutUtility.GetLastRect ();
					//temp.yOffset = lastRect.y + (lastRect.height / 2);
					//node.links[i] = temp;
					if(lastRect.width > width){
						width = lastRect.width;
					}
					if(lastRect.y + (lastRect.height / 2) > 1){
						node.links[i].SetOffset (lastRect.y + (lastRect.height / 2));
					}
				}
				EditorGUILayout.EndVertical ();

				EditorGUILayout.BeginVertical ();
				GUI.color = Color.green;
				if(node.Collapsed){
					GUILayout.Label ("C\nO\nL\nL\nA\nP\nS\nE\nD");
				}
				GUI.color = Color.white;
				EditorGUILayout.EndVertical ();

				EditorGUILayout.EndHorizontal ();

				if(repaint){
					Repaint();
				}

				DragNodes ();
			},
			node.representedNode.Name, UnityEditor.Graphs.Styles.GetNodeStyle ("node", nodeColor, _selection.Contains (node)));

			_allNodePos.Add (node.nodePos);
		}

		/// <summary>
		/// Draws the link using nodes as start and end points.
		/// </summary>
		/// <param name="startNode">Start node.</param>
		/// <param name="endNode">End node.</param>
		/// <param name="yOffset">Y offset in the node.</param>
		/// <param name="linkColor">Link color.</param>
		private void DrawLink(Node startNode, Node endNode, float yOffset, Color linkColor){
			Rect start = startNode.nodePos;
			Rect end = endNode.nodePos;

			Vector3 startPos = new Vector3(start.x + start.width, start.y + yOffset, 0);
			Vector3 endPos = new Vector3(end.x, end.y + (end.height / 2), 0);
			DrawLink (startPos, endPos, linkColor);
		}

		/// <summary>
		/// Draws the link using vectors as start and end points.
		/// </summary>
		/// <param name="startPos">Start position.</param>
		/// <param name="endPos">End position.</param>
		/// <param name="yOffset">Y offset in the node.</param>
		/// <param name="linkColor">Link color.</param>
		private void DrawLink(Vector3 startPos, Vector3 endPos, Color linkColor){
			Vector3[] points;
			Vector3[] tangents;

			if(_selected.Settings.useCurvedConnectors){
				GetCurvyConnectorValues (startPos, endPos, out points, out tangents);
		        Handles.DrawBezier(points[0], points[1], tangents[0], tangents[1], linkColor, (Texture2D)UnityEditor.Graphs.Styles.connectionTexture.image, _selected.Settings.linkWidth);
			}
			else{
				Handles.DrawBezier (startPos, endPos, endPos, startPos, linkColor, (Texture2D)UnityEditor.Graphs.Styles.connectionTexture.image, _selected.Settings.linkWidth);
			}
			
			
			// Good for connecting where direction is unimportant. But need a solution to show direction.
			/*startPos.y = start.y + start.height / 2;
			endPos.y = end.y + end.height / 2;
			GetAngularConnectorValues (startPos, endPos, out points, out tangents);
			DrawRoundedPolyLine (points, tangents, (Texture2D)UnityEditor.Graphs.Styles.connectionTexture.image, _selected._settings._linkColor);*/
			//Handles.DrawBezier (startPos, endPos, startPos, endPos, _selected._settings._linkColor, null, _selected._settings._linkWidth);
		}
		#endregion

		/// <summary>
		/// Handles events when in a node.
		/// </summary>
		/// <param name="n">Node.</param>
		private void SelectNode(Node n){
			Event current = Event.current;
			if(current.alt){
				return;
			}
			if (current.type == EventType.MouseDown && current.button == 0){
				if(_linkingNode){
					if(n.representedNode != _nodeToLink){
						_selected.ConnectNodes (_nodeToLink.representedNode.EditorObj, n.representedNode.EditorObj);
						_nodeToLink = null;
						_linkingNode = false;
						Repaint ();
					}
				}
				else if (EditorGUI.actionKey || current.shift){
					if (_selection.Contains (n)){
						_selection.Remove (n);
					}
					else{
						_selection.Add (n);
					}
					current.Use ();
				}
				else{
					if (!_selection.Contains (n)){
						_selection.Clear ();
						_selection.Add (n);
					}
					HandleUtility.Repaint ();
				}
				this.UpdateUnitySelection ();
			}
			else if(current.type == EventType.MouseUp && current.button == 1){
				if(_selection.Count > 1 && _selection.Contains (n)){
					DrawContextMenu (_selection.ToArray ());
				}
				else{
					DrawContextMenu (n);
				}
			}
		}

		/// <summary>
		/// Updates the unity selection using the selected nodes.
		/// </summary>
		private void UpdateUnitySelection(){
			List<UnityEngine.Object> newSelected = new List<UnityEngine.Object>();
			foreach(Node n in _selection){
				newSelected.Add (n.representedNode.EditorObj);
			}
			Selection.objects = newSelected.ToArray ();
		}

		#region Drag Functions
		/// <summary>
		/// Updates the scroll position to stay locked on position even if extents changed.
		/// </summary>
		private void UpdateScrollPosition (){
			scrollPos.x = scrollPos.x + (_lastGraphExtents.xMin - _graphExtents.xMin);
			scrollPos.y = scrollPos.y + (_lastGraphExtents.yMin - _graphExtents.yMin);
			_lastGraphExtents = _graphExtents;
		}

		/// <summary>
		/// Handles the events for dragging a node.
		/// </summary>
		private void DragNodes(){
			Event current = Event.current;
			int controlID = GUIUtility.GetControlID (FocusType.Passive);
			if(current.alt){
				return;
			}
			switch (current.GetTypeForControl (controlID)){
				case EventType.MouseDown:{
					if (current.button == 0){
						this._nodeDragDistance = current.mousePosition;
						this._initMousePos = current.mousePosition;
						foreach (Node node in _selection){
							_initialDragNodePosition [node] = node.nodePos;
						}
						GUIUtility.hotControl = controlID;
						current.Use ();
					}
					break;
				}
				case EventType.MouseUp:{
					if (GUIUtility.hotControl == controlID){
						foreach (Node node in _selection){
							EditorUtility.SetDirty (_selected as UnityEngine.Object);
						}
						this._initialDragNodePosition.Clear ();
						_graphExtents = GetGraphExtents ();
						GUIUtility.hotControl = 0;
					}
					break;
				}
				case EventType.MouseDrag:{
					if (GUIUtility.hotControl == controlID){
						this._nodeDragDistance += current.mousePosition - _initMousePos;
						foreach (Node node in _selection){
							Rect newPosition = node.nodePos;
							Rect rect = _initialDragNodePosition [node];
							newPosition.x = rect.x + _nodeDragDistance.x - _initMousePos.x;
							newPosition.y = rect.y + _nodeDragDistance.y - _initMousePos.y;
						
							node.nodePos = newPosition;
							EditorUtility.SetDirty (_selected as UnityEngine.Object);
						}
					}
					break;
				}
				case EventType.KeyDown:{
					if (GUIUtility.hotControl == controlID && current.keyCode == KeyCode.Escape){
						foreach (Node node in _selection){
							node.nodePos = _initialDragNodePosition [node];
							EditorUtility.SetDirty (_selected as UnityEngine.Object);
						}
						GUIUtility.hotControl = 0;
						current.Use ();
					}
					break;
				}
			}
		}

		/// <summary>
		/// Handles Events for dragging the graph.
		/// </summary>
		private void DragGraph (){
			int controlID = GUIUtility.GetControlID (FocusType.Passive);
			Event current = Event.current;
			if (current.button != 2 && (current.button != 0 || !current.alt)){
				return;
			}
			switch (current.GetTypeForControl (controlID)){
				case EventType.MouseDown:{
					GUIUtility.hotControl = controlID;
					current.Use ();
					EditorGUIUtility.SetWantsMouseJumping (1);
					break;
				}
				case EventType.MouseUp:{
					if (GUIUtility.hotControl == controlID){
						GUIUtility.hotControl = 0;
						current.Use ();
						EditorGUIUtility.SetWantsMouseJumping (0);
					}
					break;
				}
				case EventType.MouseMove:
				case EventType.MouseDrag:{
					if (GUIUtility.hotControl == controlID){
					Vector2 delta = current.delta;
					if((delta.x < 0 && position.width - _optionWindowRect.width + scrollPos.x + _graphExtents.xMin - delta.x > _graphExtents.xMax) ||
					   (delta.x > 0 && scrollPos.x + _graphExtents.xMin - delta.x < _graphExtents.xMin)){
						delta.x = 0;
					}

					if((delta.y < 0 && position.height + scrollPos.y + _graphExtents.yMin - delta.y > _graphExtents.yMax) ||
					   (delta.y > 0 && scrollPos.y + _graphExtents.yMin - delta.y < _graphExtents.yMin)){
						delta.y = 0;
					}
						scrollPos -= delta;
						current.Use ();
					}
					break;
				}
			}
		}

		/// <summary>
		/// Creates a draggable zone that selects all nodes it contains.
		/// </summary>
		/// <param name="position">Zone start position.</param>
		private void DragSelection (Rect position){
			int controlID = GUIUtility.GetControlID (FocusType.Passive);
			Event current = Event.current;
			switch (current.GetTypeForControl (controlID)){
			case EventType.MouseDown:{
				if (position.Contains (current.mousePosition) && current.button == 0 && current.clickCount != 2 && !current.alt){
					if(_linkingNode){
						_linkingNode = false;
						_nodeToLink = null;
					}
					GUIUtility.hotControl = controlID;
					_dragStartPoint = current.mousePosition;
					_oldSelection = new List<Node>(_selection);
					_selection.Clear ();
					_dragType = SelectionDragType.pick;
					current.Use ();
				}
				break;
			}
			case EventType.MouseUp:{
				if (GUIUtility.hotControl == controlID){
					GUIUtility.hotControl = 0;
					_oldSelection.Clear ();
					this.UpdateUnitySelection ();
					if(Selection.objects.Length == 0){
						Selection.objects = new Object[1]{(_selected as MonoBehaviour).gameObject};
					}
					_dragType = SelectionDragType.None;
					current.Use ();
				}
				break;
			}
			case EventType.MouseDrag:{
				if (GUIUtility.hotControl == controlID){
					_dragType = SelectionDragType.Rect;
					GetNodesInSelectionRect (GetRectBetweenPoints(_dragStartPoint, current.mousePosition));
					current.Use ();
				}
				break;
			}
			case EventType.KeyDown:{
				if (_dragType != SelectionDragType.None && current.keyCode == KeyCode.Escape){
					_selection = _oldSelection;
					GUIUtility.hotControl = 0;
					_dragType = SelectionDragType.None;
					current.Use ();
				}
				break;
			}
			case EventType.Repaint:{
				if (_dragType == SelectionDragType.Rect){
					UnityEditor.Graphs.Styles.selectionRect.Draw (GetRectBetweenPoints(_dragStartPoint, current.mousePosition), false, false, false, false);
				}
				break;
			}
			}
		}
		#endregion

		/// <summary>
		/// Sets the values for drawing a curved line.
		/// </summary>
		/// <param name="start">Start.</param>
		/// <param name="end">End.</param>
		/// <param name="points">Points.</param>
		/// <param name="tangents">Tangents.</param>
		private void GetCurvyConnectorValues (Vector2 start, Vector2 end, out Vector3[] points, out Vector3[] tangents){
			points = new Vector3[]{
				start,
				end
			};
			tangents = new Vector3[2];
			float num = (start.y >= end.y) ? 0.7f : 0.3f;
			num = 0.5f;
			float num2 = 1f - num;
			float num3 = 0f;
			if (start.x > end.x){
				num = (num2 = -0.25f);
				float f = (start.x - end.x) / (start.y - end.y);
				if (Mathf.Abs (f) > 0.5f){
					float num4 = (Mathf.Abs (f) - 0.5f) / 8f;
					num4 = Mathf.Sqrt (num4);
					num3 = Mathf.Min (num4 * 80f, 80f);
					if (start.y > end.y){
						num3 = -num3;
					}
				}
			}
			float d = Mathf.Clamp01 (((start - end).magnitude - 10f) / 50f);
			if(start.x < end.x){
				tangents [0] = start + new Vector2 ((end.x - start.x) * num + 30f, num3) * d;
				tangents [1] = end + new Vector2 ((end.x - start.x) * -num2 - 30f, -num3) * d;
			}
			else{
				tangents [0] = start - new Vector2 ((end.x - start.x) * num + 30f, num3) * d;
				tangents [1] = end - new Vector2 ((end.x - start.x) * -num2 - 30f, -num3) * d;
			}
		}

		/// <summary>
		///  Sets the values for drawing an angular-stepped line.
		/// </summary>
		/// <param name="start">Start.</param>
		/// <param name="end">End.</param>
		/// <param name="points">Points.</param>
		/// <param name="tangents">Tangents.</param>
		private void GetAngularConnectorValues (Vector2 start, Vector2 end, out Vector3[] points, out Vector3[] tangents){
			Vector2 a = start - end;
			Vector2 vector = a / 2f + end;
			Vector2 vector2 = new Vector2 (Mathf.Sign (a.x), Mathf.Sign (a.y));
			Vector2 vector3 = new Vector2 (Mathf.Min (Mathf.Abs (a.x / 2f), 5f), Mathf.Min (Mathf.Abs (a.y / 2f), 5f));
			points = new Vector3[]{
				start,
				new Vector3 (vector.x + vector3.x * vector2.x, start.y),
				new Vector3 (vector.x, start.y - vector3.y * vector2.y),
				new Vector3 (vector.x, end.y + vector3.y * vector2.y),
				new Vector3 (vector.x - vector3.x * vector2.x, end.y),
				end
			};
			tangents = new Vector3[]{
				(points [1] - points [0]).normalized * vector3.x * 0.6f + points [1],
				(points [2] - points [3]).normalized * vector3.y * 0.6f + points [2],
				(points [3] - points [2]).normalized * vector3.y * 0.6f + points [3],
				(points [4] - points [5]).normalized * vector3.x * 0.6f + points [4]
			};
		}

		/// <summary>
		/// Draws rounded corners.
		/// </summary>
		/// <param name="points">Points.</param>
		/// <param name="tangents">Tangents.</param>
		/// <param name="tex">Tex.</param>
		/// <param name="color">Color.</param>
		private static void DrawRoundedPolyLine (Vector3[] points, Vector3[] tangents, Texture2D tex, Color color){
			Handles.color = color;
			for (int i = 0; i < points.Length; i += 2){
				Handles.DrawAAPolyLine (tex, 3f, new Vector3[]{
					points [i],
					points [i + 1]
				});
			}
			for (int j = 0; j < tangents.Length; j += 2){
				Handles.DrawBezier (points [j + 1], points [j + 2], tangents [j], tangents [j + 1], color, tex, 3f);
			}
		}

		/// <summary>
		/// Gets the graph extents defined by the nodes present plus a padding.
		/// </summary>
		/// <returns>The graph extents.</returns>
		private Rect GetGraphExtents(){
			Rect extents = new Rect();
			if(_allNodePos.Count < 1){
				return extents;
			}
			
			extents = _allNodePos[0];
			for(int i = 1; i < _allNodePos.Count; i++){
				if(_allNodePos[i].xMin < extents.xMin){
					extents.xMin = _allNodePos[i].xMin;
				}
				
				if(_allNodePos[i].xMax > extents.xMax){
					extents.xMax = _allNodePos[i].xMax;
				}
				
				if(_allNodePos[i].yMin < extents.yMin){
					extents.yMin = _allNodePos[i].yMin;
				}
				
				if(_allNodePos[i].yMax > extents.yMax){
					extents.yMax = _allNodePos[i].yMax;
				}
			}
			
			extents.xMax += 100;
			extents.yMax += 100;
			
			extents.xMin -= 100;
			extents.yMin -= 100;
			
			if(extents.xMax < position.width - _optionWindowRect.width + scrollPos.x + extents.xMin){
				extents.xMax = position.width - _optionWindowRect.width + scrollPos.x + extents.xMin;
			}
			
			if(extents.xMin > scrollPos.x + extents.xMin){
				extents.xMin = scrollPos.x + extents.xMin;
			}
			
			if(extents.yMax < position.height + scrollPos.y + extents.yMin){
				extents.yMax = position.height + scrollPos.y + extents.yMin;
			}
			
			if(extents.yMin > scrollPos.y + extents.yMin){
				extents.yMin = scrollPos.y + extents.yMin;
			}
			
			return extents;
		}

		/// <summary>
		/// Creates a rect from two points.
		/// </summary>
		/// <returns>The rect between points.</returns>
		/// <param name="start">Start.</param>
		/// <param name="end">End.</param>
		private Rect GetRectBetweenPoints (Vector2 start, Vector2 end){
			Rect result = new Rect (start.x, start.y, end.x - start.x, end.y - start.y);
			if (result.width < 0f){
				result.x += result.width;
				result.width = -result.width;
			}
			
			if (result.height < 0f){
				result.y += result.height;
				result.height = -result.height;
			}
			
			return result;
		}

		/// <summary>
		/// Sets the selection to all nodes contained in the rect.
		/// </summary>
		/// <param name="r">The rect in which to look for nodes.</param>
		private void GetNodesInSelectionRect(Rect r){
			_selection.Clear ();
			foreach (Node current in _allNodes){
				Rect position = current.nodePos;
				
				if (position.xMax >= r.x && position.x <= r.xMax && position.yMax >= r.y && position.y <= r.yMax){
					_selection.Add (current);
				}
			}
		}

		// TODO: Finish implementing a method of adding new nodes and links to the nodemap.
		private void UpdateNodes(){
			if(_selected == null){
				return;
			}
			List<IAnyGraphNode> nodes = _selected.Nodes;
			foreach(IAnyGraphNode node in nodes){
				if(!_allNodes.Select (x => x.representedNode).Contains(node)){
					Node newNode = new Node(){
						representedNode = node,
						isRoot = false,
						guid = System.Guid.NewGuid ().ToString (),
						links = new List<Link>(),
						nodePos = new Rect()
					};
				}
			}
		}

		private void CheckNodeLinks(){
			foreach(Node n in _allNodes){
				if(n.representedNode.Links.Count != n.links.Count){
					//GenerateCompleteNodeMap (_selected.Nodes, _selected.ExplicitRootNodeName);
					return;
				}
			}
		}

		private void CheckNodes(){
			List<IAnyGraphNode> pulledNodes = _selected.Nodes;
			for(int i = 0; i < pulledNodes.Count; i++){
				if(!_cachedNodes.Contains(pulledNodes[i])){
					Debug.Log ("New Node");
					GenerateCompleteNodeMap (pulledNodes, _selected.ExplicitRootNodeName);
					return;
				}
			}
			
			for(int i = 0; i < _cachedNodes.Count; i++){
				if(!pulledNodes.Contains (_cachedNodes[i])){
					Debug.Log ("Discarded Node");
					GenerateCompleteNodeMap (pulledNodes, _selected.ExplicitRootNodeName);
				}
			}
		}

		/// <summary>
		/// Rearranges the nodes as tree.
		/// </summary>
		/// <param name="xSpacing">X spacing.</param>
		/// <param name="ySpacing">Y spacing.</param>
		private void RearrangeNodesAsTree(float xSpacing, float ySpacing, bool duplicateBranches = false){
			_needRearrange = false;

			List<List<Node>> allNodeLevels = new List<List<Node>>();
			List<Node> rootNodes = new List<Node>();
			rootNodes.AddRange (_allNodes.Where(x => x.isRoot));

			for(int i = 0; i < rootNodes.Count; i++){
				rootNodes[i].UpdateChildBlocks(_selected.Settings.nodePlacementOffset.y);
			}

			allNodeLevels.Add (rootNodes);

			while(true){
				List<Node> newLevel = new List<Node>();
				for(int i = 0; i < allNodeLevels.Last ().Count; i++){
					if(allNodeLevels.Last ()[i].Collapsed){
						continue;
					}

					for(int l = 0; l < allNodeLevels.Last ()[i].links.Count; l++){
						if(!string.IsNullOrEmpty(allNodeLevels.Last()[i].links[l].guid)){
							newLevel.Add (_allNodes.Find (x => x.guid == allNodeLevels.Last()[i].links[l].guid));
						}
					}
				}
				
				if(newLevel.Count == 0){
					break;
				}
				allNodeLevels.Add (newLevel);
			}

			if(_selected.Settings.structuringMode == AnyGraphSettings.GraphOrganizingMode.SpreadOut){
				float[] xOffset = new float[allNodeLevels.Count];
				float totOffset = 0;
				for(int i = 0; i < allNodeLevels.Count - 1; i++){
					for(int j = 0; j < allNodeLevels[i+1].Count; j++){
						xOffset[i+1] = Mathf.Max (allNodeLevels[i+1][j].nodePos.width, xOffset[i+1]);
					}
					xOffset[i+1] += _selected.Settings.nodePlacementOffset.x + totOffset;
					totOffset = xOffset[i+1];
				}

				foreach(Node n in rootNodes){
					n.nodePos = new Rect(0, 0, n.nodePos.width, n.nodePos.height);
					n.RecursiveSetPosition (1, xOffset);
				}
			}
			else if(_selected.Settings.structuringMode == AnyGraphSettings.GraphOrganizingMode.Pack){
				List<Rect> levelRects = new List<Rect>();
				foreach(List<Node> level in allNodeLevels){
					Rect levRect = new Rect(0, 0, 0, 0);
					foreach(Node node in level){
						if(levRect.width < node.nodePos.width)
							levRect.width = node.nodePos.width;
						
						levRect.height += node.nodePos.height;
					}
					levRect.height += (level.Count - 1) * ySpacing;
					
					levelRects.Add (levRect);
				}

				float nodeX = 0;
				for(int i = 0; i < levelRects.Count; i++){
					float nodeY = (position.height / 2) - (levelRects[i].height / 2);
					
					foreach(Node node in allNodeLevels[i]){
						node.nodePos = new Rect(nodeX, nodeY, node.nodePos.width, node.nodePos.height);
						nodeY += node.nodePos.height + ySpacing;
					}
					
					nodeX += levelRects[i].width + xSpacing;
				}
			}
			_graphExtents = GetGraphExtents();
		}
		
		[System.Serializable]
		private class Node{
			public IAnyGraphNode representedNode;
			public bool isRoot = false;
			public string guid;
			public List<Link> links;
			public Rect nodePos;
			public float heightExtent {get; private set;}
			private bool _collapsed = false;
			private List<Node> _parents = new List<Node>();

			public List<Node> SetupRecursively(){
				List<Node> linked = new List<Node>();
				if(representedNode == null){
					return linked;
				}
				
				List<Node> parents = Parents;
				parents.Add (this);

				foreach(AnyGraphLink link in representedNode.Links){
					if(link.connection != null && !parents.Select (x => x.representedNode).Contains (link.connection)){

						Node newNode = new Node(){
							representedNode = link.connection,
							isRoot = false,
							guid = System.Guid.NewGuid ().ToString (),
							links = new List<Link>(),
							nodePos = new Rect(),
							Parents = parents,
						};
						links.Add (new Link(){
							linkName = link.linkText,
							guid = newNode.guid
						});
						linked.Add (newNode);
						linked.AddRange (newNode.SetupRecursively ());
					}
					else{
						links.Add (new Link(){
							linkName = link.linkText
						});
					}

				}

				return linked;
			}

			public void UpdateChildBlocks(float yStep){
				int extentCount = 0;
				heightExtent = 0;

				if(!Collapsed){
					foreach(Link l in links.Where (x => x.TargetNode != null)){
						l.TargetNode.UpdateChildBlocks(yStep);
						heightExtent += l.TargetNode.heightExtent;
						extentCount++;
					}
				}
				
				if(extentCount == 0){
					heightExtent = nodePos.height;
				}

				heightExtent += yStep * (extentCount + 1);
			}

			public void RecursiveSetPosition(int level, float[] xOffsets){
				float nextY = nodePos.y - (nodePos.height / 2) + heightExtent / 2;
				for(int l = links.Count - 1; l >= 0; l--){
					if(links[l].TargetNode == null){
						continue;
					}

					links[l].TargetNode.nodePos = new Rect(xOffsets[level], nextY - (links[l].TargetNode.heightExtent / 2), links[l].TargetNode.nodePos.width, links[l].TargetNode.nodePos.height);
					nextY -= links[l].TargetNode.heightExtent;
					links[l].TargetNode.RecursiveSetPosition ((level + 1), xOffsets);
				}
			}

			public bool Collapsed{
				get{return _collapsed;}
				set{
					_collapsed = value;
					for(int i = 0; i < links.Count; i++){
						if(links[i].TargetNode != null){
							links[i].TargetNode.Collapsed = value;
						}
					}
				}
			}

			public List<Node> Parents{
				get{return _parents;}
				set{_parents = value;}
			}
		}

		[System.Serializable]
		private class Link{
			public string linkName;
			public string guid;
			public float yOffset;

			public void SetOffset(float newOffset){
				yOffset = newOffset;
			}

			public Node TargetNode{
				get{
					return string.IsNullOrEmpty(this.guid) ? null : _allNodes.Find (x => x.guid == this.guid);
				}
			}
		}
	}
	
	public class EditorZoomArea{
		private const float kEditorWindowTabHeight = 21.0f;
		private static Matrix4x4 _prevGuiMatrix;
		
		public static Rect Begin(float zoomScale, Rect screenCoordsArea){
			GUI.EndGroup();        // End the group Unity begins automatically for an EditorWindow to clip out the window tab. This allows us to draw outside of the size of the EditorWindow.
			
			Rect clippedArea = screenCoordsArea.ScaleSizeBy(1.0f / zoomScale, screenCoordsArea.TopLeft());
			clippedArea.y += kEditorWindowTabHeight;
			GUI.BeginGroup(clippedArea);
			
			_prevGuiMatrix = GUI.matrix;
			Matrix4x4 translation = Matrix4x4.TRS(clippedArea.TopLeft(), Quaternion.identity, Vector3.one);
			Matrix4x4 scale = Matrix4x4.Scale(new Vector3(zoomScale, zoomScale, 1.0f));
			GUI.matrix = translation * scale * translation.inverse * GUI.matrix;
			
			return clippedArea;
		}
		
		public static void End(){
			GUI.matrix = _prevGuiMatrix;
			GUI.EndGroup();
			GUI.BeginGroup(new Rect(0.0f, kEditorWindowTabHeight, Screen.width, Screen.height));
		}
	}
}