using UnityEngine;
using System.Collections.Generic;
using System;
using Zenject;
using StealthGame.RoomClassification;

namespace StealthGame.AI
{
    public delegate void SearchTrackCompleteEvent(
        SearchTrackManager searchTrackManager
    );
    public class SearchTrackManager
    {

        private bool _isSearching;

        public event SearchTrackCompleteEvent OnSearchTrackComplete;

        private List<GuardStateBrainPBT> _searchingGuards,
            _coveringGuards;

        private SearchTrack _searchTrack;

        [Inject]
        private GuardManager _guardManager;

        GuardReachedRoomSignal _guardReachedRoomSignal;

        NodeSearchedSignal _nodeSearchedSignal;

        SearchTrackSearchedSignal _searchTrackSearchedSignal;

        Dictionary<SearchTrackNode, GuardStateBrainPBT> _guardsPerNode;

        private List<SearchTrackNode> _nodesToSearch;

        private int _nodesToSearchIndex;

        private List<SearchTrackNode> _nodesInSearching;

        private List<GuardStateBrainPBT> _allGuards;

        private int _assignedExitIndex;

        private int _coveredExits;

        private bool _searchingInProgress;


        [Inject]
        public SearchTrackManager(
            SearchTrack searchTrack,
            List<GuardStateBrainPBT> guards,
            NodeSearchedSignal nodeSearchedSignal,
            GuardReachedRoomSignal guardReachedRoomSignal,
            SearchTrackSearchedSignal searchTrackSearchedSignal,
            List<SearchTrackNode> likelyNodes
        )
        {
            _nodesToSearchIndex = 0;
            _coveringGuards = new List<GuardStateBrainPBT>();
            _searchingGuards = new List<GuardStateBrainPBT>();
            _allGuards = new List<GuardStateBrainPBT>();

            _nodeSearchedSignal = nodeSearchedSignal;
            _guardReachedRoomSignal = guardReachedRoomSignal;

            _searchTrack = searchTrack;

            _assignedExitIndex = 0;
            _coveredExits = 0;

            SetUpDelegates();
            // CheckIfReady();


            _searchTrackSearchedSignal = searchTrackSearchedSignal;

            _guardsPerNode = new Dictionary<SearchTrackNode, GuardStateBrainPBT>();

            if( likelyNodes != null )
            {
                _nodesToSearch = likelyNodes;
            }
            else
            {
                _nodesToSearch = searchTrack.GetSearchTrackNodes ();
            }

            if (searchTrack.GetSearchTrackNodes().Count == 0)
            {
                // _searchTrackSearchedSignal.Fire(searchTrack);
                RegisterComplete ();
            }
            else
            {
                Init();
                AddGuards(guards);
            }

        }

        

        private void Init()
        {

            _nodeSearchedSignal += OnNodeSearched;
            _guardReachedRoomSignal += OnGuardReachRoom;

        }


        private void OnGuardReachRoom( GuardStateBrainPBT guard, Room room, RoomConnection roomConnection )
        {
            
            if (_allGuards.Contains(guard))
            {
                guard.SetRoom(null);
                if (_nodesToSearch.Count != 0)
                {
                    guard.SearchPoint = GetNextSearchPoint ();
                }
            }

        }

        private SearchTrackNode GetNextSearchPoint ()
        {

            if( _nodesToSearch.Count == 0 )
            {
                return null;
            }

            _nodesToSearchIndex = (_nodesToSearchIndex+1) % _nodesToSearch.Count;

            return _nodesToSearch[_nodesToSearchIndex];

        }

        private void RemoveNode( SearchTrackNode node )
        {
            
            int nodeIndex = _nodesToSearch.IndexOf( node );
            
            if( nodeIndex < _nodesToSearchIndex )
            {
                _nodesToSearchIndex--;
            }

            _nodesToSearch.Remove( node );

        }

        private void OnNodeSearched(GuardStateBrainPBT guard, SearchTrackNode node)
        {
            _nodesToSearch.Remove( node );
            // use the list stored in the actual search track since we pop things from our local copy
            if (!_searchTrack.GetSearchTrackNodes().Contains(node))
            {
                return;
            }

            if (_nodesToSearch.Count != 0)
            {
                // guard.SearchPoint = _nodesToSearch.Pop();
                guard.SearchPoint = GetNextSearchPoint ();
            }
            else
            {
                guard.SearchPoint = null;
                RegisterComplete();
            }

        }

        public SearchTrack SearchTrack
        {
            get
            {
                return _searchTrack;
            }
        }

        public List<GuardStateBrainPBT> AllGuards
        {
            get
            {
                return _allGuards;
            }
        }



        public void AddGuard(GuardStateBrainPBT guard, bool checkIfReady = true)
        {

            if (
                _coveringGuards.Count < _searchTrack.Exits.Count
               && _searchTrack.RequriesCover
              )
            {
                AddCoveringGuard(guard);
            }
            else
            {
                AddSearchingGuard(guard);
            }

            _allGuards.Add(guard);

            if (checkIfReady)
            {
                CheckIfReady();
            }


        }

        public void AddGuards(List<GuardStateBrainPBT> guards)
        {

            foreach (var guard in guards)
            {
                AddGuard(guard, false);
            }
            CheckIfReady();
            // Debug.Log("Added guards and now have " + _allGuards.Count);
        }

        public void AddSearchingGuard(GuardStateBrainPBT guard)
        {
            if (_searchingInProgress)
                guard.SearchPoint = GetNextSearchPoint();

            GenericUtilities.SafelyAddItemToList(guard, _searchingGuards);
            // if (_searchTrack.Exits.Count > 0)
            // {
            //     guard.GetComponent<SearchModeBehaviour>().InitSearch(
            //         this, _searchTrack.Exits[0]
            // );
            // }


        }



        private void AddCoveringGuard(GuardStateBrainPBT guard)
        {

            GenericUtilities.SafelyAddItemToList(guard, _coveringGuards);

            // old implementation reliant on searchmodebehaviour
            // guard.GetComponent<SearchModeBehaviour>().InitSearch(
            //     this, _searchTrack.Exits[_assignedExitIndex]
            // );
            _assignedExitIndex++;

        }

        private void CheckIfReady()
        {

            bool sufficientForSearch = _searchingGuards.Count >=
                                       _searchTrack.AmountOfGuardsForSearching;
            bool allExitsAreCovered = true;

            if (_searchTrack.RequriesCover)
            {
                allExitsAreCovered = _coveredExits >= _searchTrack.Exits.Count;
            }

            if (sufficientForSearch && allExitsAreCovered)
            {
                PerformSearch();
            }

        }

        private void SetUpDelegates()
        {

            _searchingGuards.ForEach((GuardStateBrainPBT guard) =>
            {
                guard.HealthController.OnDeath +=
                                        OnSearchingGuardDeath;

            });

            _coveringGuards.ForEach((GuardStateBrainPBT guard) =>
            {
                guard.HealthController.OnDeath +=
                                            OnCoveringGuardDeath;

            });

        }




        private void OnSearchingGuardDeath(HealthController healthController)
        {
            // set something in motion where if it's just one, wait a while
            // random 3-7 seconds before they ask where he is, then go in
        }

        private void OnCoveringGuardDeath(HealthController healthController)
        {

            // should we care about this one in the gameplay?

        }

        public void RegisterCoveredExit(GuardStateBrainPBT guard)
        {


            if (_coveringGuards.IndexOf(guard) != -1)
            {
                _coveredExits++;
                CheckIfReady();

            }



        }

        private void PerformSearch()
        {
            
            foreach (var guard in _allGuards)
            {
                guard.SearchTrack = SearchTrack;
                guard.SetRoom(_searchTrack.Room);
            }

            _searchingInProgress = true;
        }




        private void RegisterComplete()
        {
            foreach (var guard in _allGuards)
            {
                guard.SetRoom(null);
                guard.SearchPoint = null;
            }
            _searchTrackSearchedSignal.Fire(_searchTrack);
            // OnSearchTrackComplete?.Invoke(this);

        }


        public class Factory :
            Factory
                <SearchTrack, List<GuardStateBrainPBT>, List<SearchTrackNode>, SearchTrackManager>
        { }

        public class SearchConfig
        {

            public int searchDirection { get; private set; }
            public Vector2 searchStartPosition { get; private set; }

            public SearchConfig(int dir, Vector2 pos)
            {
                searchDirection = dir;
                searchStartPosition = pos;
            }

        }


    }



}