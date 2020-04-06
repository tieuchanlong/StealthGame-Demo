using Panda;
using StealthGame.Actors.Movement;
using StealthGame.MapLoadingLayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
namespace StealthGame
{
    public class FootStepFactory : IFactory<Vector3, GameObject>
    {
        private DiContainer _diContainer;
        private MovementCoordinator _playerMovementsCoordinator;
        private PlayerController _playerController;
        //private List<GameObject> footsteps;

        [Inject]
        PathEntity.Factory _pathEntityFactory;

        PathEntity _path;

        public PathEntity Path
        {
            get
            {
                return _path;
            }
        }

        private FootStep _latestFootstep;
        private FootStep _firstFootstep;

        private List<GameObject> _footSteps;

        private int _latestFootstepIndex = -1;

        public FootStep FirstFootStep { get => _firstFootstep; }
        public FootStep LatestFootStep { get => _latestFootstep; }

        [Inject]
        public FootStepFactory(DiContainer diContainer, PlayerController playerController)
        {
            _diContainer = diContainer;
            _playerController = playerController;
            _playerMovementsCoordinator = _playerController.gameObject.GetComponent<MovementCoordinator>();
            _footSteps = new List<GameObject>();
            //footsteps = new List<GameObject>();
        }

        public GameObject Create(Vector3 position)
        {

            Quaternion rot = new Quaternion(0, 0, 0, 0);

            Object footstepPrefab = Resources.Load("Prefabs/FootStep"); // Load footstep, need to change

            GameObject footstep = _diContainer.InstantiatePrefab(
                footstepPrefab,
                position,
                rot,
                null
            );

            footstep.name = "FootStep";

            _latestFootstep = footstep.GetComponent<FootStep>();

            // Create footstep
            _footSteps.Add(footstep);
            _firstFootstep = _footSteps[0].GetComponent<FootStep>();
            _path = GeneratePathFromTrail();
            //footsteps.Add(footstep);

            return footstep;

        }


        public PathEntity GeneratePathFromTrail()
        {
            List<Vector2> newPositions = new List<Vector2>();

            int i = 0;

            foreach (GameObject foot in _footSteps)
            {
                if (foot != null)
                {
                    //if (i == 0)
                        //return null;

                    newPositions.Add(foot.transform.position);
                }
                i++;
            }

            _latestFootstepIndex = newPositions.Count;

            return _pathEntityFactory.Create(newPositions);

        }
    }
}