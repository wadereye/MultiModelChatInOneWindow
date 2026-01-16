using System;
using System.IO;
using MultiModelChat.LocalFileSearch.Native;

namespace MultiModelChat.LocalFileSearch.Models
{
    public class UsnJournalData
    {
        public DriveInfo Drive { get; private set; }
        public UInt64 UsnJournalID { get; private set; }
        public Int64 FirstUsn { get; private set; }
        public Int64 NextUsn { get; private set; }
        public Int64 LowestValidUsn { get; private set; }
        public Int64 MaxUsn { get; private set; }
        public UInt64 MaximumSize { get; private set; }
        public UInt64 AllocationDelta { get; private set; }

        public UsnJournalData(DriveInfo drive, USN_JOURNAL_DATA ntfsUsnJournalData)
        {
            this.Drive = drive;
            this.UsnJournalID = ntfsUsnJournalData.UsnJournalID;
            this.FirstUsn = ntfsUsnJournalData.FirstUsn;
            this.NextUsn = ntfsUsnJournalData.NextUsn;
            this.LowestValidUsn = ntfsUsnJournalData.LowestValidUsn;
            this.MaxUsn = ntfsUsnJournalData.MaxUsn;
            this.MaximumSize = ntfsUsnJournalData.MaximumSize;
            this.AllocationDelta = ntfsUsnJournalData.AllocationDelta;
        }
    }
}
