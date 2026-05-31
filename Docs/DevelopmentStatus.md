# 推箱子项目开发状态

## 当前目标

项目当前目标是完成一个 Unity 2D 推箱子游戏，并提供可在运行时使用的关卡编辑、关卡管理和测试流程。当前主要功能已经从“基础可玩”推进到“统一关卡库 + 主流程编排”的阶段。下一阶段重点是完善主流程编辑 UI 配置，并继续推进导入、导出、筛选、排序等关卡文件管理能力。

## 当前整体架构

核心逻辑仍由 `GameController` 统筹，UI 已迁移为手工制作 UI + View 脚本绑定字段：

- `GameController`：游戏状态切换、输入分发、菜单回调、关卡列表、关卡文件面板、编辑器入口、测试关卡流程。
- `BoardModel`：推箱子棋盘规则，处理移动、推箱、撤销、胜利判断。
- `BoardRenderer`：根据棋盘状态渲染 2D 方块对象，并按棋盘长边调整相机可视范围。
- `RuntimeLevelEditor`：运行时关卡编辑数据逻辑，处理工具选择、格子绘制、校验、保存、编辑撤销。
- `LevelSaveSystem`：读取根目录 `Level` 关卡库，保存、复制、删除、重命名关卡，并处理关卡命名冲突与稳定 ID。
- `MainFlowSaveSystem`：读取和保存主流程配置，存放于项目根目录 `Config/main_flow.json`。
- `GameProgressSaveSystem`：保存已完成关卡信息，存放于项目根目录 `Save/progress.json`。

## UI 迁移状态

运行时生成 UI 到手工制作 UI 的迁移已完成。

当前状态：

- `GameController` 不再创建旧的运行时 Canvas、旧侧栏、旧编辑器栏或旧关卡列表。
- `GameController` 只持有并初始化 Inspector 中配置好的 View 引用。
- 新增 UI 应继续采用“手工制作 UI + View 脚本绑定字段”的方式。
- View 统一采用 `container + root` 的显示模式，用于解决父节点默认关闭时首次显示失败的问题。

当前 `GameController` 需要在 Inspector 配置：

- `MainMenuView`
- `PopUpView`
- `CommonMenuView`
- `CommonEditorMenuView`
- `TestLevelPanelView`
- `PlayHudView`
- `LevelListView`
- `LevelFilePanelView`
- `MainFlowEditorView`
- `RuntimeEditorView`

## 已完成的主要功能

### 游戏玩法

- 支持基础推箱子规则：玩家移动、推箱、墙体阻挡、箱子阻挡。
- 支持游玩时撤销一步、重开当前关卡。
- 支持胜利检测。
- 通关后会按实际关卡 key 记录完成状态。
- 通关后弹出确认窗口：
  - 非最后一关：`下一关` / `取消`
  - 最后一关：`返回主界面` / `取消`
- 通关后当前关卡会锁定移动输入，避免玩家继续改动已完成局面。
- 游玩关卡的 ESC 菜单支持返回游戏、返回标题、关卡列表、上一关、下一关、重开、编辑关卡。

### 关卡数据与保存

- 所有关卡统一位于项目根目录 `Level` 文件夹。
- 项目不再从 `Assets/Resources/Levels` 读取关卡。
- 保存格式为 JSON。
- `LevelSaveSystem.LoadAllLevels()` 会从统一关卡库加载全部关卡。
- `LevelSaveSystem.LoadLevelFileEntries()` 会返回统一关卡库条目，用于关卡文件管理面板。
- 当前关卡排序规则是按 `displayName` 升序。
- 主流程配置位于项目根目录 `Config/main_flow.json`。
- 首次没有主流程配置时，会按当前关卡库的 `displayName` 顺序把所有关卡加入主流程并写入配置。
- 主流程配置保存关卡稳定 `id` 顺序；删除关卡后会清理失效 ID。
- 关卡保存、复制、重命名会进行命名冲突处理：
  - 若重名且名称不含 `-n`，尝试追加 `-1`。
  - 若重名且名称已含 `-n`，尝试将 `n` 加 1。
  - 若自动候选名称仍重名，则操作失败，并在 `PlayHudView` 显示失败信息。
- 关卡 `id` 是稳定 ID：新建、复制关卡时生成；重命名和保存不会再用显示名覆盖 `id`。

### 进度存档

当前不再使用“最高已完成关卡索引”推断完成状态，而是记录实际完成的关卡 key。

这样可以避免玩家跳过前面关卡后，完成后面关卡时把前面关卡误标为已完成。

相关逻辑在 `GameProgressSaveSystem`：

- `IsLevelCompleted(LevelData level)`
- `MarkLevelCompleted(LevelData level)`
- `AreAllLevelsCompleted(IReadOnlyList<LevelData> levels)`
- `GetContinueLevelIndex(IReadOnlyList<LevelData> levels)`

## 已拆分的 UI View

### `MainMenuView`

用于开始页面。

包含按钮：

- 继续游戏
- 关卡列表
- 清空存档
- 关卡编辑
- 主流程编辑
- 退出

其中“关卡编辑”当前进入 `LevelFilePanelView`，不再是占位入口。
其中“主流程编辑”当前进入 `MainFlowEditorView`，用于添加、移除和排序进入继续游戏流程的关卡。

### `PopUpView`

用于确认弹窗和命名输入弹窗。

支持：

- 自定义提示文字
- 自定义确认按钮文字
- 自定义取消按钮文字
- 可选第三按钮 `alternateButton`
- 可选命名输入框 `inputField`
- 确认、取消、第三按钮回调

当前用于：

- 清空存档确认
- 退出编辑器确认
- 保存并退出编辑器
- 通关完成弹窗
- 测试关卡完成弹窗
- 新建关卡命名
- 重命名关卡

重要配置：

- `container`：默认关闭的父节点
- `root`：实际弹窗根节点
- `messageText`
- `confirmButtonText`
- `alternateButtonText`
- `cancelButtonText`
- `inputField`
- `confirmButton`
- `alternateButton`
- `cancelButton`

### `CommonMenuView`

用于游玩关卡时按 `ESC` 唤出的菜单。

包含按钮：

- 返回游戏
- 返回标题页面
- 关卡列表
- 上一关
- 下一关
- 重开
- 编辑关卡

其中“关卡列表”与主界面的“关卡列表”按钮一致，进入 `LevelListView` 并清空当前棋盘渲染。

编辑关卡规则：

- 所有关卡都来自统一关卡库，可直接编辑原文件。
- 从游玩界面进入编辑器后，退出编辑器统一回到 `LevelFilePanelView`。

### `CommonEditorMenuView`

用于关卡编辑状态下按 `ESC` 唤出的菜单。

包含按钮：

- 返回编辑
- 返回标题页面
- 试玩
- 保存关卡
- 删除关卡

当前试玩、保存、删除已接入实际逻辑，不再是占位方法。

### `TestLevelPanelView`

用于编辑器测试关卡时按 `ESC` 唤出的测试菜单。

包含按钮：

- 返回测试
- 返回关卡编辑
- 重开关卡

测试关卡完成时使用专用弹窗：

- `返回关卡编辑`
- `取消`

测试关卡不写入通关进度，也不算退出关卡编辑，因此不会清空编辑撤销历史。

### `PlayHudView`

用于显示 HUD 文本。

只管理三个 TextMeshPro 文本：

- 关卡名
- 操作提示
- 当前状态说明

当前很多操作结果都会写入 HUD，例如保存失败、复制关卡结果、命名重复、当前编辑工具、撤销结果。

### `LevelListView`

用于游玩关卡列表页面。

负责：

- 显示/隐藏关卡列表
- 管理滚动列表内容根节点
- 生成关卡项
- 筛选主流程关卡、未加入主流程关卡、全部关卡
- 空列表提示
- 返回标题页按钮

点击关卡项后进入对应关卡游玩。
默认筛选为主流程关卡。非主流程关卡可以从筛选列表进入游玩，但不会写入主流程继续游戏进度。

### `LevelFilePanelView`

用于关卡文件管理页面。

负责：

- 显示全部关卡文件条目
- 选中关卡条目
- 新建关卡
- 删除关卡
- 复制关卡
- 重命名关卡
- 编辑关卡
- 返回标题页

当前规则：

- 所有关卡都来自统一关卡库。
- 所有关卡均可编辑原文件、删除、复制、重命名。

### `LevelListItemView`

用于单个游玩关卡列表项。

显示：

- 序号
- 关卡名
- 完成状态
- 是否在主流程中

点击后进入对应关卡。

### `LevelFileListItemView`

用于关卡文件管理列表项。

显示：

- 序号
- 关卡名
- 是否在主流程中
- 当前选中状态

### `MainFlowEditorView`

用于编辑游戏主要流程。

负责：

- 显示已加入主流程的关卡列表。
- 显示未加入主流程、可添加的关卡列表。
- 添加关卡到主流程。
- 从主流程移除关卡。
- 上移 / 下移关卡，调整主流程顺序。
- 保存主流程配置。
- 返回标题页。

当前 `GameController` 会根据 `MainFlowSaveSystem` 读取的主流程关卡决定继续游戏、通关下一关、ESC 菜单上一关和下一关的目标。

### `MainFlowLevelItemView`

用于主流程编辑页面中的单个关卡列表项。

显示：

- 序号
- 关卡名
- 当前选中状态

### `RuntimeEditorView`

用于运行时编辑器 UI。

包含：

- 关卡名称输入框
- 宽度输入框
- 高度输入框
- 新建按钮
- 工具按钮：地板、墙、空白、玩家、箱子、目标点、擦除实体
- 撤销按钮
- 试玩按钮
- 保存按钮
- 退出按钮

当前行为：

- 命名输入框只允许英文字母、数字、空格、`-`、`_`，长度上限 50。
- 宽高输入框只允许整数，范围为 1 到 20。实际创建关卡时仍会经过 `LevelData.CreateBlank()` 的最小尺寸修正。
- 工具按钮使用 `Image.color` 维护自身高亮状态，选中工具为浅蓝色。
- 进入编辑器、创建新棋盘、从测试返回编辑器时默认选择地板工具。
- 退出编辑器弹窗支持 `确认`、`保存并退出`、`取消`。

## 运行时编辑器状态

### 已完成

- 支持从关卡文件管理入口新建关卡。
- 支持从关卡文件管理入口编辑关卡库中的任意关卡。
- 支持从游玩菜单直接编辑当前关卡。
- 支持保存关卡，并执行命名冲突处理。
- 支持删除当前正在编辑的已保存关卡。
- 支持测试关卡，且测试流程与普通游玩流程分离。
- 支持编辑撤销，`Ctrl+Z` 和撤销按钮均可使用。
- 编辑撤销上限为 20 步。
- 编辑撤销以鼠标按下到松开作为一步，一次拖拽绘制多个格子只需要撤销一次。
- 退出关卡编辑会清空编辑撤销历史；进入测试关卡不会清空。

### 仍需完善

- 未保存修改检测尚未完成。
- 退出编辑器、新建关卡、切换关卡时还没有统一的 dirty-state 判断。
- 关卡合法性校验仍是基础版，只检查玩家、箱子、目标点等基础条件。
- 编辑操作还不支持框选、填充、批量替换等高级功能。

## 渲染与交互状态

### 棋盘渲染

- 棋盘格子使用世界坐标 `(x, y, 0)` 生成 Sprite。
- 相机按棋盘长边调整 `orthographicSize`，避免宽棋盘比方形棋盘更容易与 UI 冲突。
- 游玩模式下 `LevelTile.Empty` 不绘制格子，因此显示为透明。
- 编辑模式下 `LevelTile.Empty` 绘制为半透明地块，便于看出可编辑区域。

### UI 与棋盘点击冲突

编辑器棋盘绘制逻辑依赖 `IsPointerOverUi()`，只有鼠标不在 UI 上时才绘制：

```csharp
if (Input.GetMouseButtonDown(0) && !IsPointerOverUi())
{
    levelEditor.BeginEditOperation();
    levelEditor.TryPaintFromScreenPosition(Input.mousePosition);
}
```

UI 配置要求：

- 工具按钮、输入框、弹窗按钮等交互控件应开启 `Raycast Target`。
- 不希望阻挡棋盘绘制的大背景 Image 应关闭 `Raycast Target`。
- 弹窗、ESC 菜单、测试菜单可以使用全屏遮罩阻挡底层棋盘，这是预期行为。

### TextMeshPro 字体警告

若出现类似：

```text
Unable to add the requested character to font asset ... atlas texture.
Please make the texture readable.
```

含义：

- TMP Font Asset 缺少某些字符。
- TextMeshPro 尝试动态加入 Atlas。
- 但 Atlas Texture 不可读，动态添加失败。

建议：

- 手工 UI 中所有文本优先使用 TextMeshPro。
- 检查字体资源 `Atlas Population Mode`。
- 若使用 Dynamic 字体，确保 Atlas Texture 可读。
- 为中文字体配置 fallback，避免部分中文标点缺字。

## 当前主流程编排状态

### 当前问题

当前已经取消“内置关卡 / 用户关卡”的核心分类，并加入主流程配置：

- 所有关卡从项目根目录 `Level` 文件夹读取。
- `LevelFileEntry` 只表示关卡库条目和文件路径。
- 删除、重命名、编辑、复制等行为不再根据来源分支处理。
- `id` 作为稳定标识，不再跟随显示名变化。
- `MainFlowData` 保存进入继续游戏流程的关卡 ID 顺序。
- `ContinueGame()`、通关下一关、ESC 菜单上一关 / 下一关都基于主流程关卡列表。

主流程不是关卡分类，而是一份可编辑的有序关卡编排。

### 数据划分方向

当前已有主流程模型；后续可继续新增更明确的关卡元信息模型，例如：

- `LevelRecord`
  - `LevelData level`
  - `string filePath`
  - `List<string> tags`
  - `DateTime createdTime`
  - `DateTime modifiedTime`
- `MainFlowData`
  - `List<string> levelIdsInOrder`

合理划分建议：

- 关卡库负责管理所有关卡资源。
- 主流程配置负责记录进入继续游戏流程的关卡 ID 与顺序。
- 是否在主流程中应由 `MainFlowData.levelIdsInOrder.Contains(level.id)` 推导。
- 标签只用于辅助筛选，例如教学、困难、测试、导入等，不承担主流程顺序职责。

### 关卡列表合并方向

当前已新增主流程编排页面，并让 `LevelListView` 支持主流程关卡、未加入主流程、全部关卡筛选。后续仍建议合并 `LevelListView` 与 `LevelFilePanelView` 背后的数据源，而不是立即合并 UI。

推荐路径：

1. 后续让 `LevelSaveSystem` 提供统一的 `LoadLevelRecords()`，补齐标签与元信息。
2. `LevelFilePanelView` 继续负责全部关卡资源管理。
3. 如果 UI 形态稳定，再考虑抽出通用的列表控件或列表项基类。

不要急于把“游玩关卡列表”和“关卡文件管理”合成一个 View，因为两者交互目标不同：

- `LevelListView`：选择后立即进入游玩。
- `LevelFilePanelView`：选择后再执行新建、复制、删除、重命名、编辑、导入、导出等操作。

## 后续功能规划

### 导入

建议功能：

- 从外部 `.json` 文件导入关卡。
- 导入前执行 JSON 解析和 `LevelValidator.Validate()`。
- 若名称冲突，沿用当前 `ResolveLevelDisplayName()` 规则。
- 导入成功后保存到关卡库目录，并刷新 `LevelFilePanelView`。

需要注意：

- 外部 JSON 可能缺少 `id`、`displayName`、`tiles` 或实体列表，需要在导入时补齐默认值并校验。
- 导入失败应明确提示是格式错误、校验失败还是命名冲突。

### 导出

建议功能：

- 允许导出关卡库中的任意关卡。
- 导出使用当前 JSON 格式。
- 默认导出文件名使用关卡 `displayName` 或 `id` 的安全文件名。
- 导出路径可以先固定到项目根目录 `Export` 文件夹，后续再接入系统文件选择器。

### 筛选

建议先支持：

- 主流程关卡
- 未加入主流程
- 全部关卡
- 已完成
- 未完成

后续可增加：

- 名称搜索
- 尺寸范围
- 是否可解
- 是否包含校验错误

### 排序

建议先支持：

- 名称升序 / 降序
- 主流程顺序
- 创建时间升序 / 降序
- 修改时间升序 / 降序
- 完成状态
- 地图尺寸

当前 JSON 数据中没有显式创建时间和修改时间。短期可使用文件系统时间；长期建议在关卡元信息中写入 `createdAt`、`updatedAt`，避免复制、导入、跨平台移动后时间失真。

### 元信息升级

建议后续扩展关卡 JSON 或旁路元数据：

- `author`
- `description`
- `createdAt`
- `updatedAt`
- `source`
- `difficulty`
- `tags`
- `bestStepCount`
- `isVerifiedSolvable`

这些字段会直接服务筛选、排序、导入导出和求解器校验。

## 后续开发建议顺序

1. 在 Unity 场景中补齐 `MainFlowEditorView`、`MainFlowLevelItemView`、主菜单“主流程编辑”按钮、`LevelListView` 筛选下拉框等 Inspector 引用。
2. 将 `LevelSaveSystem.LoadLevelFileEntries()` 升级为 `LoadLevelRecords()`，补齐标签与元信息。
3. 实现导入关卡 JSON 到关卡库目录。
4. 实现导出关卡 JSON 到导出目录。
5. 增加未保存修改检测，统一处理退出、新建、切换、测试前的确认流程。
6. 增加关卡可解性验证入口。
7. 开发求解器基础 BFS，并逐步加入死锁检测和 A* 优化。
