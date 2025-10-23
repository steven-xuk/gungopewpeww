using UnityEngine;

namespace NWH.DWP2.WaterObjects
{
    [RequireComponent(typeof(Rigidbody))]
    [ExecuteInEditMode]
    public class CenterOfMass : MonoBehaviour
    {
        public Vector3 centerOfMassOffset = Vector3.zero;
        public bool    showCOM            = true;

        private Vector3   centerOfMass;
        private Vector3   prevOffset = Vector3.zero;
        private Rigidbody rb;


        private void Start()
        {
            Debug.LogWarning("This script has been deprecated and will be removed in the future. Use VariableCenterOfMass instead.");
            rb           = GetComponent<Rigidbody>();
            centerOfMass = rb.centerOfMass;
        }


        private void Update()
        {
            if (centerOfMassOffset != prevOffset)
            {
                rb.centerOfMass = centerOfMass + centerOfMassOffset;
            }

            prevOffset = centerOfMassOffset;
        }


        private void OnDrawGizmos()
        {
            if (showCOM && rb != null)
            {
                float radius = 0.1f;
                try
                {
                    radius = GetComponent<MeshFilter>().sharedMesh.bounds.size.z / 10f;
                }
                catch
                {
                }

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(rb.transform.TransformPoint(rb.centerOfMass), radius);
            }
        }
    }
}