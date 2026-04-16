using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Core.Runtime.Framework;
using Project.Narrative.Scripts;

namespace Project.Core.Runtime.Managers
{
    public sealed class SaveManager : ManagerBehaviour
    {
        private const string QuickSaveSlotId = "quick";
        private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        public async UniTask SaveAsync(string slotId)
        {
            var data = CreateSaveData();
            data.slotId = slotId;
            data.timestampTicks = DateTime.UtcNow.Ticks;
            Directory.CreateDirectory(SaveDirectory);
            var path = GetSlotPath(slotId);
            var json = JsonUtility.ToJson(data, true);
            await UniTask.RunOnThreadPool(() => File.WriteAllText(path, json));
        }

        public async UniTask<SaveData> LoadAsync(string slotId)
        {
            var path = GetSlotPath(slotId);
            if (!File.Exists(path)) return null;
            var json = await UniTask.RunOnThreadPool(() => File.ReadAllText(path));
            var data = JsonUtility.FromJson<SaveData>(json);
            ApplySaveData(data);
            return data;
        }

        public async UniTask<SaveData> LoadLatestAsync()
        {
            var latest = GetAllSaveSlots().OrderByDescending(x => x.timestampTicks).FirstOrDefault();
            return latest == null ? null : await LoadAsync(latest.slotId);
        }

        public bool HasSave(string slotId) => File.Exists(GetSlotPath(slotId));
        public bool HasAnySave() => Directory.Exists(SaveDirectory) && Directory.EnumerateFiles(SaveDirectory, "*.json").Any();

        public List<SaveSlotInfo> GetAllSaveSlots()
        {
            if (!Directory.Exists(SaveDirectory)) return new List<SaveSlotInfo>();

            var slots = new List<SaveSlotInfo>();
            foreach (var file in Directory.EnumerateFiles(SaveDirectory, "*.json"))
            {
                var json = File.ReadAllText(file);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) continue;
                slots.Add(new SaveSlotInfo { slotId = data.slotId, sceneName = data.sceneName, timestampTicks = data.timestampTicks });
            }
            return slots;
        }

        public async UniTask DeleteSave(string slotId)
        {
            var path = GetSlotPath(slotId);
            if (File.Exists(path))
            {
                await UniTask.RunOnThreadPool(() => File.Delete(path));
            }
        }

        public async UniTask DeleteAllSaves()
        {
            if (Directory.Exists(SaveDirectory))
            {
                await UniTask.RunOnThreadPool(() => Directory.Delete(SaveDirectory, true));
            }
        }

        public async UniTask QuickSaveAsync() => await SaveAsync(QuickSaveSlotId);
        public async UniTask<SaveData> QuickLoadAsync() => await LoadAsync(QuickSaveSlotId);
        public bool HasQuickSave() => HasSave(QuickSaveSlotId);

        public SaveData CreateSaveData()
        {
            var data = new SaveData
            {
                sceneName = SceneManager.GetActiveScene().name,
                timestampTicks = DateTime.UtcNow.Ticks,
                currentState = Services.TryGet<GameManager>(out var gameManager) ? gameManager.CurrentState : GameState.None
            };

            if (Services.TryGet<SanityManager>(out var sanityManager)) data.sanity = sanityManager.GetSaveData();
            if (Services.TryGet<InventoryManager>(out var inventoryManager)) data.inventory = inventoryManager.GetSaveData();
            if (Services.TryGet<FlagManager>(out var flagManager)) data.flags = flagManager.GetSaveData();
            if (Services.TryGet<BranchManager>(out var branchManager)) data.branch = branchManager.GetSaveData();
            if (Services.TryGet<EvidenceManager>(out var evidenceManager)) data.evidence = evidenceManager.GetSaveData();
            if (Services.TryGet<VNDirector>(out var vnDirector)) data.visualNovel = vnDirector.GetSaveData();
            return data;
        }

        public void ApplySaveData(SaveData data)
        {
            if (data == null) return;
            if (Services.TryGet<SanityManager>(out var sanityManager)) sanityManager.LoadState(data.sanity).Forget();
            if (Services.TryGet<InventoryManager>(out var inventoryManager)) inventoryManager.LoadState(data.inventory).Forget();
            if (Services.TryGet<FlagManager>(out var flagManager)) flagManager.LoadState(data.flags);
            if (Services.TryGet<BranchManager>(out var branchManager)) branchManager.LoadState(data.branch);
            if (Services.TryGet<EvidenceManager>(out var evidenceManager)) evidenceManager.LoadState(data.evidence);

            var hasVnDirector = Services.TryGet<VNDirector>(out var vnDirector);
            var isVisualNovelState = data.currentState == GameState.VisualNovel;
            if (isVisualNovelState)
            {
                if (hasVnDirector)
                {
                    vnDirector.LoadState(data.visualNovel);
                }
                else if (Services.TryGet<GameManager>(out var vnFallbackGameManager))
                {
                    Debug.LogWarning("Saved state was VisualNovel, but VNDirector is not available. Falling back to Exploration.");
                    vnFallbackGameManager.SwitchState(GameState.Exploration);
                }

                return;
            }

            if (hasVnDirector)
            {
                vnDirector.LoadState(new VNSaveData { isPlaying = false });
            }

            if (Services.TryGet<GameManager>(out var gameManager))
            {
                gameManager.SwitchState(data.currentState);
            }
        }

        private string GetSlotPath(string slotId) => Path.Combine(SaveDirectory, $"{slotId}.json");
    }
}
