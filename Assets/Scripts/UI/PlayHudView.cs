using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class PlayHudView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text levelNameText;
        [SerializeField] private TMP_Text operationHintText;
        [SerializeField] private TMP_Text statusText;

        public static PlayHudView CreateRuntime(Transform parent)
        {
            GameObject hudObject = new GameObject("Play HUD");
            hudObject.transform.SetParent(parent, false);

            VerticalLayoutGroup layout = hudObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;

            PlayHudView view = hudObject.AddComponent<PlayHudView>();
            view.root = hudObject;
            view.levelNameText = CreateText("Level Name", hudObject.transform, "Sokoban", 24, TextAlignmentOptions.Left);
            view.operationHintText = CreateText("Operation Hint", hudObject.transform, string.Empty, 14, TextAlignmentOptions.Left);
            view.statusText = CreateText("Status", hudObject.transform, string.Empty, 15, TextAlignmentOptions.TopLeft);

            LayoutElement statusLayout = view.statusText.GetComponent<LayoutElement>();
            statusLayout.preferredHeight = 96f;
            return view;
        }

        public void Show()
        {
            GetRoot().SetActive(true);
        }

        public void Hide()
        {
            GetRoot().SetActive(false);
        }

        public void SetLevelName(string levelName)
        {
            if (levelNameText != null)
            {
                levelNameText.text = levelName ?? string.Empty;
            }
        }

        public void SetOperationHint(string operationHint)
        {
            if (operationHintText != null)
            {
                operationHintText.text = operationHint ?? string.Empty;
            }
        }

        public void SetStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = status ?? string.Empty;
            }
        }

        public void SetLevelNameVisible(bool visible)
        {
            if (levelNameText != null)
            {
                levelNameText.gameObject.SetActive(visible);
            }
        }

        private GameObject GetRoot()
        {
            return root != null ? root : gameObject;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, int size, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;

            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = Mathf.Max(32f, size + 12f);
            return text;
        }
    }
}
