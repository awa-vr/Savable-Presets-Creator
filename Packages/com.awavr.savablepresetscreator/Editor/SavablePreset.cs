using System;
using System.Collections.Generic;
using UnityEngine;

namespace AwAVR.SavablePresetsCreator
{
    [Serializable]
    public class SavablePreset
    {
        public string Name;
        public List<string> Parameters = new List<string>();
    }
}