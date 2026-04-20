using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Narrative.Scripts;
using UnityEngine;

namespace Project.Samples.Stage2Breach.Scripts
{
    public sealed class Stage2BreachVnDebugUi : MonoBehaviour
    {
        private string currentSpeaker = string.Empty;
        private string currentText = string.Empty;
        private readonly List<VNChoiceViewData> currentChoices = new();
        private bool vnVisible;

        private void Update()
        {
            if (!vnVisible)
            {
                return;
            }

            if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space)) && currentChoices.Count == 0)
            {
                if (Services.TryGet<VNDirector>(out var vnDirector))
                {
                    vnDirector.Advance().Forget();
                }
            }
        }

        private void OnGUI()
        {
            if (!vnVisible)
            {
                return;
            }

            var area = new Rect(40f, Screen.height - 260f, Screen.width - 80f, 220f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(string.IsNullOrWhiteSpace(currentSpeaker) ? "旁白" : currentSpeaker);
            GUILayout.Space(8f);
            GUILayout.Label(string.IsNullOrWhiteSpace(currentText) ? "..." : currentText, GUILayout.ExpandHeight(true));
            GUILayout.Space(12f);

            if (currentChoices.Count > 0)
            {
                var choiceSnapshot = currentChoices.ToArray();
                foreach (var choice in choiceSnapshot)
                {
                    if (choice != null && GUILayout.Button(choice.Text, GUILayout.Height(32f)))
                    {
                        if (Services.TryGet<UIManager>(out var uiManager))
                        {
                            uiManager.SelectVNChoice(choice.ChoiceId);
                        }

                        break;
                    }
                }
            }
            else
            {
                GUILayout.Label("右键或空格推进");
            }

            GUILayout.EndArea();
        }

        public void ShowPanel()
        {
            vnVisible = true;
        }

        public void HidePanel()
        {
            vnVisible = false;
            currentSpeaker = string.Empty;
            currentText = string.Empty;
            currentChoices.Clear();
        }

        public void ShowLine(string speakerName, string text)
        {
            vnVisible = true;
            currentSpeaker = speakerName;
            currentText = text;
            currentChoices.Clear();
        }

        public void ShowChoices(IReadOnlyList<VNChoiceViewData> choices)
        {
            currentChoices.Clear();
            if (choices == null)
            {
                return;
            }

            foreach (var choice in choices)
            {
                if (choice != null)
                {
                    currentChoices.Add(choice);
                }
            }
        }

        public void HideChoices()
        {
            currentChoices.Clear();
        }
    }
}
