using DiffSync;
using JsonDiffPatchDotNet;
using Moq;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public class DocumentManagerTests
	{
		Mock<IChangeCommunicator> mockCommunicator = new();
		JsonDiffPatch diffPatcher = new JsonDiffPatch(new Options { MinEfficientTextDiffLength = 2 });

		[SetUp]
		public void Setup()
		{
			mockCommunicator.Invocations.Clear();
		}

		private Queue<IDocumentAction> GenerateTestEditStack(JObject?[] originals,
			JObject[] updates,
			long clientVersion,
			long serverVersion,
			Guid serverGuid, Guid clientGuid)
		{
			Assert.That(originals.Length, Is.EqualTo(updates.Length), "You must have the same number of original and update objects");
			var editStack = new Queue<IDocumentAction>();

			for (var i = 0; i < originals.Length; i++, clientVersion++)
			{
				editStack.Enqueue(new Edit
				{
					Diff = diffPatcher.Diff(originals[i], updates[i]),
					ClientVersion = clientVersion,
					ServerVersion = serverVersion,
					ServerId = serverGuid,
					ClientId = clientGuid
				});
			}
			return editStack;
		}

		[Test]
		public void InitializeServer()
		{
			var catContent = "{'string':'cougar'}";
			var testDoc = new ClientDocumentManager();
			// SUT
			testDoc.InitializeServer(JObject.Parse(catContent), mockCommunicator.Object, Guid.NewGuid());
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(0)); // No shadows created until clients are added
			Assert.That(diffPatcher.Diff(testDoc.Content, JObject.Parse(catContent)), Is.Null);
		}

		[Test]
		public void AddClientSetsUpServerShadows()
		{
			var catContent = "{'string':'cougar'}";
			var serverDoc = new ClientDocumentManager();
			serverDoc.InitializeServer(JObject.Parse(catContent), mockCommunicator.Object, Guid.NewGuid());
			var clientGuid = Guid.NewGuid();
			// SUT
			serverDoc.SyncClient(clientGuid);
			Assert.That(serverDoc.GetClientCount(), Is.EqualTo(1));
			var shadow = serverDoc.GetShadow(clientGuid);
			Assert.That(shadow.ClientVersion, Is.EqualTo(0));
			Assert.That(shadow.ServerVersion, Is.EqualTo(0));
			mockCommunicator.Verify(x=>x.SendEdits(
				It.Is<Queue<IDocumentAction>>(s => s.Count == 1)), Times.Once);
		}

		[Test]
		public void ServerHasChangesClientSentEdit()
		{
			var catContent = JObject.Parse("{'string':'cougar'}");
			var testDoc = new ClientDocumentManager();
			var serverGuid = Guid.NewGuid();
			// SUT
			testDoc.InitializeServer(catContent, mockCommunicator.Object, serverGuid);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(0)); // No shadows created until clients are added
			Assert.That(diffPatcher.Diff(testDoc.Content, catContent), Is.Null);
			var clientGuid = Guid.NewGuid();
			testDoc.SyncClient(clientGuid);
			var clientEditStack =
				GenerateTestEditStack(new []{catContent}, new []{JObject.Parse("{'string':'cougars'}")}, 0, 0, serverGuid, clientGuid);
			testDoc.Content["string"] = "My cat";
			mockCommunicator.Invocations.Clear();
			testDoc.ApplyRemoteChangesToServer(clientEditStack);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(1));
			Assert.That(testDoc.GetShadow(clientGuid).ServerVersion, Is.EqualTo(1));
			mockCommunicator.Verify(x => x.SendEdits(It.Is<Queue<IDocumentAction>>(s => s.Peek().ServerVersion == 0)), Times.Once);
		}

		[Test]
		public void ServerRollsBackToBackupIfClientMissedEdit()
		{
			var catContent = JObject.Parse("{'string':'cougar'}");
			var testDoc = new ClientDocumentManager();
			var serverGuid = Guid.NewGuid();
			// SUT
			testDoc.InitializeServer(catContent, mockCommunicator.Object, serverGuid);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(0)); // No shadows created until clients are added
			Assert.That(diffPatcher.Diff(testDoc.Content, catContent), Is.Null);
			var clientGuid = Guid.NewGuid();
			testDoc.SyncClient(clientGuid);
			// At this point the server has the original content in an edit stack for the client
			var clientEditStack =
				GenerateTestEditStack(new[] { catContent},
					new []{ JObject.Parse("{'string':'cougars'}")},
					0, 0, serverGuid, clientGuid);
			testDoc.Content["string"] = "My cougar";
			mockCommunicator.Invocations.Clear();
			testDoc.ApplyRemoteChangesToServer(clientEditStack);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(1));
			Assert.That(testDoc.GetShadow(clientGuid).ServerVersion, Is.EqualTo(1));
			mockCommunicator.Verify(x => x.SendEdits(It.Is<Queue<IDocumentAction>>(s => s.Peek().ServerVersion == 0)), Times.Once);
			mockCommunicator.Invocations.Clear();
			// Simulate dropped packet from server
			var clientEditStack2 =
				GenerateTestEditStack(new[] { catContent, JObject.Parse("{'string':'cougars'}") },
					new[] { JObject.Parse("{'string':'cougars'}"), JObject.Parse("{'string':'cougars!'}") },
					0, 0, serverGuid, clientGuid);
			testDoc.ApplyRemoteChangesToServer(clientEditStack2);
			Assert.That(testDoc.GetClientCount(), Is.EqualTo(1));
			Assert.That(testDoc.GetShadow(clientGuid).ServerVersion, Is.EqualTo(1));
			mockCommunicator.Verify(x => x.SendEdits(It.Is<Queue<IDocumentAction>>(s => s.Count == 1 && s.Peek().ServerVersion == 0)), Times.Once);
		}

		[Test]
		public void InitFromServer()
		{
			var catContent = "{'string':'cougar'}";
			long serverVersion = 2013; // arbitrary
			Guid serverId = Guid.NewGuid();
			var testDoc = new ServerDocumentManager();
			// Set up our mock remote call to directly call the client method which would result from the server activity
			mockCommunicator.Setup(x => x.RequestDump(It.IsAny<Guid>())).Callback((Guid guid) =>
			{
				var editStack = new Queue<IDocumentAction>();
				editStack.Enqueue(new Edit { ClientId = guid, ServerId = serverId, ClientVersion = 0, Diff = diffPatcher.Diff(null, JObject.Parse(catContent)), ServerVersion = serverVersion });
				testDoc.ApplyRemoteChangesToClient(editStack);
			});
			var clientId = Guid.NewGuid();
			// SUT
			testDoc.InitFromServer(mockCommunicator.Object, clientId);
			mockCommunicator.Verify((x => x.RequestDump(It.IsAny<Guid>())), Times.Once);
			Assert.That(testDoc.Content, Is.EqualTo(JObject.Parse(catContent)));
			var shadow = testDoc.GetShadow(serverId);
			Assert.That(shadow.ClientVersion, Is.EqualTo(0));
			Assert.That(shadow.ServerVersion, Is.EqualTo(serverVersion));
			Assert.That(shadow.Document, Is.EqualTo(JObject.Parse(catContent)));
		}

		[Test]
		public void ApplyLocalChangeUpdatesShadow()
		{
			var testJson = JObject.Parse("{string: 'cougar'}");
			var testDoc = new ServerDocumentManager();
			Guid serverGuid = Guid.NewGuid();
			testDoc.InitFromServer(mockCommunicator.Object, new Guid());
			// make edit stack to simulate the server sending edits to the client
			var serverEdits = GenerateTestEditStack(new JObject?[]{null}, new []{testJson}, 0, 0, serverGuid, Guid.Empty);
			testDoc.ApplyRemoteChangesToClient(serverEdits);
			var shadow = testDoc.GetShadow(serverGuid);
			Assert.That(shadow, Is.Not.Null);
			Assert.That(ReferenceEquals(testDoc.Content, shadow.Document), Is.False);
			testDoc.Content["string"] = "cougars";
			Assert.That(shadow.Document!.Value<string>("string"), Is.EqualTo("cougar"));
			testDoc.ApplyLocalChange();
			Assert.That(shadow.Document.Value<string>("string"), Is.EqualTo("cougars"));
		}


		[Test]
		public void ApplyLocalChangeSendsSingleEdit()
		{
			var testJson = JObject.Parse("{string: 'cougar'}");
			var testDoc = new ServerDocumentManager();
			Guid serverGuid = Guid.NewGuid();
			testDoc.InitFromServer(mockCommunicator.Object, new Guid());
			testDoc.ApplyRemoteChangesToClient(GenerateTestEditStack(new JObject?[] { null }, new[] { testJson }, 0, 0, serverGuid, Guid.Empty));
			Assert.That(testDoc.GetShadow(serverGuid).ClientVersion, Is.EqualTo(0));
			testJson["string"] = "cougars";
			testDoc.ApplyLocalChange();
			Assert.That(testDoc.GetShadow(serverGuid).ClientVersion, Is.EqualTo(1));
			mockCommunicator.Verify(x => x.SendEdits(
				It.Is<Queue<IDocumentAction>>(x => x.Count == 1 && ((IEdit)x.Peek()).ClientVersion == 0)),
				Times.Once);
		}

		[Test]
		public void ApplyLocalBeforeAcknowledgementSendsTwoEdits()
		{
			var testJson = JObject.Parse("{string: 'cougar'}");
			var testDoc = new ServerDocumentManager();
			Guid serverGuid = Guid.NewGuid();
			testDoc.InitFromServer(mockCommunicator.Object, new Guid());
			testDoc.ApplyRemoteChangesToClient(GenerateTestEditStack(new JObject?[] { null }, new[] { testJson }, 0, 0, serverGuid, Guid.Empty));
			testJson["string"] = "cougars";
			testDoc.ApplyLocalChange();
			testJson["string"] = "cougars!";
			mockCommunicator.Reset();
			testDoc.ApplyLocalChange();
			mockCommunicator.Verify(x => x.SendEdits(It.Is<Queue<IDocumentAction>>(s => s.Count == 2)), Times.Once);
		}

	}
}