using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ifc2gbXMLConverter
{
    public class ifc2gbXML
    {
        private static Logger logger = new Logger("ifc2gbXML");

        [DllImport("USER32.DLL")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("USER32.DLL", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private string revitPath;

        private const int HIDE = 0x5;

        public ifc2gbXML(string revitPath)
        {
            this.revitPath = revitPath;
        }

        private void tryToKill(Process process)
        {
            try
            {
                logger.log("Closing process '" + process.ProcessName + "' with id " + process.Id);
                process.Kill();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                logger.log(ex.Message, Logger.LogType.ERROR);
            }
        }

        private bool Convert_(string journal,string inFile,string outFile)
        {
            bool isDirectory = Directory.Exists(outFile);
            if (isDirectory)
            {
                outFile = outFile + "\\" + Path.ChangeExtension(Path.GetFileName(inFile), "xml");
            }
            string appPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string tmpIfclFile = appPath + @"\ifc2gbXML\in.ifc";
            string tmpXmlFile = appPath + @"\ifc2gbXML\out.xml";
            IntPtr hWnd = IntPtr.Zero;
            IntPtr hWndJ = IntPtr.Zero;
            DateTime t1;
            double deltaT;
            int timeOut = 240;
            Process process;
            if (!Directory.Exists(Path.GetDirectoryName(tmpIfclFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(tmpIfclFile));
            }
            if (File.Exists(tmpIfclFile))
            {
                File.Delete(tmpIfclFile);
            }
            if (File.Exists(tmpXmlFile))
            {
                File.Delete(tmpXmlFile);
            }
            if (File.Exists(outFile))
            {
                File.Delete(outFile);
            }
            File.Copy(inFile, tmpIfclFile);
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = this.revitPath + @"\Revit.exe";
            startInfo.Arguments = journal + " /nosplash";
            process = Process.Start(startInfo);
            logger.log("Process '" + process.ProcessName + "' with id " + process.Id+" started");
            logger.log("Importing IFC file...");
            do
            {
                Thread.Sleep(1);
                ShowWindow(process.MainWindowHandle, HIDE);
                hWnd = FindWindow(null, "Export gbXML - Settings");
                hWndJ = FindWindow(null, "Journal Error");
                if (hWndJ != IntPtr.Zero)
                {
                    logger.log("Error importing IFC file", Logger.LogType.ERROR);
                    tryToKill(process);
                    return false;
                }

            } while (hWnd == IntPtr.Zero);
            logger.log("Generating gbXML file...");
            t1 = DateTime.Now;
            do
            {
                Thread.Sleep(1);
                hWnd = FindWindow(null, "Export gbXML - Settings");
                ShowWindow(hWnd, HIDE);
                deltaT = (DateTime.Now - t1).TotalSeconds;
                hWndJ = FindWindow(null, "Journal Error");
                if (hWndJ != IntPtr.Zero)
                {
                    logger.log("Error generating gbXML file", Logger.LogType.ERROR);
                    tryToKill(process);
                    return false;
                }

            } while (hWnd != IntPtr.Zero && deltaT < timeOut);

            if (!process.HasExited)
            {
                tryToKill(process);
            }

            if (deltaT < timeOut)
            {
                File.Copy(tmpXmlFile, outFile);
                return true;
            }

            return false;

        }

        public bool Convert(string inFile,string outFile)
        {
            Process[] processes;
            try
            {
                string[] journals = new string[] { "ifc2gbXML.txt", "ifc2gbXMLNoJoin.txt", "ifc2gbXMLNoPopUp.txt" };

                processes = Process.GetProcessesByName("Revit");
                for (var i = 0; i < processes.Length; i++)
                {
                    tryToKill(processes[i]);
                }
                for (var i = 0; i < journals.Length; i++)
                {
                    logger.log("Launching Revit with journal file " + journals[i]);
                    if (Convert_(journals[i], inFile, outFile))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch(Exception e)
            {
                logger.log(e.Message, Logger.LogType.ERROR);
                processes = Process.GetProcessesByName("Revit");
                for (var i = 0; i < processes.Length; i++)
                {
                    tryToKill(processes[i]);
                }
                return false;
            }
        }

    }
}
