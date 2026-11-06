using Vn.Authoring.Chapters;
using Vn.Authoring.Definition;
using Vn.Authoring.Editing;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Services;

/// <summary>
/// 앱이 지금 무엇을 열고 있는지 아는 유일한 객체.
///
/// 편집 자체는 <see cref="Vn.Authoring.Editing.ProjectEditor"/>가 한다. 이 클래스는 그 위에
/// 파일 경로·저장 여부·선택 상태처럼 <em>도구</em>의 관심사만 얹는다.
/// 도메인이 파일 경로를 알 필요는 없고, 알게 되면 콘솔이나 테스트에서 쓰기 어려워진다.
/// </summary>
internal sealed class AuthoringSession
{
    private readonly HashSet<string> _expandedFileIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _knownFileIds = new(StringComparer.Ordinal);
    private readonly HashSet<GraphSceneKey> _collapsedScenes = [];
    private readonly HashSet<string> _hiddenChoiceNodeIds = new(StringComparer.Ordinal);
    private string _fileListSignature = string.Empty;
    private string _savedSnapshot;

    public AuthoringSession()
    {
        Editor = new ProjectEditor(NewProjectInstance());
        ResetFileGraphState(Editor.Project);
        _savedSnapshot = ProjectSnapshotCodec.Encode(Editor.Project);
        Editor.Changed += OnEditorChanged;
    }

    public ProjectEditor Editor { get; }

    public StoryProject Project => Editor.Project;

    /// <summary>현재 프로젝트 manifest 경로. 아직 저장하지 않았으면 null이다.</summary>
    public string? ProjectPath { get; private set; }

    /// <summary>
    /// `game.definition.json`의 내용 — 기획자가 소유하는 어휘(스탯·조건·화자). 화면은 그릴 때마다 여기서 읽는다.
    /// </summary>
    public GameDefinition Definition { get; private set; } = GameDefinition.Empty;

    // 주의: 챕터에서 오는 화자 어휘는 없다 — 화자의 유일한 원천은 <see cref="Definition"/>이다.

    /// <summary>
    /// 새 노드가 추가될 현재 작업 파일.
    ///
    /// 그래프에 펼쳐 보이는 파일 목록과는 독립된 workspace 상태다. 현재 파일을 접어 둔
    /// 상태에서도 그 파일을 계속 작업 대상으로 유지할 수 있다.
    /// </summary>
    public string? ActiveFileId { get; private set; }

    public StoryFile? ActiveFile => Project.FindFile(ActiveFileId);

    /// <summary>
    /// 그래프에 실제 NodeCard로 펼쳐 보일 StoryFile Id 목록.
    ///
    /// 저장 콘텐츠나 Undo 대상이 아니라 앱 workspace 상태다. 파일을 접어도 StoryProject와
    /// 실행·Settings 연결은 전혀 바뀌지 않는다.
    /// </summary>
    public IReadOnlySet<string> ExpandedFileIds => _expandedFileIds;

    /// <summary>
    /// 펼친 판 안에서 접어 둔 장면들 (하이어아키의 눈, <c>docs/plans/hierarchy.md</c>). 그 장면만 줄 목록
    /// 상자로 간소화된다. <see cref="ExpandedFileIds"/>와 같은 작업 상태다 — 저장 · 되돌리기 대상이 아니다.
    /// </summary>
    public IReadOnlySet<GraphSceneKey> CollapsedScenes => _collapsedScenes;

    /// <summary>
    /// [대본] 탭에서 빈 선택지 칸을 감춘 카드들 (인스펙터의 「빈 선택지 숨기기」). 여기 든 카드는 <b>이은 선택지만</b> 세운다 —
    /// 안 이은 칸은 자동 길 자리까지 감춘다. 여기 없는 카드는 칸이 셋 선다. 화면 편의라 프로젝트 파일이 아니라 사용자 설정에
    /// 프로젝트별로 기억한다.
    /// </summary>
    public IReadOnlySet<string> HiddenChoiceNodeIds => _hiddenChoiceNodeIds;

    /// <summary>
    /// [구성] 탭에서 빈 선택지를 감추는가 — 그 탭 툴바의 「빈 선택지 숨기기」(하나다). 켜면 [구성] 카드는 빈 선택지 점과 들어오는
    /// 길 없는 입구 점을 감춘다. [대본] 탭은 카드마다 정한다(<see cref="HiddenChoiceNodeIds"/>). 화면 편의라 프로젝트 파일이
    /// 아니라 사용자 설정에 기억한다(앱 전체).
    /// </summary>
    public bool HideEmptyChoices { get; private set; } = AppSettingsService.LoadHideEmptyChoices();

    /// <summary>[구성] 탭의 「빈 선택지 숨기기」가 바뀌었다 — [구성] 판이 다시 그린다.</summary>
    public event EventHandler? HideEmptyChoicesChanged;

    public string? SelectedNodeId { get; private set; }

    public StoryNode? SelectedNode => Project.FindNode(SelectedNodeId);

    public string StatusMessage { get; private set; } = "새 프로젝트입니다. 노드를 추가해 시작하세요.";


    /// <summary>저장한 뒤로 바뀐 것이 있는지. 실제 내용을 비교하므로 되돌리기로 원상복구하면 깨끗해진다.</summary>
    public bool IsDirty => !string.Equals(_savedSnapshot, ProjectSnapshotCodec.Encode(Project), StringComparison.Ordinal);

    public event EventHandler<ProjectChangedEventArgs>? Changed;

    /// <summary>
    /// 상태줄 글자가 바뀌었을 때. <b><see cref="Changed"/>와 갈라 둔 이유가 성능이다</b>:
    /// <see cref="SetStatus"/>가 프로젝트 변경으로 방송되면,
    /// "에피소드 3개를 반영했습니다" 한 줄이 챕터 전체 재읽기 + 판 재구성을
    /// 부른다. 동기화가 상태를 여러 번 적으면 그만큼 되풀이되고, 그 되풀이가 다시
    /// 상태를 적어 스스로를 먹인다 — 노드 60개에서 첫 화면이 58초 걸린다.
    ///
    /// 상태줄은 프로젝트가 아니다. 듣는 쪽은 글자만 갈아 끼운다.
    /// </summary>
    public event EventHandler? StatusChanged;

    public event EventHandler? SelectionChanged;

    /// <summary>
    /// 현재 파일 또는 그래프 펼침 상태가 바뀌었을 때.
    /// StoryProject 변경이 아니므로 <see cref="Changed"/>와 분리한다.
    /// </summary>
    public event EventHandler<FileGraphStateChangedEventArgs>? FileGraphStateChanged;

    public void Select(string? nodeId)
    {
        if (string.Equals(SelectedNodeId, nodeId, StringComparison.Ordinal))
        {
            return;
        }

        SelectedNodeId = nodeId;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // 상태를 모두 맞춘 뒤에 알린다. 알린 다음에 상태를 바꾸면
    // 화면은 방금 지나간 값을 그린 채로 남는다.
    /// <summary>
    /// 게임 폴더 설정 —프로젝트 폴더의 <c>runtime.target.json</c>. 없으면 null. 풀린 절대 경로가 실제로
    /// 있을 때만 <see cref="GameFolder"/>가 있다 — 다른 PC에서 상대 경로가 빗나가면 아무것도 안 쓴다.
    /// </summary>
    public RuntimeTarget? RuntimeTarget { get; private set; }

    public string? GameFolder =>
        ProjectPath is { } path && RuntimeTarget is { } target ? target.ResolveExisting(path) : null;

    public void ReloadRuntimeTarget() =>
        RuntimeTarget = ProjectPath is { } path ? RuntimeTarget.LoadBeside(path) : null;

    public void NewProject()
    {
        StoryProject project = NewProjectInstance();

        ProjectPath = null;
        Definition = GameDefinition.Empty;
        RuntimeTarget = null;
        _savedSnapshot = ProjectSnapshotCodec.Encode(project);
        StatusMessage = "새 프로젝트입니다. 노드를 추가해 시작하세요.";

        ResetFileGraphState(project);
        Editor.Replace(project);
        Select(project.EnumerateNodes().FirstOrDefault()?.Id);
        RaiseFileGraphStateChanged(
            activeFileChanged: true,
            expandedFilesChanged: true,
            fileListChanged: true);
    }

    /// <summary>
    /// 연출을 담은 옛 프로젝트를 열었을 때 한 줄 — 연출은 이 툴이 읽지 않고(저장하면 사라진다) 원본은 남겼다.
    /// 연출은 ked-story-compositor가 그 원본에서 가져간다.
    /// </summary>
    private static string PresentationBackupNote(string? backupDirectory) =>
        backupDirectory is null
            ? string.Empty
            : $" · 연출 데이터는 읽지 않습니다 — 원본을 '{Path.GetFileName(backupDirectory)}' 폴더에 남겼습니다(연출은 ked-story-compositor에서)";

    public void Open(string path)
    {
        ProjectLoadResult loaded = ProjectStore.Load(path);
        (StoryProject project, string alignment) = AlignBoards(loaded.Project);

        ProjectPath = loaded.ManifestPath;
        Definition = GameDefinition.LoadBeside(loaded.ManifestPath);
        ReloadRuntimeTarget();
        _savedSnapshot = ProjectSnapshotCodec.Encode(project);
        StatusMessage =
            $"{Path.GetFileName(Path.GetDirectoryName(ProjectPath))}/ · 노드 {project.EnumerateNodes().Count()}개" +
            alignment +
            PresentationBackupNote(loaded.PresentationBackupDirectory) +
            (RuntimeTarget is null ? " · 게임 폴더 미지정" : $" · 게임 폴더 {RuntimeTarget.GameFolder}") +
            // 열면서 버린 옛 구조(대사 안 선택지 등)는 조용히 지나가지 않는다 — 글을 손봐야 한다.
            (loaded.LoadNotices.Count == 0 ? string.Empty : " · ⚠ " + string.Join(" / ", loaded.LoadNotices));

        AppSettingsService.SaveRecentProject(loaded.ManifestPath);

        string? activeFileId = project.FindFileContainingNode(project.StartNodeId)?.Id
            ?? project.Files.FirstOrDefault()?.Id;
        ResetFileGraphState(project, activeFileId);

        Editor.Replace(project);
        Select(project.StartNodeId ?? project.EnumerateNodes().FirstOrDefault()?.Id);
        RaiseFileGraphStateChanged(
            activeFileChanged: true,
            expandedFilesChanged: true,
            fileListChanged: true);
    }

    /// <summary>
    /// 열 때 판을 챕터에 맞춘다 — <b>저장본 스냅샷을 뜨기 전에</b>.
    ///
    /// 맞추는 것은 카드 없는 에피소드에 빈 카드를 세우는 일과 조건 공급이다.
    /// 규칙은 <see cref="ProjectEditor.AlignBoardsWithChapters"/>에 있다.
    ///
    /// 주의: 스냅샷 전이라 <b>맞춘 것만으로는 저장할 것이 생기지 않는다</b> — 열자마자 "고쳤다"는 표시가
    /// 뜨면 사람은 자기가 무엇을 건드렸는지 찾는다. 맞춘 결과는 다음 저장에 함께 실리고, 저장 전에
    /// 닫으면 다음에 열 때 다시 맞춘다(멱등이다).
    ///
    /// 주의: 맞추다 실패해도 <b>여는 일은 막지 않는다</b> — 맞추기는 묶음이라 실패하면 연 그대로 돌아온다.
    /// </summary>
    /// <returns>맞춘 프로젝트와, 상태줄에 덧붙일 한 마디(맞춘 것이 없으면 빈 글).</returns>
    private static (StoryProject Project, string Note) AlignBoards(StoryProject loaded)
    {
        var aligner = new ProjectEditor(loaded);

        try
        {
            BoardAlignment aligned = aligner.AlignBoardsWithChapters();

            if (!aligned.Changed)
            {
                return (aligner.Project, string.Empty);
            }

            var parts = new List<string>();

            if (aligned.CardsStood.Count > 0)
            {
                parts.Add($"카드가 없던 에피소드 {aligned.CardsStood.Count}개에 빈 카드를 세웠습니다");
            }

            if (aligned.ScenesLifted.Count > 0)
            {
                parts.Add($"자유 씬 {aligned.ScenesLifted.Count}개를 에피소드로 올렸습니다");
            }

            return (aligner.Project, $" · {string.Join(" · ", parts)} — 저장하면 남습니다.");
        }
        catch (Exception exception)
        {
            return (aligner.Project, $" · 판을 챕터에 맞추지 못했습니다: {exception.Message}");
        }
    }

    public void Save(string? path = null)
    {
        string target = path ?? ProjectPath
            ?? throw new InvalidOperationException("저장할 경로가 없습니다.");

        ProjectStore.Save(target, Project);

        ProjectPath = Path.GetFullPath(target);
        _savedSnapshot = ProjectSnapshotCodec.Encode(Project);
        Definition = GameDefinition.LoadBeside(ProjectPath);
        ReloadRuntimeTarget();
        AppSettingsService.SaveRecentProject(ProjectPath);

        bool provisioned = EnsureProjectFolders();

        // 저장은 프로젝트에 쓰는 일 하나다. "다른 이름으로 저장"은 없다 — 프로젝트는 폴더이고,
        // 복제는 탐색기 · 버전은 git이 한다(ProjectFolder).
        StatusMessage = $"{Path.GetFileName(Path.GetDirectoryName(ProjectPath))}/ 에 저장했습니다." +
            (provisioned ? " 없던 정의 파일의 뼈대를 깔았습니다." : string.Empty) +
            (RuntimeTarget is null ? " 게임 폴더가 없습니다 — [🎮 게임 폴더…]로 정하면 진행 JSON이 런타임으로도 나갑니다." : string.Empty);
        Changed?.Invoke(this, new ProjectChangedEventArgs(ProjectChangeKind.Content));
    }

    /// <summary>
    /// 프로젝트 폴더의 기본 살림 — <b>매 저장마다</b> 정의 파일이 없으면 뼈대를 깐다. 손으로 만들어야
    /// 하는 파일이면 새 프로젝트마다 스탯 경고부터 맞는다. 있는 파일은 불가침이다. 무언가
    /// 실제로 만들었을 때만 true — 조용한 저장이 시끄러워지지 않는다.
    /// </summary>
    private bool EnsureProjectFolders() =>
        ProjectPath is not null && GameDefinitionStore.EnsureBeside(ProjectPath);

    public void SetStatus(string message)
    {
        StatusMessage = message;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    // `game.definition.json`은 기획자 전용이다 — 작가 화면은 거기 쓰지 않는다. 작가가 더한
    // 화자는 프로젝트에 산다 (`StoryProject.WriterSpeakers`). 정의 파일에 쓰는 창구는 아래 둘 — 화자 탭의
    // [저장](`SaveSpeakers`)과 검증 보고의 [등록](`RegisterVariables`) — 뿐이고, 둘 다
    // 기획자 화면(챕터 그래프)에서만 불린다.

    /// <summary>
    /// 화자 목록을 <c>game.definition.json</c>에 통째로 쓴다 — 챕터 그래프 [화자] 탭 전용.
    /// 여기서 더한 화자는 게임 정의에 저장되며, 모든 챕터가 공유해서 쓴다.
    ///
    /// <b>통째로 쓰는 것이 규격이다</b> — 더하기만 하는 <see cref="RegisterVariables"/>와
    /// 다르다. 탭에서 지운 이름이 파일에 남으면 목록이 화면과 갈리고, 그 순간부터 어느 쪽이
    /// 진짜인지 아무도 모른다. 쓰고 나면 정의를 다시 읽어 드롭다운이 같은 목록을 본다.
    ///
    /// 초상화 매핑도 같은 배열이 진다(<c>characterId</c>) — 화자의 집이 하나라는 뜻이다.
    /// </summary>
    public bool SaveSpeakers(IReadOnlyList<SpeakerSpec> speakers)
    {
        ArgumentNullException.ThrowIfNull(speakers);

        if (ProjectPath is null)
        {
            SetStatus("화자는 game.definition.json에 저장됩니다. 프로젝트를 먼저 저장해 주세요.");
            return false;
        }

        GameDefinitionStore.SaveSpeakers(ProjectPath, speakers);
        Definition = GameDefinition.LoadBeside(ProjectPath);
        return true;
    }

    /// <summary>
    /// 스탯 키를 <c>game.definition.json</c>의 variables에 더한다 — 검증 보고의 [등록] 단추 전용.
    /// 존재의 원천은 정의 파일이므로(§3.1) 자동 쓰기가 아니라 사람이 누른 순간에만 쓰고,
    /// 없는 이름만 더한다. 쓰고 나면 정의를 다시 읽어 챕터 검증이 새 원천을 본다.
    /// </summary>
    public bool RegisterVariables(IReadOnlyList<VariableSpec> variables)
    {
        if (ProjectPath is null)
        {
            SetStatus("스탯은 game.definition.json에 등록됩니다. 프로젝트를 먼저 저장해 주세요.");
            return false;
        }

        int added = GameDefinitionStore.AddVariables(ProjectPath, variables);
        Definition = GameDefinition.LoadBeside(ProjectPath);

        SetStatus(added > 0
            ? $"{GameDefinition.FileName}에 변수 {added}개를 등록했습니다."
            : "새로 등록할 변수가 없습니다 — 이미 정의 파일에 있습니다.");

        return added > 0;
    }

    /// <summary>
    /// 그 챕터의 판을 보장한다. <b>규칙은 <see cref="ProjectEditor.EnsureChapterBoard"/>가
    /// 갖는다</b> — 세션 상태를 하나도 안 보는 순수 편집 작업이다.
    /// 여기 있는 것은 부르는 자리 스물여덟을 안 건드리기 위한 이름 하나뿐이다.
    /// </summary>
    internal string EnsureChapterBoard(string chapterId) => Editor.EnsureChapterBoard(chapterId);

    /// <summary>
    /// <b>챕터 하나를 세운다</b> — 이름을 받고, 스탯을 정의 파일에서 깔고, 판을 만들어 고른다.
    ///
    /// <b>챕터를 세우는 규칙은 여기 한 벌이다.</b> 두 벌이면 어느 창구로 만들었느냐에 따라 챕터가
    /// 달라진다 — 실제로 스탯을 한쪽만 깔면 그 챕터는 도달성 증명의 탐색 경계가 없다.
    ///
    /// 주의: 챕터는 프로젝트가 든다 — 만드는 파일은 없다.
    /// </summary>
    /// <returns>못 만든 이유. null이면 만들었다.</returns>
    internal string? CreateChapter(string chapterId)
    {
        chapterId = (chapterId ?? string.Empty).Trim();

        if (chapterId.Length == 0)
        {
            return "챕터 Id를 적어 주세요.";
        }

        if (ProjectPath is null)
        {
            return "프로젝트를 먼저 저장해 주세요 — 진행 JSON이 나갈 자리가 프로젝트 폴더에서 정해집니다.";
        }

        if (Editor.FindChapter(chapterId) is not null)
        {
            return $"챕터 '{chapterId}'가 이미 있습니다.";
        }

        // 스탯은 정의 파일의 변수에서 온다.
        ChapterDocument chapter = Editor.EnsureChapter(chapterId);

        foreach (Vn.Authoring.Definition.VariableSpec variable in Definition.Variables)
        {
            chapter.Stats.Add(new ChapterStat(
                variable.Name, variable.Name, Initial: 0, Minimum: 0, Maximum: 100));
        }

        SelectFile(EnsureChapterBoard(chapterId));

        return null;
    }

    /// <param name="reveal">
    /// 접혀 있으면 편다(기본). 하이어아키에서 고를 때는 false다 — 보이기는 눈이 정한다.
    /// </param>
    internal void SelectFile(string? fileId, bool reveal = true)
    {
        if (fileId is not null && Project.FindFile(fileId) is null)
        {
            return;
        }

        if (string.Equals(ActiveFileId, fileId, StringComparison.Ordinal))
        {
            return;
        }

        ActiveFileId = fileId;

        // 선택과 펼침은 독립이다 — 고를 때 다른 파일을 저절로 접으면 "여러 파일을 오가며
        // 함께 본다"가 막힌다. 활성 파일이 접혀 있으면 펴 주기만 한다(고른 파일이 안 보이는 일은 없어야 한다).
        bool expandedChanged = reveal && fileId is not null && _expandedFileIds.Add(fileId);

        RaiseFileGraphStateChanged(
            activeFileChanged: true,
            expandedFilesChanged: expandedChanged,
            fileListChanged: false);
    }

    internal void SetFileExpanded(string fileId, bool expanded)
    {
        if (Project.FindFile(fileId) is null)
        {
            return;
        }

        bool changed = expanded
            ? _expandedFileIds.Add(fileId)
            : _expandedFileIds.Remove(fileId);

        if (changed)
        {
            RaiseFileGraphStateChanged(
                activeFileChanged: false,
                expandedFilesChanged: true,
                fileListChanged: false);
        }
    }

    /// <summary>[대본] 탭에서 그 카드의 빈 선택지 칸을 감추거나(참) 다시 세운다 — 기억하고 판을 다시 세운다.</summary>
    internal void SetHideEmptyChoices(string nodeId, bool hide)
    {
        bool changed = hide ? _hiddenChoiceNodeIds.Add(nodeId) : _hiddenChoiceNodeIds.Remove(nodeId);

        if (!changed)
        {
            return;
        }

        if (ProjectPath is not null)
        {
            AppSettingsService.SaveHiddenChoices(ProjectPath, _hiddenChoiceNodeIds);
        }

        RaiseFileGraphStateChanged(
            activeFileChanged: false,
            expandedFilesChanged: true,
            fileListChanged: false);
    }

    /// <summary>[구성] 탭에서 빈 선택지를 감추거나(참) 다시 세운다 — 기억하고 [구성] 판에 알린다.</summary>
    internal void SetHideEmptyChoices(bool hide)
    {
        if (HideEmptyChoices == hide)
        {
            return;
        }

        HideEmptyChoices = hide;
        AppSettingsService.SaveHideEmptyChoices(hide);
        HideEmptyChoicesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>장면 하나를 접거나 편다 — 판 접기와 같은 알림으로 그래프 · 하이어아키가 다시 선다.</summary>
    internal void SetSceneCollapsed(string fileId, string sceneId, bool collapsed)
    {
        if (Project.FindFile(fileId) is null)
        {
            return;
        }

        var key = new GraphSceneKey(fileId, sceneId);
        bool changed = collapsed ? _collapsedScenes.Add(key) : _collapsedScenes.Remove(key);

        if (changed)
        {
            RaiseFileGraphStateChanged(
                activeFileChanged: false,
                expandedFilesChanged: true,
                fileListChanged: false);
        }
    }

    private void OnEditorChanged(object? sender, ProjectChangedEventArgs e)
    {
        bool activeChanged = false;
        bool expandedChanged = false;
        bool fileListChanged = false;

        HashSet<string> validFileIds = Project.Files
            .Select(file => file.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (Project.FindFile(ActiveFileId) is null)
        {
            ActiveFileId = Project.Files.FirstOrDefault()?.Id;
            activeChanged = true;
        }

        expandedChanged |= _expandedFileIds.RemoveWhere(fileId => !validFileIds.Contains(fileId)) > 0;
        expandedChanged |= _collapsedScenes.RemoveWhere(key => !validFileIds.Contains(key.FileId)) > 0;
        _knownFileIds.RemoveWhere(fileId => !validFileIds.Contains(fileId));

        // 새로 생긴 파일만 기본적으로 펼친다. 이미 존재하던 파일을 사용자가 접어 둔 상태는
        // 대사 수정이나 노드 이동 같은 프로젝트 변경 뒤에도 그대로 유지되어야 한다.
        foreach (string fileId in validFileIds)
        {
            if (_knownFileIds.Add(fileId))
            {
                expandedChanged |= _expandedFileIds.Add(fileId);
            }
        }

        string nextFileListSignature = BuildFileListSignature(Project);

        if (!string.Equals(_fileListSignature, nextFileListSignature, StringComparison.Ordinal))
        {
            _fileListSignature = nextFileListSignature;
            fileListChanged = true;
        }

        // 선택하고 있던 노드가 사라졌다면 선택을 놓는다.
        if (SelectedNodeId is not null && Project.FindNode(SelectedNodeId) is null)
        {
            SelectedNodeId = Project.EnumerateNodes().FirstOrDefault()?.Id;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        Changed?.Invoke(this, e);

        if (activeChanged || expandedChanged || fileListChanged)
        {
            RaiseFileGraphStateChanged(activeChanged, expandedChanged, fileListChanged);
        }
    }

    private void ResetFileGraphState(StoryProject project, string? activeFileId = null)
    {
        ActiveFileId = activeFileId ?? project.Files.FirstOrDefault()?.Id;

        _expandedFileIds.Clear();
        _knownFileIds.Clear();
        _collapsedScenes.Clear();
        _hiddenChoiceNodeIds.Clear();

        if (ProjectPath is not null)
        {
            _hiddenChoiceNodeIds.UnionWith(AppSettingsService.LoadHiddenChoices(ProjectPath));
        }

        foreach (StoryFile file in project.Files)
        {
            _expandedFileIds.Add(file.Id);
            _knownFileIds.Add(file.Id);
        }

        _fileListSignature = BuildFileListSignature(project);
    }

    private void RaiseFileGraphStateChanged(
        bool activeFileChanged,
        bool expandedFilesChanged,
        bool fileListChanged)
    {
        FileGraphStateChanged?.Invoke(
            this,
            new FileGraphStateChangedEventArgs(
                activeFileChanged,
                expandedFilesChanged,
                fileListChanged));
    }

    private static string BuildFileListSignature(StoryProject project)
    {
        return string.Join(
            "\n",
            project.Files.Select(file =>
                $"{file.Id}\u001f{file.Name}\u001f{file.RelativePath}\u001f{file.Nodes.Count}"));
    }

    private static StoryProject NewProjectInstance()
    {
        // 대본을 미리 만들지 않는다 —대사 노드가 생성될 때 전용 대본이
        // 함께 태어나므로, 미리 둔 대본은 아무도 안 읽는 빈 파일로 남을 뿐이다.
        var project = new StoryProject { Title = "새 프로젝트" };
        project.Files.Add(new StoryFile(name: "기본 파일"));
        return project;
    }
}

internal sealed class FileGraphStateChangedEventArgs : EventArgs
{
    public FileGraphStateChangedEventArgs(
        bool activeFileChanged,
        bool expandedFilesChanged,
        bool fileListChanged)
    {
        ActiveFileChanged = activeFileChanged;
        ExpandedFilesChanged = expandedFilesChanged;
        FileListChanged = fileListChanged;
    }

    public bool ActiveFileChanged { get; }

    public bool ExpandedFilesChanged { get; }

    /// <summary>파일 이름·경로·순서 또는 소유 노드 개수가 바뀌어 목록 행을 다시 만들어야 하는가.</summary>
    public bool FileListChanged { get; }
}
