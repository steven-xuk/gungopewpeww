using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using Random = System.Random;

namespace NWH.DWP2.WaterObjects
{
    [RequireComponent(typeof(WaterObject))]
    public class WaterObjectDebug : MonoBehaviour
    {
        public int dataStart;
        public int dataEnd;
        public int dataLength;

        private WaterObjectManager _wom;
        private WaterObject        _wo;
        
        void Start()
        {
            _wom = WaterObjectManager.Instance;
            _wo  = GetComponent<WaterObject>();
        }


        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            #if UNITY_EDITOR

            dataStart  = _wo.TriDataStart;
            dataEnd    = _wo.TriDataEnd;
            dataLength = _wo.TriDataLength;
            
            for (int i = dataStart; i < dataEnd; i++)
            {
                int triIndex  = i;
                int vertIndex = i * 3;
                int pIndex    = i * 6;
                int state     = _wom.States[triIndex];
                
                if(state >= 2) continue;

                Color lineColor = state == 0 ? Color.cyan : state == 1 ? Color.green : Color.white;


                // Draw force point
                Vector3 forcePoint = _wom.ForcePoints[triIndex];
                Gizmos.DrawWireSphere(forcePoint, 0.01f);
                Handles.Label(forcePoint, $"T{i}-S{state}");
                
                // Draw normal
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(forcePoint, forcePoint + _wom.Normals[triIndex] * 0.1f);
                
                // Draw P0s
                Gizmos.color = lineColor;
                Vector3 p00 = _wom.P0S[pIndex];
                Vector3 p01 = _wom.P0S[pIndex + 1];
                Vector3 p02 = _wom.P0S[pIndex + 2];
                
                Gizmos.DrawLine(p00, p01);
                Gizmos.DrawLine(p01, p02);
                Gizmos.DrawLine(p02, p00);
                
                // Draw indices
                // Handles.Label(p00, $"{i}:P00");
                // Handles.Label(p01, $"{i}:P01");
                // Handles.Label(p02, $"{i}:P02");

                // Draw P1s
                Gizmos.color = lineColor;
                Vector3 p10 = _wom.P0S[pIndex + 3];
                Vector3 p11 = _wom.P0S[pIndex + 4];
                Vector3 p12 = _wom.P0S[pIndex + 5];
                
                if (state == 1)
                {
                    Gizmos.DrawLine(p10, p11);
                    Gizmos.DrawLine(p11, p12);
                    Gizmos.DrawLine(p12, p10);
                    
                    // Handles.Label(p10, $"{i}:P10");
                    // Handles.Label(p11, $"{i}:P11");
                    // Handles.Label(p12, $"{i}:P12");
                }

                // Draw vertices
                int v0i = _wom.Triangles[vertIndex];
                int v1i = _wom.Triangles[vertIndex + 1];
                int v2i = _wom.Triangles[vertIndex + 2];

                Vector3 v0 = _wom.WorldVertices[v0i];
                Vector3 v1 = _wom.WorldVertices[v1i];
                Vector3 v2 = _wom.WorldVertices[v2i];
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(v0, 0.01f);
                Gizmos.DrawSphere(v1, 0.01f);
                Gizmos.DrawSphere(v2, 0.01f);
                
                Handles.color = Color.yellow;
                // Handles.Label(v0, $"T{i}V0");
                // Handles.Label(v0, $"T{i}V1");
                // Handles.Label(v0, $"T{i}V2");

                // Draw vertex water heights
                Gizmos.color = Color.magenta;
                Vector3 up = Vector3.up;
                Gizmos.DrawLine(v0, v0 - up * (v0.y - _wom.WaterHeights[v0i]));
                Gizmos.DrawLine(v1, v1 - up * (v1.y - _wom.WaterHeights[v1i]));
                Gizmos.DrawLine(v2, v2 - up * (v2.y - _wom.WaterHeights[v2i]));

                // Visualize distance to water
                Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
                Gizmos.DrawLine(forcePoint, forcePoint + Vector3.up * _wom.DistancesToSurface[triIndex]);
            }
            #endif
        }
    }
}

