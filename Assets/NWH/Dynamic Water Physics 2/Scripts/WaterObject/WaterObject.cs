using System;
using UnityEngine;
using UnityEngine.Serialization;
using NWH.Common.Utility;

namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    ///     Data holder class for water objects.
    ///     All physics calculations are done inside WaterObjectManager.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [DisallowMultipleComponent]
    public class WaterObject : MonoBehaviour
    {
        public const bool ShowEditorWarnings = true;

        // Per-object sim settings
        
        /// <summary>
        /// Global dynamic force setting in WaterObjectManager is multiplied by this coefficient for this object only.
        /// </summary>
        [Range(0f, 5f)]
        public float dynamicForceCoeff = 1f;
        
        /// <summary>
        /// Global dynamic force power setting in WaterObjectManager is multiplied by this coefficient for this object only.
        /// </summary>
        [Range(0.01f, 5f)]
        public float dynamicForcePowerCoeff = 1f;
        
        /// <summary>
        /// Global skin friction setting in WaterObjectManager is multiplied by this coefficient for this object only.
        /// </summary>
        [Range(0f, 5f)]
        public float skinFrictionDragCoeff = 1f;

        /// <summary>
        ///     Should the simulation mesh be made convex?
        /// </summary>
        public bool convexifyMesh = true;

        /// <summary>
        ///     Should the simulation mesh be simplified / decimated?
        /// </summary>
        public bool simplifyMesh = true;

        /// <summary>
        ///     If true vertices with same position will be welded.
        ///     Improves performance.
        /// </summary>
        public bool weldColocatedVertices = true;

        /// <summary>
        ///     Target triangle count for the simulation mesh.
        ///     Original mesh will be decimated to this number of triangles is "SimplifyMesh" is enabled.
        ///     Otherwise does nothing.
        /// </summary>
        [FormerlySerializedAs("targetTris")] [Range(8, 256)]
        public int targetTriangleCount = 64;

        [SerializeField] private Mesh           _originalMesh;
        [SerializeField] private SerializedMesh _serializedSimulationMesh;
        [SerializeField] private Mesh           _simulationMesh;
        [SerializeField] private bool           editorHasErrors;

        private MeshFilter _meshFilter;
        private float      _simplificationRatio;


        /// <summary>
        ///     Rigidbody that the forces will be applied to.
        /// </summary>
        public Rigidbody TargetRigidbody { get; set; }

        /// <summary>
        ///     Original mesh of the object, non-simplified and non-convexified.
        /// </summary>
        public Mesh OriginalMesh
        {
            get { return _originalMesh; }
        }

        /// <summary>
        ///     Mesh used to simulate water physics.
        /// </summary>
        public Mesh SimulationMesh
        {
            get { return _simulationMesh; }
        }

        /// <summary>
        ///     Is the object initialized?
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        ///     Is the simulation mesh preview enabled?
        /// </summary>
        public bool PreviewEnabled
        {
            get
            {
                if (_meshFilter == null || _meshFilter.sharedMesh == null)
                {
                    return false;
                }

                return _meshFilter.sharedMesh.name == "DWP_SIM_MESH";
            }
        }

        /// <summary>
        ///     Number of triangles in simulation mesh.
        /// </summary>
        public int TriangleCount
        {
            get { return _simulationMesh == null ? 0 : _simulationMesh.triangles.Length / 3; }
        }

        public SerializedMesh SerializedSimulationMesh
        {
            get { return _serializedSimulationMesh; }
        }

        /// <summary>
        ///     Start index of this object's data in WaterObjectManager's arrays.
        /// </summary>
        public int TriDataStart
        {
            get
            {
                return WaterObjectManager.Instance.WaterObjectTriDataStarts[WoArrayIndex];
            }
        }

        /// <summary>
        ///     Length of this object's data.
        /// </summary>
        public int TriDataLength
        {
            get { return TriDataEnd - TriDataStart; }
        }

        public int TriDataEnd
        {
            get
            {
                return WaterObjectManager.Instance.WaterObjectTriDataEnds[WoArrayIndex];
            }
        }

        /// <summary>
        ///     Start index of this object's data in WaterObjectManager's arrays.
        /// </summary>
        public int VertDataStart
        {
            get
            {
                return WaterObjectManager.Instance.WaterObjectVertDataStarts[WoArrayIndex];
            }
        }

        /// <summary>
        ///     Length of this object's data.
        /// </summary>
        public int VertDataLength {
            get
            {
                return VertDataEnd - VertDataStart;
            }
        }

        public int VertDataEnd
        {
            get
            {
                return WaterObjectManager.Instance.WaterObjectVertDataEnds[WoArrayIndex];
            }
        }

        public int WoArrayIndex { get; set; } = -1;
        
        private void Awake()
        {
            WoArrayIndex = -1;
        }
        
        public void Initialize()
        {
            Initialized = false;

            if (editorHasErrors)
            {
                Debug.LogError(
                    $"WaterObject {name} has setup errors. It will not be simulated. If you have fixed the error but this message still shows, select the WaterObject in question so that editor can refresh.");
                return;
            }

            TargetRigidbody = transform.GetComponentInParent<Rigidbody>(true);
            if (TargetRigidbody == null)
            {
                Debug.LogError($"TargetRigidbody on object {name} is null.");
                return;
            }

            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null)
            {
                Debug.LogError($"MeshFilter on object {name} is null.");
                return;
            }

            if (PreviewEnabled)
            {
                StopSimMeshPreview();
            }

            int colliderCount = TargetRigidbody.transform.GetComponentsInChildren<Collider>(true).Length;
            if (colliderCount == 0)
            {
                Debug.LogError($"{TargetRigidbody.name} has 0 colliders.");
                return;
            }

            if (!PreviewEnabled)
            {
                _originalMesh = _meshFilter.sharedMesh;

                if (_originalMesh == null)
                {
                    Debug.LogError($"MeshFilter on object {name} does not have a valid mesh assigned.");
                    ShowErrorMessage();
                    return;
                }

                if (_simulationMesh == null)
                {
                    _simulationMesh = _serializedSimulationMesh.Deserialize();
                    if (_simulationMesh == null)
                    {
                        _simulationMesh = MeshUtility.GenerateMesh(_originalMesh.vertices, _originalMesh.triangles);
                    }
                }

                // Serialize only if mesh has been modified
                _simulationMesh.name = "DWP_SIM_MESH";
                _serializedSimulationMesh.Serialize(_simulationMesh);
            }

            Debug.Assert(SimulationMesh != null, $"Simulation mesh is null on object {name}.");

            if (editorHasErrors)
            {
                Debug.LogError($"WaterObject {name} has setup errors. Will not simulate.");
                return;
            }

            Debug.Assert(SimulationMesh.GetInstanceID() != OriginalMesh.GetInstanceID(),
                         $"Simulation mesh and original mesh have same Instance ID on object {name}. !BUG!.");

            Initialized = true;
        }


        /// <summary>
        ///     Returns true if object is touching water or false if it is not.
        ///     It is recommended to cache the result.
        /// </summary>
        public bool IsTouchingWater()
        {
            for (int i = TriDataStart; i < TriDataEnd; i++)
            {
                int state = WaterObjectManager.Instance.States[i];
                if (state <= 1)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        ///     Swaps original mesh with simulation mesh on MeshFilter for in-scene preview.
        /// </summary>
        public void StartSimMeshPreview()
        {
            if (PreviewEnabled)
            {
                return;
            }

            if (_simulationMesh == null)
            {
                Debug.LogError("Could not start simulation mesh preview. Simulation mesh is null.");
                return;
            }

            if (_originalMesh == null)
            {
                Debug.LogError("Could not start simulation mesh preview. Original mesh is null.");
                return;
            }

            if (!Initialized)
            {
                Debug.LogError(
                    "Can not show dummy mesh preview for object {name}. WaterObject could not be initialized" +
                    " due to setup errors above. Fix these errors before trying to generate dummy mesh.");
                return;
            }

            if (_simulationMesh != null)
            {
                _meshFilter.sharedMesh = _simulationMesh;
            }
        }


        private void OnDisable()
        {
            if (WaterObjectManager.Instance != null)
            {
                WaterObjectManager.Instance.MarkTrisAsDisabled(TriDataStart, TriDataEnd);
            }

            if (Application.isEditor)
            {
                StopSimMeshPreview();
            }
        }


        private void OnEnable()
        {
            if (Initialized && WaterObjectManager.Instance != null)
            {
                WaterObjectManager.Instance.MarkTrisAsEnabled(TriDataStart, TriDataEnd);
            }
        }


        /// <summary>
        ///     Returns the states of triangles.
        ///     0 - Triangle is under water
        ///     1 - Triangle is partially under water
        ///     2 - Triangle is above water
        ///     3 - Triangle's object is disabled
        ///     4 - Triangle's object is deleted
        /// </summary>
        /// <param name="states"></param>
        public void GetStates(ref byte[] states)
        {
            if (!Initialized)
            {
                return;
            }

            Debug.Assert(states.Length == TriDataLength,
                         "Size mismatch. Array must have length of 'WaterObject.VertDataLength'");
            Array.Copy(WaterObjectManager.Instance.States, TriDataStart, states, 0, TriDataLength);
        }


        /// <summary>
        ///     Force application points.
        /// </summary>
        public void GetForcePoints(ref Vector3[] points)
        {
            if (!Initialized)
            {
                return;
            }

            Debug.Assert(points.Length == TriDataLength,
                         "Size mismatch. Array must have length of 'WaterObject.VertDataLength'");
            Array.Copy(WaterObjectManager.Instance.ForcePoints, TriDataStart, points, 0, TriDataLength);
        }


        /// <summary>
        ///     Forces for each force point.
        /// </summary>
        public void GetForces(ref Vector3[] forces)
        {
            if (!Initialized)
            {
                return;
            }

            Debug.Assert(forces.Length == TriDataLength,
                         "Size mismatch. Array must have length of 'WaterObject.VertDataLength'");
            Array.Copy(WaterObjectManager.Instance.Forces, TriDataStart, forces, 0, TriDataLength);
        }


        /// <summary>
        ///     Triangle normals
        /// </summary>
        public void GetNormals(ref Vector3[] normals)
        {
            if (!Initialized)
            {
                return;
            }

            Debug.Assert(normals.Length == TriDataLength,
                         "Size mismatch. Array must have length of 'WaterObject.VertDataLength'");
            Array.Copy(WaterObjectManager.Instance.States, TriDataStart, normals, 0, TriDataLength);
        }


        /// <summary>
        ///     Triangle areas
        /// </summary>
        public void GetAreas(ref Vector3[] areas)
        {
            if (!Initialized)
            {
                return;
            }

            Debug.Assert(areas.Length == TriDataLength,
                         "Size mismatch. Array must have length of 'WaterObject.VertDataLength'");
            Array.Copy(WaterObjectManager.Instance.States, TriDataStart, areas, 0, TriDataLength);
        }


        /// <summary>
        ///     Velocities at centers of triangles
        /// </summary>
        public void GetVelocities(ref Vector3[] velocities)
        {
            if (!Initialized)
            {
                return;
            }

            Debug.Assert(velocities.Length == TriDataLength,
                         "Size mismatch. Array must have length of 'WaterObject.VertDataLength'");
            Array.Copy(WaterObjectManager.Instance.States, TriDataStart, velocities, 0, TriDataLength);
        }


        /// <summary>
        ///     Distance from triangle to water surface
        /// </summary>
        public void GetDistances(ref Vector3[] distances)
        {
            if (!Initialized)
            {
                return;
            }

            Debug.Assert(distances.Length == TriDataLength,
                         "Size mismatch. Array must have length of 'WaterObject.VertDataLength'");
            Array.Copy(WaterObjectManager.Instance.States, TriDataStart, distances, 0, TriDataLength);
        }


        private void OnDestroy()
        {
            if (WaterObjectManager.Instance != null)
            {
                WaterObjectManager.Instance.MarkTrisAsDeleted(TriDataStart, TriDataEnd);
            }

            if (Application.isEditor)
            {
                StopSimMeshPreview();
            }
        }
        

        /// <summary>
        ///     Generates simulation mesh according to the settings
        /// </summary>
        public void GenerateSimMesh()
        {
            bool previewWasEnabled = false;

            if (PreviewEnabled)
            {
                StopSimMeshPreview();
                previewWasEnabled = true;
            }

            if (!PreviewEnabled)
            {
                if (!Initialized)
                {
                    Initialize();
                    if (!Initialized)
                    {
                        Debug.LogError(
                            $"Could not generate dummy mesh for object {name}. WaterObject could not be initialized" +
                            " due to setup errors above. Fix these errors before trying to generate dummy mesh.");
                        return;
                    }
                }

                if (_simulationMesh == null)
                {
                    _simulationMesh = new Mesh
                    {
                        name = "DWP_SIM_MESH",
                    };
                }

                if (simplifyMesh)
                {
                    _simplificationRatio = (targetTriangleCount * 3f + 16) / _originalMesh.triangles.Length;
                    if (_simplificationRatio >= 1f && !convexifyMesh)
                    {
                        Debug.Log("Target tri count larger than the original tri count. Nothing to simplify.");
                    }

                    _simplificationRatio = Mathf.Clamp(_simplificationRatio, 0f, 1f);
                }

                if (!simplifyMesh && !convexifyMesh && !weldColocatedVertices)
                {
                    _simulationMesh = MeshUtility.GenerateMesh(_originalMesh.vertices, _originalMesh.triangles);
                }
                else
                {
                    MeshUtility.GenerateSimMesh(ref _originalMesh, ref _simulationMesh, simplifyMesh, convexifyMesh,
                                                  weldColocatedVertices, _simplificationRatio);
                }
                
                _serializedSimulationMesh.Serialize(_simulationMesh);
            }
            else
            {
                Debug.LogError("Cannot generate simulation mesh while preview is enabled.");
            }

            if (previewWasEnabled)
            {
                StartSimMeshPreview();
            }
        }


        /// <summary>
        ///     Swaps simulation mesh on MeshFilter with original mesh.
        /// </summary>
        public void StopSimMeshPreview()
        {
            if (!PreviewEnabled)
            {
                return;
            }

            if (_meshFilter == null || _originalMesh == null)
            {
                Debug.LogError($"Cannot stop sim mesh preview on object {name}. MeshFilter or original mesh is null.");
                return;
            }

            if (_originalMesh != null)
            {
                _meshFilter.sharedMesh = _originalMesh;
            }
            else
            {
                Debug.LogError($"Original mesh on object {name} could not be found. !BUG!");
            }
        }


        /// <summary>
        ///     Shows setup error message.
        /// </summary>
        private void ShowErrorMessage()
        {
            Debug.LogWarning($"One or more setup errors have been found. WaterObject {name} will not be " +
                             "simulated until these are fixed. Check manual for more details on WaterObject setup.");
        }
    }
}