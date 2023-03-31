using System;
using System.Windows.Controls;
using Ycs;

namespace DiffSync.TestApp;

public static class YTextExtensions
{
    public static void SetFromEvent(this YText text, TextChangedEventArgs e, string newString)
    {
        foreach (var change in e.Changes)
        {
            //if you select text and replace it with new text, the change will be a remove and an add in one change.
            //if you select text and drag to move it somewhere else that will be 2 changes.
            if (change.RemovedLength > 0)
            {
                text.Delete(change.Offset, change.RemovedLength);
            }
            if (change.AddedLength > 0)
            {
                text.Insert(change.Offset, newString.Substring(change.Offset, change.AddedLength));
            }
        }
    }
}