using System;
using System.Runtime.InteropServices;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace MultiModelChat.LocalFileSearch.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public FILETIME CreationTime;
        public FILETIME LastAccessTime;
        public FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CREATE_USN_JOURNAL_DATA
    {
        public UInt64 MaximumSize;
        public UInt64 AllocationDelta;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MFT_ENUM_DATA
    {
        public UInt64 StartFileReferenceNumber;
        public Int64 LowUsn;
        public Int64 HighUsn;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct READ_USN_JOURNAL_DATA
    {
        public Int64 StartUsn;
        public UInt32 ReasonMask;
        public UInt32 ReturnOnlyOnClose;
        public UInt64 Timeout;
        public UInt64 bytesToWaitFor;
        public UInt64 UsnJournalId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct USN_JOURNAL_DATA
    {
        public UInt64 UsnJournalID;
        public Int64 FirstUsn;
        public Int64 NextUsn;
        public Int64 LowestValidUsn;
        public Int64 MaxUsn;
        public UInt64 MaximumSize;
        public UInt64 AllocationDelta;
    }

    public class USN_RECORD_V2
    {
        private const int FR_OFFSET = 8;
        private const int PFR_OFFSET = 16;
        private const int USN_OFFSET = 24;
        private const int REASON_OFFSET = 40;
        private const int FA_OFFSET = 52;
        private const int FNL_OFFSET = 56;
        private const int FN_OFFSET = 58;

        public UInt32 RecordLength { get; private set; }
        public UInt64 FileReferenceNumber { get; private set; }
        public UInt64 ParentFileReferenceNumber { get; private set; }
        public Int64 Usn { get; private set; }
        public UInt32 Reason { get; private set; }
        public UInt32 FileAttributes { get; private set; }
        public Int32 FileNameLength { get; private set; }
        public Int32 FileNameOffset { get; private set; }
        public string FileName { get; private set; }

        public USN_RECORD_V2(IntPtr usnRecordPtr)
        {
            this.RecordLength = (UInt32)Marshal.ReadInt32(usnRecordPtr);
            this.FileReferenceNumber = (UInt64)Marshal.ReadInt64(usnRecordPtr, FR_OFFSET);
            this.ParentFileReferenceNumber = (UInt64)Marshal.ReadInt64(usnRecordPtr, PFR_OFFSET);
            this.Usn = Marshal.ReadInt64(usnRecordPtr, USN_OFFSET);
            this.Reason = (UInt32)Marshal.ReadInt32(usnRecordPtr, REASON_OFFSET);
            this.FileAttributes = (UInt32)Marshal.ReadInt32(usnRecordPtr, FA_OFFSET);
            this.FileNameLength = Marshal.ReadInt16(usnRecordPtr, FNL_OFFSET);
            this.FileNameOffset = Marshal.ReadInt16(usnRecordPtr, FN_OFFSET);
            this.FileName = Marshal.PtrToStringUni(new IntPtr(usnRecordPtr.ToInt64() + this.FileNameOffset), this.FileNameLength / sizeof(char));
        }
    }
}
