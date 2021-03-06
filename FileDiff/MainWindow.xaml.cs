﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FileDiff
{
	public partial class MainWindow : Window
	{

		#region Members

		MainWindowViewModel ViewModel { get; } = new MainWindowViewModel();

		List<FileItem> folderDiffItems = new List<FileItem>();

		int firstDiff = -1;
		int lastDiff = -1;
		bool renderComplete = false;

		DiffControl activeDiff;

		string rightSelection = "";
		string leftSelection = "";

		readonly DispatcherTimer progressTimer = new DispatcherTimer();

		#endregion

		#region Constructor

		public MainWindow()
		{
			InitializeComponent();

			progressTimer.Interval = new TimeSpan(0, 0, 0, 1);
			progressTimer.Tick += ProgressTimer_Tick;

			DataContext = ViewModel;

			SearchPanel.Visibility = Visibility.Collapsed;
			activeDiff = LeftDiff;
		}

		#endregion

		#region Properties

		private bool NoManualEdit
		{
			get
			{
				return !ViewModel.LeftFileEdited && !ViewModel.RightFileEdited;
			}
		}

		#endregion

		#region Methods

		private void Compare()
		{
			rightSelection = "";
			leftSelection = "";
			ViewModel.LeftPath = ViewModel.LeftPath.Trim();
			ViewModel.RightPath = ViewModel.RightPath.Trim();

			if (File.Exists(ViewModel.LeftPath) && File.Exists(ViewModel.RightPath))
			{
				ViewModel.Mode = CompareMode.File;

				CompareFiles();
			}
			else if (Directory.Exists(ViewModel.LeftPath) && Directory.Exists(ViewModel.RightPath))
			{
				ViewModel.Mode = CompareMode.Folder;

				CompareDirectories();
			}
		}

		private void CompareFiles()
		{
			Debug.Print("------ CompareFiles");

			string leftPath = "";
			string rightPath = "";

			ProgressPanel.Visibility = Visibility.Hidden;

			if (ViewModel.Mode == CompareMode.File)
			{
				leftPath = ViewModel.LeftPath;
				rightPath = ViewModel.RightPath;
			}
			else if (ViewModel.MasterDetail && LeftFolder.SelectedFile != null && RightFolder.SelectedFile != null)
			{
				leftPath = LeftFolder.SelectedFile.Path;
				rightPath = RightFolder.SelectedFile.Path;
			}

			List<Line> leftLines = null;
			List<Line> rightLines = null;

			ViewModel.LeftFileEncoding = null;
			ViewModel.RightFileEncoding = null;

			try
			{
				if (File.Exists(leftPath))
				{
					leftLines = new List<Line>();
					leftSelection = leftPath;
					ViewModel.LeftFileEncoding = Unicode.GetEncoding(leftPath);
					ViewModel.LeftFileDirty = false;

					int i = 0;
					foreach (string s in File.ReadAllLines(leftPath, ViewModel.LeftFileEncoding.Type))
					{
						leftLines.Add(new Line() { Type = TextState.Deleted, Text = s, LineIndex = i++ });
					}
				}

				if (File.Exists(rightPath))
				{
					rightLines = new List<Line>();
					rightSelection = rightPath;
					ViewModel.RightFileEncoding = Unicode.GetEncoding(rightPath);
					ViewModel.RightFileDirty = false;

					int i = 0;
					foreach (string s in File.ReadAllLines(rightPath, ViewModel.RightFileEncoding.Type))
					{
						rightLines.Add(new Line() { Type = TextState.New, Text = s, LineIndex = i++ });
					}
				}
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (leftLines?.Count > 0 && rightLines?.Count > 0)
			{
				ViewModel.GuiFrozen = true;

				ProgressBarCompare.Value = 0;
				ProgressBarCompare.Maximum = leftLines.Count + rightLines.Count;

				BackgroundCompare.progressHandler = new Progress<int>(CompareStatusUpdate);
				Task.Run(() => BackgroundCompare.CompareFiles(leftLines, rightLines)).ContinueWith(CompareFilesFinnished, TaskScheduler.FromCurrentSynchronizationContext());

				progressTimer.Start();
			}
			else
			{
				ViewModel.LeftFile = leftLines == null ? new ObservableCollection<Line>() : new ObservableCollection<Line>(leftLines);
				ViewModel.RightFile = rightLines == null ? new ObservableCollection<Line>() : new ObservableCollection<Line>(rightLines);
				InitNavigationState();
			}
		}

		private void CompareFilesFinnished(Task<Tuple<List<Line>, List<Line>, TimeSpan>> task)
		{
			Debug.Print("------ CompareFilesFinnished");

			progressTimer.Stop();
			ViewModel.GuiFrozen = false;

			if (!BackgroundCompare.CompareCancelled)
			{
				ViewModel.LeftFile = new ObservableCollection<Line>(task.Result.Item1);
				ViewModel.RightFile = new ObservableCollection<Line>(task.Result.Item2);
				Statusbar.Text = $"Compare time {TimeSpanToShortString(task.Result.Item3)}";
			}
			else
			{
				ViewModel.LeftFile = new ObservableCollection<Line>();
				ViewModel.RightFile = new ObservableCollection<Line>();
				Statusbar.Text = $"Compare cancelled";
			}

			LeftDiff.Focus();
			InitNavigationState();
		}

		private void CompareStatusUpdate(int progress)
		{
			ProgressBarCompare.Value = progress;
		}

		private void CompareDirectories()
		{
			Debug.Print("------ CompareDirectories");

			ProgressPanel.Visibility = Visibility.Hidden;

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			ViewModel.Clear();

			ViewModel.GuiFrozen = true;

			ProgressBarCompare.Value = 0;
			ProgressBarCompare.Maximum = 'Z' - 'A';

			string leftPath = ViewModel.LeftPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string rightPath = ViewModel.RightPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			BackgroundCompare.progressHandler = new Progress<int>(CompareStatusUpdate);
			Task.Run(() => BackgroundCompare.CompareDirectories(leftPath, rightPath)).ContinueWith(CompareDirectoriesFinnished, TaskScheduler.FromCurrentSynchronizationContext());

			progressTimer.Start();
		}

		private void CompareDirectoriesFinnished(Task<Tuple<ObservableCollection<FileItem>, ObservableCollection<FileItem>, TimeSpan>> task)
		{
			Debug.Print("------ CompareDirectoriesFinnished");

			progressTimer.Stop();
			ViewModel.GuiFrozen = false;

			if (!BackgroundCompare.CompareCancelled)
			{
				LeftFolder.Init();
				RightFolder.Init();

				ViewModel.LeftFolder = task.Result.Item1;
				ViewModel.RightFolder = task.Result.Item2;
				Statusbar.Text = $"Compare time {TimeSpanToShortString(task.Result.Item3)}";

				folderDiffItems = new List<FileItem>();
				GetFolderDiffItems(ViewModel.RightFolder, folderDiffItems);

			}
			else
			{
				ViewModel.LeftFile = new ObservableCollection<Line>();
				ViewModel.RightFile = new ObservableCollection<Line>();
				Statusbar.Text = $"Compare cancelled";
			}

			LeftFolder.Focus();
		}

		private string TimeSpanToShortString(TimeSpan timeSpan)
		{
			if (timeSpan.TotalHours >= 1)
			{
				return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
			}
			if (timeSpan.Minutes > 0)
			{
				return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
			}
			return $"{timeSpan.Seconds}.{timeSpan.Milliseconds.ToString().PadLeft(3, '0')}s";
		}

		private void InitNavigationState()
		{
			ViewModel.CurrentDiff = -1;
			ViewModel.CurrentDiffLength = 1;
			firstDiff = -1;
			lastDiff = -1;
			ViewModel.EditMode = false;
			ViewModel.LeftFileDirty = false;
			ViewModel.LeftFileEdited = false;
			ViewModel.RightFileDirty = false;
			ViewModel.RightFileEdited = false;

			if (ViewModel.LeftFile?.Count > 0 && ViewModel.RightFile?.Count > 0)
			{
				UpdateNavigationButtons();
			}

			LeftDiff.Init();
			RightDiff.Init();

			VerticalFileScrollbar.Value = 0;
			LeftHorizontalScrollbar.Value = 0;

			if (firstDiff != -1)
			{
				MoveToDiffLine(firstDiff);
				ViewModel.CurrentDiff = firstDiff;
			}
		}

		private void UpdateNavigationButtons()
		{
			firstDiff = -1;
			lastDiff = -1;

			for (int i = 0; i < ViewModel.LeftFile.Count; i++)
			{
				if (i == 0 || ViewModel.LeftFile[i - 1].Type == TextState.FullMatch)
				{
					if (firstDiff == -1 && ViewModel.LeftFile[i].Type != TextState.FullMatch)
					{
						firstDiff = i;

					}
					if (ViewModel.LeftFile[i].Type != TextState.FullMatch)
					{
						lastDiff = i;
					}
				}
			}

			if (lastDiff == -1)
			{
				ViewModel.CurrentDiff = -1;
				ViewModel.CurrentDiffLength = -1;
			}
		}

		private void MoveToFirstDiff()
		{
			ViewModel.CurrentDiff = -1;
			MoveToNextDiff();
		}

		private void MoveToLastDiff()
		{
			ViewModel.CurrentDiff = ViewModel.LeftFile.Count;
			MoveToPrevoiusDiff();
		}

		private void MoveToPrevoiusDiff()
		{
			for (int i = ViewModel.CurrentDiff - 1; i >= 0; i--)
			{
				if (i == 0 || ViewModel.LeftFile[i - 1].Type == TextState.FullMatch)
				{
					if (ViewModel.LeftFile[i].Type != TextState.FullMatch || ViewModel.RightFile[i].Type != TextState.FullMatch)
					{
						ViewModel.CurrentDiff = i;
						MoveToDiffLine(i);
						return;
					}
				}
			}
		}

		private void MoveToNextDiff()
		{
			for (int i = ViewModel.CurrentDiff + 1; i < ViewModel.LeftFile.Count; i++)
			{
				if (i == 0 || ViewModel.LeftFile[i - 1].Type == TextState.FullMatch)
				{
					if (ViewModel.LeftFile[i].Type != TextState.FullMatch || ViewModel.RightFile[i].Type != TextState.FullMatch)
					{
						ViewModel.CurrentDiff = i;
						MoveToDiffLine(i);
						return;
					}
				}
			}
		}

		private void MoveToDiffLine(int i)
		{
			int diffLength = 1;

			while (i + diffLength < ViewModel.LeftFile.Count && ViewModel.LeftFile[i + diffLength].Type != TextState.FullMatch)
			{
				diffLength++;
			}

			ViewModel.CurrentDiff = i;
			ViewModel.CurrentDiffLength = diffLength;

			CenterOnLine(i);
		}

		private void CenterOnLine(int i)
		{
			int visibleLines = LeftDiff.VisibleLines <= 0 ? (int)(LeftDiff.ActualHeight / OneCharacter.ActualHeight) : LeftDiff.VisibleLines;
			VerticalFileScrollbar.Value = i - (visibleLines / 2) + 1;
		}

		private void LoadSettings()
		{
			AppSettings.ReadSettingsFromDisk();

			this.Left = AppSettings.PositionLeft;
			this.Top = AppSettings.PositionTop;
			this.Width = AppSettings.Width;
			this.Height = AppSettings.Height;
			this.WindowState = AppSettings.WindowState;
		}

		private void SaveSettings()
		{
			AppSettings.PositionLeft = this.Left;
			AppSettings.PositionTop = this.Top;
			AppSettings.Width = this.Width;
			AppSettings.Height = this.Height;
			AppSettings.WindowState = this.WindowState;

			AppSettings.WriteSettingsToDisk();
		}

		private void ProcessSearchResult(int result)
		{
			if (result != -1)
			{
				SearchBox.Background = new SolidColorBrush(Colors.White);
				CenterOnLine(result);
			}
			else
			{
				SearchBox.Background = new SolidColorBrush(Colors.Tomato);
			}
		}

		private void UpdateColumnWidths(Grid columnGrid)
		{
			ViewModel.NameColumnWidth = Math.Max(columnGrid.ColumnDefinitions[0].Width.Value, 20);
			ViewModel.SizeColumnWidth = Math.Max(columnGrid.ColumnDefinitions[2].Width.Value, 20);
			ViewModel.DateColumnWidth = Math.Max(columnGrid.ColumnDefinitions[4].Width.Value, 20);


			// HACK: Workaround until I figure out how to data bind the column definition widths two way.
			LeftColumns.ColumnDefinitions[0].Width = new GridLength(ViewModel.NameColumnWidth);
			RightColumns.ColumnDefinitions[0].Width = new GridLength(ViewModel.NameColumnWidth);
			LeftColumns.ColumnDefinitions[2].Width = new GridLength(ViewModel.SizeColumnWidth);
			RightColumns.ColumnDefinitions[2].Width = new GridLength(ViewModel.SizeColumnWidth);
			LeftColumns.ColumnDefinitions[4].Width = new GridLength(ViewModel.DateColumnWidth);
			RightColumns.ColumnDefinitions[4].Width = new GridLength(ViewModel.DateColumnWidth);


			double totalWidth = 0;

			foreach (ColumnDefinition d in columnGrid.ColumnDefinitions)
			{
				totalWidth += d.Width.Value;
			}

			LeftColumns.Width = totalWidth;
			LeftFolderHorizontalScrollbar.ViewportSize = LeftFolder.ActualWidth;
			LeftFolderHorizontalScrollbar.Maximum = totalWidth - LeftFolder.ActualWidth;
			LeftFolderHorizontalScrollbar.LargeChange = LeftFolder.ActualWidth;

			RightColumns.Width = totalWidth;
			RightFolderHorizontalScrollbar.ViewportSize = RightFolder.ActualWidth;
			RightFolderHorizontalScrollbar.Maximum = totalWidth - RightFolder.ActualWidth;
			RightFolderHorizontalScrollbar.LargeChange = RightFolder.ActualWidth;
		}

		private void GetFolderDiffItems(ObservableCollection<FileItem> parent, List<FileItem> items)
		{
			foreach (FileItem fileItem in parent)
			{
				if (!fileItem.IsFolder && fileItem.Type == TextState.PartialMatch || fileItem == LeftFolder.SelectedFile)
				{
					items.Add(fileItem);
				}
				GetFolderDiffItems(fileItem.Children, items);
			}
		}

		private string GetFocusedPath(object control)
		{
			if (control == LeftDiff) return leftSelection;
			else if (control == RightDiff) return rightSelection;
			else if (control == LeftFolder) return LeftFolder.SelectedFile.Path;
			else if (control == RightFolder) return RightFolder.SelectedFile.Path;

			return "";
		}

		private void CheckForUpdate(bool forced = false)
		{
			if (AppSettings.CheckForUpdates && AppSettings.LastUpdateTime < DateTime.Now.AddDays(-5) || forced)
			{
				Task.Run(() =>
				{
					try
					{
						Debug.Print("Checking for new version...");

						WebClient webClient = new WebClient();
						string result = webClient.DownloadString("https://jonashertzman.github.io/FileDiff/download/version.txt");

						Debug.Print($"Latest version found: {result}");

						return result;
					}
					catch (Exception exception)
					{
						Debug.Print($"Version check failed: {exception.Message}");
					}

					return null;

				}).ContinueWith(ProcessUpdate, TaskScheduler.FromCurrentSynchronizationContext());

				AppSettings.LastUpdateTime = DateTime.Now;
			}
		}

		private void ProcessUpdate(Task<string> task)
		{
			if (task.Result != null)
			{
				try
				{
					ViewModel.NewBuildAvailable = int.Parse(task.Result) > int.Parse(ViewModel.BuildNumber);
				}
				catch (Exception) { }
			}
		}

		#endregion

		#region Events

		private void Window_ContentRendered(object sender, EventArgs e)
		{
			if (Environment.GetCommandLineArgs().Length > 2)
			{
				ViewModel.LeftPath = Environment.GetCommandLineArgs()[1];
				ViewModel.RightPath = Environment.GetCommandLineArgs()[2];
				Compare();
			}

			renderComplete = true;
			UpdateColumnWidths(LeftColumns);
		}

		private void Window_Initialized(object sender, EventArgs e)
		{
			LoadSettings();
			CheckForUpdate();
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			SaveSettings();
		}

		private void ProgressTimer_Tick(object sender, EventArgs e)
		{
			ProgressPanel.Visibility = Visibility.Visible;
			progressTimer.Stop();
		}

		private void ToggleButtonIgnoreWhiteSpace_Click(object sender, RoutedEventArgs e)
		{
			if (File.Exists(ViewModel.LeftPath) && File.Exists(ViewModel.RightPath))
			{
				CompareFiles();
			}
		}

		private void ToggleButtonMasterDetail_Click(object sender, RoutedEventArgs e)
		{
			if (ViewModel.FileVissible)
			{
				CompareFiles();
			}
		}

		private void LeftSide_PreviewDragOver(object sender, DragEventArgs e)
		{
			e.Effects = DragDropEffects.Move;
			e.Handled = true;
		}

		private void LeftSide_DragDrop(object sender, DragEventArgs e)
		{
			var paths = (string[])e.Data.GetData(DataFormats.FileDrop);

			if (paths?.Length >= 2)
			{
				ViewModel.LeftPath = paths[0];
				ViewModel.RightPath = paths[1];
			}
			else if (paths?.Length >= 1)
			{
				ViewModel.LeftPath = paths[0];
			}
		}

		private void RightSide_PreviewDragOver(object sender, DragEventArgs e)
		{
			e.Effects = DragDropEffects.Move;
			e.Handled = true;
		}

		private void RightSide_DragDrop(object sender, DragEventArgs e)
		{
			var paths = (string[])e.Data.GetData(DataFormats.FileDrop);

			if (paths?.Length >= 2)
			{
				ViewModel.LeftPath = paths[0];
				ViewModel.RightPath = paths[1];
			}
			else if (paths?.Length >= 1)
			{
				ViewModel.RightPath = paths[0];
			}
		}

		private void FileDiff_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			int lines = SystemParameters.WheelScrollLines * e.Delta / 120;
			VerticalFileScrollbar.Value -= lines;
		}

		private void FolderDiff_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			int lines = SystemParameters.WheelScrollLines * e.Delta / 120;
			VerticalTreeScrollbar.Value -= lines;
		}

		private void LeftHorizontalScrollbar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			RightHorizontalScrollbar.Value = e.NewValue;
		}

		private void RightHorizontalScrollbar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			LeftHorizontalScrollbar.Value = e.NewValue;
		}

		private void LeftFolderHorizontalScrollbar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			LeftColumnScroll.ScrollToHorizontalOffset(e.NewValue);
			RightFolderHorizontalScrollbar.Value = e.NewValue;
		}

		private void RightFolderHorizontalScrollbar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			RightColumnScroll.ScrollToHorizontalOffset(e.NewValue);
			LeftFolderHorizontalScrollbar.Value = e.NewValue;
		}

		private void LeftColumnScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			if (!renderComplete)
				return;

			UpdateColumnWidths(LeftColumns);
		}

		private void RightColumnScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			if (!renderComplete)
				return;

			UpdateColumnWidths(RightColumns);
		}

		private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			ProcessSearchResult(activeDiff.Search(SearchBox.Text, MatchCase.IsChecked == true));
		}

		private void MatchCase_Checked(object sender, RoutedEventArgs e)
		{
			ProcessSearchResult(activeDiff.Search(SearchBox.Text, MatchCase.IsChecked == true));
		}

		private void RightDiff_GotFocus(object sender, RoutedEventArgs e)
		{
			activeDiff = RightDiff;
			ViewModel.FindPanelRight = true;
		}

		private void LeftDiff_GotFocus(object sender, RoutedEventArgs e)
		{
			activeDiff = LeftDiff;
			ViewModel.FindPanelRight = false;
		}

		private void LeftColumns_Resized(object sender, SizeChangedEventArgs e)
		{
			if (!renderComplete)
				return;

			UpdateColumnWidths(LeftColumns);
		}

		private void RightColumns_Resized(object sender, SizeChangedEventArgs e)
		{
			if (!renderComplete)
				return;

			UpdateColumnWidths(RightColumns);
		}

		private void LeftFolder_SelectionChanged(FileItem selectedItem)
		{
			rightSelection = selectedItem.CorrespondingItem.Path;

			RightFolder.SelectedFile = selectedItem.CorrespondingItem;

			CompareFiles();
		}

		private void RightFolder_SelectionChanged(FileItem selectedItem)
		{
			leftSelection = selectedItem.CorrespondingItem.Path;

			LeftFolder.SelectedFile = selectedItem.CorrespondingItem;

			CompareFiles();
		}

		private void Hyperlink_OpenHomepage(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			Process.Start(new ProcessStartInfo(AppSettings.HOMEPAGE));
			e.Handled = true;
		}

		#endregion

		#region Commands

		private void CommandExit_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.Close();
		}

		private void CommnadOptions_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			// Store existing settings data in case the changes are canceled.
			var oldCheckForUpdates = ViewModel.CheckForUpdates;
			var oldFont = ViewModel.Font;
			var oldFontSize = ViewModel.FontSize;
			var oldTabSize = ViewModel.TabSize;
			var oldDeletedBackground = ViewModel.DeletedBackground;
			var oldDeletedForeground = ViewModel.DeletedForeground;
			var oldFullMatchBackground = ViewModel.FullMatchBackground;
			var oldFullMatchForeground = ViewModel.FullMatchForeground;
			var oldIgnoredBackground = ViewModel.IgnoredBackground;
			var oldIgnoredForeground = ViewModel.IgnoredForeground;
			var oldNewBackground = ViewModel.NewBackground;
			var oldNewForeground = ViewModel.NewForeground;
			var oldPartialMatchBackground = ViewModel.PartialMatchBackground;
			var oldPartialMatchForeground = ViewModel.PartialMatchForeground;
			var oldSelectionBackground = ViewModel.SelectionBackground;
			var oldIgnoredFiles = new ObservableCollection<TextAttribute>(ViewModel.IgnoredFiles);
			var oldIgnoredFolders = new ObservableCollection<TextAttribute>(ViewModel.IgnoredFolders);

			OptionsWindow optionsWindow = new OptionsWindow() { DataContext = ViewModel, Owner = this };
			optionsWindow.ShowDialog();

			if (optionsWindow.DialogResult == true)
			{
				SaveSettings();
			}
			else
			{
				// Options window was canceled, revert to old settings.
				ViewModel.CheckForUpdates = oldCheckForUpdates;
				ViewModel.Font = oldFont;
				ViewModel.FontSize = oldFontSize;
				ViewModel.TabSize = oldTabSize;
				ViewModel.DeletedBackground = oldDeletedBackground;
				ViewModel.DeletedForeground = oldDeletedForeground;
				ViewModel.FullMatchBackground = oldFullMatchBackground;
				ViewModel.FullMatchForeground = oldFullMatchForeground;
				ViewModel.IgnoredBackground = oldIgnoredBackground;
				ViewModel.IgnoredForeground = oldIgnoredForeground;
				ViewModel.NewBackground = oldNewBackground;
				ViewModel.NewForeground = oldNewForeground;
				ViewModel.PartialMatchBackground = oldPartialMatchBackground;
				ViewModel.PartialMatchForeground = oldPartialMatchForeground;
				ViewModel.SelectionBackground = oldSelectionBackground;
				ViewModel.IgnoredFiles = new ObservableCollection<TextAttribute>(oldIgnoredFiles);
				ViewModel.IgnoredFolders = new ObservableCollection<TextAttribute>(oldIgnoredFolders);
			}
		}

		private void CommandAbout_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			CheckForUpdate(true);

			AboutWindow aboutWindow = new AboutWindow() { Owner = this, DataContext = ViewModel };
			aboutWindow.ShowDialog();
		}

		private void CommandCompare_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			// Make sure view model is updated with both paths before we compare
			TextBoxRightPath.Focus();
			TextBoxLeftPath.Focus();

			Compare();
		}

		private void CommandCancelCompare_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			BackgroundCompare.Cancel();
		}

		private void CommandSaveLeftFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			string leftPath = ViewModel.Mode == CompareMode.File ? ViewModel.LeftPath : LeftFolder.SelectedFile.Path;

			if (File.Exists(leftPath) && ViewModel.LeftFileDirty)
			{
				try
				{
					using (StreamWriter sw = new StreamWriter(leftPath, false, ViewModel.LeftFileEncoding.GetEncoding))
					{
						sw.NewLine = ViewModel.LeftFileEncoding.GetNewLineString;
						foreach (Line l in ViewModel.LeftFile)
						{
							if (l.Type != TextState.Filler)
							{
								sw.WriteLine(l.Text);
							}
						}
					}
					ViewModel.LeftFileDirty = false;
					ViewModel.LeftFileEdited = false;
				}
				catch (Exception exception)
				{
					MessageBox.Show(exception.Message, "Error Saving File", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		private void CommandSaveLeftFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.LeftFileDirty;
		}

		private void CommandSaveRightFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			string rightPath = ViewModel.Mode == CompareMode.File ? ViewModel.RightPath : RightFolder.SelectedFile.Path;

			if (File.Exists(rightPath) && ViewModel.RightFileDirty)
			{
				try
				{
					using (StreamWriter sw = new StreamWriter(rightPath, false, ViewModel.RightFileEncoding.GetEncoding))
					{
						sw.NewLine = ViewModel.RightFileEncoding.GetNewLineString;
						foreach (Line l in ViewModel.RightFile)
						{
							if (l.Type != TextState.Filler)
							{
								sw.WriteLine(l.Text);
							}
						}
					}
					ViewModel.RightFileDirty = false;
					ViewModel.RightFileEdited = false;
				}
				catch (Exception exception)
				{
					MessageBox.Show(exception.Message, "Error Saving File", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		private void CommandSaveRightFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.RightFileDirty;
		}

		private void CommandEdit_Executed(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void CommandEdit_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = NoManualEdit;
		}

		private void CommandBrowseLeft_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			BrowseFolderWindow browseFolderWindow = new BrowseFolderWindow() { DataContext = ViewModel, Owner = this, SelectedPath = ViewModel.LeftPath };
			browseFolderWindow.ShowDialog();

			if (browseFolderWindow.DialogResult == true)
			{
				ViewModel.LeftPath = browseFolderWindow.SelectedPath;
			}
		}

		private void CommandBrowseRight_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			BrowseFolderWindow browseFolderWindow = new BrowseFolderWindow() { DataContext = ViewModel, Owner = this, SelectedPath = ViewModel.RightPath };
			browseFolderWindow.ShowDialog();

			if (browseFolderWindow.DialogResult == true)
			{
				ViewModel.RightPath = browseFolderWindow.SelectedPath;
			}
		}

		private void CommandPreviousDiff_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			MoveToPrevoiusDiff();
		}

		private void CommandPreviousDiff_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.FileVissible && firstDiff < ViewModel.CurrentDiff && NoManualEdit;
		}

		private void CommandNextDiff_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			MoveToNextDiff();
		}

		private void CommandNextDiff_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.FileVissible && lastDiff > ViewModel.CurrentDiff && NoManualEdit;
		}

		private void CommandCurrentDiff_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			MoveToDiffLine(ViewModel.CurrentDiff);
		}

		private void CommandCurrentDiff_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.FileVissible && ViewModel.CurrentDiff != -1 && ViewModel.CurrentDiffLength > 0 && NoManualEdit;
		}

		private void CommandFirstDiff_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			MoveToFirstDiff();
		}

		private void CommandFirstDiff_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.FileVissible && firstDiff < ViewModel.CurrentDiff && firstDiff != -1 && NoManualEdit;
		}

		private void CommandLastDiff_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			MoveToLastDiff();
		}

		private void CommandLastDiff_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.FileVissible && lastDiff > ViewModel.CurrentDiff && NoManualEdit;
		}

		private void CommandNextFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (folderDiffItems.Count > 0)
			{
				int currentIndex = folderDiffItems.IndexOf(RightFolder.SelectedFile);

				if (currentIndex == -1)
				{
					RightFolder.Select(folderDiffItems[0]);
				}
				else if (currentIndex < folderDiffItems.Count - 1)
				{
					RightFolder.Select(folderDiffItems[currentIndex + 1]);
				}
			}
		}

		private void CommandNextFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.FolderVissible && folderDiffItems.IndexOf(RightFolder.SelectedFile) < folderDiffItems.Count - 1; ;
		}

		private void CommandPreviousFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (folderDiffItems.Count > 0)
			{
				int currentIndex = folderDiffItems.IndexOf(RightFolder.SelectedFile);

				if (currentIndex == -1)
				{
					RightFolder.Select(folderDiffItems[folderDiffItems.Count - 1]);
				}
				else if (currentIndex > 0)
				{
					RightFolder.Select(folderDiffItems[currentIndex - 1]);
				}
			}
		}

		private void CommandPreviousFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.FolderVissible && folderDiffItems.IndexOf(RightFolder.SelectedFile) > 0;
		}

		private void CommandFind_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			SearchPanel.Visibility = Visibility.Visible;
			SearchBox.Focus();
		}

		private void CommandFindNext_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (SearchPanel.Visibility != Visibility.Visible)
			{
				SearchPanel.Visibility = Visibility.Visible;
				SearchBox.Focus();
			}
			else
			{
				ProcessSearchResult(activeDiff.SearchNext(SearchBox.Text, MatchCase.IsChecked == true));
			}
		}

		private void CommandFindNext_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (SearchBox.Text != "" && activeDiff.Lines.Count > 0) || SearchPanel.Visibility != Visibility.Visible;
		}

		private void CommandFindPrevious_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			ProcessSearchResult(activeDiff.SearchPrevious(SearchBox.Text, MatchCase.IsChecked == true));
		}

		private void CommandFindPrevious_CanExecute_1(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = SearchBox.Text != "" && activeDiff.Lines.Count > 0;
		}

		private void CommandCloseFind_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			SearchPanel.Visibility = Visibility.Collapsed;
		}

		private void CommandSwap_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			string temp = ViewModel.LeftPath;
			ViewModel.LeftPath = ViewModel.RightPath;
			ViewModel.RightPath = temp;

			Compare();
		}

		private void CommandUp_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			try
			{
				ViewModel.LeftPath = Path.GetDirectoryName(Path.GetFullPath(ViewModel.LeftPath));
				ViewModel.RightPath = Path.GetDirectoryName(Path.GetFullPath(ViewModel.RightPath));

				Compare();
			}
			catch (ArgumentException) { }
		}

		private void CommandOpenContainingFolder_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			string args = $"/Select, {Path.GetFullPath(GetFocusedPath(e.OriginalSource))}";
			ProcessStartInfo pfi = new ProcessStartInfo("Explorer.exe", args);
			Process.Start(pfi);
		}

		private void CommandOpenContainingFolder_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = GetFocusedPath(e.OriginalSource) != "";
		}

		private void CommandCopyPathToClipboard_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			Clipboard.SetText(Path.GetFullPath(GetFocusedPath(e.OriginalSource)));
		}

		private void CommandCopyPathToClipboard_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = GetFocusedPath(e.OriginalSource) != "";
		}

		private void CommandCopyLeftDiff_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			for (int i = ViewModel.CurrentDiff; i < ViewModel.CurrentDiff + ViewModel.CurrentDiffLength; i++)
			{
				ViewModel.RightFile[i].Text = ViewModel.LeftFile[i].Text;
				ViewModel.RightFile[i].Type = ViewModel.LeftFile[i].Type;
			}

			ViewModel.RightFileDirty = true;
			ViewModel.CurrentDiffLength = 0;
			ViewModel.UpdateTrigger++;

			UpdateNavigationButtons();
		}

		private void CommandCopyLeftDiff_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.CurrentDiff != -1 && NoManualEdit;
		}

		private void CommandCopyRightDiff_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			for (int i = ViewModel.CurrentDiff; i < ViewModel.CurrentDiff + ViewModel.CurrentDiffLength; i++)
			{
				ViewModel.LeftFile[i].Text = ViewModel.RightFile[i].Text;
				ViewModel.LeftFile[i].Type = ViewModel.RightFile[i].Type;
			}

			ViewModel.LeftFileDirty = true;
			ViewModel.CurrentDiffLength = 0;
			ViewModel.UpdateTrigger++;

			UpdateNavigationButtons();
		}

		private void CommandCopyRightDiff_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ViewModel.CurrentDiff != -1 && NoManualEdit;
		}

		#endregion

	}
}
