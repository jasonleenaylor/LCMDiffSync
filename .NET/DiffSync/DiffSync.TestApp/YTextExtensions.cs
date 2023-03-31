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
            //todo according to the documentation both AddedLength and RemovedLength can be greater than 1, I'm not really sure
            //how to solve that, but for our simple app this doesn't come up
            if (change.AddedLength > 0 && change.RemovedLength > 0)
                throw new NotSupportedException("characters both added and removed in the same event, not currently supported");
            if (change.AddedLength > 0)
            {
                text.Insert(change.Offset, newString.Substring(change.Offset, change.AddedLength));
            }
            else 
            {
                text.Delete(change.Offset, change.RemovedLength);
            }
        }
    }
}