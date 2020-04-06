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



namespace StealthGame.Environment
{
    public partial class DoorManager : MonoBehaviour
    {
        
        private TilemapManager _tilemapManager;

        private MapDataObject _mapDataObject;

        private NewMapObjectSignal _newMapObjectSignal;

        private MapStartSignal _mapStartSignal;


        #region TILE_MAP_DECLARATIONS
        private Tilemap WallLayer { get => _mapDataObject.WallLayer; }
        private Tilemap CeilingLayer { get => _mapDataObject.CeilingLayer; }
        private Tilemap CeilingDetailsLayer { get => _mapDataObject.CeilingDetailsLayer; }
        private Tilemap FloorLayer { get => _mapDataObject.FloorLayer; }

        private BoundsInt _mapBounds;

        private BoundsInt _wallBounds;
        private TileBase[] _wallTiles;

        private BoundsInt _ceilingBounds;
        private TileBase[] _ceilingTiles;

        private BoundsInt _ceilingDetailBounds;
        private TileBase[] _ceilingDetailTiles;

        private BoundsInt _floorBounds;
        private TileBase[] _floorTiles;

        #endregion

        [Inject]
        public void Construct ( TilemapManager tilemapManager, NewMapObjectSignal newMapObjectSignal,
                                MapStartSignal mapStartSignal )
        {
            _tilemapManager = tilemapManager;
            _newMapObjectSignal = newMapObjectSignal;
            _mapStartSignal = mapStartSignal;
        }

        private void OnEnable()
        {
            _newMapObjectSignal += OnNewMapObject;
            _mapStartSignal += OnMapStart;    
        }

        private void OnDisable()
        {
            _newMapObjectSignal -= OnNewMapObject;
            _mapStartSignal -= OnMapStart;    
        }

        private void OnNewMapObject ( MapDataObject mapObject)
        {
            _mapDataObject = mapObject;
        }

        /// <summary>
        /// Changes the size of a tilemap's bounds to match those of the entire map.
        /// If there is a difference in position, an offset is utilized.
        /// </summary>
        /// <param name="tilemap"></param>
        private void ResizeTilemap ( Tilemap tilemap )
        {
            TileBase[] tiles = tilemap.GetTilesBlock( tilemap.cellBounds );
            TileBase[] newTiles = new TileBase[  _mapBounds.size.x * _mapBounds.size.y ];
            Vector3Int offset = tilemap.cellBounds.position - _mapBounds.position;
            Debug.Log("offset: " + offset);
            for( int x = 0; x < _mapBounds.size.x; x++ )
            {
                for( int y = 0; y < _mapBounds.size.y; y++ )
                {
                    
                    int index1 = x + y*tilemap.cellBounds.size.x;
                    int index2 = x+offset.x + (y+offset.y)*_mapBounds.size.x;

                    if( index1 < tiles.Length )
                    {
                        TileBase tile = tiles[index1];
                        newTiles[index2] = tile;
                    }

                }
            }

            tilemap.SetTilesBlock( _mapBounds, newTiles );

        }

        private void OnMapStart ()
        {
            _mapBounds = new BoundsInt( new Vector3Int( 0, -1*_tilemapManager.MapHeight, 0 ),
                                        new Vector3Int( _tilemapManager.MapWidth, _tilemapManager.MapHeight, 1 ) );


            Tilemap [] tilemaps = new Tilemap[] { _mapDataObject.WallLayer, _mapDataObject.CeilingLayer,
                                                _mapDataObject.CeilingDetailsLayer, _mapDataObject.FloorLayer };


            foreach( var tilemap in tilemaps )
            {
                ResizeTilemap( tilemap );
            }

            _wallBounds = _mapDataObject.WallLayer.cellBounds;
            _wallTiles = _mapDataObject.WallLayer.GetTilesBlock( _mapBounds );

            _ceilingBounds = _mapDataObject.CeilingLayer.cellBounds;
            _ceilingTiles = _mapDataObject.CeilingLayer.GetTilesBlock(_mapBounds);

            _ceilingDetailBounds = _mapDataObject.CeilingDetailsLayer.cellBounds;
            _ceilingDetailTiles = _mapDataObject.CeilingDetailsLayer.GetTilesBlock( _mapBounds );

            _floorBounds = FloorLayer.cellBounds;
            _floorTiles = FloorLayer.GetTilesBlock( _mapBounds );
            
            foreach ( var door in DoorController.GetInstances() )
            {
                if( door.FixObstaclesBehind )
                {
                    ClearAroundDoor ( door );
                }
            }

            WallLayer.SetTilesBlock(_wallBounds, _wallTiles);
            CeilingLayer.SetTilesBlock(_ceilingBounds, _ceilingTiles);
            CeilingDetailsLayer.SetTilesBlock(_ceilingDetailBounds, _ceilingDetailTiles);
            FloorLayer.SetTilesBlock(_floorBounds, _floorTiles);

        }


        /// <summary>
        /// "Clears" the area around a door by taking all wall tiles above it
        /// and the closest ceiling tile above it and putting them on the
        /// CeilingDetails layer and, if the door requires it, adding
        /// the floor tiles from below it to underneath it
        /// </summary>
        /// <param name="door"></param>
        private void ClearAroundDoor ( DoorController door )
        {

            Vector3Int tilePos = ((Vector3)_tilemapManager.WorldPosToTilePos( door.transform.position )).ToInt();
            tilePos.y = _tilemapManager.MapHeight - tilePos.y;
            


            List <Vector3Int> topLocations = new List<Vector3Int> ()
            {
                tilePos,
                tilePos + Vector3Int.left
            };

            List<Vector3Int> bottomLocations = new List<Vector3Int> ()
            {
                tilePos + Vector3Int.down,
                tilePos + Vector3Int.down + Vector3Int.left
            };

            // all locatoins that need to be cleared
            List<Vector3Int> clearedLocations = topLocations.Concat(bottomLocations);



            // add floor tiles and remove wall tiles
            foreach( var location in clearedLocations )
            {

                if( door.AddFloorTilesWhenFixingObstacles )
                {
                    int floorIndex1 = location.x + location.y * _floorBounds.size.x;
                    int floorIndex2 = location.x + (location.y-2) * _floorBounds.size.x;

                    BoundsInt b = new BoundsInt(location, new Vector3Int(1,1,1) );

                    _floorTiles[floorIndex1] = _floorTiles[floorIndex2];
                    // FloorLayer.SetTilesBlock( b, new TileBase[]{floorTiles[floorIndex2]});

                }
                
                int indexWall = location.x + location.y * _wallBounds.size.x;
                _wallTiles[indexWall] = null;
            }
            
            // move walltiles and ceiling tiles to ceiling details layer 
            foreach( var location in topLocations )
            {
                int x = location.x;
                for( int y = location.y+1; y < _tilemapManager.MapHeight; y++ )
                {

                    int ceilingIndex = x + y * _ceilingBounds.size.x;
                    int wallIndex = x + y * _wallBounds.size.x;
                    int ceilingDetailsIndex = x+y*_ceilingDetailBounds.size.x;


                    TileBase ceilingTile = _ceilingTiles[ceilingIndex];
                    TileBase wallTile = _wallTiles[wallIndex];

                    if( ceilingTile != null && wallTile == null )
                    {
                        _ceilingDetailTiles[ceilingDetailsIndex] = ceilingTile;
                        _ceilingTiles[ceilingIndex] = null;
                        break;
                    }
                    else if( ceilingTile == null && wallTile == null )
                    {
                        break;
                    }
                    else if( ceilingTile == null && wallTile != null )
                    {
                        _ceilingDetailTiles[ceilingDetailsIndex] = wallTile;
                        _wallTiles[wallIndex] = null;
                    }

                }

                
            
            }


        }


        public int GetDistanceToCeilingLayer ( Vector3Int tilePos )
        {
            int x = tilePos.x;
            int distance = 0;
            
            for( int y = tilePos.y; y < _tilemapManager.MapHeight; y ++ )
            {
                int index = x + y*_ceilingBounds.size.x;
                if( _ceilingTiles[index] != null )
                {
                    return distance;
                }
                distance++;

            }

            return -1;
        
        }


    }


}