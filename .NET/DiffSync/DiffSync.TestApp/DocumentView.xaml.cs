using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DiffSync.TestApp
{
	/// <summary>
	/// Interaction logic for DocumentView
	/// </summary>
	public partial class DocumentView : UserControl
	{
		private ObservableCollection<ClientDocData> _dataSource = new ();
		public DocumentView()
		{
			InitializeComponent();
			ServerClientView.ItemsSource = _dataSource;
		}

		public void UpdateClientView(Dictionary<Guid, DiffSyncDocument> latestData)
		{
			_dataSource.Clear();
			foreach (var kvp in latestData)
			{
				_dataSource.Add(new ClientDocData(kvp.Key, kvp.Value.Shadow.ClientVersion, kvp.Value.Shadow.ServerVersion,
					kvp.Value.Backup.ClientVersion, kvp.Value.Backup.ServerVersion, kvp.Value.DocActions.Count,
					kvp.Value.Shadow.Document?.ToString(),
					kvp.Value.Backup.Document?.ToString()));
			}
		}

		private class ClientDocData
		{
			public Guid Guid { get; }
			public long ShadowClientVer { get; }
			public long ShadowServerVer { get; }
			public long BackupClientVer { get; }
			public long BackupServerVer { get; }
			public long EditCount { get; }
			public string ShadowContent { get; }
			public string BackupContent { get; }

			public ClientDocData(Guid guid, long shadowClientVer, long shadowServerVer, long backupClientVer, long backupServerVer,
				long editCount, string? shadowContent, string? backupContent)
			{
				Guid = guid;
				ShadowClientVer = shadowClientVer;
				ShadowServerVer = shadowServerVer;
				BackupClientVer = backupClientVer;
				BackupServerVer = backupServerVer;
				EditCount = editCount;
				ShadowContent = shadowContent;
				BackupContent = backupContent;
			}
		}
	}
}
