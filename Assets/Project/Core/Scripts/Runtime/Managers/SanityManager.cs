using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Project.Core.Runtime.Framework;

namespace Project.Core.Runtime.Managers
{
    public sealed class SanityManager : ManagerBehaviour, ISaveable<SanitySaveData>
    {
        public event Action<int, int> OnSanityChanged;
        public event Action OnSanityDepleted;
        public event Action<int> OnSanityLow;

        public int CurrentSanity { get; private set; }
        public int MaxSanity { get; private set; }
        public float SanityRatio => MaxSanity <= 0 ? 0f : (float)CurrentSanity / MaxSanity;

        public async UniTask Initialize(int initialValue)
        {
            MaxSanity = Mathf.Max(1, initialValue);
            CurrentSanity = Mathf.Clamp(initialValue, 0, MaxSanity);
            NotifyChanged();
            await UniTask.Yield();
        }

        public void SetMaxSanity(int value)
        {
            MaxSanity = Mathf.Max(1, value);
            CurrentSanity = Mathf.Clamp(CurrentSanity, 0, MaxSanity);
            NotifyChanged();
        }

        public void ReduceSanity(int amount)
        {
            if (amount <= 0) return;
            CurrentSanity = Mathf.Clamp(CurrentSanity - amount, 0, MaxSanity);
            NotifyChanged();
            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.PlaySanDamageEffect(amount);
            if (CurrentSanity <= 0) OnSanityDepleted?.Invoke();
        }

        public void RestoreSanity(int amount)
        {
            if (amount <= 0) return;
            CurrentSanity = Mathf.Clamp(CurrentSanity + amount, 0, MaxSanity);
            NotifyChanged();
        }

        public async UniTask LoadState(SanitySaveData data)
        {
            ApplyState(data);
            await UniTask.Yield();
        }

        public SanitySaveData GetSaveData()
        {
            return new SanitySaveData { currentSanity = CurrentSanity, maxSanity = MaxSanity };
        }

        public async UniTask ProcessDelayedPenalty(string penaltyId, float delaySeconds)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds));
            ReduceSanity(1);
        }

        void ISaveable<SanitySaveData>.LoadState(SanitySaveData data) => ApplyState(data);

        private void ApplyState(SanitySaveData data)
        {
            if (data == null) return;
            MaxSanity = Mathf.Max(1, data.maxSanity);
            CurrentSanity = Mathf.Clamp(data.currentSanity, 0, MaxSanity);
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            OnSanityChanged?.Invoke(CurrentSanity, MaxSanity);
            if (SanityRatio <= 0.3f) OnSanityLow?.Invoke(CurrentSanity);
            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.UpdateSanDisplay(CurrentSanity, MaxSanity);
        }
    }
}
