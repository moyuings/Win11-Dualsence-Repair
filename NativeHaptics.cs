using System;
using System.Runtime.InteropServices;
using System.Threading;

[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface NativeAudioClient {
 [PreserveSig] int Initialize(int mode,uint flags,long duration,long period,IntPtr format,IntPtr session);
 [PreserveSig] int GetBufferSize(out uint frames);
 [PreserveSig] int GetStreamLatency(out long latency);
 [PreserveSig] int GetCurrentPadding(out uint frames);
 [PreserveSig] int IsFormatSupported(int mode,IntPtr format,out IntPtr closest);
 [PreserveSig] int GetMixFormat(out IntPtr format);
 [PreserveSig] int GetDevicePeriod(out long normal,out long minimum);
 [PreserveSig] int Start();
 [PreserveSig] int Stop();
 [PreserveSig] int Reset();
 [PreserveSig] int SetEventHandle(IntPtr handle);
 [PreserveSig] int GetService(ref Guid iid,[MarshalAs(UnmanagedType.IUnknown)] out object service);
}
[ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface NativeRenderClient {
 [PreserveSig] int GetBuffer(uint frames,out IntPtr data);
 [PreserveSig] int ReleaseBuffer(uint frames,uint flags);
}
static class NativeHaptics {
 static void Check(int hr) { Marshal.ThrowExceptionForHR(hr); }
 public static string Run(bool play) {
  Controller.FindUsb();
  var enumerator=(IMMEnumerator)new MMEnumerator(); IMMDevice device=null;
  object clientObject=null,renderObject=null; IntPtr format=IntPtr.Zero;
  NativeAudioClient client=null; bool started=false;
  try {
   Check(enumerator.GetDevice(Controller.FindAudio(),out device));
   Guid iid=typeof(NativeAudioClient).GUID;
   Check(device.Activate(ref iid,23,IntPtr.Zero,out clientObject));
   client=(NativeAudioClient)clientObject; Check(client.GetMixFormat(out format));
   int tag=(ushort)Marshal.ReadInt16(format,0),channels=(ushort)Marshal.ReadInt16(format,2);
   int rate=Marshal.ReadInt32(format,4),align=(ushort)Marshal.ReadInt16(format,12),bits=(ushort)Marshal.ReadInt16(format,14);
   bool floating=tag==3;
   if(tag==65534) {
    Guid sub=(Guid)Marshal.PtrToStructure(IntPtr.Add(format,24),typeof(Guid));
    floating=sub==new Guid("00000003-0000-0010-8000-00aa00389b71");
    if(!floating && sub!=new Guid("00000001-0000-0010-8000-00aa00389b71")) throw new Exception("不支持此音频子格式。");
   } else if(tag!=1 && tag!=3) throw new Exception("不支持此音频格式。");
   if(channels!=4 || rate<8000 || rate>192000 || align!=4*(bits/8) || (floating?bits!=32:(bits!=16 && bits!=32)))
    throw new Exception("原生触觉需要四声道 PCM/Float 播放格式；当前："+channels+"声道 / "+rate+"Hz / "+bits+"bit。请在声音设置检查手柄设备。");
   string result="原生触觉端点：4 声道 / "+rate+"Hz / "+bits+"bit；WASAPI 共享模式。";
   if(!play) return result+" 尚未输出测试信号。";
   Controller.Output(false);
   Check(client.Initialize(0,0,1000000,0,format,IntPtr.Zero));
   uint size; Check(client.GetBufferSize(out size));
   iid=typeof(NativeRenderClient).GUID; Check(client.GetService(ref iid,out renderObject));
   var render=(NativeRenderClient)renderObject;
   int position=0,total=rate*2; Check(client.Start()); started=true;
   var watch=System.Diagnostics.Stopwatch.StartNew();
   while(position<total) {
    if(watch.ElapsedMilliseconds>6000) throw new Exception("触觉测试超时，已停止输出。");
    uint padding; Check(client.GetCurrentPadding(out padding));
    uint count=Math.Min(size-padding,(uint)(total-position));
    if(count==0) {Thread.Sleep(4); continue;}
    byte[] bytes=new byte[checked((int)count*align)];
    for(int n=0;n<(int)count;n++) {
     double t=(position+n)/(double)rate;
     // Left grip, silence, right grip. Front speaker channels always remain zero.
     int channel=t<0.65?2:(t>=1.0 && t<1.65?3:-1);
     if(channel<0) continue;
     double local=t<0.65?t:t-1;
     double envelope=Math.Min(1,Math.Min(local/0.03,(0.65-local)/0.03));
     double value=0.18*envelope*Math.Sin(2*Math.PI*(t<0.65?95:160)*local);
     byte[] sample=floating?BitConverter.GetBytes((float)value):(bits==16?BitConverter.GetBytes((short)(value*32767)):BitConverter.GetBytes((int)(value*2147483647)));
     Buffer.BlockCopy(sample,0,bytes,n*align+channel*(bits/8),sample.Length);
    }
    IntPtr buffer; Check(render.GetBuffer(count,out buffer));
    try {Marshal.Copy(bytes,0,buffer,bytes.Length);} finally {Check(render.ReleaseBuffer(count,0));}
    position+=(int)count;
   }
   uint remaining;
   do {Check(client.GetCurrentPadding(out remaining)); if(remaining>0) Thread.Sleep(4);}
   while(remaining>0 && watch.ElapsedMilliseconds<6000);
   return result+" 左握把低频 → 右握把高频测试已发送；请以实际手感确认。游戏原生效果仍需进任务验证。";
  } finally {
   if(started && client!=null) client.Stop();
   if(renderObject!=null) Marshal.ReleaseComObject(renderObject);
   if(format!=IntPtr.Zero) Marshal.FreeCoTaskMem(format);
   if(clientObject!=null) Marshal.ReleaseComObject(clientObject);
   if(device!=null) Marshal.ReleaseComObject(device);
   Marshal.ReleaseComObject(enumerator);
  }
 }
}
