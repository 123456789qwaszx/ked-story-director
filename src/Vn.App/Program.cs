using Avalonia;
using Vn.App.Services;

namespace Vn.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // 데스크톱 앱이라 콘솔이 없다. 여기서 잡지 않으면 시작 예외는 아무 흔적 없이 사라진다.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception exception)
        {
            Report($"{AppIdentity.Name} 시작", exception);
            return 1;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .With(Views.HangulFonts.Options)   // 한글은 이름으로 찾은 한글 글꼴로 — 기울임을 청한 한글도 기운다(writer-formatting WF-D17)
            .LogToTrace();

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Report($"{AppIdentity.Name} 실행", exception);
        }
    }

    /// <summary>
    /// 파일로 남기고, 콘솔이 붙어 있으면 콘솔에도 낸다.
    ///
    /// OutputType은 WinExe라 Release에서 콘솔 창이 새로 뜨지 않는다.
    /// 다만 <c>dotnet run</c>이나 터미널에서 실행하면 부모의 stderr를 물려받으므로
    /// 그 자리에서 바로 원인이 보인다.
    /// </summary>
    private static void Report(string context, Exception exception)
    {
        string? path = StartupLog.TryWrite(context, exception);

        try
        {
            Console.Error.WriteLine($"[{AppIdentity.Name}] {context} 실패: {exception}");

            if (path is not null)
            {
                Console.Error.WriteLine($"[{AppIdentity.Name}] 로그: {path}");
            }
        }
        catch (Exception)
        {
            // 콘솔이 없으면 파일 기록만으로 충분하다.
        }
    }
}
