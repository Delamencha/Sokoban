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

        public static LevelListItemView CreateRuntime(Transform parent)
        {
            GameObject itemObject = new GameObject("Level List Item");
            itemObject.transform.SetParent(parent, false);
            Image image = itemObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.30f, 0.92f);

            HorizontalLayoutGroup layout = itemObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 0, 0);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            LayoutElement itemLayout = itemObject.AddComponent<LayoutElement>();
            itemLayout.preferredHeight = 36f;

            LevelListItemView view = itemObject.AddComponent<LevelListItemView>();
            view.button = itemObject.AddComponent<Button>();
            view.indexText = CreateText("Index", itemObject.transform, 14, TextAlignmentOptions.Left, 34f);
            view.levelNameText = CreateText("Level Name", itemObject.transform, 14, TextAlignmentOptions.Left, 150f);
            view.completionStateText = CreateText("Completion State", itemObject.transform, 14, TextAlignmentOptions.Right, 64f);
            return view;
        }

        public void SetData(int index, string levelName, bool isCompleted, Action onClick)
        {
            string completionState = isCompleted ? "已完成" : "未完成";

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
                levelNameText.text = (index + 1) + ". " + (levelName ?? string.Empty) + " - " + completionState;
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

        private static TMP_Text CreateText(string name, Transform parent, int size, TextAlignmentOptions alignment, float preferredWidth)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;

            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = preferredWidth > 100f ? 1f : 0f;
            return text;
        }
    }
}
