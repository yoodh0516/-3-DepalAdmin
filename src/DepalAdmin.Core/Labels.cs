namespace DepalAdmin.Core;

/// <summary>
/// 코드 값 → 화면용 한글. 모든 페이지가 같은 말을 쓰도록 여기 모아 둔다.
/// 새 값이 생기면 공통 틀 담당(조시현)에게 알려 주세요.
/// </summary>
public static class Labels
{
    public static string Mode(string? mode) => mode switch
    {
        "idle" => "대기",
        "running" => "가동 중",
        "paused" => "일시정지",
        "estop" => "비상정지",
        "manual" => "수동",
        null => "-",
        _ => mode,
    };

    public static string Step(string? step) => step switch
    {
        "scan" => "팔레트 인식",
        "pick" => "집기",
        "inspect" => "외관 검사",
        "measure" => "전압 측정",
        "home" => "원점 복귀",
        "sort" => "분류",
        "wait_agv" => "AGV 대기",
        "wait_operator" => "작업자 대기",
        null => "-",
        _ => step,
    };

    public static string Grade(string? grade) => grade switch
    {
        "A" => "A",
        "B" => "B",
        "NG" => "불량",
        "RESORT" => "재분류",
        null or "" => "-",
        _ => grade,
    };

    public static string Dest(string? dest) => dest switch
    {
        "AGV" => "AGV 적재",
        "B_BOX" => "B 분류함",
        "NG_BOX" => "불량함",
        "RESORT_BOX" => "재분류함",
        null => "배출 안 됨",
        _ => dest,
    };

    public static string AgvState(string? state) => state switch
    {
        "home" => "분류 위치",
        "going" => "이동 중",
        "away" => "목적지",
        "returning" => "복귀 중",
        null => "-",
        _ => state,
    };

    public static string Device(string? name) => name switch
    {
        "server" => "중앙 서버",
        "controller" => "라인 컨트롤러",
        "pi" => "라즈베리파이",
        "yolo" => "YOLO 검사",
        "agv" => "AGV",
        "db_saver" => "DB 저장",
        "ui" => "관리자 화면",
        null => "-",
        _ => name,
    };

    /// <summary>hb/pi의 gripper 값</summary>
    public static string Gripper(string? state) => state switch
    {
        "normal" => "집게 정상",
        "weak" => "집게 약함 (전자석 병행)",
        "broken" => "집게 고장 (전자석만)",
        null or "" => "-",
        _ => state,
    };

    /// <summary>측정 극성 (normal / reversed / abnormal)</summary>
    public static string Polarity(string? polarity) => polarity switch
    {
        "normal" => "정상",
        "reversed" => "반대 (+극 위)",
        "abnormal" => "접촉 불량",
        null => "-",
        _ => polarity,
    };

    /// <summary>상단 카메라 극 방향 (minus_up / plus_up / unknown)</summary>
    public static string VisionPolarity(string? polarity) => polarity switch
    {
        "minus_up" => "−극 위 (정상)",
        "plus_up" => "+극 위 (거꾸로)",
        "unknown" => "판단 못 함",
        null => "-",
        _ => polarity,
    };

    public static string ElectricalStatus(string? status) => status switch
    {
        "measured" => "측정함",
        "skipped" => "생략",
        "failed" => "측정 실패",
        null => "-",
        _ => status,
    };

    public static string RoleName(Auth.Role role) => role switch
    {
        Auth.Role.Admin => "관리자",
        Auth.Role.Operator => "작업자",
        _ => "보기 전용",
    };

    public static string Level(string? level) => level switch
    {
        "ERROR" => "고장",
        "WARN" => "주의",
        "INFO" => "정보",
        _ => level ?? "-",
    };

    // TODO(조시현): central_server.py의 실제 상태 값으로 맞추기
    public static string DeviceState(string? state) => state?.ToLowerInvariant() switch
    {
        "ok" or "online" or "normal" => "정상",
        "delayed" or "delay" or "warn" => "지연",
        "lost" or "offline" => "끊김",
        null or "" or "unknown" => "알 수 없음",
        _ => state!,
    };
}
