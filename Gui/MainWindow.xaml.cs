﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DataModel;
using TfsData;
using Xceed.Wpf.Toolkit;

namespace Gui
{
    public partial class MainWindow
    {
        private ReleaseData _data;
        private bool _includeTfsService = false;
        private const string RegexString = @".*\\\w+(?:.*)?\\((\w\d.\d+.\d+).\d+)";
        private static TfsConnector _tfs;
        public List<string> Categories => GettrimmedSettingList("categories");
        public MainWindow()
        {
            //DoStuff();

            InitializeComponent();

            //Close();
            Loaded += MainWindow_Loaded;
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ProjectCombo.SelectedItem = "FenergoCoreSupport";
            _data = new ReleaseData();
            _data = new ReleaseData{TfsProject = "FenergoCore",IterationSelected = @"FenergoCoreSupport\Current\R8.3.1.7", ChangesetFrom  = "175971" };
            DataContext = _data;
            
            var tfsUrl = ConfigurationManager.AppSettings["tfsUrl"];
            if (string.IsNullOrWhiteSpace(tfsUrl)) return;

            _tfs = new TfsConnector(tfsUrl);

            if (!_tfs.IsConnected) return;
            ProjectStack.Visibility = Visibility.Visible;
            ProjectCombo.ItemsSource = _tfs.Projects;
        }
        

        private void ProjectSelected(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectCombo.SelectedItem == null) return;
            IterationStack.Visibility = Visibility.Visible;
            _data.TfsProject = ProjectCombo.SelectedItem.ToString();
            var iterationPaths = _tfs.GetIterationPaths(_data.TfsProject);

            var regex = new Regex(RegexString);
            var filtered = iterationPaths.Where(x => regex.IsMatch(x)).ToList();

            IterationCombo.ItemsSource = filtered;
        }

        private void IterationSelected(object sender, SelectionChangedEventArgs e)
        {
            if (IterationCombo.SelectedItem == null) return;
            var iteration = IterationCombo.SelectedItem.ToString();
            _data.IterationSelected = iteration;
            var regex = new Regex(RegexString);
            var matchedGroups = regex.Match(iteration).Groups;

            var extractedData = matchedGroups.Count == 3
                ? new Tuple<string, string>(matchedGroups[1].Value, matchedGroups[2].Value)
                : new Tuple<string, string>("", matchedGroups[1].Value);

            _data.ReleaseName = extractedData.Item1;
            _data.TfsBranch = extractedData.Item2;
        }

        private async void ConvertClicked(object sender, RoutedEventArgs e)
        {
            var queryLocation = $"$/{_data.TfsProject}/{_data.TfsBranch}";
            var workItemStateFilter = GettrimmedSettingList("workItemStateFilter");
            LoadingBar.Visibility = Visibility.Visible;
            var downloadedData = await Task.Run(() =>  _tfs.GetChangesetsAndWorkItems(_data.IterationSelected, queryLocation,
                _data.ChangesetFrom, _data.ChangesetTo, Categories, workItemStateFilter));

            LoadingBar.Visibility = Visibility.Hidden;
            _data.CategorizedChanges = downloadedData.CategorizedChanges;

            FilterTfsChanges();

            _data.WorkItems = downloadedData.WorkItems;
            _dataGrid.ItemsSource = _data.CategorizedChanges;
            _dataGrid.Items.SortDescriptions.Add(new SortDescription("Id", ListSortDirection.Descending));
        }

        private static List<string> GettrimmedSettingList(string key)
        {
            return ConfigurationManager.AppSettings[key].Split(',').Select(x => x.Trim()).ToList();
        }

        private void GetChangesetTo(object sender, RoutedEventArgs e)
        {
            ShowChangesetTitleByChangesetId(ChangesetTo, ChangesetToText);
        }

        private void GetChangesetFrom(object sender, RoutedEventArgs e)
        {
            ShowChangesetTitleByChangesetId(ChangesetFrom, ChangesetFromText);
        }

        private async void ShowChangesetTitleByChangesetId(IntegerUpDown input, TextBlock output)
        {
            var changeset = input.Value.GetValueOrDefault();

            string result = "";
            if (changeset > 1) result = await Task.Run(() => _tfs.GetChangesetTitleById(changeset));

            output.Text = result;
        }

        private void SetAsPsRefreshClick(object sender, RoutedEventArgs e)
        {
            ChangesetInfo item = (ChangesetInfo) ((Button) e.Source).DataContext;
            _data.PsRefresh = item;
        }

        private void SetAsCoreClick(object sender, RoutedEventArgs e)
        {
            ChangesetInfo item = (ChangesetInfo)((Button)e.Source).DataContext;
            _data.CoreChange = item;
        }

        private void CreateDocument(object sender, RoutedEventArgs e)
        {
            var changesets = _data.CategorizedChanges.Where(x=>x.Selected).ToList();
            var categories = new Dictionary<string, List<ChangesetInfo>>();
            foreach (var category in Categories)
            {
                var cha = changesets.Where(x => x.Categories.Contains(category)).ToList();
                if (cha.Any())
                {
                    categories.Add(category, cha);
                }
            }

            var workItems = _data.WorkItems.Where(x => x.ClientProject != "General");
            var pbi = _data.WorkItems.Where(x => x.ClientProject == "General");
            new DocumentEditor().ProcessData(_data, categories, workItems, pbi);
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            var checkbox = (CheckBox) sender;
            if (checkbox == null) return;
            _includeTfsService = checkbox.IsChecked.GetValueOrDefault(false);

            FilterTfsChanges();

        }

        private void FilterTfsChanges()
        {
            foreach (var change in _data.CategorizedChanges)
            {
                if (change.CommitedBy == "TFS Service")
                {
                    change.Selected = _includeTfsService;
                }
            }
        }
    }
}
