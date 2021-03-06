﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileDiff
{
	public class DiffMapControl : Control
	{

		#region Members

		private double dpiScale = 0;

		#endregion

		#region Constructors

		static DiffMapControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(DiffMapControl), new FrameworkPropertyMetadata(typeof(DiffMapControl)));
		}

		public DiffMapControl()
		{
			this.ClipToBounds = true;
		}

		#endregion

		#region Overrides

		protected override void OnRender(DrawingContext drawingContext)
		{
			Debug.Print("DiffMap OnRender");

			// Fill background
			drawingContext.DrawRectangle(SystemColors.ControlBrush, null, new Rect(0, 0, this.ActualWidth, this.ActualHeight));

			if (Lines.Count == 0)
				return;

			Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
			dpiScale = 1 / m.M11;

			double scrollableHeight = ActualHeight - (2 * RoundToWholePixels(SystemParameters.VerticalScrollBarButtonHeight));
			double lineHeight = scrollableHeight / Lines.Count;
			double lastHeight = -1;

			SolidColorBrush partialMatchBrush = BlendColors(AppSettings.PartialMatchBackground, AppSettings.PartialMatchForeground, .7);
			SolidColorBrush deletedBrush = BlendColors(AppSettings.DeletedBackground, AppSettings.DeletedForeground, .7);
			SolidColorBrush newhBrush = BlendColors(AppSettings.NewBackground, AppSettings.NewForeground, .7);

			SolidColorBrush lineBrush;

			for (int i = 0; i < Lines.Count; i++)
			{
				Line line = Lines[i];

				switch (line.Type)
				{
					case TextState.PartialMatch:
						lineBrush = partialMatchBrush;
						break;

					case TextState.New:
						lineBrush = newhBrush;
						break;

					case TextState.Filler:
						lineBrush = deletedBrush;
						break;

					default:
						continue;
				}

				int count = 1;

				while (i + count < Lines.Count && line.Type == Lines[i + count].Type)
				{
					count++;
				}

				Rect rect = new Rect(RoundToWholePixels(1), (Math.Floor((i * lineHeight + SystemParameters.VerticalScrollBarButtonHeight) / dpiScale) * dpiScale), ActualWidth - RoundToWholePixels(2), Math.Ceiling(Math.Max((lineHeight * count), 1) / dpiScale) * dpiScale);

				if (rect.Bottom > lastHeight)
				{
					drawingContext.DrawRectangle(lineBrush, null, rect);

					lastHeight = rect.Bottom;
				}

				i += count - 1;
			}
		}

		#endregion

		#region Dependency Properties

		public static readonly DependencyProperty LinesProperty = DependencyProperty.Register("Lines", typeof(ObservableCollection<Line>), typeof(DiffMapControl), new FrameworkPropertyMetadata(new ObservableCollection<Line>(), FrameworkPropertyMetadataOptions.AffectsRender));

		public ObservableCollection<Line> Lines
		{
			get { return (ObservableCollection<Line>)GetValue(LinesProperty); }
			set { SetValue(LinesProperty, value); }
		}


		public static readonly DependencyProperty UpdateTriggerProperty = DependencyProperty.Register("UpdateTrigger", typeof(int), typeof(DiffMapControl), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

		public int UpdateTrigger
		{
			get { return (int)GetValue(UpdateTriggerProperty); }
			set { SetValue(UpdateTriggerProperty, value); }
		}

		#endregion

		#region Methods

		private SolidColorBrush BlendColors(SolidColorBrush color1, SolidColorBrush color2, double blendFactor)
		{
			byte r = (byte)((color1.Color.R * blendFactor) + color2.Color.R * (1 - blendFactor));
			byte g = (byte)((color1.Color.G * blendFactor) + color2.Color.G * (1 - blendFactor));
			byte b = (byte)((color1.Color.B * blendFactor) + color2.Color.B * (1 - blendFactor));
			return new SolidColorBrush(Color.FromRgb(r, g, b));
		}

		private double RoundToWholePixels(double x)
		{
			return Math.Round(x / dpiScale) * dpiScale;
		}

		#endregion

	}
}
