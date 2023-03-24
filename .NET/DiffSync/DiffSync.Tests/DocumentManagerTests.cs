using Moq;

namespace DiffSync
{
	public class DocumentManagerTests
	{
		Mock<IClientToServerCommunicator> mockClientServerCommunicator = new();
		Mock<IServerToClientCommunicator> mockServerClientCommunicator = new();

		[SetUp]
		public void Setup()
		{
			mockClientServerCommunicator.Invocations.Clear();
			mockServerClientCommunicator.Invocations.Clear();
		}

		private Queue<IDocumentAction> GenerateTestEditQueue(Document?[] originals,
			Document[] updates,
			long clientVersion,
			long serverVersion,
			Guid serverGuid, Guid clientGuid)
		{
			Assert.That(originals.Length, Is.EqualTo(updates.Length), "You must have the same number of original and update objects");
			var editStack = new Queue<IDocumentAction>();

			for (var i = 0; i < originals.Length; i++, clientVersion++)
			{
				editStack.Enqueue(DocumentActionFactory.CreateEdit(clientGuid,
					serverGuid, clientVersion, serverVersion, Document.Diff(originals[i], updates[i])));
			}
			return editStack;
		}
		private Queue<IDocumentAction> GenerateTestReset(Document content,
			long clientVersion,
			long serverVersion,
			Guid serverGuid, Guid clientGuid)
		{
			var actions = new Queue<IDocumentAction>();
			actions.Enqueue(DocumentActionFactory.CreateReset(clientGuid, serverGuid, clientVersion, serverVersion, content.Clone()));
			return actions;
		}

		private Queue<IDocumentAction> GenerateClientAck(Guid clientId, Guid serverId, long clientVersion)
		{
			var actions = new Queue<IDocumentAction>();
			actions.Enqueue(DocumentActionFactory.CreateClientAck(clientId, serverId, clientVersion));
			return actions;
		}

		[Test]
		public void InitializeServer()
		{
			var catContent = "{'string':'cougar'}";
			var contents = new Document(catContent);
			var testDoc = new ClientDocumentManager();
			// SUT
			testDoc.InitializeServer(contents, mockServerClientCommunicator.Object, Guid.NewGuid());
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(0)); // No shadows created until clients are added
			Assert.That(testDoc.Content.Diff(contents), Is.Null);
		}

		[Test]
		public void AddClientSetsUpServerShadows()
		{
			var catContent = "{'string':'cougar'}";
			var serverDoc = new ClientDocumentManager();
			serverDoc.InitializeServer(new Document(catContent), mockServerClientCommunicator.Object, Guid.NewGuid());
			var clientGuid = Guid.NewGuid();
			// SUT
			serverDoc.SyncClient(clientGuid);
			Assert.That(serverDoc.GetClientCount(), Is.EqualTo(1));
			var shadow = serverDoc.GetShadow(clientGuid);
			Assert.That(shadow.ClientVersion, Is.EqualTo(0));
			Assert.That(shadow.ServerVersion, Is.EqualTo(0));
			mockServerClientCommunicator.Verify(x=>x.SendServerEdits(It.Is<Guid>(g => g == clientGuid),
				It.Is<Queue<IDocumentAction>>(s => s.Count == 1)), Times.Once);
		}

		[Test]
		public void ServerHasNoChangesClientSentAck()
		{
			var catContent = new Document("{'string':'cougar'}");
			var testDoc = new ClientDocumentManager();
			var serverGuid = Guid.NewGuid();
			// SUT
			testDoc.InitializeServer(catContent, mockServerClientCommunicator.Object, serverGuid);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(0)); // No shadows created until clients are added
			Assert.That(testDoc.Content.Diff(catContent), Is.Null);
			var clientGuid = Guid.NewGuid();
			testDoc.SyncClient(clientGuid);
			var clientEditStack =
				GenerateTestEditQueue(new[] { catContent }, new[] { new Document("{'string':'cougars'}") }, 0, 0, serverGuid, clientGuid);
			mockServerClientCommunicator.Invocations.Clear();
			testDoc.ApplyRemoteChangesToServer(clientEditStack);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(1));
			Assert.That(testDoc.GetShadow(clientGuid).ServerVersion, Is.EqualTo(0));
			mockServerClientCommunicator.Verify(x => x.SendServerEdits(It.Is<Guid>(g => g == clientGuid), 
				It.Is<Queue<IDocumentAction>>(s => s.Peek().ClientVersion == 0)), Times.Once);
		}

		[Test]
		public void ServerHasChangesClientSentEdit()
		{
			var catContent = new Document("{'string':'cougar'}");
			var testDoc = new ClientDocumentManager();
			var serverGuid = Guid.NewGuid();
			// SUT
			testDoc.InitializeServer(catContent, mockServerClientCommunicator.Object, serverGuid);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(0)); // No shadows created until clients are added
			Assert.That(testDoc.Content.Diff(catContent), Is.Null);
			var clientGuid = Guid.NewGuid();
			testDoc.SyncClient(clientGuid);
			var clientEditStack =
				GenerateTestEditQueue(new []{catContent}, new []{new Document("{'string':'cougars'}")}, 0, 0, serverGuid, clientGuid);
			testDoc.Content.SetString("string", "My cat");
			mockServerClientCommunicator.Invocations.Clear();
			testDoc.ApplyRemoteChangesToServer(clientEditStack);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(1));
			Assert.That(testDoc.GetShadow(clientGuid).ServerVersion, Is.EqualTo(1));
			mockServerClientCommunicator.Verify(x => x.SendServerEdits(It.Is<Guid>(g => g == clientGuid), 
				It.Is<Queue<IDocumentAction>>(s => s.Peek().ServerVersion == 0)), Times.Once);
		}

		[Test]
		public void ServerRollsBackToBackupIfClientMissedEdit()
		{
			var catContent = new Document("{'string':'cougar'}");
			var testDoc = new ClientDocumentManager();
			var serverGuid = Guid.NewGuid();
			// SUT
			testDoc.InitializeServer(catContent, mockServerClientCommunicator.Object, serverGuid);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(0)); // No shadows created until clients are added
			Assert.That(testDoc.Content.Diff(catContent), Is.Null);
			var clientGuid = Guid.NewGuid();
			testDoc.SyncClient(clientGuid);
			// At this point the server has the original content in an edit stack for the client
			var clientEditStack =
				GenerateTestEditQueue(new[] { catContent},
					new []{ new Document("{'string':'cougars'}")},
					0, 0, serverGuid, clientGuid);
			testDoc.Content.SetString("string", "My cougar");
			mockServerClientCommunicator.Invocations.Clear();
			testDoc.ApplyRemoteChangesToServer(clientEditStack);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(1));
			Assert.That(testDoc.GetShadow(clientGuid).ServerVersion, Is.EqualTo(1));
			mockServerClientCommunicator.Verify(x => x.SendServerEdits(It.Is<Guid>(g => g == clientGuid), 
				It.Is<Queue<IDocumentAction>>(s => s.Peek().ServerVersion == 0)), Times.Once);
			mockServerClientCommunicator.Invocations.Clear();
			// Simulate dropped packet from server
			var clientEditStack2 =
				GenerateTestEditQueue(new[] { catContent, new Document("{'string':'cougars'}") },
					new[] { new Document("{'string':'cougars'}"), new Document("{'string':'cougars!'}") },
					0, 0, serverGuid, clientGuid);
			testDoc.ApplyRemoteChangesToServer(clientEditStack2);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(1));
			Assert.That(testDoc.GetShadow(clientGuid).ServerVersion, Is.EqualTo(1));
			mockServerClientCommunicator.Verify(x => x.SendServerEdits(It.Is<Guid>(g => g == clientGuid), 
				It.Is<Queue<IDocumentAction>>(s => s.Count == 1 && s.Peek().ServerVersion == 0)), Times.Once);
		}

		[Test]
		public void InitFromServer()
		{
			var serverDoc = new Document("{'string':'cougar'}");
			long serverVersion = 2013; // arbitrary
			Guid serverId = Guid.NewGuid();
			var testDoc = new ServerDocumentManager();
			// Set up our mock remote call to directly call the client method which would result from the server activity
			mockClientServerCommunicator.Setup(x => x.RequestDump(It.IsAny<Guid>())).Callback((Guid guid) =>
			{
				var editStack = new Queue<IDocumentAction>();
				editStack.Enqueue(DocumentActionFactory.CreateReset(guid, serverId, 0, serverVersion, serverDoc));
				testDoc.ApplyRemoteChangesToClient(editStack);
			});
			// SUT
			testDoc.InitFromServer(mockClientServerCommunicator.Object, testDoc.Guid);
			mockClientServerCommunicator.Verify((x => x.RequestDump(It.IsAny<Guid>())), Times.Once);
			Assert.That(testDoc.Content, Is.EqualTo(serverDoc));
			var shadow = testDoc.GetShadow(serverId);
			Assert.That(shadow.ClientVersion, Is.EqualTo(0));
			Assert.That(shadow.ServerVersion, Is.EqualTo(serverVersion));
			Assert.That(shadow.Document, Is.EqualTo(serverDoc));
		}

		[Test]
		public void ApplyLocalChangeUpdatesShadow()
		{
			var testJson = new Document("{string: 'cougar'}");
			var testDoc = new ServerDocumentManager();
			Guid serverGuid = Guid.NewGuid();
			testDoc.InitFromServer(mockClientServerCommunicator.Object, testDoc.Guid);
			// make edit stack to simulate the server sending edits to the client
			var serverEdits = GenerateTestEditQueue(new Document?[]{null}, new []{testJson}, 0, 0, serverGuid, testDoc.Guid);
			testDoc.ApplyRemoteChangesToClient(serverEdits);
			var shadow = testDoc.GetShadow(serverGuid);
			Assert.That(shadow, Is.Not.Null);
			Assert.That(ReferenceEquals(testDoc.Content, shadow.Document), Is.False);
			testDoc.Content.SetString("string", "cougars");
			Assert.That(shadow.Document.GetString("string"), Is.EqualTo("cougar"));
			testDoc.ApplyLocalChange();
			Assert.That(shadow.Document.GetString("string"), Is.EqualTo("cougars"));
		}


		[Test]
		public void ApplyLocalChangeSendsSingleEdit()
		{
			var testJson = new Document("{string: 'cougar'}");
			var testDoc = new ServerDocumentManager();
			Guid serverGuid = Guid.NewGuid();
			testDoc.InitFromServer(mockClientServerCommunicator.Object, testDoc.Guid);
			testDoc.ApplyRemoteChangesToClient(GenerateTestReset(testJson, 0, 0, serverGuid, testDoc.Guid));
			Assert.That(testDoc.GetShadow(serverGuid).ClientVersion, Is.EqualTo(0));
			mockClientServerCommunicator.Verify(x => x.SendClientEdits(
					It.Is<Queue<IDocumentAction>>(x => x.Count == 1 && x.Peek().Type == DocActionType.ServerAck)),
				Times.Once);
			testDoc.ApplyRemoteChangesToClient(GenerateClientAck(testDoc.Guid, serverGuid, 0));
			mockClientServerCommunicator.Invocations.Clear();
			testJson.SetString("string", "cougars");
			testDoc.ApplyLocalChange();
			Assert.That(testDoc.GetShadow(serverGuid).ClientVersion, Is.EqualTo(1));
			mockClientServerCommunicator.Verify(x => x.SendClientEdits(
				It.Is<Queue<IDocumentAction>>(x => x.Count == 1 && x.Peek().ClientVersion == 0)),
				Times.Once);
		}

		[Test]
		public void ApplyLocalBeforeAcknowledgementSendsTwoEdits()
		{
			var testJson = new Document("{string: 'cougar'}");
			var testDoc = new ServerDocumentManager();
			Guid serverGuid = Guid.NewGuid();
			testDoc.InitFromServer(mockClientServerCommunicator.Object, testDoc.Guid);
			testDoc.ApplyRemoteChangesToClient(GenerateTestEditQueue(new Document[] { null }, new[] { testJson }, 0, 0, serverGuid, testDoc.Guid));
			testJson.SetString("string", "cougars");
			testDoc.ApplyLocalChange();
			testJson.SetString("string", "cougars!");
			mockClientServerCommunicator.Reset();
			testDoc.ApplyLocalChange();
			// One Server acknowledgement followed by two edits
			mockClientServerCommunicator.Verify(x => x.SendClientEdits(It.Is<Queue<IDocumentAction>>(s => s.Count == 3)), Times.Once);
		}

	}
}