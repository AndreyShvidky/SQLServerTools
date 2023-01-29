using System.Windows;
using System.Windows.Controls;

namespace DBConnectDialog
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class DBConnectWindow : Window
	{
		public DBConnectWindow(DBConnectViewModel viewModel)
		{
			DataContext = viewModel;
			InitializeComponent();
		}

		private void OnPasswordChanged(object sender, RoutedEventArgs e)
		{
			if (this.DataContext != null)
			{ ((DBConnectViewModel)this.DataContext).Password = ((PasswordBox)sender).SecurePassword; }
		}

		private void OnCancelButtonClick(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
