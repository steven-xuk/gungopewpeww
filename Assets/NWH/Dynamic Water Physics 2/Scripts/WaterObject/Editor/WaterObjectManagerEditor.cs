#if UNITY_EDITOR
using NWH.DWP2.NUI;
using NWH.DWP2.WaterData;
using NWH.DWP2.WaterObjects;
using UnityEditor;
using UnityEngine;

namespace NWH.DWP2
{
    [CustomEditor(typeof(WaterObjectManager))]
    [CanEditMultipleObjects]
    public class WaterObjectManagerEditor : DWP_NUIEditor
    {
        private WaterObjectManager _waterObjectManager;


        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return true;
            }

            _waterObjectManager = (WaterObjectManager) target;

            // Draw logo texture
            Rect logoRect = drawer.positionRect;
            logoRect.height = 60f;
            drawer.DrawEditorTexture(logoRect, "DWP2/Logos/WaterObjectManagerLogo");
            drawer.AdvancePosition(logoRect.height);

            if (Application.isPlaying)
            {
                string heights = WaterDataProvider.Instance.SupportsWaterHeightQueries() ? "heights, " : "";
                string flows   = WaterDataProvider.Instance.SupportsWaterFlowQueries() ? "velocities, " : "";
                string normals = WaterDataProvider.Instance.SupportsWaterNormalQueries() ? "normals. " : "";
                drawer.Info(
                    $"Retrieving data from {WaterDataProvider.Instance}. Supported data: {heights}{flows}{normals}");

                if (drawer.Button("Synchronize"))
                {
                    WaterObjectManager.Instance.Synchronize();
                }
            }

            // Simulation settings
            drawer.Field("includeInactive");
            drawer.Field("includeMultipleScenes");

            drawer.BeginSubsection("Simulation");
            drawer.Field("fluidDensity", true, "kg/m3");
            drawer.Field("velocityDotPower");
            drawer.Field("finishJobsInOneFrame");
            drawer.Info("FinishJobsInOneFrame improves behavior of low density and fast objects at a slight cost to the performance.");
            drawer.EndSubsection();

            drawer.BeginSubsection("Buoyancy");
            drawer.Field("calculateBuoyancyForces");
            drawer.EndSubsection();

            drawer.BeginSubsection("Hydrodynamics");
            if (drawer.Field("calculateDynamicForces").boolValue)
            {
                drawer.Field("dynamicForceCoefficient");
                drawer.Field("dynamicForcePower");
            }

            drawer.EndSubsection();

            drawer.BeginSubsection("Skin Drag");
            if (drawer.Field("calculateSkinDrag").boolValue)
            {
                drawer.Field("skinFrictionDrag");
            }

            drawer.EndSubsection();

            drawer.BeginSubsection("Debug");
            if (Application.isPlaying)
            {
                int triCount = _waterObjectManager.TriangleCount;
                int woCount  = _waterObjectManager.WaterObjects.Count;
                float avg = woCount == 0 ? 0 : triCount / woCount;
                drawer.Info($"Simulating a total of {triCount} tris on " +
                            $"{woCount} WaterObject(s), avg. {avg} tris per WaterObject.");

                drawer.Label($"Active Tri Count: {_waterObjectManager.ActiveTriCount}");
                drawer.Label($"Active Underwater Tri Count: {_waterObjectManager.ActiveUnderwaterTriCount}");
                drawer.Label($"Active Above Water Tri Count: {_waterObjectManager.ActiveAboveWaterTriCount}");
                drawer.Label($"Disabled Tri Count: {_waterObjectManager.DisabledTriCount}");
                drawer.Label($"Destroyed Tri Count: {_waterObjectManager.DestroyedTriCount}");
                drawer.Label($"Inactive Tri Count: {_waterObjectManager.InactiveTriCount}");
            }
            else
            {
                drawer.Info("Debug info available only in play mode.");
            }

            drawer.EndEditor(this);
            return true;
        }
    }
}

#endif
