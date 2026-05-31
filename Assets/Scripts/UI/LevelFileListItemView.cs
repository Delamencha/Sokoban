using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class LevelFileListItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text indexText;
        [SerializeField] private TMP_Text levelNameText;
        [SerializeField] private TMP_Text sourceText;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(0.75f, 0.85f, 1f, 1f);

        public void SetData(int index, LevelFileEntry entry, bool isInMainFlow, Action onClick)
        {
            if (indexText != null)
            {
                indexText.text = (index + 1).ToString();
            }

            if (levelNameText != null)
            {
                levelNameText.text = entry != null && entry.level != null ? entry.level.displayName : string.Empty;
            }

            if (sourceText != null)
            {
                sourceText.text = isInMainFlow ? "主流程" : "非主流程";
            }

            BindButton(onClick);
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectedIndicator != null)
            {
                selectedIndicator.SetActive(selected);
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = selected ? selectedColor : normalColor;
            }
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
