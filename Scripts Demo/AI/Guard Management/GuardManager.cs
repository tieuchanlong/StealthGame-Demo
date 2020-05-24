using Zenject;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using StealthGame.MapLoadingLayer;
using StealthGame.Actors.Movement;
using StealthGame.SecurityAndAlarms;
using System.Linq;
using StealthGame.RoomClassification;
using StealthGame.Util;

namespace StealthGame.AI
{
    public class GuardManager : Initializable<PatrolRoutesPreparedSignal>
    {

        private enum ChaseState { REPORTING_PLAYER, SEARCHING_FIRST_ROOM, SEARCHING_FOR_PLAYER, CHASING_PLAYER, DONE }

        #region GENERAL_FUNCTIONALITY_DECLARATIONS
        

        private int _searchCount;

        [SerializeField]
        private bool _playerChangedRoomAfterGettingSpotted;

        [SerializeField]
        private Room _currentRoom;


        [SerializeField]
        private ChaseState _chaseState;

        private bool _firstGuardHasReachedSearchedRoom;
        PathManager _pathManager;
        private GuardStateBrainPBT _lastGuardToSeePlayer;
        private List<GuardStateBrainPBT> _extraGuards;
        
        private PlayerController _player;

        private GeometryUtilities _geometryUtilities;


        private Dictionary<PatrolRouteEntity, GuardStateBrainPBT>
                                                        _guardsByPatrolRoute;

        private ExitPoint _guardReplacementExit;


        private List<GuardStateBrainPBT> _guardsSeeingPlayer;
        
        private List<GuardStateBrainPBT> _guardsNotSeeingPlayer;

        private PatrolRouteManager _patrolRouteManager;

        private GuardFactory _guardFactory;
        private SearchTrackManager.Factory _searchTrackManagerFactory;

        private LevelGameplayManager _levelGameplayManager;

        private LevelLoadingManager _levelLoadingManager;

        private CollectiveGuardState _collectiveGuardState;

        private MapHelper _mapHelper;
        
        private DeadGuardController.Factory _deadGuardFactory;
        private SquadManager.Factory _squadManagerFactory;

        private SquadManager _squadManager;

        private Vector2 _playerLastKnownPosition;

        private SearchTrackManager _searchTrackManager;

        #endregion

        #region MICRO_MANAGEMENT_DECLARATIONS
        private Dictionary<int, List<GuardSpecificBehaviour>>
                                                      _guardsShootingPlayer;

        #endregion

        #region GUARD_SEARCH_MANAGEMENT_DECLARATIONS

        private List<GuardStateBrainPBT> _availableGuards;

        private bool _initialScanPerformed = false;
        private List<Room> _roomsToSearch;

        [SerializeField]
        private bool _playerPositionIsKnown;

        private bool finishedReinforceGuards = true;

        private Room _playerLastKnownRoom;
        private Room _playerRoomLastFrame;

        private List<Room> _playerAssociatedRooms;


        private bool _enemyPresenceAwareness;


        private List<RoomConnection> _blockedConnections;
        private List<RoomConnection> _likelyEscapeRoutes;
        private List<ExitPoint> _exits;
        private List<MapEntryPoint> _mapEntryPoints;

        private int _serializedDirWhenLostPlayer = -1;

        #endregion

        #region GETTERS

        public CollectiveGuardState CollectiveGuardState { get => _collectiveGuardState; }

        public bool PlayerPositionIsKnown
        {
            get
            {
                return _playerPositionIsKnown;
            }
        }

        public bool EnemyPresenceAwareness
        {
            get
            {
                return _enemyPresenceAwareness;
            }
        }

        

        public bool PlayerIsVisible
        {
            get
            {
                return _guardsSeeingPlayer.Count > 0;
            }
        }

        public int AmountOfGuardsWithVision
        {
            get
            {
                return _guardsSeeingPlayer.Count;
            }
        }

        public CollectiveGuardState GuardState
        {
            get
            {
                return _collectiveGuardState;
            }
        }

        public bool TreatAlarmsSeriously
        {
            get
            {
                return _reactedAlarms >
                    _levelGameplayManager
                        .MapDataObject
                        .AmountOfAlarmsBeforeInvestigation;
            }
        }

        public int AmountOfGuardsSeeingPlayer
        {
            get
            {
                return _guardsSeeingPlayer.Count;
            }
        }

        #endregion
        

        #region ALARM_MANAGEMENT_DECLARATIONS

        private int _reactedAlarms;

        private List<IAlarm> _alarms;

        #endregion


        #region SIGNALS_DECLARATION

        private GuardReachedRoomSignal _guardReachedRoomSignal;

        private PlayerTouchExitSignal _playerTouchExitSignal;
        private GuardDeathSignal _guardDeathSignal;

        private GuardStatusChangeSignal _guardStatusChangeSignal;

        private GuardReportEnemyPresenceSignal _guardReportEnemyPresenceSignal;

        private SearchTrackSearchedSignal _searchTrackSearchedSignal;

        private AIPathFindCompleteSignal _aiPathFindCompleteSignal;

        private PlayerChangeRoomSignal _playerChangeRoomSignal;

        private GuardSpotTargetSignal _guardSpotTargetSignal;

        private GuardLoseTargetVisionSignal _guardLoseTargetVisionSignal;

        private GuardReportDeadGuardSignal _guardReportDeadGuardSignal;


        #endregion


        #region INITIALIZATION_AND_STARTUP
        

        [Inject]
        public void Construct(PatrolRouteManager patrolRouteManager,
                             GuardFactory guardFactory,
                             MapHelper mapHelper,
                             SearchTrackManager.Factory
                                    searchTrackManagerFactory,
                             TilemapManager tilemapManager,
                            GuardDeathSignal guardDeathSignal,
                            DeadGuardController.Factory deadGuardFactory,
                            GuardStatusChangeSignal guardStatusChangeSignal,
                            SquadManager.Factory squadManagerFactory,
                            LevelGameplayManager levelGameplayManager,
                            LevelLoadingManager levelLoadingManager,
                            GeometryUtilities geometryUtilities,
                            GuardReportEnemyPresenceSignal guardReportEnemyPresenceSignal,
                            SearchTrackSearchedSignal searchTrackSearchedSignal,
                            AIPathFindCompleteSignal aIPathFindCompleteSignal,
                            PlayerController player,
                            PlayerChangeRoomSignal playerChangeRoomSignal,
                            GuardSpotTargetSignal guardSpotTargetSignal,
                            GuardLoseTargetVisionSignal guardLoseTargetVisionSignal,
                            GuardReportDeadGuardSignal guardReportDeadGuardSignal,
                            PlayerTouchExitSignal playerTouchExitSignal,
                            PathManager pathManager,
                            GuardReachedRoomSignal guardReachedRoomSignal


                            )
        {
            _geometryUtilities = geometryUtilities;

            _levelLoadingManager = levelLoadingManager;



            _squadManagerFactory = squadManagerFactory;

            _guardStatusChangeSignal = guardStatusChangeSignal;

            _mapHelper = mapHelper;
            _patrolRouteManager = patrolRouteManager;

            _guardFactory = guardFactory;

            _guardsNotSeeingPlayer = new List<GuardStateBrainPBT>();
            _guardsSeeingPlayer = new List<GuardStateBrainPBT>();
            _extraGuards = new List<GuardStateBrainPBT>();


            _searchTrackManagerFactory = searchTrackManagerFactory;

            _guardDeathSignal = guardDeathSignal;

            _guardDeathSignal += OnGuardDeath;

            _deadGuardFactory = deadGuardFactory;

            _levelGameplayManager = levelGameplayManager;

            _guardReportEnemyPresenceSignal = guardReportEnemyPresenceSignal;

            _searchTrackSearchedSignal = searchTrackSearchedSignal;

            _aiPathFindCompleteSignal = aIPathFindCompleteSignal;

            _player = player;

            _playerChangeRoomSignal = playerChangeRoomSignal;

            _guardSpotTargetSignal = guardSpotTargetSignal;
            _guardLoseTargetVisionSignal = guardLoseTargetVisionSignal;

            _guardReportDeadGuardSignal = guardReportDeadGuardSignal;

            _playerTouchExitSignal = playerTouchExitSignal;

            _pathManager = pathManager;

            _guardReachedRoomSignal = guardReachedRoomSignal;


        }



        protected override void Awake()
        {
            _chaseState = ChaseState.DONE;
            _playerAssociatedRooms = new List<Room>();
            _availableGuards = new List<GuardStateBrainPBT>();
            base.Awake();
            _playerPositionIsKnown = false;

        }

        private void LateUpdate ()
        {
            _playerRoomLastFrame = _playerLastKnownRoom;
            CheckExtraGuardsState();
        }

        private void OnEnable()
        {
            _guardReportEnemyPresenceSignal += OnGuardReportEnemyPresence;
            _searchTrackSearchedSignal += OnSearchTrackSearched;
            _aiPathFindCompleteSignal += OnAIPathFindComplete;
            _playerChangeRoomSignal += OnPlayerChangeRoom;
            _guardSpotTargetSignal += OnGuardSpotPlayer;
            _guardLoseTargetVisionSignal += OnGuardLoseTrackOfPlayer;
            _guardReportDeadGuardSignal += OnGuardReportDeadGuard;

            _playerTouchExitSignal += OnPlayerTouchExit;

            _guardReachedRoomSignal += OnGuardReachedRoom;
            
        }

        private void OnDisable()
        {
            _playerTouchExitSignal -= OnPlayerTouchExit;

            _guardReportEnemyPresenceSignal -= OnGuardReportEnemyPresence;
            _searchTrackSearchedSignal -= OnSearchTrackSearched;
            _aiPathFindCompleteSignal -= OnAIPathFindComplete;
            _playerChangeRoomSignal -= OnPlayerChangeRoom;
            _guardSpotTargetSignal -= OnGuardSpotPlayer;
            _guardLoseTargetVisionSignal -= OnGuardLoseTrackOfPlayer;
            _guardReportDeadGuardSignal -= OnGuardReportDeadGuard;
            _guardReachedRoomSignal -= OnGuardReachedRoom;
        }

        /// <summary>
        /// Called automatically when patrol routes are done computing
        /// as per the Initializable implementation
        /// </summary>
        protected override void Initialize()
        {

            
            
            _guardsByPatrolRoute =
                    new Dictionary<PatrolRouteEntity, GuardStateBrainPBT>();

            _blockedConnections = new List<RoomConnection>();
            _enemyPresenceAwareness = false;
            SetupAlarmListeners();
            _guardsSeeingPlayer = new List<GuardStateBrainPBT>();
            _guardsNotSeeingPlayer = new List<GuardStateBrainPBT>();

            InitializeGuardReplacementExit ();
            AddPatrollingGuards ();
            _guardsNotSeeingPlayer.AddRange(_guardFactory.AllGuards);
            InitGuardsShootingPlayer();

            // Add exit points
            _exits = new List<ExitPoint>();
            _exits.AddRange(FindObjectsOfType<ExitPoint>());

            _mapEntryPoints = new List<MapEntryPoint>();
            _mapEntryPoints.AddRange(FindObjectsOfType<MapEntryPoint>());
        }


        /// <summary>
        /// Triggered by the playertouchexitsignal, resets guard status to patrolling
        /// </summary>
        private void OnPlayerTouchExit ( LevelDestination ld )
        {
            if(_collectiveGuardState != AI.CollectiveGuardState.PATROLLING )
            {
                _enemyPresenceAwareness = false;
                ReturnToNormal( true );
            }
        }


        /// <summary>
        /// Finds the first ExitPoint that is set to be a GuardReplacementSpawner
        /// and stores it for later use
        /// </summary>
        protected void InitializeGuardReplacementExit ()
        {
            _guardReplacementExit = ExitPoint.GetInstances()
                .FilterFirst((ExitPoint exitPoint) =>
                {

                    return exitPoint.GuardReinfocementSpawner;

                });
        }

        /// <summary>
        /// Adds patrolling guards by going thorugh each patrol route in the patrol route manager
       /// and using the GuardFactory to create one for each route.
        /// </summary>
        protected void AddPatrollingGuards ()
        {
            foreach (var patrolRoute in _patrolRouteManager.PatrolRouteEntities)
            {
                if (patrolRoute.CreateGuardOnMapStart)
                {
                    GuardStateBrainPBT guard =
                                            _guardFactory.Create(patrolRoute, patrolRoute.GetPositionAtIndex(0));
                    _guardsByPatrolRoute.Add(patrolRoute, guard);
                }
            }
        }


        /// <summary>
        /// Add 3 guards spawning from 1 exit to cover 3 security route during alert mode
        /// </summary>
        IEnumerator AddGuardAtExit()
        {
            // Create some delay time for a guard to check to respawn for dead guard
            //yield return new WaitForSeconds(10);

            finishedReinforceGuards = false;
            // Add replacement for dead guards
            foreach (var patrolRoute in _patrolRouteManager.PatrolRouteEntities)
            {
                if (patrolRoute.CreateGuardOnMapStart && _guardsByPatrolRoute[patrolRoute] == null)
                {
                    GuardStateBrainPBT guard =
                                            _guardFactory.Create(patrolRoute, patrolRoute.GetPositionAtIndex(0));
                    _guardsByPatrolRoute.Remove(patrolRoute);
                    _guardsByPatrolRoute.Add(patrolRoute, guard);

                    yield return new WaitForSeconds(2);
                }
            }

            // Add extra 3 guards
            foreach (var patrolRoute in _patrolRouteManager.PatrolRouteEntities)
            {
                if (_extraGuards.Count == 3)
                    break;

                if (patrolRoute.SecurityRoute)
                {
                    yield return new WaitForSeconds(1);
                    GuardStateBrainPBT guard =
                                    _guardFactory.Create(patrolRoute, _mapEntryPoints[0].transform.position);

                    guard.gameObject.GetComponentInChildren<GuardSpecificBehaviour>().MapEntryPoint = _guardReplacementExit;
                    guard.gameObject.GetComponentInChildren<GuardSpecificBehaviour>().TempGuard = true;
                    _extraGuards.Add(guard);
                    _searchTrackManager.AddGuard(guard, false); // Add reinforced guards to the search track to join the search
                    _guardsByPatrolRoute.Add(patrolRoute, guard);

                    yield return new WaitForSeconds(2);
                }
            }

            // If already added but the 3 guards are disabled because of previous alert mode, enable them and reset their patrol time
            foreach (GuardStateBrainPBT guard in _extraGuards)
            {
                guard.gameObject.transform.parent.gameObject.SetActive(true);
                guard.gameObject.GetComponentInChildren<GuardSpecificBehaviour>().PatrolTime = 0;

                yield return new WaitForSeconds(2);
            }

            finishedReinforceGuards = true;
        }

        /// <summary>
        /// Check the extra guards if already reached the exit 
        /// then disable them for future or check if guards already switched back to normal patrol mode to increase patrol time
        /// </summary>
        public void CheckExtraGuardsState()
        {
            foreach (GuardStateBrainPBT guard in _extraGuards)
            {
                // If the distance between the guard and exit point less than 1, disable them instead of destroying to prevent recreating guards

                if (guard.gameObject.GetComponentInChildren<GuardSpecificBehaviour>().PatrolFinished 
                    && Vector2.Distance(guard.transform.position, _guardReplacementExit.transform.position) <= 1f)
                {
                    guard.gameObject.GetComponentInChildren<GuardSpecificBehaviour>().PatrolTime = 0; // Reset patrol time
                    guard.gameObject.transform.parent.gameObject.SetActive(false);
                    continue;
                }

                // In case of null because of the reinforced guard getting killed, remove it
                if (guard == null)
                {
                    _extraGuards.Remove(guard);
                    continue;
                }

                // Increase the Patrol Time count when the game is back to normal patrol mode, this is specically used for Reinforced guards
                if (!_playerPositionIsKnown && 
                    guard.gameObject.GetComponentInChildren<GuardSpecificBehaviour>().HasSearchTrack 
                    && guard.gameObject.GetComponentInChildren<GuardSpecificBehaviour>().HasRoom)
                    guard.gameObject.GetComponentInChildren<GuardSpecificBehaviour>().PatrolTime += Time.deltaTime;
            }
        }

        private void SetupAlarmListeners()
        {

            _alarms = new List<IAlarm>();
            _reactedAlarms = 0;

            var alarms =
                Object.FindObjectsOfType<MonoBehaviour>().OfType<IAlarm>();

            foreach (IAlarm alarm in alarms)
            {
                _alarms.Add(alarm);
                alarm.OnAlarmStart += ReactToAlarm;

            }

        }


        #endregion
        

        #region EVENT_HANDLERS
        


        /// <summary>
        /// Triggers when the player changes room and stores the new room in case a guard sees him
        /// </summary>
        /// <param name="room"></param>
        private void OnPlayerChangeRoom(Room room)
        {
            
            if(
                _chaseState == ChaseState.REPORTING_PLAYER ||
                _chaseState == ChaseState.SEARCHING_FOR_PLAYER &&
                !_playerChangedRoomAfterGettingSpotted
            )
            {

                StartCoroutine( WaitBeforeGettingEscapeRoutes (room) );
                _playerLastKnownRoom = room;
                _playerChangedRoomAfterGettingSpotted = true;

            }
            else
            {
                if (_guardsSeeingPlayer.Count == 0)
                {
                    // _playerLastKnownRoom = room;
                    return;
                }
                foreach( var guard in _guardFactory.AllGuards )
                {
                    if( guard.VisionCone.ObjectIsInVisionCone(_player.VisibleObject) )
                    {
                        _playerLastKnownRoom = room;
                        break;
                    }
                }
            }
            
        
        }

        /// <summary>
        /// Triggers when a guard's pathfinding is complete.
        /// When triggered during alert mode:
        /// Goes through all RoomConnections to see if the guard's path
        /// passes through them.
        /// and marks them as blocked for a later room search based on some conditions.
        /// 
        /// Ignores any connections where one of the rooms contains the player and one of the rooms
        /// is a connection.
        /// </summary>
        /// <param name="pathEntity"></param>
        private void OnAIPathFindComplete ( GuardStateBrainPBT guard, PathEntity pathEntity )
        {
            // pathEntity.PaintPath ();
            if(_collectiveGuardState == AI.CollectiveGuardState.ATTACKING )
            {
                List<RoomConnectorPathDepth> touchedRoomConnectors = GetTouchedRoomConnectors( pathEntity );
                if( guard == _lastGuardToSeePlayer)
                {
                    // _blockedConnections.AddRange( touchedRoomConnectors );
                    List<RoomConnection> connections = touchedRoomConnectors.Map( (RoomConnectorPathDepth rcpd) =>
                        {
                            return rcpd.RoomConnection;
                        }
                    );
                    if( _searchCount > 1 )
                    {
                        // Debug.Break();
                        _blockedConnections.AddRange( connections );
                    }
                    
                }
                else
                {
                    if( _blockedConnections.Count > 0 && touchedRoomConnectors.Count > 0 )
                    {
                        _blockedConnections.Add( touchedRoomConnectors[0].RoomConnection );
                        for( int i = 1; i < touchedRoomConnectors.Count; i++ )
                        {
                            RoomConnectorPathDepth rcpd = touchedRoomConnectors[i];
                            if( rcpd.Depth < 20 )       
                            {
                                _blockedConnections.Add( rcpd.RoomConnection );
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    
                }
            }
        }


        private List<RoomConnectorPathDepth> GetTouchedRoomConnectors ( PathEntity pathEntity )
        {

            List<RoomConnectorPathDepth> touchedRoomConnectors = new List<RoomConnectorPathDepth> ();
            List<Vector2> nodes = pathEntity.GetNodes();
            for( int i = 0; i < nodes.Count; i++ )
            {
                
                var node = nodes[i];

                foreach( var connRep in RoomConnectionRepresentation.Instances )
                {
                    float minX = connRep.RoomConnection.coveredTiles[0].x;
                    float maxY = -1 * connRep.RoomConnection.coveredTiles[0].y;
                    float maxX = connRep.RoomConnection.coveredTiles.LastElement().x;
                    float minY = -1 * connRep.RoomConnection.coveredTiles.LastElement().y;

                    if (
                        minX <= node.x &&
                        minY <= node.y &&
                        node.x <= maxX  &&
                        node.y <= maxY
                    )
                    {
                        GenericUtilities.SafelyAddItemToList(
                            new RoomConnectorPathDepth( connRep.RoomConnection, i ),
                            touchedRoomConnectors
                        );
                    }
                }

            }


            return touchedRoomConnectors;

        }

        /// <summary>
        /// Triggers when a guard has finished his request for reinforcements.
        /// Starts a search from the room where the player was found.
        /// </summary>
        /// <param name="playerRoom"></param>
        /// <param name="guardRoom"></param>
        /// <param name="coverPoint"></param>
        /// <param name="guard"></param>
        private void OnGuardReportEnemyPresence(Room playerRoom,
                Room guardRoom, CoverPoint coverPoint, GuardStateBrainPBT guard)
        {

            _playerAssociatedRooms.Clear();
            _playerAssociatedRooms.Add(playerRoom);

            if ( _collectiveGuardState == AI.CollectiveGuardState.ATTACKING )
            {
                _blockedConnections.Clear();
                _playerPositionIsKnown = true;
                _playerLastKnownRoom = _levelLoadingManager
                                        .RoomGraphManager
                                        .GetRoomContainingObject(_player);
                _chaseState = ChaseState.CHASING_PLAYER;
            }
            else if ( _collectiveGuardState == AI.CollectiveGuardState.PATROLLING )
            {

                _enemyPresenceAwareness = true;
                

                _searchCount = 0;
                InitSearchFromRoom( playerRoom, guardRoom, guard, true );

                SetGuardState(AI.CollectiveGuardState.ATTACKING);
            }


        }

        private void OnGuardReportDeadGuard ( MonoBehaviour guard )
        {
            
            _enemyPresenceAwareness = true;
            

            Room room = _levelLoadingManager
                            .RoomGraphManager
                            .GetRoomContainingObject(guard);
            _playerAssociatedRooms.Add(room);
            GuardStateBrainPBT g = ((GuardSpecificBehaviour)guard).StateBrain as GuardStateBrainPBT;
            InitSearchFromRoom( room, room, g, false );
            SetGuardState(AI.CollectiveGuardState.ATTACKING);
        }

        /// <summary>
        /// Triggers when an alarm system on the level has triggered
        /// and determines if a guard should investigate it or shrug it off
        /// as rats or something.
        /// </summary>
        /// <param name="alarm"></param>
        /// <param name="triggeringGameObject"></param>
        private void ReactToAlarm(IAlarm alarm,
                                   GameObject triggeringGameObject)
        {

            if (_collectiveGuardState == AI.CollectiveGuardState.ATTACKING)
            {

                return;
            }

            _reactedAlarms++;

            GuardStateBrainPBT investigatingGuard =
                _geometryUtilities.GetClosestToPoint(_guardFactory.AllGuards,
                               ((MonoBehaviour)alarm).transform.position);

            if (TreatAlarmsSeriously)
            {
                // old implementation reliant on searchmode behaviour
                // needs to do this in a different way
                // investigatingGuard.GetComponent<SearchModeBehaviour>()
                //       .SearchTrack = alarm.SearchTrack;

            }

            investigatingGuard.InitAlarmInvestigation(alarm);

        }

        #endregion


        #region GUARD_SEARCH_MANAGEMENT

        /// <summary>
        /// Uses a SearchRoomFinder to traverse the RoomGraph from a room to find appropriate ones to visit.
        /// Use this after searching a room to find which rooms should be searched afterwards
        /// </summary>
        /// <param name="startRoom">The room to scan from</param>
        private List<Room> ScanRooms ( Room startRoom ) 
        {
            // IF THIS IS UNCOMMENTED IT MUST BE FIXED
            // IT BREAKS THE LOGIC IN THE SITUATION THAT THE PLAYER IS NO LONGER IN THAT ROOM
            // AND NONE OF ITS CONNECTIONS ARE IN THE POTENTIAL ESCAPE ROUTES
            // REMOVING ANYWAYS SINCE WE ARE PROBABLY NOT GOING TO BE USING THIS
            // if( _playerChangedRoomAfterGettingSpotted )
            // {
            //     _blockedConnections.Clear ();
            //     foreach( var connection in _playerLastKnownRoom.allConnections )
            //     {
            //         if( !_likelyEscapeRoutes.Contains(connection) )
            //         {
            //             _blockedConnections.Add(connection);
            //         }
            //     }
                
            // }

            return new SearchRoomFinder(
                startRoom,
                _blockedConnections,
                _playerLastKnownPosition
            ).FindRooms();
        }

        /// <summary>
        /// Runs whenever a searchtrack is searched and marks the track's room as searched.
        /// If this is the first time a track is searched after the player's presence was reported,
        /// a list of rooms is extracted by a SearchRoomFinder.
        /// As long as there are more rooms to search, a new search is initiated.
        /// When no more rooms are available, a the guard state is set back to patrolling.
        /// </summary>
        /// <param name="searchTrack"></param>
        private void OnSearchTrackSearched(SearchTrack searchTrack)
        {
            if( _searchCount == 1 )
            {
                
                // first search
                Room playerRoom = _levelLoadingManager
                                .RoomGraphManager
                                .GetRoomContainingObject(_player);
                InitSearchFromRoom( playerRoom, playerRoom, _lastGuardToSeePlayer, false, true );
            }
            else
            {
                if ( !_initialScanPerformed )
                {
                    _roomsToSearch = ScanRooms( searchTrack.Room );
                    _initialScanPerformed = true;
                }
                if ( _roomsToSearch.Count == 0 )
                {
                    _initialScanPerformed = false;
                    ReturnToNormal ();
                }
                else
                {
                    StartCoroutine(SearchNextFrame());
                }

            }

        }
        
        /// <summary>
        /// Waits a frame and then initiates a new search. This is to make sure the BehaviourTrees
        /// get 1 frame without a room.
        /// </summary>
        /// <returns></returns>dded
        private IEnumerator SearchNextFrame()
        {
            yield return null;
            _currentRoom = _roomsToSearch.PopFirst();
            SearchTrack searchTrackToSearch = _currentRoom.SearchTrack;

            SearchTrackManager searchTrackManager =
                _searchTrackManagerFactory.Create(searchTrackToSearch, _guardFactory.AllGuards, null);
        }

        
        /// <summary>
        /// Keeps track of which guards can see the player
        /// </summary>
        /// <param name="guard"></param>
        /// <param name="target"></param>
        private void OnGuardSpotPlayer(
            GuardStateBrainPBT guard, MonoBehaviour target
        )
        {
            if( _searchInitializationCoroutine != null )
            {
                StopCoroutine( _searchInitializationCoroutine );
            }
            
            _playerChangedRoomAfterGettingSpotted = false;
            if( _chaseState == ChaseState.DONE )
            {
                _chaseState = ChaseState.REPORTING_PLAYER;
            }
            GenericUtilities.SafelyAddItemToList(guard, _guardsSeeingPlayer);
            _guardsNotSeeingPlayer.Remove(guard);
            _playerLastKnownPosition = target.transform.position;
            _playerLastKnownRoom = _levelLoadingManager
                                        .RoomGraphManager
                                        .GetRoomContainingObject(_player);

        }


        /// <summary>
        /// Keeps track of which guards can see the player.
        /// If this is the last guard to lose sight of the player,
        /// initialize a search from the room where the player was lost
        /// </summary>
        /// <param name="guard"></param>
        /// <param name="target"></param>
        private void OnGuardLoseTrackOfPlayer(GuardStateBrainPBT guard,
                                            MonoBehaviour target)
        {
            GenericUtilities.SafelyAddItemToList(guard, _guardsNotSeeingPlayer);
            _guardsSeeingPlayer.Remove(guard);



            if (_guardsSeeingPlayer.Count == 0 )
            {
                // Debug.Break();
                // we have lost him in the midst of a chase
                _lastGuardToSeePlayer = guard;

                if( _collectiveGuardState == AI.CollectiveGuardState.ATTACKING )
                {
                    SearchInitialization ();
                }

            }

        }

        private void OnGuardReachedRoom ( GuardStateBrainPBT guard, Room room, RoomConnection roomConnection )
        {
            

            // todo: handle edge case when guard enters the player's room as part of chasing instead.
            if( _chaseState == ChaseState.SEARCHING_FOR_PLAYER && room == _currentRoom )
            {

                if( !_firstGuardHasReachedSearchedRoom )
                {
                    
                    _lastGuardToSeePlayer = guard;
                    _firstGuardHasReachedSearchedRoom = true;

                }


            }

        }


        private void SearchInitialization ()
        {
            
            if( _searchInitializationCoroutine != null )
            {
                StopCoroutine( _searchInitializationCoroutine );
            }

            StartCoroutine( SearchInitializationProcess() );

        }

        private Coroutine _searchInitializationCoroutine;
        private float _searchStartDelay = 10f;
        private IEnumerator SearchInitializationProcess ()
        {
            
            yield return new WaitForSeconds(_searchStartDelay);
            Room playerRoom = _levelLoadingManager
                                .RoomGraphManager
                                .GetRoomContainingObject(_player);
            Room guardRoom = _levelLoadingManager
                                .RoomGraphManager
                                .GetRoomContainingObject(_lastGuardToSeePlayer);
            _playerPositionIsKnown = false;
            InitSearchFromRoom( playerRoom, guardRoom, _lastGuardToSeePlayer, false );

        }

        /// <summary>
        /// Searches from a room (or its closest searchable neighbour) by
        /// reseting the blockedconnections and then giving the room to a searchtrack manager
        /// When the search track is searched, blocked connections will have been filled and
        /// the search will continue from ther, excluding rooms connected by blocked connections 
        /// /// </summary>
        /// <param name="playerRoom"></param>
        /// <param name="guardRoom"></param>
        /// <param name="lastSpottingGuard"></param>
        /// <param name="searchConfig"></param>
        private void InitSearchFromRoom(Room playerRoom, Room guardRoom,
                                        GuardStateBrainPBT lastSpottingGuard,
                                        bool performProperRoomExtraction,
                                        bool keepBlockedConnections = false
                                         )
        {
            _searchCount++;
            if( !keepBlockedConnections )
            {
                _blockedConnections.Clear();
            }
            
            _initialScanPerformed = false;
            Room roomToSearchFrom = playerRoom; // performProperRoomExtraction ? ExtractProperRoomToSearch(playerRoom, guardRoom, lastSpottingGuard) : playerRoom;

            _enemyPresenceAwareness = true;

            
            _firstGuardHasReachedSearchedRoom = false;
            _chaseState = ChaseState.SEARCHING_FOR_PLAYER;

            foreach( var guard in _guardFactory.AllGuards )
            {
                guard.ResetTarget ();
            }

            _searchTrackManager =
                _searchTrackManagerFactory.Create(
                    roomToSearchFrom.SearchTrack,
                    _guardFactory.AllGuards,
                    null
                );
            
        }

        /// <summary>
        /// Performs a clearing by sending a squad to the map.
        /// The squad searches every search track and then leaves the map.
        /// </summary>
        private void PerformClearing()
        {

            _squadManager = _squadManagerFactory.Create(
                3,
                SearchTrack.GetSearchTracks(),
                this
            );
            _squadManager.OnSquadClearingComplete += ReturnToNormal;

            SetGuardState(AI.CollectiveGuardState.CLEARING);

        }


        /// <summary>
        /// Returns the guards to normal by setting the guard state
        /// back to patrolling.
        /// </summary>
        private void ReturnToNormal ()
        {
            ReturnToNormal(false);
            
        }

        /// <summary>
        /// Returns the guards to normal by setting the guard state
        /// back to patrolling. Optional extra argument for saying whether
        /// guards should not be replaced. NOTE: THIS IS HANDLED WITH POLYMORPHISM
        /// INSTEAD OF DEFAULT VALUES TO ALLOW FOR THE NON-CONFIGURABLE
        /// METHOD TO SUBSCRIBE TO SIGNALS (SEE ONSQUADCLEARINGCOMPLETE FOR EXAMPLE)
        /// </summary>
        private void ReturnToNormal( bool skipReplacement )
        {
            _playerChangedRoomAfterGettingSpotted = false;
            _initialScanPerformed = false;
            SetGuardState(AI.CollectiveGuardState.PATROLLING, skipReplacement );
            _chaseState = ChaseState.DONE;

        }

        #endregion

        #region GENERAL_FUNCTIONALITY


        private IEnumerator WaitBeforeGettingEscapeRoutes ( Room room )
        {
            yield return new WaitForSeconds(1f);
            GetLikelyPlayerEscapeRoutes(
                _player.GetComponent<MovementCoordinator> (),
                room, ( List<RoomConnection> likelyEscapeRoutes, List<SearchTrackNode> likelySearchTrackNodes ) =>
                {
                    _likelyEscapeRoutes = likelyEscapeRoutes;
                }
            );

        }

        /// <summary>
        /// Sets the state and informs other components of the current guardstate
        /// by firing the StateChange signal.
        /// </summary>
        /// <param name="guardState"></param>
        private void SetGuardState(CollectiveGuardState guardState, bool skipReplacement = false )
        {
            if (guardState == AI.CollectiveGuardState.PATROLLING
                && _collectiveGuardState == AI.CollectiveGuardState.ATTACKING && !skipReplacement) 
            {
                StartCoroutine(ReplaceDeadGuards() );
            }

            if (guardState == AI.CollectiveGuardState.ATTACKING && 
                _collectiveGuardState == CollectiveGuardState.PATROLLING &&
                finishedReinforceGuards)
            {
                StartCoroutine(AddGuardAtExit());
            }

            _collectiveGuardState = guardState;
            _guardStatusChangeSignal.Fire(_collectiveGuardState); 

        }

        #endregion

        #region GUARD_DEATH_MANAGEMENT



        private IEnumerator ReplaceDeadGuards()
        {
            yield return new WaitForSeconds(1f);
            Dictionary<PatrolRouteEntity, GuardStateBrainPBT> newCollection =
                                new Dictionary<PatrolRouteEntity, GuardStateBrainPBT> ();
                                
            foreach (var keyValuePair in _guardsByPatrolRoute)
            {
                // dead guards are null
                GuardStateBrainPBT guard = keyValuePair.Value;
                PatrolRouteEntity patrolRoute = keyValuePair.Key;
                if ( guard == default(GuardStateBrainPBT))
                {
                    guard = _guardFactory.Create
                            (_guardReplacementExit.transform.position, patrolRoute);
                }
                newCollection[patrolRoute] = guard;
                yield return new WaitForSeconds(2f);
            }

            _guardsByPatrolRoute = newCollection;
        }

        /// <summary>
        /// Triggers when a guard dies.
        /// Creates a dead guard using the guard factory, removes the guard from lists etc.
        /// </summary>
        /// <param name="guard"></param>
        private void OnGuardDeath(GuardStateBrainPBT guard)
        { 
            _deadGuardFactory.Create(
                guard.transform.position,
                guard.MovementCoordinator.SerializedDir * 90
            );

            _guardsSeeingPlayer.Remove(guard);
            _guardsNotSeeingPlayer.Remove(guard);
            _availableGuards.Remove(guard);

            if( _guardFactory.AllGuards.Count == 0 )
            {
                ReturnToNormal ();
            }

        }


        #endregion


        #region GUARD_MICRO_MANAGEMENT

        /// <summary>
        /// Removes a guard from all shootingpositions
        /// </summary>
        /// <param name="guard"></param>
        private void RemoveGuardFromShootingPositions(GuardSpecificBehaviour guard)
        {
            for ( int i = 0; i < 4; i++ )
            {
                _guardsShootingPlayer[i].Remove(guard);
            }
        }

        /// <summary>
        /// Returns a shootingPosition and removes that guard from any previous shooting positions
        /// </summary>
        /// <param name="guard"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public int GetShootingPosition(GuardSpecificBehaviour guard, int dir)
        {
            RemoveGuardFromShootingPositions(guard);
            _guardsShootingPlayer[dir].Add(guard);
            return _guardsShootingPlayer[dir].Count;
        }


        /// <summary>
        /// Initializes _guardsShootingPlayer by creating one list for each direction [0,1,2,3]
        /// and adding it to the dictionary
        /// </summary>
        protected void InitGuardsShootingPlayer()
        {
            _guardsShootingPlayer = new Dictionary<int, List<GuardSpecificBehaviour>>();
            for (int i = 0; i < 4; i++)
            {
                _guardsShootingPlayer[i] = new List<GuardSpecificBehaviour>();
            }
        }

        
        public void RegisterNonPatrolRouteGuard(GuardStateBrainPBT guard)
        {

            GenericUtilities.SafelyAddItemToList(guard, _guardsNotSeeingPlayer);

        }


        #endregion


        #region OLD_UNUSED_METHODS


        /// <summary>
        /// 
        /// </summary>
        /// <param name="room"></param>
        /// <param name="callback"></param>
        private void GetLikelyPlayerEscapeRoutes
            ( MovementCoordinator searchingFromObject,  Room room, System.Action<List<RoomConnection>, List<SearchTrackNode>> callback )
        {
            List<RoomConnection> likelyEscapeRoutes = new List<RoomConnection> ();

            List<Vector2> blockedCoordinates = _player.GetComponent<MovementCoordinator> ()
                                                            .GetCoordinateBlockers(20, true);

            List<PathObjectPair> popsRoomConnection = new List<PathObjectPair> ();
            List<PathObjectPair> popsSearchTrackNode = new List<PathObjectPair> ();

            List<IStrategicPoint> _pointsToFilter = new List<IStrategicPoint> ();

            _pointsToFilter.AddRange( room.allConnections.Map(
                (RoomConnection rc) => {
                    return rc.graphicalRepresentation;
                })
            );


            _pointsToFilter.AddRange( room.SearchTrack.GetSearchTrackNodes() );
            
            

            AsyncListOperation<IStrategicPoint> listOperation =
                new AsyncListOperation<IStrategicPoint> (
                    _pointsToFilter,
                    (
                        IStrategicPoint strategicPoint,
                        System.Action callbackOperation
                    ) =>
                    {
                        _pathManager.RequestTask(
                            _player,
                            (MonoBehaviour) strategicPoint,
                            ( PathObjectPair pop ) => 
                            {
                                if( strategicPoint.GetType () == typeof( RoomConnectionRepresentation ) )
                                {
                                    popsRoomConnection.Add( pop );
                                }
                                else if( strategicPoint.GetType () == typeof( SearchTrackNode ) )
                                {
                                    popsSearchTrackNode.Add( pop );
                                }
                                
                                callbackOperation ();
                            },
                            blockedCoordinates
                        );
                    },
                    () =>
                    {
                        popsRoomConnection.Sort( ( PathObjectPair a, PathObjectPair b) => {
                            return a.GetPathLength () - b.GetPathLength ();
                        });
                        Room guardRoom = _levelLoadingManager
                                        .RoomGraphManager
                                        .GetRoomContainingObject(_lastGuardToSeePlayer);
                        foreach( var pop in popsRoomConnection )
                        {
                            
                            RoomConnection roomConnection =
                                        ((RoomConnectionRepresentation) pop.GetObject2 ()).RoomConnection;
                            List<RoomConnectorPathDepth> touchedRoomConnectors =
                                                            GetTouchedRoomConnectors( pop.GetPath() );
                            if( pop.GetPathLength() != 0 &&
                                touchedRoomConnectors.Count == 1 &&
                                (!roomConnection.ConnectsRooms( _playerLastKnownRoom, guardRoom ) && guardRoom != _playerLastKnownRoom)
                                
                                )
                            {
                                likelyEscapeRoutes.Add(
                                    ((RoomConnectionRepresentation) pop.GetObject2()).RoomConnection
                                );
                            }

                        }

                        if( likelyEscapeRoutes.Count == 0 )
                        {
                            likelyEscapeRoutes.AddRange( GetUnlikelyRoomConnections ( room, guardRoom, true ) );
                        }

                        List<SearchTrackNode> likelySearchTrackNodes = popsSearchTrackNode.Map( ( PathObjectPair pop) => { return (SearchTrackNode) pop.GetObject2(); });
                        
                        callback( likelyEscapeRoutes, likelySearchTrackNodes );
                    }


                );
                listOperation.RunParallel ();

            // return likelyEscapeRoutes;
        }


        /// <summary>
        /// Takes a list of points and a guard. Determines points as unlikely if the guard
        /// and the point are on the same side of the player with regard to each axis.
        /// </summary>
        /// <param name="guard"></param>
        /// <param name="points"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private List<T> GetUnlikelyPoints<T> (GuardStateBrainPBT guard, List<T> points ) where T: MonoBehaviour, IAlignedObject
        {


            Vector2 guardPos = guard.transform.position;
            Vector2 playerPos = _playerLastKnownPosition;

            List<T> unlikelyPoints = new List<T> ();


            foreach( T point in points )
            {
                Vector2 pointPosition = point.transform.position;
                bool shouldBeDisregarded = 
                    (guardPos.x > playerPos.x && pointPosition.x > playerPos.x) ||
                    (guardPos.x < playerPos.x && pointPosition.x < playerPos.x) ||
                    (guardPos.y > playerPos.y && pointPosition.y > playerPos.y) ||
                    (guardPos.y < playerPos.y && pointPosition.y < playerPos.y);
                
                if( shouldBeDisregarded )
                {
                    unlikelyPoints.Add( point );
                }
            }

            return unlikelyPoints;

        }

        private bool PointIsUnlikely<T> ( GuardStateBrainPBT guard, T point, bool reverse = false ) where T: MonoBehaviour, IAlignedObject
        {

            Vector2 guardPos = guard.transform.position;
            Vector2 playerPos = _playerLastKnownPosition;
            Vector2 pointPosition = point.transform.position;

            float xDiff = Mathf.Abs( guardPos.x - playerPos.x );
            float yDiff = Mathf.Abs( guardPos.y - playerPos.y );

            // bool checkY = yDiff > xDiff && point.Orientation == Orientation.;
            bool checkX = xDiff >= yDiff;

            bool pointIsUnlikely = 
                    (guardPos.x > playerPos.x && pointPosition.x > playerPos.x && checkX) ||
                    (guardPos.x < playerPos.x && pointPosition.x < playerPos.x && checkX) ||
                    (guardPos.y > playerPos.y && pointPosition.y > playerPos.y && !checkX) ||
                    (guardPos.y < playerPos.y && pointPosition.y < playerPos.y && !checkX);

            if( reverse )
            {
                return !pointIsUnlikely;
            }

            return pointIsUnlikely;

        }


        private List<RoomConnection> GetUnlikelyRoomConnections ( Room room, Room guardRoom, bool reverse = false  )
        {
            
            List<RoomConnection> unlikelyRoomConnections =
                
                room.allConnections.Filter((RoomConnection rc) =>
                {

                    if( room == guardRoom )
                    {
                        return PointIsUnlikely( _lastGuardToSeePlayer, rc.graphicalRepresentation, reverse );
                    }

                    if( rc.ConnectsRooms( room, guardRoom ) )
                    {
                        return true;
                    }

                    
                    return PointIsUnlikely( _lastGuardToSeePlayer, rc.graphicalRepresentation )&& !rc.ConnectsRooms( room, guardRoom );

                }

            );
            

            return unlikelyRoomConnections;

        }


        
        /// <summary>
        /// This method helps in finding a searchable room
        /// in case the player's room isn't searchable.
        /// Reasons for not being searchable are
        ///     1. There are no nodes in the room' searchtrack
        ///     2. The room is marked as a connector
        /// If any of these criteria are true, all connected rooms
        /// are investigated, and the one with an entrance closest
        /// to the guard is chosen.
        /// 
        /// If the room is searchable it is simply returned.
        /// </summary>
        /// <param name="playerRoom"></param>
        /// <param name="guardRoom"></param>
        /// <param name="guard"></param>
        /// <returns></returns>
        private Room ExtractProperRoomToSearch(Room playerRoom, Room guardRoom,
                                                        GuardStateBrainPBT guard)
        {

            SearchTrack searchTrack = playerRoom.SearchTrack;

            if (searchTrack.GetSearchTrackNodes().Count == 0 || playerRoom.IsConnector )
            {
                // it is a connection corridor, pick one of its neighbours that isn't the guardRoom
                float shortestDistance = 100000f;
                RoomConnectionRepresentation closestConnection = null;
                Room closestRoom = null;
                foreach (KeyValuePair<Room, List<RoomConnection>> kvp in playerRoom.connectionsByRoom)
                {

                    Room room = kvp.Key;
                    List<RoomConnection> connections = kvp.Value;
                    if (room == guardRoom)
                    {
                        continue;
                    }

                    foreach (RoomConnection connection in connections)
                    {
                        float distance = _geometryUtilities.DistanceBetweenObjects
                                (guard.gameObject, connection.graphicalRepresentation.gameObject);

                        if (distance < shortestDistance)
                        {

                            closestConnection = connection.graphicalRepresentation;
                            closestRoom = room;
                            shortestDistance = distance;

                        }
                    }
                }

                if (closestRoom != null)
                {
                    return closestRoom;
                }

            }

            return playerRoom;



        }


        


        #endregion


        private class RoomConnectorPathDepth
        {
            private RoomConnection _roomConnection;
            private int _depth;

            public RoomConnection RoomConnection { get => _roomConnection; }
            public int Depth { get => _depth; }

            public RoomConnectorPathDepth ( RoomConnection roomConnection, int depth )
            {
                _roomConnection = roomConnection;
                _depth = depth;
            }


        }

    }
}