using ApcUpsLogParser.UI;

namespace ApcUpsLogParser
{
    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // Enable visual styles and proper text rendering for Unicode/emoji support
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Set the application's default font to support emojis
            Application.SetDefaultFont(new System.Drawing.Font("Segoe UI Emoji", 9F));

#if DEBUG
            AllocConsole();
#endif

            var mainForm = new MainForm();
            Application.Run(mainForm);
            
            return 0;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool AllocConsole();
    }
}
