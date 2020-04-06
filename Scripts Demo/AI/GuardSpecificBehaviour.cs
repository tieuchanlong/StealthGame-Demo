using UnityEngine;
using Panda;
using Zenject;
using StealthGame.RoomClassification;
using System.Collections;
using System.Collections.Generic;
using StealthGame.MapLoadingLayer;

namespace StealthGame.AI
{
    public class GuardSpecificBehaviour : GenericAIBehaviour
    {

        /// <summary>
        /// A representation of the conditions during which this guard spotted the player.
        /// Default is used for every situation except for if the player was not visible to any guards during either patrol or alert phase
        /// </summary>
        public enum TargetRelationship { DEFAULT, FIRST_SPOTTER_ON_PATROL, FIRST_SPOTTER_ON_ALERT };

        #region FUNCTIONAL_VARIABLES_DECLARATION

        [SerializeField]
        private int _shootingPosition;

        private LevelLoadingManager _levelLoadingManager;

        private Vector3 _initialShootingPosition;
        private bool _hasSteppedAside;

        private Vector2 _initialMovementDirection, _initialPerpendicularDirection;

        private int _initialSerializedDir;

        [Task]
        public bool SeePlayer
        {
            get
            {
                return _player.Recognized;
            }
        }

        [Task]
        ///<summary>
        /// Check if the Current Guard has any locker to search for
        ///</summary>
        public bool HasLocker
        {
            get
            {
                return _currentLocker != null;
            }
        }



        #endregion

        #region SIGNALS_DECLARATION

        protected GuardReportEnemyPresenceSignal _guardReportEnemyPresenceSignal;

        protected NodeSearchedSignal _nodeSearchedSignal;

        protected GuardReachedRoomSignal _guardReachedRoomSignal;

        protected GuardReportDeadGuardSignal _guardReportDeadGuardSignal;



        #endregion


        #region OTHER_COPMPONENTS

        #endregion

        #region AI

        [SerializeField]
        private TargetRelationship _targetRelationship;

        private Room _roomOfInterest;

        #endregion

        #region MANAGER_DECLARATIONS

        private GuardManager _guardManager;

        #endregion




        #region TASK_PROPERTIES

        /// <summary>
        /// Utilizes the GuardManager to see if the player is known
        /// </summary>
        /// <returns></returns>
        [Task]
        public bool PlayerPositionIsKnown
        {
            get
            {
                return _guardManager.PlayerPositionIsKnown;
            }
        }

        /// <summary>
        /// Whether or not the guard has a searchtrack
        /// </summary>
        /// <returns></returns>
        [Task]
        public bool HasSearchTrack
        {
            get
            {
                return _searchTrack != null;
            }
        }

        /// <summary>
        /// Returns whetheror not the guard has a searchpoint
        /// </summary>
        /// <returns></returns>
        [Task]
        public bool HasSearchPoint
        {
            get
            {
                return _searchPoint != null;
            }
        }


        /// <summary>
        /// Utilizes the guard manager to see if any guard is aware of the player's presence on the map
        /// </summary>
        /// <returns></returns>
        [Task]
        public bool EnemyPresenceAwareness
        {
            get
            {
                return _guardManager.EnemyPresenceAwareness;
            }
        }

        /// <summary>
        /// Utilizes the guard manager to check if any guard can currently see the player
        /// </summary>
        /// <returns></returns>
        [Task]
        public bool PlayerIsVisible
        {
            get
            {
                return _guardManager.PlayerIsVisible;
            }
        }

        /// <summary>
        /// Utilizes the _targetRelationship variable to check if the guard is a default spotter
        /// </summary>
        /// <returns></returns>
        [Task]
        public bool IsDefaultSpotter
        {
            get
            {
                return _targetRelationship == TargetRelationship.DEFAULT;
            }
        }

        /// <summary>
        /// Utilizes the _targetRelationship variable to check if this guard was the first guard to spot the player
        /// during the patrol phase
        /// </summary>
        /// <returns></returns>
        [Task]
        public bool IsFirstSpotterPatrol
        {
            get
            {
                return _targetRelationship == TargetRelationship.FIRST_SPOTTER_ON_PATROL;
            }
        }

        /// <summary>
        /// Utilizes the _targetRelationship variable to check if this guard was the first guard to spot the player
        /// during the alert phase
        /// </summary>
        /// <returns></returns>
        [Task]
        public bool IsFirstSpotterAlert
        {
            get
            {
                return _targetRelationship == TargetRelationship.FIRST_SPOTTER_ON_ALERT;
            }
        }
        /// <summary>
        /// Utilizes the RoomGraphManager to check if the guard is in the same room as its searchpoint
        /// </summary>
        /// <returns></returns>
        [Task]
        public bool IsInSameRoomAsSearchPoint
        {
            get
            {
                return _levelLoadingManager.RoomGraphManager.GetRoomContainingObject(_parentObject) ==
                    _levelLoadingManager.RoomGraphManager.GetRoomContainingObject(_searchPoint);
            }
        }

        


        #endregion


        #region STARTUP_METHODS

        [Inject]
        public void Construct(GuardReportEnemyPresenceSignal
                                            guardReportEnemyPresenceSignal,
                                GuardManager guardManager,
                                GuardReachedRoomSignal guardReachedRoomSignal,
                                NodeSearchedSignal nodeSearchedSignal,
                                LevelLoadingManager levelLoadingManager,
                                GuardReportDeadGuardSignal
                                                guardReportDeadGuardSignal
                                )
        {
            _guardReportEnemyPresenceSignal = guardReportEnemyPresenceSignal;
            _guardManager = guardManager;
            _guardReachedRoomSignal = guardReachedRoomSignal;
            _nodeSearchedSignal = nodeSearchedSignal;
            _levelLoadingManager = levelLoadingManager;
            _guardReportDeadGuardSignal = guardReportDeadGuardSignal;
        }

        private void Awake()
        {
            base.Awake();
            if (_stateBrain.GetType() != typeof(GuardStateBrainPBT))
            {
                throw new System.InvalidCastException("Guard statebrain is not a GuardStateBrainPBT");
            }

        }


        #endregion

        #region LISTERNERS

        /// <summary>
        /// Overrides the inherited method to check the situation during which the player was spotted
        /// and stores the result of this in the _targetRelationship variable.
        /// Calls the inherited method in the end.
        /// </summary>
        /// <param name="target"></param>
        protected override void OnStatebrainChangedTarget(MonoBehaviour target)
        {

            if (target != null)
            {
                if (_guardManager.AmountOfGuardsWithVision == 0 && !_guardManager.EnemyPresenceAwareness)
                {
                    _targetRelationship = TargetRelationship.FIRST_SPOTTER_ON_PATROL;
                }
                else if (_guardManager.AmountOfGuardsWithVision == 0 && _guardManager.EnemyPresenceAwareness)
                {
                    _targetRelationship = TargetRelationship.FIRST_SPOTTER_ON_ALERT;
                }
                else
                {
                    _targetRelationship = TargetRelationship.DEFAULT;
                }
            }


            base.OnStatebrainChangedTarget(target);

        }
        

        #endregion


        #region TASKS

        /// <summary>
        /// 
        /// </summary>
        [Task]
        public void ReportDeadGuard ()
        {
            _guardReportDeadGuardSignal.Fire ( this );
            Task.current.Complete(true);
        }

        /// <summary>
        /// Resets the target relationship to default
        /// </summary>
        [Task]
        public void ResetTargetRelationship()
        {
            _targetRelationship = TargetRelationship.DEFAULT;
            Task.current.Complete(true);
        }

        /// <summary>
        /// Interfaces with the vision cone to increase its range. This is used during alert mode
        /// </summary>
        [Task]
        public void IncreaseVisionConeRange()
        {
            _visionCone.IncreaseRange();
            Task.current.Complete(true);
        }

        /// <summary>
        /// Interfaces with the vision cone to reset its range to default value.
        /// This is used when the game returns to normal after alert mode is over
        /// </summary>
        [Task]
        public void ResetVisionConeRange()
        {
            _visionCone.ResetRange();
            Task.current.Complete(true);
        }

        /// <summary>
        /// Sets the movement goal to the current _searchPoint
        /// </summary>
        [Task]
        public void SetMovementGoalToSearchPoint()
        {
            SetMovementGoal(_searchPoint);
            Task.current.Complete(true);
        }

        /// <summary>
        /// Marks the current searchpoint as searched
        /// by firing the NodeSearchedSignal that SearchTrackManagers listen for
        /// </summary>
        [Task]
        public void MarkNodeAsSearched()
        {
            _nodeSearchedSignal.Fire(
                (GuardStateBrainPBT)_stateBrain,
                (SearchTrackNode)_searchPoint
            );

            Task.current.Complete(true);
        }

        /// <summary>
        /// Triggers the GuardReachedRoomSignal to notify any intersted listeners that it has reached a room.
        /// </summary>
        [Task]
        public void ReportReachedRoom()
        {
            _guardReachedRoomSignal.Fire((GuardStateBrainPBT)_stateBrain);
            Task.current.Complete(true);
        }

        /// <summary>
        /// Fires the GuardReportEnemyPresenceSignal to notify any interested listeners that the enemy has been spotted.
        /// </summary>
        [Task]
        public void ReportEnemyPresence()
        {
            Room myRoom =
                    _levelLoadingManager.RoomGraphManager.GetRoomContainingObject(gameObject);

            _guardReportEnemyPresenceSignal.Fire(
                _roomOfInterest,
                myRoom,
                CoverPoint,
                _stateBrain as GuardStateBrainPBT
            );

            Task.current.Complete(true);
        }

        /// <summary>
        /// Overrides the inherited method. Runs for a second at a time as long as the player is not visible.
        /// When the player is visible it resets the need once per second.
        /// </summary>
        /// <returns></returns>
        protected override IEnumerator ResetPathFindNeed()
        {

            yield return new WaitForSeconds(1f);
            if (_movementGoal == _player && !PlayerIsVisible )
            {
                StartCoroutine(ResetPathFindNeed());
            }
            else
            {
                _needResetPathFind = true;
            }

        }

        



        /// <summary>
        /// Gets a shooting position from the GuardManager to ensure that is does not stand on the same square as
        /// another guard.
        /// </summary>
        [Task]
        public void GetShootingPosition()
        {
            _initialShootingPosition = _parentObject.transform.position;
            _hasSteppedAside = false;
            _shootingPosition = _guardManager.GetShootingPosition(this, _movementCoordinator.SerializedDir);
            _initialSerializedDir = _movementCoordinator.SerializedDir;
            _initialMovementDirection = _movementCoordinator.DirectionVector;
            _initialPerpendicularDirection = _movementCoordinator.PerpendicularDirectionVector;
            Task.current.Complete(true);
        }


        /// <summary>
        /// Maintains the shooting position that was given by the GuardManager
        /// </summary>
        [Task]
        public void KeepShootingPosition()
        {
            float distanceToPlayer = _geometryUtilities.DistanceBetweenObjects(_parentObject, _player.gameObject);

            if (distanceToPlayer > (3 + _shootingPosition * 2))
            {
                bool res = _movementController.StepInDirection(_geometryUtilities.DegreeToDirectionVector(_movementCoordinator.SerializedDir * 90));
            }
            else
            {
                Task.current.Complete(true);
            }

        }

        #endregion

        // [SerializeField]
        // List<Vector2> _blockedCoordinates;

        private void Update()
        {
            // _blockedCoordinates = GetBlockedCoordinates ();
            if (_target != null &&
                            _visionCone.ObjectIsVisible(_target.gameObject))
            {
                _roomOfInterest = _levelLoadingManager.RoomGraphManager.GetRoomContainingObject
                                                        (_target.gameObject);
            }
        }

        #region LOCKER_SEARCH
        /// <summary>
        /// Assign the current available, unsearched locker for the guard to check
        /// </summary>
        [Task]
        public void GetCurrentLocker()
        {
            if (_lockerManager.Lockers.Count == 0)
            {
                _currentLocker = null;
                _lockerManager.CurrentLocker = null;
                searchingLocker = false;
                return;
            }
            _currentLocker = _lockerManager.Lockers[0];
            _lockerManager.CurrentLocker = _lockerManager.Lockers[0];
            _lockerManager.NumerSearchingLockers++;
            _lockerManager.Lockers.RemoveAt(0);
        }

        /// <summary>
        /// Search the lockers when the guard is close to and open doors
        /// </summary>
        [Task]
        public void SearchLocker()
        {
            //if (searchingLocker || _currentLocker == null)
                //return;

            StartCoroutine(SearchPlayerInLocker(_currentLocker));
        }

        /// <summary>
        /// Initialize the searching locker for guards
        /// Using InitiateLockerSearching to prevent re-initialize
        /// </summary>
        [Task]
        public void InitiateLockersSearch()
        {
            if (!_lockerManager.InitiateLockerSearching)
            {

                _lockerManager.InitiateLockerSearching = true;
                _lockerManager.Lockers.Clear();
                DoorController[] lockers = GameObject.FindObjectsOfType<DoorController>();
                foreach (DoorController locker in lockers)
                    if (Vector2.Distance(locker.transform.position, transform.position) < searchArea)
                        _lockerManager.Lockers.Add(locker);
            }
            GetCurrentLocker();
        }

        IEnumerator SearchPlayerInLocker(DoorController locker)
        {
            searchingLocker = true;
            locker.OpenLocker(_player);
            yield return new WaitForSeconds(2);
            searchingLocker = false;
            locker.CloseLocker(_player);
            _lockerManager.NumerSearchingLockers--;
            _currentLocker = null;
            CancelSearching();
        }

        /// <summary>
        /// Cancel the searching when every guard has finished their locker search
        /// </summary>
        [Task]
        public void CancelSearching()
        {
            if ((!_lockerManager.InitiateLockerSearching || _lockerManager.NumerSearchingLockers > 0) && !_player.Recognized)
                return;
            _lockerManager.InitiateLockerSearching = false;
            _lockerManager.NumerSearchingLockers = 0;
            _lockerManager.Lockers.Clear();
        }
        #endregion

    }
}