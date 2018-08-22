﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DataModel;
using TfsData;
using Xceed.Words.NET;
using Xceed.Wpf.Toolkit;

namespace Gui
{
    public partial class MainWindow
    {
        private ReleaseData _data;
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
            _data = new ReleaseData();
            DataContext = _data;
            ConnectTfsButton(null, null);
        }

        private void DoStuff(ReleaseData data)
        {

            var dTestDocx = @"D:\test.docx";
            string fileName = @"D:\Template.docx";

            using (var doc = DocX.Load(fileName))
            {
                doc.ReplaceText("{ReleaseName}", _data.ReleaseName);
                doc.ReplaceText("{ReleaseDate}", _data.ReleaseDateFormated);
                doc.ReplaceText("{TfsBranch}", _data.TfsBranch);
                doc.ReplaceText("{QaBuildName}", _data.QaBuildName);
                doc.ReplaceText("{QaBuildDate}", _data.QaBuildDateFormated);
                doc.ReplaceText("{CoreBuildName}", _data.CoreBuildName);
                doc.ReplaceText("{CoreBuildDate}", _data.CoreBuildDateFormated);
                doc.ReplaceText("{PsRefreshChangeset}", _data.PsRefreshChangeset);
                doc.ReplaceText("{PsRefreshDate}", _data.PsRefreshDateFormated);
                doc.ReplaceText("{PsRefreshName}", _data.PsRefreshName);
                doc.ReplaceText("{CoreChangeset}", _data.CoreChangeset);
                doc.ReplaceText("{CoreDate}", _data.CoreDateFormated);

                var secondSection = doc.Paragraphs.FirstOrDefault(x => x.Text == "Code Change sets in this Release");
                var paragraph = secondSection.InsertParagraphAfterSelf("asd").FontSize(10d);
                InsertBeforeOrAfter placeholder = paragraph;
                foreach (var category in data.CategorizedChanges)
                {
                    var p = placeholder.InsertParagraphAfterSelf(category.Name).FontSize(11d).Heading(HeadingType.Heading2);

                    var table = p.InsertTableAfterSelf(2, 6);
                    table.Rows[0].Cells[0].Paragraphs[0].Append("TFS").Bold();
                    table.Rows[0].Cells[1].Paragraphs[0].Append("Developer").Bold();
                    table.Rows[0].Cells[2].Paragraphs[0].Append("Date/Time").Bold();
                    table.Rows[0].Cells[3].Paragraphs[0].Append("Description").Bold();
                    table.Rows[0].Cells[4].Paragraphs[0].Append("Work Item").Bold();
                    table.Rows[0].Cells[5].Paragraphs[0].Append("Work Item Description").Bold();

                    var rowPattern = table.Rows[1];
                    rowPattern.Cells[0].Paragraphs[0].Append("{TfsID}");
                    rowPattern.Cells[1].Paragraphs[0].Append("{Dev}");
                    rowPattern.Cells[2].Paragraphs[0].Append("{Date}");
                    rowPattern.Cells[3].Paragraphs[0].Append("{Desc}");
                    rowPattern.Cells[4].Paragraphs[0].Append("{WorkItemId}");
                    rowPattern.Cells[5].Paragraphs[0].Append("{WorkItemTitle}");

                    foreach (var change in category.Changes)
                    {
                        var newItem = table.InsertRow(rowPattern, table.RowCount - 1);

                        newItem.ReplaceText("{TfsID}", change.Id.ToString());
                        newItem.ReplaceText("{Dev}", change.CommitedBy);
                        newItem.ReplaceText("{Date}", change.Created.ToString());
                        newItem.ReplaceText("{Desc}", change.Comment);
                        newItem.ReplaceText("{WorkItemId}", change.WorkItemId.ToString());
                        newItem.ReplaceText("{WorkItemTitle}", change.WorkItemTitle.ToString());

                    }





                    rowPattern.Remove();
                    placeholder = table;
                }

                var thirdSection = placeholder.CreateHeadingSection("Product reported Defects in this Release");
                var workItemTable = thirdSection.InsertTableAfterSelf(2, 3);
                workItemTable.SetWidths(new[] { 100f, 1000f, 100f });
                workItemTable.Rows[0].Cells[0].Paragraphs[0].Append("Bug Id").Bold();
                workItemTable.Rows[0].Cells[1].Paragraphs[0].Append("Work Item Description").Bold();
                workItemTable.Rows[0].Cells[2].Paragraphs[0].Append("Client Project").Bold();

                var placeholderRow = workItemTable.Rows[1];
                placeholderRow.Cells[0].Paragraphs[0].Append("{TfsID}");
                placeholderRow.Cells[1].Paragraphs[0].Append("{WorkItemTitle}");
                placeholderRow.Cells[2].Paragraphs[0].Append("{Client}");

                foreach (var item in data.WorkItems)
                {
                    var newItem = workItemTable.InsertRow(placeholderRow, workItemTable.RowCount - 1);

                    newItem.ReplaceText("{TfsID}", item.Id.ToString());
                    newItem.ReplaceText("{WorkItemTitle}", item.Title);
                    newItem.ReplaceText("{Client}", item.ClientProject);

                }
                placeholderRow.Remove();


                var fourthSection = workItemTable.CreateHeadingSection("Product Backlog Items and KTRs in this Release");
                var fifthSection = fourthSection.CreateHeadingSection("Test Report");
                var sixthSection = fifthSection.CreateHeadingSection("Known issues in this Release");

                doc.SaveAs(dTestDocx);
            }

            Process.Start(dTestDocx);
        }

        private void ConnectTfsButton(object sender, RoutedEventArgs e)
        {
            _tfs = new TfsConnector(_data.Url);

            if (!_tfs.IsConnected) return;
            ProjectStack.Visibility = Visibility.Visible;
            TfsProjectStack.Visibility = Visibility.Visible;
            ProjectCombo.ItemsSource = _tfs.Projects;
        }

        private void ProjectSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_data.ProjectSelected == "") return;
            IterationStack.Visibility = Visibility.Visible;
            BranchStack.Visibility = Visibility.Visible;
            TfsProject.Text = _data.ProjectSelected;
            var iterationPaths = _tfs.GetIterationPaths(_data.ProjectSelected);

            var regex = new Regex(RegexString);
            var filtered = iterationPaths.Where(x => regex.IsMatch(x)).ToList();

            IterationCombo.ItemsSource = filtered;
        }

        private void IterationSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_data.IterationSelected == "") return;
            var iteration = IterationCombo.SelectedItem.ToString();
            var regex = new Regex(RegexString);
            var matchedGroups = regex.Match(iteration).Groups;

            var extractedData = matchedGroups.Count == 3
                ? new Tuple<string, string>(matchedGroups[1].Value, matchedGroups[2].Value)
                : new Tuple<string, string>("", matchedGroups[1].Value);

            _data.ReleaseName = extractedData.Item1;
            _data.TfsBranch = extractedData.Item2;
        }

        private void ConvertClicked(object sender, RoutedEventArgs e)
        {
            var queryLocation = $"$/FenergoCore/{_data.TfsBranch}";
            var workItemStateFilter = GettrimmedSettingList("workItemStateFilter");
            var data = _tfs.GetChangesetsAndWorkItems(_data.IterationSelected, queryLocation,
                _data.ChangesetFrom, _data.ChangesetTo, Categories, workItemStateFilter);

            DoStuff(data);
            //_listBox.ItemsSource = data.;
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
    }
}
