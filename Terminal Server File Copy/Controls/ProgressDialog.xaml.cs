using System;
using System.Windows;
using System.Windows.Controls;

namespace TerminalServerFileCopy.Controls
{
	public partial class ProgressDialog : UserControl
	{
		public ProgressDialog()
		{
			InitializeComponent();
		}

		public event EventHandler<EventArgs> Cancelled;

		private void CancelButton_OnClick(object sender, RoutedEventArgs e)
		{
			Cancelled?.Invoke(this, EventArgs.Empty);
		}
	}
}