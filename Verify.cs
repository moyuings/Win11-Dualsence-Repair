using System;
class Verify {
    [STAThread] static int Main() {
        try {
            Controller.Output(true);
            Console.WriteLine("USB pulse and stop accepted.");
            using(var form=new RepairWindow()) {
                form.Repair();
                Console.WriteLine("Repair and volume read-back passed.");
                form.Restore();
                Console.WriteLine("Original volume restoration passed.");
            }
            return 0;
        } catch(Exception ex) {Console.WriteLine(ex);return 1;}
    }
}
