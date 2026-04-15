using UnityEngine;
using Project.Core.Runtime.Framework;

namespace Project.Core.Runtime.Managers
{
    public sealed class AudioManager : ManagerBehaviour
    {
        public void PlayBGM(string bgmId, float fadeDuration) => Debug.Log($"PlayBGM: {bgmId}, fade={fadeDuration}");
        public void PlayAmbience(string ambienceId, float fadeDuration) => Debug.Log($"PlayAmbience: {ambienceId}, fade={fadeDuration}");
        public void PlaySFX(string sfxId, float volume) => Debug.Log($"PlaySFX 2D: {sfxId}, volume={volume}");
        public void PlaySFX(string sfxId, Vector3 position) => Debug.Log($"PlaySFX 3D: {sfxId}, position={position}");
        public void PlayToolEffect(ToolType toolType, string effectId) => Debug.Log($"PlayToolEffect: {toolType}, effect={effectId}");
        public void PlayRecording(RecordedAudio recordedAudio) => Debug.Log($"PlayRecording: {recordedAudio?.recordingId}");
    }
}
