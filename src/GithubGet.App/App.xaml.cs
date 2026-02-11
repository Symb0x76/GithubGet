using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.UI;
using WinRT.Interop;

namespace GithubGet.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? window;
        private AppWindow? appWindow;
        private MainPage? mainPage;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            window = new Window();
            mainPage = new MainPage();

            window.Title = "GithubGet";
            window.Content = mainPage;
            window.Closed += OnWindowClosed;

            ConfigureTitleBar();
            window.Activate();

            mainPage.ActualThemeChanged += OnMainPageActualThemeChanged;
            ApplyTitleBarTheme(mainPage.ActualTheme);
        }

        private void ConfigureTitleBar()
        {
            if (window is null || mainPage is null)
            {
                return;
            }

            window.ExtendsContentIntoTitleBar = true;
            window.SetTitleBar(mainPage.TitleBarElement);

            var windowHandle = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            appWindow = AppWindow.GetFromWindowId(windowId);
        }

        private void OnMainPageActualThemeChanged(FrameworkElement sender, object args)
        {
            ApplyTitleBarTheme(sender.ActualTheme);
        }

        private void ApplyTitleBarTheme(ElementTheme theme)
        {
            if (appWindow is null || !AppWindowTitleBar.IsCustomizationSupported())
            {
                return;
            }

            var useDarkPalette = theme != ElementTheme.Light;
            var foregroundColor = useDarkPalette ? Colors.White : Colors.Black;
            var inactiveForegroundColor = useDarkPalette
                ? Color.FromArgb(170, 255, 255, 255)
                : Color.FromArgb(170, 0, 0, 0);
            var hoverBackgroundColor = useDarkPalette
                ? Color.FromArgb(30, 255, 255, 255)
                : Color.FromArgb(30, 0, 0, 0);
            var pressedBackgroundColor = useDarkPalette
                ? Color.FromArgb(55, 255, 255, 255)
                : Color.FromArgb(55, 0, 0, 0);

            var titleBar = appWindow.TitleBar;
            titleBar.BackgroundColor = Colors.Transparent;
            titleBar.ForegroundColor = foregroundColor;
            titleBar.InactiveBackgroundColor = Colors.Transparent;
            titleBar.InactiveForegroundColor = inactiveForegroundColor;

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = foregroundColor;
            titleBar.ButtonHoverBackgroundColor = hoverBackgroundColor;
            titleBar.ButtonHoverForegroundColor = foregroundColor;
            titleBar.ButtonPressedBackgroundColor = pressedBackgroundColor;
            titleBar.ButtonPressedForegroundColor = foregroundColor;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = inactiveForegroundColor;
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            if (mainPage is not null)
            {
                mainPage.ActualThemeChanged -= OnMainPageActualThemeChanged;
            }

            if (window is not null)
            {
                window.Closed -= OnWindowClosed;
            }
        }
    }
}
