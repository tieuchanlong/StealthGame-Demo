using System;
using UnityEngine;
using System.Collections;
using StealthGame.Environment.Interactable;
using StealthGame.AI;

public class SearchTrackNode : MonoBehaviour, IStrategicPoint {
   

	Vector2 coordinates;
	SearchTrack track;

    InteractableController _interactableController;
    bool _hasInteractable;

	public NodeType nodeType;
	public enum NodeType {RegularNode, Exit, DisregardableExit, BridgeExitLeft, BridgeExitRight};

	public SearchTrack Track { get{ return track; } set { track = value; } }


	public bool KeepSprite = false;

	public void Init( SearchTrack track){
		
		coordinates = transform.position;
		this.track = track;


	}


	void Awake() {
		coordinates = transform.position;
        _interactableController = GetComponent<InteractableController>();
        _hasInteractable = _interactableController != default(DoorController);
		try
        {
			if ( Application.isPlaying && !KeepSprite )
            {
                Destroy(GetComponent<SpriteRenderer>());
            }
        }
        catch (Exception e) { }
        
    }


    // public IEnumerator Investigate ( GuardController guardController ) {

    //     // only returns instead of yields, so that we can do the yielding
    //     // from the behaviour that is triggering this and get the coroutines
    //     // stored in the behaviour's AI stack!

    //     if( _hasInteractable ) {
    //         return _interactableController.AIActivation(
    //             guardController
    //         );
    //     }

    //     return guardController.GetComponent<PathMovement>().LookAround();
        


    // }

	public Vector3 GetCoordinates() {

        if(  _hasInteractable ) {
            // return _interactableController.GetAIActivatingPosition();
        }

		return this.coordinates;

	}

	public bool IsDisregardable() {
		return nodeType == NodeType.DisregardableExit || nodeType == NodeType.BridgeExitLeft || nodeType == NodeType.BridgeExitRight;
	}

}

