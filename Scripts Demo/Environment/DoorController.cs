using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using StealthGame;
using Zenject;
using StealthGame.Environment.Interactable;
using StealthGame.Inventory;
using StealthGame.Inventory.UI;
using StealthGame.MapLoadingLayer;
using UnityEngine.Tilemaps;
using StealthGame.Input;

[RequireComponent(typeof(BoxCollider2D))]
public class DoorController : ListableObject<DoorController>,
                    IVisibilityStateHolder
{


	[SerializeField]
	protected GameObject _visionBlocker;

    [SerializeField]
    protected Key _requiredKey;


	[SerializeField]
	protected bool _fixObstaclesBehind;

	[SerializeField]
	protected bool _addFloorTilesWhenFixingObstacles;

    protected InventoryUIManager _inventoryUIManager;


	protected SpriteRenderer _spriteRenderer;
	protected Sprite _originalSprite;


	protected MapDataObject _mapDataObject;

	protected InteractableController _interactableController;
    protected InputMovementController _inputMovementController;
    protected PlayerController _playerController;
	protected BoxCollider2D _boxCollider;

	protected MapStartSignal _mapStartSignal;

	protected Vector2 _audibleSoundPosition;

	protected GameManager _gameManager;

	protected bool _open;

	protected Coroutine _automaticClose;

	[SerializeField]
	protected Sprite _openDoorSprite;

	[SerializeField]
	protected bool _blocked = false;

	protected bool _remoteOpeningInProgress = false;

    private bool _inLocker = false;


	private TilemapManager _tilemapManager;
	public bool Open
	{
		get
		{
			return _open;
		}
	}


	public bool FixObstaclesBehind { get => _fixObstaclesBehind; }
	public bool AddFloorTilesWhenFixingObstacles { get => _addFloorTilesWhenFixingObstacles; }


	[Inject]
	public void Construct ( GameManager gameManager,
                            MapStartSignal mapStartSignal,
                            InventoryUIManager inventoryUIManager,
							TilemapManager tilemapManager, 
                            PlayerController playerController)
	{
		_gameManager = gameManager;
		_mapStartSignal = mapStartSignal;
        _inventoryUIManager = inventoryUIManager;
		_tilemapManager = tilemapManager;
        _playerController = playerController;
	}


    public IVisibilityState GetVisibilityState () {
        return new VisibilityStateDoor(_open);
    }

	protected void Awake()
    {

		_interactableController = GetComponentInChildren<InteractableController> ();
        _inputMovementController = _playerController.GetComponentInChildren<InputMovementController>(); ;

        _mapDataObject = FindObjectOfType<MapDataObject> ();

        _audibleSoundPosition = transform.position + new Vector3(0, -1, 0);
            
        _open = false;
        _boxCollider = GetComponent<BoxCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalSprite = _spriteRenderer.sprite;


		_mapStartSignal += OnMapReady;

		SpawnCheckerCollider();

		


    }

	
    private void SpawnCheckerCollider ()
	{

		GameObject go = new GameObject();      
        go.transform.parent = transform;
		DoorFloorChecker doorFloorchecker = go.AddComponent<DoorFloorChecker>();
		doorFloorchecker.Init(this);
		doorFloorchecker.OnEnter += OnBlocked;
		doorFloorchecker.OnLeave += OnUnBlocked;


	}

    private void OnBlocked ()
	{
		Debug.Log("OnBlocked");
		_blocked = true;
	}

    private void OnUnBlocked ()
	{
		Debug.Log("OnUnBlocked");
		_blocked = false;
	}



    protected void OnMapReady ()
	{
		// shouldn't have to check this, but it actually happens...
		if( this != null )
		{
			_gameManager.AddDoor(this);
		}
		
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if( _interactableController != null )
		{
			_interactableController.OnInteraction += OnInteraction;
		}
        
    }

	protected override void OnDisable()
	{
		base.OnDisable ();
		if( _interactableController != null )
		{
        	_interactableController.OnInteraction -= OnInteraction;
		}
    }


	protected void OnInteraction ( MonoBehaviour mb ) {

        
        if( _open ) {
            CloseDoor(mb);
        } else {

            bool shouldUnlock = _requiredKey == null ||
                (_requiredKey != null &&
                _inventoryUIManager.HasItem(_requiredKey as Item) );
            if( shouldUnlock )
            {
                OpenDoor(mb);
            }
            
        }


    }


    protected virtual void CloseDoor(MonoBehaviour mb, bool guard_open = false )
    {
        _spriteRenderer.sprite = _originalSprite;
        if (!guard_open)
            CalloutController.Door.PerformSoundClose(_audibleSoundPosition, mb);
        _open = false;

        //_visionBlocker?.SetActive(true);
		
    }


	protected virtual void OpenDoor ( MonoBehaviour mb, bool guard_open = false )
    {
        _spriteRenderer.sprite = _openDoorSprite;
        if (!guard_open)
            CalloutController.Door.PerformSoundOpen(_audibleSoundPosition, mb);
        _open = true;

        //_visionBlocker?.SetActive(false);

    }

    /// <summary>
    /// Public mehod for Open door for guard to access
    /// </summary>
    /// <param name="mb"></param>
    public void OpenLocker(MonoBehaviour mb)
    {
        OpenDoor(mb, true);

        _open = true;
        ControlPlayerSprite(true);
    }

    /// <summary>
    /// Close the locker when the player is not inside the locker
    /// </summary>
    /// <param name="mb"></param>
    public void CloseLocker(MonoBehaviour mb)
    {
        if (!_playerController.Recognized)
            CloseDoor(mb, true);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.name == "Ali(Clone)" || collision.gameObject.name == "AliBody")
        {
            ControlPlayerSprite(true);
            _playerController.InLocker = true;
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.gameObject.name == "Ali(Clone)" || collision.gameObject.name == "AliBody")
        {
            if (_inLocker)
            {
                //_interactableController.OnInteraction += OnInteraction;
                _inputMovementController.LockMovement = !_open;

                for (int i = 0; i < _playerController.gameObject.transform.childCount; i++)
                    if (_playerController.gameObject.transform.GetChild(i).name == "AliBody")
                    {
                        Transform AliBody = _playerController.gameObject.transform.GetChild(i);
                        AliBody.gameObject.GetComponent<VisibleObject>().enabled = _open || _playerController.Recognized;
                    }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.name == "Ali(Clone)" || collision.gameObject.name == "AliBody")
        {
            ControlPlayerSprite(false);
            _playerController.InLocker = false;
        }
    }

    /// <summary>
    /// Control the player sprite considering when the player is in or outside the locker
    /// </summary>
    private void ControlPlayerSprite(bool hide)
    {
        // Make the Player sprite disappear
        for (int i = 0; i < _playerController.gameObject.transform.childCount; i++)
            if (_playerController.gameObject.transform.GetChild(i).name == "AliBody")
            {
                Transform AliBody = _playerController.gameObject.transform.GetChild(i);
                if (!_open && AliBody.gameObject.GetComponent<VisibleObject>().enabled == true)
                    return;
                AliBody.gameObject.GetComponent<VisibleObject>().enabled = true;
                for (int j = 0; j < AliBody.childCount; j++)
                    if (AliBody.GetChild(j).name != "CarryingDeadGuard")
                        AliBody.GetChild(j).GetComponent<SpriteRenderer>().enabled = !hide;
                break;
            }

        _inLocker = hide;
    }

    protected virtual IEnumerator RemoteOpenDoorRoutine ()
	{
		yield return null;
	}

    // exists for later use. should use a coroutine to bleep or something.
	public virtual void RemoteOpenDoor ( MonoBehaviour monoBehaviour )
	{
		if( _remoteOpeningInProgress )
		{
			return;
		}

		if( !_open )
		{
			OpenDoor(monoBehaviour);
		}

		if( _automaticClose != null )
		{

			StopCoroutine(_automaticClose);
            
		}

		_automaticClose = StartCoroutine(CloseDoorInXSeconds(4f, monoBehaviour));

		// todo: add logic to start timer for auto close.


	}

	protected IEnumerator CloseDoorInXSeconds ( float seconds, MonoBehaviour monoBehaviour )
	{


		yield return new WaitForSeconds(seconds);
		while(_blocked)
		{
			yield return null;
		}
		CloseDoor(monoBehaviour);


	}


}
