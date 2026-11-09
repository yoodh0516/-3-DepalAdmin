using DepalAdmin.Core.Handover;

namespace DepalAdmin.Feature.Handover;

/// <summary>
/// 담당: 유동호 — 인수인계 저장소 껍데기 (보고서 모듈 DI 호환 유지용).
/// TODO(유동호 + DB 담당): 파일(data/handover.json) 또는 MySQL 저장소 실제 기능 구현 및 연결.
/// </summary>
public sealed class JsonHandoverStore : IHandoverStore
{
    public event EventHandler? Changed;

    public Task<IReadOnlyList<HandoverNote>> GetAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        IReadOnlyList<HandoverNote> empty = Array.Empty<HandoverNote>();
        return Task.FromResult(empty);
    }

    public Task AddAsync(HandoverNote note, CancellationToken ct = default)
    {
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task MarkReadAsync(string noteId, string userId, CancellationToken ct = default)
    {
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
