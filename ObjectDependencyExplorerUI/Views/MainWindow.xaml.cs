using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace ObjectDependencyExplorerUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private bool _autoScroll = true;

		public MainWindow()
		{
			InitializeComponent();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			(DataContext as MainViewModel)?.Dispose();
		}

		// AutoScroll
		private void LogConsole_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			// User scroll event : set or unset auto-scroll mode
			if (e.ExtentHeightChange == 0)
			{   // Content unchanged : user scroll event
				if (LogConsole.VerticalOffset == LogConsole.ScrollableHeight)
				{   // Scroll bar is in bottom
					// Set auto-scroll mode
					_autoScroll = true;
				}
				else
				{   // Scroll bar isn't in bottom
					// Unset auto-scroll mode
					_autoScroll = false;
				}
			}

			// Content scroll event : auto-scroll eventually
			if (_autoScroll && e.ExtentHeightChange != 0)
			{   // Content changed and auto-scroll mode set
				// Autoscroll
				LogConsole.ScrollToVerticalOffset(LogConsole.ExtentHeight);
			}
		}

		private void wndMainWindow_Loaded(object sender, RoutedEventArgs e)
		{
			// При старте сами сразу открываем диалог
			(DataContext as MainViewModel).TryConnectServer.Execute(null);
		}

		private void tvExplorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			(DataContext as MainViewModel).SelectedDataRowView = (sender as TreeView).SelectedItem as DataRowView;
		}

		private void tvRevertExplorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			(DataContext as MainViewModel).SelectedReferencedDataRowView = (sender as TreeView).SelectedItem as DataRowView;
		}
    }
}
