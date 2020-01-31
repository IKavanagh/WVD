using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TerminalServerFileCopy
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string _sourceFileName;

		private string SourceFileName
		{
			get => _sourceFileName;
			set
			{
				_sourceFileName = value;
				SourceFileInfo = new FileInfo(_sourceFileName);
			}
		}

		private FileInfo SourceFileInfo { get; set; }

		private const string Destination = @"\\tsclient\C\Temp";

		private CancellationTokenSource _cts;
		private CancellationToken _ct;

		public MainWindow()
		{
			InitializeComponent();

			_cts = new CancellationTokenSource();
			_ct = _cts.Token;
		}

		private void FileButton_OnClick(object sender, RoutedEventArgs e)
		{
			var fileDialog = new OpenFileDialog();
			if (fileDialog.ShowDialog() == true)
			{
				SourceFileName = fileDialog.FileName;

				FileNameLabel.Content = Path.GetFileName(SourceFileName);
				FileSizeLabel.Content = SizeSuffix(SourceFileInfo.Length);

				TransferTime.Content = "";
			}
		}

		private async void CopyButton_OnClick(object sender, RoutedEventArgs e)
		{
			TransferTime.Content = "";

			if (String.IsNullOrWhiteSpace(SourceFileName))
			{
				MessageBox.Show("Please choose a file to copy and a location to copy to.",
								"Input error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (!Int64.TryParse(CopyTimesTextBox.Text, out long times))
			{
				times = 10;
				MessageBox.Show("Unable to determine number of times to copy file, defaulting to 100.",
								"Input error", MessageBoxButton.OK, MessageBoxImage.Error);
			}

			CopyLocationLabel.Content = Destination;

			ProgressDialog.Visibility = Visibility.Visible;

			var watch = new Stopwatch();

			try
			{
				double totalElapsedMilliseconds = 0;

				await Task.Run(() =>
				{
					Directory.CreateDirectory(Destination);
					string destinationFileName = Path.Combine(Destination, Path.GetFileName(SourceFileName));

					for (int i = 0; i < times; ++i)
					{
						File.Delete(destinationFileName);
						watch.Restart();
						File.Copy(SourceFileName, destinationFileName, true);
						watch.Stop();

						totalElapsedMilliseconds += watch.ElapsedMilliseconds;
						_ct.ThrowIfCancellationRequested();

						double milliseconds = totalElapsedMilliseconds;
						int time = i + 1;
						Dispatcher?.Invoke(() => TransferTime.Content = DisplayString(milliseconds / time));
					}
				}, _ct).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				_cts.Dispose();
				_cts = new CancellationTokenSource();
				_ct = _cts.Token;
			}
			finally
			{
				Dispatcher?.Invoke(() => ProgressDialog.Visibility = Visibility.Collapsed);
			}
		}

		private void ProgressDialog_OnCancelled(object sender, EventArgs e)
		{
			_cts.Cancel();
			ProgressDialog.Visibility = Visibility.Collapsed;
		}

		private string DisplayString(double elapsedMilliseconds)
		{
			return $"{elapsedMilliseconds:n2}ms at {SizeSuffix(SourceFileInfo.Length / (elapsedMilliseconds / 1000))}/s";
		}

		#region Convert bytes to KB, MB, GB, etc.

		private static readonly string[] _SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

		private static string SizeSuffix(double value, int decimalPlaces = 2)
		{
			if (decimalPlaces < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
			}
			if (value < 0)
			{
				return "-" + SizeSuffix(-value, decimalPlaces);
			}
			if (Math.Abs(value) < 1e-16)
			{
				return String.Format("{0:n" + decimalPlaces + "} bytes", 0);
			} // Check for 0

			// mag is 0 for bytes, 1 for KB, 2, for MB, etc.
			int mag = (int)Math.Log(value, 1024);

			// 1L << (mag * 10) == 2 ^ (10 * mag)
			// [i.e. the number of bytes in the unit corresponding to mag]
			decimal adjustedSize = (decimal)value / (1L << (mag * 10));

			// make adjustment when the value is large enough that
			// it would round up to 1000 or more
			if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
			{
				mag++;
				adjustedSize /= 1024;
			}

			if (mag > 8)
			{
				adjustedSize *= 1024 * (mag - 8);
				mag = 8;
			}

			return String.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, _SizeSuffixes[mag]);
		}

		#endregion
	}
}