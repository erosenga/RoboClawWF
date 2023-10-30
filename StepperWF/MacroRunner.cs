using CommandMessenger.Transport.Serial;
using System;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;



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

        public MacroRunner( StepperController sc, string filename, SerialTransport serialPortIn )
        {
            serialPort = serialPortIn;
            CurrentMacro = filename;
            fs = new StreamReader( CurrentMacro );
            controller = sc;
        }
        public MacroRunner( StepperController sc, Socket socket, SerialTransport serialPortIn )
        {
            serialPort = serialPortIn;
            CurrentMacro = null;
            MRSocket = socket;
            ns = new NetworkStream( socket );
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
                    int numberOfBytesRead = ns.Read( myReadBuffer, 0, 1 );
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
            System.Text.UTF8Encoding temp = new System.Text.UTF8Encoding( true );
            string line;
            string response = "";
            while ((line = readLine()) != null)
            {
                // "Nested" macro calling
                if (line.StartsWith( "@" ))
                {
                    MacroRunner macroRunner = new MacroRunner( controller, line.Substring( 1 ), serialPort );
                    macroRunner.RunMacro();
                    continue;
                }
                // Wait for fixed time
                if (line.StartsWith( "SLEEP" ))
                {
                    int delay = 0;
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        delay = Int32.Parse( parsedLine[1] );
                    Thread.Sleep( delay );
                    continue;
                }
                // Wait until status is idle
                if (line.StartsWith( "WAIT" ))
                {
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                    {
                        bool motionDone = false;
                        do
                        {
                            Int32.Parse( parsedLine[1] );
                            serialPort.WriteLine( "/Q" + parsedLine[1] + "R" );
                            Thread.Sleep( 100 );
                            byte c1;
                            do
                            {
                                c1 = (byte)serialPort.ReadByte();
                                response += c1;
                            } while (c1 != '\n');
                            if ((response.TrimEnd( '\r', '\n' )[2] & 0x40) != 0) continue; //isolate status byte, busy bit
                            motionDone = true;
                        } while (!motionDone);

                    }
                    continue;
                }
                // Pop up MessageBox
                if (line.StartsWith( "ALERT" ))
                {
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        continue;
                }

                //Actual command
                string[] lin2 = line.Split( '#' ); //kill comments
                if (!string.IsNullOrWhiteSpace( lin2[0] ))
                {
                    string[] lin1= line.Split( ',' ); //split parameters
                    Int32 commandNumber = -1;
                    try
                    {
                        commandNumber = controller.CommandNumber[lin1[0]];
                    }
                    catch(Exception e)
                    {
                        // invalid command (not in dictionary)
                    }
                    Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
                    CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand( commandNumber );
                    for (Int32 pn = 0 ; pn < parametersRequired ; pn++)
                    {
                        switch (controller.commandStructure[commandNumber].parameters[pn - 1])
                        {
                            case 'i':
                                Int16 pi = Int16.Parse( lin1[pn + 1] );
                                cmd.AddArgument( pi );
                                break;
                            case 'l':
                                Int32 pl = Int32.Parse( lin1[pn + 1] );
                                cmd.AddArgument( pl );
                                break;
                            case 'b':
                                bool pb = bool.Parse( lin1[pn + 1] );
                                break;
                            case 's':
                                cmd.AddArgument( lin1[pn + 1] );
                                break;
                            default:
                                break;

                        }

                    }
                    controller._cmdMessenger.SendCommand( cmd );

                    response = "";
                    do
                    {
                        byte RxBuffer = (byte)serialPort.ReadByte();
                        response += RxBuffer;
                        if (response.Contains( "\n" )) break;
                    } while (true);
                }
            }


        }

    }
}