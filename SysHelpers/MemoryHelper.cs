﻿using RockSnifferLib.RSHelpers;
using RockSnifferLib.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
namespace RockSnifferLib.SysHelpers
{
    public struct ProcessInfo
    {
        //Process handles
        public Process rsProcess;
        public IntPtr rsProcessHandle;
        public ulong PID;
        /* mac os task port */
        public uint Task;
    }
    class MemoryHelper
    {
        /* Scan Memory pointed by ptr for the magic int  */
        public static ulong ScanMem(ProcessInfo pInfo, IntPtr ptr, int bytesRead, ulong dataIndex, int magic)
        {
            return MacOSAPI.scan_mem(pInfo.Task, (ulong)ptr, (ulong)bytesRead, dataIndex, magic);
        }
        /// <summary>
        /// Read a number of bytes from a processes memory into given byte array buffer
        /// </summary>
        /// <param name="processHandle"></param>
        /// <param name="address"></param>
        /// <param name="bytes"></param>
        /// <returns>bytes read</returns>
        public static int ReadBytesFromMemory(ProcessInfo pInfo, IntPtr address, int bytes, ref byte[] buffer)
        {
            int bytesRead = 0;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                case PlatformID.Unix:
                    IntPtr ptr;
                    int ret = MacOSAPI.vm_read_wrapper(pInfo.Task, (ulong)address, (ulong)bytes, out ptr, out bytesRead);
                    if (ret == 0)
                    {
                        Marshal.Copy(ptr, buffer, 0, bytesRead);
                        MacOSAPI.vm_deallocate_wrapper(pInfo.Task, (ulong)ptr, (ulong)bytesRead);
                    }
                    break;
                default:
                    Win32API.ReadProcessMemory((int)pInfo.rsProcessHandle, (int)address, buffer, bytes, ref bytesRead);
                    break;
            }
            return bytesRead;
        }

        /// <summary>
        /// Read a number of bytes from a processes memory into a byte array
        /// </summary>
        /// <param name="pInfo"></param>
        /// <param name="address"></param>
        /// <param name="bytes"></param>
        /// <returns>bytes read</returns>
        public static byte[] ReadBytesFromMemory(ProcessInfo pInfo, IntPtr address, int bytes)
        {
            int bytesRead = 0;
            byte[] buf = new byte[bytes];

            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                case PlatformID.Unix:
                    IntPtr ptr;
                    int ret = MacOSAPI.vm_read_wrapper(pInfo.Task, (ulong)address, (ulong)bytes, out ptr, out bytesRead);
                    //Logger.Log(bytes.ToString() + " " + bytesRead.ToString() + " " + ret.ToString());
                    if (ret == 0)
                    {
                        Marshal.Copy(ptr, buf, 0, bytesRead);
                        MacOSAPI.vm_deallocate_wrapper(pInfo.Task, (ulong)ptr, (ulong)bytesRead);
                    }
                    MacOSAPI.free_wrapper(ptr);
                    break;
                default:
                    Win32API.ReadProcessMemory((int)pInfo.rsProcessHandle, (int)address, buf, bytes, ref bytesRead);
                    break;
            }
            return buf;
        }

        /// <summary>
        /// Reads an Int32 from a processes memory
        /// </summary>
        /// <param name="processHandle"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        public static int ReadInt32FromMemory(ProcessInfo processHandle, IntPtr address)
        {
            return BitConverter.ToInt32(ReadBytesFromMemory(processHandle, address, 4), 0);
        }

        /// <summary>
        /// Reads a single byte from a processes memory
        /// </summary>
        /// <param name="processHandle"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        public static byte ReadByteFromMemory(ProcessInfo processHandle, IntPtr address)
        {
            return ReadBytesFromMemory(processHandle, address, 1)[0];
        }

        /// <summary>
        /// Follows a pointer by reading destination address from process memory and applying offset
        /// <para></para>
        /// Returns the new pointer
        /// </summary>
        /// <param name="processHandle"></param>
        /// <param name="address"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static IntPtr FollowPointer(ProcessInfo processHandle, IntPtr address, int offset)
        {
            //Logger.Log(string.Format("PreFollow Pointer: Address:{0} offset: {1}", address.ToString("X8"), offset));
            IntPtr readPointer = (IntPtr)ReadInt32FromMemory(processHandle, address);

            return IntPtr.Add(readPointer, offset);
        }

        /// <summary>
        /// Reads a float from a processes memory
        /// </summary>
        /// <param name="processHandle"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        public static float ReadFloatFromMemory(ProcessInfo processHandle, IntPtr address)
        {
            return BitConverter.ToSingle(ReadBytesFromMemory(processHandle, address, 4), 0);
        }

        /// <summary>
        /// get user_tag associated with a memory region
        /// </summary>
        /// <param name="pInfo"></param>
        /// <param name="Address"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static UInt32 GetUserTag(ProcessInfo pInfo, ulong Address, ulong size)
        {
            UInt32 userTag = 0;
            int ret = MacOSAPI.mach_vm_region_recurse_wrapper(pInfo.Task, out Address, out size, out userTag);
            return userTag;
        }

        /// <summary>
        /// get all memory regions of a process
        /// </summary>
        /// <param name="pInfo"></param>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static List<MacOSAPI.Region> GetAllRegions(ProcessInfo pInfo, ulong begin, ulong end)
        {
            List<MacOSAPI.Region> Regions = new List<MacOSAPI.Region>();
            ulong address = 0;
            while (true)
            {
                ulong size = 0;
                int protection = 0;
                int ret = MacOSAPI.mach_vm_region_wrapper(pInfo.Task, out address, out size, out protection);
                if (ret != 0)
                    break;
                //Logger.Log(string.Format("Ret: {0} Address: {1} Size: {2}", ret, address, size));
                MacOSAPI.Region reg = new MacOSAPI.Region()
                {
                    Address = address,
                    Size = size,
                    Protection = protection
                };
                if (reg.Address < end && (reg.Address + size) > begin && ((reg.Protection & 0x02) == 2)) /* writable protection */
                    Regions.Add(reg);
                address += size;
            }
            return Regions;
        }
    }
}
