namespace DiffSync.Tests;

public class DocumentTests
{
    [Test]
    public void ClonesAreEqual()
    {
        var doc1 = new Document("{\"a\": 1}");
        var doc2 = doc1.Clone();
        Assert.That(doc1, Is.EqualTo(doc2));
    }
}