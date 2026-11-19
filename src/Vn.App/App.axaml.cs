using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Vn.App.Services;

namespace Vn.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 맥의 메뉴 막대 첫 칸(앱 메뉴)에 서는 이름이다 — 다른 OS에서는 보이지 않는다. 앱 메뉴를 빈 것으로 주어, Avalonia가 대신 채우는
        // 「About Avalonia」를 걷는다(가리기 · 종료 같은 맥의 기본 줄은 맥이 그대로 세운다). File 메뉴는 창 안에 그대로다(writer-app WA4).
        Name = AppIdentity.Name;
        NativeMenu.SetMenu(this, new NativeMenu());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 주 창을 만들지 못해도 아무 말 없이 사라지지 않는다.
    /// 창이 하나도 없으면 프로세스가 그대로 끝나 작가는 원인을 알 수 없다.
    /// 그래서 실패는 파일로 남기고, 무슨 일이 있었는지 보여주는 창을 대신 띄운다.
    /// </summary>
    private static Window CreateMainWindow()
    {
        try
        {
            return new MainWindow();
        }
        catch (Exception exception)
        {
            StartupLog.TryWrite("주 창 만들기", exception);
            return CreateStartupFailureWindow(exception);
        }
    }

    private static Window CreateStartupFailureWindow(Exception exception)
    {
        var details = new TextBox
        {
            Text = exception.ToString(),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Cascadia Mono,Consolas"),
            FontSize = 12,
            Height = 260
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(details, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(details, ScrollBarVisibility.Auto);

        return new Window
        {
            Title = $"{AppIdentity.Name} — 시작하지 못했습니다",
            Width = 760,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        // 작가용 메시지. 개발자용 상세 내용은 아래 상자와 로그 파일에 있다.
                        Text = StartupLog.WriterMessage($"{AppIdentity.Name}를 시작하는"),
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = "아래 내용을 그대로 개발자에게 전달해 주세요.",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.7
                    },
                    details
                }
            }
        };
    }
}
