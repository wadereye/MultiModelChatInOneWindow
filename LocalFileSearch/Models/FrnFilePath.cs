using System;

namespace MultiModelChat.LocalFileSearch.Models
{
    internal class FrnFilePath
    {
        private UInt64 fileReferenceNumber;
        private UInt64? parentFileReferenceNumber;
        private string fileName;
        private string path;

        public UInt64 FileReferenceNumber => this.fileReferenceNumber;
        public UInt64? ParentFileReferenceNumber => this.parentFileReferenceNumber;
        public string FileName => this.fileName;
        public string Path
        { 
            get => this.path; 
            set => this.path = value;
        }

        public FrnFilePath(UInt64 fileReferenceNumber, UInt64? parentFileReferenceNumber, string fileName, string path = null)
        {
            this.fileReferenceNumber = fileReferenceNumber;
            this.parentFileReferenceNumber = parentFileReferenceNumber;
            this.fileName = fileName;
            this.path = path;
        }
    }
}
