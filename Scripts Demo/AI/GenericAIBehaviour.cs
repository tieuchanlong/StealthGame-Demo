using UnityEngine;
using StealthGame.Actors.Movement;
using Panda;
using Zenject;
using System.Collections.Generic;
using StealthGame.MapLoadingLayer;
using StealthGame.RoomClassification;
using System.Collections;

namespace StealthGame.AI 
{



    public class GenericAIBehaviour : BehaviourBase
    {
    
        public enum MovementGoalType { HEARD_NOISE, TARGET, COVER_SELF,
                        COVER_TARGET, ROOM, PLAYER, PLAYER_ROOM, DEAD_GUARD, FOOTSTEP, FOOTSTEP_PATH, LOCKER }

        public enum AimingTargetType { TARGET, MOVEMENT_GOAL, SEARCH_POINT, LOCKER, DEFAULT }


        #region FUNCTIONAL_VARIABLES_DECLARATIONS


        private MovementGoalType _movementGoalType;

        [SerializeField]
        protected GameObject _parentObject;

        LevelLoadingManager _levelLoadingManager;

        protected float searchArea = 4f;
        protected bool searchingLocker = false;

        protected LockersManager _lockerManager;
        protected DoorController _currentLocker = null;

        #endregion

        #region COVER_DECLARATIONS

        protected List<CoverPoint> _coverPoints;
        protected CoverPoint _coverPoint;

        #endregion

        #region SIGNALS

        protected AIPathFindCompleteSignal _aiPathFindCompleteSignal;

        protected MapStartSignal _mapStartSignal;

        [Task]
        public bool MoveToFirstStep;

        #endregion

        

        #region GENERIC_DECLARATIONS
        protected PlayerController _player;

        [Inject]
        protected TilemapManager _tilemapManager;

        protected List<PathObjectPair> _pathObjectPairsForRoomRoomExitFinding = new List<PathObjectPair> ();
        protected bool _roomExitFindingComplete = false;

        protected PathEntity _preComputedPathToMovementGoal;


        protected MapHelper _mapHelper;

        [SerializeField]
        protected MovementCoordinator _movementCoordinator;

        [SerializeField]
        protected MinimapObjectController _minimapObjectController;


        [SerializeField]
        protected EmojiManager _emojiManager;


        [SerializeField]
        protected VisionCone _visionCone;

        [SerializeField]
        protected HearingObjectController _hearingObjectController;

        [SerializeField]
        protected WeaponBase _weapon;

        [Inject]
        protected GeometryUtilities _geometryUtilities;

        [Inject]
        protected FootStepFactory _footStepFactory;

        private FootStepController _footStepController;




        [Task]
        public bool HasTarget 
        {
            get
            {
                return _target != null;
            }
        }

        [Task]
        public bool HasDeadGuard
        {
            get
            {
                return _deadGuardController != null;
            }
        }

        [Task]
        public bool HasFootStep
        {
            get
            {
                return _footStepFactory.LatestFootStep != null && _hasFootStep;
            }
        }

        [Task]
        public bool IsInSameRoomAsPlayer
        {
            get
            {
                Room myRoom = _levelLoadingManager
                                .RoomGraphManager
                                .GetRoomContainingObject(gameObject);
                Room playerRoom = _levelLoadingManager
                                    .RoomGraphManager
                                    .GetRoomContainingObject(_player);
                return myRoom == playerRoom;
            }
        }

        [Task]
        public bool CanSeeTarget
        {
            get
            {
                if( _target == null )
                {
                    return false;
                }
                return _visionCone.ObjectIsVisible(_target.gameObject);
            }
        }

        

        [Task]
        public bool RoomHasChanged
        {
            get
            {
                return _roomOfInterest != _roomOfInterestLastFrame;
            }
        }

        [Task]
        public bool HasRoom
        {
            get
            {
                // sometimes has value null for empty
                // someitmes a room with all values set to null
                if( _roomOfInterest != null )
                {
                    return _roomOfInterest.Tilemap != null;
                }
                return false;
                
            }
        }

        [Task]
        public bool HeardNoise
        {
            get
            {
                return _heardNoise != null;
            }
        }

        #endregion

        #region EMOJI_DECLARATIONS

            AudibleSound _currentCallout;

        #endregion


        #region MOVEMENT_DECLARATIONS

            protected MonoBehaviour _movementGoal;
            protected PathEntity _path;
            protected int _pathIndex;
            protected PathManager _pathManager;
            protected bool _pathIsFound;
            protected bool _coverPointFound;

            protected List<Vector2> _doorStopPoints;

            [SerializeField]
            protected MovementController _movementController;

            protected bool _needResetPathFind = false;

        #endregion

        #region GETTERS_AND_SETTERS

        public PathEntity Path
        {
            get
            {
                return _path;
            }
        }

        public int PathIndex 
        {
            get
            {
                return _pathIndex;
            }
        }

        public CoverPoint CoverPoint
        {
            get
            {
                return _coverPoint;
            }
        }

        #endregion


        #region STARTUP

            protected override void Awake()
            {

                base.Awake ();

                if( _movementCoordinator == null )
                {
                    _movementCoordinator = GetComponent<MovementCoordinator>();
                }
                
                _pathIndex = 0;
                _pathIsFound = false;
                _coverPoints = CoverPoint.GetInstances ();

            }



            [Inject]
            public void Construct(PathManager pathManager, MapHelper mapHelper,
                            AIPathFindCompleteSignal aIPathFindCompleteSignal,
                            PlayerController player,
                            LevelLoadingManager levelLoadingManager,
                            MapStartSignal mapStartSignal,
                            FootStepFactory footStepFactory,
                            LockersManager lockersManager
                        )
            {

                _player = player;
                _pathManager = pathManager;
                _mapHelper = mapHelper;
                _aiPathFindCompleteSignal = aIPathFindCompleteSignal;
                _levelLoadingManager = levelLoadingManager;
                _footStepFactory = footStepFactory;
                _lockerManager = lockersManager;

                _mapStartSignal = mapStartSignal;

            }

            protected override void OnEnable ()
            {
                base.OnEnable ();
                _mapStartSignal += OnMapStart;
            
            }

            protected override void OnDisable ()
            {
                base.OnEnable ();
                _mapStartSignal -= OnMapStart;
            }

        #endregion


        #region OTHER_EVENTS

            protected virtual void OnMapStart ()
            {

            }

        #endregion

        


        #region UTIL_TASKS

            [Task]
            public void DestroySelf ()
            {
                Destroy(_stateBrain.ParentGameObject);
            }

        #endregion

        #region MOVEMENT_METHODS

            /// <summary>
            /// Sets the movement goal of the behaviour based on input.
            /// - For the values HEARD_NOISE, TARGET and PLAYER it sets it to the corresponding object.
            /// - For the values COVER_TARGET and COVER_SELF it takes the cover points, sorts them by distance and makes sure
            ///   that there is an obstacle between the cover and the target or Behaving GameObject (corresponding to input)
            /// - For the value ROOM it performs a pathfinding to the exits of the room and picks the exit that has the
            ///   shortest path and sets it as the movement goal along with the precomputed path.
            /// </summary>
            /// <param name="movementGoalType"></param>
            [Task]
            public void SetMovementGoal ( MovementGoalType movementGoalType )
            {
                _movementGoalType = movementGoalType;
                if( movementGoalType == MovementGoalType.PLAYER_ROOM )
                {
                    
                    _roomOfInterest = _levelLoadingManager
                                        .RoomGraphManager
                                        .GetRoomContainingObject( _player );
                    movementGoalType = MovementGoalType.ROOM;

                }
                

                switch ( movementGoalType )
                {
                    case MovementGoalType.FOOTSTEP:
                        SetMovementGoal(_footStepFactory.FirstFootStep, null);
                        Task.current.Complete(true);
                        break;
                    case MovementGoalType.FOOTSTEP_PATH:
                        SetMovementGoal( _footStepFactory.LatestFootStep, _footStepFactory.Path );
                        Task.current.Complete(true);
                        break;
                    case MovementGoalType.HEARD_NOISE:  SetMovementGoal(_heardNoise); Task.current.Complete(true); break;
                    case MovementGoalType.LOCKER:
                    {
                        if (_lockerManager.CurrentLocker != null)
                            SetMovementGoal(_lockerManager.CurrentLocker.gameObject.transform.parent.transform.GetChild(1).transform.gameObject.GetComponent<DestinationSearch>());
                        Task.current.Complete(true); break;
                    }
                    case MovementGoalType.TARGET:       SetMovementGoal(_target); Task.current.Complete(true); break;
                    case MovementGoalType.PLAYER:       SetMovementGoal(_player); Task.current.Complete(true); break;
                    case MovementGoalType.DEAD_GUARD:   SetMovementGoal(_deadGuardController);Task.current.Complete(true);break;
                    case MovementGoalType.COVER_TARGET:
                    case MovementGoalType.COVER_SELF:
                    {
                        if( Task.current.isStarting )
                        {
                            _coverPointFound = false;
                            _coverPoint = null;
                            List<PathObjectPair> pops = new List<PathObjectPair> ();
                            List<Vector2> blockedCoordinates = GetBlockedCoordinates ();

                            AsyncListOperation<CoverPoint> listOperation =
                                new AsyncListOperation<CoverPoint>(
                                    _coverPoints,
                                    ( 
                                        CoverPoint coverPoint,
                                        System.Action callbackOperation
                                    ) =>
                                    {
                                        _pathManager.RequestTask(
                                            this,
                                            coverPoint,
                                            ( PathObjectPair pop ) =>
                                            {
                                                pops.Add(pop);
                                                callbackOperation ();
                                            },
                                            blockedCoordinates
                                        );

                                    },
                                    () =>
                                    {
                                        pops.Sort( (PathObjectPair a, PathObjectPair b) =>
                                        {
                                            
                                            float distanceA = a.GetPathLength ();
                                            float distanceB = b.GetPathLength();
                                            
                                            if( distanceA == 0 )
                                            {
                                                distanceA = 999999;
                                            }

                                            if( distanceB == 0 )
                                            {
                                                distanceB = 999999;
                                            }

                                            return (int) distanceA - (int) distanceB;

                                        });

                                        GameObject coverFrom = gameObject;
                                        
                                        if( movementGoalType == MovementGoalType.COVER_TARGET )
                                        {
                                            coverFrom = _target.gameObject;
                                        }

                                        foreach( var pop in pops )
                                        {
                                            RaycastHit2D hit = _mapHelper.Probe(
                                                coverFrom,
                                                ((MonoBehaviour) pop.GetObject2()).gameObject,
                                                new string[] {"CeilingLayer", "WallLayer"}
                                            );


                                            if( hit.collider != null )
                                            {
                                                _coverPoint = (CoverPoint) pop.GetObject2();
                                                SetMovementGoal(_coverPoint);
                                                _preComputedPathToMovementGoal = pop.GetPath ();
                                                _coverPointFound = true;
                                                break;
                                            }
                                        }
                                    }
                                );
                            listOperation.RunParallel();

                        }
                        else if( _coverPointFound )
                        {
                            Task.current.Complete(true);
                        }
                        

                        
                        // _coverPoints.Sort( (CoverPoint a, CoverPoint b) =>
                        // {
                            
                        //     float distanceA = (a.transform.position - transform.position).magnitude;
                        //     float distanceB = (b.transform.position - transform.position).magnitude;
                        //     return (int) distanceA - (int) distanceB;

                        // });
                        // GameObject coverFrom = gameObject;
                        // if( movementGoalType == MovementGoalType.COVER_TARGET )
                        // {
                        //     coverFrom = _target.gameObject;
                        // }
                        // foreach( CoverPoint coverPoint in _coverPoints )
                        // {
                        //     RaycastHit2D hit = _mapHelper.Probe(
                        //         coverFrom,
                        //         coverPoint.gameObject,
                        //         new string[] {"CeilingLayer", "WallLayer"}
                        //     );


                        //     if( hit.collider != null )
                        //     {
                        //         _coverPoint = coverPoint;
                        //         break;
                        //     }
                        // }

                        // // if no cover was found we should probably be more aggressive.
                        // SetMovementGoal(_coverPoint);
                        // Task.current.Complete(true);

                    }; break;
                    case MovementGoalType.ROOM:
                    {

                        if( Task.current.isStarting )
                        {
                            PerformRoomExitPathFinding ();
                        }
                        else if( _roomExitFindingComplete )
                        {
                            SetMovementGoal(
                                _pathObjectPairsForRoomRoomExitFinding[0].GetObject2 (),
                                _pathObjectPairsForRoomRoomExitFinding[0].GetPath ()
                            );
                            Task.current.Complete (true);
                        }
                    };break;
                }

                
            }
            
            /// <summary>
            /// Sets the movement goal to a specific Monobehaviour. Use this method if a path has already been found as part of another process.
            /// </summary>
            /// <param name="movementGoal"></param>
            /// <param name="preComputedPath"></param>
            public void SetMovementGoal(MonoBehaviour movementGoal, PathEntity preComputedPath = null  )
            {
                _preComputedPathToMovementGoal = preComputedPath;
                _movementGoal = movementGoal;

            }

            
            /// <summary>
            /// Waits for 1 second, then resets the need for pathfinding. This is used when following a moving target.
            /// Run as coroutine.
            /// </summary>
            /// <returns></returns>
            protected virtual IEnumerator ResetPathFindNeed ()
            {

                yield return new WaitForSeconds(1f);
                _needResetPathFind = true;
            }

            [SerializeField]
            protected List<Vector2> GetBlockedCoordinates ()
            {

                int length = 3;
                List<Vector2> blockedCoordinates = new List<Vector2> ();


                Vector2 baseCoordinateAddition = new Vector2(0,0);
                Vector2 minimumValue = new Vector2(0,0);
                Vector2 maximumValue = new Vector2(0,0);
                Vector2 increment = new Vector2(0,0);
                
                if( _movementCoordinator.Dir == 0 || _movementCoordinator.Dir == 180 )
                {

                    baseCoordinateAddition =    _movementCoordinator.Dir == 0 ? Vector2.right :
                                                                                Vector2.left;
                    minimumValue = Vector2.up;
                    maximumValue = Vector2.down;
                
                }
                else
                {
                    baseCoordinateAddition =    _movementCoordinator.Dir == 90 ? Vector2.up :
                                                                                 Vector2.down;
                    minimumValue = Vector2.left;
                    maximumValue = Vector2.right;
                }

                increment = maximumValue;
                minimumValue *= length;
                maximumValue *= length;
                Vector2 baseCoordinate = (Vector2) transform.position + baseCoordinateAddition;
                Vector2 currentPosition = baseCoordinate  + minimumValue;

                for ( int i = 0; i < 2*length + 1; i++  )
                {
                    
                    blockedCoordinates.Add( _tilemapManager.WorldPosToTilePos(  currentPosition) );
                    currentPosition += increment;

                }

                return blockedCoordinates;

            }

            /// <summary>
            /// Moves to the movement goal that was previously set by first pathfinding, and when that is done
            /// using the StepPath method in the movement controller. If there is a precomputed path stored it is used instead of pathfinding.
            /// The reason that the precomputed path is not used as an argument is that Panda Behaviour Trees won't be able to access it
            /// If the target is moving, a new pathfinding will be performed once per second.
            /// </summary>
            /// <param name="movingTarget">Whether or not the target is moving</param>
            [Task]
            public void MoveToMovementGoal( bool movingTarget = false, bool useBlockedCoordinates = false )
            {
                
                if ( Task.current.isStarting || _needResetPathFind )
                {
                    _needResetPathFind = false;
                    if( _preComputedPathToMovementGoal == null )
                    {
                        
                        List<Vector2> blockedCoordinates = null;
                        
                        if( useBlockedCoordinates )
                        {
                            blockedCoordinates = GetBlockedCoordinates ();
                        }

                        _pathIsFound = false;
                        //_movementCoordinator.UnlockDir();
                        _pathManager.RequestTask(
                            this,
                            _movementGoal,
                            (PathObjectPair pop) =>
                            {
                                _pathIsFound = true;
                                _path = pop.GetPath();
                                
                                _pathIndex = 0;
                                _aiPathFindCompleteSignal.Fire(_path);
                                //  _path.PaintPath();
                                    
                            },
                            blockedCoordinates
                        );
                        if( movingTarget )
                        {
                            StartCoroutine( ResetPathFindNeed() );
                            _needResetPathFind = false;
                        }
                        

                    }
                    else
                    {
                        _path = _preComputedPathToMovementGoal;
                        _pathIndex = 0;
                        _pathIsFound = true;
                        _aiPathFindCompleteSignal.Fire(_preComputedPathToMovementGoal);
                    }
                    
                }

                if (_pathIsFound)
                {
                    if (_path.GetLength() != 0)
                    {
                        
                        _pathIndex = _movementController.StepPath(_path, _pathIndex);
                        if (_pathIndex == -1)
                        {
                            Task.current.Complete(true);
                        }
                    }
                    else
                    {
                        Task.current.Complete(false);
                    }
                }

            }

            /// <summary>
            /// Uses the movement coordinator to face any direction
            /// </summary>
            /// <param name="dir"></param>
            [Task]
            public void FaceDir( int dir )
            {
                _movementCoordinator.Dir = dir & 360;
            }

            /// <summary>
            /// Turns 45 degrees to the left a set amount of times. For example 3 times would be 3x45 = 135.
            /// </summary>
            /// <param name="times">How many times to turn to the left</param>
            [Task]
            public void TurnLeft(int times = 1)
            {
                _movementCoordinator.Dir = (_movementCoordinator.Dir + 45 * times) % 360;
                Task.current.Complete(true);
            }

            /// <summary>
            /// Turns 45 degrees to the right a set amount of times. For example 3 times would be 3x45 = 135.
            /// Since unity does not like negative angles a check is made to make sure this does not happen.
            /// </summary>
            /// <param name="times">How many times to turn to the right</param>
            [Task]
            public void TurnRight(int times = 1)
            {

                int tempDir = _movementCoordinator.Dir;

                for (int i = times; i > 0; i--)
                {
                    tempDir -= 45;
                    if (tempDir < 0)
                    {
                        tempDir += 360;
                    }
                }

                _movementCoordinator.Dir = tempDir;
                Task.current.Complete(true);

            }



        #endregion

        #region EMOJIS

        /// <summary>
        /// Uses the EmojiManager to show an emoji ?/! above the Behaving Object.
        /// </summary>
        /// <param name="emojiType">Which type of emoji should be shown</param>    
        [Task]
        public void ShowEmoji ( EmojiManager.EmojiType emojiType )
        {
            
            if( emojiType == EmojiManager.EmojiType.EXCLAMATION_MARK )
            {
                _emojiManager.ShowExclamationMark();
            }
            else if( emojiType == EmojiManager.EmojiType.QUESTION_MARK )
            {
                _emojiManager.ShowQuestionMark();
            }

            Task.current.Complete(true);
            
        }

        /// <summary>
        /// Uses the EmojiManager to hide whichever emoji is currently shown.
        /// </summary>
        [Task]
        public void HideEmoji()
        {      
            _emojiManager.HideEmoji();
            Task.current.Complete(true);

        }

        /// <summary>
        /// Uses the CalloutController.Guard to make a callout of the specified type.
        /// If a callout is already being made, it is stopped.
        /// </summary>
        /// <param name="calloutType"></param>
        [Task]
        public void Callout(CalloutControllerGuard.CalloutType calloutType)
        {
            if (Task.current.isStarting)
            {

                if (
                _currentCallout != default(AudibleSound) &&
                _currentCallout.IsPlaying
                )
                {
                    _currentCallout.Stop();
                }

                Debug.Log(calloutType);

                _currentCallout = CalloutController.Guard
                    .PerformGenericCallout(
                    calloutType,
                    transform.position,
                    _minimapObjectController,
                    this

            );

            }

            if ( _currentCallout == null || !_currentCallout.IsPlaying)
            {
                Task.current.Complete(true);
            }

        }

        #endregion

        #region MINIMAP    

            [Task]
            public void ResetVisionConeColour()
            {
                _minimapObjectController.ResetVisionConeColor();
                Task.current.Complete(true);
            }

            [Task]
            public void SetVisionConeColour( MinimapObjectController.VisionConeColourType colourType )
            {
                _minimapObjectController.SetVisionConeColor( colourType );
                Task.current.Complete(true);
            }

        #endregion


        #region AIMING

            /// <summary>
            /// Uses the MovementCoordinator to stop aiming
            /// </summary>
            [Task]
            public void StopAiming()
            {

                _movementCoordinator.StopAiming();
                Task.current.Complete(true);
            }

            /// <summary>
            /// Defers to StartAimingConfigurable to aim at the target regardless of if it's visible
            /// </summary>
            [Task]
            public void StartAiming ()
            {
                StartAimingConfigurable( AimingTargetType.TARGET, false );
            }


            /// <summary>
            /// Sets an aiming target. Can be configured to only aim if target is visible.
            /// </summary>
            /// <param name="aimingTargetType"></param>
            /// <param name="onlyIfVisible"></param>
            [Task]
            public void StartAimingConfigurable( AimingTargetType aimingTargetType = AimingTargetType.TARGET, bool onlyIfVisible = false ){
                
                // GameObject aimingTarget = aimingTargetType == AimingTargetType.MOVEMENT_GOAL ? _movementGoal.gameObject : _target.gameObject;

                GameObject aimingTarget;
                switch( aimingTargetType )
                {
                    case AimingTargetType.MOVEMENT_GOAL: aimingTarget = _movementGoal.gameObject; break;
                    case AimingTargetType.TARGET: aimingTarget = _target.gameObject; break;
                case AimingTargetType.LOCKER: aimingTarget = _movementGoal.gameObject.transform.parent.gameObject; break;
                    default: aimingTarget = null; break;
                }


                // switch( aimingTargetType )
                // {
                //     case AimingTargetType.MOVEMENT_GOAL: aimingTarget = _movementGoal; break;
                //     case AimingTargetType.TARGET: aimingTarget = _target; break;

                // }

                _movementCoordinator.SetAimingTarget( aimingTarget, onlyIfVisible );
                Task.current.Complete(true);

            }

        #endregion
        
        #region ATTACKING

            [Task]
            public void Shoot(){
                _weapon.Fire( (int) _geometryUtilities.DirectionToObject(gameObject, _target.gameObject) );
                Task.current.Complete(true);
            }

        #endregion

        #region CLEANUP

            /// <summary>
            /// Rests the noiseinvestigation by unregistering itself as a dependency on the noise it heard
            /// and then setting the _heardNoise variable to null
            /// </summary>
            [Task]
            public void CompleteNoiseInvestigation ()
            {
                if (_heardNoise != null)
                {
                    _heardNoise.UnRegisterDependancy(this);
                    _heardNoise = null;
                }
                Task.current.Complete(true);
            }

        #endregion

        #region UTIL_METHODS
        /// <summary>
        /// Uses an AsyncListOperation to pathfind to all exits of the _roomOfInterest
        /// This process is asynchronous and it sets _roomExitFindingComplete to true when done
        /// </summary>
        protected void PerformRoomExitPathFinding ()
        {
            _pathObjectPairsForRoomRoomExitFinding = new List<PathObjectPair> ();
            _roomExitFindingComplete = false;

            AsyncListOperation<RoomConnection> listOperation =
                new AsyncListOperation<RoomConnection> (
                    _roomOfInterest.allConnections,
                    (
                        RoomConnection roomConnection,
                        System.Action callbackOperation
                    ) =>
                    {
                            _pathManager.RequestTask (
                            this,
                            roomConnection.graphicalRepresentation,
                            (PathObjectPair receivedPath) =>
                            {
                                _pathObjectPairsForRoomRoomExitFinding.Add(receivedPath);
                                callbackOperation ();
                            }


                        );
                    },
                    () =>
                    {
                        Debug.Log(_pathObjectPairsForRoomRoomExitFinding.Count);
                        _movementGoal = _pathObjectPairsForRoomRoomExitFinding[0].GetObject2 ();

                        _pathObjectPairsForRoomRoomExitFinding.Sort( (PathObjectPair a, PathObjectPair b ) => 
                        {
                            return a.GetPathLength() - b.GetPathLength ();
                        });
                        _roomExitFindingComplete = true;
                        
                    }

                );


            listOperation.RunParallel();
        }

        #endregion
    
    }
}
