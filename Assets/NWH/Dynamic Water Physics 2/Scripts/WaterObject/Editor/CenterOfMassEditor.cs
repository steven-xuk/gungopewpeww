#if UNITY_EDITOR
using NWH.DWP2.NUI;
using NWH.DWP2.WaterObjects;
using UnityEditor;

namespace NWH.DWP2
{
    [CustomEditor(typeof(CenterOfMass))]
    [CanEditMultipleObjects]
    public class CenterOfMassEditor : DWP_NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }
            
            drawer.Info("This script has been deprecated and will be removed in the future. Use VariableCenterOfMass instead.", 
                        MessageType.Warning);

            drawer.Field("centerOfMassOffset");
            drawer.Field("showCOM");

            drawer.EndEditor(this);
            return true;
        }
    }
}

#endif
