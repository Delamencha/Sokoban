using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class RuntimeEditorView : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_InputField widthInput;
        [SerializeField] private TMP_InputField heightInput;
        [SerializeField] private Button createBlankButton;
        [SerializeField] private Button floorToolButton;
        [SerializeField] private Button wallToolButton;
        [SerializeField] private Button emptyToolButton;
        [SerializeField] private Button playerToolButton;
        [SerializeField] private Button boxToolButton;
        [SerializeField] private Button targetToolButton;
        [SerializeField] private Button eraseEntityToolButton;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button testButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button exitButton;

        private const string DefaultLevelName = "Custom Level";
        private const int MinDimension = 1;
        private const int MaxDimension = 20;
        private static readonly Color SelectedToolColor = new Color(0.65f, 0.85f, 1f, 1f);

        private readonly Button[] toolButtons = new Button[7];
        private readonly Color[] toolButtonDefaultColors = new Color[7];
        private bool isFilteringDimensionInput;
        private bool hasToolButtonDefaultColors;
        private EditorTool selectedTool = EditorTool.Floor;
        private Action<EditorTool> onSelectTool;

        private void Awake()
        {
            NameInputFilter.Configure(nameInput);
            ConfigureDimensionInput(widthInput);
            ConfigureDimensionInput(heightInput);
            Hide();
        }

        public void Initialize(
            Action onCreateBlank,
            Action<EditorTool> onSelectTool,
            Action onUndo,
            Action onTest,
            Action onSave,
            Action onExit)
        {
            this.onSelectTool = onSelectTool;
            CacheToolButtons();
            CacheToolButtonDefaultColors();
            BindButton(createBlankButton, onCreateBlank);
            BindButton(floorToolButton, () => SelectTool(EditorTool.Floor));
            BindButton(wallToolButton, () => SelectTool(EditorTool.Wall));
            BindButton(emptyToolButton, () => SelectTool(EditorTool.Empty));
            BindButton(playerToolButton, () => SelectTool(EditorTool.Player));
            BindButton(boxToolButton, () => SelectTool(EditorTool.Box));
            BindButton(targetToolButton, () => SelectTool(EditorTool.Target));
            BindButton(eraseEntityToolButton, () => SelectTool(EditorTool.EraseEntity));
            BindButton(undoButton, onUndo);
            BindButton(testButton, onTest);
            BindButton(saveButton, onSave);
            BindButton(exitButton, onExit);
            RefreshToolSelection();
        }

        public void Show()
        {
            GameObject containerObject = GetContainer();
            GameObject rootObject = GetRoot();
            containerObject.SetActive(true);
            SetParentsActive(rootObject.transform);
            rootObject.SetActive(true);
            containerObject.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            GetRoot().SetActive(false);
        }

        public void SetLevelFields(string levelName, int width, int height)
        {
            SetInputText(nameInput, NameInputFilter.Sanitize(string.IsNullOrWhiteSpace(levelName) ? DefaultLevelName : levelName));
            SetInputText(widthInput, ClampDimension(width).ToString());
            SetInputText(heightInput, ClampDimension(height).ToString());
        }

        public string GetLevelName(string fallback = DefaultLevelName)
        {
            if (nameInput == null || string.IsNullOrWhiteSpace(nameInput.text))
            {
                return fallback;
            }

            return NameInputFilter.Sanitize(nameInput.text);
        }

        public int GetWidth(int fallback)
        {
            return ParseDimensionInput(widthInput, fallback);
        }

        public int GetHeight(int fallback)
        {
            return ParseDimensionInput(heightInput, fallback);
        }

        public void SelectTool(EditorTool tool)
        {
            selectedTool = tool;
            onSelectTool?.Invoke(tool);
            RefreshToolSelection();
        }

        private GameObject GetRoot()
        {
            return root != null ? root : gameObject;
        }

        private GameObject GetContainer()
        {
            return container != null ? container : GetRoot();
        }

        private void CacheToolButtons()
        {
            toolButtons[(int)EditorTool.Floor] = floorToolButton;
            toolButtons[(int)EditorTool.Wall] = wallToolButton;
            toolButtons[(int)EditorTool.Empty] = emptyToolButton;
            toolButtons[(int)EditorTool.Player] = playerToolButton;
            toolButtons[(int)EditorTool.Box] = boxToolButton;
            toolButtons[(int)EditorTool.Target] = targetToolButton;
            toolButtons[(int)EditorTool.EraseEntity] = eraseEntityToolButton;
        }

        private void CacheToolButtonDefaultColors()
        {
            if (hasToolButtonDefaultColors)
            {
                return;
            }

            for (int i = 0; i < toolButtons.Length; i++)
            {
                Image image = GetButtonImage(toolButtons[i]);
                toolButtonDefaultColors[i] = image != null ? image.color : Color.white;
            }

            hasToolButtonDefaultColors = true;
        }

        private void RefreshToolSelection()
        {
            CacheToolButtons();
            CacheToolButtonDefaultColors();
            for (int i = 0; i < toolButtons.Length; i++)
            {
                Image image = GetButtonImage(toolButtons[i]);
                if (image != null)
                {
                    image.color = i == (int)selectedTool ? SelectedToolColor : toolButtonDefaultColors[i];
                }
            }
        }

        private static Image GetButtonImage(Button button)
        {
            return button != null ? button.targetGraphic as Image : null;
        }

        private static int ParseDimensionInput(TMP_InputField input, int fallback)
        {
            if (input == null || !int.TryParse(input.text, out int value))
            {
                return ClampDimension(fallback);
            }

            return ClampDimension(value);
        }

        private void ConfigureDimensionInput(TMP_InputField input)
        {
            if (input == null)
            {
                return;
            }

            input.characterLimit = 2;
            input.onValidateInput = ValidateDimensionCharacter;

            if (input == widthInput)
            {
                input.onValueChanged.RemoveListener(HandleWidthValueChanged);
                input.onValueChanged.AddListener(HandleWidthValueChanged);
                input.onEndEdit.RemoveListener(HandleWidthEndEdit);
                input.onEndEdit.AddListener(HandleWidthEndEdit);
            }
            else if (input == heightInput)
            {
                input.onValueChanged.RemoveListener(HandleHeightValueChanged);
                input.onValueChanged.AddListener(HandleHeightValueChanged);
                input.onEndEdit.RemoveListener(HandleHeightEndEdit);
                input.onEndEdit.AddListener(HandleHeightEndEdit);
            }
        }

        private char ValidateDimensionCharacter(string text, int charIndex, char addedChar)
        {
            return addedChar >= '0' && addedChar <= '9' ? addedChar : '\0';
        }

        private void HandleWidthValueChanged(string value)
        {
            FilterDimensionInput(widthInput, value);
        }

        private void HandleHeightValueChanged(string value)
        {
            FilterDimensionInput(heightInput, value);
        }

        private void HandleWidthEndEdit(string _)
        {
            ClampDimensionInput(widthInput);
        }

        private void HandleHeightEndEdit(string _)
        {
            ClampDimensionInput(heightInput);
        }

        private void FilterDimensionInput(TMP_InputField input, string value)
        {
            if (isFilteringDimensionInput || input == null)
            {
                return;
            }

            string filtered = SanitizeDimensionText(value);
            if (filtered == value)
            {
                return;
            }

            isFilteringDimensionInput = true;
            input.text = filtered;
            input.caretPosition = filtered.Length;
            input.selectionAnchorPosition = filtered.Length;
            input.selectionFocusPosition = filtered.Length;
            input.ForceLabelUpdate();
            isFilteringDimensionInput = false;
        }

        private void ClampDimensionInput(TMP_InputField input)
        {
            if (input == null)
            {
                return;
            }

            input.text = ParseDimensionInput(input, MinDimension).ToString();
        }

        private static string SanitizeDimensionText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string digits = string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character >= '0' && character <= '9')
                {
                    digits += character;
                }
            }

            if (string.IsNullOrEmpty(digits))
            {
                return string.Empty;
            }

            if (!int.TryParse(digits, out int dimension))
            {
                return MaxDimension.ToString();
            }

            return ClampDimension(dimension).ToString();
        }

        private static int ClampDimension(int value)
        {
            return Mathf.Clamp(value, MinDimension, MaxDimension);
        }

        private static void SetInputText(TMP_InputField input, string value)
        {
            if (input != null)
            {
                input.text = input.contentType == TMP_InputField.ContentType.Name ? NameInputFilter.Sanitize(value) : value;
            }
        }

        private static void SetParentsActive(Transform child)
        {
            Transform parent = child.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(true);
                }

                parent = parent.parent;
            }
        }

        private static void BindButton(Button button, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(() => action());
            }
        }

    }
}
