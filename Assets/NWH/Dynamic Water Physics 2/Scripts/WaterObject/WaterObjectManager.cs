using System;
using System.Collections.Generic;
using System.Linq;
using NWH.Common.Utility;
using NWH.DWP2.WaterData;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    ///     Main class for data processing and triangle job management. Handles all the physics calculations.
    /// </summary>
    [Serializable]
    public class WaterObjectManager : MonoBehaviour
    {
        /// <summary>
        ///     Static instance of this object.
        /// </summary>
        public static WaterObjectManager Instance;

        /// <summary>
        /// If true WaterObjects will be loaded from multiple scenes.
        /// </summary>
        public bool includeMultipleScenes = true;

        /// <summary>
        ///     Should buoyant forces be calculated? When disabled the object will sink, but the hydrodynamic forces
        ///     will still affect it.
        /// </summary>
        public bool calculateBuoyancyForces = true;

        /// <summary>
        ///     Should hydrodynamic forces be calculated? When disabled the object will still float, but interaction
        ///     with water will not depend on the velocity of the object.
        /// </summary>
        public bool calculateDynamicForces = true;

        /// <summary>
        ///     Should skin drag be calculated? Skin drag applies drag on the surface when the fluid is traveling over it.
        /// </summary>
        public bool calculateSkinDrag = true;

        /// <summary>
        ///     Coefficient by which all the non-buoyancy forces will be multiplied.
        /// </summary>
        [Range(0.5f, 2f)]
        public float dynamicForceCoefficient = 1f;

        /// <summary>
        ///     Set to 1 for linear force/velocity ratio or >1 for exponential ratio.
        ///     If higher than one forces will increase exponentially with speed.
        ///     Use 1 for best performance. Any other value will result in additional Mathf.Pow() call per triangle.
        /// </summary>
        [Range(0.5f, 2f)]
        public float dynamicForcePower = 1f;

        /// <summary>
        ///     Density of the fluid the object is in. Affects only buoyancy.
        /// </summary>
        public float fluidDensity = 1030f;
        
        /// <summary>
        ///     Called after WaterObjectManager data is synchronized.
        /// </summary>
        [HideInInspector] public UnityEvent onSynchronize = new UnityEvent();

        /// <summary>
        ///     Should water velocities be taken into account when calculating forces?
        ///     Should be disabled if the water used does not support velocity/flow map queries
        ///     Will be automatically disabled if the water system does not support velocity queries.
        ///     to save on performance.
        /// </summary>
        public bool simulateWaterFlow = true;

        /// <summary>
        ///     Should water normals be taken into account when calculating forces?
        ///     Should be disabled if the water if flat or the water system does not support water normal queries
        ///     to save on performance.
        ///     Will be automatically disabled if the water system does not support normal queries.
        /// </summary>
        public bool simulateWaterNormals = true;

        /// <summary>
        ///     Resistant force exerted on an object moving in a fluid. Skin friction drag is caused by the viscosity of fluids and
        ///     is developed as a fluid moves on the surface of an object.
        /// </summary>
        [Range(0f, 0.2f)]
        public float skinFrictionDrag = 0.01f;

        /// <summary>
        ///     Set to 1 for linear dot/force ratio or other than 1 for exponential ratio.
        ///     When force is calculated it is multiplied by the dot value between normal of the surface and the velocity.
        ///     High power values will result in shallow angles between the two having less of an effect on the final force, and
        ///     vice versa.
        ///     Use 1 for best performance. Any other value will result in additional Mathf.Pow() call per triangle.
        /// </summary>
        [Range(0.5f, 2f)]
        public float velocityDotPower = 1f;

        /// <summary>
        ///     When true even inactive objects will get synchronized, but will not be simulated until they become active.
        /// </summary>
        public bool includeInactive = true;

        /// <summary>
        /// When true jobs will be scheduled and finished on the same frame.
        /// Worse for performance but reduces lag between the physics and visual update.
        /// </summary>
        public bool finishJobsInOneFrame = true;

        private Vector3[] _objRigidbodyAngVels;
        private Vector3[] _objRigidbodyCoMs;
        private Vector3[] _objRigidbodyLinVels;
        private int[]     _woIndices;
        private int[]     _woTriDataStart;
        private int[]     _woTriDataEnd;
        private int[]     _woVertDataStart;
        private int[]     _woVertDataEnd;
        private float[]   _objDynamicForceCoeffs;
        private float[]   _objDynamicForcePowerCoeffs;
        private float[]   _objSkinFrictionDragCoeffs;
        
        private float[]           _triAreas;
        private float[]           _triDistToSurface;
        private bool              _finished;
        private Vector3[]         _triForcePoints;
        private bool              _initialized;
        private int[]             _triangles;
        private Vector3[]         _vertices;
        private Vector3[]         _triNormals;
        private Vector3[]         _triForces;
        private bool              _scheduled;
        private byte[]            _triStates;
        private Vector3[]         _triVelocities;
        private Vector3[]         _waterFlows;
        private float[]           _waterHeights;
        private JobHandle         _waterJobHandle;
        private Vector3[]         _waterNormals;
        private List<WaterObject> _waterObjects;
        private WaterTriangleJob  _waterTriJob;
        private Vector3[]         _worldVerts;

        public List<WaterObject> WaterObjects
        {
            get { return _waterObjects; }
        }

        public int WaterObjectCount
        {
            get;
            private set;
        }
        
        /// <summary>
        ///     An array representing the state of each triangle:
        ///     0 - all three verts are under water
        ///     1 - one or two verts are under water
        ///     2 - triangle is above water
        ///     3 - triangle is disabled
        ///     4 - triangle has been deleted
        ///     To see if the triangle should be simulated check if its state is less or equal to 2.
        ///     For triangle to be touching water its state should be 0 or 1.
        /// </summary>
        public byte[] States
        {
            get { return _triStates; }
            private set { _triStates = value; }
        }

        public int[] WaterObjectTriDataStarts
        {
            get { return _woTriDataStart; }
        }
        
        public int[] WaterObjectTriDataEnds
        {
            get { return _woTriDataEnd; }
        }
        
        public int[] WaterObjectVertDataStarts
        {
            get { return _woVertDataStart; }
        }
        
        public int[] WaterObjectVertDataEnds
        {
            get { return _woVertDataEnd; }
        }

        public Vector3[] WorldVertices
        {
            get { return _worldVerts; }
        }

        public int[] Triangles
        {
            get { return _triangles; }
        }


        public Vector3[] Vertices
        {
            get { return _vertices; }
        }

        /// <summary>
        ///     Forces in world coordinates. Each force should be applied at the corresponding force point.
        /// </summary>
        public Vector3[] Forces
        {
            get { return _triForces; }
            private set { _triForces = value; }
        }

        /// <summary>
        ///     Force application points in world coordinates.
        /// </summary>
        public Vector3[] ForcePoints
        {
            get { return _triForcePoints; }
            private set { _triForcePoints = value; }
        }

        /// <summary>
        ///     Water heights at each vertex.
        /// </summary>
        public float[] WaterHeights
        {
            get { return _waterHeights; }
        }

        /// <summary>
        ///     Water surface normals at each vertex.
        /// </summary>
        public Vector3[] WaterNormals
        {
            get { return _waterNormals; }
        }

        /// <summary>
        ///     Water surface flow at each vertex.
        /// </summary>
        public Vector3[] WaterFlows
        {
            get { return _waterFlows; }
        }

        /// <summary>
        ///     Triangle normals. Normals are calculated from vertex data.
        ///     Mesh normals are ignored.
        /// </summary>
        public Vector3[] Normals
        {
            get { return _triNormals; }
            private set { _triNormals = value; }
        }

        /// <summary>
        ///     Areas of each simulated triangle.
        /// </summary>
        public float[] Areas
        {
            get { return _triAreas; }
            private set { _triAreas = value; }
        }

        /// <summary>
        ///     Velocities of centers of each simulated triangle.
        /// </summary>
        public Vector3[] Velocities
        {
            get { return _triVelocities; }
            private set { _triVelocities = value; }
        }

        /// <summary>
        ///     Distances to surface from the center of each triangle.
        /// </summary>
        public float[] DistancesToSurface
        {
            get { return _triDistToSurface; }
            private set { _triDistToSurface = value; }
        }

        public Vector3[] P0S { get; private set; }

        /// <summary>
        ///     Total triangle count.
        ///     To get the simulated triangle count use ActiveTriCount instead.
        /// </summary>
        public int TriangleCount { get; private set; }

        /// <summary>
        ///     Total simulated vertex count.
        /// </summary>
        public int VertexCount { get; private set; }

        /// <summary>
        ///     Count of currently simulated triangles.
        /// </summary>
        public int ActiveTriCount
        {
            get { return States?.Count(s => s <= 2) ?? 0; }
        }

        /// <summary>
        ///     Count of currently simulated triangles that are under water.
        /// </summary>
        public int ActiveUnderwaterTriCount
        {
            get { return States?.Count(s => s <= 1) ?? 0; }
        }

        /// <summary>
        ///     Count of currently simulated triangles that are above water.
        /// </summary>
        public int ActiveAboveWaterTriCount
        {
            get { return States?.Count(s => s == 2) ?? 0; }
        }

        /// <summary>
        ///     Disabled triangle count.
        ///     Triangle will be marked as disabled when the parent GameObject is disabled.
        /// </summary>
        public int DisabledTriCount
        {
            get { return States?.Count(s => s == 3) ?? 0; }
        }

        /// <summary>
        ///     Destroyed triangle count.
        ///     Triangle will be marked as destroyed when the parent GameObject is destroyed.
        /// </summary>
        public int DestroyedTriCount
        {
            get { return States?.Count(s => s == 4) ?? 0; }
        }

        /// <summary>
        ///     Total number of disabled and destoryed triangles.
        /// </summary>
        public int InactiveTriCount
        {
            get { return States?.Count(s => s == 3 || s == 4) ?? 0; }
        }


        private void Initialize()
        {
            if (_initialized)
            {
                Deallocate();
            }

            // Get all WaterObjects
            _waterObjects = new List<WaterObject>();
            FindSceneWaterObjects(ref _waterObjects, includeInactive);
            WaterObjectCount = _waterObjects.Count;
            
            if (WaterObjectCount == 0)
            {
                return;
            }

            // Initialize WaterObjects
            foreach (WaterObject wo in _waterObjects)
            {
                wo.Initialize();
            }

            _waterObjects = _waterObjects.Where(w => w.Initialized).ToList();


            // Initialize arrays
            for (int i = 0; i < WaterObjectCount; i++)
            {
                _waterObjects[i].WoArrayIndex = i;
            }

            // Allocate arrays
            int totalTriIndexCount = 0;
            int totalVertCount     = 0;
            foreach (WaterObject waterObject in _waterObjects)
            {
                totalTriIndexCount += waterObject.SerializedSimulationMesh.triangles.Length;
                totalVertCount     += waterObject.SerializedSimulationMesh.vertices.Length;
            }
            
            _triangles          = new int[totalTriIndexCount];
            _vertices           = new Vector3[totalVertCount];

            int triCount = totalTriIndexCount / 3;
            _woTriDataStart             = new int[WaterObjectCount];
            _woTriDataEnd               = new int[WaterObjectCount];
            _woVertDataStart            = new int[WaterObjectCount];
            _woVertDataEnd              = new int[WaterObjectCount];
            _woIndices                  = new int[triCount];
            _objDynamicForceCoeffs      = new float[WaterObjectCount];
            _objDynamicForcePowerCoeffs = new float[WaterObjectCount];
            _objSkinFrictionDragCoeffs  = new float[WaterObjectCount];

            // Get triangle data     
            totalTriIndexCount = 0;
            totalVertCount     = 0;
            
            // Vert data - one index per vert
            // Tri data - one index per three triangle indices / one triangle
            // Obj data - one index per three triangle indices / one triangle
            for (int woIndex = 0; woIndex < _waterObjects.Count; woIndex++)
            {
                WaterObject waterObject   = _waterObjects[woIndex];
                int         woTriCount    = waterObject.SerializedSimulationMesh.triangles.Length;
                int         woVertCount   = waterObject.SerializedSimulationMesh.vertices.Length;

                int woTriDataStart = totalTriIndexCount / 3;
                int woTriDataEnd = totalTriIndexCount / 3 + woTriCount / 3;
                _woTriDataStart[woIndex] = woTriDataStart;
                _woTriDataEnd[woIndex]   = woTriDataEnd;

                int woVertDataStart = totalVertCount;
                int woVertDataEnd   = totalVertCount + woVertCount;
                _woVertDataStart[woIndex] = woVertDataStart;
                _woVertDataEnd[woIndex]   = woVertDataEnd;
                
                for (int i = woTriDataStart; i < woTriDataEnd; i++)
                {
                    _woIndices[i] = woIndex;
                }
                
                for (int i = 0; i < woTriCount; i += 3)
                {
                    _triangles[totalTriIndexCount + i] =
                        waterObject.SerializedSimulationMesh.triangles[i] + totalVertCount;
                    _triangles[totalTriIndexCount + i + 1] =
                        waterObject.SerializedSimulationMesh.triangles[i + 1] + totalVertCount;
                    _triangles[totalTriIndexCount + i + 2] =
                        waterObject.SerializedSimulationMesh.triangles[i + 2] + totalVertCount;
                }

                for (int i = 0; i < woVertCount; i++)
                {
                    _vertices[totalVertCount + i]       = waterObject.SerializedSimulationMesh.vertices[i];
                }

                if (woTriCount > 400)
                {
                    Debug.LogWarning($"Excessive number of triangles found on {waterObject.name}'s simulation mesh. " +
                                     $"Such high number of triangles ({woTriCount / 3}) will not improve simulation quality but will have a " +
                                     "large performance impact. Use 'Simplify Mesh' option on this WaterObject.");
                }

                totalTriIndexCount += woTriCount;
                totalVertCount     += woVertCount;
            }

            // Allocate native arrays for water tri job
            TriangleCount = totalTriIndexCount / 3;
            VertexCount   = totalVertCount;

            _waterTriJob.ObjectIndices              = new NativeArray<int>(TriangleCount, Allocator.Persistent);
            _waterTriJob.ObjDynamicForceCoeffs      = new NativeArray<float>(WaterObjectCount, Allocator.Persistent);
            _waterTriJob.ObjDynamicForcePowerCoeffs = new NativeArray<float>(WaterObjectCount, Allocator.Persistent);
            _waterTriJob.ObjSkinFrictionDragCoeffs  = new NativeArray<float>(WaterObjectCount, Allocator.Persistent);
            _waterTriJob.ObjRigidbodyCoMs           = new NativeArray<Vector3>(WaterObjectCount, Allocator.Persistent);
            _waterTriJob.ObjRigidbodyAngularVels    = new NativeArray<Vector3>(WaterObjectCount, Allocator.Persistent);
            _waterTriJob.ObjRigidbodyLinearVels     = new NativeArray<Vector3>(WaterObjectCount, Allocator.Persistent);

            _waterTriJob.States       = new NativeArray<byte>(TriangleCount, Allocator.Persistent);
            _waterTriJob.ResultForces = new NativeArray<Vector3>(TriangleCount, Allocator.Persistent);
            _waterTriJob.ResultPoints = new NativeArray<Vector3>(TriangleCount, Allocator.Persistent);
            _waterTriJob.WorldVertices     = new NativeArray<Vector3>(VertexCount,   Allocator.Persistent);
            _waterTriJob.Triangles    = new NativeArray<int>(TriangleCount * 3, Allocator.Persistent);
            _waterTriJob.WaterHeights = new NativeArray<float>(VertexCount, Allocator.Persistent);
            _waterTriJob.WaterFlows   = new NativeArray<Vector3>(VertexCount,   Allocator.Persistent);
            _waterTriJob.WaterNormals = new NativeArray<Vector3>(VertexCount,   Allocator.Persistent);
            _waterTriJob.Velocities   = new NativeArray<Vector3>(TriangleCount, Allocator.Persistent);
            _waterTriJob.Normals      = new NativeArray<Vector3>(TriangleCount, Allocator.Persistent);
            _waterTriJob.Areas        = new NativeArray<float>(TriangleCount, Allocator.Persistent);
            _waterTriJob.P0s          = new NativeArray<Vector3>(6 * TriangleCount, Allocator.Persistent);
            _waterTriJob.Distances    = new NativeArray<float>(TriangleCount, Allocator.Persistent);

            // Allocate managed arrays
            // Input
            _waterHeights = new float[VertexCount];
            _waterHeights.Fill(0);

            _waterFlows = new Vector3[VertexCount];
            Vector3 zeroVector = Vector3.zero;
            _waterFlows.Fill(zeroVector);

            _waterNormals = new Vector3[VertexCount];
            Vector3 upVector = -Physics.gravity.normalized;
            _waterNormals.Fill(upVector);
            
            _objRigidbodyCoMs    = new Vector3[WaterObjectCount];
            _objRigidbodyLinVels = new Vector3[WaterObjectCount];
            _objRigidbodyAngVels = new Vector3[WaterObjectCount];

            // Output
            _worldVerts       = new Vector3[VertexCount];
            _triStates        = new byte[TriangleCount];
            _triVelocities    = new Vector3[TriangleCount];
            _triForces        = new Vector3[TriangleCount];
            _triForcePoints   = new Vector3[TriangleCount];
            _triAreas         = new float[TriangleCount];
            _triNormals       = new Vector3[TriangleCount];
            _triDistToSurface = new float[TriangleCount];

            P0S               = new Vector3[6 * TriangleCount];

            // Copy data to native arrays
            _waterTriJob.Triangles.FastCopyFrom(_triangles);

            // Copy default data
            _waterTriJob.WaterHeights.FastCopyFrom(_waterHeights);
            _waterTriJob.WaterFlows.FastCopyFrom(_waterFlows);
            _waterTriJob.WaterNormals.FastCopyFrom(_waterNormals);

            _initialized = true;
        }


        private void Awake()
        {
            // Check if there is only one WaterObjectManager
            if (FindObjectsOfType<WaterObjectManager>().Length > 1)
            {
                Debug.LogError("There can be only one WaterObjectManager in the scene.");
            }

            Instance = this;

            _finished    = true;
            _scheduled   = false;
            _initialized = false;
        }


        private void Start()
        {
            Initialize();
        }


        private void FixedUpdate()
        {
            if (finishJobsInOneFrame)
            {
                Schedule();
                Finish();
                Process();
            }
            else
            {
                Finish();
                Process();
                Schedule();
            }
        }


        private void OnDisable()
        {
            Deallocate();
        }


        /// <summary>
        ///     Slow. Avoid if at all possible.
        ///     Instead of spawning new objects it is preferable to spawn them at start and then disable them - this way
        ///     job arrays will get initialized to correct size and yet inactive objects will not be simulated.
        /// </summary>
        public void Synchronize()
        {
            Initialize();
            onSynchronize.Invoke();
        }


        public void MarkTrisAsDeleted(int triStartIndex, int triEndIndex)
        {
            for (int i = triStartIndex; i < triEndIndex; i++)
            {
                _triStates[i] = 4;
            }
        }
        
        public void MarkTrisAsDisabled(int triStartIndex, int triEndIndex)
        {
            for (int i = triStartIndex; i < triEndIndex; i++)
            {
                _triStates[i] = 3;
            }
        }
        
        public void MarkTrisAsEnabled(int triStartIndex, int triEndIndex)
        {
            for (int i = triStartIndex; i < triEndIndex; i++)
            {
                _triStates[i] = 2;
            }
        }

        private void OnValidate()
        {
            Instance = this;
        }


        private void OnDestroy()
        {
            Deallocate();
        }


        private void FindSceneWaterObjects(ref List<WaterObject> waterObjects, bool includeInactive)
        {
            if (includeMultipleScenes)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    // Not using FindObjectsOfType since it ignores inactive objects.
                    waterObjects.AddRange(SceneManager.GetSceneAt(i).GetRootGameObjects()
                        .SelectMany(g => g.GetComponentsInChildren<WaterObject>(includeInactive))
                        .Where(w => w.gameObject.activeInHierarchy ||
                                    !w.gameObject.activeInHierarchy && includeInactive));
                }
            }
            else
            {
                waterObjects = SceneManager.GetActiveScene().GetRootGameObjects()
                    .SelectMany(g => g.GetComponentsInChildren<WaterObject>(includeInactive))
                    .Where(w => w.gameObject.activeInHierarchy ||
                                !w.gameObject.activeInHierarchy && includeInactive)
                    .ToList();
            }
        }


        private void Schedule()
        {
            if (!_initialized || !_finished || TriangleCount == 0)
            {
                return;
            }

            if (WaterDataProvider.Instance == null)
            {
                Debug.LogError(
                    "There is no WaterDataProvider present in the scene. Since DWP2 2.2 there has to be exactly one WaterDataProvider present in the scene." +
                    "If using flat water attach FlatWaterDataProvider to it.");
                return;
            }

            _finished  = false;
            _scheduled = true;

            //  Fill in new data into managed containers
            int waterObjectCount = _waterObjects.Count;
            for (int i = 0; i < waterObjectCount; i++)
            {
                int triDataStart  = _woTriDataStart[i];
                int triDataEnd    = _woTriDataEnd[i];
                int vertDataStart = _woVertDataStart[i];
                int vertDataEnd   = _woVertDataEnd[i];
                
                WaterObject wo            = _waterObjects[i];
                if (wo == null)
                {
                    for (int j = triDataStart; j < triDataEnd; j += 3)
                    {
                        _triStates[j] = 4;
                    }
                    
                    continue;
                }

                _objRigidbodyCoMs[i]           = wo.TargetRigidbody.worldCenterOfMass;
                _objRigidbodyLinVels[i]        = wo.TargetRigidbody.linearVelocity;
                _objRigidbodyAngVels[i]        = wo.TargetRigidbody.angularVelocity;
                _objDynamicForceCoeffs[i]      = wo.dynamicForceCoeff;
                _objDynamicForcePowerCoeffs[i] = wo.dynamicForcePowerCoeff;
                _objSkinFrictionDragCoeffs[i]  = wo.skinFrictionDragCoeff;
                
                //Fill in data
                for (int j = triDataStart; j < triDataEnd; j += 3)
                {
                    _triStates[j] = wo.isActiveAndEnabled ? (byte) 2 : (byte) 3;
                }
                
                // Convert local vertex positions to world positions
                Matrix4x4 cachedLocalToWorldMatrix = wo.transform.localToWorldMatrix;
                for (int j = vertDataStart; j < vertDataEnd; j++)
                {
                    _worldVerts[j] = cachedLocalToWorldMatrix.MultiplyPoint3x4(_vertices[j]);
                }
            }

            // Get water data
            WaterDataProvider.Instance.GetWaterHeightsFlowsNormals(ref _worldVerts, ref _waterHeights, ref _waterFlows,
                                                                   ref _waterNormals);

            // Set simulation settings
            _waterTriJob.GlobalUpVector = Vector3.up;
            _waterTriJob.ZeroVector     = Vector3.zero;
            _waterTriJob.SimulateWaterNormals =
                simulateWaterNormals && WaterDataProvider.Instance.SupportsWaterNormalQueries();
            _waterTriJob.SimulateWaterFlow = simulateWaterFlow && WaterDataProvider.Instance.SupportsWaterFlowQueries();
            _waterTriJob.CalculateDynamicForces = calculateDynamicForces;
            _waterTriJob.CalculateBuoyantForces = calculateBuoyancyForces;
            _waterTriJob.CalculateSkinDrag = calculateSkinDrag;
            _waterTriJob.Gravity = Physics.gravity;
            _waterTriJob.FluidDensity = fluidDensity;
            _waterTriJob.DynamicForceFactor = dynamicForceCoefficient;
            _waterTriJob.DynamicForcePower = dynamicForcePower;
            _waterTriJob.VelocityDotPower = velocityDotPower;
            _waterTriJob.SkinDrag = skinFrictionDrag;
            _waterTriJob.ObjDynamicForceCoeffs.FastCopyFrom(_objDynamicForceCoeffs);
            _waterTriJob.ObjSkinFrictionDragCoeffs.FastCopyFrom(_objSkinFrictionDragCoeffs);
            _waterTriJob.ObjDynamicForcePowerCoeffs.FastCopyFrom(_objDynamicForcePowerCoeffs);

            // Copy new data to native containers
            _waterTriJob.WorldVertices.FastCopyFrom(_worldVerts);
            _waterTriJob.WaterHeights.FastCopyFrom(_waterHeights);
            _waterTriJob.WaterFlows.FastCopyFrom(_waterFlows);
            _waterTriJob.WaterNormals.FastCopyFrom(_waterNormals);
            _waterTriJob.States.FastCopyFrom(_triStates);
            _waterTriJob.Velocities.FastCopyFrom(_triVelocities);
            
            _waterTriJob.ObjectIndices.FastCopyFrom(_woIndices);
            _waterTriJob.ObjRigidbodyCoMs.FastCopyFrom(_objRigidbodyCoMs);
            _waterTriJob.ObjRigidbodyLinearVels.FastCopyFrom(_objRigidbodyLinVels);
            _waterTriJob.ObjRigidbodyAngularVels.FastCopyFrom(_objRigidbodyAngVels);

            _waterJobHandle = _waterTriJob.Schedule(TriangleCount, 32);
        }


        /// <summary>
        ///     Manipulate data retrieved from job before job is started again.
        ///     Accessing job data other than here will result in error due to job being unfinished.
        /// </summary>
        private void Process()
        {
            if (!_initialized || !_finished)
            {
                return;
            }

            if (TriangleCount == 0)
            {
                return;
            }

            // Copy native arrays to managed arrays for faster access
            _waterTriJob.States.FastCopyTo(_triStates);
            _waterTriJob.ResultForces.FastCopyTo(_triForces);
            _waterTriJob.ResultPoints.FastCopyTo(_triForcePoints);
            _waterTriJob.Normals.FastCopyTo(_triNormals);
            _waterTriJob.Areas.FastCopyTo(_triAreas);
            _waterTriJob.Velocities.FastCopyTo(_triVelocities);
            _waterTriJob.Distances.FastCopyTo(_triDistToSurface);
            _waterTriJob.P0s.FastCopyTo(P0S);

            // Apply forces
            bool initAutoSync = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = false;
            
            for (int i = 0; i < TriangleCount; i++)
            {
                if (_triStates[i] >= 2)
                {
                    continue;
                }

                _waterObjects[_woIndices[i]].TargetRigidbody.AddForceAtPosition(_triForces[i], _triForcePoints[i]);
            }

            Physics.autoSyncTransforms = initAutoSync;
        }


        private void Finish()
        {
            if (!_initialized || !_scheduled)
            {
                return;
            }

            _scheduled = false;
            _waterJobHandle.Complete();
            _finished = true;
        }


        private static void FastCopy<T>(T[] source, T[] destination) where T : struct
        {
            Array.Copy(source, destination, source.Length);
        }


        private void Deallocate()
        {
            if (!_initialized)
            {
                return;
            }

            _initialized = false;

            try
            {
                _waterJobHandle.Complete();
                _waterTriJob.States.Dispose();
                _waterTriJob.ResultForces.Dispose();
                _waterTriJob.ResultPoints.Dispose();
                _waterTriJob.WorldVertices.Dispose();
                _waterTriJob.Triangles.Dispose();
                _waterTriJob.WaterHeights.Dispose();
                _waterTriJob.WaterFlows.Dispose();
                _waterTriJob.WaterNormals.Dispose();
                _waterTriJob.Velocities.Dispose();
                _waterTriJob.Normals.Dispose();
                _waterTriJob.Areas.Dispose();
                _waterTriJob.ObjRigidbodyCoMs.Dispose();
                _waterTriJob.ObjRigidbodyAngularVels.Dispose();
                _waterTriJob.ObjRigidbodyLinearVels.Dispose();
                _waterTriJob.P0s.Dispose();
                _waterTriJob.Distances.Dispose();
                _waterTriJob.ObjectIndices.Dispose();
                _waterTriJob.ObjDynamicForceCoeffs.Dispose();
                _waterTriJob.ObjDynamicForcePowerCoeffs.Dispose();
                _waterTriJob.ObjSkinFrictionDragCoeffs.Dispose();
            }
            catch
            {
                // Possibly a Unity bug where, despite calling Complete(), the job might not be finished.
            }
        }
    }
}