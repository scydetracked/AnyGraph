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
		private const int kFrameThrottle = 4;
		private const float kToolbarButtonSize = 40;
		
		private Rect _zoomArea;
		private float _zoom = 1.0f;
		private Vector2 _zoomCoordsOrigin = Vector2.zero;

		private Vector2 _nodeDragDistance = new Vector2();
		private Vector2 _initMousePos = new Vector2();

		private Vector2 _scrollPos = new Vector2();
		private Rect _graphExtents;
		private Rect _lastGraphExtents;
		private Vector2 _dragStartPoint;
		private SelectionDragType _dragType = SelectionDragType.None;

		private bool _optionWindowOpen = false;
		private Rect _toolbarRect;
		private Vector2 _optionWindowScrollPos = new Vector2();

		private Dictionary<Node, Rect> _initialDragNodePosition = new Dictionary<Node, Rect>();
		private List<Rect> _allNodePos = new List<Rect>();
		private List<IAnyGraphNode> _cachedNodes = new List<IAnyGraphNode>();
		private IAnyGraphable _selected = null;
		private bool _linkingNode = false;
		private Node _nodeToLink = null;

		private static List<Node> _allNodes = new List<Node>();
		private List<Node> _selection = new List<Node>();
		private List<Node> _oldSelection = new List<Node>();

		private int _updateThrottler = 0;

		private bool _needRearrange = false;
		private IEnumerator _rearrange;
		private bool _passedRepaint = false;

		private Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
	
		private string _searchString = "";

		// Settings are saved on a per-type basis. When trying to access unexisting settings, a new instance is created and saved in the project.
		private const string _settingsPath = "Assets/AnyGraphSettings.asset";
		private AnyGraphSavedSettings _loadedSettings;
		private AnyGraphSettings SelectedSettings{
			get{
				if(_loadedSettings == null){
					_loadedSettings = AssetDatabase.LoadAssetAtPath (_settingsPath, typeof(AnyGraphSavedSettings)) as AnyGraphSavedSettings;
					if(_loadedSettings == null){
						_loadedSettings = ScriptableObject.CreateInstance<AnyGraphSavedSettings>();
						AssetDatabase.CreateAsset (_loadedSettings, _settingsPath);
					}
				}

				return _loadedSettings.GetSettings (_selected.GetType ());
			}
		}

		[MenuItem("Window/AnyGraph %g")]
		public static void Openwindow(){
			EditorWindow.GetWindow<AnyGraph>("AnyGraph", true);
		}

		private void OnEnable(){
			// Load the required textures if they haven't been loaded yet.
			if(textures.Count == 0){
				string[] texuresToAdd = new string[]{"Connect", "Disconnect", "Options", "Collapse", "Expand", "Refresh", "BreakPoint"};
				
				string assetPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject (this));
				int endIndex = assetPath.LastIndexOf ("/");
				assetPath = assetPath.Substring (0, endIndex + 1) + "EditorAssets/";

				string proString = Application.HasProLicense () ? "_pro" : "";
				for(int i = 0; i < texuresToAdd.Length; i++){
					textures.Add (texuresToAdd[i], AssetDatabase.LoadAssetAtPath (assetPath + texuresToAdd[i] + proString + ".png", typeof(Texture)) as Texture);
					if(textures[texuresToAdd[i]] == null){
						Debug.LogWarning(string.Format ("AnyGraph-> Could not locate texture \"{0}\"", texuresToAdd[i]));
					}
				}
			}

			Reset ();
		}

		private void OnDisable(){
			Reset ();
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
			
			_scrollPos = new Vector2();
			_graphExtents = new Rect();
			_lastGraphExtents = new Rect();
			_dragStartPoint = new Vector2();
			_dragType = SelectionDragType.None;

			_toolbarRect = new Rect();
			_optionWindowScrollPos = new Vector2();

			_initialDragNodePosition = new Dictionary<Node, Rect>();
			_allNodePos = new List<Rect>();
			_cachedNodes = new List<IAnyGraphNode>();
			_selected = null;
			_linkingNode = false;
			_nodeToLink = null;

			_allNodes = new List<Node>();
			_selection = new List<Node>();
			_oldSelection = new List<Node>();

			_needRearrange = false;
			_rearrange = null;

			_searchString = "";

			// Call on the garbage collector to free up resources.
			System.GC.Collect ();
		}

		/// <summary>
		/// Handles the input events for zooming and cancelling node linking.
		/// </summary>
		private void HandleEvents(){
			if(Event.current.type == EventType.MouseDown){
				EditorGUIUtility.keyboardControl = 0;
			}

			if (Event.current.type == EventType.ScrollWheel){
				Vector2 screenCoordsMousePos = Event.current.mousePosition;
				Vector2 delta = Event.current.delta;
				Vector2 zoomCoordsMousePos = ConvertScreenCoordsToZoomCoords(screenCoordsMousePos);
				float zoomDelta = -delta.y / 150.0f;
				float oldZoom = _zoom;
				_zoom += zoomDelta;
				_zoom = Mathf.Clamp(_zoom, kZoomMin, kZoomMax);

				Vector2 toAdd = (zoomCoordsMousePos - _zoomCoordsOrigin) - (oldZoom / _zoom) * (zoomCoordsMousePos - _zoomCoordsOrigin);
				_scrollPos += toAdd;
				_zoomCoordsOrigin += toAdd;
				
				Event.current.Use ();
			}

			if(_linkingNode && Event.current.type == EventType.MouseDown && Event.current.button == 1){
				_linkingNode = false;
				_nodeToLink = null;
				Event.current.Use ();
			}

			if(Event.current.modifiers == EventModifiers.Control){
				switch(Event.current.keyCode){
				case KeyCode.Alpha0:	// Reset zoom
					_zoom = 1;
					break;
				case KeyCode.Equals:	// Zoom-in
					_zoom += 0.05f;
					break;
				case KeyCode.Minus:		// Zoom-out
					_zoom -= 0.05f;
					break;
				}

				_zoom = Mathf.Clamp (_zoom, kZoomMin, kZoomMax);
			}
		}

		/// <summary>
		/// Updates the GUI. Base GUI update is 100 times per second.
		/// GUI updates are throttled to 100 / kFrameThrottle times per second.
		/// </summary>
		private void Update(){
			if(_updateThrottler == 0){
				Repaint ();
			}
			_updateThrottler = _updateThrottler++ % kFrameThrottle;
		}

		/// <summary>
		/// Generates a recursive node map to be used by the graph.
		/// </summary>
		/// <param name="nodes">Nodes to generate a map for.</param>
		private void GenerateCompleteNodeMap(List<IAnyGraphNode> nodes){
			_cachedNodes = nodes;
			_allNodes = new List<Node>();
			List<IAnyGraphNode> rootNodes = new List<IAnyGraphNode>();

			if(!string.IsNullOrEmpty(_selected.ExplicitRootNodeName)){
				for(int i = 0; i < _cachedNodes.Count; i++){
					if(_cachedNodes[i].Name == _selected.ExplicitRootNodeName){
						rootNodes.Add (_cachedNodes[i]);
						break;
					}
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

			for(int i = 0; i < rootNodes.Count; i++){
				if(rootNodes[i] == null){
					continue;
				}

				Node newNode = new Node(){
					representedNode = rootNodes[i],
					isRoot = true,
					guid = System.Guid.NewGuid ().ToString (),
					links = new List<Link>(),
					nodePos = new Rect(),
				};
				
				_allNodes.Add (newNode);
				_allNodes.AddRange (newNode.SetupRecursively (new List<IAnyGraphNode>()));
			}

			_needRearrange = true;
		}

		private void OnGUI(){

			// Draw the grid and background.
			Rect grapphRect = new Rect(0, 0, position.width - _toolbarRect.width, position.height);
			GUI.Box (grapphRect, "", UnityEditor.Graphs.Styles.graphBackground);
			DrawGrid (grapphRect);
			
			// Draw the search bar.
			Rect searchBarRect = new Rect(0, 0, position.width - _toolbarRect.width, 16);
			DrawSearchBar (searchBarRect);

			// Draw the Toolbar.
			DrawToolbar();

			_zoomArea = new Rect(0, searchBarRect.height, position.width - _toolbarRect.width, position.height - searchBarRect.height);

			// If it isn't a repaint event, we can modify the node and links.
			if(Event.current.type != EventType.Repaint){
				// Update the currently selected object.
				IAnyGraphable newSelected = null;

				if(Selection.activeObject is IAnyGraphable){
					newSelected = Selection.activeObject as IAnyGraphable;
				}
				else if(Selection.activeGameObject != null){
					MonoBehaviour[] behaviours = Selection.activeGameObject.GetComponents<MonoBehaviour>();
					for(int i = 0; i < behaviours.Length; i++){
						if(behaviours[i] is IAnyGraphable){
							newSelected = behaviours[i] as IAnyGraphable;
						}
					}
				}

				// If the selected object has changed, we need to regraph.
				if(newSelected != _selected && newSelected != null){
					Reset ();
					_selected = newSelected;
					GenerateCompleteNodeMap (_selected.Nodes);
				}

				if(_selected != null){
					CheckNodes ();
					CheckNodeLinks ();

					// Move to the next rearrange iteration if it isn't null.
					if(_rearrange != null && _passedRepaint){
						_passedRepaint = false;
						if(!_rearrange.MoveNext ()){
							_rearrange = null;
						}
					}
					// Start a new rearrange if the instance was null and the graph needs rearranging.
					else if(_needRearrange){
						_passedRepaint = false;
						RearrangeTree(SelectedSettings.nodePlacementOffset.x, SelectedSettings.nodePlacementOffset.y);
					}
				}
			}
			else{
				_passedRepaint = true;
			}
			
			HandleEvents ();

			// if there is nothing to draw.
			if(_selected == null){
				ShowNotification (new GUIContent("No graphable object\nselected."));
				return;
			}
			else{
				RemoveNotification ();
			}

			// Set the main view rect by removing the search bar and toolbar from the window size.
			Rect scrollViewRect = EditorZoomArea.Begin (_zoom, new Rect(0, 0, position.width - _toolbarRect.width, position.height - searchBarRect.height), searchBarRect.height);
			scrollViewRect.y -= 21 + searchBarRect.height;

			if(scrollViewRect.width > _graphExtents.width){
				_scrollPos.x += (scrollViewRect.width - _graphExtents.width) / 2;
			}

			if(scrollViewRect.height > _graphExtents.height){
				_scrollPos.y += (scrollViewRect.height - _graphExtents.height) / 2;
			}

			// Start graph scroll view.
			_scrollPos = GUI.BeginScrollView (scrollViewRect, _scrollPos, _graphExtents, GUIStyle.none, GUIStyle.none);

			// Get the current active path from the selected IAnyGraphable.
			// Recursively set active nodes.
			string[] activePath = _selected.ActiveNodePath;
			if(activePath != null && activePath.Length > 0){
				for(int i = 0; i < _allNodes.Count; i++){
					if(_allNodes[i].isRoot && _allNodes[i].representedNode.Name == activePath[0]){
						_allNodes[i].SetActiveRecursively (activePath, 0);
						break;
					}
				}
			}

			DrawLinks ();
			DrawNodes ();

			DragSelection(new Rect(-5000f, -5000f, 10000f, 10000f));

			UpdateScrollPosition ();
			DragGraph();

			GUI.EndScrollView();
			EditorZoomArea.End ();
		}

		#region Drawing Functions
		/// <summary>
		/// Draw the context menu for multiple nodes.
		/// </summary>
		/// <param name="n">Nodes affected by context menu.</param>
		private void DrawContextMenu(Node[] n){
			GenericMenu menu = new GenericMenu();
			menu.AddItem (new GUIContent("Debug/Set Breakpoint"), false, delegate() {
				for(int i = 0; i < n.Length; i++){
					n[i].breakpoint = true;
				}
			});

			menu.AddItem (new GUIContent("Formating/Expand"), false, delegate() {
				for(int i = 0; i < n.Length; i ++){
					n[i].Collapsed = false;
				}
				RearrangeTree (SelectedSettings.nodePlacementOffset.x, SelectedSettings.nodePlacementOffset.y);
			});

			menu.AddItem (new GUIContent("Formating/Collapse"), false, delegate() {
				for(int i = 0; i < n.Length; i++){
					n[i].Collapsed = true;
				}
				RearrangeTree (SelectedSettings.nodePlacementOffset.x, SelectedSettings.nodePlacementOffset.y);
			});

			if(n.Length == 1){
				menu.AddItem (new GUIContent("Formating/Collapse All But This"), false, delegate() {
					Node current = n[0];
					while(current.parentNode != null){
						Node parent = current.parentNode;
						for(int i = 0; i < parent.links.Count; i++){
							if(parent.links[i].TargetNode != null && !System.Object.ReferenceEquals (parent.links[i].TargetNode, current)){
								parent.links[i].TargetNode.Collapsed = true;

							}
						}
						current = parent;
					}
				});
			}
			else{
				menu.AddDisabledItem (new GUIContent("Formating/Collapse All But This"));
			}

			// Add custom actions that have been defined by the implementation, if any.
			KeyValuePair<string, System.Action<IAnyGraphNode>>[] customActions = _selected.ContextActions;
			if(customActions.Length > 0){
				menu.AddSeparator ("");
			}
			for(int i = 0; i < customActions.Length; i++){
				int index = i;
				menu.AddItem (new GUIContent(customActions[i].Key), false, delegate {
					for(int j = 0; j < n.Length; j++){
						customActions[index].Value(n[j].representedNode);
					}
				});
			}

			// TODO: Need to fix the positioning of the context menu.
			Vector2 mousePos = (Event.current.mousePosition);
			menu.DropDown (new Rect(mousePos.x, mousePos.y, 0, 0));
			
			menu.ShowAsContext ();
		}

		/// <summary>
		/// Draws the search bar at the top of the window.
		/// </summary>
		/// <param name="area">Search bar area.</param>
		private void DrawSearchBar(Rect area){
			GUI.BeginGroup (area, GUI.skin.FindStyle("Toolbar"));
			_searchString = GUI.TextField (new Rect(4, (area.height - 15) / 2, position.width - _toolbarRect.width - 19, 15), _searchString, GUI.skin.FindStyle("ToolbarSeachTextField"));
			if(GUI.Button (new Rect(position.width - _toolbarRect.width - 19, (area.height - 15) / 2, 15, 15), "", GUI.skin.FindStyle("ToolbarSeachCancelButton"))){
				_searchString = "";
			}
			GUI.EndGroup ();
		}

		/// <summary>
		/// Draws the options window. All custom drawing from IAnyGraphable will be called in here as well.
		/// </summary>
		private void DrawToolbar(){
			// Set the toolbar rect size depending on its state.
			if(_optionWindowOpen){
				_toolbarRect = new Rect(position.width - 322, 0, 322, position.height);
			}
			else{
				_toolbarRect = new Rect(position.width - 42, 0, 42, position.height);
			}

			GUI.Box (_toolbarRect, "", UnityEditor.Graphs.Styles.graphBackground);
			GUILayout.BeginArea (_toolbarRect);
			int offsetStep = Mathf.RoundToInt (kToolbarButtonSize) + 2;
			int curOffset = -offsetStep;
			if(_selected == null || _selected is IAnyGraphableLinkable){
				if(GUI.Button (new Rect(1, curOffset = curOffset + offsetStep, kToolbarButtonSize, kToolbarButtonSize), new GUIContent(textures["Connect"], "Connect Selected Nodes Together."))){
					// TODO: Implement node connecting here.
					Debug.LogWarning ("AnyGraph-> Node connecting has not yet been implemented.");
				}
				if(GUI.Button (new Rect(1, curOffset = curOffset + offsetStep, kToolbarButtonSize, kToolbarButtonSize), new GUIContent(textures["Disconnect"], "Disconnect Selected Nodes."))){
					// TODO: Implement node disconnecting here.
					Debug.LogWarning ("AnyGraph-> Node disconnecting has not yet been implemented.");
				}
			}
			if(GUI.Button (new Rect(1, curOffset = curOffset + offsetStep, kToolbarButtonSize, kToolbarButtonSize), new GUIContent(textures["Collapse"], "Collapse Selected Nodes.")) && _selected != null){
				for(int i = 0; i < _selection.Count; i++){
					_selection[i].Collapsed = true;
				}
				RearrangeTree (SelectedSettings.nodePlacementOffset.x, SelectedSettings.nodePlacementOffset.y);
			}
			if(GUI.Button (new Rect(1, curOffset = curOffset + offsetStep, kToolbarButtonSize, kToolbarButtonSize), new GUIContent(textures["Expand"], "Expand Selected Nodes.")) && _selected != null){
				for(int i = 0; i < _selection.Count; i++){
					_selection[i].Collapsed = false;
				}
				RearrangeTree (SelectedSettings.nodePlacementOffset.x, SelectedSettings.nodePlacementOffset.y);
			}
			if(GUI.Button (new Rect(1, curOffset = curOffset + offsetStep, kToolbarButtonSize, kToolbarButtonSize), new GUIContent(textures["BreakPoint"], "Toggle breakpoints on selected nodes.")) && _selected != null){
				for(int i = 0; i < _selection.Count; i++){
					_selection[i].breakpoint = !_selection[i].breakpoint;
					if(!_selection[i].breakpoint){
						_selection[i].nodePos.height = 0;
					}
				}
			}
			if(GUI.Button (new Rect(1, curOffset = curOffset + offsetStep, kToolbarButtonSize, kToolbarButtonSize), new GUIContent(textures["Refresh"], "Force The Graph To Refresh.")) && _selected != null){
				Reset ();
			}
			if(GUI.Button (new Rect(1, curOffset = curOffset + offsetStep, kToolbarButtonSize, kToolbarButtonSize), new GUIContent(textures["Options"], "Open The Options WIndow.")) && _selected != null){
				_optionWindowOpen = !_optionWindowOpen;
			}

			// Options window. Will be drawn only if it is opened.
			GUI.BeginGroup (new Rect(42, 0, _toolbarRect.width - 42, _toolbarRect.height));
			if(_optionWindowOpen){
				_optionWindowScrollPos = GUILayout.BeginScrollView(_optionWindowScrollPos, GUILayout.MaxHeight (_toolbarRect.height - 20),GUILayout.MaxWidth (_toolbarRect.width - 42));

				SelectedSettings.nodePlacementOffset = EditorGUILayout.Vector2Field ("Auto-Placement Offset", SelectedSettings.nodePlacementOffset);
				SelectedSettings.structuringMode = (AnyGraphSettings.GraphOrganizingMode)EditorGUILayout.EnumPopup ("Placement Type", SelectedSettings.structuringMode);
				SelectedSettings.drawLinkOnTop = EditorGUILayout.Toggle ("Draw Links On Top", SelectedSettings.drawLinkOnTop);
				SelectedSettings.linkWidth = EditorGUILayout.FloatField ("Link Width", SelectedSettings.linkWidth);
				SelectedSettings.baseLinkColor = EditorGUILayout.ColorField ("Base Link Color", SelectedSettings.baseLinkColor);
				SelectedSettings.selectedNodeColor = (AnyGraphSettings.NodeColors)EditorGUILayout.EnumPopup("Selected Node Color", SelectedSettings.selectedNodeColor);
				SelectedSettings.selectedLinkColor = EditorGUILayout.ColorField ("Selected Link Color", SelectedSettings.selectedLinkColor);

				EditorGUILayout.Space ();
				SelectedSettings.colorFromSelected = EditorGUILayout.Toggle ("Color Links From Selected", SelectedSettings.colorFromSelected);
				if(SelectedSettings.colorFromSelected){
					EditorGUI.indentLevel++;
					SelectedSettings.fromNodeColor = (AnyGraphSettings.NodeColors)EditorGUILayout.EnumPopup("Selected Node Color", SelectedSettings.fromNodeColor);
					SelectedSettings.fromLinkColor = EditorGUILayout.ColorField ("From Link Color", SelectedSettings.fromLinkColor);
					EditorGUI.indentLevel--;
				}
				SelectedSettings.colorToSelected = EditorGUILayout.Toggle ("Color Links To Selected", SelectedSettings.colorToSelected);
				if(SelectedSettings.colorToSelected){
					EditorGUI.indentLevel++;
					SelectedSettings.toNodeColor = (AnyGraphSettings.NodeColors)EditorGUILayout.EnumPopup("Selected Node Color", SelectedSettings.toNodeColor);
					SelectedSettings.toLinkColor = EditorGUILayout.ColorField ("To Link Color", SelectedSettings.toLinkColor);
					EditorGUI.indentLevel--;
				}

				// Draw custom gui implemented by user.
				_selected.AdditionalOptionsGUI ();
				
				// Consume the event.
				if(Event.current.type == EventType.MouseDown || Event.current.type == EventType.mouseUp){
					Event.current.Use ();
				}

				GUILayout.EndScrollView();
			}
			GUI.EndGroup ();
			GUILayout.EndArea ();
		}

		/// <summary>
		/// Draws the background grid.
		/// </summary>
		private void DrawGrid (Rect area){
			if (Event.current.type != EventType.Repaint){
				return;
			}
			Profiler.BeginSample ("DrawGrid");
			GL.PushMatrix ();
			GL.Begin (1);
			DrawGridLines (15f, Color.white, area);
			DrawGridLines (150f, Color.gray, area);
			GL.End ();
			GL.PopMatrix ();
			Profiler.EndSample ();
		}

		/// <summary>
		/// Draws the grid lines.
		/// </summary>
		/// <param name="gridSize">Line spacing.</param>
		/// <param name="gridColor">Line color.</param>
		private void DrawGridLines (float gridSize, Color gridColor, Rect extents){
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
					Color linkColor = SelectedSettings.baseLinkColor;
					if(n.active && l.TargetNode.active){
						linkColor = new Color(1, 0, 0, 1);
					}
					else if(SelectedSettings.colorFromSelected && _selection.Find (x => x.links.Contains (l)) != null &&
					   SelectedSettings.colorToSelected && _selection.Select (x => x.guid).Contains (l.guid)){
						linkColor = SelectedSettings.selectedLinkColor;
					}
					else if(SelectedSettings.colorFromSelected && _selection.Find (x => x.links.Contains (l)) != null){
						linkColor = SelectedSettings.fromLinkColor;
					}
					else if(SelectedSettings.colorToSelected && _selection.Select (x => x.guid).Contains (l.guid)){
						linkColor = SelectedSettings.toLinkColor;
					}

					DrawLink(n, _allNodes.Find (x => x.guid == l.guid), l.yOffset, linkColor);
				}
			}

			// Draw an extra link if the user is currently connecting a node.
			if(_linkingNode){
				if(_nodeToLink == null){
					_linkingNode = false;
				}
				else{
					Color linkColor = SelectedSettings.baseLinkColor;
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
	
			_allNodePos.Clear ();
			foreach(Node n in _allNodes){
				if(n.parentNode == null || !n.parentNode.Collapsed){
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
			if(node.representedNode == null){
				return;
			}

			UnityEditor.Graphs.Styles.Color nodeColor = UnityEditor.Graphs.Styles.Color.Gray;
			bool recolored = false;

			// Color if the node is in the active path.
			if(node.active){
				nodeColor = UnityEditor.Graphs.Styles.Color.Red;
				recolored = true;
				node.active = false;
				if(node.breakpoint && !EditorApplication.isPaused && EditorApplication.isPlaying){
					Debug.LogWarning (string.Format ("AnyGraph-> Debug breakpoint triggered by node \"{0}\".\nNode's path is \"{1}\"", node.representedNode.Name, node.NodePath));
					Debug.Break ();
				}
			}

			if(!string.IsNullOrEmpty (_searchString)){
				string realSearchString = _searchString;

				if(realSearchString.StartsWith ("link:") || realSearchString.StartsWith("node:")){
					realSearchString = _searchString.Remove (0, 5);
				}

				if(!recolored && !_searchString.StartsWith ("link:") && node.representedNode.Name.ToLower ().Contains (realSearchString.ToLower ())){
					nodeColor = UnityEditor.Graphs.Styles.Color.Green;
					recolored = true;
				}

				for(int i = 0; !recolored && !_searchString.StartsWith ("node:") && i < node.links.Count; i++){
					if(node.links[i].linkName.ToLower ().Contains (realSearchString.ToLower ())){
						nodeColor = UnityEditor.Graphs.Styles.Color.Green;
						recolored = true;
					}
				}
			}

			// Color if the node is going to the selected node.
			if(SelectedSettings.colorToSelected && !recolored){
				foreach(Link l in node.links){
					if(_selection.Select (x => x.guid).Contains (l.guid)){
						nodeColor = (UnityEditor.Graphs.Styles.Color)(int)SelectedSettings.toNodeColor;
						recolored = true;
						break;
					}
				}
			}

			// Color if the node is coming from the selected node.
			if(SelectedSettings.colorFromSelected && !recolored){
				foreach(Node selectedNode in _selection){
					if(selectedNode.links.Select (x => x.guid).Contains (node.guid)){
						nodeColor = (UnityEditor.Graphs.Styles.Color)(int)SelectedSettings.fromNodeColor;
						recolored = true;
						break;
					}
				}
			}

			// Color if node is selected.
			if(!recolored && _selection.Contains (node)){
				nodeColor = (UnityEditor.Graphs.Styles.Color)(int)SelectedSettings.selectedNodeColor;
				recolored = true;
			}

			// Draw node.
			node.nodePos = GUILayout.Window (_allNodes.FindIndex (x => x.Equals(node)), node.nodePos, delegate{
				float width = GUILayoutUtility.GetRect (new GUIContent(node.representedNode.Name), "Label").width;
				SelectNode (node);

				if(node.breakpoint){
					GUI.color = Color.red;
					GUILayout.FlexibleSpace ();
					if(GUILayout.Button ("Breakpoint")){
						node.breakpoint = false;
						node.nodePos.height = 0;
					}
					GUILayout.FlexibleSpace ();
					GUI.color = Color.white;
				}

				if(!node.Collapsed){
					// TODO: Implement node linking button somewhere here.

					_selected.DrawNode (node.representedNode);

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
				}
				else if(node.representedNode.Links.Count > 0){
					Color oldColor = GUI.color;
					GUI.color = Color.green;
					GUILayout.Label ("Collapsed");
					GUI.color = oldColor;
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
			if(startNode == null || endNode == null){
				return;
			}

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

			GetCurvyConnectorValues (startPos, endPos, out points, out tangents);
	        Handles.DrawBezier(points[0], points[1], tangents[0], tangents[1], linkColor, (Texture2D)UnityEditor.Graphs.Styles.connectionTexture.image, SelectedSettings.linkWidth);
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
						(_selected as IAnyGraphableLinkable).ConnectNodes (_nodeToLink.representedNode, n.representedNode);
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
						_selection = new List<Node>();
						_selection.Add (n);
					}
					HandleUtility.Repaint ();
				}
				this.UpdateUnitySelection ();
			}
			else if(current.type == EventType.MouseUp && current.button == 1){
				if(!_selection.Contains (n)){
					_selection = new List<Node>();
					_selection.Add (n);
				}
				DrawContextMenu (_selection.ToArray ());
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
			_scrollPos.x = _scrollPos.x + (_lastGraphExtents.xMin - _graphExtents.xMin);
			_scrollPos.y = _scrollPos.y + (_lastGraphExtents.yMin - _graphExtents.yMin);
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
						_initialDragNodePosition.Clear ();
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
						}
					}
					break;
				}
				case EventType.KeyDown:{
					if (GUIUtility.hotControl == controlID && current.keyCode == KeyCode.Escape){
						foreach (Node node in _selection){
							node.nodePos = _initialDragNodePosition [node];
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
					if((delta.x < 0 && position.width - _toolbarRect.width + _scrollPos.x + _graphExtents.xMin - delta.x > _graphExtents.xMax) ||
					   (delta.x > 0 && _scrollPos.x + _graphExtents.xMin - delta.x < _graphExtents.xMin)){
						delta.x = 0;
					}

					if((delta.y < 0 && position.height + _scrollPos.y + _graphExtents.yMin - delta.y > _graphExtents.yMax) ||
					   (delta.y > 0 && _scrollPos.y + _graphExtents.yMin - delta.y < _graphExtents.yMin)){
						delta.y = 0;
					}
						_scrollPos -= delta;
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
					_oldSelection = new List<Node>();
					_selection = new List<Node>();
					_dragType = SelectionDragType.pick;
					current.Use ();
				}
				break;
			}
			case EventType.MouseUp:{
				if (GUIUtility.hotControl == controlID){
					GUIUtility.hotControl = 0;
					_oldSelection = new List<Node>();
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
					_selection = new List<Node>();
					_selection = GetNodesInSelectionRect (GetRectBetweenPoints(_dragStartPoint, current.mousePosition));
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
			
			if(extents.xMax < position.width - _toolbarRect.width + _scrollPos.x + extents.xMin){
				extents.xMax = position.width - _toolbarRect.width + _scrollPos.x + extents.xMin;
			}
			
			if(extents.xMin > _scrollPos.x + extents.xMin){
				extents.xMin = _scrollPos.x + extents.xMin;
			}
			
			if(extents.yMax < position.height + _scrollPos.y + extents.yMin){
				extents.yMax = position.height + _scrollPos.y + extents.yMin;
			}
			
			if(extents.yMin > _scrollPos.y + extents.yMin){
				extents.yMin = _scrollPos.y + extents.yMin;
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
		private List<Node> GetNodesInSelectionRect(Rect r){
			List<Node> nodesInRect = new List<Node>();
			foreach (Node current in _allNodes){
				Rect position = current.nodePos;
				
				if (position.xMax >= r.x && position.x <= r.xMax && position.yMax >= r.y && position.y <= r.yMax){
					nodesInRect.Add (current);
				}
			}
			return nodesInRect;
		}

		/// <summary>
		/// Checks each nodes to see if links have been added or removed.
		/// </summary>
		private void CheckNodeLinks(){
			for(int i = 0; i < _allNodes.Count; i++){
				if(_allNodes[i].representedNode.Links.Count != _allNodes[i].links.Count){
					Debug.Log ("AnyGraph-> Links did not match in a node, regenerating node map.");
					GenerateCompleteNodeMap (_selected.Nodes);
					return;
				}
			}
		}

		// Checks cached nodes against the selected object's nodes.
		private void CheckNodes(){
			List<IAnyGraphNode> selectedNodes = new List<IAnyGraphNode>();
			selectedNodes.AddRange (_selected.Nodes);

			for(int i = 0; i < _cachedNodes.Count; i++){
				if(!selectedNodes.Contains (_cachedNodes[i])){
					Debug.Log ("AnyGraph-> A node was removed, regenerating node map.");
					GenerateCompleteNodeMap (_selected.Nodes);
					return;
				}

				selectedNodes.Remove (_cachedNodes[i]);
			}

			if(selectedNodes.Count > 0){
				Debug.Log ("AnyGraph-> A node was added, regenerating node map.");
				GenerateCompleteNodeMap (_selected.Nodes);
			}
		}

		/// <summary>
		/// Starts a new rearrange Enumerator.
		/// </summary>
		/// <param name="xSpacing">X spacing.</param>
		/// <param name="ySpacing">Y spacing.</param>
		private void RearrangeTree(float xSpacing, float ySpacing){
			if(_rearrange == null){
				_rearrange = RearrangeNodesAsTree (xSpacing, ySpacing);
				_rearrange.MoveNext ();
				_needRearrange = false;
			}
		}

		/// <summary>
		/// Rearranges the nodes as tree.
		/// </summary>
		/// <param name="xSpacing">X spacing.</param>
		/// <param name="ySpacing">Y spacing.</param>
		private IEnumerator RearrangeNodesAsTree(float xSpacing, float ySpacing){
			yield return null;
			/*for(int i = 0; i < _allNodes.Count; i++){
				if(_allNodes[i] != null && _allNodes[i].representedNode.IsNodeRedundant()){
					Debug.LogWarning ("There is a redundancy in the graph. Aborting tree graphing.");
					yield break;
				}
			}*/

			List<List<Node>> allNodeLevels = new List<List<Node>>();
			List<Node> rootNodes = new List<Node>();
			rootNodes.AddRange (_allNodes.Where(x => x.isRoot));

			for(int i = 0; i < rootNodes.Count; i++){
				rootNodes[i].UpdateChildBlocks(SelectedSettings.nodePlacementOffset.y);
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

			if(SelectedSettings.structuringMode == AnyGraphSettings.GraphOrganizingMode.SpreadOut){
				float[] xOffset = new float[allNodeLevels.Count];
				float totOffset = 0;
				for(int i = 0; i < allNodeLevels.Count - 1; i++){
					for(int j = 0; j < allNodeLevels[i+1].Count; j++){
						xOffset[i+1] = Mathf.Max (allNodeLevels[i+1][j].nodePos.width, xOffset[i+1]);
					}
					xOffset[i+1] += SelectedSettings.nodePlacementOffset.x + totOffset;
					totOffset = xOffset[i+1];
				}

				foreach(Node n in rootNodes){
					n.nodePos = new Rect(0, 0, n.nodePos.width, n.nodePos.height);
					n.RecursiveSetPosition (1, xOffset);
				}
			}
			else if(SelectedSettings.structuringMode == AnyGraphSettings.GraphOrganizingMode.Pack){
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
			yield return null;
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
			public Node parentNode;
			private bool _collapsed = false;
			public bool active = false;
			public bool breakpoint = false;
			private string _nodePath = "";

			public List<Node> SetupRecursively(List<IAnyGraphNode> parents){
				List<Node> linked = new List<Node>();
				if(representedNode == null){
					return linked;
				}

				List<IAnyGraphNode> nextParents = new List<IAnyGraphNode>();
				nextParents.AddRange (parents);
				nextParents.Add (representedNode);

				for(int i = 0; i < parents.Count; i++){
					_nodePath += parents[i].Name + "/";
				}

				_nodePath += representedNode.Name;

				for(int i = 0; i < representedNode.Links.Count; i++){
					AnyGraphLink link = representedNode.Links[i];

					if(link.connection != null && !nextParents.Contains (link.connection)){
						Node newNode = new Node(){
							representedNode = link.connection,
							isRoot = false,
							guid = System.Guid.NewGuid ().ToString (),
							links = new List<Link>(),
							nodePos = new Rect(),
							parentNode = this,
						};
						links.Add (new Link(){
							linkName = link.linkText,
							guid = newNode.guid
						});
						linked.Add (newNode);
						linked.AddRange (newNode.SetupRecursively (nextParents));
					}
					else{
						links.Add (new Link(){
							linkName = link.linkText
						});
					}

				}

				return linked;
			}

			public void SetActiveRecursively(string[] activePath, int curLevel){
				curLevel++;
				active = true;

				if(curLevel >= activePath.Length){
					return;
				}

				for(int i = 0; i < links.Count; i++){
					Node next = links[i].TargetNode;
					if(next != null && next.representedNode.Name == activePath[curLevel]){
						next.SetActiveRecursively (activePath, curLevel);
						break;
					}
				}
			}

			public void UpdateChildBlocks(float yStep){
				int extentCount = 0;
				heightExtent = 0;

				if(!Collapsed){
					for(int i = 0; i < links.Count; i++){
						if(links[i].TargetNode == null){
							continue;
						}

						links[i].TargetNode.UpdateChildBlocks(yStep);
						heightExtent += links[i].TargetNode.heightExtent;
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
					if(value){
						nodePos.height = 0;
						nodePos.width = 0;
					}
					for(int i = 0; i < links.Count; i++){
						if(links[i].TargetNode != null){
							links[i].TargetNode.Collapsed = value;
						}
					}
				}
			}

			public string NodePath{
				get{return _nodePath;}
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
		
		public static Rect Begin(float zoomScale, Rect screenCoordsArea, float yOffset){
			GUI.EndGroup();        // End the group Unity begins automatically for an EditorWindow to clip out the window tab. This allows us to draw outside of the size of the EditorWindow.
			
			Rect clippedArea = screenCoordsArea.ScaleSizeBy(1.0f / zoomScale, screenCoordsArea.TopLeft());
			clippedArea.y += kEditorWindowTabHeight + yOffset;
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