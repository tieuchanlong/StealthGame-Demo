using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace StealthGame.Actors.Movement
{
    public class FootStepController : MonoBehaviour {

        private AudioSource _audioSource;

        [SerializeField]
        private List<AudioClip> _audioClipsLeftFoot, _audioClipsRightFoot, puddleClipLeftFoot, puddleClipsRightFoot;

        private Vector3 _previousPosition, _positionAtPreviousStep;


        public enum WalkState { WALKING, SNEAKING, RUNNING };



        [SerializeField]
        private MovementCoordinator _movementCoordinator;

        private bool _moving;

        [SerializeField]
        private float _distanceToStep = 1f;

        public bool _audibleFootsteps = true;

        protected WalkState _walkState;

        public WalkState CurrentWalkState {

            get {
                return _walkState;
            }
            set {
                _walkState = value;
            }

        }


        private int _indexLeft, _indexRight, puddleLeft, puddleRight;
        private bool _wet = false;
        private bool exitPuddle = false;

        private GameObject Player;
        [SerializeField] private float soundLast = 10f; // how long will the sound last
        [SerializeField] private float delaySound = 1f;
        private float delayCount = 0f;
        public float soundTime = 0f; //count up the sound length
        [SerializeField] private int maxRange = 5;

        private FootStepFactory _footStepFactory;

        [Inject]
        public void Construct(FootStepFactory footstepfactory)
        {
            _footStepFactory = footstepfactory;
        }

        // Main program
        void Awake() {
            _indexLeft = 0;
            _indexRight = 0;
            puddleLeft = 0;
            puddleRight = 0;
            _moving = false;
            _previousPosition = transform.position;
        }

        private void OnEnable()
        {

            _movementCoordinator.OnChangeMovementState += OnChangeMovementState;

        }


        private void OnDisable()
        {

            _movementCoordinator.OnChangeMovementState -= OnChangeMovementState;

        }

        private void OnChangeMovementState ( bool movementState )
        {
            _moving = movementState;
            if( !_moving )
            {
                // we were not moving in the previous check
                _positionAtPreviousStep = transform.position;
                _indexLeft = 1;
                _indexRight = 0;
                puddleLeft = 1;
                puddleRight = 0;
                PlayNextFootStep ();
            }

        }

        // Update is called once per frame
        void Update () {
            
            if( _moving ) {

                // if( !_moving ) {
                //     // we were not moving in the previous check
                //     _positionAtPreviousStep = transform.position;
                //     _indexLeft = 1;
                //     _indexRight = 0;
                // }

                float posDiff =
                    ( transform.position - _positionAtPreviousStep ).magnitude;

                if( posDiff >= _distanceToStep || posDiff == 0 ) {

                    PlayNextFootStep ();
                    _positionAtPreviousStep = transform.position;

                }

                _previousPosition = transform.position;
                // _moving = true;

            }

            UpdatePuddleSound();

        }

        /// <summary>
        /// Check and update the Puddle Sound timers
        /// </summary>
        private void UpdatePuddleSound()
        {

            if (_wet)
            {
                delayCount += Time.deltaTime;
                soundTime += Time.deltaTime;

                if (delayCount >= delaySound)
                    delayCount = 0;

                if (soundTime >= soundLast)
                {
                    _wet = false;
                    delayCount = 0;
                    soundTime = 0;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {

            Debug.Log("ENTER PUDDLE");
            // _standingInPuddle = true;
            // _wet = true;
            if (!_wet && other.tag == "Puddle")
            {
                _wet = true;
                exitPuddle = false;
            }

        }

        private void  OnTriggerExit2D( Collider2D other )
        {

            // _standingInPuddle = true;
            // StartCoroutine( StopWetness() );

            // Reset not using the puddle sound anymore
            if (other.tag == "Puddle")
            {
                //_wet = false;
                exitPuddle = true;
                puddleLeft = 1;
                puddleRight = 0;
            }
        }

        private IEnumerator StopWetness ()
        {
            yield return null;
            // yield return new WaitForSeconds(2f);
            // _wet = false;
        }

        private void PlayNextFootStep ()
        {
            // pick a sound effect
            AudioClip audioClip;

            List<AudioClip> clipsToUseRight = _wet ? puddleClipsRightFoot : _audioClipsRightFoot;
            List<AudioClip> clipsToUseLeft = _wet ? puddleClipLeftFoot : _audioClipsLeftFoot;

            if (_indexLeft > _indexRight)
            {
                // Create normal sound
                audioClip = clipsToUseRight
                                [_indexLeft % clipsToUseRight.Count];

                _indexRight++;

            }
            else
            {
                audioClip = clipsToUseLeft
                            [_indexRight % clipsToUseLeft.Count];

                _indexLeft++;

            }


            float volume = 0.4f;
            int range = 4;
            if( _wet )
            {
                range = 12;
                volume = 1f;
            }
            else if( CurrentWalkState == WalkState.RUNNING )
            {
                volume = 1f;
                range = 12;
            }
            else if( CurrentWalkState == WalkState.SNEAKING )
            {
                volume = 0.2f;
                range = 0;
            }

            if (_audibleFootsteps)
            {
                if (_wet && exitPuddle)
                {
                    // Create footstep
                    _footStepFactory.Create(transform.position);
                }

                AudibleSound.GenerateAudibleSound(
                    transform.position,
                    range,
                    AudibleSound.SoundType.PlayerFootstep,
                    audioClip,
                    this,
                    volume,
                    true

                );
            }
        }
    }
}