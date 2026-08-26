using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class FolderLinkSuggestionDialog : Window
{
	private sealed record Suggestion(string Path, double Score)
	{
		public override string ToString()
		{
			return $"{Score:P0}  {Path}";
		}
	}

	private readonly ComboBox _suggestions = new ComboBox
	{
		MinWidth = 520.0
	};

	private readonly TextBox _manual = new TextBox();

	public string? SelectedFolder { get; private set; }

	public FolderLinkSuggestionDialog(string productName, string modelNumber, string searchRoot)
	{
		FolderLinkSuggestionDialog folderLinkSuggestionDialog = this;
		base.Title = "製品と素材フォルダの紐づけ － MISE";
		base.Width = 720.0;
		base.Height = 360.0;
		base.MinWidth = 560.0;
		base.MinHeight = 300.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 720.0, 360.0, 560.0, 300.0);
		List<Suggestion> list = (from x in ScanFolders(searchRoot)
			select new Suggestion(x, Similarity(productName + modelNumber, Path.GetFileName(x))) into x
			orderby x.Score descending, x.Path
			select x).Take(12).ToList();
		_suggestions.ItemsSource = list;
		if (list.Count > 0)
		{
			_suggestions.SelectedIndex = 0;
		}
		_manual.Text = list.FirstOrDefault()?.Path ?? searchRoot;
		_suggestions.SelectionChanged += delegate
		{
			if (folderLinkSuggestionDialog._suggestions.SelectedItem is Suggestion suggestion)
			{
				folderLinkSuggestionDialog._manual.Text = suggestion.Path;
			}
		};
		base.Content = Build(productName, modelNumber);
	}

	private UIElement Build(string name, string model)
	{
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(18.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "キャンセル",
			MinWidth = 90.0
		};
		button.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button2 = new Button
		{
			Content = "紐づける",
			MinWidth = 100.0,
			Style = (TryFindResource("PrimaryButton") as Style)
		};
		button2.Click += delegate
		{
			if (!Directory.Exists(_manual.Text))
			{
				MessageBox.Show("存在するフォルダを選択してください。", "フォルダ紐づけ");
			}
			else
			{
				SelectedFolder = _manual.Text;
				base.DialogResult = true;
			}
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		obj.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = "素材フォルダ候補",
					FontSize = 22.0,
					FontWeight = FontWeights.Bold
				},
				(UIElement)new TextBlock
				{
					Text = name + " " + model + " とフォルダ名の類似度から候補を並べました。",
					Margin = new Thickness(0.0, 3.0, 0.0, 14.0)
				},
				(UIElement)new TextBlock
				{
					Text = "候補"
				},
				(UIElement)_suggestions,
				(UIElement)new TextBlock
				{
					Text = "選択フォルダ（手動変更可）",
					Margin = new Thickness(0.0, 12.0, 0.0, 4.0)
				}
			}
		};
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition(),
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			},
			Children = { (UIElement)_manual }
		};
		Button button3 = new Button
		{
			Content = "参照"
		};
		button3.Click += delegate
		{
			OpenFolderDialog openFolderDialog = new OpenFolderDialog
			{
				Title = "素材フォルダを選択",
				InitialDirectory = (Directory.Exists(_manual.Text) ? _manual.Text : null)
			};
			if (openFolderDialog.ShowDialog(this) == true)
			{
				_manual.Text = openFolderDialog.FolderName;
			}
		};
		Grid.SetColumn(button3, 1);
		grid.Children.Add(button3);
		stackPanel2.Children.Add(grid);
		obj.Children.Add(stackPanel2);
		return obj;
	}

	public static IEnumerable<string> ScanFolders(string root)
	{
		Queue<(string path, int depth)> queue = new Queue<(string, int)>();
		queue.Enqueue((root, 0));
		int count = 0;
		while (queue.Count > 0 && count < 2000)
		{
			var (path, depth) = queue.Dequeue();
			if (depth > 0)
			{
				yield return path;
				count++;
			}
			if (depth >= 4)
			{
				continue;
			}
			IEnumerable<string> enumerable;
			try
			{
				enumerable = Directory.EnumerateDirectories(path).Take(500).ToList();
			}
			catch
			{
				continue;
			}
			foreach (string item in enumerable)
			{
				queue.Enqueue((item, depth + 1));
			}
		}
	}

	public static double Similarity(string a, string b)
	{
		string x = N(a);
		string y = N(b);
		if (x.Length == 0 || y.Length == 0)
		{
			return 0.0;
		}
		if (x.Contains(y) || y.Contains(x))
		{
			return 0.9;
		}
		HashSet<string> hashSet = (from i in Enumerable.Range(0, Math.Max(1, x.Length - 1))
			select x.Substring(i, Math.Min(2, x.Length - i))).ToHashSet();
		HashSet<string> hashSet2 = (from i in Enumerable.Range(0, Math.Max(1, y.Length - 1))
			select y.Substring(i, Math.Min(2, y.Length - i))).ToHashSet();
		return 2.0 * (double)hashSet.Intersect(hashSet2).Count() / (double)Math.Max(1, hashSet.Count + hashSet2.Count);
		static string N(string value)
		{
			StringBuilder stringBuilder = new StringBuilder();
			string text = value.ToUpperInvariant();
			foreach (char c in text)
			{
				if (char.IsLetterOrDigit(c))
				{
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		}
	}
}
