using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace array_sensor;

//只能在后台线程调用
internal partial class CH9140
{
    // 定义回调函数的委托类型
    public delegate void pFunReadCallBack(IntPtr ParamInf, IntPtr ReadBuf, uint ReadBufLen);
    public delegate void pFunDevConnChangeCallBack(IntPtr hDev, byte ConnectStatus);
    public delegate void pFunRSSICallBack(string pMAC, int rssi, byte ChipVer);
    public delegate void pFunRecvModemCallBack(IntPtr hDev, bool DCD, bool RI, bool DSR, bool CTS);

    // 定义结构体类型
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct BLENameDevID
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DevID;
        public int Rssi;
        public byte ChipVer;
    }

    // 加载dll文件
    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial void CH9140Init();

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CH9140IsBluetoothOpened();

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial byte CH9140GetBluetoothVer();

    [DllImport("Services/CH9140DLL.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern void CH9140EnumDevice(uint scanTimes, string DevNameFilter, out BLENameDevID[] pBLENameDevIDArry, out ulong pNum);

    [LibraryImport("Services/CH9140DLL.dll", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial IntPtr CH9140UartOpenDevice(string DevID, pFunDevConnChangeCallBack pFun, pFunRecvModemCallBack pModem, pFunReadCallBack pRead);

    [LibraryImport("Services/CH9140DLL.dll", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial int CH9140UartWriteBuffer(IntPtr DevHandle, string buf, int buflen);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CH9140UartSetSerialBaud(IntPtr DevHandle, int baudRate, int dataBit, int stopBit, int parity);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CH9140UartSetSerialModem(IntPtr DevHandle, [MarshalAs(UnmanagedType.Bool)] bool flow, int DTR, int RTS);

    [LibraryImport("Services/CH9140DLL.dll", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial IntPtr CH9140OpenDevice(string deviceID, pFunDevConnChangeCallBack pFunDevConnChange);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial void CH9140CloseDevice(IntPtr pDev);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial byte CH9140GetAllOpHandle(IntPtr pDev, [Out] ushort[] pHandleArry, out ushort pHandleArryLen);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial byte CH9140GetHandleAction(IntPtr pDev, ushort AttributeHandle, out uint pAction);

    [LibraryImport("Services/CH9140DLL.dll", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial byte CH9140WriteBuffer(IntPtr pDev, ushort AttributeHandle, [MarshalAs(UnmanagedType.Bool)] bool bWriteWithResponse, string buffer, uint length);

    [LibraryImport("Services/CH9140DLL.dll", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial byte CH9140ReadBuffer(IntPtr pDev, ushort AttributeHandle, out string buffer, out uint pLength);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial byte CH9140RegisterReadNotify(IntPtr pDev, ushort AttributeHandle, pFunReadCallBack pFun, IntPtr paramInf);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial byte CH9140RegisterRSSINotify(pFunRSSICallBack pFun);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial byte CH9140GetMtu(IntPtr pDev, out ushort pMTU);

    [LibraryImport("Services/CH9140DLL.dll", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CH9140GetCfgPara(IntPtr pDev, out string buf);

    [LibraryImport("Services/CH9140DLL.dll", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CH9140SetCfgPara(IntPtr pDev, string buf);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CH9140ResetCfgPara(IntPtr pDev);

    [LibraryImport("Services/CH9140DLL.dll", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CH9140GetExdCfg(IntPtr pDev, out string buf);

    [LibraryImport("Services/CH9140DLL.dll", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CH9140SetExdCfg(IntPtr pDev, string buf);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CH9140ResetExdCfg(IntPtr pDev);

    [LibraryImport("Services/CH9140DLL.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CH9140ResetCh(IntPtr pDev);
}
