using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Windows.System;

namespace GithubGet.App.Views
{
    public partial class MainPage : Page
    {
        private bool _loaded;

        public SubscriptionsViewModel SubscriptionsVm { get; } = new();
        public SettingsViewModel SettingsVm { get; } = new();

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            await SettingsVm.InitializeAsync();
            await SubscriptionsVm.InitializeAsync();
        }

        private void OnSectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectedTag = args.SelectedItemContainer?.Tag as string;
            if (string.IsNullOrWhiteSpace(selectedTag))
            {
                return;
            }

            var showSubscriptions = selectedTag == "subscriptions";
            var showUpdates = selectedTag == "updates";
            var showInstalled = selectedTag == "installed";
            var showSettings = selectedTag == "settings" || selectedTag == "manager" || selectedTag == "more";

            SubscriptionsSection.Visibility = showSubscriptions ? Visibility.Visible : Visibility.Collapsed;
            UpdatesSection.Visibility = showUpdates ? Visibility.Visible : Visibility.Collapsed;
            InstalledSection.Visibility = showInstalled ? Visibility.Visible : Visibility.Collapsed;
            SettingsSection.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnRepoSearchInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            if (SubscriptionsVm.SearchReposCommand.CanExecute(null))
            {
                SubscriptionsVm.SearchReposCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnInstalledSearchInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            if (SubscriptionsVm.ApplyInstalledFilterCommand.CanExecute(null))
            {
                SubscriptionsVm.ApplyInstalledFilterCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnUpdatesSearchInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            if (SubscriptionsVm.ApplyUpdatesFilterCommand.CanExecute(null))
            {
                SubscriptionsVm.ApplyUpdatesFilterCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
