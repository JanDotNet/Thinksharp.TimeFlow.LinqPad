using LINQPad;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using System;
using System.Data;
using System.Linq;
using System.Windows.Controls;

namespace Thinksharp.TimeFlow
{
    public static class LinqPadExtensions
	{
		public static void PlotLine(this TimeFrame tf, Action<PlotModel> configure)
		{
			var model = new PlotModel();
			foreach (var ts in tf)
			{
				var series = new LineSeries();
				foreach (var tp in ts.Value)
				{
					var x = DateTimeAxis.ToDouble(tp.Key.DateTime);
					var y = (double)(tp.Value ?? 0M);
					series.Points.Add(new DataPoint(x, y));
				}

				series.Title = ts.Key;
				model.Series.Add(series);
			}

			model.Legends.Add(new OxyPlot.Legends.Legend()
			{
				LegendTitle = "Time Series",
				LegendPosition = LegendPosition.RightMiddle,
				LegendPlacement = LegendPlacement.Outside
			});
			model.Background = OxyColors.White;
			model.Axes.Add(new DateTimeAxis
			{
				Position = AxisPosition.Bottom,
				Minimum = DateTimeAxis.ToDouble(tf.Start.DateTime),
				Maximum = DateTimeAxis.ToDouble(tf.End.DateTime),
				StringFormat = GetDateFormat(tf),
			});

			if (configure != null)
			{
				configure(model);
			}

			var plotView = new PlotView();
			plotView.Model = model;
			PanelManager.DisplayControl(plotView, "Time Series Chart");
		}

		public static void PlotLine(this TimeFrame tf, string title = null)
		{
			tf.PlotLine(m => m.Title = title);
		}

		public static void PlotLine(this TimeSeries ts, Action<PlotModel> configure)
		{
			var tf = new TimeFrame();
			tf["ts"] = ts;

			tf.PlotLine(configure);
		}

		public static void PlotLine(this TimeSeries ts, string title = null)
		{
			ts.PlotLine(m =>
			{
				m.Title = title;
				m.Legends.Clear();
			});
		}

		public static string GetDateFormat(TimeFrame tf)
		{
			var dateDiff = tf.End - tf.Start;

			// same day
			if (tf.Start.Date == tf.End.Date)
			{
				return tf.Frequency == Period.Seconds ? "HH:mm:ss" :
					   tf.Frequency == Period.Milliseconds ? "HH:mm:ss.fff" :
					   "HH:mm";
			}

			var format = tf.End.Year == tf.Start.Year ? "d.M." : "yyyy-MM-dd";
			if (tf.Frequency < Period.Day)
			{
				format += " HH:mm";
			}

			return format;
		}

		public static void RenderTable(this TimeFrame tf)
		{
			var timeFormatter = GetTimeFormatter(tf);
			var dt = new DataTable();
			timeFormatter.AddColumns(dt);
			var firstTimeSeriesIdx = dt.Columns.Count;
			foreach (var ts in tf)
			{
				dt.Columns.Add(ts.Key, typeof(decimal)).AllowDBNull = true;
			}

			foreach (var tp in tf.EnumerateTimePoints())
			{
				var colIdx = firstTimeSeriesIdx;
				var row = dt.NewRow();
				timeFormatter.AddValues(row, tp, tf.Frequency);
				foreach (var ts in tf.EnumerateTimeSeries())
				{
					if (ts[tp].HasValue)
					{
						row[colIdx++] = ts[tp].Value;
					}
					else
					{
						row[colIdx++] = DBNull.Value;
					}
				}

				dt.Rows.Add(row);
			}

			var grid = new System.Windows.Controls.DataGrid
			{
				ItemsSource = dt.DefaultView,
				IsReadOnly = true,
				SelectionMode = DataGridSelectionMode.Extended,
				SelectionUnit = DataGridSelectionUnit.Cell
			};
			PanelManager.DisplayWpfElement(grid, "Time Series Data");
		}

		public static void RenderTable(this TimeSeries ts)
		{
			var tf = new TimeFrame();
			tf["Value"] = ts;

			RenderTable(tf);
		}

		private static bool HasTimeComponent(TimeFrame tf)
		{
			foreach (var tp in tf.EnumerateTimePoints().Take(3))
			{
				if (tp.Hour != 0
				|| tp.Minute != 0
				|| tp.Second != 0
				|| tp.Millisecond != 0)
				{
					return true;
				}
			}

			return false;
		}

		private static bool HasMillisecondComponent(TimeFrame tf)
		{
			foreach (var tp in tf.EnumerateTimePoints().Take(3))
			{
				if (tp.Millisecond != 0)
				{
					return true;
				}
			}

			return false;
		}

		private static bool HasSecondComponent(TimeFrame tf)
		{
			foreach (var tp in tf.EnumerateTimePoints().Take(3))
			{
				if (tp.Second != 0
					|| tp.Millisecond != 0)
				{
					return true;
				}
			}

			return false;
		}

		public abstract class TimeColumnFormatter
		{
			public abstract void AddColumns(DataTable table);

			public abstract void AddValues(DataRow row, DateTimeOffset start, Period frequency);
		}

		public class DefaultTimeColumnFormatter : TimeColumnFormatter
		{
			private readonly string format;

			public DefaultTimeColumnFormatter()
			: this("yyyy-MM-dd HH:mm:ss.fff")
			{
			}

			public DefaultTimeColumnFormatter(string format)
			{
				this.format = format;
			}

			public override void AddColumns(DataTable table)
			{
				table.Columns.Add("Start", typeof(string));
				table.Columns.Add("End", typeof(string));
			}

			public override void AddValues(DataRow row, DateTimeOffset start, Period frequency)
			{
				var end = frequency.AddPeriod(start);
				row["Start"] = start.ToString(format);
				row["End"] = end.ToString(format);
			}
		}

		public class SingleDayTimeColumnFormatter : TimeColumnFormatter
		{
			public override void AddColumns(DataTable table)
			{
				table.Columns.Add("Date", typeof(string));
				table.Columns.Add("Start", typeof(string));
				table.Columns.Add("End", typeof(string));
			}

			public override void AddValues(DataRow row, DateTimeOffset start, Period frequency)
			{
				var end = frequency.AddPeriod(start);
				row["Date"] = start.Date.ToString("yyyy-MM-dd");
				row["Start"] = start.ToString("HH:mm");
				row["End"] = end.ToString("HH:mm");
			}
		}

		public class DayTimeColumnFormatter : TimeColumnFormatter
		{
			public override void AddColumns(DataTable table)
			{
				table.Columns.Add("Start", typeof(string));
				table.Columns.Add("End", typeof(string));
			}

			public override void AddValues(DataRow row, DateTimeOffset start, Period frequency)
			{
				var end = frequency.AddPeriod(start);
				row["Start"] = start.ToString("yyyy-MM-dd");
				row["End"] = end.ToString("yyyy-MM-dd");
			}
		}

		private static TimeColumnFormatter GetTimeFormatter(TimeFrame tf)
		{
			if (tf.Frequency < Period.Day)
			{
				return new SingleDayTimeColumnFormatter();
			}

			if (tf.Frequency >= Period.Day && !HasTimeComponent(tf))
			{
				return new DayTimeColumnFormatter();
			}

			if (!HasSecondComponent(tf))
			{
				return new DefaultTimeColumnFormatter("yyyy-MM-dd HH:mm");
			}

			if (!HasMillisecondComponent(tf))
			{
				return new DefaultTimeColumnFormatter("yyyy-MM-dd HH:mm:ss");
			}

			return new DefaultTimeColumnFormatter("yyyy-MM-dd HH:mm:ss.fff");
		}
	}
}