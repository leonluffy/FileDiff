﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace FileDiff
{
	public class Line : INotifyPropertyChanged
	{

		#region Members

		private int hash;
		private int hashNoWhitespace;

		#endregion

		#region Constructor

		public Line()
		{

		}

		#endregion

		#region Overrides

		public override string ToString()
		{
			return $"{LineIndex}  {Text}  {MatchingLineIndex}";
		}

		public override int GetHashCode()
		{
			return AppSettings.Settings.IgnoreWhiteSpace ? hashNoWhitespace : hash;
		}

		#endregion

		#region Properties

		private string text;
		public string Text
		{
			get { return text; }
			set
			{
				text = value;
				TrimmedText = value.Trim();

				hash = value.GetHashCode();
				hashNoWhitespace = Regex.Replace(value, @"\s+", "").GetHashCode();

				TextSegments.Clear();
				TextSegments.Add(new TextSegment(value, Type));

				OnPropertyChanged(nameof(Text));
			}
		}

		private string trimmedText;
		public string TrimmedText
		{
			get { return AppSettings.Settings.IgnoreWhiteSpace ? trimmedText : text; }
			private set { trimmedText = value; }
		}

		private ObservableCollection<TextSegment> textSegments = new ObservableCollection<TextSegment>();
		public ObservableCollection<TextSegment> TextSegments
		{
			get { return textSegments; }
			set { textSegments = value; OnPropertyChanged(nameof(TextSegments)); }
		}

		public List<object> Characters
		{
			get
			{
				List<object> list = new List<object>();

				foreach (char c in text.ToCharArray())
				{
					list.Add(c);
				}
				return list;
			}
		}

		public List<object> TrimmedCharacters
		{
			get
			{
				List<object> list = new List<object>();

				foreach (char c in TrimmedText.ToCharArray())
				{
					list.Add(c);
				}
				return list;
			}
		}

		private int? lineindex;
		public int? LineIndex
		{
			get { return lineindex; }
			set { lineindex = value; OnPropertyChanged(nameof(LineIndex)); }
		}

		private TextState type;
		public TextState Type
		{
			get { return type; }
			set
			{
				type = value;
				TextSegments.Clear();
				TextSegments.Add(new TextSegment(Text, value));
				OnPropertyChanged(nameof(Type));
			}
		}

		public int? MatchingLineIndex { get; set; }

		#endregion

		#region INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		public void OnPropertyChanged(string name)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		#endregion

	}
}
