using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sokoban
{
    public class GameController : MonoBehaviour
    {
        private const float ReferenceSolutionInputCooldownSeconds = 0.2f;
        private const float ReferenceSolutionHoldDelaySeconds = 0.5f;
        private const float ReferenceSolutionHoldRepeatSeconds = 0.5f;
        private const int ReferenceSolutionNoHeldInput = 0;
        private const int ReferenceSolutionForwardHeldInput = 1;
        private const int ReferenceSolutionBackwardHeldInput = -1;

        private readonly BoardModel boardModel = new BoardModel();
        private BoardRenderer boardRenderer;
        private RuntimeLevelEditor levelEditor;
        private List<LevelData> levels = new List<LevelData>();
        private List<LevelFileEntry> levelFileEntries = new List<LevelFileEntry>();
        private List<LevelFileEntry> mainFlowEntries = new List<LevelFileEntry>();
        private List<LevelFileEntry> activePlayEntries = new List<LevelFileEntry>();
        private List<LevelFileEntry> levelListEntries = new List<LevelFileEntry>();
        private List<LevelFileEntry> filteredLevelFileEntries = new List<LevelFileEntry>();
        private List<LevelFileEntry> editingMainFlowEntries = new List<LevelFileEntry>();
        private string mainFlowEditorBaselineSnapshot = string.Empty;
        private int currentLevelIndex;
        private bool gameActive;
        private bool editorMode;
        private bool testLevelActive;
        private bool referenceSolutionActive;
        private bool saveProgressForCurrentLevel;
        private bool currentLevelSolved;
        private bool editorPaintOperationActive;
        private string editingLevelPath;
        private string editorBaselineSnapshotJson = string.Empty;
        private bool returnToLevelFilePanelOnExit;
        private bool solverActive;
        private string referenceSolutionActions = string.Empty;
        private int referenceSolutionStepIndex;
        private float nextReferenceSolutionForwardInputTime;
        private float nextReferenceSolutionBackwardInputTime;
        private int referenceSolutionHeldInputDirection;
        private float nextReferenceSolutionHeldInputTime;
        private bool referenceSolutionInputLockedUntilRelease;
        private SpriteRenderer runtimeBackgroundRenderer;

        [SerializeField] private Sprite runtimeBackgroundSprite;
        [SerializeField] private Color runtimeBackgroundColor = Color.white;
        [SerializeField] private MainMenuView mainMenuView;
        [SerializeField] private PopUpView popUpView;
        [SerializeField] private CommonMenuView commonMenuView;
        [SerializeField] private CommonEditorMenuView commonEditorMenuView;
        [SerializeField] private TestLevelPanelView testLevelPanelView;
        [SerializeField] private ReferenceSolutionPanelView referenceSolutionPanelView;
        [SerializeField] private PlayHudView playHudView;
        [SerializeField] private LevelListView levelListView;
        [SerializeField] private LevelFilePanelView levelFilePanelView;
        [SerializeField] private MainFlowEditorView mainFlowEditorView;
        [SerializeField] private RuntimeEditorView runtimeEditorView;

        private void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();
            EnsureRuntimeBackground();

            boardRenderer = new GameObject("Board Renderer").AddComponent<BoardRenderer>();
            levelEditor = new RuntimeLevelEditor(boardRenderer, SetStatus);
            InitializeUi();
            ReloadLevels();
            ShowStartPage();
        }

        private void LateUpdate()
        {
            UpdateRuntimeBackground();
        }

        private void Update()
        {
            if (!gameActive && !editorMode)
            {
                return;
            }

            if (referenceSolutionActive)
            {
                UpdateReferenceSolution();
                return;
            }

            if (editorMode)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    EndEditorPaintOperation();
                    ToggleCommonEditorMenu();
                    return;
                }

                if (commonEditorMenuView != null && commonEditorMenuView.IsVisible)
                {
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Z) && IsControlPressed())
                {
                    EndEditorPaintOperation();
                    UndoEditorLevel();
                    return;
                }

                if (Input.GetMouseButtonDown(0) && !IsPointerOverUi())
                {
                    if (levelEditor.ShouldUseRectangleBrush)
                    {
                        editorPaintOperationActive = levelEditor.BeginBrushOperation(Input.mousePosition);
                    }
                    else
                    {
                        editorPaintOperationActive = true;
                        levelEditor.BeginEditOperation();
                        if (levelEditor.TryPaintFromScreenPosition(Input.mousePosition))
                        {
                            MarkCurrentEditorLevelUnverified();
                        }
                    }
                }
                else if (Input.GetMouseButton(0) && editorPaintOperationActive && !IsPointerOverUi())
                {
                    if (levelEditor.ShouldUseRectangleBrush)
                    {
                        levelEditor.UpdateBrushOperation(Input.mousePosition);
                    }
                    else if (levelEditor.TryPaintFromScreenPosition(Input.mousePosition))
                    {
                        MarkCurrentEditorLevelUnverified();
                    }
                }

                if (Input.GetMouseButtonUp(0))
                {
                    if (editorPaintOperationActive && levelEditor.ShouldUseRectangleBrush)
                    {
                        if (levelEditor.EndBrushOperation(Input.mousePosition))
                        {
                            MarkCurrentEditorLevelUnverified();
                        }

                        editorPaintOperationActive = false;
                    }
                    else
                    {
                        EndEditorPaintOperation();
                    }
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (testLevelActive)
                {
                    ToggleTestLevelPanel();
                }
                else
                {
                    ToggleCommonMenu();
                }

                return;
            }

            if (testLevelPanelView != null && testLevelPanelView.IsVisible)
            {
                return;
            }

            if (commonMenuView != null && commonMenuView.IsVisible)
            {
                return;
            }

            if (!currentLevelSolved && InputController.TryGetMoveDirection(out Vector2Int direction))
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
                        //SetStatus("移动：" + result);
                        //SetStatus(" ");
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
            ReloadLevelFileEntries();
            ReloadMainFlowEntries();
            SetActivePlayEntries(mainFlowEntries);
            currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, Mathf.Max(0, levels.Count - 1));
        }

        private void ReloadLevelFileEntries()
        {
            levelFileEntries = LevelSaveSystem.LoadLevelFileEntries();
        }

        private void ReloadMainFlowEntries()
        {
            mainFlowEntries = MainFlowSaveSystem.LoadMainFlowEntries(levelFileEntries);
        }

        private void SetActivePlayEntries(IReadOnlyList<LevelFileEntry> entries)
        {
            activePlayEntries = entries != null ? entries.Where(entry => entry != null && entry.level != null).ToList() : new List<LevelFileEntry>();
            levels = activePlayEntries.Select(entry => entry.level).ToList();
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
            List<string> errors = LevelValidator.ValidateBasic(level);
            if (errors.Count > 0)
            {
                SetStatus("关卡无效：\n" + string.Join("\n", errors));
                return;
            }

            boardModel.Load(level);
            boardRenderer.Render(boardModel);
            saveProgressForCurrentLevel = MainFlowSaveSystem.IsMainFlowLevel(level, mainFlowEntries);
            currentLevelSolved = false;
            ShowPlayHudLevelName(true);
            SetLevelName(level.displayName);
            SetOperationHint("WASD/方向键移动，Z 撤销，R 重开，ESC 菜单。");
            SetStatus("正在游玩。");
        }

        private void ShowStartPage()
        {
            gameActive = false;
            editorMode = false;
            testLevelActive = false;
            referenceSolutionActive = false;
            saveProgressForCurrentLevel = false;
            currentLevelSolved = false;
            boardRenderer.ClearBoard();
            ShowPlayHudLevelName(false);
            SetOperationHint(string.Empty);
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            HideReferenceSolutionPanel();
            levelEditor.ClearUndoHistory();
            editorPaintOperationActive = false;
            mainMenuView.Show();
            levelListView.Hide();
            HideLevelFilePanel();
            HideMainFlowEditor();
            runtimeEditorView.Hide();
            editingLevelPath = string.Empty;
            editorBaselineSnapshotJson = string.Empty;
            returnToLevelFilePanelOnExit = false;

            ReloadLevelFileEntries();
            ReloadMainFlowEntries();
            UpdateMainMenuContinueButtonText();
            if (mainFlowEntries.Count == 0)
            {
                SetStatus("主流程没有可游玩的关卡，请先编辑主流程。");
            }
            else if (!GameProgressSaveSystem.HasProgress())
            {
                SetStatus("欢迎游玩。当前没有关卡进度存档。");
            }
            else if (GameProgressSaveSystem.AreAllLevelsCompleted(mainFlowEntries.Select(entry => entry.level).ToList()))
            {
                SetStatus("已完成全部关卡。继续游戏将从最后一关开始。");
            }
            else
            {
                SetStatus("继续游戏将从下一关开始。");
            }
        }

        private void UpdateMainMenuContinueButtonText()
        {
            if (mainMenuView == null)
            {
                return;
            }

            bool canContinue = mainFlowEntries.Count > 0 && GameProgressSaveSystem.HasProgress();
            mainMenuView.SetContinueButtonText(canContinue ? "继续游戏" : "开始游戏");
        }

        private void ContinueGame()
        {
            ReloadLevelFileEntries();
            ReloadMainFlowEntries();
            if (mainFlowEntries.Count == 0)
            {
                SetStatus("主流程没有可游玩的关卡，请先编辑主流程。");
                return;
            }

            SetActivePlayEntries(mainFlowEntries);
            mainMenuView.Hide();
            levelListView.Hide();
            HideLevelFilePanel();
            HideMainFlowEditor();
            runtimeEditorView.Hide();
            editorMode = false;
            testLevelActive = false;
            gameActive = true;
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            HideReferenceSolutionPanel();
            LoadLevel(GameProgressSaveSystem.GetContinueLevelIndex(levels));
        }

        private void ClearProgress()
        {
            GameProgressSaveSystem.ClearProgress();
            UpdateMainMenuContinueButtonText();
            SetStatus("已清空当前完成关卡信息。");
        }

        private void RequestClearProgress()
        {
            Confirm("是否确认删除当前关卡进度？", ClearProgress);
        }

        private void ShowLevelList()
        {
            ShowLevelList(true, LevelListFilter.All);
        }

        private void ShowMainFlowLevelListFromCommonMenu()
        {
            ShowLevelList(true, LevelListFilter.MainFlow);
        }

        private void ShowLevelList(bool allowFilter, LevelListFilter defaultFilter)
        {
            gameActive = false;
            editorMode = false;
            testLevelActive = false;
            referenceSolutionActive = false;
            saveProgressForCurrentLevel = false;
            currentLevelSolved = false;
            boardRenderer.ClearBoard();
            ReloadLevelFileEntries();
            ReloadMainFlowEntries();
            RebuildLevelList(defaultFilter);
            if (levelListView != null)
            {
                levelListView.SetFilterVisible(allowFilter);
            }

            ShowPlayHudLevelName(true);
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            HideReferenceSolutionPanel();
            mainMenuView.Hide();
            levelListView.Show();
            HideLevelFilePanel();
            HideMainFlowEditor();
            runtimeEditorView.Hide();
            SetLevelName("关卡列表");
            SetOperationHint(string.Empty);
            SetStatus(GetLevelListFilterStatus(defaultFilter));
        }

        private void RebuildLevelList(LevelListFilter filter)
        {
            levelListEntries = GetLevelListEntries(filter);
            SetActivePlayEntries(levelListEntries);
            if (levelListView != null)
            {
                levelListView.SetFilter(filter);
                levelListView.Rebuild(levelListEntries, GameProgressSaveSystem.IsLevelCompleted, IsInMainFlow, StartSelectedLevel);
            }
        }

        private void HandleLevelListFilterChanged(LevelListFilter filter)
        {
            RebuildLevelList(filter);
            SetStatus(GetLevelListFilterStatus(filter));
        }

        private string GetLevelListFilterStatus(LevelListFilter filter)
        {
            switch (filter)
            {
                case LevelListFilter.NotInMainFlow:
                    return "当前显示未加入主流程的关卡。";
                case LevelListFilter.All:
                    return "当前显示全部关卡。";
                default:
                    return "当前显示主流程关卡。";
            }
        }

        private List<LevelFileEntry> GetLevelListEntries(LevelListFilter filter)
        {
            HashSet<string> mainFlowIds = GetMainFlowIdSet();
            switch (filter)
            {
                case LevelListFilter.NotInMainFlow:
                    return LevelSaveSystem.SortEntriesByCreatedAtThenDisplayName(levelFileEntries
                        .Where(entry => entry != null && entry.level != null && !mainFlowIds.Contains(entry.level.id))
                        .ToList());
                case LevelListFilter.All:
                    return LevelSaveSystem.SortEntriesByCreatedAtThenDisplayName(levelFileEntries);
                default:
                    return mainFlowEntries.ToList();
            }
        }

        private HashSet<string> GetMainFlowIdSet()
        {
            return new HashSet<string>(
                mainFlowEntries
                    .Where(entry => entry != null && entry.level != null && !string.IsNullOrWhiteSpace(entry.level.id))
                    .Select(entry => entry.level.id));
        }

        private bool IsInMainFlow(LevelData level)
        {
            if (level == null || string.IsNullOrWhiteSpace(level.id))
            {
                return false;
            }

            return GetMainFlowIdSet().Contains(level.id);
        }

        private bool IsInMainFlow(LevelFileEntry entry)
        {
            return entry != null && IsInMainFlow(entry.level);
        }

        private void StartSelectedLevel(int levelIndex)
        {
            levelListView.Hide();
            mainMenuView.Hide();
            HideLevelFilePanel();
            HideMainFlowEditor();
            runtimeEditorView.Hide();
            editorMode = false;
            testLevelActive = false;
            referenceSolutionActive = false;
            gameActive = true;
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            HideReferenceSolutionPanel();
            LoadLevel(levelIndex);
        }

        private void ShowLevelFilePanel()
        {
            if (levelFilePanelView == null)
            {
                SetStatus("请先在 Inspector 中配置 LevelFilePanelView。");
                return;
            }

            gameActive = false;
            editorMode = false;
            testLevelActive = false;
            referenceSolutionActive = false;
            saveProgressForCurrentLevel = false;
            currentLevelSolved = false;
            boardRenderer.ClearBoard();
            ReloadLevels();
            RefreshLevelFilePanel(LevelListFilter.All);
            ShowPlayHudLevelName(true);
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            HideReferenceSolutionPanel();
            mainMenuView.Hide();
            levelListView.Hide();
            HideMainFlowEditor();
            runtimeEditorView.Hide();
            levelFilePanelView.Show();
            SetLevelName("关卡文件");
            SetOperationHint(string.Empty);
            SetStatus("选择关卡后可复制、重命名、删除或进入编辑。");
        }

        private void RefreshLevelFilePanel()
        {
            RefreshLevelFilePanel(LevelListFilter.All);
        }

        private void RefreshLevelFilePanel(LevelListFilter filter)
        {
            ReloadLevelFileEntries();
            ReloadMainFlowEntries();
            filteredLevelFileEntries = GetLevelFilePanelEntries(filter);
            if (levelFilePanelView != null)
            {
                levelFilePanelView.SetFilter(filter);
                levelFilePanelView.Rebuild(filteredLevelFileEntries, IsInMainFlow);
            }
        }

        private void HandleLevelFilePanelFilterChanged(LevelListFilter filter)
        {
            RefreshLevelFilePanel(filter);
            switch (filter)
            {
                case LevelListFilter.MainFlow:
                    SetStatus("当前显示主流程关卡文件。");
                    break;
                case LevelListFilter.NotInMainFlow:
                    SetStatus("当前显示未加入主流程的关卡文件。");
                    break;
                default:
                    SetStatus("当前显示全部关卡文件。");
                    break;
            }
        }

        private List<LevelFileEntry> GetLevelFilePanelEntries(LevelListFilter filter)
        {
            HashSet<string> mainFlowIds = GetMainFlowIdSet();
            switch (filter)
            {
                case LevelListFilter.MainFlow:
                    return mainFlowEntries.ToList();
                case LevelListFilter.NotInMainFlow:
                    return LevelSaveSystem.SortEntriesByCreatedAtThenDisplayName(levelFileEntries
                        .Where(entry => entry != null && entry.level != null && !mainFlowIds.Contains(entry.level.id))
                        .ToList());
                default:
                    return LevelSaveSystem.SortEntriesByCreatedAtThenDisplayName(levelFileEntries);
            }
        }

        private void HideLevelFilePanel()
        {
            if (levelFilePanelView != null)
            {
                levelFilePanelView.Hide();
            }
        }

        private void ShowMainFlowEditor()
        {
            if (mainFlowEditorView == null)
            {
                SetStatus("请先在 Inspector 中配置 MainFlowEditorView。");
                return;
            }

            gameActive = false;
            editorMode = false;
            testLevelActive = false;
            saveProgressForCurrentLevel = false;
            currentLevelSolved = false;
            boardRenderer.ClearBoard();
            ReloadLevelFileEntries();
            ReloadMainFlowEntries();
            editingMainFlowEntries = mainFlowEntries.ToList();
            CaptureMainFlowEditorBaselineSnapshot();
            RefreshMainFlowEditor();
            ShowPlayHudLevelName(true);
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            mainMenuView.Hide();
            levelListView.Hide();
            HideLevelFilePanel();
            runtimeEditorView.Hide();
            mainFlowEditorView.Show();
            SetLevelName("主流程编辑");
            SetOperationHint(string.Empty);
            SetStatus("添加、移除或调整主流程关卡顺序，完成后保存。");
        }

        private void HideMainFlowEditor()
        {
            if (mainFlowEditorView != null)
            {
                mainFlowEditorView.Hide();
            }
        }

        private void RequestExitMainFlowEditor()
        {
            if (!HasMainFlowEditorChanges())
            {
                ExitMainFlowEditor();
                return;
            }

            if (popUpView == null)
            {
                ExitMainFlowEditor();
                return;
            }

            popUpView.Show(
                "游戏流程编辑的进度未保存，是否确认退出",
                "确认",
                "保存并退出",
                "取消",
                ExitMainFlowEditor,
                SaveAndExitMainFlowEditor);
        }

        private void ExitMainFlowEditor()
        {
            mainFlowEditorBaselineSnapshot = string.Empty;
            ShowStartPage();
        }

        private void SaveAndExitMainFlowEditor()
        {
            SaveMainFlowEditor();
            ExitMainFlowEditor();
        }

        private void CaptureMainFlowEditorBaselineSnapshot()
        {
            mainFlowEditorBaselineSnapshot = CreateMainFlowEditorSnapshot();
        }

        private bool HasMainFlowEditorChanges()
        {
            return !string.Equals(mainFlowEditorBaselineSnapshot, CreateMainFlowEditorSnapshot(), StringComparison.Ordinal);
        }

        private string CreateMainFlowEditorSnapshot()
        {
            return string.Join(
                "\n",
                editingMainFlowEntries
                    .Where(entry => entry != null && entry.level != null && !string.IsNullOrWhiteSpace(entry.level.id))
                    .Select(entry => entry.level.id));
        }

        private void RefreshMainFlowEditor()
        {
            RefreshMainFlowEditor(null, false);
        }

        private void RefreshMainFlowEditor(LevelFileEntry selectedEntry, bool selectInMainFlow)
        {
            if (mainFlowEditorView == null)
            {
                return;
            }

            HashSet<string> editingIds = new HashSet<string>(
                editingMainFlowEntries
                    .Where(entry => entry != null && entry.level != null && !string.IsNullOrWhiteSpace(entry.level.id))
                    .Select(entry => entry.level.id));
            List<LevelFileEntry> availableEntries = LevelSaveSystem.SortEntriesByCreatedAtThenDisplayName(levelFileEntries
                .Where(entry => entry != null && entry.level != null && !editingIds.Contains(entry.level.id))
                .ToList());
            string selectedLevelId = selectedEntry != null && selectedEntry.level != null ? selectedEntry.level.id : null;
            mainFlowEditorView.Rebuild(editingMainFlowEntries, availableEntries, selectedLevelId, selectInMainFlow);
        }

        private void AddLevelToMainFlow(LevelFileEntry entry)
        {
            if (entry == null || entry.level == null || string.IsNullOrWhiteSpace(entry.level.id))
            {
                return;
            }

            if (editingMainFlowEntries.Any(existing => existing != null && existing.level != null && existing.level.id == entry.level.id))
            {
                return;
            }

            LevelValidationResult result;
            try
            {
                result = ValidateSavedLevel(entry, true);
                RefreshMainFlowEditor(entry, false);
            }
            catch (Exception exception)
            {
                SetStatus("验证失败：\n" + exception.Message);
                return;
            }

            if (!result.Passed && popUpView != null)
            {
                SetStatus("关卡没有通过简单验证：\n" + string.Join("\n", result.errors));
                popUpView.Show(
                    "关卡没有有效解，是否加入游玩流程",
                    "确认",
                    "取消",
                    () => AddLevelToMainFlowUnchecked(entry));
                return;
            }

            if (!result.Passed)
            {
                SetStatus("关卡没有通过简单验证，已加入主流程：\n" + string.Join("\n", result.errors));
            }

            AddLevelToMainFlowUnchecked(entry);
        }

        private void AddLevelToMainFlowUnchecked(LevelFileEntry entry)
        {
            editingMainFlowEntries.Add(entry);
            RefreshMainFlowEditor(entry, true);
            SetStatus("已加入主流程：" + entry.level.displayName);
        }

        private void RemoveLevelFromMainFlow(LevelFileEntry entry)
        {
            int index = FindEditingMainFlowIndex(entry);
            if (index < 0)
            {
                return;
            }

            string displayName = editingMainFlowEntries[index].level.displayName;
            editingMainFlowEntries.RemoveAt(index);
            RefreshMainFlowEditor(entry, false);
            SetStatus("已移出主流程：" + displayName);
        }

        private void MoveMainFlowLevelUp(LevelFileEntry entry)
        {
            int index = FindEditingMainFlowIndex(entry);
            if (index <= 0)
            {
                return;
            }

            SwapEditingMainFlowEntries(index, index - 1);
        }

        private void MoveMainFlowLevelDown(LevelFileEntry entry)
        {
            int index = FindEditingMainFlowIndex(entry);
            if (index < 0 || index >= editingMainFlowEntries.Count - 1)
            {
                return;
            }

            SwapEditingMainFlowEntries(index, index + 1);
        }

        private void SaveMainFlowEditor()
        {
            List<string> ids = editingMainFlowEntries
                .Where(entry => entry != null && entry.level != null && !string.IsNullOrWhiteSpace(entry.level.id))
                .Select(entry => entry.level.id)
                .ToList();
            MainFlowSaveSystem.SaveMainFlow(ids);
            ReloadMainFlowEntries();
            SetActivePlayEntries(mainFlowEntries);
            editingMainFlowEntries = mainFlowEntries.ToList();
            CaptureMainFlowEditorBaselineSnapshot();
            RefreshMainFlowEditor();
            SetStatus("主流程已保存。");
        }

        private int FindEditingMainFlowIndex(LevelFileEntry entry)
        {
            if (entry == null || entry.level == null || string.IsNullOrWhiteSpace(entry.level.id))
            {
                return -1;
            }

            return editingMainFlowEntries.FindIndex(existing =>
                existing != null
                && existing.level != null
                && string.Equals(existing.level.id, entry.level.id, StringComparison.Ordinal));
        }

        private void SwapEditingMainFlowEntries(int firstIndex, int secondIndex)
        {
            LevelFileEntry temp = editingMainFlowEntries[firstIndex];
            editingMainFlowEntries[firstIndex] = editingMainFlowEntries[secondIndex];
            editingMainFlowEntries[secondIndex] = temp;
            RefreshMainFlowEditor(editingMainFlowEntries[secondIndex], true);
            SetStatus("已调整主流程顺序。");
        }

        private void CreateLevelFromFilePanel()
        {
            RequestLevelName("请输入新关卡名称：", "Custom Level", "创建", displayName =>
            {
                LevelData level = LevelData.CreateBlank(8, 8);
                level.displayName = displayName;
                level.id = LevelSaveSystem.CreateStableLevelId();
                EnterEditor(level, string.Empty, true, "已创建空白关卡。");
            });
        }

        private void DeleteLevelFromFilePanel(LevelFileEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            Confirm("是否确认删除关卡“" + entry.level.displayName + "”？", () =>
            {
                try
                {
                    LevelSaveSystem.DeleteLevel(entry);
                    ReloadLevelFileEntries();
                    MainFlowSaveSystem.CleanupInvalidLevelIds(levelFileEntries);
                    ReloadMainFlowEntries();
                    SetActivePlayEntries(mainFlowEntries);
                    RefreshLevelFilePanel();
                    SetStatus("已删除关卡：" + entry.level.displayName);
                }
                catch (Exception exception)
                {
                    SetStatus("删除失败：\n" + exception.Message);
                }
            });
        }

        private void CopyLevelFromFilePanel(LevelFileEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            try
            {
                LevelFileEntry copy = LevelSaveSystem.CopyLevel(entry.level);
                ReloadLevels();
                RefreshLevelFilePanel();
                SetStatus("已复制关卡：" + copy.level.displayName);
            }
            catch (Exception exception)
            {
                SetStatus("复制失败：\n" + exception.Message);
            }
        }

        private void ImportLevelsFromFilePanel()
        {
            try
            {
                LevelImportSummary summary = LevelSaveSystem.ImportLevelsFromConfiguredDirectory();
                ReloadLevels();
                RefreshLevelFilePanel();
                SetStatus(CreateImportStatus(summary));
            }
            catch (Exception exception)
            {
                SetStatus("导入失败：\n" + exception.Message);
            }
        }

        private void ExportLevelFromFilePanel(LevelFileEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            try
            {
                string path = LevelSaveSystem.ExportLevel(entry);
                SetStatus("已导出关卡：" + entry.level.displayName + "\n" + path);
            }
            catch (Exception exception)
            {
                SetStatus("导出失败：\n" + exception.Message);
            }
        }

        private static string CreateImportStatus(LevelImportSummary summary)
        {
            if (summary == null)
            {
                return "导入完成。";
            }

            string message = "导入完成：成功 " + summary.importedCount + "，跳过 " + summary.skippedCount + "，失败 " + summary.failedCount + "。";
            if (summary.messages.Count > 0)
            {
                message += "\n" + string.Join("\n", summary.messages.Take(5));
                if (summary.messages.Count > 5)
                {
                    message += "\n...";
                }
            }

            return message;
        }

        private void RenameLevelFromFilePanel(LevelFileEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (popUpView == null)
            {
                SetStatus("请先配置支持输入框的 PopUpView。");
                return;
            }

            popUpView.ShowInput(
                "请输入新的关卡名称：",
                entry.level.displayName,
                "重命名",
                "取消",
                newName =>
                {
                    try
                    {
                        LevelFileEntry renamed = LevelSaveSystem.RenameLevel(entry, newName);
                        ReloadLevels();
                        RefreshLevelFilePanel();
                        SetStatus("已重命名为：" + renamed.level.displayName);
                    }
                    catch (Exception exception)
                    {
                        SetStatus("重命名失败：\n" + exception.Message);
                    }
                });
        }

        private void EditLevelFromFilePanel(LevelFileEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            LevelData source = entry.level.Clone();
            EnterEditor(source, entry.filePath, true, "正在编辑关卡。");
        }

        private void RequestLevelName(string message, string initialName, string confirmText, Action<string> onConfirm)
        {
            if (popUpView == null)
            {
                TryConfirmLevelName(initialName, onConfirm);
                return;
            }

            popUpView.ShowInput(message, initialName, confirmText, "取消", displayName => TryConfirmLevelName(displayName, onConfirm));
        }

        private void TryConfirmLevelName(string displayName, Action<string> onConfirm)
        {
            try
            {
                string resolvedDisplayName = LevelSaveSystem.ResolveLevelDisplayName(displayName);
                onConfirm?.Invoke(resolvedDisplayName);
            }
            catch (Exception exception)
            {
                SetStatus("创建失败：\n" + exception.Message);
            }
        }

        private void HandleLevelFileSelection(LevelFileEntry entry)
        {
            if (entry == null)
            {
                SetStatus("请选择一个关卡。");
                return;
            }

            SetStatus("已选择关卡：" + entry.level.displayName);
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
            if (testLevelActive)
            {
                SetStatus("测试关卡完成。");
                currentLevelSolved = true;
                ShowTestLevelCompletePopup();
                return;
            }

            if (saveProgressForCurrentLevel)
            {
                GameProgressSaveSystem.MarkLevelCompleted(levels[currentLevelIndex]);
                SetStatus("完成关卡！进度已保存。");
            }
            else
            {
                SetStatus("试玩关卡完成！试玩不会写入通关进度。");
            }

            currentLevelSolved = true;
            ShowLevelCompletePopup();
        }

        private void ShowTestLevelCompletePopup()
        {
            if (popUpView == null)
            {
                return;
            }

            if (!HasTestCompletionSaveChanges())
            {
                popUpView.Show("已完成关卡", "返回关卡编辑", "取消", ReturnToEditorFromTestLevel);
                return;
            }

            popUpView.Show(
                "测试关卡已完成，是否保存当前状态？",
                "返回关卡编辑",
                "保存",
                "取消",
                ReturnToEditorFromTestLevel,
                SaveSolvedEditorLevelFromTestCompletion);
        }

        private bool HasTestCompletionSaveChanges()
        {
            if (levelEditor == null || levelEditor.CurrentLevel == null)
            {
                return false;
            }

            return HasUnsavedEditorChanges()
                || LevelValidator.GetValidationStatusText(levelEditor.CurrentLevel) != LevelValidationStatus.Solved;
        }

        private void SaveSolvedEditorLevelFromTestCompletion()
        {
            if (levelEditor == null || levelEditor.CurrentLevel == null)
            {
                SetStatus("保存失败：当前没有可保存的编辑关卡。");
                return;
            }

            levelEditor.CurrentLevel.validationStatus = LevelValidationStatus.Solved;
            RequestSaveEditorLevel(() =>
            {
                ReturnToEditorFromTestLevel();
                SetStatus("测试关卡已完成，当前关卡已保存为有解。");
            });
        }

        private void ShowLevelCompletePopup()
        {
            if (popUpView == null)
            {
                return;
            }

            bool isLastLevel = currentLevelIndex >= levels.Count - 1;
            if (isLastLevel)
            {
                popUpView.Show("完成关卡！", "返回主界面", "取消", ShowStartPage);
                return;
            }

            popUpView.Show("完成关卡！", "下一关", "取消", () => LoadLevel(currentLevelIndex + 1));
        }

        private void RestartLevel()
        {
            boardModel.Restart();
            boardRenderer.Render(boardModel);
            currentLevelSolved = false;
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

        private void ToggleCommonMenu()
        {
            if (commonMenuView == null)
            {
                return;
            }

            if (commonMenuView.IsVisible)
            {
                commonMenuView.Hide();
            }
            else
            {
                commonMenuView.Show();
            }
        }

        private void HideCommonMenu()
        {
            if (commonMenuView != null)
            {
                commonMenuView.Hide();
            }
        }

        private void ToggleTestLevelPanel()
        {
            if (testLevelPanelView == null)
            {
                SetStatus("请先在 Inspector 中配置 TestLevelPanelView。");
                return;
            }

            if (testLevelPanelView.IsVisible)
            {
                testLevelPanelView.Hide();
            }
            else
            {
                testLevelPanelView.Show();
            }
        }

        private void HideTestLevelPanel()
        {
            if (testLevelPanelView != null)
            {
                testLevelPanelView.Hide();
            }
        }

        private void ToggleReferenceSolutionPanel()
        {
            if (referenceSolutionPanelView == null)
            {
                SetStatus("请先在 Inspector 中配置 ReferenceSolutionPanelView。");
                return;
            }

            if (referenceSolutionPanelView.IsVisible)
            {
                referenceSolutionPanelView.Hide();
            }
            else
            {
                referenceSolutionPanelView.Show();
            }
        }

        private void HideReferenceSolutionPanel()
        {
            if (referenceSolutionPanelView != null)
            {
                referenceSolutionPanelView.Hide();
            }
        }

        private void ReturnToReferenceSolution()
        {
            HideReferenceSolutionPanel();
        }

        private void ReturnToEditorFromReferenceSolution()
        {
            referenceSolutionActive = false;
            testLevelActive = false;
            gameActive = false;
            editorMode = true;
            currentLevelSolved = false;
            saveProgressForCurrentLevel = false;
            referenceSolutionActions = string.Empty;
            referenceSolutionStepIndex = 0;
            ResetReferenceSolutionInputState();
            HideReferenceSolutionPanel();
            HideTestLevelPanel();
            HideCommonMenu();
            HideCommonEditorMenu();
            runtimeEditorView.Show();
            runtimeEditorView.ClearToolSelection();
            runtimeEditorView.SelectBrushMode(EditorBrushMode.Normal);
            levelEditor.Render();
            ShowPlayHudLevelName(true);
            SetLevelName("关卡编辑器");
            SetOperationHint("鼠标绘制关卡，Ctrl+Z/撤销按钮撤销，ESC 菜单。");
            SetStatus("已返回关卡编辑。");
        }

        private void RestartReferenceSolution()
        {
            HideReferenceSolutionPanel();
            boardModel.Load(levelEditor.CurrentLevel.Clone());
            boardRenderer.Render(boardModel);
            referenceSolutionStepIndex = 0;
            ResetReferenceSolutionInputState();
            UpdateReferenceSolutionProgressStatus();
        }

        private void UpdateReferenceSolution()
        {
            bool forwardHeld = Input.GetKey(KeyCode.Space);
            bool backwardHeld = Input.GetKey(KeyCode.Z);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                InterruptReferenceSolutionInputUntilRelease(forwardHeld, backwardHeld);
                ToggleReferenceSolutionPanel();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                InterruptReferenceSolutionInputUntilRelease(forwardHeld, backwardHeld);
                RestartReferenceSolution();
                return;
            }

            if (referenceSolutionPanelView != null && referenceSolutionPanelView.IsVisible)
            {
                return;
            }

            bool forwardPressed = Input.GetKeyDown(KeyCode.Space);
            bool backwardPressed = Input.GetKeyDown(KeyCode.Z);

            if (referenceSolutionInputLockedUntilRelease)
            {
                if (!forwardHeld && !backwardHeld)
                {
                    referenceSolutionInputLockedUntilRelease = false;
                }

                return;
            }

            if (referenceSolutionHeldInputDirection != ReferenceSolutionNoHeldInput)
            {
                if (IsReferenceSolutionOppositeInputPressed(forwardPressed, backwardPressed))
                {
                    StopReferenceSolutionHeldInput();
                    referenceSolutionInputLockedUntilRelease = true;
                    return;
                }

                if (!IsReferenceSolutionHeldInputStillDown(forwardHeld, backwardHeld))
                {
                    StopReferenceSolutionHeldInput();
                    return;
                }

                if (Time.unscaledTime >= nextReferenceSolutionHeldInputTime)
                {
                    if (PlayReferenceSolutionStep(referenceSolutionHeldInputDirection))
                    {
                        SetReferenceSolutionInputCooldown(referenceSolutionHeldInputDirection);
                        nextReferenceSolutionHeldInputTime = Time.unscaledTime + ReferenceSolutionHoldRepeatSeconds;
                    }
                    else
                    {
                        StopReferenceSolutionHeldInput();
                    }
                }

                return;
            }

            if ((forwardPressed && backwardHeld) || (backwardPressed && forwardHeld))
            {
                referenceSolutionInputLockedUntilRelease = true;
                return;
            }

            if (forwardPressed)
            {
                TryStartReferenceSolutionHeldInput(ReferenceSolutionForwardHeldInput);
                return;
            }

            if (backwardPressed)
            {
                TryStartReferenceSolutionHeldInput(ReferenceSolutionBackwardHeldInput);
            }
        }

        private bool CanUseReferenceSolutionForwardInput()
        {
            return Time.unscaledTime >= nextReferenceSolutionForwardInputTime;
        }

        private bool CanUseReferenceSolutionBackwardInput()
        {
            return Time.unscaledTime >= nextReferenceSolutionBackwardInputTime;
        }

        private void ResetReferenceSolutionInputCooldowns()
        {
            nextReferenceSolutionForwardInputTime = 0f;
            nextReferenceSolutionBackwardInputTime = 0f;
        }

        private void ResetReferenceSolutionInputState()
        {
            ResetReferenceSolutionInputCooldowns();
            StopReferenceSolutionHeldInput();
            referenceSolutionInputLockedUntilRelease = false;
        }

        private void InterruptReferenceSolutionInputUntilRelease(bool forwardHeld, bool backwardHeld)
        {
            StopReferenceSolutionHeldInput();
            if (forwardHeld || backwardHeld)
            {
                referenceSolutionInputLockedUntilRelease = true;
            }
        }

        private void TryStartReferenceSolutionHeldInput(int direction)
        {
            if (!CanUseReferenceSolutionInput(direction) || !PlayReferenceSolutionStep(direction))
            {
                return;
            }

            SetReferenceSolutionInputCooldown(direction);
            referenceSolutionHeldInputDirection = direction;
            nextReferenceSolutionHeldInputTime = Time.unscaledTime + ReferenceSolutionHoldDelaySeconds;
        }

        private bool CanUseReferenceSolutionInput(int direction)
        {
            if (direction == ReferenceSolutionForwardHeldInput)
            {
                return CanUseReferenceSolutionForwardInput();
            }

            if (direction == ReferenceSolutionBackwardHeldInput)
            {
                return CanUseReferenceSolutionBackwardInput();
            }

            return false;
        }

        private void SetReferenceSolutionInputCooldown(int direction)
        {
            if (direction == ReferenceSolutionForwardHeldInput)
            {
                nextReferenceSolutionForwardInputTime = Time.unscaledTime + ReferenceSolutionInputCooldownSeconds;
            }
            else if (direction == ReferenceSolutionBackwardHeldInput)
            {
                nextReferenceSolutionBackwardInputTime = Time.unscaledTime + ReferenceSolutionInputCooldownSeconds;
            }
        }

        private bool PlayReferenceSolutionStep(int direction)
        {
            if (direction == ReferenceSolutionForwardHeldInput)
            {
                return PlayNextReferenceSolutionStep();
            }

            if (direction == ReferenceSolutionBackwardHeldInput)
            {
                return PlayPreviousReferenceSolutionStep();
            }

            return false;
        }

        private bool IsReferenceSolutionOppositeInputPressed(bool forwardPressed, bool backwardPressed)
        {
            return (referenceSolutionHeldInputDirection == ReferenceSolutionForwardHeldInput && backwardPressed)
                || (referenceSolutionHeldInputDirection == ReferenceSolutionBackwardHeldInput && forwardPressed);
        }

        private bool IsReferenceSolutionHeldInputStillDown(bool forwardHeld, bool backwardHeld)
        {
            return (referenceSolutionHeldInputDirection == ReferenceSolutionForwardHeldInput && forwardHeld)
                || (referenceSolutionHeldInputDirection == ReferenceSolutionBackwardHeldInput && backwardHeld);
        }

        private void StopReferenceSolutionHeldInput()
        {
            referenceSolutionHeldInputDirection = ReferenceSolutionNoHeldInput;
            nextReferenceSolutionHeldInputTime = 0f;
        }

        private bool PlayNextReferenceSolutionStep()
        {
            if (referenceSolutionStepIndex >= referenceSolutionActions.Length)
            {
                return false;
            }

            Vector2Int direction = GetDirectionFromSolutionAction(referenceSolutionActions[referenceSolutionStepIndex]);
            MoveResult result = boardModel.TryMove(direction);
            if (result == MoveResult.Blocked)
            {
                SetStatus("参考解法数据与当前关卡不一致。");
                return false;
            }

            referenceSolutionStepIndex++;
            boardRenderer.Render(boardModel);
            UpdateReferenceSolutionProgressStatus();
            return true;
        }

        private bool PlayPreviousReferenceSolutionStep()
        {
            if (referenceSolutionStepIndex <= 0)
            {
                return false;
            }

            if (boardModel.Undo())
            {
                referenceSolutionStepIndex--;
                boardRenderer.Render(boardModel);
                UpdateReferenceSolutionProgressStatus();
                return true;
            }

            return false;
        }

        private void UpdateReferenceSolutionProgressStatus()
        {
            int totalStepCount = string.IsNullOrEmpty(referenceSolutionActions) ? 0 : referenceSolutionActions.Length;
            SetStatus("当前步数：" + referenceSolutionStepIndex + " / " + totalStepCount);
        }

        private void ReturnToTestLevel()
        {
            HideTestLevelPanel();
        }

        private void ReturnToEditorFromTestLevel()
        {
            testLevelActive = false;
            gameActive = false;
            editorMode = true;
            currentLevelSolved = false;
            saveProgressForCurrentLevel = false;
            HideTestLevelPanel();
            HideCommonMenu();
            HideCommonEditorMenu();
            runtimeEditorView.Show();
            runtimeEditorView.ClearToolSelection();
            runtimeEditorView.SelectBrushMode(EditorBrushMode.Normal);
            levelEditor.Render();
            ShowPlayHudLevelName(true);
            SetLevelName("关卡编辑器");
            SetOperationHint("鼠标绘制关卡，Ctrl+Z/撤销按钮撤销，ESC 菜单。");
            SetStatus("已返回关卡编辑。");
        }

        private void RestartTestLevel()
        {
            HideTestLevelPanel();
            RestartLevel();
        }

        private void ReturnToGame()
        {
            HideCommonMenu();
        }

        private void ReturnToTitlePage()
        {
            HideCommonMenu();
            ShowStartPage();
        }

        private void LoadPreviousLevelFromCommonMenu()
        {
            HideCommonMenu();
            if (currentLevelIndex <= 0)
            {
                ShowLevelBoundaryPopup("该关卡为初始关");
                return;
            }

            LoadLevel(currentLevelIndex - 1);
        }

        private void LoadNextLevelFromCommonMenu()
        {
            HideCommonMenu();
            if (currentLevelIndex >= levels.Count - 1)
            {
                ShowLevelBoundaryPopup("该关卡为末尾关");
                return;
            }

            LoadLevel(currentLevelIndex + 1);
        }

        private void RestartLevelFromCommonMenu()
        {
            HideCommonMenu();
            RestartLevel();
        }

        private void EnterEditorFromCommonMenu()
        {
            HideCommonMenu();
            LevelFileEntry currentEntry = GetCurrentLevelFileEntry();
            if (currentEntry != null)
            {
                EnterEditor(currentEntry.level.Clone(), currentEntry.filePath, true, "正在编辑关卡。");
                return;
            }

            LevelData source = levels.Count > 0 ? levels[currentLevelIndex].Clone() : LevelData.CreateBlank(8, 8);
            EnterEditor(source, string.Empty, true, "正在编辑未保存关卡。");
        }

        private LevelFileEntry GetCurrentLevelFileEntry()
        {
            if (currentLevelIndex < 0 || currentLevelIndex >= activePlayEntries.Count)
            {
                return null;
            }

            return activePlayEntries[currentLevelIndex];
        }

        private void ToggleCommonEditorMenu()
        {
            if (commonEditorMenuView == null)
            {
                return;
            }

            if (commonEditorMenuView.IsVisible)
            {
                commonEditorMenuView.Hide();
            }
            else
            {
                commonEditorMenuView.Show();
            }
        }

        private void HideCommonEditorMenu()
        {
            if (commonEditorMenuView != null)
            {
                commonEditorMenuView.Hide();
            }
        }

        private void ReturnToEditor()
        {
            HideCommonEditorMenu();
        }

        private void EndEditorPaintOperation()
        {
            if (!editorPaintOperationActive)
            {
                return;
            }

            levelEditor.EndEditOperation();
            editorPaintOperationActive = false;
        }

        private void ReturnToTitlePageFromEditor()
        {
            HideCommonEditorMenu();
            RequestExitEditor(ShowStartPage);
        }

        private void TestLevelFromEditorMenu()
        {
            HideCommonEditorMenu();
            TestEditorLevel();
        }

        private void SaveLevelFromEditorMenu()
        {
            HideCommonEditorMenu();
            RequestSaveEditorLevel(null);
        }

        private void DeleteLevelFromEditorMenu()
        {
            HideCommonEditorMenu();
            if (string.IsNullOrWhiteSpace(editingLevelPath))
            {
                SetStatus("当前编辑内容还不是已保存的关卡，不能直接删除。");
                return;
            }

            Confirm("是否确认删除当前关卡？", () =>
            {
                try
                {
                    LevelSaveSystem.DeleteLevel(new LevelFileEntry(levelEditor.CurrentLevel, editingLevelPath));
                    editingLevelPath = string.Empty;
                    ReloadLevelFileEntries();
                    MainFlowSaveSystem.CleanupInvalidLevelIds(levelFileEntries);
                    ReloadMainFlowEntries();
                    SetActivePlayEntries(mainFlowEntries);
                    RefreshLevelFilePanel();
                    SetStatus("已删除当前关卡。");
                    ExitEditor();
                }
                catch (Exception exception)
                {
                    SetStatus("删除失败：\n" + exception.Message);
                }
            });
        }

        private void EnterEditor(LevelData source, string levelPath, bool returnToLevelFilePanel, string statusMessage)
        {
            gameActive = false;
            editorMode = true;
            testLevelActive = false;
            referenceSolutionActive = false;
            ShowPlayHudLevelName(true);
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            HideReferenceSolutionPanel();
            mainMenuView.Hide();
            levelListView.Hide();
            HideLevelFilePanel();
            HideMainFlowEditor();
            runtimeEditorView.Show();
            saveProgressForCurrentLevel = false;
            currentLevelSolved = false;
            editingLevelPath = levelPath ?? string.Empty;
            returnToLevelFilePanelOnExit = returnToLevelFilePanel;

            levelEditor.SetLevel(source);
            runtimeEditorView.SetLevelFields(source.displayName, source.width, source.height);
            runtimeEditorView.ClearToolSelection();
            runtimeEditorView.SelectBrushMode(EditorBrushMode.Normal);
            CaptureEditorBaselineSnapshot();
            SetLevelName("关卡编辑器");
            SetOperationHint("鼠标绘制关卡，Ctrl+Z/撤销按钮撤销，ESC 菜单。");
            SetStatus(statusMessage);
        }

        private void ExitEditor()
        {
            EndEditorPaintOperation();
            editorMode = false;
            testLevelActive = false;
            referenceSolutionActive = false;
            runtimeEditorView.Hide();
            levelListView.Hide();
            HideMainFlowEditor();
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            HideReferenceSolutionPanel();
            ReloadLevels();
            levelEditor.ClearUndoHistory();
            editorPaintOperationActive = false;
            editorBaselineSnapshotJson = string.Empty;
            referenceSolutionActions = string.Empty;
            referenceSolutionStepIndex = 0;
            ResetReferenceSolutionInputState();
            if (returnToLevelFilePanelOnExit)
            {
                editingLevelPath = string.Empty;
                returnToLevelFilePanelOnExit = false;
                ShowLevelFilePanel();
                return;
            }

            gameActive = true;
            editingLevelPath = string.Empty;
            LoadLevel(currentLevelIndex);
        }

        private void RequestExitEditor()
        {
            RequestExitEditor(ExitEditor);
        }

        private void RequestExitEditor(Action exitAction)
        {
            EndEditorPaintOperation();
            if (!HasUnsavedEditorChanges())
            {
                exitAction?.Invoke();
                return;
            }

            if (popUpView == null)
            {
                exitAction?.Invoke();
                return;
            }

            popUpView.Show(
                "当前关卡编辑进度未保存，是否确认退出？",
                "直接退出",
                "保存并退出",
                "取消",
                exitAction,
                () => SaveAndExitEditor(exitAction));
        }

        private void CreateBlankEditorLevel()
        {
            int width = runtimeEditorView.GetWidth(8);
            int height = runtimeEditorView.GetHeight(8);
            levelEditor.CreateBlank(width, height);
            levelEditor.CurrentLevel.displayName = runtimeEditorView.GetLevelName();
            levelEditor.CurrentLevel.id = LevelSaveSystem.CreateStableLevelId();
            levelEditor.CurrentLevel.validationStatus = LevelValidationStatus.Unverified;
            runtimeEditorView.SetLevelFields(
                levelEditor.CurrentLevel.displayName,
                levelEditor.CurrentLevel.width,
                levelEditor.CurrentLevel.height);
            runtimeEditorView.ClearToolSelection();
            runtimeEditorView.SelectBrushMode(EditorBrushMode.Normal);
            SetLevelName("关卡编辑器");
        }

        private void UndoEditorLevel()
        {
            if (levelEditor.Undo())
            {
                MarkCurrentEditorLevelUnverified();
            }
        }

        private bool SaveEditorLevel()
        {
            PrepareCurrentEditorLevelForSave();

            try
            {
                editingLevelPath = levelEditor.Save(editingLevelPath);
                runtimeEditorView.SetLevelFields(
                    levelEditor.CurrentLevel.displayName,
                    levelEditor.CurrentLevel.width,
                    levelEditor.CurrentLevel.height);
                CaptureEditorBaselineSnapshot();
                ReloadLevels();
                RefreshLevelFilePanel();
                return true;
            }
            catch (Exception exception)
            {
                SetStatus("保存失败：\n" + exception.Message);
                return false;
            }
        }

        private void SaveAndExitEditor()
        {
            SaveAndExitEditor(ExitEditor);
        }

        private void SaveAndExitEditor(Action exitAction)
        {
            RequestSaveEditorLevel(exitAction);
        }

        private void RequestSaveEditorLevel(Action onSaved)
        {
            PrepareCurrentEditorLevelForSave();
            if (LevelValidator.GetValidationStatusText(levelEditor.CurrentLevel) == LevelValidationStatus.Solved)
            {
                if (SaveEditorLevel())
                {
                    onSaved?.Invoke();
                }

                return;
            }

            LevelValidationResult result = LevelValidator.ValidateSimpleAndApplyStatus(levelEditor.CurrentLevel);
            if (result.Passed || popUpView == null)
            {
                if (SaveEditorLevel())
                {
                    onSaved?.Invoke();
                }

                return;
            }

            SetStatus("关卡没有通过简单验证：\n" + string.Join("\n", result.errors));
            popUpView.Show(
                "关卡没有有效解，是否确认保存",
                "确认",
                "取消",
                () =>
                {
                    if (SaveEditorLevel())
                    {
                        onSaved?.Invoke();
                    }
                });
        }

        private void PrepareCurrentEditorLevelForSave()
        {
            levelEditor.CurrentLevel.displayName = runtimeEditorView.GetLevelName();
            if (string.IsNullOrWhiteSpace(levelEditor.CurrentLevel.id))
            {
                levelEditor.CurrentLevel.id = LevelSaveSystem.CreateStableLevelId();
            }
        }

        private void ValidateCurrentEditorLevel()
        {
            PrepareCurrentEditorLevelForSave();
            LevelValidationResult result = LevelValidator.ValidateSimpleAndApplyStatus(levelEditor.CurrentLevel);
            SetValidationStatusMessage("当前编辑关卡", result);
        }

        private void SolveCurrentEditorLevel()
        {
            RequestSolveCurrentEditorLevel(LevelSolver.SolveAStar, "正在使用 A* 求解...");
        }

        private void StrictSolveCurrentEditorLevel()
        {
            RequestSolveCurrentEditorLevel(LevelSolver.Solve, "正在严格求解...");
        }

        private void RequestSolveCurrentEditorLevel(Func<LevelData, LevelSolverOptions, LevelSolverResult> solveFunc, string solvingMessage)
        {
            if (solverActive)
            {
                return;
            }

            solverActive = true;
            RequestSaveEditorLevelBeforeSolve(
                () => StartCoroutine(SolveCurrentEditorLevelRoutine(solveFunc, solvingMessage)),
                () => solverActive = false);
        }

        private void RequestSaveEditorLevelBeforeSolve(Action onReadyToSolve, Action onStopSolving)
        {
            PrepareCurrentEditorLevelForSave();
            LevelValidationResult result = LevelValidator.ValidateSimpleAndApplyStatus(levelEditor.CurrentLevel);
            if (result.Passed)
            {
                if (SaveEditorLevel())
                {
                    onReadyToSolve?.Invoke();
                    return;
                }

                onStopSolving?.Invoke();
                return;
            }

            SetStatus("关卡没有通过简单验证：\n" + string.Join("\n", result.errors));
            if (popUpView == null)
            {
                if (SaveEditorLevel())
                {
                    SetStatus("关卡没有通过简单验证，已保存，未执行求解。");
                }

                onStopSolving?.Invoke();
                return;
            }

            popUpView.Show(
                "关卡没有通过简单验证，是否确认保存？确认保存后不会继续求解。",
                "确认",
                "取消",
                () =>
                {
                    if (SaveEditorLevel())
                    {
                        SetStatus("关卡没有通过简单验证，已保存，未执行求解。");
                    }

                    onStopSolving?.Invoke();
                },
                onStopSolving);
        }

        private IEnumerator SolveCurrentEditorLevelRoutine(Func<LevelData, LevelSolverOptions, LevelSolverResult> solveFunc, string solvingMessage)
        {
            EndEditorPaintOperation();
            SetSolverBlockingOverlayVisible(true);
            SetStatus(solvingMessage);
            yield return null;

            try
            {
                LevelSolverResult result = solveFunc(levelEditor.CurrentLevel, new LevelSolverOptions
                {
                    maxExploredStates = runtimeEditorView.GetSolverMaxExploredStates(),
                    maxDurationSeconds = runtimeEditorView.GetSolverMaxDurationSeconds()
                });
                ApplySolverStatus(levelEditor.CurrentLevel, result);
                PersistSolvedEditorValidationStatusIfSafe(result);
                SetSolverStatusMessage("当前编辑关卡", result);
            }
            catch (Exception exception)
            {
                SetStatus("求解失败：\n" + exception.Message);
            }
            finally
            {
                SetSolverBlockingOverlayVisible(false);
                solverActive = false;
            }
        }

        private void ValidateLevelFromFilePanel(LevelFileEntry entry)
        {
            ValidateSavedLevelFromUi(entry, () => RefreshLevelFilePanel(), false);
        }

        private void ValidateLevelFromMainFlowEditor(LevelFileEntry entry)
        {
            ValidateSavedLevelFromUi(entry, RefreshMainFlowEditor, true);
        }

        private void ValidateSavedLevelFromUi(LevelFileEntry entry, Action refreshUi, bool preserveSolvedStatus)
        {
            if (entry == null || entry.level == null)
            {
                return;
            }

            try
            {
                LevelValidationResult result = ValidateSavedLevel(entry, preserveSolvedStatus);
                refreshUi?.Invoke();
                SetValidationStatusMessage(entry.level.displayName, result);
            }
            catch (Exception exception)
            {
                SetStatus("验证失败：\n" + exception.Message);
            }
        }

        private LevelValidationResult ValidateSavedLevel(LevelFileEntry entry, bool preserveSolvedStatus = false)
        {
            if (entry == null || entry.level == null)
            {
                throw new InvalidOperationException("请选择要验证的关卡。");
            }

            bool shouldPreserveSolvedStatus = preserveSolvedStatus
                && LevelValidator.GetValidationStatusText(entry.level) == LevelValidationStatus.Solved;
            string preservedSolutionActions = shouldPreserveSolvedStatus ? entry.level.solutionActions : string.Empty;
            LevelValidationResult result = LevelValidator.ValidateSimpleAndApplyStatus(entry.level);
            if (shouldPreserveSolvedStatus)
            {
                entry.level.validationStatus = LevelValidationStatus.Solved;
                entry.level.solutionActions = preservedSolutionActions;
            }

            if (!string.IsNullOrWhiteSpace(entry.filePath))
            {
                entry.filePath = LevelSaveSystem.SaveLevel(entry.level, entry.filePath);
            }

            return result;
        }

        private void SetValidationStatusMessage(string levelName, LevelValidationResult result)
        {
            string label = string.IsNullOrWhiteSpace(levelName) ? "关卡" : levelName;
            if (result == null || result.Passed)
            {
                SetStatus(label + "：通过简单验证。");
                return;
            }

            SetStatus(label + "：没有通过简单验证。\n" + string.Join("\n", result.errors));
        }

        private void ApplySolverStatus(LevelData level, LevelSolverResult result)
        {
            if (level == null || result == null)
            {
                return;
            }

            if (result.solved)
            {
                level.validationStatus = LevelValidationStatus.Solved;
                level.solutionActions = result.actionSequence;
                return;
            }

            if (LevelValidator.GetValidationStatusText(level) == LevelValidationStatus.Solved)
            {
                return;
            }

            if (result.HasErrors)
            {
                level.validationStatus = LevelValidationStatus.NoValidSolution;
                level.solutionActions = string.Empty;
                return;
            }

            if (result.ConfirmedNoSolution)
            {
                level.validationStatus = LevelValidationStatus.NoValidSolution;
                level.solutionActions = string.Empty;
            }
        }

        private void SetSolverStatusMessage(string levelName, LevelSolverResult result)
        {
            string label = string.IsNullOrWhiteSpace(levelName) ? "关卡" : levelName;
            if (result == null)
            {
                SetStatus(label + "：求解失败。");
                return;
            }

            if (result.solved)
            {
                int stepCount = string.IsNullOrEmpty(result.actionSequence) ? 0 : result.actionSequence.Length;
                SetStatus(label + "：有解。" + "\n已探索状态数：" + result.exploredStateCount);
                return;
            }

            if (result.HasErrors)
            {
                SetStatus(label + "：无法求解。\n" + string.Join("\n", result.errors));
                return;
            }

            if (result.LimitReached)
            {
                SetStatus(label + "：求解未确认。\n" + result.message + "\n已探索状态数：" + result.exploredStateCount + "\n当前验证状态：" + LevelValidator.GetValidationStatusText(levelEditor.CurrentLevel));
                return;
            }

            SetStatus(label + "：无有效解。\n已探索状态数：" + result.exploredStateCount);
        }

        private void PersistSolvedEditorValidationStatusIfSafe(LevelSolverResult result)
        {
            if (result == null || !result.solved || string.IsNullOrWhiteSpace(editingLevelPath) || HasUnsavedEditorChanges())
            {
                return;
            }

            try
            {
                editingLevelPath = LevelSaveSystem.SaveLevel(levelEditor.CurrentLevel, editingLevelPath);
                CaptureEditorBaselineSnapshot();
                RefreshLevelFilePanel();
            }
            catch (Exception exception)
            {
                SetStatus("保存求解状态失败：\n" + exception.Message);
            }
        }

        private void MarkCurrentEditorLevelUnverified()
        {
            if (levelEditor == null || levelEditor.CurrentLevel == null)
            {
                return;
            }

            levelEditor.CurrentLevel.validationStatus = LevelValidationStatus.Unverified;
            levelEditor.CurrentLevel.solutionActions = string.Empty;
        }

        private void ShowReferenceSolution()
        {
            PrepareCurrentEditorLevelForSave();
            if (!HasValidReferenceSolution(levelEditor.CurrentLevel))
            {
                ShowInvalidReferenceSolutionMessage();
                return;
            }

            referenceSolutionActions = levelEditor.CurrentLevel.solutionActions;
            referenceSolutionStepIndex = 0;
            ResetReferenceSolutionInputState();
            referenceSolutionActive = true;
            editorMode = false;
            gameActive = true;
            testLevelActive = false;
            saveProgressForCurrentLevel = false;
            currentLevelSolved = false;
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            HideReferenceSolutionPanel();
            levelListView.Hide();
            HideLevelFilePanel();
            HideMainFlowEditor();
            runtimeEditorView.Hide();
            boardModel.Load(levelEditor.CurrentLevel.Clone());
            boardRenderer.Render(boardModel);
            ShowPlayHudLevelName(true);
            SetLevelName(levelEditor.CurrentLevel.displayName + " (参考解法)");
            SetOperationHint("空格播放下一步，Z 回退一步，R 重开，ESC 参考解法菜单。");
            UpdateReferenceSolutionProgressStatus();
        }

        private bool HasValidReferenceSolution(LevelData level)
        {
            if (level == null || LevelValidator.GetValidationStatusText(level) != LevelValidationStatus.Solved || string.IsNullOrWhiteSpace(level.solutionActions))
            {
                return false;
            }

            for (int i = 0; i < level.solutionActions.Length; i++)
            {
                if (!IsSolutionAction(level.solutionActions[i]))
                {
                    return false;
                }
            }

            BoardModel previewModel = new BoardModel();
            previewModel.Load(level.Clone());
            for (int i = 0; i < level.solutionActions.Length; i++)
            {
                MoveResult result = previewModel.TryMove(GetDirectionFromSolutionAction(level.solutionActions[i]));
                if (result == MoveResult.Blocked)
                {
                    return false;
                }
            }

            return previewModel.IsSolved;
        }

        private void ShowInvalidReferenceSolutionMessage()
        {
            const string message = "当前未记录有效解，点击关卡求解按钮可尝试获取关卡解法";
            if (popUpView != null)
            {
                popUpView.Show(message, null);
                return;
            }

            SetStatus(message);
        }

        private static bool IsSolutionAction(char action)
        {
            return action == 'U' || action == 'D' || action == 'L' || action == 'R';
        }

        private static Vector2Int GetDirectionFromSolutionAction(char action)
        {
            switch (action)
            {
                case 'U':
                    return Vector2Int.up;
                case 'D':
                    return Vector2Int.down;
                case 'L':
                    return Vector2Int.left;
                case 'R':
                    return Vector2Int.right;
                default:
                    return Vector2Int.zero;
            }
        }

        private void TestEditorLevel()
        {
            levelEditor.CurrentLevel.displayName = runtimeEditorView.GetLevelName("Unsaved Test");
            if (string.IsNullOrWhiteSpace(levelEditor.CurrentLevel.id))
            {
                levelEditor.CurrentLevel.id = LevelSaveSystem.CreateStableLevelId();
            }
            LevelValidationResult validationResult = LevelValidator.ValidateSimple(levelEditor.CurrentLevel);
            if (!validationResult.Passed)
            {
                SetStatus("无法试玩：\n" + string.Join("\n", validationResult.errors));
                return;
            }

            editorMode = false;
            runtimeEditorView.Hide();
            levelListView.Hide();
            HideLevelFilePanel();
            HideMainFlowEditor();
            gameActive = true;
            testLevelActive = true;
            referenceSolutionActive = false;
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            HideReferenceSolutionPanel();
            boardModel.Load(levelEditor.CurrentLevel.Clone());
            boardRenderer.Render(boardModel);
            saveProgressForCurrentLevel = false;
            currentLevelSolved = false;
            ShowPlayHudLevelName(true);
            SetLevelName(levelEditor.CurrentLevel.displayName + " (试玩)");
            SetOperationHint("WASD/方向键移动，Z 撤销，R 重开，ESC 测试菜单。");
            SetStatus("正在测试编辑中的关卡。");
        }

        private void SetStatus(string message)
        {
            if (playHudView != null)
            {
                playHudView.SetStatus(message);
            }
        }

        private void SetLevelName(string levelName)
        {
            if (playHudView != null)
            {
                playHudView.SetLevelName(levelName);
            }
        }

        private void SetOperationHint(string operationHint)
        {
            if (playHudView != null)
            {
                playHudView.SetOperationHint(operationHint);
            }
        }

        private void ShowPlayHudLevelName(bool visible)
        {
            if (playHudView != null)
            {
                playHudView.SetLevelNameVisible(visible);
            }
        }

        private void SetSolverBlockingOverlayVisible(bool visible)
        {
            if (runtimeEditorView != null)
            {
                runtimeEditorView.SetSolverBlockingOverlayVisible(visible);
            }
        }

        private void EnsureRuntimeBackground()
        {
            if (runtimeBackgroundRenderer == null)
            {
                GameObject backgroundObject = new GameObject("Runtime Background");
                runtimeBackgroundRenderer = backgroundObject.AddComponent<SpriteRenderer>();
                runtimeBackgroundRenderer.sortingOrder = -1000;
            }

            runtimeBackgroundRenderer.sprite = runtimeBackgroundSprite;
            runtimeBackgroundRenderer.color = runtimeBackgroundColor;
            runtimeBackgroundRenderer.enabled = runtimeBackgroundSprite != null;
            UpdateRuntimeBackground();
        }

        private void UpdateRuntimeBackground()
        {
            if (runtimeBackgroundRenderer == null)
            {
                return;
            }

            runtimeBackgroundRenderer.sprite = runtimeBackgroundSprite;
            runtimeBackgroundRenderer.color = runtimeBackgroundColor;
            runtimeBackgroundRenderer.enabled = runtimeBackgroundSprite != null;
            if (runtimeBackgroundSprite == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic)
            {
                return;
            }

            runtimeBackgroundRenderer.transform.position = new Vector3(
                camera.transform.position.x,
                camera.transform.position.y,
                0f);

            Vector2 spriteSize = runtimeBackgroundSprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            float cameraHeight = camera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * camera.aspect;
            float scale = Mathf.Max(cameraWidth / spriteSize.x, cameraHeight / spriteSize.y);
            runtimeBackgroundRenderer.transform.localScale = Vector3.one * scale;
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

        private void ShowLevelBoundaryPopup(string message)
        {
            if (popUpView == null)
            {
                SetStatus(message);
                return;
            }

            popUpView.Show(message, "确认", "取消", null);
        }

        private void CaptureEditorBaselineSnapshot()
        {
            editorBaselineSnapshotJson = CreateEditorSnapshotJson();
        }

        private bool HasUnsavedEditorChanges()
        {
            return editorBaselineSnapshotJson != CreateEditorSnapshotJson();
        }

        private string CreateEditorSnapshotJson()
        {
            if (levelEditor == null || levelEditor.CurrentLevel == null)
            {
                return string.Empty;
            }

            LevelData snapshot = levelEditor.CurrentLevel.Clone();
            snapshot.id = string.Empty;
            snapshot.displayName = runtimeEditorView != null ? runtimeEditorView.GetLevelName() : snapshot.displayName;
            snapshot.validationStatus = LevelValidationStatus.Unverified;
            snapshot.solutionActions = string.Empty;
            snapshot.EnsureTiles();
            return JsonUtility.ToJson(snapshot);
        }

        private void InitializeUi()
        {
            mainMenuView.Initialize(ContinueGame, ShowLevelList, RequestClearProgress, ShowLevelFilePanel, ShowMainFlowEditor, ExitGame);
            mainMenuView.Hide();

            if (popUpView != null)
            {
                popUpView.Hide();
            }

            commonMenuView.Initialize(
                ReturnToGame,
                ReturnToTitlePage,
                ShowMainFlowLevelListFromCommonMenu,
                LoadPreviousLevelFromCommonMenu,
                LoadNextLevelFromCommonMenu,
                RestartLevelFromCommonMenu,
                EnterEditorFromCommonMenu);
            commonMenuView.Hide();

            commonEditorMenuView.Initialize(
                ReturnToEditor,
                ReturnToTitlePageFromEditor,
                TestLevelFromEditorMenu,
                SaveLevelFromEditorMenu,
                DeleteLevelFromEditorMenu);
            commonEditorMenuView.Hide();

            if (testLevelPanelView != null)
            {
                testLevelPanelView.Initialize(ReturnToTestLevel, ReturnToEditorFromTestLevel, RestartTestLevel);
                testLevelPanelView.Hide();
            }

            if (referenceSolutionPanelView != null)
            {
                referenceSolutionPanelView.Initialize(ReturnToReferenceSolution, ReturnToEditorFromReferenceSolution, RestartReferenceSolution);
                referenceSolutionPanelView.Hide();
            }

            levelListView.Initialize(ShowStartPage, HandleLevelListFilterChanged);
            levelListView.Hide();

            if (levelFilePanelView != null)
            {
                levelFilePanelView.Initialize(
                    CreateLevelFromFilePanel,
                    DeleteLevelFromFilePanel,
                    CopyLevelFromFilePanel,
                    RenameLevelFromFilePanel,
                    EditLevelFromFilePanel,
                    ImportLevelsFromFilePanel,
                    ExportLevelFromFilePanel,
                    ValidateLevelFromFilePanel,
                    ShowStartPage,
                    HandleLevelFileSelection,
                    HandleLevelFilePanelFilterChanged);
                levelFilePanelView.Hide();
            }

            if (mainFlowEditorView != null)
            {
                mainFlowEditorView.Initialize(
                    AddLevelToMainFlow,
                    RemoveLevelFromMainFlow,
                    MoveMainFlowLevelUp,
                    MoveMainFlowLevelDown,
                    ValidateLevelFromMainFlowEditor,
                    SaveMainFlowEditor,
                    RequestExitMainFlowEditor);
                mainFlowEditorView.Hide();
            }

            runtimeEditorView.Initialize(
                CreateBlankEditorLevel,
                levelEditor.SetTool,
                levelEditor.SetBrushMode,
                UndoEditorLevel,
                TestEditorLevel,
                () => RequestSaveEditorLevel(null),
                ValidateCurrentEditorLevel,
                SolveCurrentEditorLevel,
                StrictSolveCurrentEditorLevel,
                ShowReferenceSolution,
                RequestExitEditor);
            runtimeEditorView.Hide();

            if (playHudView != null)
            {
                playHudView.Show();
            }

            SetSolverBlockingOverlayVisible(false);
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static bool IsControlPressed()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
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

    }
}
