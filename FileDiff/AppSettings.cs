﻿using System;
using System.IO;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Xml;

namespace FileDiff
{
	public static class AppSettings
	{

		const string SETTINGS_DIRECTORY = "FileDiff";
		const string SETTINGS_FILE_NAME = "Settings.xml";

		#region Defaults

		internal static Color DefaultFullMatchForeground { get; } = Colors.Black;
		internal static Color DefaultFullMatchBackground { get; } = Colors.White;

		internal static Color DefaultPartialMatchForeground { get; } = Colors.Black;
		internal static Color DefaultPartialMatchBackground { get; } = Color.FromRgb(220, 220, 255);

		internal static Color DefaultDeletedForeground { get; } = Color.FromRgb(200, 0, 0);
		internal static Color DefaultDeletedBackground { get; } = Color.FromRgb(255, 220, 220);

		internal static Color DefaultNewForeground { get; } = Color.FromRgb(0, 120, 0);
		internal static Color DefaultNewBackground { get; } = Color.FromRgb(220, 255, 220);

		internal static string DefaultFont { get; } = "Courier New";
		internal static int DefaultFontSize { get; } = 12;
		internal static int DefaultTabSize { get; } = 2;

		#endregion

		#region Properies

		public static SettingsData Settings { get; private set; } = new SettingsData();

		public static SolidColorBrush fullMatchForegroundBrush { get; set; }
		public static SolidColorBrush fullMatchBackgroundBrush { get; set; }

		public static SolidColorBrush partialMatchForegroundBrush { get; set; }
		public static SolidColorBrush partialMatchBackgroundBrush { get; set; }

		public static SolidColorBrush deletedForegroundBrush { get; set; }
		public static SolidColorBrush deletedBackgroundBrush { get; set; }

		public static SolidColorBrush newForegroundBrush { get; set; }
		public static SolidColorBrush newBackgrounBrush { get; set; }

		#endregion

		#region Methods

		internal static void ReadSettingsFromDisk()
		{
			string settingsPath = Path.Combine(Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), SETTINGS_DIRECTORY), SETTINGS_FILE_NAME);
			DataContractSerializer xmlSerializer = new DataContractSerializer(typeof(SettingsData));

			if (File.Exists(settingsPath))
			{
				using (var xmlReader = XmlReader.Create(settingsPath))
				{
					try
					{
						Settings = (SettingsData)xmlSerializer.ReadObject(xmlReader);
					}
					catch (Exception e)
					{
						MessageBox.Show(e.Message, "Error Parsing XML", MessageBoxButton.OK, MessageBoxImage.Error);
					}
				}
			}

			if (Settings == null)
			{
				Settings = new SettingsData();
			}

			UpdateBrushCache();
		}

		internal static void WriteSettingsToDisk()
		{
			try
			{
				string settingsPath = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), SETTINGS_DIRECTORY);

				DataContractSerializer xmlSerializer = new DataContractSerializer(typeof(SettingsData));
				var xmlWriterSettings = new XmlWriterSettings { Indent = true, IndentChars = " " };

				if (!Directory.Exists(settingsPath))
				{
					Directory.CreateDirectory(settingsPath);
				}

				using (var xmlWriter = XmlWriter.Create(Path.Combine(settingsPath, SETTINGS_FILE_NAME), xmlWriterSettings))
				{
					xmlSerializer.WriteObject(xmlWriter, Settings);
				}
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message);
			}
		}

		private static void UpdateBrushCache()
		{
			fullMatchForegroundBrush = new SolidColorBrush(Settings.FullMatchForeground);
			fullMatchBackgroundBrush = new SolidColorBrush(Settings.FullMatchBackground);

			partialMatchForegroundBrush = new SolidColorBrush(Settings.PartialMatchForeground);
			partialMatchBackgroundBrush = new SolidColorBrush(Settings.PartialMatchBackground);

			deletedForegroundBrush = new SolidColorBrush(Settings.DeletedForeground);
			deletedBackgroundBrush = new SolidColorBrush(Settings.DeletedBackground);

			newForegroundBrush = new SolidColorBrush(Settings.NewForeground);
			newBackgrounBrush = new SolidColorBrush(Settings.NewBackground);
		}

		#endregion

	}
}
