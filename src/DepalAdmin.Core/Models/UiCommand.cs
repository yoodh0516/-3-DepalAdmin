namespace DepalAdmin.Core.Models;

/// <summary>ui/cmd — 화면 → 서버 버튼 명령. 통신 규약 v0.9 5장.</summary>
public sealed class UiCommand
{
    public string Action { get; set; } = "";
    public string Ts { get; set; } = "";
}
