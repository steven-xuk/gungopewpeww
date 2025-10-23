using System;
using NWH.DWP2.WaterData;
using NWH.Common.Utility;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    ///     Class for generating water particles based on simulation data.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class WaterParticleSystem : MonoBehaviour
    {
        /// <summary>
        ///     Should the particle system emit?
        /// </summary>
        [Tooltip("Should the particle system emit?")]
        public bool emit = true;

        /// <summary>
        ///     Render queue of the particle material.
        /// </summary>
        [Tooltip("Render queue of the water particle material.")]
        public int renderQueue = 2700;

        /// <summary>
        ///     Elevation above water at which the particles will spawn. Used to avoid clipping.
        /// </summary>
        [Tooltip("Elevation above water at which the particles will spawn. Used to avoid clipping.")]
        [Range(0f, 0.1f)] public float surfaceElevation = 0.016f;

        /// <summary>
        ///     Initial size of the particle.
        /// </summary>
        [Tooltip("Initial size of the particle.")]
        [Range(0f, 64f)] public float startSize = 4f;

        /// <summary>
        ///     Velocity object has to have to emit particles.
        /// </summary>
        [FormerlySerializedAs("sleepTresholdVelocity")]
        [Tooltip("Velocity object has to have to emit particles.")]
        [Range(0.1f, 5f)] public float sleepThresholdVelocity = 1.5f;

        /// <summary>
        ///     Determines how much velocity of the object will affect initial particle speed.
        /// </summary>
        [Tooltip("Determines how much velocity of the object will affect initial particle speed.")]
        [Range(0f, 5f)] public float initialVelocityModifier = 0.01f;

        /// <summary>
        ///     Limit initial alpha to this value.
        /// </summary>
        [Tooltip("Limit initial alpha to this value.")]
        [Range(0f, 1f)] public float maxInitialAlpha = 0.15f;

        /// <summary>
        ///     Multiplies initial alpha by this value. Alpha cannot be higher than maxInitialAlpha.
        /// </summary>
        [Tooltip("Multiplies initial alpha by this value. Alpha cannot be higher than maxInitialAlpha.")]
        [Range(0f, 10f)] public float initialAlphaModifier = 0.4f;

        /// <summary>
        ///     How many particles should be emitted each 'emitTimeInterval' seconds.
        /// </summary>
        [Tooltip("How many particles should be emitted each 'emitTimeInterval' seconds.")]
        [Range(0f, 20f)] public int emitPerCycle = 6;

        /// <summary>
        ///     Determines how often the particles will be emitted.
        /// </summary>
        [Tooltip("Determines how often the particles will be emitted.")]
        [Range(0f, 0.1f)] public float emitTimeInterval = 0.04f;

        /// <summary>
        ///     Script will try to predict where the object will be in the next n frames.
        /// </summary>
        [Tooltip("Script will try to predict where the object will be in the next n frames.")]
        public int positionExtrapolationFrames = 4;

        private float                      _timeElapsed;
        private WaterObject                _targetWaterObject;
        private ParticleSystem             _particleSystem;
        private int[]                      _waterlineIndices;
        private float                      _volumeOfMesh;
        private ParticleSystem.NoiseModule _noiseModule;
        private bool                       _initialized;
        private int                        _prevDataLength;
        private int                        _waterlineCount;
        private WaterObjectManager         _wom;

        public int TriDataLength
        {
            get { return _targetWaterObject.TriDataLength; }
        }

        public int TriDataStart
        {
            get { return _targetWaterObject.TriDataStart; }
        }

        public int TriDataEnd
        {
            get { return _targetWaterObject.TriDataEnd; }
        }

        public int VertDataLength
        {
            get { return _targetWaterObject.VertDataLength; }
        }

        public int VertDataStart
        {
            get { return _targetWaterObject.VertDataStart; }
        }

        public int VertDataEnd
        {
            get { return _targetWaterObject.VertDataEnd; }
        }
        

        private void Start()
        {
            _targetWaterObject = transform.GetComponentInParentsOrChildren<WaterObject>(true);
            if (_targetWaterObject == null)
            {
                Debug.LogError(
                    $"{name}: WaterParticleSystem requires WaterObject attached to the same object or one of parent objects to function.");
                return;
            }

            _particleSystem = GetComponent<ParticleSystem>();
            if (_particleSystem == null)
            {
                Debug.LogError("No ParticleSystem found.");
            }
            
            _wom = WaterObjectManager.Instance;
            Debug.Assert(_wom != null, "WaterObjectManager not found.");

            _particleSystem.GetComponent<Renderer>().material.renderQueue = renderQueue;
            _noiseModule                                                  = _particleSystem.noise;

            _initialized    = true;
            _prevDataLength = -999;
        }


        private void LateUpdate()
        {
            if (!_initialized || !_targetWaterObject.Initialized)
            {
                return;
            }

            if (!emit)
            {
                return;
            }

            if (_prevDataLength != TriDataLength && TriDataLength > 0)
            {
                _waterlineIndices = new int[TriDataLength];
            }

            if (_targetWaterObject.TargetRigidbody.linearVelocity.magnitude > sleepThresholdVelocity)
            {
                EmitNew();
            }

            _timeElapsed    += Time.deltaTime;
            _prevDataLength =  TriDataLength;
        }


        private void OnDestroy()
        {
            if (!Application.isPlaying)
            {
                DestroyImmediate(_particleSystem);
            }
        }


        private void EmitNew()
        {
            if (_targetWaterObject == null)
            {
                return;
            }

            if (emit && _timeElapsed >= emitTimeInterval && TriDataLength > 0f)
            {
                _timeElapsed = 0;

                int emitted = 0;

                // Emit allowed number of particles
                float elevation = 0;
                if (WaterDataProvider.Instance != null)
                {
                    elevation = WaterDataProvider.Instance.GetWaterHeight(Vector3.zero);
                }
                else
                {
                    Debug.LogWarning("Will not emit. WaterDataProvider is not present in the scene.");
                }

                _waterlineCount = 0;
                for (int i = TriDataStart; i < TriDataEnd; i++)
                {
                    if (_wom.States[i] != 1) continue;

                    _waterlineIndices[_waterlineCount] = i;
                    _waterlineCount++;
                }

                if (_waterlineCount == 0)
                {
                    return;
                }

                float noise = startSize > 1f ? Mathf.Sqrt(startSize) * 0.1f : startSize * 0.1f;
                _noiseModule.strengthX = noise;
                _noiseModule.strengthY = 0f;
                _noiseModule.strengthZ = noise;

                while (emitted < emitPerCycle)
                {
                    int i                 = Random.Range(0, _waterlineCount);
                    int waterLineTriIndex = _waterlineIndices[i];

                    EmitParticle(
                        _wom.P0S[waterLineTriIndex * 6 + 2],
                        _wom.P0S[waterLineTriIndex * 6 + 1],
                        elevation,
                        _wom.Velocities[waterLineTriIndex],
                        _wom.Normals[waterLineTriIndex],
                        _wom.Forces[waterLineTriIndex],
                        _wom.Areas[waterLineTriIndex]);

                    emitted++;
                }
            }
        }


        private void OnDrawGizmosSelected()
        {
            if (_wom == null || _waterlineIndices == null)
            {
                return;
            }
            
            Gizmos.color = Color.magenta;
            for (int i = 0; i < _waterlineIndices.Length; i++)
            {
                if (_wom.States[_waterlineIndices[i]] != 1) continue;
                Vector3 a = _wom.P0S[_waterlineIndices[i] * 6 + 2];
                Vector3 b = _wom.P0S[_waterlineIndices[i] * 6 + 1];
                Gizmos.DrawLine(a, b);
            }
        }


        /// <summary>
        ///     Emit a single particle
        /// </summary>
        /// <param name="p0">First point of water line</param>
        /// <param name="p1">Second point of water line</param>
        /// <param name="elevation">Water elevation</param>
        /// <param name="velocity">Triangle velocity</param>
        /// <param name="normal">Triangle normal</param>
        /// <param name="force">Triangle force</param>
        /// <param name="area">Triangle area</param>
        private void EmitParticle(Vector3 p0,    Vector3 p1, float elevation, Vector3 velocity, Vector3 normal,
            Vector3                       force, float   area)
        {
            if (area < 0.0001f)
            {
                return;
            }

            // Start velocity
            Vector3 startVelocity = normal * velocity.magnitude;
            startVelocity.y =  0f;
            startVelocity   *= initialVelocityModifier;

            // Start position
            Vector3 emissionPoint = (p0 + p1) / 2f;
            emissionPoint   += Time.deltaTime * positionExtrapolationFrames * velocity;
            emissionPoint.y =  elevation + surfaceElevation;

            float normalizedForce = force.magnitude / area;
            float startAlpha      = Mathf.Clamp(normalizedForce * 0.00005f * initialAlphaModifier, 0f, maxInitialAlpha);
            Color startColor      = new Color(1f, 1f, 1f, startAlpha);
            float size            = startSize;

            if (startAlpha < 0.001f)
            {
                return;
            }

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                startColor = startColor,
                position   = emissionPoint,
                velocity   = startVelocity,
                startSize  = size,
            };
            _particleSystem.Emit(emitParams, 1);
            _particleSystem.Play();
        }
    }
}