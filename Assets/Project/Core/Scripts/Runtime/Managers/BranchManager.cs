using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Project.Core.Runtime.Framework;

namespace Project.Core.Runtime.Managers
{
    public sealed class BranchManager : ManagerBehaviour, ISaveable<BranchSaveData>
    {
        private readonly Dictionary<string, BranchObservationSaveData> observations = new();
        private int baseSeed;

        public void Initialize(int seed)
        {
            baseSeed = seed;
            observations.Clear();
            Random.InitState(seed);
        }

        public void RegisterTemperature(string interactableId, float value) => GetOrCreate(interactableId).temperature = value;
        public void RegisterEMF(string interactableId, int value) => GetOrCreate(interactableId).emfLevel = value;
        public void RegisterUVResult(string interactableId, bool value) => GetOrCreate(interactableId).uvDetected = value;
        public void RegisterAudioResult(string interactableId, Project.Core.Runtime.Framework.AudioType audioType) => GetOrCreate(interactableId).audioType = audioType;

        public bool TryGetObservation(string interactableId, out BranchObservationSaveData observation)
        {
            return observations.TryGetValue(interactableId, out observation);
        }

        public float GetTemperature(string interactableId) => observations.TryGetValue(interactableId, out var value) ? value.temperature : 0f;
        public int GetEMF(string interactableId) => observations.TryGetValue(interactableId, out var value) ? value.emfLevel : 0;

        public BranchSaveData GetSaveData()
        {
            return new BranchSaveData
            {
                baseSeed = baseSeed,
                observations = observations.Values.Select(CloneObservation).ToList()
            };
        }

        public void LoadState(BranchSaveData data)
        {
            observations.Clear();
            if (data == null) return;
            baseSeed = data.baseSeed;
            foreach (var observation in data.observations)
            {
                observations[observation.interactableId] = CloneObservation(observation);
            }
        }

        private BranchObservationSaveData GetOrCreate(string interactableId)
        {
            if (!observations.TryGetValue(interactableId, out var observation))
            {
                observation = new BranchObservationSaveData { interactableId = interactableId };
                observations[interactableId] = observation;
            }
            return observation;
        }

        private static BranchObservationSaveData CloneObservation(BranchObservationSaveData source)
        {
            return new BranchObservationSaveData
            {
                interactableId = source.interactableId,
                temperature = source.temperature,
                emfLevel = source.emfLevel,
                uvDetected = source.uvDetected,
                audioType = source.audioType
            };
        }
    }
}
