using Vn.Authoring.Chapters;
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

    public string StatusMessage { get; private set; } = "새 프로젝트입니다. [대본] 탭의 [＋ 대사 노드]를 누르면 첫 챕터 · 장면과 함께 카드가 섭니다.";


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
    /// 프로젝트의 화자 목록이 바뀌었다 — 화자 명령 · 되돌리기 · 열기 모두. <see cref="Changed"/> 뒤에 알린다. [대본] 탭의 프로젝트
    /// 요약(화자 이름)과 [구성] 탭의 [화자] 탭(캐릭터 키)이 듣고 목록을 다시 세운다 — 모든 변경마다 다시 세우지 않으려고 따로 둔다.
    /// </summary>
    public event EventHandler? SpeakersChanged;

    /// <summary>마지막으로 알린 화자 목록의 지문 — <see cref="SpeakersChanged"/>를 바뀐 때만 낸다.</summary>
    private string _speakerSignature = string.Empty;

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
    /// 보낼 곳 — 프로젝트 폴더의 <c>runtime.target.json</c>(<see cref="ExportDestinations"/>). 형식마다 <c>exported/</c> 밖에 한 벌 더 낼
    /// 폴더이고, <b>저장할 때만</b> 거기로 보낸다(<see cref="TakeDeliveryRequest"/> — 2026-11-17 · ⛔ 그 전에는 게임 폴더 하나였고 바뀔 때마다 썼다).
    /// </summary>
    public ExportDestinations Destinations { get; private set; } = ExportDestinations.None;

    public void ReloadDestinations() =>
        Destinations = ProjectPath is { } path ? ExportDestinations.LoadBeside(path) : ExportDestinations.None;

    private bool _deliveryRequested;

    /// <summary>
    /// 저장이 보내기를 부탁했는가 — 묻는 순간 부탁을 내린다. [구성]의 다시 읽기가 <c>exported/</c>를 새로 쓴 <b>뒤에</b> 묻고 보낸다 — 저장
    /// 직후의 다시 읽기라 보낼 곳이 저장된 판과 같다(저장에서 곧바로 보내면 처음 저장한 프로젝트는 아직 <c>exported/</c>가 없다).
    /// </summary>
    public bool TakeDeliveryRequest()
    {
        bool requested = _deliveryRequested;
        _deliveryRequested = false;
        return requested;
    }

    /// <summary>상태줄의 한 마디 — 보낼 곳이 있으면 어느 형식인지. 없으면 말하지 않는다(설정은 [⚙ 설정]에 있다).</summary>
    private string DestinationsNote() => Destinations.IsEmpty
        ? string.Empty
        : " · 저장하면 보냅니다: " + string.Join(" · ", ExportDestinations.Formats
            .Where(format => Destinations.FolderOf(format) is not null)
            .Select(ExportDestinations.LabelOf));

    public void NewProject()
    {
        StoryProject project = NewProjectInstance();

        ProjectPath = null;
        Destinations = ExportDestinations.None;
        _deliveryRequested = false;
        _savedSnapshot = ProjectSnapshotCodec.Encode(project);
        StatusMessage = "새 프로젝트입니다. [대본] 탭의 [＋ 대사 노드]를 누르면 첫 챕터 · 장면과 함께 카드가 섭니다.";

        ResetFileGraphState(project);
        Editor.Replace(project);
        Select(project.EnumerateNodes().FirstOrDefault()?.Id);
        RaiseFileGraphStateChanged(
            activeFileChanged: true,
            expandedFilesChanged: true,
            fileListChanged: true);
    }

    public void Open(string path)
    {
        ProjectLoadResult loaded = ProjectStore.Load(path);
        StoryProject project = loaded.Project;

        ProjectPath = loaded.ManifestPath;
        ReloadDestinations();
        _deliveryRequested = false;
        _savedSnapshot = ProjectSnapshotCodec.Encode(project);
        StatusMessage =
            $"{Path.GetFileName(Path.GetDirectoryName(ProjectPath))}/ · 노드 {project.EnumerateNodes().Count()}개" +
            DestinationsNote();

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

    public void Save(string? path = null)
    {
        string target = path ?? ProjectPath
            ?? throw new InvalidOperationException("저장할 경로가 없습니다.");

        ProjectStore.Save(target, Project);

        ProjectPath = Path.GetFullPath(target);
        _savedSnapshot = ProjectSnapshotCodec.Encode(Project);
        ReloadDestinations();
        _deliveryRequested = !Destinations.IsEmpty;
        AppSettingsService.SaveRecentProject(ProjectPath);

        // 저장은 프로젝트에 쓰는 일 하나다. "다른 이름으로 저장"은 없다 — 프로젝트는 폴더이고,
        // 복제는 탐색기 · 버전은 git이 한다(ProjectFolder).
        StatusMessage = $"{Path.GetFileName(Path.GetDirectoryName(ProjectPath))}/ 에 저장했습니다." +
            (Destinations.IsEmpty ? string.Empty : " 보낼 곳에도 보냅니다 — 결과는 [구성] 탭의 검증 보고에 섭니다.");
        Changed?.Invoke(this, new ProjectChangedEventArgs(ProjectChangeKind.Content));
    }

    public void SetStatus(string message)
    {
        StatusMessage = message;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    // 화자의 집은 프로젝트다(<see cref="StoryProject.Speakers"/> · 2026-11-17 definition-split S3 — ⛔ 그 전에는 game.definition.json의 speakers).
    // 화자 <b>이름</b>은 [대본] 탭(작가)이 더하고 고치고 지우며, <b>캐릭터 키</b>는 [구성] 탭(기획자)이 잇는다(writer-fields W3). 규칙(빈 이름 ·
    // 같은 이름)은 아래 화자 창구 한 벌이고, 바꾸는 것은 편집기의 화자 명령이다 — 조작 하나 = 되돌리기 한 칸.

    /// <summary>화자 이름을 더한다 — [대본] 탭. 못 더했으면 그 이유(빈 이름 · 같은 이름).</summary>
    public string? AddSpeakerName(string? name)
    {
        string typed = (name ?? string.Empty).Trim();

        if (typed.Length == 0)
        {
            return "화자 이름을 적어 주세요.";
        }

        if (Project.FindSpeaker(typed) is not null)
        {
            return $"화자 '{typed}'은 이미 있습니다.";
        }

        Editor.AddSpeaker(typed);
        return null;
    }

    /// <summary>
    /// 화자 이름을 바꾼다 — [대본] 탭. 이미 쓰인 대사 줄의 화자가 같은 칸에서 따라간다(<see cref="ProjectEditor.RenameSpeaker"/>), 캐릭터 키는 그대로다.
    /// 비울 수 없고(지우기는 따로), 이미 있는 이름으로는 바꾸지 않는다(두 화자의 줄이 한 이름으로 합쳐진다).
    /// </summary>
    /// <returns>상태줄에 세울 말 — 무엇이 따라갔는지, 또는 못 바꾼 이유. 같은 이름이면 null.</returns>
    public string? RenameSpeakerName(string oldName, string? newName)
    {
        string typed = (newName ?? string.Empty).Trim();

        if (string.Equals(oldName, typed, StringComparison.Ordinal))
        {
            return null;
        }

        if (typed.Length == 0)
        {
            return "이름은 비울 수 없습니다 — 지우려면 ✕를 누르세요.";
        }

        if (Project.FindSpeaker(oldName) is null)
        {
            return $"화자 '{oldName}'이 없습니다 — 목록이 바뀌었습니다.";
        }

        if (Project.FindSpeaker(typed) is not null)
        {
            return $"화자 '{typed}'은 이미 있습니다.";
        }

        int lines = Editor.RenameSpeaker(oldName, typed);

        return lines == 0
            ? $"화자 '{oldName}' → '{typed}'."
            : $"화자 '{oldName}' → '{typed}' — 줄 {lines}개가 따라갔습니다.";
    }

    /// <summary>화자를 목록에서 지운다 — [대본] 탭. 이미 대본에 적힌 이름은 그대로 남는다(화자 칸은 자유 입력).</summary>
    public bool RemoveSpeakerName(string name) => Editor.RemoveSpeaker(name);

    /// <summary>화자의 캐릭터 키 — [구성] 탭. 초상화 · 표정이 이 키로 붙는다. 같은 값이면 기록하지 않는다.</summary>
    public bool SetSpeakerCharacterId(string name, string? characterId) => Editor.SetSpeakerCharacterId(name, characterId);

    /// <summary>
    /// 그 챕터의 판을 보장한다. <b>규칙은 <see cref="ProjectEditor.EnsureChapterBoard"/>가
    /// 갖는다</b> — 세션 상태를 하나도 안 보는 순수 편집 작업이다.
    /// 여기 있는 것은 부르는 자리 스물여덟을 안 건드리기 위한 이름 하나뿐이다.
    /// </summary>
    internal string EnsureChapterBoard(string chapterId) => Editor.EnsureChapterBoard(chapterId);

    /// <summary>
    /// <b>챕터 하나를 세운다</b> — 이름을 받고, 판을 만들어 고른다. <b>스탯 없이 시작한다</b> — 스탯은 챕터마다 [구성] 탭에서 더한다
    /// (2026-11-17 소유자 · <c>docs/work-orders/definition-split-orders.md</c> S1 — ⛔ 그 전에는 정의 파일의 <c>variables</c>로 깔았다.
    /// 그 목록은 더하기만 해서 지운 스탯이 새 챕터마다 되살아났다).
    ///
    /// <b>챕터를 세우는 규칙은 여기 한 벌이다.</b> 두 벌이면 어느 창구로 만들었느냐에 따라 챕터가 달라진다.
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

        // 챕터와 그 판이 한 되돌리기 칸이다(⛔ 2026-11-18까지는 둘로 쌓여 Ctrl+Z 한 번에 판만 물러났다).
        string board = string.Empty;
        Editor.Batch(() =>
        {
            Editor.EnsureChapter(chapterId);
            board = EnsureChapterBoard(chapterId);
        });
        SelectFile(board);

        return null;
    }

    /// <summary>
    /// 작가가 이름으로 새 챕터를 만든다 — ID는 자동(<see cref="NewNames.NextChapterId"/>, <c>ch01</c>…), 적은 이름은 표시 이름이다
    /// (비우면 「새 챕터」). [대본] 탭 하이어아키의 「챕터 추가」와 [＋ 대사 노드](챕터가 없을 때)가 부른다(display-names N2). 세우는 규칙은 <see cref="CreateChapter"/> 그대로다.
    /// </summary>
    /// <returns>만든 챕터의 ID, 못 만든 이유 — 둘 중 하나만 있다.</returns>
    internal (string? ChapterId, string? Failure) CreateNamedChapter(string? name)
    {
        string chapterId = NewNames.NextChapterId(Project);
        string? failure = null;

        // 챕터 · 판 · 이름이 한 되돌리기 칸이다. 이름도 편집 명령으로 적는다(⛔ 2026-11-18까지는 모델에 바로 적었다).
        Editor.Batch(() =>
        {
            failure = CreateChapter(chapterId);

            if (failure is null)
            {
                Editor.UpdateChapterDisplayName(chapterId, string.IsNullOrWhiteSpace(name) ? NewNames.Chapter : name);
            }
        });

        return failure is null ? (chapterId, null) : (null, failure);
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

        string speakers = string.Join('\n', Project.Speakers.Select(speaker => speaker.Name + '\t' + speaker.CharacterId));
        bool speakersChanged = !string.Equals(_speakerSignature, speakers, StringComparison.Ordinal);
        _speakerSignature = speakers;

        Changed?.Invoke(this, e);

        if (speakersChanged)
        {
            SpeakersChanged?.Invoke(this, EventArgs.Empty);
        }

        if (activeChanged || expandedChanged || fileListChanged)
        {
            RaiseFileGraphStateChanged(activeChanged, expandedChanged, fileListChanged);
        }
    }

    /// <summary>
    /// 프로젝트를 열거나 새로 만들 때마다 하나씩 는다 — 화면이 「새 프로젝트의 첫 화면」을 알아보는 열쇠다(되돌리기는 프로젝트 객체를
    /// 갈아 끼우므로 객체로는 못 가린다).
    /// </summary>
    internal int ProjectGeneration { get; private set; }

    private void ResetFileGraphState(StoryProject project, string? activeFileId = null)
    {
        ProjectGeneration++;
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
        // 판을 미리 두지 않는다 — 판은 챕터가 세운다(챕터 = 판 1:1). 첫 [＋ 대사 노드]가 「새 챕터」 · 「새 장면」과 함께 첫 카드를
        // 세운다(2026-11-17 소유자). ⛔ 그 전에는 챕터가 아닌 판 「기본 파일」(낙서판)으로 시작했다.
        return new StoryProject { Title = "새 프로젝트" };
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
