using UnityEngine;
using Project.Core.Runtime.Framework;

namespace Project.Core.Runtime.Managers
{
    public sealed class AudioManager : ManagerBehaviour
    {
        public void PlayBGM(string bgmId, float fadeDuration) => Debug.Log($"PlayBGM: {bgmId}, fade={fadeDuration}");
        public void PlayAmbience(string ambienceId, float fadeDuration) => Debug.Log($"PlayAmbience: {ambienceId}, fade={fadeDuration}");
        public void PlaySFX(string sfxId, float volume) => Debug.Log($"PlaySFX 2D: {sfxId}, volume={volume}");

        /// <summary>主音量（0~1）。会直接落到 <see cref="AudioListener.volume"/>（现在音频还多是桩，先把链路搭好）。</summary>
        public float MasterVolume { get; private set; } = 0.8f;

        /// <summary>音乐/BGM 音量（0~1）。真正接 BGM 时用它乘到 AudioSource 上。</summary>
        public float MusicVolume { get; private set; } = 0.6f;

        /// <summary>人物/语音音量（0~1）。</summary>
        public float VoiceVolume { get; private set; } = 1f;

        public void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            AudioListener.volume = MasterVolume;
            Debug.Log($"[Audio] 主音量 = {MasterVolume:0.##}");
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            Debug.Log($"[Audio] 音乐音量 = {MusicVolume:0.##}");
        }

        public void SetVoiceVolume(float value)
        {
            VoiceVolume = Mathf.Clamp01(value);
            Debug.Log($"[Audio] 人物音量 = {VoiceVolume:0.##}");
        }
        public void PlaySFX(string sfxId, Vector3 position) => Debug.Log($"PlaySFX 3D: {sfxId}, position={position}");
        public void PlayVoice(string voiceId, float volume) => Debug.Log($"PlayVoice: {voiceId}, volume={volume}");
        public void PlayToolEffect(ToolType toolType, string effectId) => Debug.Log($"PlayToolEffect: {toolType}, effect={effectId}");
        public void PlayRecording(RecordedAudio recordedAudio) => Debug.Log($"PlayRecording: {recordedAudio?.recordingId}");
    }
}
