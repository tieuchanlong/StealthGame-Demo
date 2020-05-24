using System;
using UnityEngine;
using System.Collections.Generic;
using Zenject;
using StealthGame.MapLoadingLayer;

namespace StealthGame.AI
{
    public class SquadManager
    {


        public delegate void SquadActionCompleteEvent();
        public event SquadActionCompleteEvent OnSquadClearingComplete;

        // public event TargetEvent OnChangeTarget;

        /******************* FUNCTIONAL VARIABLES ************/

        private MonoBehaviour _target;

        private List<GuardStateBrainPBT> _guards;

        private List<SquadMember> _allSquadMembers;
        private List<SquadDroneBehaviour> _drones;
        private List<SquadDroneBehaviour> _leftPhalanx, _rightPhalanx;

        List<SquadDroneBehaviour>[] _phalanxes;

        SquadLeaderBehaviour _leader;

        private List<SearchTrack> _searchTracks;
        private int _currentSearchTrackIndex;

        [Inject]
        private SearchTrackManager.Factory _searchTrackManagerFactory;
        private SearchTrackManager _searchTrackManager;

        [Inject]
        private GuardManager _guardManager;

        /*******************************/


        public List<GuardStateBrainPBT> Guards
        {
            get
            {
                return _guards;
            }
        }

        //[Inject]
        //public void Construct(
        //	SearchTrackManager.Factory searchTrackManagerFactory,
        //	GuardManager guardManager
        //) 
        //{
        //	_guardManager = guardManager;
        //	_searchTrackManagerFactory = searchTrackManagerFactory;	
        //}

        public SquadManager(
            List<GuardStateBrainPBT> guards,
            SearchTrackManager.Factory searchTrackManagerFactory,
            GuardManager guardManager
        )
        {
            _guards = guards.Copy();
            _searchTrackManagerFactory = searchTrackManagerFactory;
            _guardManager = guardManager;

            _guards.ForEach(
                (guard) => _guardManager.RegisterNonPatrolRouteGuard(guard)
            );

            _drones = new List<SquadDroneBehaviour>();
            _leftPhalanx = new List<SquadDroneBehaviour>();
            _rightPhalanx = new List<SquadDroneBehaviour>();

            _phalanxes = new List<SquadDroneBehaviour>[]
            {
                _leftPhalanx,
                _rightPhalanx
            };

            _leader = guards.Pop().SquadLeaderBehaviour;
            _leader.gameObject.SetActive(true);
            _leader.Init(this);


            _allSquadMembers = new List<SquadMember>()
                            { (SquadMember) _leader };

            AddDrones(
                guards.Map((guard) => guard.SquadDroneBehaviour)
            );

            SetupListeners();

        }


        public SquadManager(List<GuardStateBrainPBT> guards,
                         SearchTrackManager.Factory searchTrackManagerFactory,
                         GuardManager guardManager,
                         List<SearchTrack> searchTracks) : this(guards,
                                                    searchTrackManagerFactory,
                                                    guardManager
                                                   )
        {
            _searchTracks = searchTracks;
            _currentSearchTrackIndex = 0;
            TakeNextSearchTrack();
            SetupListeners();

        }


        public SquadLeaderBehaviour Leader
        {
            get
            {
                return _leader;
            }
        }

        private void SetupListeners()
        {

            foreach (var guard in _guards)
            {
                guard.OnChangeTarget += OnGuardGetsTarget;
            }

        }

        private void OnGuardGetsTarget(MonoBehaviour target)
        {
            _target = target;
            foreach (var guard in _guards)
            {
                if (guard.Target != target)
                {
                    guard.SetTarget(target);
                }
            }
        }



        private void AddDrones(List<SquadDroneBehaviour> drones)
        {

            // List<SquadMember> newDrones = Util.GetUniqueElementsInList(
            // 	(List<SquadMember>) drones,
            // 	_allSquadMembers
            // );

            List<SquadDroneBehaviour> newDrones = drones.Filter((drone) =>
           {
               return !_allSquadMembers.Contains(drone);
           });

            int currentPhalanxIndex = 0;


            _drones.AddRange(drones);
            _allSquadMembers.AddRange(drones);



            if (_rightPhalanx.Count < _leftPhalanx.Count)
            {
                currentPhalanxIndex = 1;
            }

            foreach (var guard in newDrones)
            {
                guard.gameObject.SetActive(true);
                _phalanxes[currentPhalanxIndex].Add(
                    guard.GetComponent<SquadDroneBehaviour>()
                );

                if (currentPhalanxIndex == 1)
                {
                    currentPhalanxIndex = 0;
                }
                else
                {
                    currentPhalanxIndex = 1;
                }

            }

            AdjustPhalanxes();
        }

        private void AdjustPhalanxes()
        {


            if (_allSquadMembers.Count == 2)
            {
                SquadDroneBehaviour followGuideBehaviour = _leftPhalanx[0]
                                    .GetComponent<SquadDroneBehaviour>();

                followGuideBehaviour.followMode =
                                        SquadDroneBehaviour.FollowMode.LINE;

                followGuideBehaviour.SetGuide(_leader);

            }
            else
            {

                for (int i = 0; i < _allSquadMembers.Count / 2; i++)
                {
                    for (int j = 0; j < _phalanxes.Length; j++)
                    {
                        var phalanx = _phalanxes[j];
                        if (i < phalanx.Count)
                        {
                            SquadDroneBehaviour followGuideBehaviour =
                                                        phalanx[i];

                            if (i == 0)
                            {
                                followGuideBehaviour.SetGuide(_leader);
                                followGuideBehaviour.FollowingLeader = true;
                            }
                            else
                            {
                                followGuideBehaviour.SetGuide((SquadMember)phalanx[i - 1]);
                            }

                            followGuideBehaviour.followMode =
                                        (SquadDroneBehaviour.FollowMode)j;



                        }

                    }

                }


            }



        }

        public void LeaderPositionReached()
        {
            SearchTrack searchTrack = _searchTracks[_currentSearchTrackIndex];
            _searchTrackManager = _searchTrackManagerFactory.Create(
                searchTrack,
                _allSquadMembers.Map(
                    (guard) => guard.GetComponent<GuardStateBrainPBT>()
                ),
                null
            );

            _searchTrackManager.OnSearchTrackComplete += OnSearchTrackComplete;

        }

        public void OnSearchTrackComplete(
            SearchTrackManager searchTrackManager)
        {

            searchTrackManager.OnSearchTrackComplete -= OnSearchTrackComplete;

            foreach (var guard in _allSquadMembers)
            {
                // old implementation reliant on searchmode behaviours
                // neds to be remade
                // guard.GetComponent<SearchModeBehaviour>()
                //                 .EndSearchParticipation();
            }

            _currentSearchTrackIndex++;

            if (_currentSearchTrackIndex >= _searchTracks.Count)
            {


                ExitPoint exit = UnityEngine.Object
                                            .FindObjectOfType<ExitPoint>();

                // todo: move logic for initexit etc. to SquadMember
                // _allSquadMembers.ForEach(
                // 	(squadBehaviour) => squadBehaviour.InitExit(exit)
                // );
                OnSquadClearingComplete?.Invoke();

            }
            else
            {
                TakeNextSearchTrack();
            }

        }

        public void TakeNextSearchTrack()
        {



            SearchTrack searchTrack = _searchTracks[_currentSearchTrackIndex];

            _leader.GetComponent<SquadLeaderBehaviour>()._strategicPoint =
                                                        (StrategicPoint) searchTrack.Exits[0];


        }


        public class Factory : IFactory<int, GuardManager, SquadManager>
        {


            private DiContainer _diContainer;

            private GuardFactory _guardFactory;
            private SearchTrackManager.Factory _searchTrackManagerFactory;


            public Factory(
                DiContainer diContainer,
                GuardFactory guardFactory,
                SearchTrackManager.Factory searchTrackManagerFactory

            )
            {
                _diContainer = diContainer;
                _guardFactory = guardFactory;

                _searchTrackManagerFactory = searchTrackManagerFactory;

            }


            private List<GuardStateBrainPBT> GetGuards(int guardCount)
            {
                List<GuardStateBrainPBT> guards =
                                new List<GuardStateBrainPBT>();

                Vector3 pos = UnityEngine.Object
                                .FindObjectsOfType<ExitPoint>()[0]
                                .transform.position;

                for (int i = 0; i < guardCount; i++)
                {

                    guards.Add(_guardFactory.Create(pos));

                }

                return guards;

            }

            public SquadManager Create(int guardCount,
                                       GuardManager guardManager)
            {

                SquadManager squadManager =
                    new SquadManager(GetGuards(guardCount),
                                     _searchTrackManagerFactory, guardManager);

                _diContainer.Inject(squadManager);

                return squadManager;

            }


            public SquadManager Create(int guardCount,
                                       List<SearchTrack> searchTracks,
                                       GuardManager guardManager)
            {

                SquadManager squadManager =
                    new SquadManager(GetGuards(guardCount),
                                     _searchTrackManagerFactory, guardManager,
                                     searchTracks);

                _diContainer.Inject(squadManager);

                return squadManager;

            }

        }



    }

}