using System.Windows;
using DepalAdmin.Comm;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Data;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Pages;
using DepalAdmin.Core.Settings;
using DepalAdmin.Feature.Alarms;
using DepalAdmin.Feature.Auth;
using DepalAdmin.Feature.Chat;
using DepalAdmin.Feature.Devices;
using DepalAdmin.Feature.EventLog;
using DepalAdmin.Feature.Handover;
using DepalAdmin.Feature.History;
using DepalAdmin.Feature.Operation;
using DepalAdmin.Feature.PiStatus;
using DepalAdmin.Feature.Process;
using DepalAdmin.Feature.Report;
using DepalAdmin.Feature.Results;
using DepalAdmin.Feature.Settings;
using DepalAdmin.Feature.Stats;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Shell;

/// <summary>담당: 조시현 — 시작 코드. 모든 기능을 여기서 한 번씩 등록한다.</summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 화면 코드에서 잡지 못한 오류 → 프로그램이 꺼지지 않고 알림만
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        var settings = AppSettings.Load(AppContext.BaseDirectory);
        var services = new ServiceCollection();

        // ── 공통 (조시현) ──
        services.AddSingleton(settings);
        services.AddSingleton<Session>();
        services.AddSingleton<Navigator>();

        // 라인 통신: 가짜/실제를 고르고, 권한 확인·작업 기록 겉포장을 씌운다
        services.AddSingleton<ILineClient>(sp =>
        {
            ILineClient inner = settings.UseFakeMqtt ? new FakeLineClient() : new MqttLineClient(settings);
            return new AuditingLineClient(inner, sp.GetRequiredService<Session>(), sp.GetRequiredService<IAuditLog>());
        });

        // 채팅 통신: 가짜/실제
        services.AddSingleton<IChatClient>(_ => settings.UseFakeMqtt
            ? (IChatClient)new FakeChatClient()
            : new MqttChatClient(settings));

        // 이력 DB: 가짜/실제 (MySqlHistoryRepository는 장성찬 프로젝트에 있음)
        services.AddSingleton<IHistoryRepository>(_ => settings.UseFakeDb
            ? (IHistoryRepository)new FakeHistoryRepository()
            : new MySqlHistoryRepository(settings));

        // ── 기능 (탭 순서는 각 페이지의 Order, README 탭 순서표 참고) ──
        // 장성찬
        services.AddAuthFeature();         // 로그인 · 계정 · 작업 기록
        services.AddEventLogFeature();     // 이벤트 로그
        services.AddHistoryFeature();      // 이력 조회
        // 유동호
        services.AddOperationFeature();    // 조작 버튼 패널 → 라인 현황에 끼움
        services.AddHandoverFeature();     // 인수인계
        // 노원종
        services.AddSettingsFeature();     // 설정
        services.AddAlarmsFeature();       // 알람 · 아래 알람 줄
        services.AddPiStatusFeature();     // 라파 세부 패널 → 장치 상태에 끼움
        // 김하균
        services.AddResultsFeature();      // 검사 결과 · 팔레트 맵 패널 → 라인 현황에 끼움
        // 김초아
        services.AddProcessFeature();      // 라인 현황 (공정 흐름도 · 분류 개수 · AGV)
        // 조시현
        services.AddDevicesFeature();      // 장치 상태 카드
        services.AddStatsFeature();        // 통계
        services.AddReportFeature();       // 보고서
        services.AddChatFeature();         // 채팅

        // ── 메인 창 (조시현) ──
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider();

        var window = _services.GetRequiredService<MainWindow>();   // 이때 모든 페이지가 만들어지고 이벤트를 구독한다

        if (!_services.GetRequiredService<ILoginFlow>().ShowLogin(null))
        {
            Shutdown();
            return;
        }

        MainWindow = window;
        window.Show();
        await _services.GetRequiredService<ILineClient>().StartAsync();
        await _services.GetRequiredService<IChatClient>().StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_services != null)
        {
            var line = _services.GetRequiredService<ILineClient>();
            var chat = _services.GetRequiredService<IChatClient>();
            // UI 스레드를 막지 않도록 별도 스레드에서 최대 2초만 기다림
            Task.Run(async () =>
            {
                await chat.StopAsync();
                await line.StopAsync();
            }).Wait(TimeSpan.FromSeconds(2));
            _services.Dispose();
        }
        base.OnExit(e);
    }
}
