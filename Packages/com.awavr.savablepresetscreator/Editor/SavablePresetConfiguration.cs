using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace AwAVR.SavablePresetsCreator
{
    [CreateAssetMenu(fileName = "New Savable Preset Configuration", menuName = "AwA/Savable Preset Configuration")]
    public class SavablePresetConfiguration : ScriptableObject
    {
        public string Name;
        public AnimatorController Controller;
        public VRCExpressionParameters VRCParameters;
        public List<SavablePreset> SavablePresets;
    }
}