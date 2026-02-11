using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Windows.System;

namespace GithubGet.App.Views
{
    public partial class MainPage : Page
    {
        private bool _loaded;
        private string _selectedSectionTag = "installed";
        private readonly Stack<string> _sectionHistory = new();
        private bool _isNavigatingBack;

        public SubscriptionsViewModel SubscriptionsVm { get; } = new();
        public SettingsViewModel SettingsVm { get; } = new();
        public FrameworkElement TitleBarElement => AppTitleBar;

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
            UpdateTitleBarForSection(_selectedSectionTag);
        }

        private void OnSectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectedTag = args.SelectedItemContainer?.Tag as string;
            if (string.IsNullOrWhiteSpace(selectedTag))
            {
                return;
            }

            if (!_isNavigatingBack && !string.Equals(selectedTag, _selectedSectionTag, StringComparison.OrdinalIgnoreCase))
            {
                _sectionHistory.Push(_selectedSectionTag);
            }

            var showSubscriptions = selectedTag == "subscriptions";
            var showUpdates = selectedTag == "updates";
            var showInstalled = selectedTag == "installed";
            var showSettings = selectedTag == "settings" || selectedTag == "manager" || selectedTag == "more";

            SubscriptionsSection.Visibility = showSubscriptions ? Visibility.Visible : Visibility.Collapsed;
            UpdatesSection.Visibility = showUpdates ? Visibility.Visible : Visibility.Collapsed;
            InstalledSection.Visibility = showInstalled ? Visibility.Visible : Visibility.Collapsed;
            SettingsSection.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
            UpdateTitleBarForSection(selectedTag);
        }

        private void UpdateTitleBarForSection(string selectedTag)
        {
            _selectedSectionTag = selectedTag;

            var showSubscriptionsTools = selectedTag == "subscriptions";
            var showUpdatesTools = selectedTag == "updates";
            var showInstalledTools = selectedTag == "installed";
            var showNeutralTools = !showSubscriptionsTools && !showUpdatesTools && !showInstalledTools;

            TitleBarSubscriptionsSearchPanel.Visibility = showSubscriptionsTools ? Visibility.Visible : Visibility.Collapsed;
            TitleBarUpdatesSearchPanel.Visibility = showUpdatesTools ? Visibility.Visible : Visibility.Collapsed;
            TitleBarInstalledSearchPanel.Visibility = showInstalledTools ? Visibility.Visible : Visibility.Collapsed;
            TitleBarNeutralPanel.Visibility = showNeutralTools ? Visibility.Visible : Visibility.Collapsed;
            TitleBarBackButton.IsEnabled = _sectionHistory.Count > 0;
        }

        private void OnTitleBarMenuClicked(object sender, RoutedEventArgs e)
        {
            RootNavigation.IsPaneOpen = !RootNavigation.IsPaneOpen;
        }

        private void OnTitleBarBackClicked(object sender, RoutedEventArgs e)
        {
            if (_sectionHistory.Count == 0)
            {
                TitleBarBackButton.IsEnabled = false;
                return;
            }

            string? targetTag = null;
            while (_sectionHistory.Count > 0)
            {
                var candidate = _sectionHistory.Pop();
                if (string.IsNullOrWhiteSpace(candidate) ||
                    string.Equals(candidate, _selectedSectionTag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                targetTag = candidate;
                break;
            }

            if (targetTag is null)
            {
                TitleBarBackButton.IsEnabled = false;
                return;
            }

            _isNavigatingBack = true;
            try
            {
                NavigateToSection(targetTag);
            }
            finally
            {
                _isNavigatingBack = false;
                TitleBarBackButton.IsEnabled = _sectionHistory.Count > 0;
            }
        }

        private void NavigateToSection(string tag)
        {
            var targetItem = FindNavigationItemByTag(RootNavigation.MenuItems, tag) ??
                             FindNavigationItemByTag(RootNavigation.FooterMenuItems, tag);
            if (targetItem is null)
            {
                return;
            }

            RootNavigation.SelectedItem = targetItem;
        }

        private static NavigationViewItem? FindNavigationItemByTag(IList<object> items, string tag)
        {
            foreach (var item in items)
            {
                if (item is not NavigationViewItem navigationItem)
                {
                    continue;
                }

                if (string.Equals(navigationItem.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return navigationItem;
                }

                if (navigationItem.MenuItems.Count == 0)
                {
                    continue;
                }

                var nestedItem = FindNavigationItemByTag(navigationItem.MenuItems, tag);
                if (nestedItem is not null)
                {
                    return nestedItem;
                }
            }

            return null;
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
