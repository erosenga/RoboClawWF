using CommandMessenger.Transport.Serial;
using System;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace StepperWF
{
    public class MacroRunner
    {
        public string CurrentMacro;
        public SerialTransport serialPort;
        public Socket MRSocket = null;
        StreamReader fs = null;
        NetworkStream ns = null;
        StepperController controller = null;

        public MacroRunner(StepperController sc, string filename)
        {
            serialPort = sc._serialTransport;
            CurrentMacro = filename;
            fs = new StreamReader(CurrentMacro);
            controller = sc;
        }
        public MacroRunner(StepperController sc, Socket socket)
        {
            serialPort = sc._serialTransport;
            CurrentMacro = null;
            MRSocket = socket;
            ns = new NetworkStream(socket);
            controller = sc;
        }




        public string readLine()
        {
            if (CurrentMacro == null)
            {
                byte[] myReadBuffer = new byte[2];
                string line = "";


                while (true)
                {
                    int numberOfBytesRead = ns.Read(myReadBuffer, 0, 1);
                    if (numberOfBytesRead > 0)
                    {
                        if (myReadBuffer[0] == '\n')
                        {
                            break;
                        }
                        else
                        if (myReadBuffer[0] == '\r')
                        {
                            //swallow CR
                        }
                        else
                        if (myReadBuffer[0] == 0x03) //EOF
                            return null;
                        else
                            line += myReadBuffer[0];
                    }
                }
                return line;
            }
            else
                return fs.ReadLine();
        }

        public void RunMacro()
        {

            //  Read in macro stream

            byte[] b = new byte[1024];
            System.Text.UTF8Encoding temp = new System.Text.UTF8Encoding(true);
            string line;
            while ((line = readLine()) != null)
            {
                // "Nested" macro calling
                if (line.StartsWith("@"))
                {
                    MacroRunner macroRunner = new MacroRunner(controller, line.Substring(1));
                    macroRunner.RunMacro();
                    continue;
                }
                // Wait for fixed time
                if (line.StartsWith("SLEEP"))
                {
                    int delay = 0;
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    if (string.IsNullOrWhiteSpace(parsedLine[0])) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        delay = Int32.Parse(parsedLine[1]);
                    Thread.Sleep(delay);
                    continue;
                }
                // Wait until status is idle
                if (line.StartsWith("WAIT"))
                {
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    if (string.IsNullOrWhiteSpace(parsedLine[0])) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                    {
                        bool motionDone = false;
                        do
                        {
                            Int32.Parse(parsedLine[1]);
                            //serialPort.WriteLine( "/Q" + parsedLine[1] + "R" );
                            Thread.Sleep(100);
                            CommandMessenger.ReceivedCommand responseCmd;

                            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand(7, 7,
                                                     controller.commandStructure[7].timeout);
                            responseCmd = controller._cmdMessenger.SendCommand(cmd);
                            string[] line2 = responseCmd.RawString.TrimEnd('\r', '\n').Split(',');
                            if (line2.Length<3 || line2[2][0]=='1') continue; //isolate status
                            motionDone = true;
                        } while (!motionDone);

                    }
                    continue;
                }
                // Pop up MessageBox
                if (line.StartsWith("ALERT"))
                {
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    if (string.IsNullOrWhiteSpace(parsedLine[0])) //Disregard blanks lines
                        continue;

                    if (parsedLine[1] != null)
                    {
                        MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                        DialogResult result;
                        result = MessageBox.Show(parsedLine[1], "Alert!", buttons);
                        continue;
                    }
                }

                //Actual command
                string[] lin2 = line.Split('#'); //kill comments
                if (!string.IsNullOrWhiteSpace(lin2[0]))
                {
                    string[] lin1 = lin2[0].Split(','); //split parameters
                    Int32 commandNumber = -1;
                    try
                    {
                        commandNumber = controller.CommandNumber[lin1[0]];
                    }
                    catch (Exception e)
                    {
                        // invalid command (not in dictionary)
                        Console.WriteLine(e.Message);
                    }

                    Int32 response1 = controller.commandStructure[commandNumber].response;
                    if (response1 < 0) response1 = commandNumber; //use default response
                    Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
                    CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand(commandNumber, response1,
                                                         controller.commandStructure[commandNumber].timeout);
                    for (Int32 pn = 0; pn < parametersRequired; pn++)
                    {
                        switch (controller.commandStructure[commandNumber].parameters[pn])
                        {
                            case 'i':
                                Int16 pi = Int16.Parse(lin1[pn + 1]);
                                cmd.AddArgument(pi);
                                break;
                            case 'l':
                                Int32 pl = Int32.Parse(lin1[pn + 1]);
                                cmd.AddArgument(pl);
                                break;
                            case 'b':
                                bool pb = bool.Parse(lin1[pn + 1]);
                                break;
                            case 's':
                                cmd.AddArgument(lin1[pn + 1]);
                                break;
                            default:
                                break;

                        }

                    }
                    cmd.ReqAc = true;
                    if (cmd.Ok)
                    {
                        CommandMessenger.ReceivedCommand responseCmd = controller._cmdMessenger.SendCommand(cmd);
                        if (responseCmd.RawString != null)
                            Console.WriteLine(String.Format(">>>>{0}<<<{1}", cmd.CmdId, responseCmd.RawString.Trim()));
                        else
                        { //received negative response Id... probably timeout
                            MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                            DialogResult result;
                            result = MessageBox.Show(String.Format("Command {0} Timeout??", cmd.CmdId), "Timeout!", buttons);
                            continue;
                        }


                        controller._cmdMessenger.ClearReceiveQueue();
                        controller._cmdMessenger.ClearSendQueue();
                    }
                    else
                        Console.WriteLine(string.Format("Unknown command {n} issued\n", cmd.CmdId));
                }
            }


        }

    }
}