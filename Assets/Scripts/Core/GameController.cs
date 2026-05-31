using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sokoban
{
    public class GameController : MonoBehaviour
    {
        private readonly BoardModel boardModel = new BoardModel();
        private BoardRenderer boardRenderer;
        private RuntimeLevelEditor levelEditor;
        private List<LevelData> levels = new List<LevelData>();
        private List<LevelFileEntry> levelFileEntries = new List<LevelFileEntry>();
        private List<LevelFileEntry> mainFlowEntries = new List<LevelFileEntry>();
        private List<LevelFileEntry> activePlayEntries = new List<LevelFileEntry>();
        private List<LevelFileEntry> levelListEntries = new List<LevelFileEntry>();
        private List<LevelFileEntry> editingMainFlowEntries = new List<LevelFileEntry>();
        private int currentLevelIndex;
        private bool gameActive;
        private bool editorMode;
        private bool testLevelActive;
        private bool saveProgressForCurrentLevel;
        private bool currentLevelSolved;
        private bool editorPaintOperationActive;
        private string editingLevelPath;
        private bool returnToLevelFilePanelOnExit;

        [SerializeField] private MainMenuView mainMenuView;
        [SerializeField] private PopUpView popUpView;
        [SerializeField] private CommonMenuView commonMenuView;
        [SerializeField] private CommonEditorMenuView commonEditorMenuView;
        [SerializeField] private TestLevelPanelView testLevelPanelView;
        [SerializeField] private PlayHudView playHudView;
        [SerializeField] private LevelListView levelListView;
        [SerializeField] private LevelFilePanelView levelFilePanelView;
        [SerializeField] private MainFlowEditorView mainFlowEditorView;
        [SerializeField] private RuntimeEditorView runtimeEditorView;

        private void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();

            boardRenderer = new GameObject("Board Renderer").AddComponent<BoardRenderer>();
            levelEditor = new RuntimeLevelEditor(boardRenderer, SetStatus);
            InitializeUi();
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
                    editorPaintOperationActive = true;
                    levelEditor.BeginEditOperation();
                    levelEditor.TryPaintFromScreenPosition(Input.mousePosition);
                }
                else if (Input.GetMouseButton(0) && editorPaintOperationActive && !IsPointerOverUi())
                {
                    levelEditor.TryPaintFromScreenPosition(Input.mousePosition);
                }

                if (Input.GetMouseButtonUp(0))
                {
                    EndEditorPaintOperation();
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
            List<string> errors = LevelValidator.Validate(level);
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
            saveProgressForCurrentLevel = false;
            currentLevelSolved = false;
            boardRenderer.ClearBoard();
            ShowPlayHudLevelName(false);
            SetOperationHint(string.Empty);
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            levelEditor.ClearUndoHistory();
            editorPaintOperationActive = false;
            mainMenuView.Show();
            levelListView.Hide();
            HideLevelFilePanel();
            HideMainFlowEditor();
            runtimeEditorView.Hide();
            editingLevelPath = string.Empty;
            returnToLevelFilePanelOnExit = false;

            ReloadLevelFileEntries();
            ReloadMainFlowEntries();
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
            ShowLevelList(true, LevelListFilter.All);
        }

        private void ShowMainFlowLevelListFromCommonMenu()
        {
            ShowLevelList(false, LevelListFilter.MainFlow);
        }

        private void ShowLevelList(bool allowFilter, LevelListFilter defaultFilter)
        {
            gameActive = false;
            editorMode = false;
            testLevelActive = false;
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
            mainMenuView.Hide();
            levelListView.Show();
            HideLevelFilePanel();
            HideMainFlowEditor();
            runtimeEditorView.Hide();
            SetLevelName("关卡列表");
            SetOperationHint(string.Empty);
            SetStatus(allowFilter ? "当前显示全部关卡。" : "当前显示主流程关卡。");
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
            switch (filter)
            {
                case LevelListFilter.NotInMainFlow:
                    SetStatus("当前显示未加入主流程的关卡。");
                    break;
                case LevelListFilter.All:
                    SetStatus("当前显示全部关卡。");
                    break;
                default:
                    SetStatus("当前显示主流程关卡。");
                    break;
            }
        }

        private List<LevelFileEntry> GetLevelListEntries(LevelListFilter filter)
        {
            HashSet<string> mainFlowIds = GetMainFlowIdSet();
            switch (filter)
            {
                case LevelListFilter.NotInMainFlow:
                    return levelFileEntries
                        .Where(entry => entry != null && entry.level != null && !mainFlowIds.Contains(entry.level.id))
                        .ToList();
                case LevelListFilter.All:
                    return levelFileEntries.ToList();
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
            gameActive = true;
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
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
            saveProgressForCurrentLevel = false;
            currentLevelSolved = false;
            boardRenderer.ClearBoard();
            ReloadLevels();
            RefreshLevelFilePanel();
            ShowPlayHudLevelName(true);
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
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
            ReloadLevelFileEntries();
            ReloadMainFlowEntries();
            if (levelFilePanelView != null)
            {
                levelFilePanelView.Rebuild(levelFileEntries, IsInMainFlow);
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

        private void RefreshMainFlowEditor()
        {
            if (mainFlowEditorView == null)
            {
                return;
            }

            HashSet<string> editingIds = new HashSet<string>(
                editingMainFlowEntries
                    .Where(entry => entry != null && entry.level != null && !string.IsNullOrWhiteSpace(entry.level.id))
                    .Select(entry => entry.level.id));
            List<LevelFileEntry> availableEntries = levelFileEntries
                .Where(entry => entry != null && entry.level != null && !editingIds.Contains(entry.level.id))
                .ToList();
            mainFlowEditorView.Rebuild(editingMainFlowEntries, availableEntries);
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

            editingMainFlowEntries.Add(entry);
            RefreshMainFlowEditor();
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
            RefreshMainFlowEditor();
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
            RefreshMainFlowEditor();
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

            popUpView.Show("测试关卡完成！", "返回关卡编辑", "取消", ReturnToEditorFromTestLevel);
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
            runtimeEditorView.SelectTool(EditorTool.Floor);
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
            levelEditor.ClearUndoHistory();
            editorPaintOperationActive = false;
            ShowStartPage();
        }

        private void TestLevelFromEditorMenu()
        {
            HideCommonEditorMenu();
            TestEditorLevel();
        }

        private void SaveLevelFromEditorMenu()
        {
            HideCommonEditorMenu();
            SaveEditorLevel();
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
            ShowPlayHudLevelName(true);
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
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
            runtimeEditorView.SelectTool(EditorTool.Floor);
            SetLevelName("关卡编辑器");
            SetOperationHint("鼠标绘制关卡，Ctrl+Z/撤销按钮撤销，ESC 菜单。");
            SetStatus(statusMessage);
        }

        private void ExitEditor()
        {
            EndEditorPaintOperation();
            editorMode = false;
            testLevelActive = false;
            runtimeEditorView.Hide();
            levelListView.Hide();
            HideMainFlowEditor();
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
            ReloadLevels();
            levelEditor.ClearUndoHistory();
            editorPaintOperationActive = false;
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
            if (popUpView == null)
            {
                ExitEditor();
                return;
            }

            popUpView.Show(
                "是否已保存当前关卡？若直接退出，则放弃当前关卡的编辑进度。",
                "直接退出",
                "保存并退出",
                "取消",
                ExitEditor,
                SaveAndExitEditor);
        }

        private void CreateBlankEditorLevel()
        {
            int width = runtimeEditorView.GetWidth(8);
            int height = runtimeEditorView.GetHeight(8);
            levelEditor.CreateBlank(width, height);
            levelEditor.CurrentLevel.displayName = runtimeEditorView.GetLevelName();
            levelEditor.CurrentLevel.id = LevelSaveSystem.CreateStableLevelId();
            runtimeEditorView.SetLevelFields(
                levelEditor.CurrentLevel.displayName,
                levelEditor.CurrentLevel.width,
                levelEditor.CurrentLevel.height);
            runtimeEditorView.SelectTool(EditorTool.Floor);
            SetLevelName("关卡编辑器");
        }

        private void UndoEditorLevel()
        {
            levelEditor.Undo();
        }

        private bool SaveEditorLevel()
        {
            levelEditor.CurrentLevel.displayName = runtimeEditorView.GetLevelName();
            if (string.IsNullOrWhiteSpace(levelEditor.CurrentLevel.id))
            {
                levelEditor.CurrentLevel.id = LevelSaveSystem.CreateStableLevelId();
            }

            try
            {
                editingLevelPath = levelEditor.Save(editingLevelPath);
                runtimeEditorView.SetLevelFields(
                    levelEditor.CurrentLevel.displayName,
                    levelEditor.CurrentLevel.width,
                    levelEditor.CurrentLevel.height);
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
            if (SaveEditorLevel())
            {
                ExitEditor();
            }
        }

        private void TestEditorLevel()
        {
            levelEditor.CurrentLevel.displayName = runtimeEditorView.GetLevelName("Unsaved Test");
            if (string.IsNullOrWhiteSpace(levelEditor.CurrentLevel.id))
            {
                levelEditor.CurrentLevel.id = LevelSaveSystem.CreateStableLevelId();
            }
            List<string> errors = levelEditor.Validate();
            if (errors.Count > 0)
            {
                SetStatus("无法试玩：\n" + string.Join("\n", errors));
                return;
            }

            editorMode = false;
            runtimeEditorView.Hide();
            levelListView.Hide();
            HideLevelFilePanel();
            HideMainFlowEditor();
            gameActive = true;
            testLevelActive = true;
            HideCommonMenu();
            HideCommonEditorMenu();
            HideTestLevelPanel();
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
                    ShowStartPage,
                    HandleLevelFileSelection);
                levelFilePanelView.Hide();
            }

            if (mainFlowEditorView != null)
            {
                mainFlowEditorView.Initialize(
                    AddLevelToMainFlow,
                    RemoveLevelFromMainFlow,
                    MoveMainFlowLevelUp,
                    MoveMainFlowLevelDown,
                    SaveMainFlowEditor,
                    ShowStartPage);
                mainFlowEditorView.Hide();
            }

            runtimeEditorView.Initialize(
                CreateBlankEditorLevel,
                levelEditor.SetTool,
                UndoEditorLevel,
                TestEditorLevel,
                () => SaveEditorLevel(),
                RequestExitEditor);
            runtimeEditorView.Hide();

            if (playHudView != null)
            {
                playHudView.Show();
            }
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
