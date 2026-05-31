using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class LevelListItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text indexText;
        [SerializeField] private TMP_Text levelNameText;
        [SerializeField] private TMP_Text completionStateText;
        [SerializeField] private TMP_Text mainFlowStateText;

        public void SetData(int index, string levelName, bool isCompleted, bool isInMainFlow, Action onClick)
        {
            string completionState = isCompleted ? "已完成" : "未完成";
            string mainFlowState = isInMainFlow ? "主流程" : "非主流程";

            if (indexText != null)
            {
                indexText.text = (index + 1).ToString();
            }

            if (levelNameText != null)
            {
                levelNameText.text = levelName ?? string.Empty;
            }

            if (completionStateText != null)
            {
                completionStateText.text = completionState;
            }
            else if (levelNameText != null)
            {
                levelNameText.text = (index + 1) + ". " + (levelName ?? string.Empty) + " - " + completionState + " - " + mainFlowState;
            }

            if (mainFlowStateText != null)
            {
                mainFlowStateText.text = mainFlowState;
            }
            else if (completionStateText != null)
            {
                completionStateText.text = completionState + " / " + mainFlowState;
            }

            BindButton(onClick);
        }

        private void BindButton(Action onClick)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }
        }

    }
}
