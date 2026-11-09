namespace DepalAdmin.Core.Handover;

/// <summary>
/// 인수인계 메모 1건 (교대할 때 다음 근무자에게 남기는 글).
/// 쓸 때 그 순간의 라인 상태·미확인 알람 수를 자동으로 붙인다.
/// 보고서(조시현)도 이 모양 그대로 읽으므로, 칸을 바꿀 때는 서로 알려 준다.
/// </summary>
public sealed class HandoverNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime WrittenAt { get; set; } = DateTime.Now;
    public string AuthorId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Text { get; set; } = "";

    /// <summary>쓴 순간의 라인 상태 (idle / running / paused / estop / manual)</summary>
    public string LineMode { get; set; } = "";

    public string? RunId { get; set; }

    /// <summary>쓴 순간의 확인 안 된 알람 수</summary>
    public int UnackedAlarms { get; set; }

    /// <summary>읽음을 누른 계정 아이디</summary>
    public List<string> ReadBy { get; set; } = new();

    public bool IsReadBy(string userId) => ReadBy.Contains(userId, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 인수인계 메모 저장소. 구현: 유동호(Feature.Handover)의 JsonHandoverStore.
/// TODO(유동호 + DB 담당): 여러 PC에서 같이 보려면 MySQL 구현으로 바꾼다 (이 인터페이스는 그대로).
/// </summary>
public interface IHandoverStore
{
    /// <summary>from 이상 to 미만에 쓴 메모, 최신이 먼저</summary>
    Task<IReadOnlyList<HandoverNote>> GetAsync(DateTime from, DateTime to, CancellationToken ct = default);

    Task AddAsync(HandoverNote note, CancellationToken ct = default);

    Task MarkReadAsync(string noteId, string userId, CancellationToken ct = default);

    /// <summary>메모가 추가되거나 읽음이 바뀌었을 때</summary>
    event EventHandler? Changed;
}
