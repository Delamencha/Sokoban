using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sokoban
{
    public class GameController : MonoBehaviour
    {
        private readonly BoardModel boardModel = new BoardModel();
        private BoardRenderer boardRenderer;
        private RuntimeLevelEditor levelEditor;
        private List<LevelData> levels = new List<LevelData>();
        private int currentLevelIndex;
        private bool gameActive;
        private bool editorMode;
        private bool saveProgressForCurrentLevel;

        [SerializeField] private bool showInfoPanelOnStartPage = true;
        [SerializeField] private MainMenuView mainMenuView;
        [SerializeField] private PopUpView popUpView;

        private GameObject infoPanel;
        private RectTransform infoPanelRect;
        private Text titleText;
        private Text statusText;
        private GameObject levelListScrollPanel;
        private Transform levelListContent;
        private GameObject playPanel;
        private GameObject editorScrollPanel;
        private GameObject editorPanel;
        private InputField widthInput;
        private InputField heightInput;
        private InputField nameInput;

        private void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();

            boardRenderer = new GameObject("Board Renderer").AddComponent<BoardRenderer>();
            levelEditor = new RuntimeLevelEditor(boardRenderer, SetStatus);
            CreateUi();
            ReloadLevels();
            ShowStartPage();
        }

        private void Update()
        {
            if (!gameActive && !editorMode)
            {
                return;
            }

            if (editorMode)
            {
                if (Input.GetMouseButton(0) && !IsPointerOverUi())
                {
                    levelEditor.TryPaintFromScreenPosition(Input.mousePosition);
                }

                return;
            }

            if (InputController.TryGetMoveDirection(out Vector2Int direction))
            {
                MoveResult result = boardModel.TryMove(direction);
                if (result != MoveResult.Blocked)
                {
                    boardRenderer.Render(boardModel);
                    if (result == MoveResult.Solved)
                    {
                        HandleLevelSolved();
                    }
                    else
                    {
                        SetStatus("移动：" + result);
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartLevel();
            }

            if (Input.GetKeyDown(KeyCode.Z))
            {
                Undo();
            }
        }

        private void ReloadLevels()
        {
            levels = LevelSaveSystem.LoadAllLevels();
            currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, Mathf.Max(0, levels.Count - 1));
        }

        private void LoadLevel(int index)
        {
            if (levels.Count == 0)
            {
                SetStatus("没有可加载的关卡。");
                return;
            }

            currentLevelIndex = (index + levels.Count) % levels.Count;
            LevelData level = levels[currentLevelIndex].Clone();
            List<string> errors = LevelValidator.Validate(level);
            if (errors.Count > 0)
            {
                SetStatus("关卡无效：\n" + string.Join("\n", errors));
                return;
            }

            boardModel.Load(level);
            boardRenderer.Render(boardModel);
            saveProgressForCurrentLevel = true;
            ConfigureInfoPanelForDefaultPage();
            titleText.text = level.displayName;
            SetStatus("WASD/方向键移动，Z 撤销，R 重开。");
        }

        private void ShowStartPage()
        {
            gameActive = false;
            editorMode = false;
            ConfigureInfoPanelForStartPage();
            mainMenuView.Show();
            levelListScrollPanel.SetActive(false);
            playPanel.SetActive(false);
            editorScrollPanel.SetActive(false);

            if (!GameProgressSaveSystem.HasProgress())
            {
                SetStatus("欢迎游玩。当前没有关卡进度存档。");
            }
            else if (GameProgressSaveSystem.AreAllLevelsCompleted(levels))
            {
                SetStatus("已完成全部关卡。继续游戏将从最后一关开始。");
            }
            else
            {
                SetStatus("继续游戏将从下一关开始。");
            }
        }

        private void ContinueGame()
        {
            ReloadLevels();
            mainMenuView.Hide();
            levelListScrollPanel.SetActive(false);
            playPanel.SetActive(true);
            editorScrollPanel.SetActive(false);
            editorMode = false;
            gameActive = true;
            LoadLevel(GameProgressSaveSystem.GetContinueLevelIndex(levels));
        }

        private void ClearProgress()
        {
            GameProgressSaveSystem.ClearProgress();
            SetStatus("已清空当前完成关卡信息。");
        }

        private void RequestClearProgress()
        {
            Confirm("是否确认删除当前关卡进度？", ClearProgress);
        }

        private void ShowLevelList()
        {
            ReloadLevels();
            RebuildLevelList();
            ConfigureInfoPanelForDefaultPage();
            mainMenuView.Hide();
            levelListScrollPanel.SetActive(true);
            playPanel.SetActive(false);
            editorScrollPanel.SetActive(false);
            titleText.text = "关卡列表";
            SetStatus("点击关卡可直接开始游玩。");
        }

        private void RebuildLevelList()
        {
            ClearChildren(levelListContent);

            if (levels.Count == 0)
            {
                CreateText("Empty Level List", levelListContent, "当前没有可用关卡", 16, TextAnchor.MiddleLeft);
                CreateButton("返回开始页面", levelListContent, ShowStartPage);
                return;
            }

            for (int i = 0; i < levels.Count; i++)
            {
                int levelIndex = i;
                string state = GameProgressSaveSystem.IsLevelCompleted(levels[i]) ? "已完成" : "未完成";
                string label = (i + 1) + ". " + levels[i].displayName + " - " + state;
                CreateButton(label, levelListContent, () => StartSelectedLevel(levelIndex));
            }

            CreateButton("返回开始页面", levelListContent, ShowStartPage);
        }

        private void StartSelectedLevel(int levelIndex)
        {
            levelListScrollPanel.SetActive(false);
            mainMenuView.Hide();
            playPanel.SetActive(true);
            editorScrollPanel.SetActive(false);
            editorMode = false;
            gameActive = true;
            LoadLevel(levelIndex);
        }

        private void ShowReservedEditorEntry()
        {
            SetStatus("关卡编辑入口预留中。当前请进入游戏后使用侧栏的“编辑关卡”。");
        }

        private void ExitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void HandleLevelSolved()
        {
            if (saveProgressForCurrentLevel)
            {
                GameProgressSaveSystem.MarkLevelCompleted(levels[currentLevelIndex]);
                SetStatus("完成关卡！进度已保存。");
            }
            else
            {
                SetStatus("试玩关卡完成！试玩不会写入通关进度。");
            }
        }

        private void RestartLevel()
        {
            boardModel.Restart();
            boardRenderer.Render(boardModel);
            SetStatus("已重开当前关卡。");
        }

        private void Undo()
        {
            if (boardModel.Undo())
            {
                boardRenderer.Render(boardModel);
                SetStatus("已撤销一步。");
            }
            else
            {
                SetStatus("没有可撤销的步骤。");
            }
        }

        private void EnterEditor()
        {
            gameActive = false;
            editorMode = true;
            ConfigureInfoPanelForDefaultPage();
            mainMenuView.Hide();
            levelListScrollPanel.SetActive(false);
            playPanel.SetActive(false);
            editorScrollPanel.SetActive(true);
            saveProgressForCurrentLevel = false;

            LevelData source = levels.Count > 0 ? levels[currentLevelIndex].Clone() : LevelData.CreateBlank(8, 8);
            source.displayName = source.displayName + " Copy";
            source.id = source.id + "_copy";
            levelEditor.SetLevel(source);
            nameInput.text = source.displayName;
            widthInput.text = source.width.ToString();
            heightInput.text = source.height.ToString();
            titleText.text = "关卡编辑器";
            SetStatus("选择工具后在棋盘上拖拽绘制。");
        }

        private void ExitEditor()
        {
            gameActive = true;
            editorMode = false;
            editorScrollPanel.SetActive(false);
            levelListScrollPanel.SetActive(false);
            playPanel.SetActive(true);
            LoadLevel(currentLevelIndex);
        }

        private void RequestExitEditor()
        {
            Confirm("是否已保存当前关卡？若直接退出，则放弃当前关卡的编辑进度。", ExitEditor);
        }

        private void CreateBlankEditorLevel()
        {
            int width = ParseInput(widthInput, 8, 3, 24);
            int height = ParseInput(heightInput, 8, 3, 16);
            levelEditor.CreateBlank(width, height);
            levelEditor.CurrentLevel.displayName = string.IsNullOrWhiteSpace(nameInput.text) ? "Custom Level" : nameInput.text;
            levelEditor.CurrentLevel.id = CreateId(levelEditor.CurrentLevel.displayName);
            titleText.text = "关卡编辑器";
        }

        private void SaveEditorLevel()
        {
            levelEditor.CurrentLevel.displayName = string.IsNullOrWhiteSpace(nameInput.text) ? "Custom Level" : nameInput.text;
            levelEditor.CurrentLevel.id = CreateId(levelEditor.CurrentLevel.displayName);

            try
            {
                levelEditor.Save();
                ReloadLevels();
            }
            catch (Exception exception)
            {
                SetStatus("保存失败：\n" + exception.Message);
            }
        }

        private void TestEditorLevel()
        {
            levelEditor.CurrentLevel.displayName = string.IsNullOrWhiteSpace(nameInput.text) ? "Unsaved Test" : nameInput.text;
            levelEditor.CurrentLevel.id = CreateId(levelEditor.CurrentLevel.displayName);
            List<string> errors = levelEditor.Validate();
            if (errors.Count > 0)
            {
                SetStatus("无法试玩：\n" + string.Join("\n", errors));
                return;
            }

            editorMode = false;
            editorScrollPanel.SetActive(false);
            levelListScrollPanel.SetActive(false);
            playPanel.SetActive(true);
            gameActive = true;
            boardModel.Load(levelEditor.CurrentLevel.Clone());
            boardRenderer.Render(boardModel);
            saveProgressForCurrentLevel = false;
            ConfigureInfoPanelForDefaultPage();
            titleText.text = levelEditor.CurrentLevel.displayName + " (试玩)";
            SetStatus("正在试玩编辑中的关卡。保存后会出现在关卡列表中。");
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private void Confirm(string message, Action onConfirm)
        {
            if (popUpView == null)
            {
                onConfirm?.Invoke();
                return;
            }

            popUpView.Show(message, onConfirm);
        }

        private void ConfigureInfoPanelForStartPage()
        {
            if (infoPanel == null || infoPanelRect == null)
            {
                return;
            }

            infoPanel.SetActive(showInfoPanelOnStartPage);
            titleText.gameObject.SetActive(false);

            infoPanelRect.anchorMin = new Vector2(1f, 0f);
            infoPanelRect.anchorMax = new Vector2(1f, 0f);
            infoPanelRect.pivot = new Vector2(1f, 0f);
            infoPanelRect.sizeDelta = new Vector2(320f, 92f);
            infoPanelRect.anchoredPosition = new Vector2(-18f, 18f);
        }

        private void ConfigureInfoPanelForDefaultPage()
        {
            if (infoPanel == null || infoPanelRect == null)
            {
                return;
            }

            infoPanel.SetActive(true);
            titleText.gameObject.SetActive(true);

            infoPanelRect.anchorMin = new Vector2(0f, 0f);
            infoPanelRect.anchorMax = new Vector2(0f, 1f);
            infoPanelRect.pivot = new Vector2(0.5f, 0.5f);
            infoPanelRect.sizeDelta = new Vector2(280f, 0f);
            infoPanelRect.anchoredPosition = new Vector2(140f, 0f);
        }

        private void CreateUi()
        {
            Canvas canvas = new GameObject("Runtime UI").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            GameObject root = CreatePanel("Root Panel", canvas.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(280f, 0f), new Vector2(140f, 0f));
            infoPanel = root;
            infoPanelRect = root.GetComponent<RectTransform>();
            VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            titleText = CreateText("Title", root.transform, "Sokoban", 24, TextAnchor.MiddleLeft);
            statusText = CreateText("Status", root.transform, string.Empty, 15, TextAnchor.UpperLeft);
            statusText.GetComponent<LayoutElement>().preferredHeight = 96f;

            if (mainMenuView == null)
            {
                mainMenuView = MainMenuView.CreateRuntime(root.transform);
            }

            mainMenuView.Initialize(ContinueGame, ShowLevelList, RequestClearProgress, ShowReservedEditorEntry, ExitGame);

            if (popUpView != null)
            {
                popUpView.Hide();
            }

            levelListScrollPanel = CreateScrollView("Level List Scroll", root.transform, out levelListContent);

            playPanel = CreateStack("Play Panel", root.transform);
            CreateButton("上一关", playPanel.transform, () => LoadLevel(currentLevelIndex - 1));
            CreateButton("下一关", playPanel.transform, () => LoadLevel(currentLevelIndex + 1));
            CreateButton("重开 (R)", playPanel.transform, RestartLevel);
            CreateButton("撤销 (Z)", playPanel.transform, Undo);
            CreateButton("编辑关卡", playPanel.transform, EnterEditor);

            editorScrollPanel = CreateScrollView("Editor Scroll", root.transform, out Transform editorContent);
            editorPanel = editorContent.gameObject;
            CreateText("Editor Hint", editorPanel.transform, "编辑器", 18, TextAnchor.MiddleLeft);
            nameInput = CreateInputField("Name Input", editorPanel.transform, "Custom Level", "关卡名称");
            widthInput = CreateInputField("Width Input", editorPanel.transform, "8", "宽度");
            heightInput = CreateInputField("Height Input", editorPanel.transform, "8", "高度");
            CreateButton("新建指定尺寸", editorPanel.transform, CreateBlankEditorLevel);
            CreateToolButton("地板", EditorTool.Floor);
            CreateToolButton("墙", EditorTool.Wall);
            CreateToolButton("空白", EditorTool.Empty);
            CreateToolButton("玩家", EditorTool.Player);
            CreateToolButton("箱子", EditorTool.Box);
            CreateToolButton("目标点", EditorTool.Target);
            CreateToolButton("擦除实体", EditorTool.EraseEntity);
            CreateButton("试玩当前关卡", editorPanel.transform, TestEditorLevel);
            CreateButton("保存关卡", editorPanel.transform, SaveEditorLevel);
            CreateButton("退出编辑器", editorPanel.transform, RequestExitEditor);
            levelListScrollPanel.SetActive(false);
            playPanel.SetActive(false);
            editorScrollPanel.SetActive(false);
        }

        private void CreateToolButton(string label, EditorTool tool)
        {
            CreateButton(label, editorPanel.transform, () => levelEditor.SetTool(tool));
        }

        private static GameObject CreateStack(string name, Transform parent)
        {
            GameObject stack = new GameObject(name);
            stack.transform.SetParent(parent, false);
            VerticalLayoutGroup layout = stack.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = stack.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return stack;
        }

        private static GameObject CreateScrollView(string name, Transform parent, out Transform content)
        {
            GameObject scrollObject = new GameObject(name);
            scrollObject.transform.SetParent(parent, false);
            Image background = scrollObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.12f);

            LayoutElement layoutElement = scrollObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 410f;
            layoutElement.flexibleHeight = 1f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObject.transform, false);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport.transform, false);
            content = contentObject.transform;
            VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6f;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter contentFitter = contentObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();

            ConfigureFillRect(viewportRect, 0f, 0f);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            scrollRectTransform.sizeDelta = Vector2.zero;
            return scrollObject;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
            return panel;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;

            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = Mathf.Max(32f, size + 12f);
            return text;
        }

        private static Button CreateButton(string label, Transform parent, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(label + " Button");
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.30f, 0.92f);

            Button button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(action);

            Text text = CreateText("Text", buttonObject.transform, label, 16, TextAnchor.MiddleCenter);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 34f;
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        private static InputField CreateInputField(string name, Transform parent, string value, string placeholder)
        {
            GameObject inputObject = new GameObject(name);
            inputObject.transform.SetParent(parent, false);
            Image image = inputObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.92f);

            InputField input = inputObject.AddComponent<InputField>();
            Text text = CreateText("Text", inputObject.transform, value, 15, TextAnchor.MiddleLeft);
            text.color = Color.black;
            Text placeholderText = CreateText("Placeholder", inputObject.transform, placeholder, 15, TextAnchor.MiddleLeft);
            placeholderText.color = new Color(0f, 0f, 0f, 0.45f);

            ConfigureFillRect(text.GetComponent<RectTransform>(), 8f, 4f);
            ConfigureFillRect(placeholderText.GetComponent<RectTransform>(), 8f, 4f);

            input.textComponent = text;
            input.placeholder = placeholderText;
            input.text = value;

            LayoutElement layout = inputObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 34f;
            return input;
        }

        private static void ConfigureFillRect(RectTransform rect, float horizontalPadding, float verticalPadding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        }

        private static int ParseInput(InputField input, int fallback, int min, int max)
        {
            if (input == null || !int.TryParse(input.text, out int value))
            {
                return fallback;
            }

            return Mathf.Clamp(value, min, max);
        }

        private static string CreateId(string displayName)
        {
            string source = string.IsNullOrWhiteSpace(displayName) ? "custom_level" : displayName.ToLowerInvariant();
            char[] chars = source.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray();
            return new string(chars).Trim('_');
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }
    }
}
