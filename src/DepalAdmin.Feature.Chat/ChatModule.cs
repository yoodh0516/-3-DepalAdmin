using DepalAdmin.Core.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.Chat;

/// <summary>담당: 조시현 — 채팅 (연결 IChatClient는 Shell이 가짜/실제를 골라 등록)</summary>
public static class ChatModule
{
    public static IServiceCollection AddChatFeature(this IServiceCollection services)
    {
        services.AddSingleton<ChatLogFile>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<IPage, ChatPage>();
        return services;
    }
}

public sealed class ChatPage : PageBase<ChatView>
{
    public ChatPage(ChatViewModel vm) : base(vm) { }
    public override string Title => "채팅";
    public override int Order => 60;
}
