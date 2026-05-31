using TMPro;
using UnityEngine;

namespace Sokoban
{
    public class PlayHudView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text levelNameText;
        [SerializeField] private TMP_Text operationHintText;
        [SerializeField] private TMP_Text statusText;

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

    }
}
