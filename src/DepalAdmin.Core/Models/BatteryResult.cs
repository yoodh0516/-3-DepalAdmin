namespace DepalAdmin.Core.Models;

/// <summary>
/// server/results — 건전지 1개 최종 결과. 메시지_형식 v1.2 3장.
/// 등급 기준값은 임시값이므로 화면에서 기준을 직접 계산하지 말고 Grade를 그대로 쓴다.
/// </summary>
public sealed class BatteryResult
{
    public string RunId { get; set; } = "";
    public int BatteryId { get; set; }

    /// <summary>[행, 열]. 행 0~1, 열 0~4</summary>
    public int[]? PalletCell { get; set; }

    public string? PickedAt { get; set; }
    public VisionInfo? Vision { get; set; }

    /// <summary>measured / skipped / failed</summary>
    public string? ElectricalStatus { get; set; }

    /// <summary>normal / reversed / abnormal / null</summary>
    public string? Polarity { get; set; }

    public double? VOpen { get; set; }
    public double? VLoad { get; set; }
    public double? RInt { get; set; }

    /// <summary>A / B / NG / RESORT</summary>
    public string Grade { get; set; } = "";

    public string? Reason { get; set; }

    /// <summary>AGV / B_BOX / NG_BOX / RESORT_BOX / null(배출 실패)</summary>
    public string? Dest { get; set; }

    public string? FinishedAt { get; set; }
    public string? RuleVersion { get; set; }
    public double? RLoad { get; set; }
}

public sealed class VisionInfo
{
    /// <summary>ok / failed</summary>
    public string? Status { get; set; }

    /// <summary>normal / scratch / sticker / ...</summary>
    public string? Result { get; set; }

    public double? Conf { get; set; }

    /// <summary>minus_up / plus_up / unknown</summary>
    public string? Polarity { get; set; }
}
