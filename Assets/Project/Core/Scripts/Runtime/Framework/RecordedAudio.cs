using System;
using UnityEngine;

namespace Project.Core.Runtime.Framework
{
    [Serializable]
    public class RecordedAudio
    {
        public string recordingId;
        public AudioClip clip;
        public AudioType audioType;
        public float duration;
    }
}
