using UnityEngine;
using Zenject;
using StealthGame.RoomClassification;
using System.Collections.Generic;

namespace StealthGame.AI
{

    public class SearchRoomFinder
    {


        private List<Room> _rooms;

        private List<Room> _roomsToInvestigate;

        private List<RoomConnection> _connectionsToInvestigate;

        private List<Room> _investigatedRooms;

        private Room _startingRoom;

        private List<RoomConnection> _blockedConnections;

        private List <RoomConnection> _likelyEscapeRoutes;

        private Vector2 _playerLastKnownPosition;

        public SearchRoomFinder ( Room startingRoom, List<RoomConnection> blockedConnections, Vector2 playerLastKnownPosition )
        {
            _startingRoom = startingRoom;
            _blockedConnections = blockedConnections;
            _roomsToInvestigate = new List<Room> () {startingRoom};
            _rooms = new List<Room> ();
            _connectionsToInvestigate = new List<RoomConnection> ();
            _investigatedRooms = new List<Room> ();
            _playerLastKnownPosition = playerLastKnownPosition;
        }

        public List<Room> FindRooms ()
        {

            
            while( true )
            {
                Room _currentRoom = _roomsToInvestigate.Pop();
                List<Room> potentialRooms = new List<Room> ();

                Dictionary<Room, List<RoomConnection>> connectionDictionary =
                                                                _currentRoom.connectionsByRoom;

                foreach( KeyValuePair <Room, List<RoomConnection>> kvp in connectionDictionary )
                {
                    
                    if( _investigatedRooms.Contains(kvp.Key) )
                    {
                        continue;
                    }

                    List<RoomConnection> allowedConnections = GetAllowedConnections(kvp.Value);
                    if( allowedConnections.Count != 0  )
                    {
                        _rooms.Add( kvp.Key );
                        _roomsToInvestigate.Add(kvp.Key);
                        
                    }

                    _investigatedRooms.Add(_currentRoom);

                    
                }

                if( _roomsToInvestigate.Count == 0 )
                {
                    break;
                }

            }

            // clean up rooms
            List<Room> filteredRooms = new List<Room> ();
            foreach( var room in _rooms )
            {

                if( !room.IsConnector && !filteredRooms.Contains(room) )
                {
                    filteredRooms.Add(room);
                }

            }

            List<Room> sortedRooms = SortRooms( filteredRooms );

            string roomNames = "";
            foreach( var room in _rooms )
            {
                roomNames += room.gameObject.name +"; ";
            }
            UnityEngine.Debug.Log( "RoomNames: " + roomNames );

            return sortedRooms;

        }

        /// <summary>
        /// Attempts to sort the order in which to search rooms by first comparing the nodes on the
        /// Rooms' searchtracks to find the room closest to the player's last known position.
        /// After this, it has a first room and repeats a process where it uses search track nodes to find the closest room after that.
        /// </summary>
        /// <param name="rooms"></param>
        private List<Room> SortRooms ( List<Room> rooms )
        {

            List<Room> roomsToReturn = new List<Room> ();
            // get flat list of all searchtrack nodes
            List<SearchTrackNode> allNodes = new List<SearchTrackNode> ();
            foreach( var room in rooms )
            {
                allNodes.AddRange( room.SearchTrack.GetSearchTrackNodes() );
            }

            Vector3 searchPosition = _playerLastKnownPosition;

            // sort nodes on position to search position, pick the room with the closest one and
            // remove its nodes. repeat the process until the list of nodes is empty.
            while( true )
            {
                
                if( allNodes.Count == 0)
                {
                    break;
                }
                
                allNodes.Sort( (SearchTrackNode a, SearchTrackNode b) =>
                {
                    return (int) ( a.transform.position - searchPosition ).magnitude -
                            (int) ( b.transform.position - searchPosition ).magnitude;


                });
            

                Room room = allNodes[0].Track.Room;
                
                roomsToReturn.Add(room);

                searchPosition = allNodes[0].transform.position;

                foreach( var node in room.SearchTrack.GetSearchTrackNodes() )
                {
                    allNodes.Remove(node);
                }

                

            }

            return roomsToReturn;

            

            // get flat list of all RoomConnectionRepresentation
            // List<RoomConnectionRepresentation> allConnections = new List<RoomConnectionRepresentation> ();
            // foreach( var room in rooms )
            // {
            //     allConnections.AddRange( room.allConnections.Map( (RoomConnection rc ) => {return rc.graphicalRepresentation;} ) );
            // }

            

            // while( true )
            // {
            //     allConnections.Sort( (RoomConnectionRepresentation a, RoomConnectionRepresentation b) => {

            //         return (int) ( a.transform.position - searchPosition ).magnitude -
            //                 (int) ( b.transform.position - searchPosition ).magnitude;


            //     });
            //     Room room = allConnections[0].RoomConnection
            // }

            // sort this list based on proximity to the 
            
        }

        private Room GetOtherRoom( Room first, Room second, Room currentRoom )
        {
            if( first == currentRoom )
            {
                return second;
            }
            return first;
        }

        private List<RoomConnection> GetAllowedConnections ( List<RoomConnection> connections )
        {
            List<RoomConnection> allowedConnections = new List<RoomConnection> ();
            foreach( var rc in connections )
            {
                if( !_blockedConnections.Contains(rc) )
                {
                    allowedConnections.Add( rc );
                }
            }
            return allowedConnections;
        }

        private bool MayUseAnyConnection( List<RoomConnection> connections )
        {
            int blockedCounter = 0;
            foreach( var rc in connections )
            {
                if( _blockedConnections.Contains(rc) )
                {
                    blockedCounter++;
                }
            }
            return connections.Count != blockedCounter;
        }




    }

}