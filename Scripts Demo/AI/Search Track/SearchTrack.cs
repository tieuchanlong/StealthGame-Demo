using System;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine;
using StealthGame.RoomClassification;
using StealthGame.AI;

public class SearchTrack : MonoBehaviour {

	/************** CONFIG ************************/

    [SerializeField]
	private int amountOfGuardsForSearching = 1;

	[SerializeField]
	private bool requriesCover = false;

	/****************************************/




	/************** OTHER COMPONENTS ************************/

	private TextMesh _textMesh;

	private Room _room;
	/****************************************/



	/************** FUNCTIONAL VARIABLES ************************/
	private List<IStrategicPoint> _exits;
	private List<SearchTrackNode> _bridgeExitsNegative;
	private List<SearchTrackNode> _bridgeExitsPositive;
	private List<SearchTrackNode> _searchTrackNodes;
	private string _name;


	/****************************************/




	/************** STATIC ************************/
	private static List<SearchTrack> searchTracks;
	private static bool searchTracksInstantiated;

	private static List<SearchTrackNode> allSearchTrackNodes;
	private static bool allSearchTrackNodesInstantiated;

	public int AmountOfGuardsForSearching
	{
		get
		{
			return amountOfGuardsForSearching;
		}
	}

	public bool RequriesCover
	{
		get
		{
			return requriesCover;
		}
	}

	public List<IStrategicPoint> Exits
	{
		get
		{
			return _exits;
		}
	}

	public Room Room
	{
		get
		{
			return _room;
		}
	}

	/****************************************/


	void Awake() {
		
		_room = GetComponent<Room> ();

		_textMesh = GetComponent<TextMesh>();
		if( Application.isPlaying )
		{
			Destroy(_textMesh);
		}
		else
		{
			
            SetTextMeshName();
            _name = name;
		}

		_exits = new List<IStrategicPoint> ();
		_searchTrackNodes = new List<SearchTrackNode> ();

		// _geoArea = GetComponent<GeoArea>();

		ConfigureChildren ();
		RegisterSearchTrack (this);



	}


    private void EditorUpdate ()
	{

		if (_name != name)
        {
            _name = name;
            SetTextMeshName();
        }

	}

	private void Update()
	{      
		if(  !Application.isPlaying )
		{
			EditorUpdate();
		}      
	}



	void SetTextMeshName ()
	{
		if( _textMesh == default(TextMesh ) )
		{
			_textMesh = GetComponent<TextMesh>();
		}

		_textMesh.text = name;

	}
    
	void ConfigureChildren() {

		SearchTrackNode[] searchTrackNodes = gameObject.GetComponentsInChildren<SearchTrackNode> ();

		_bridgeExitsNegative = new List<SearchTrackNode> ();
		_bridgeExitsPositive = new List<SearchTrackNode> ();

		//_exits = new List<SearchTrackNode> ();

		for (int i = 0; i < searchTrackNodes.Length; i++) {

			SearchTrackNode searchTrackNode = searchTrackNodes [i];

			switch (searchTrackNode.nodeType) {

				case (SearchTrackNode.NodeType.Exit):
				case (SearchTrackNode.NodeType.DisregardableExit):
					//_exits.Add (searchTrackNode);
					break;
				case (SearchTrackNode.NodeType.BridgeExitLeft):
					_bridgeExitsNegative.Add (searchTrackNode);
					break;
				case (SearchTrackNode.NodeType.BridgeExitRight):
					_bridgeExitsPositive.Add (searchTrackNode);
					break;
				default:
					this._searchTrackNodes.Add (searchTrackNode);
					break;

			}

			searchTrackNode.Track = this;

		}

	}

	public static void RegisterSearchTrack( SearchTrack searchTrack ) {

		if (!searchTracksInstantiated) {

			searchTracks = new List<SearchTrack> ();
			searchTracksInstantiated = true;

		}

		searchTracks.Add (searchTrack);

	}

	public static List<SearchTrack> GetSearchTracks() {

		return searchTracks;

	}

	public static List<SearchTrackNode> GetAllSearchTrackNodes(){

		if (allSearchTrackNodesInstantiated) {
			return allSearchTrackNodes;
		}

		allSearchTrackNodes = new List<SearchTrackNode> ();

		List<SearchTrack> searchTracks = GetSearchTracks ();

		for (int i = 0; i < searchTracks.Count; i++) {

			SearchTrack searchTrack = searchTracks [i];
			List<SearchTrackNode> searchTrackNodes = searchTrack.GetSearchTrackNodes ();
			allSearchTrackNodes.AddRange (searchTrackNodes);

		}

		return allSearchTrackNodes;
      
	}

	public List<SearchTrackNode> GetSearchTrackNodes () {

		return _searchTrackNodes.Copy();

	}



	public List<IStrategicPoint> GetExits() {
		return this._exits;
	}

	// TODO: Perhaps separate this method?
	public static List<IStrategicPoint> GetAllExits() {
		
		List<IStrategicPoint> allExits = new List<IStrategicPoint> ();
		for (int i = 0; i < searchTracks.Count; i++) {
			SearchTrack searchTrack = searchTracks [i];
			allExits.AddRange (searchTrack.Exits);

		} 

		return allExits;
	}

	public List<SearchTrackNode> GetBridgeExitsPositive() { return this._bridgeExitsPositive; }
	public List<SearchTrackNode> GetBridgeExitsNegative() { return this._bridgeExitsNegative; }


	[MenuItem("Benji's Tools/Hide SearchTrack Meshes")]
    private static void HideCoverAreaMeshes()
    {


        SearchTrack[] areas = FindObjectsOfType<SearchTrack>();

        foreach (SearchTrack area in areas)
        {
            area.GetComponent<MeshRenderer>().enabled = false;
        }

    }
	[MenuItem("Benji's Tools/Show SearchTrack Meshes")]
    private static void ShowCoverAreaMeshes()
    {

        SearchTrack[] areas = FindObjectsOfType<SearchTrack>();

        foreach (SearchTrack area in areas)
        {
            area.GetComponent<MeshRenderer>().enabled = true;
        }

    }


}




