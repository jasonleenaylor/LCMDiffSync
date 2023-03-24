using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace DiffSync.TestApp
{
	/// <summary>
	/// Interaction logic for ClientWindow.xaml
	/// </summary>
	public partial class ClientWindow : Window
	{
		private readonly ServerDocumentManager _serverDocumentManager = new ();
		private IClientToServerCommunicator _changeCommunicator;
		public ClientWindow(IClientToServerCommunicator changeCommunicator)
		{
			InitializeComponent();
			_changeCommunicator = changeCommunicator;
			_serverDocumentManager.OnContentChanged += HandleOnContentChange;
		}

		private void HandleOnContentChange()
		{
			textBox.TextChanged -= TextBox_TextChanged;
			textBox.Text = _serverDocumentManager.Content?.GetString("string") ?? "";
			textBox.TextChanged += TextBox_TextChanged;
			var tempDict = new Dictionary<Guid, DiffSyncDocument>();
			tempDict[_serverDocumentManager.Guid] = _serverDocumentManager._serverDocument;
			ServerShadowView.UpdateClientView(tempDict);
		}
		public ServerDocumentManager ServerDocumentManager => _serverDocumentManager;

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			_serverDocumentManager.Content.SetString("string", textBox.Text);
			_serverDocumentManager.ApplyLocalChange();
		}

		private void Grid_Loaded(object sender, RoutedEventArgs e)
		{
			_serverDocumentManager.InitFromServer(_changeCommunicator, _serverDocumentManager.Guid);
		}
	}
}
