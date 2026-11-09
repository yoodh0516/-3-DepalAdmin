using DepalAdmin.Core.Data;
using DepalAdmin.Core.Settings;
using MySqlConnector;

namespace DepalAdmin.Feature.History;

/// <summary>
/// MySQL(aa_battery_inspection)에서 지난 결과를 읽는다. 읽기 전용 계정 사용.
/// 테이블·칸 이름은 DB 담당의 teamproject.sql(2026-11-06, 테이블 10개) 기준으로 맞춤.
///   runs(run_id ↔ source_run_id) → battery_cycles(run_id, cycle_id) → electrical_tests(cycle_id, 건전지당 0~1행)
///   전압 측정을 건너뛰거나 실패한 건전지는 electrical_tests 행이 없어서 전압 칸이 NULL로 나온다.
///
/// TODO(장성찬 + DB 담당): 화면용 읽기 전용 계정(SELECT 권한만) 만들기, 접속 문자열은 settings.local.json에
/// </summary>
public sealed class MySqlHistoryRepository : IHistoryRepository
{
    private const int MaxRows = 5000;

    private const string ResultsSql = @"
SELECT r.source_run_id, c.source_battery_id, c.pallet_slot,
       c.started_at, c.finished_at, c.final_grade, c.result_reason, c.sorted_destination,
       e.no_load_voltage_v, e.estimated_internal_resistance_ohm
FROM battery_cycles c
JOIN runs r ON r.run_id = c.run_id
LEFT JOIN electrical_tests e ON e.cycle_id = c.cycle_id
WHERE c.finished_at >= @from AND c.finished_at < @to
  AND c.final_grade IS NOT NULL
  AND (@grade IS NULL OR c.final_grade = @grade)
ORDER BY c.finished_at DESC
LIMIT @limit;";

    // 메시지_형식 v1.2 4장: server/events → equipment_events (level → severity, ts → occurred_at)
    private const string EventCountsSql = @"
SELECT severity, COUNT(*)
FROM equipment_events
WHERE occurred_at >= @from AND occurred_at < @to
GROUP BY severity;";

    private readonly string _connectionString;

    public MySqlHistoryRepository(AppSettings settings) => _connectionString = settings.MySqlConnection;

    public async Task<IReadOnlyList<HistoryRow>> GetResultsAsync(DateTime from, DateTime to, string? grade, CancellationToken ct = default)
    {
        var rows = new List<HistoryRow>();

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new MySqlCommand(ResultsSql, conn);
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);
        cmd.Parameters.AddWithValue("@grade", (object?)grade ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@limit", MaxRows);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            rows.Add(new HistoryRow
            {
                RunId = rd.IsDBNull(0) ? "" : rd.GetString(0),
                BatteryId = Convert.ToInt32(rd.GetValue(1)),
                PalletSlot = rd.IsDBNull(2) ? null : Convert.ToInt32(rd.GetValue(2)),
                StartedAt = rd.IsDBNull(3) ? null : rd.GetDateTime(3),
                FinishedAt = rd.IsDBNull(4) ? null : rd.GetDateTime(4),
                Grade = rd.IsDBNull(5) ? "" : rd.GetString(5),
                Reason = rd.IsDBNull(6) ? null : rd.GetString(6),
                Dest = rd.IsDBNull(7) ? null : rd.GetString(7),
                VOpen = rd.IsDBNull(8) ? null : Convert.ToDouble(rd.GetValue(8)),
                RInt = rd.IsDBNull(9) ? null : Convert.ToDouble(rd.GetValue(9)),
            });
        }
        return rows;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetEventCountsAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var counts = new Dictionary<string, int>();

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new MySqlCommand(EventCountsSql, conn);
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            counts[rd.IsDBNull(0) ? "-" : rd.GetString(0)] = Convert.ToInt32(rd.GetValue(1));
        return counts;
    }
}
