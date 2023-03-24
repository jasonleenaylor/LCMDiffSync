using System;
using System.Collections.Generic;
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
using Newtonsoft.Json.Linq;

namespace DiffSync.TestApp
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private ClientDocumentManager _documentManager;
		private Guid _guid;
		private DirectChangeCommunicator _changeCommunicator;

		public MainWindow()
		{
			_guid = Guid.NewGuid();
			_documentManager = new ClientDocumentManager();
			_changeCommunicator = new DirectChangeCommunicator(_documentManager);
			_documentManager.InitializeServer(new Document("{'string':''}"), _changeCommunicator, _guid);
			_documentManager.OnContentChanged += ServerContentUpdate;
			InitializeComponent();
		}

		private void ServerContentUpdate()
		{
			serverText.TextChanged -= TextBox_TextChanged;
			if (serverText.Text != _documentManager.Content.GetString("string"))
			{
				serverText.Text = _documentManager.Content.GetString("string");
			}
			serverText.TextChanged += TextBox_TextChanged;
			ClientViews.UpdateClientView(_documentManager.clientDocuments);
		}

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			SetText(_documentManager.Content, serverText.Text);
		}

		private void SetText(Document content, string textboxContent)
		{
			content.SetString("string", textboxContent);
		}

		private void addClient_Click(object sender, RoutedEventArgs e)
		{
			var clientWindow = new ClientWindow(_changeCommunicator);
			_changeCommunicator.AddClient(clientWindow.ServerDocumentManager.Guid, clientWindow.ServerDocumentManager);
			clientWindow.Show();
		}
	}

	public class DirectChangeCommunicator : IServerToClientCommunicator, IClientToServerCommunicator
	{
		private ClientDocumentManager _server;
		private Dictionary<Guid, ServerDocumentManager> _clientManagers;
		public Guid RemoteGuid { get; }

		public DirectChangeCommunicator(ClientDocumentManager server)
		{
			_server = server;
			_clientManagers = new Dictionary<Guid, ServerDocumentManager>();
		}

		public void SendClientEdits(Queue<IDocumentAction> edits)
		{
			_server.ApplyRemoteChangesToServer(edits);
			
		}

		public void RequestDump(Guid clientGuid)
		{
			_server.SyncClient(clientGuid);
		}

		public void AddClient(Guid clientId, ServerDocumentManager clientManager)
		{
			_clientManagers[clientId] = clientManager;
		}

		public void SendServerEdits(Queue<IDocumentAction> edits)
		{
			foreach (var client in _clientManagers)
			{
				client.Value.ApplyRemoteChangesToClient(edits);
			}
		}
	}
}
