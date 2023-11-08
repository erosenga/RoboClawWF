﻿
using System;
using System.Collections;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace RoboClawWF
{
    public class MacroRunner
    {
        public string CurrentMacro;
        public RoboclawClassLib.Roboclaw rc;
        public Socket MRSocket = null;
        StreamReader fs = null;
        NetworkStream ns = null;
        RoboClawController controller = null;
        UInt16 m_crc;

        public MacroRunner( RoboClawController sc, string filename )
        {
            rc = sc.rc;
            CurrentMacro = filename;
            fs = new StreamReader( CurrentMacro );
            controller = sc;
        }
        public MacroRunner( RoboClawController sc, Socket socket )
        {

            CurrentMacro = null;
            MRSocket = socket;
            ns = new NetworkStream( socket );
            controller = sc;
            rc = sc.rc;
            
        }

        private UInt16 crc_update( byte data )
        {
            int i;
            m_crc = (UInt16)(m_crc ^ ((UInt16)data << 8));
            for (i = 0 ; i < 8 ; i++)
            {
                if ((m_crc & 0x8000) != 0)
                    m_crc = (UInt16)((m_crc << 1) ^ 0x1021);
                else
                    m_crc <<= 1;
            }
            return m_crc;
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
            byte[] sendBuffer = new byte[1024];
            int byteCount = 0;
            while ((line = readLine()) != null)
            {
                // "Nested" macro calling
                if (line.StartsWith( "@" ))
                {
                    MacroRunner macroRunner = new MacroRunner( controller, line.Substring( 1 ) );
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
                    {
                        MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                        DialogResult result;
                        result = MessageBox.Show( parsedLine[1], "Alert!", buttons );
                        continue;
                    }
                }

                //Actual command
                string[] lin2 = line.Split( '#' ); //kill comments
                if (!string.IsNullOrWhiteSpace( lin2[0] ))
                {
                    string[] lin1 = lin2[0].Split( ',' ); //split parameters
                    Int32 commandNumber = -1;
                    try
                    {
                        commandNumber = controller.CommandNumber[lin1[0]];
                    }
                    catch (Exception e)
                    {
                        // invalid command (not in dictionary)
                        Console.WriteLine( e.Message );
                    }
                    Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
                    ArrayList args=new ArrayList();
                    byteCount = 0;
                    sendBuffer[byteCount++] = 0x80; //address
                    sendBuffer[byteCount++] = (byte)commandNumber; //command (1 byte)
                    for (Int32 pn = 0 ; pn < parametersRequired ; pn++)
                    {
                        switch (controller.commandStructure[commandNumber].parameters[pn])
                        {
                            case 'i':
                                Int16 pi = Int16.Parse( lin1[pn + 1] );
                                args.Add( pi );
                                break;
                            case 'l':
                                Int32 pl = Int32.Parse( lin1[pn + 1] );
                                args.Add( pl );
                                break;
                            case 'b':
                                bool pb = bool.Parse( lin1[pn + 1] );
                                args.Add( pb );
                                break;
                            case 's':
                                args.Add(lin1[pn+1]) ;
                                break;
                            case 'c':
                                sendBuffer[byteCount++] = (byte)lin1[pn + 1][0];
                                break;
                            default:
                                break;

                        }

                    }
                    if (controller.commandStructure[commandNumber].returns == "")
                        rc.Write_CRC( rc.m_address, (byte)commandNumber, args );
                    else
                        rc.ReadCmd( rc.m_address, (byte)commandNumber, ref args );

                }
    
            }
        }


    }

}