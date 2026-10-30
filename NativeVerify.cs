using System; class NativeVerify { [STAThread] static int Main() { try { Console.WriteLine(NativeHaptics.Run(true)); return 0; } catch(Exception e) { Console.WriteLine(e); return 1; } } }
