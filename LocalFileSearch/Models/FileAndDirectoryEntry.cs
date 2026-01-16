using System;

namespace MultiModelChat.LocalFileSearch.Models
{
    public class FileAndDirectoryEntry
    {
        protected UInt64 fileReferenceNumber;
        protected UInt64 parentFileReferenceNumber;
        protected string fileName;
        protected bool isFolder;
        protected string path;

        public UInt64 FileReferenceNumber => this.fileReferenceNumber;
        public UInt64 ParentFileReferenceNumber => this.parentFileReferenceNumber;
        public string FileName => this.fileName;
        public string Path => this.path;
        public string FullFileName => string.Concat(this.path, "\\", this.fileName);
        public bool IsFolder => this.isFolder;

        public FileAndDirectoryEntry(UsnEntry usnEntry, string path)
        {
            this.fileReferenceNumber = usnEntry.FileReferenceNumber;
            this.parentFileReferenceNumber = usnEntry.ParentFileReferenceNumber;
            this.fileName = usnEntry.FileName;
            this.isFolder = usnEntry.IsFolder;
            this.path = path;
        }
    }
}
