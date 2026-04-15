using System;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;

namespace Project.Core.Runtime.Managers
{
    public sealed class GameManager : ManagerBehaviour
    {
        public event Action<GameState, GameState> OnStateChanged;

        public GameState CurrentState { get; private set; } = GameState.None;
        public GameState PreviousState { get; private set; } = GameState.None;

        public void SwitchState(GameState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            var oldState = CurrentState;
            OnStateExit(oldState);
            PreviousState = oldState;
            CurrentState = newState;
            OnStateEnter(newState);
            OnStateChanged?.Invoke(oldState, newState);
        }

        public void RevertState()
        {
            SwitchState(PreviousState);
        }

        public async UniTask StartNewGame()
        {
            if (Services.TryGet<SaveManager>(out var saveManager))
            {
                await saveManager.DeleteAllSaves();
            }

            SwitchState(GameState.Init);
            await UniTask.Yield();
        }

        public async UniTask ContinueGame()
        {
            if (Services.TryGet<SaveManager>(out var saveManager))
            {
                await saveManager.LoadLatestAsync();
            }
        }

        public async UniTask TriggerGameOver()
        {
            SwitchState(GameState.GameOver);
            await UniTask.Yield();
        }

        public async UniTask TriggerVictory()
        {
            SwitchState(GameState.Victory);
            await UniTask.Yield();
        }

        public async UniTask QuickSave()
        {
            if (Services.TryGet<SaveManager>(out var saveManager))
            {
                await saveManager.QuickSaveAsync();
            }
        }

        public async UniTask QuickLoad()
        {
            if (Services.TryGet<SaveManager>(out var saveManager))
            {
                await saveManager.QuickLoadAsync();
            }
        }

        private void OnStateEnter(GameState state)
        {
        }

        private void OnStateExit(GameState state)
        {
        }
    }
}
