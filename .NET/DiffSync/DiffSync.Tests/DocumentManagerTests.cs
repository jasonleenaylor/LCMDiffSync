using DiffSync;
using Moq;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public class DocumentManagerTests
	{
		Mock<IChangeCommunicator> mockCommunicator = new Mock<IChangeCommunicator>();

		[SetUp]
		public void Setup()
		{
		}

		[Test]
		public void ApplyLocalChangeUpdatesShadow()
		{
			var testJson = JObject.Parse("{string: 'cat'}");
			var testDoc = new DocumentManager();
			testDoc.Initialize(testJson, mockCommunicator.Object, new Guid());
			testJson["string"] = "cats";
			Assert.That(testDoc.Shadow.Document.Value<string>("string"), Is.EqualTo("cat"));
			testDoc.ApplyLocalChange(testJson);
			Assert.That(testDoc.Shadow.Document.Value<string>("string"), Is.EqualTo("cats"));
		}

		[Test]
		public void ApplyLocalChangeSendsSingleEdit()
		{
			var testJson = JObject.Parse("{string: 'cat'}");
			var testDoc = new DocumentManager();
			testDoc.Initialize(testJson, mockCommunicator.Object, new Guid());
			testJson["string"] = "cats";
			testDoc.ApplyLocalChange(testJson);
			mockCommunicator.Verify(x => x.SendEdits(It.Is<Stack<IEdit>>(x => x.Count == 1)), Times.Once);
		}

		[Test]
		public void ApplyLocalBeforeAcknowledgementSendsTwoEdits()
		{
			var testJson = JObject.Parse("{string: 'cat'}");
			var testDoc = new DocumentManager();
			testDoc.Initialize(testJson, mockCommunicator.Object, new Guid());
			testJson["string"] = "cats";
			testDoc.ApplyLocalChange(testJson);
			testJson["string"] = "cats!";
			mockCommunicator.Reset();
			testDoc.ApplyLocalChange(testJson);
			mockCommunicator.Verify(x => x.SendEdits(It.Is<Stack<IEdit>>(s => s.Count == 2)), Times.Once);
		}
	}
}