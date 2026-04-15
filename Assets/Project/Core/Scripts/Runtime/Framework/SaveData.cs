using System;
using System.Collections.Generic;

namespace Project.Core.Runtime.Framework
{
    [Serializable]
    public class SanitySaveData
    {
        public int currentSanity;
        public int maxSanity;
    }

    [Serializable]
    public class InventorySaveData
    {
        public List<string> inventoryItemIds = new();
        public List<string> containmentItemIds = new();
        public List<string> equippedToolIds = new();
    }

    [Serializable]
    public class FlagSaveData
    {
        public List<string> keys = new();
        public List<bool> values = new();
    }

    [Serializable]
    public class BranchObservationSaveData
    {
        public string interactableId;
        public float temperature;
        public int emfLevel;
        public bool uvDetected;
        public AudioType audioType;
    }

    [Serializable]
    public class BranchSaveData
    {
        public int baseSeed;
        public List<BranchObservationSaveData> observations = new();
    }

    [Serializable]
    public class EvidenceSaveData
    {
        public int goalCount;
        public List<string> collectedEvidenceIds = new();
    }

    [Serializable]
    public class SaveData
    {
        public string slotId;
        public string sceneName;
        public long timestampTicks;
        public GameState currentState;
        public SanitySaveData sanity = new();
        public InventorySaveData inventory = new();
        public FlagSaveData flags = new();
        public BranchSaveData branch = new();
        public EvidenceSaveData evidence = new();
    }

    [Serializable]
    public class SaveSlotInfo
    {
        public string slotId;
        public string sceneName;
        public long timestampTicks;
    }
}
