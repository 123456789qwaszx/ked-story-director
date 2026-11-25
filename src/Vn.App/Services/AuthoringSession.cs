using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;
using Vn.Authoring.Text;
using Vn.App.Strings;
using Vn.Authoring.Strings;

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

    /// <summary>열거나 저장한 때의 프로젝트 파일 지문 — 그 뒤 다른 곳이 파일을 바꿨는지 견준다(<see cref="ChangedOutside"/>). 저장 전이면 null.</summary>
    private ProjectDiskStamp? _diskStamp;

    public AuthoringSession()
    {
        Editor = new ProjectEditor(NewProjectInstance())
        {
            // 새 카드 글 칸의 처음 글꼴 · 크기는 그때의 설정 「대본 글 칸」(writer-formatting WF-D13).
            NewCardWriterBase = () => Views.ScriptFont.CardBase,
            NewCardSize = () => (Views.CardDefaults.Width, Views.CardDefaults.WritingHeight),

            // 새로 잇는 선의 꼴은 그때의 설정 「기타」의 「새로 잇는 선」(2026-11-23 소유자).
            NewLineShape = () => Views.BoardLines.Kind
        };
        WriterBaseFill.Fill(Editor.Project, Views.ScriptFont.CardBase);
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
    /// 펼친 판 안에서 접어 둔 장면들 (하이어아키의 눈, hierarchy). 그 장면만 줄 목록
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

    /// <summary>
    /// [구성] 탭에서 ID로 보는가 — 그 탭 툴바의 「ID로 보기」(하나다 · 처음에는 켜져 있다 · 2026-11-18 소유자). 켜면 하이어아키 · 카드가 ID(챕터ID ·
    /// 장면ID · EpisodeId)로, 끄면 [대본] 탭처럼 보이는 이름으로 선다. 화면 편의라 사용자 설정에 기억한다(앱 전체).
    /// </summary>
    public bool ShowIds { get; private set; } = AppSettingsService.LoadShowIds();

    /// <summary>[구성] 탭의 「ID로 보기」가 바뀌었다 — [구성] 판 · 하이어아키가 다시 선다.</summary>
    public event EventHandler? ShowIdsChanged;

    /// <summary>
    /// [대본] 판의 미니맵(우하단 지도)을 보이는가 — 설정 창 「기타」(처음에는 보인다 · 2026-11-18 소유자 · side-fold K3). 끄면 지도와 배율 단추를 걷고
    /// 지금 배율 글자만 남긴다(2026-11-25 — ⛔ 그 전에는 배율 줄이 바탕째 남았다). 화면 편의라 사용자 설정에 기억한다(앱 전체).
    /// </summary>
    public bool ShowMinimap { get; private set; } = AppSettingsService.LoadShowMinimap();

    /// <summary>미니맵을 켜거나 껐다 — [대본] 판이 지도를 세우거나 걷는다.</summary>
    public event EventHandler? ShowMinimapChanged;

    public string? SelectedNodeId { get; private set; }

    public StoryNode? SelectedNode => Project.FindNode(SelectedNodeId);

    public string StatusMessage { get; private set; } = NoProjectStatus;

    /// <summary>저장된 프로젝트가 없을 때(처음 켰을 때)의 상태줄 — 판 한가운데의 두 단추를 가리킨다(usability UX35).</summary>
    internal static string NoProjectStatus => Ui.Session_NoProjectYet;


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
    /// 프로젝트의 화자 목록이 바뀌었다 — 화자 명령 · 되돌리기 · 열기 모두. 대본에만 있는 이름(<see cref="UnlistedSpeakers"/> — 이름 · 줄 수)이
    /// 바뀌어도 알린다(usability UX34). <see cref="Changed"/> 뒤에 알린다. [대본] 탭의 「화자」 탭(화자 이름)과 [구성] 탭의 「화자」 탭(캐릭터 키)이
    /// 듣고 목록을 다시 세운다 — 모든 변경마다 다시 세우지 않으려고 따로 둔다.
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

    /// <summary>상태줄의 한 마디 — 복제할 폴더가 있으면 어느 형식인지. 없으면 말하지 않는다(설정은 [⚙ 설정]에 있다).</summary>
    private string DestinationsNote() => Destinations.IsEmpty
        ? string.Empty
        : Ui.Session_CopiedOnSaveTo + string.Join(" · ", ExportDestinations.Formats
            .Where(format => Destinations.FolderOf(format) is not null)
            .Select(ExportDestinations.LabelOf));

    public void NewProject()
    {
        StoryProject project = NewProjectInstance();
        WriterBaseFill.Fill(project, Views.ScriptFont.CardBase);

        ProjectPath = null;
        _diskStamp = null;
        Destinations = ExportDestinations.None;
        _deliveryRequested = false;
        _savedSnapshot = ProjectSnapshotCodec.Encode(project);
        StatusMessage = NoProjectStatus;

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

        // 처음 글꼴 · 크기가 안 적힌 카드(그 값이 생기기 전에 만든 카드)는 지금 이 PC의 설정으로 굳힌다 — 저장 기준(아래 스냅샷)을 뜨기 전에 적어
        // 「저장 안 한 변경」이 되지 않는다. 다음 저장에 파일로 간다(writer-formatting WF-D13).
        WriterBaseFill.Fill(project, Views.ScriptFont.CardBase);

        ProjectPath = loaded.ManifestPath;
        _diskStamp = ProjectDiskStamp.Read(loaded.ManifestPath);
        ReloadDestinations();
        _deliveryRequested = false;
        _savedSnapshot = ProjectSnapshotCodec.Encode(project);
        StatusMessage =
            Plural.Format(Ui.Session_Nodes, Path.GetFileName(Path.GetDirectoryName(ProjectPath)), project.EnumerateNodes().Count()) +
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

    /// <summary>
    /// 프로젝트에 쓴다. <b>연 뒤에 다른 곳이 프로젝트 파일을 바꿨으면 쓰지 않는다</b>(<see cref="ProjectChangedOutsideException"/> ·
    /// writer-release WR1) — Git으로 받은 것 · 다른 PC의 동기화를 말없이 덮지 않는다. 덮어쓰기로 정했으면
    /// <paramref name="overwriteOutsideChanges"/>로 부른다. 다른 폴더에 처음 쓰는 저장(새 프로젝트)은 견주지 않는다.
    /// </summary>
    public void Save(string? path = null, bool overwriteOutsideChanges = false)
    {
        string target = path ?? ProjectPath
            ?? throw new InvalidOperationException(Ui.Session_ThereSNoPathTo);

        bool samePlace = ProjectPath is not null &&
                         string.Equals(Path.GetFullPath(target), ProjectPath, StringComparison.OrdinalIgnoreCase);

        if (samePlace && !overwriteOutsideChanges && ChangedOutside)
        {
            throw new ProjectChangedOutsideException();
        }

        ProjectStore.Save(target, Project);

        ProjectPath = Path.GetFullPath(target);
        _diskStamp = ProjectDiskStamp.Read(ProjectPath);
        _savedSnapshot = ProjectSnapshotCodec.Encode(Project);
        ReloadDestinations();
        _deliveryRequested = !Destinations.IsEmpty;
        AppSettingsService.SaveRecentProject(ProjectPath);

        // 저장은 프로젝트에 쓰는 일 하나다. "다른 이름으로 저장"은 없다 — 프로젝트는 폴더이고,
        // 복제는 탐색기 · 버전은 git이 한다(ProjectFolder).
        StatusMessage = string.Format(Ui.Session_SavedTo, Path.GetFileName(Path.GetDirectoryName(ProjectPath))) +
            (Destinations.IsEmpty ? string.Empty : Ui.Session_AlsoCopyingToChosenFolders);
        Changed?.Invoke(this, new ProjectChangedEventArgs(ProjectChangeKind.Content));
    }

    /// <summary>
    /// 연 뒤(또는 마지막 저장 뒤)에 <b>다른 곳이 프로젝트 파일을 바꿨는가</b> — 목차 · 판 파일의 내용으로 견준다(<see cref="ProjectDiskStamp"/>).
    /// 저장한 적 없는 새 프로젝트는 늘 거짓이다.
    /// </summary>
    public bool ChangedOutside => ProjectPath is { } path && _diskStamp is { } stamp && !ProjectDiskStamp.Read(path).SameAs(stamp);

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
            return Ui.Session_TypeSpeakerName;
        }

        if (Project.FindSpeaker(typed) is not null)
        {
            return string.Format(Ui.Session_SpeakerAlreadyExists, typed);
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
            return Ui.Session_NameCanTBeEmpty;
        }

        if (Project.FindSpeaker(oldName) is null)
        {
            return string.Format(Ui.Session_SpeakerDoesnTExistList, oldName);
        }

        if (Project.FindSpeaker(typed) is not null)
        {
            return string.Format(Ui.Session_SpeakerAlreadyExists, typed);
        }

        int lines = Editor.RenameSpeaker(oldName, typed);

        return lines == 0
            ? string.Format(Ui.Session_Speaker, oldName, typed)
            : Plural.Format(Ui.Session_SpeakerLinesFollowed, oldName, typed, lines);
    }

    /// <summary>화자를 목록에서 지운다 — [대본] 탭. 이미 대본에 적힌 이름은 그대로 남는다(화자 칸은 자유 입력).</summary>
    public bool RemoveSpeakerName(string name) => Editor.RemoveSpeaker(name);

    /// <summary>
    /// 대본에만 있는 이름(<see cref="UnlistedSpeakers"/>)들을 화자 목록에 올린다 — [대본] 「화자」 탭의 [등록] · [모두 등록](usability UX34).
    /// 이름마다 임시 키가 서고, 여럿이어도 되돌리기 한 칸이다.
    /// </summary>
    /// <returns>상태줄에 세울 말. 올린 것이 없으면 null.</returns>
    public string? RegisterSpeakerNames(IEnumerable<string> names)
    {
        IReadOnlyList<Speaker> added = Editor.AddSpeakers(names);

        return added.Count switch
        {
            0 => null,
            1 => string.Format(Ui.Session_AddedToSpeakerListIts, added[0].Name, Josa.을를(added[0].Name), added[0].CharacterId),
            _ => Plural.Format(Ui.Session_AddedSpeakersToListTheir, added.Count)
        };
    }

    /// <summary>
    /// 대본에만 있는 이름을 고친다 — 그 이름이 화자인 줄이 모든 챕터에서 따라간다(<see cref="ProjectEditor.RenameSpeaker"/> · 되돌리기 한 칸).
    /// 목록에 있는 이름으로 고치면 그 화자로 합쳐진다(오타 고치기 · usability UX34). 띄어쓰기가 든 새 이름은 목록에 있어야 한다 — 없으면 대본이
    /// 그 줄을 화자로 읽지 않는다.
    /// </summary>
    /// <returns>상태줄에 세울 말 — 무엇이 따라갔는지, 또는 못 고친 이유. 같은 이름이면 null.</returns>
    public string? FixUnlistedSpeakerName(string oldName, string? newName)
    {
        string typed = (newName ?? string.Empty).Trim();

        if (string.Equals(oldName, typed, StringComparison.Ordinal))
        {
            return null;
        }

        if (typed.Length == 0)
        {
            return Ui.Session_NameCanTBeEmpty2;
        }

        if (Project.FindSpeaker(oldName) is not null)
        {
            return string.Format(Ui.Session_IsAlreadyInListList, oldName, Josa.은는(oldName));
        }

        bool listed = Project.FindSpeaker(typed) is not null;

        if (!listed && typed.Any(char.IsWhiteSpace))
        {
            return string.Format(Ui.Session_AddNameWithSpaceAs, typed, Josa.은는(typed));
        }

        int lines = Editor.RenameSpeaker(oldName, typed);

        if (lines == 0)
        {
            return string.Format(Ui.Session_NoLineHasAsIts, oldName, Josa.이가(oldName));
        }

        return listed
            ? Plural.Format(Ui.Session_MergedLinesOfIntoSpeaker, oldName, lines, typed, Josa.으로(typed))
            : Plural.Format(Ui.Session_ChangedLinesThisNameIsn, oldName, typed, lines);
    }

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
    /// (2026-11-17 소유자 · definition-split S1 — ⛔ 그 전에는 정의 파일의 <c>variables</c>로 깔았다.
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
            return Ui.Session_TypeChapterID;
        }

        if (ProjectPath is null)
        {
            return Ui.Session_SaveProjectFirstPlaceWhere;
        }

        if (Editor.FindChapter(chapterId) is not null)
        {
            return string.Format(Ui.Session_ChapterAlreadyExists, chapterId);
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
    /// (비우면 「새 챕터」). [대본] 탭 하이어아키의 「챕터 추가」와 [＋ 에피소드](챕터가 없을 때)가 부른다(display-names N2). 세우는 규칙은 <see cref="CreateChapter"/> 그대로다.
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

        // 그 카드의 칸만 바뀌었다 — [대본] 판이 그 카드의 철도만 다시 긋는다(2026-11-18 · board-speed PF2). ⛔ 그 전에는 「펼친 판이 바뀌었다」로
        // 알려 셸이 하이어아키 둘을 다시 세우고 판을 통째로 다시 지었다(에피소드 300개에서 3.5초).
        HiddenChoicesChanged?.Invoke(nodeId);
    }

    /// <summary>
    /// 여러 카드의 「빈 선택지 숨기기」를 한 번에(2026-11-23 소유자 — 「선택 된것들 모두 일괄적으로 키고 꺼지도록」) — 한 번 기억하고, 바뀐 카드마다 알린다.
    /// </summary>
    internal void SetHideEmptyChoices(IReadOnlyCollection<string> nodeIds, bool hide)
    {
        List<string> changed = nodeIds.Distinct(StringComparer.Ordinal)
            .Where(nodeId => hide ? _hiddenChoiceNodeIds.Add(nodeId) : _hiddenChoiceNodeIds.Remove(nodeId))
            .ToList();

        if (changed.Count == 0)
        {
            return;
        }

        if (ProjectPath is not null)
        {
            AppSettingsService.SaveHiddenChoices(ProjectPath, _hiddenChoiceNodeIds);
        }

        foreach (string nodeId in changed)
        {
            HiddenChoicesChanged?.Invoke(nodeId);
        }
    }

    /// <summary>[대본] 카드 하나의 「빈 선택지 숨기기」가 바뀌었다 — 그 카드의 노드 Id.</summary>
    internal event Action<string>? HiddenChoicesChanged;

    private readonly Dictionary<string, IReadOnlySet<string>> _pendingLineRemovals = new(StringComparer.Ordinal);

    /// <summary>
    /// 그 카드의 <b>지울 줄</b> — 카드 글 칸에서 지웠지만 칸을 나올 때(또는 Ctrl+S) 지워질 줄의 LineId(2026-11-26 소유자). 쓰는 동안 고친 글 · 새 줄은
    /// 바로 들어가고 지운 줄만 대본에 남아 있다 — 인스펙터가 이 줄을 취소선으로 보인다. 화면의 일이라 저장하지 않는다.
    /// </summary>
    internal IReadOnlySet<string> PendingLineRemovalsOf(string? nodeId) =>
        nodeId is not null && _pendingLineRemovals.TryGetValue(nodeId, out IReadOnlySet<string>? lineIds) ? lineIds : EmptyLineIds;

    private static readonly IReadOnlySet<string> EmptyLineIds = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>그 카드의 지울 줄을 정한다(비우면 걷는다) — 바뀌었으면 알린다.</summary>
    internal void SetPendingLineRemovals(string nodeId, IReadOnlyCollection<string> lineIds)
    {
        IReadOnlySet<string> before = PendingLineRemovalsOf(nodeId);

        if (before.Count == lineIds.Count && lineIds.All(before.Contains))
        {
            return;
        }

        if (lineIds.Count == 0)
        {
            _pendingLineRemovals.Remove(nodeId);
        }
        else
        {
            _pendingLineRemovals[nodeId] = new HashSet<string>(lineIds, StringComparer.Ordinal);
        }

        PendingLineRemovalsChanged?.Invoke(nodeId);
    }

    /// <summary>그 카드의 지울 줄이 바뀌었다 — 그 카드의 노드 Id.</summary>
    internal event Action<string>? PendingLineRemovalsChanged;

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

    /// <summary>[구성] 탭을 ID로(참) 또는 이름으로 본다 — 기억하고 [구성] 탭에 알린다.</summary>
    internal void SetShowIds(bool show)
    {
        if (ShowIds == show)
        {
            return;
        }

        ShowIds = show;
        AppSettingsService.SaveShowIds(show);
        ShowIdsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>[대본] 판의 미니맵을 보이거나(참) 걷는다 — 기억하고 [대본] 판에 알린다.</summary>
    internal void SetShowMinimap(bool show)
    {
        if (ShowMinimap == show)
        {
            return;
        }

        ShowMinimap = show;
        AppSettingsService.SaveShowMinimap(show);
        ShowMinimapChanged?.Invoke(this, EventArgs.Empty);
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

        // 대본에만 있는 이름(과 줄 수)도 싣는다 — [대본] 「화자」 탭이 그것도 모아 보인다(usability UX34).
        string speakers = string.Join('\n', Project.Speakers.Select(speaker => speaker.Name + '\t' + speaker.CharacterId + '\t' + speaker.Color?.Hex + '\t' + speaker.Fill?.Hex)) + "\n\n" +
                          string.Join('\n', UnlistedSpeakers.Of(Project).Select(entry => entry.Name + '\t' + entry.Lines));
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
        _pendingLineRemovals.Clear();

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
        // 판을 미리 두지 않는다 — 판은 챕터가 세운다(챕터 = 판 1:1). 첫 [＋ 에피소드]가 「새 챕터」 · 「새 장면」과 함께 첫 카드를
        // 세운다(2026-11-17 소유자). ⛔ 그 전에는 챕터가 아닌 판 「기본 파일」(낙서판)으로 시작했다.
        return new StoryProject { Title = Ui.Main_NewProject };
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

/// <summary>
/// 연 뒤에 다른 곳(Git으로 받기 · 동기화 · 손으로 고침)이 프로젝트 파일을 바꿔 저장하지 않았다(writer-release WR1). 창이 「다시 열기」 ·
/// 「내 것으로 덮어쓰기」를 묻는다.
/// </summary>
public sealed class ProjectChangedOutsideException : InvalidOperationException
{
    public ProjectChangedOutsideException()
        : base(Ui.Session_ProjectFileWasChangedElsewhere)
    {
    }
}
