using UnityEditor;
using UnityEngine;

namespace AwAVR.SavablePresetsCreator
{
    [CustomEditor(typeof(SavablePresetConfiguration))]
    public class SavablePresetConfigurationEditor : Editor
    {
        private SavablePresetConfiguration configuration;

        private void OnEnable()
        {
            configuration = (SavablePresetConfiguration)target;
        }

        public override void OnInspectorGUI()
        {
            // DrawDefaultInspector();
            EditorGUILayout.HelpBox("Please use the Savable Preset Creator window to edit this file.",
                MessageType.Info);
            if (GUILayout.Button("Open Editor Window"))
            {
                SavablePresetsCreator.OpenWithConfiguration(configuration);
            }
        }
    }
}