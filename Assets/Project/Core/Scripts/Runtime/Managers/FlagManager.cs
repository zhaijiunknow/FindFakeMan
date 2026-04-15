using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Project.Core.Runtime.Framework;

namespace Project.Core.Runtime.Managers
{
    public sealed class FlagManager : ManagerBehaviour, ISaveable<FlagSaveData>
    {
        private readonly Dictionary<string, bool> flags = new();

        public void Set(string flagId, bool value = true)
        {
            if (!string.IsNullOrWhiteSpace(flagId))
            {
                flags[flagId] = value;
            }
        }

        public void SetMultiple(Dictionary<string, bool> values)
        {
            if (values == null) return;
            foreach (var pair in values)
            {
                Set(pair.Key, pair.Value);
            }
        }

        public bool Get(string flagId)
        {
            return !string.IsNullOrWhiteSpace(flagId) && flags.TryGetValue(flagId, out var value) && value;
        }

        public bool Has(string flagId) => flags.ContainsKey(flagId);

        public bool Remove(string flagId)
        {
            return !string.IsNullOrWhiteSpace(flagId) && flags.Remove(flagId);
        }

        public void ClearFlags()
        {
            flags.Clear();
        }

        public IReadOnlyList<string> GetAllFlagIds()
        {
            return flags.Keys.ToList();
        }

        public FlagSaveData GetSaveData()
        {
            var data = new FlagSaveData();
            foreach (var pair in flags)
            {
                data.keys.Add(pair.Key);
                data.values.Add(pair.Value);
            }
            return data;
        }

        public void LoadState(FlagSaveData data)
        {
            flags.Clear();
            if (data == null) return;
            for (var i = 0; i < Mathf.Min(data.keys.Count, data.values.Count); i++)
            {
                flags[data.keys[i]] = data.values[i];
            }
        }
    }
}
