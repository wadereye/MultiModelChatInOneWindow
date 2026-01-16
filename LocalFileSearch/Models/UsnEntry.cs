using System;
using MultiModelChat.LocalFileSearch.Native;

namespace MultiModelChat.LocalFileSearch.Models
{
    public class UsnEntry
    {
        public UInt32 RecordLength { get; private set; }
        public UInt64 FileReferenceNumber { get; private set; }

        public UInt64 ParentFileReferenceNumber { get; private set; }
        public Int64 Usn { get; private set; }
        public UInt32 Reason { get; private set; }
        public UInt32 FileAttributes { get; private set; }
        public Int32 FileNameLength { get; private set; }
        public Int32 FileNameOffset { get; private set; }
        public string FileName { get; private set; }
        public bool IsFolder 
        { 
            get 
            {
                return (this.FileAttributes & NativeConstants.FILE_ATTRIBUTE_DIRECTORY) != 0;
            } 
        }
            
        public UsnEntry(USN_RECORD_V2 usnRecord)
        {
            this.RecordLength = usnRecord.RecordLength;
            this.FileReferenceNumber = usnRecord.FileReferenceNumber;
            this.ParentFileReferenceNumber = usnRecord.ParentFileReferenceNumber;
            this.Usn = usnRecord.Usn;
            this.Reason = usnRecord.Reason;
            this.FileAttributes = usnRecord.FileAttributes;
            this.FileNameLength = usnRecord.FileNameLength;
            this.FileNameOffset = usnRecord.FileNameOffset;
            this.FileName = usnRecord.FileName;
        }
    }
}
