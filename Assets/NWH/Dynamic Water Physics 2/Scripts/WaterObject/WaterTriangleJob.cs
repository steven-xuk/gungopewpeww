using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

#if DWP_BURST
using Unity.Burst;
#endif

namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    ///     Job for calculating WaterObject forces.
    /// </summary>
    #if DWP_BURST
    [BurstCompile] // Burst is recommended since it can shave 30%+ of CPU usage. 
    #endif
    public struct WaterTriangleJob : IJobParallelFor
    {
        // [NativeDisableParallelForRestriction] is normally not required but some small number of users have issue
        // with Burst 1.3 and Unity 2020 throwing errors about accessing the data outside of the batch bounds.
        // Could not reproduce with Burst 1.4 and Unity 2019 LTS.
        [NativeDisableParallelForRestriction][ReadOnly] public Vector3 ZeroVector;
        [NativeDisableParallelForRestriction][ReadOnly] public Vector3 GlobalUpVector;

        [NativeDisableParallelForRestriction][ReadOnly] public bool CalculateBuoyantForces;
        [NativeDisableParallelForRestriction][ReadOnly] public bool CalculateDynamicForces;
        [NativeDisableParallelForRestriction][ReadOnly] public bool CalculateSkinDrag;

        [NativeDisableParallelForRestriction][ReadOnly] public bool SimulateWaterNormals;
        [NativeDisableParallelForRestriction][ReadOnly] public bool SimulateWaterFlow;

        [NativeDisableParallelForRestriction][ReadOnly] public Vector3 Gravity;
        [NativeDisableParallelForRestriction][ReadOnly] public float   FluidDensity;
        [NativeDisableParallelForRestriction][ReadOnly] public float   DynamicForceFactor;
        [NativeDisableParallelForRestriction][ReadOnly] public float   DynamicForcePower;
        [NativeDisableParallelForRestriction][ReadOnly] public float   SkinDrag;
        [NativeDisableParallelForRestriction][ReadOnly] public float   VelocityDotPower;

        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<int>   ObjectIndices;
        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<float> ObjDynamicForceCoeffs;
        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<float> ObjDynamicForcePowerCoeffs;
        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<float> ObjSkinFrictionDragCoeffs;
        
        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<float>   WaterHeights;
        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<Vector3> WaterFlows;
        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<Vector3> WaterNormals;

        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<Vector3> WorldVertices;

        [NativeDisableParallelForRestriction] [ReadOnly] public NativeArray<int> Triangles;

        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<Vector3> ObjRigidbodyCoMs;
        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<Vector3> ObjRigidbodyLinearVels;
        [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<Vector3> ObjRigidbodyAngularVels;

        [NativeDisableParallelForRestriction] public NativeArray<Vector3> P0s;

        /// <summary>
        ///     0 - Under water
        ///     1 - At surface
        ///     2 - Above water
        ///     3 - Disabled
        ///     4 - Destroyed
        /// </summary>
        public NativeArray<byte> States;

        public NativeArray<Vector3> ResultForces;
        public NativeArray<Vector3> ResultPoints;

        public NativeArray<float>   Areas;
        public NativeArray<Vector3> Normals;
        public NativeArray<Vector3> Velocities;

        public NativeArray<float> Distances;

        private int _vertIndex0;
        private int _vertIndex1;
        private int _vertIndex2;


        public void Execute(int i)
        {
            if (States[i] >= 3)
            {
                return;
            }

            _vertIndex0 = Triangles[i * 3];
            _vertIndex1 = Triangles[i * 3 + 1];
            _vertIndex2 = Triangles[i * 3 + 2];
            
            Vector3 P0       = WorldVertices[_vertIndex0];
            Vector3 P1       = WorldVertices[_vertIndex1];
            Vector3 P2       = WorldVertices[_vertIndex2];

            float wh_P0 = WaterHeights[_vertIndex0];
            float wh_P1 = WaterHeights[_vertIndex1];
            float wh_P2 = WaterHeights[_vertIndex2];
            
            float d0  = P0.y - wh_P0;
            float d1  = P1.y - wh_P1;
            float d2  = P2.y - wh_P2;

            //All vertices are above water
            if (d0 >= 0 && d1 >= 0 && d2 >= 0)
            {
                int pi = i * 6;
                P0s[pi + 0] = P0;
                P0s[pi + 1] = P1;
                P0s[pi + 2] = P2;
            
                P0s[pi + 3] = ZeroVector;
                P0s[pi + 4] = ZeroVector;
                P0s[pi + 5] = ZeroVector;
            
                ResultPoints[i] = ZeroVector;
                ResultForces[i] = ZeroVector;
            
                States[i] = 2;
            
                return;
            }
            
            // All vertices are underwater
            if (d0 <= 0 && d1 <= 0 && d2 <= 0)
            {
                ThreeUnderWater(P0, P1, P2, d0, d1, d2, 0, 1, 2, i);
            }
            // 1 or 2 vertices are below the water
            else
            {
                // v0 > v1
                if (d0 > d1)
                {
                    // v0 > v2
                    if (d0 > d2)
                    {
                        // v1 > v2                  
                        if (d1 > d2)
                        {
                            if (d0 > 0 && d1 < 0 && d2 < 0)
                            {
                                // 0 1 2
                                TwoUnderWater(P0, P1, P2, d0, d1, d2, 0, 1, 2, i);
                            }
                            else if (d0 > 0 && d1 > 0 && d2 < 0)
                            {
                                // 0 1 2
                                OneUnderWater(P0, P1, P2, d0, d1, d2, 0, 1, 2, i);
                            }
                        }
                        // v2 > v1
                        else
                        {
                            if (d0 > 0 && d2 < 0 && d1 < 0)
                            {
                                // 0 2 1
                                TwoUnderWater(P0, P2, P1, d0, d2, d1, 0, 2, 1, i);
                            }
                            else if (d0 > 0 && d2 > 0 && d1 < 0)
                            {
                                // 0 2 1
                                OneUnderWater(P0, P2, P1, d0, d2, d1, 0, 2, 1, i);
                            }
                        }
                    }
                    // v2 > v0
                    else
                    {
                        if (d2 > 0 && d0 < 0 && d1 < 0)
                        {
                            // 2 0 1
                            TwoUnderWater(P2, P0, P1, d2, d0, d1, 2, 0, 1, i);
                        }
                        else if (d2 > 0 && d0 > 0 && d1 < 0)
                        {
                            // 2 0 1
                            OneUnderWater(P2, P0, P1, d2, d0, d1, 2, 0, 1, i);
                        }
                    }
                }
                // v0 < v1
                else
                {
                    // v0 < v2
                    if (d0 < d2)
                    {
                        // v1 < v2
                        if (d1 < d2)
                        {
                            if (d2 > 0 && d1 < 0 && d0 < 0)
                            {
                                // 2 1 0
                                TwoUnderWater(P2, P1, P0, d2, d1, d0, 2, 1, 0, i);
                            }
                            else if (d2 > 0 && d1 > 0 && d0 < 0)
                            {
                                // 2 1 0
                                OneUnderWater(P2, P1, P0, d2, d1, d0, 2, 1, 0, i);
                            }
                        }
                        // v2 < v1
                        else
                        {
                            if (d1 > 0 && d2 < 0 && d0 < 0)
                            {
                                // 1 2 0
                                TwoUnderWater(P1, P2, P0, d1, d2, d0, 1, 2, 0, i);
                            }
                            else if (d1 > 0 && d2 > 0 && d0 < 0)
                            {
                                // 1 2 0
                                OneUnderWater(P1, P2, P0, d1, d2, d0, 1, 2, 0, i);
                            }
                        }
                    }
                    // v2 < v0
                    else
                    {
                        if (d1 > 0 && d0 < 0 && d2 < 0)
                        {
                            // 1 0 2
                            TwoUnderWater(P1, P0, P2, d1, d0, d2, 1, 0, 2, i);
                        }
                        else if (d1 > 0 && d0 > 0 && d2 < 0)
                        {
                            // 1 0 2
                            OneUnderWater(P1, P0, P2, d1, d0, d2, 1, 0, 2, i);
                        }
                    }
                }
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 CalculateForces(Vector3 p0, Vector3 p1, Vector3 p2, float dist0, float dist1, float dist2,
            int                                 index,
            out Vector3                         center, out float area, out float distanceToSurface)
        {
            int objIndex = ObjectIndices[index];
            
            center = (p0 + p1 + p2) / 3f;
            Vector3 u              = p1 - p0;
            Vector3 v              = p2 - p0;
            Vector3 crossUV        = Vector3.Cross(u, v);
            float   crossMagnitude = Mathf.Sqrt(crossUV.x * crossUV.x + crossUV.y * crossUV.y + crossUV.z * crossUV.z);
            Vector3 normal         = crossMagnitude < 0.00001f ? Vector3.zero : crossUV / crossMagnitude;
            Normals[index] = normal;

            Vector3 p = center - ObjRigidbodyCoMs[objIndex];
            Vector3 velocity = Vector3.Cross(ObjRigidbodyAngularVels[objIndex], p) + ObjRigidbodyLinearVels[objIndex];
            Vector3 waterNormalVector = GlobalUpVector;

            area = crossMagnitude * 0.5f;
            distanceToSurface = 0;
            if (area > 1e-7)
            {
                Vector3 f0         = p0 - center;
                Vector3 f1         = p1 - center;
                Vector3 f2         = p2 - center;
                float   doubleArea = area * 2f;
                float   w0         = Vector3.Cross(f1, f2).magnitude / doubleArea;
                float   w1         = Vector3.Cross(f2, f0).magnitude / doubleArea;
                float   w2         = 1f - (w0 + w1);
                Debug.Assert(w0 + w1 + w2 > 0.95f && w0 + w1 + w2 < 1.05f);

                if (SimulateWaterNormals)
                {
                    Vector3 n0 = WaterNormals[_vertIndex0];
                    Vector3 n1 = WaterNormals[_vertIndex1];
                    Vector3 n2 = WaterNormals[_vertIndex2];

                    distanceToSurface =
                        w0 * dist0 * Vector3.Dot(n0, GlobalUpVector) +
                        w1 * dist1 * Vector3.Dot(n1, GlobalUpVector) +
                        w2 * dist2 * Vector3.Dot(n2, GlobalUpVector);

                    waterNormalVector = w0 * n0 + w1 * n1 + w2 * n2;
                }
                else
                {
                    distanceToSurface =
                        w0 * dist0 +
                        w1 * dist1 +
                        w2 * dist2;
                }

                if (SimulateWaterFlow)
                {
                    velocity += w0 * -WaterFlows[_vertIndex0] +
                                w1 * -WaterFlows[_vertIndex1] +
                                w2 * -WaterFlows[_vertIndex2];
                }
            }
            else
            {
                States[index] = 2;
                return Vector3.zero;
            }


            float   velocityMagnitude  = Vector3.Magnitude(velocity);
            Vector3 velocityNormalized = Vector3.Normalize(velocity);

            Velocities[index] = velocity;
            Areas[index]      = area;

            distanceToSurface = distanceToSurface < 0 ? 0 : distanceToSurface;

            float densityArea = FluidDensity * area;

            // Buoyant force
            Vector3 buoyantForce = ZeroVector;
            if (CalculateBuoyantForces)
            {
                buoyantForce = distanceToSurface * densityArea * Gravity.y * waterNormalVector *
                               Vector3.Dot(normal, waterNormalVector);
            }

            Vector3 dynamicForce = ZeroVector;
            float   dot          = Vector3.Dot(normal, velocityNormalized);
            if (CalculateDynamicForces)
            {
                // Dynamic force
                if (dot < -0.0001f || dot > 0.0001f)
                {
                    dot = Mathf.Sign(dot) * Mathf.Pow(Mathf.Abs(dot), VelocityDotPower);
                }

                if (DynamicForcePower < 0.9999f || DynamicForcePower > 1.0001f)
                {
                    dynamicForce = -dot * Mathf.Pow(velocityMagnitude, DynamicForcePower * ObjDynamicForcePowerCoeffs[objIndex]) 
                                        * densityArea * DynamicForceFactor * ObjDynamicForceCoeffs[objIndex] * normal;
                }
                else
                {
                    dynamicForce = -dot * velocityMagnitude * densityArea * DynamicForceFactor * ObjDynamicForceCoeffs[objIndex] * normal;
                }
            }

            if (CalculateSkinDrag)
            {
                float absDot = dot < 0 ? -dot : dot;
                dynamicForce += (1f - absDot) * SkinDrag * ObjSkinFrictionDragCoeffs[objIndex] * densityArea * -velocity;
            }

            return buoyantForce + dynamicForce;
        }
        
        private void ThreeUnderWater(Vector3 p0,    Vector3 p1,    Vector3 p2,
        float                            dist0, float   dist1, float   dist2,
        int                              i0,    int     i1,    int     i2, int index)
        {
            States[index] = 0;

            int i = index * 6;
            P0s[i]     = p0;
            P0s[i + 1] = p1;
            P0s[i + 2] = p2;

            P0s[i + 3]     = Vector3.zero;
            P0s[i + 4] = Vector3.zero;
            P0s[i + 5] = Vector3.zero;

            Vector3 resultForce = CalculateForces(p0, p1, p2, -dist0, -dist1, -dist2, index, out Vector3 center,
                                                  out float area, out float distanceToSurface);
            ResultForces[index] = resultForce;
            ResultPoints[index] = center;
            Distances[index]    = distanceToSurface;
        }
        
        private void TwoUnderWater(Vector3 p0,    Vector3 p1,    Vector3 p2,
            float                          dist0, float   dist1, float   dist2,
            int                            i0,    int     i1,    int     i2, int index)
        {
            States[index] = 1;

            Vector3 H,  M,  L, IM, IL;
            float   hH, hM, hL;
            int     mIndex;

            // H is always at position 0
            H = p0;

            // Find the index of M
            mIndex = i0 - 1;
            if (mIndex < 0)
            {
                mIndex = 2;
            }

            // Heights to the water
            hH = dist0;

            if (i1 == mIndex)
            {
                M = p1;
                L = p2;

                hM = dist1;
                hL = dist2;
            }
            else
            {
                M = p2;
                L = p1;

                hM = dist2;
                hL = dist1;
            }

            IM = -hM / (hH - hM) * (H - M) + M;
            IL = -hL / (hH - hL) * (H - L) + L;

            int i = index * 6;
            P0s[i]     = M;
            P0s[i + 1] = IM;
            P0s[i + 2] = IL;

            P0s[i + 3] = M;
            P0s[i + 4] = IL;
            P0s[i + 5] = L;

            // Generate tris
            Vector3 force0 = CalculateForces(M,                   IM,              IL, -hM, 0, 0, index, 
                                             out Vector3 center0, out float area0, out float distanceToSurface0);
            Vector3 force1 = CalculateForces(M,                   IL,              L, -hM, 0, -hL, index, 
                                             out Vector3 center1, out float area1, out float distanceToSurface1);

            float weight0 = area0 / (area0 + area1);
            float weight1 = 1f - weight0;
            ResultForces[index] = force0 + force1;
            ResultPoints[index] = center0 * weight0 + center1 * weight1;
            Distances[index]    = distanceToSurface0 * weight0 + distanceToSurface1 * weight1;
        }
        
        private void OneUnderWater(Vector3 p0,    Vector3 p1,    Vector3 p2,
            float                          dist0, float   dist1, float   dist2,
            int                            i0,    int     i1,    int     i2, int index)
        {
            States[index] = 1;

            Vector3 H,  M,  L, JM, JH;
            float   hH, hM, hL;

            L = p2;

            // Find the index of H
            int hIndex = i2 + 1;
            if (hIndex > 2)
            {
                hIndex = 0;
            }

            // Get heights to water
            hL = dist2;

            if (i1 == hIndex)
            {
                H = p1;
                M = p0;

                hH = dist1;
                hM = dist0;
            }
            else
            {
                H = p0;
                M = p1;

                hH = dist0;
                hM = dist1;
            }

            JM = -hL / (hM - hL) * (M - L) + L;
            JH = -hL / (hH - hL) * (H - L) + L;

            int i = index * 6;
            P0s[i]     = L;
            P0s[i + 1] = JH;
            P0s[i + 2] = JM;

            P0s[i + 3] = Vector3.zero;
            P0s[i + 4] = Vector3.zero;
            P0s[i + 5] = Vector3.zero;

            // Generate tris
            Vector3 resultForce = CalculateForces(L,                  JH,             JM, -hL, 0, 0, index, 
                                                  out Vector3 center, out float area, out float distanceToSurface);
            ResultForces[index] = resultForce;
            ResultPoints[index] = center;
            Distances[index]    = distanceToSurface;
        }
    }
}