﻿using System.Text;

namespace FileDiff
{
	public class FileEncoding
	{

		#region Constructor

		public FileEncoding(Encoding type, bool bom, NewlineMode newline)
		{
			this.Type = type;
			this.Bom = bom;
			this.Newline = newline;
		}

		#endregion

		#region Overrides

		public override string ToString()
		{
			string name = "";

			if (Type == Encoding.UTF7)
			{
				name = "UTF-7";
			}
			else if (Type == Encoding.UTF8)
			{
				name = "UTF-8";
			}
			else if (Type == Encoding.Unicode)
			{
				name = "UTF-16 LE";
			}
			else if (Type == Encoding.BigEndianUnicode)
			{
				name = "UTF-16 BE";
			}
			else if (Type == Encoding.UTF32)
			{
				name = "UTF-32";
			}
			else if (Type == Encoding.Default)
			{
				name = Type.WebName;
			}

			if (Bom)
			{
				name += " BOM";
			}

			name += $" ({Newline.ToString()})";

			return name;
		}

		#endregion

		#region Properties

		public Encoding Type { get; set; }

		public bool Bom { get; set; }

		public NewlineMode Newline { get; set; }

		public Encoding GetEncoding
		{
			get
			{
				if (Type == Encoding.UTF7)
				{
					return new UTF7Encoding();
				}
				else if (Type == Encoding.UTF8)
				{
					return new UTF8Encoding(Bom);
				}
				else if (Type == Encoding.Unicode)
				{
					return new UnicodeEncoding(false, Bom);
				}
				else if (Type == Encoding.BigEndianUnicode)
				{
					return new UnicodeEncoding(true, Bom);
				}
				else if (Type == Encoding.UTF32)
				{
					return new UTF32Encoding(false, Bom);
				}

				return Encoding.Default;
			}
		}

		public string GetNewLineString
		{
			get
			{
				if (Newline == NewlineMode.Mac)
				{
					return "\r";
				}
				else if (Newline == NewlineMode.Unix)
				{
					return "\n";
				}
				return "\r\n";
			}
		}

		#endregion

	}
}
