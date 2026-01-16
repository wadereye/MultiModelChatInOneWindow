using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.ComponentModel;
using MultiModelChat.LocalFileSearch.Native;
using MultiModelChat.LocalFileSearch.Models;

namespace MultiModelChat.LocalFileSearch
{
    public class UsnOperator : IDisposable
    {
        protected USN_JOURNAL_DATA ntfsUsnJournalData;

        public DriveInfo Drive
        {
            get;
            private set;
        }

        public string DriveLetter
        {
            get
            {
                return this.Drive.Name.TrimEnd(new char[] { '\\', ':' });
            }
        }

        public IntPtr DriveRootHandle
        {
            get;
            private set;
        }

        public UsnOperator(DriveInfo drive)
        {
            if (string.Compare(drive.DriveFormat, "ntfs", true) != 0)
            {
                throw new ArgumentException("USN journal only exists in NTFS drive.");
            }

            this.Drive = drive;
            this.DriveRootHandle = this.GetRootHandle();
            this.ntfsUsnJournalData = new USN_JOURNAL_DATA();
        }

        public UsnJournalData GetUsnJournal()
        {
            UsnErrorCode usnErrorCode = this.QueryUSNJournal();

            UsnJournalData result = null;

            if (usnErrorCode == UsnErrorCode.SUCCESS)
            {
                result = new UsnJournalData(this.Drive, this.ntfsUsnJournalData);
            }

            return result;
        }

        public bool CreateUsnJournal(UInt64 maximumSize = 0x10000000, UInt64 allocationDelta = 0x100000)
        {
            uint bytesReturnedCount;

            var createUsnJournalData = new CREATE_USN_JOURNAL_DATA();
            createUsnJournalData.MaximumSize = maximumSize;
            createUsnJournalData.AllocationDelta = allocationDelta;

            int sizeCujd = Marshal.SizeOf(createUsnJournalData);

            IntPtr cujdBuffer = GetHeapGlobalPtr(sizeCujd);

            Marshal.StructureToPtr(createUsnJournalData, cujdBuffer, true);

            bool isSuccess = NativeMethods.DeviceIoControl(
                this.DriveRootHandle,
                NativeConstants.FSCTL_CREATE_USN_JOURNAL,
                cujdBuffer,
                sizeCujd,
                IntPtr.Zero,
                0,
                out bytesReturnedCount,
                IntPtr.Zero);

            Marshal.FreeHGlobal(cujdBuffer);

            return isSuccess;
        }

        public List<UsnEntry> GetEntries()
        {
            var result = new List<UsnEntry>();

            UsnErrorCode usnErrorCode = this.QueryUSNJournal();

            // 如果 USN Journal 不存在，尝试创建一个
            if (usnErrorCode == UsnErrorCode.ERROR_JOURNAL_NOT_ACTIVE || 
                usnErrorCode == UsnErrorCode.ERROR_JOURNAL_DELETE_IN_PROGRESS)
            {
                if (!this.CreateUsnJournal())
                {
                    int createError = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"无法创建 USN Journal (Error: {createError})");
                }
                
                // 重新查询
                usnErrorCode = this.QueryUSNJournal();
            }

            if (usnErrorCode != UsnErrorCode.SUCCESS)
            {
                throw new InvalidOperationException($"查询 USN Journal 失败 (Error: {usnErrorCode})");
            }

            MFT_ENUM_DATA mftEnumData = new MFT_ENUM_DATA();
            mftEnumData.StartFileReferenceNumber = 0;
            mftEnumData.LowUsn = 0;
            mftEnumData.HighUsn = this.ntfsUsnJournalData.NextUsn;

            int sizeMftEnumData = Marshal.SizeOf(mftEnumData);

            IntPtr ptrMftEnumData = GetHeapGlobalPtr(sizeMftEnumData);

            Marshal.StructureToPtr(mftEnumData, ptrMftEnumData, true);

            int ptrDataSize = sizeof(UInt64) + 10000;

            IntPtr ptrData = GetHeapGlobalPtr(ptrDataSize);

            uint outBytesCount;

            try
            {
                while (false != NativeMethods.DeviceIoControl(
                    this.DriveRootHandle,
                    NativeConstants.FSCTL_ENUM_USN_DATA,
                    ptrMftEnumData,
                    sizeMftEnumData,
                    ptrData,
                    ptrDataSize,
                    out outBytesCount,
                    IntPtr.Zero))
                {
                    IntPtr ptrUsnRecord = new IntPtr(ptrData.ToInt64() + sizeof(Int64));

                    while (outBytesCount > 60)
                    {
                        var usnRecord = new USN_RECORD_V2(ptrUsnRecord);

                        result.Add(new UsnEntry(usnRecord));

                        ptrUsnRecord = new IntPtr(ptrUsnRecord.ToInt64() + usnRecord.RecordLength);

                        outBytesCount -= usnRecord.RecordLength;
                    }

                    Marshal.WriteInt64(ptrMftEnumData, Marshal.ReadInt64(ptrData, 0));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptrData);
                Marshal.FreeHGlobal(ptrMftEnumData);
            }

            return result;
        }

        private static IntPtr GetHeapGlobalPtr(int size)
        {
            IntPtr buffer = Marshal.AllocHGlobal(size);
            NativeMethods.ZeroMemory(buffer, size);

            return buffer;
        }

        private UsnErrorCode QueryUSNJournal()
        {
            int sizeUsnJournalData = Marshal.SizeOf(this.ntfsUsnJournalData);

            USN_JOURNAL_DATA tempUsnJournalData;

            uint bytesReturnedCount;

            bool isSuccess = NativeMethods.DeviceIoControl(
                this.DriveRootHandle,
                NativeConstants.FSCTL_QUERY_USN_JOURNAL,
                IntPtr.Zero,
                0,
                out tempUsnJournalData,
                sizeUsnJournalData,
                out bytesReturnedCount,
                IntPtr.Zero);

            this.ntfsUsnJournalData = tempUsnJournalData;

            return (UsnErrorCode)Marshal.GetLastWin32Error();
        }

        private IntPtr GetRootHandle()
        {
            string volume = string.Format("\\\\.\\{0}:", this.DriveLetter);

            var result = NativeMethods.CreateFile(
                volume,
                NativeConstants.GENERIC_READ | NativeConstants.GENERIC_WRITE,  // 需要读写权限来访问 USN Journal
                NativeConstants.FILE_SHARE_READ | NativeConstants.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeConstants.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            // 使用 ToInt64 以支持 64 位系统
            if (result == IntPtr.Zero || result.ToInt64() == NativeConstants.INVALID_HANDLE_VALUE)
            {
                int errorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                throw new IOException($"Drive returned invalid root handle (Error: {errorCode})", new Win32Exception(errorCode));
            }

            return result;
        }

        public void Dispose()
        {
            if (this.DriveRootHandle != IntPtr.Zero && this.DriveRootHandle.ToInt64() != NativeConstants.INVALID_HANDLE_VALUE)
            {
                NativeMethods.CloseHandle(this.DriveRootHandle);
            }
        }
    }
}
