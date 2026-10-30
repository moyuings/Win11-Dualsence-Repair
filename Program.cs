using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

public class AudioBackup {
    public string Id;
    public float Volume;
    public bool Muted;
}
[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface DeviceCollection {
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int Item(uint index, out IMMDevice device);
}
static class Controller {
    static readonly Guid HidGuid = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
    [StructLayout(LayoutKind.Sequential)] struct InterfaceData {
        public int Size; public Guid Guid; public int Flags; public IntPtr Reserved;
    }
    [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll", SetLastError=true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, uint index, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint needed, IntPtr info);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool WriteFile(SafeFileHandle device, byte[] buffer, uint length, out uint written, IntPtr overlapped);
    static bool Send(SafeFileHandle device, byte[] packet) {
        uint written;
        return WriteFile(device,packet,(uint)packet.Length,out written,IntPtr.Zero) && written==packet.Length;
    }

    public static string FindUsb() {
        Guid guid=HidGuid;
        IntPtr set=SetupDiGetClassDevs(ref guid,IntPtr.Zero,IntPtr.Zero,0x12);
        if(set == new IntPtr(-1)) throw new Exception("无法枚举 USB 手柄。");
        var found=new List<string>();
        try {
            for(uint i=0;;i++) {
                var item=new InterfaceData(); item.Size=Marshal.SizeOf(item);
                if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref item)) {
                    int error=Marshal.GetLastWin32Error();
                    if(error!=259) throw new System.ComponentModel.Win32Exception(error);
                    break;
                }
                uint size;
                SetupDiGetDeviceInterfaceDetail(set,ref item,IntPtr.Zero,0,out size,IntPtr.Zero);
                if(size<8) continue;
                IntPtr detail=Marshal.AllocHGlobal((int)size);
                try {
                    Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);
                    if(!SetupDiGetDeviceInterfaceDetail(set,ref item,detail,size,out size,IntPtr.Zero))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
                    string lower=path.ToLowerInvariant();
                    if(lower.Contains("vid_054c&pid_0ce6") && lower.Contains("mi_03")) found.Add(path);
                } finally { Marshal.FreeHGlobal(detail); }
            }
        } finally { SetupDiDestroyDeviceInfoList(set); }
        if(found.Count!=1) throw new Exception("需要且只能连接一个 USB DualSense；当前匹配数量："+found.Count);
        return found[0];
    }
    public static byte[] Packet(bool vibrating) {
        byte[] data=new byte[48]; data[0]=2;
        if(vibrating) { data[1]=3; data[3]=180; data[4]=180; }
        return data;
    }
    public static void Output(bool test) {
        string path=FindUsb();
        using(var handle=CreateFile(path,0xC0000000,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
            if(handle.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try {
                if(test) {
                    if(!Send(handle,Packet(true))) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    Thread.Sleep(800);
                }
            } finally {
                if(!Send(handle,Packet(false))) throw new Exception("停止/释放指令失败，请拔插手柄以结束输出。");
            }
        }
    }
    public static string FindAudio() {
        var enumerator=(IMMEnumerator)new MMEnumerator();
        IntPtr pointer=IntPtr.Zero;
        DeviceCollection collection=null;
        try {
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(0,1,out pointer));
            collection=(DeviceCollection)Marshal.GetObjectForIUnknown(pointer);
            uint count; Marshal.ThrowExceptionForHR(collection.GetCount(out count));
            var ids=new List<string>();
            for(uint i=0;i<count;i++) {
                IMMDevice device; Marshal.ThrowExceptionForHR(collection.Item(i,out device));
                try {
                    string id; Marshal.ThrowExceptionForHR(device.GetId(out id));
                    int begin=id.LastIndexOf('{');
                    if(begin<0) continue;
                    using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\"+id.Substring(begin)+@"\Properties")) {
                        string hardware=key==null?null:key.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},2") as string;
                        if(hardware!=null && hardware.IndexOf(@"USB\VID_054C&PID_0CE6&MI_00",StringComparison.OrdinalIgnoreCase)>=0) ids.Add(id);
                    }
                } finally { Marshal.ReleaseComObject(device); }
            }
            if(ids.Count!=1) throw new Exception("没有找到唯一且已启用的 DualSense 播放设备。请检查声音设置，不要禁用手柄扬声器。");
            return ids[0];
        } finally {
            if(collection!=null) Marshal.ReleaseComObject(collection);
            if(pointer!=IntPtr.Zero) Marshal.Release(pointer);
            Marshal.ReleaseComObject(enumerator);
        }
    }
    public static AudioBackup Volume(string id, AudioBackup value) {
        var enumerator=(IMMEnumerator)new MMEnumerator(); IMMDevice device=null; object obj=null;
        try {
            Marshal.ThrowExceptionForHR(enumerator.GetDevice(id,out device));
            Guid iid=typeof(IEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid,23,IntPtr.Zero,out obj));
            var volume=(IEndpointVolume)obj;
            var result=new AudioBackup(); result.Id=id;
            Marshal.ThrowExceptionForHR(volume.GetMasterVolumeLevelScalar(out result.Volume));
            Marshal.ThrowExceptionForHR(volume.GetMute(out result.Muted));
            if(value!=null) {
                Guid context=Guid.Empty;
                Marshal.ThrowExceptionForHR(volume.SetMasterVolumeLevelScalar(value.Volume,ref context));
                Marshal.ThrowExceptionForHR(volume.SetMute(value.Muted,ref context));
            }
            return result;
        } finally {
            if(obj!=null) Marshal.ReleaseComObject(obj);
            if(device!=null) Marshal.ReleaseComObject(device);
            Marshal.ReleaseComObject(enumerator);
        }
    }
}
class RepairWindow:Form {
    readonly TextBox log=new TextBox();
    readonly string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AC8DualSenseRepair");
    string BackupFile { get { return Path.Combine(folder,"audio-backup.xml"); } }
    public RepairWindow() {
        Text="AC8 · DualSense 震动恢复工具 v0.3（原生触觉通道实验版）";
        ClientSize=new Size(760,490); MinimumSize=new Size(700,480);
        Font=new Font("Microsoft YaHei UI",10); StartPosition=FormStartPosition.CenterScreen;
        var description=new Label { Text="用于 USB 直连 DualSense。修复运行时输出模式及手柄音量。\n不会修改游戏、存档、驱动或默认音频设备；是否恢复游戏震动需进任务验证。", AutoSize=false, Dock=DockStyle.Top,Height=65,Padding=new Padding(12) };
        var actions=new FlowLayoutPanel {Dock=DockStyle.Top,Height=155,Padding=new Padding(8)};
        AddButton(actions,"1. 检查设备",delegate { Diagnose(); });
        AddButton(actions,"2. 尝试恢复输出",delegate { Repair(); });
        AddButton(actions,"普通震动测试（非高清触觉）",delegate {
            Controller.Output(true);
            Write("震动与释放指令已发送。请确认是否实际震动；这不代表 AC8 已修复。");
        });
        AddButton(actions,"恢复 PS5 原生触觉通道",delegate { Write(NativeHaptics.Run(false)); Repair(); Write("已释放普通震动占用并恢复手柄音频输出；不会伪造游戏原生触觉。请在游戏设置启用触觉反馈，进任务验证。"); });
        AddButton(actions,"PS5 高清触觉测试（左→右）",delegate { Write(NativeHaptics.Run(true)); });
        AddButton(actions,"恢复原手柄音量",delegate { Restore(); });
        AddButton(actions,"打开声音设置",delegate { Process.Start("control.exe","mmsys.cpl,,0"); });
        log.Multiline=true; log.ReadOnly=true; log.ScrollBars=ScrollBars.Vertical; log.Dock=DockStyle.Fill;
        log.BackColor=Color.FromArgb(245,247,250);
        Controls.Add(log); Controls.Add(actions); Controls.Add(description);
        Shown+=delegate { Run(delegate { Diagnose(); }); };
    }
    void AddButton(FlowLayoutPanel panel,string text,Action action) {
        var button=new Button {Text=text,AutoSize=true,Height=36,Margin=new Padding(4),Padding=new Padding(6)};
        button.Click+=delegate { Run(action); }; panel.Controls.Add(button);
    }
    void Run(Action action) {
        Cursor=Cursors.WaitCursor;
        try { action(); } catch(Exception ex) { Write("未完成："+ex.Message); }
        finally {Cursor=Cursors.Default;}
    }
    void Write(string message) { log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+message+Environment.NewLine); }
    void Diagnose() {
        Controller.FindUsb(); Write("检测到一个 USB DualSense。");
        var volume=Controller.Volume(Controller.FindAudio(),null);
        Write("手柄播放设备已启用，音量 "+(int)(volume.Volume*100)+"%，静音="+volume.Muted+"。");
        Write("下一步可尝试恢复，再回游戏测试。工具不会替游戏生成震动效果。");
    }
    internal void Repair() {
        Controller.FindUsb();
        string id=Controller.FindAudio();
        var before=Controller.Volume(id,null);
        Directory.CreateDirectory(folder);
        if(File.Exists(BackupFile)) {
            AudioBackup prior; using(var file=File.OpenRead(BackupFile)) prior=(AudioBackup)new XmlSerializer(typeof(AudioBackup)).Deserialize(file);
            if(prior.Id!=id) throw new Exception("保留着另一个音频设备的备份。请先恢复原手柄音量。");
        } else {
            using(var file=new FileStream(BackupFile,FileMode.CreateNew)) new XmlSerializer(typeof(AudioBackup)).Serialize(file,before);
        }
        try {
            Controller.Output(false);
            Controller.Volume(id,new AudioBackup {Id=id,Volume=1,Muted=false});
            var after=Controller.Volume(id,null);
            if(after.Muted || Math.Abs(after.Volume-1)>.01) throw new Exception("音量设置读回不一致。");
        } catch {
            try {Controller.Volume(id,before);} catch { }
            throw;
        }
        Write("已释放普通震动模式，手柄播放音量已设为 100% 并取消静音。");
        Write("原音量已备份，可随时恢复。现在请进 AC8 任务确认实际震动；结果尚未验证。");
    }
    internal void Restore() {
        if(!File.Exists(BackupFile)) {Write("没有待恢复的音量备份。");return;}
        AudioBackup backup;
        using(var file=File.OpenRead(BackupFile)) backup=(AudioBackup)new XmlSerializer(typeof(AudioBackup)).Deserialize(file);
        Controller.Volume(backup.Id,backup);
        var after=Controller.Volume(backup.Id,null);
        if(after.Muted!=backup.Muted || Math.Abs(after.Volume-backup.Volume)>.01) throw new Exception("恢复读回不一致，备份仍保留。");
        File.Move(BackupFile,BackupFile+".restored-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));
        Write("已恢复修复前的手柄音量和静音状态。");
    }
    [STAThread] static int Main(string[] args) {
        if(args.Length>0 && args[0]=="--native-check") { try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native-check.txt"),NativeHaptics.Run(false),Encoding.UTF8); return 0; } catch(Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native-check.txt"),ex.ToString(),Encoding.UTF8); return 1; } }
        if(args.Length>0 && args[0]=="--check") {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"check-result.txt");
            try {
                if(Controller.Packet(true).Length!=48 || Controller.Packet(false)[1]!=0) throw new Exception("Packet self check failed");
                Controller.FindUsb();
                var v=Controller.Volume(Controller.FindAudio(),null);
                File.WriteAllText(path,"USB DualSense found; audio volume="+v.Volume+"; mute="+v.Muted+"; no output or settings changed.",Encoding.UTF8);
                return 0;
            } catch(Exception ex) {File.WriteAllText(path,ex.ToString(),Encoding.UTF8);return 1;}
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new RepairWindow()); return 0;
    }
}

